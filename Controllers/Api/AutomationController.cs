// ── A2. POST /api/automation/patient ─────────────────────────────────

/// <summary>
/// Registers a new patient record via the Telegram bot flow.
/// Telegram patients only need name and phone.
/// When the Telegram flow sends 0000000000 as a placeholder,
/// the backend generates a unique internal NationalId value.
/// </summary>
[HttpPost("patient")]
[ProducesResponseType(typeof(CreatePatientResult), 201)]
[ProducesResponseType(400)]
[ProducesResponseType(401)]
[ProducesResponseType(409)]
public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request)
{
    // ---------------------------------------------
    // Authorization
    // ---------------------------------------------

    if (!IsAuthorized())
    {
        return Unauthorized(new
        {
            error = "Invalid or missing X-Automation-Key header."
        });
    }

    // ---------------------------------------------
    // Validate request
    // ---------------------------------------------

    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    if (request == null)
    {
        return BadRequest(new
        {
            error = "Request body cannot be empty."
        });
    }

    var cleanPhone = request.Phone?.Trim() ?? string.Empty;
    var cleanName = request.FullName?.Trim() ?? string.Empty;
    var cleanChatId = request.TelegramChatId?.Trim();

    if (string.IsNullOrWhiteSpace(cleanPhone))
    {
        return BadRequest(new
        {
            error = "Phone number is required."
        });
    }

    if (string.IsNullOrWhiteSpace(cleanName))
    {
        return BadRequest(new
        {
            error = "Patient name is required."
        });
    }

    // ---------------------------------------------
    // Check duplicate phone
    // ---------------------------------------------

    var existingPatient = await _context.Patients
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(p => p.PhoneNumber == cleanPhone);

    if (existingPatient != null)
    {
        return Conflict(new CreatePatientResult
        {
            Success = false,
            PatientId = existingPatient.PatientId,
            Message = $"A patient with phone {cleanPhone} already exists. Use the lookup endpoint instead."
        });
    }

    // ---------------------------------------------
    // National ID
    // ---------------------------------------------

    var requestedNationalId = request.NationalId?.Trim();

    string finalNationalId;

    // Telegram sends 0000000000 as a placeholder.
    // Because NationalId is UNIQUE in PostgreSQL,
    // we generate a unique internal value instead.
    if (string.IsNullOrWhiteSpace(requestedNationalId) ||
        requestedNationalId == "0000000000")
    {
        // Generate a unique 10-character internal value.
        // This keeps the value short in case the database
        // column has a maximum length.
        do
        {
            finalNationalId = Guid.NewGuid()
                .ToString("N")
                .Substring(0, 10)
                .ToUpperInvariant();
        }
        while (await _context.Patients
            .IgnoreQueryFilters()
            .AnyAsync(p => p.NationalId == finalNationalId));
    }
    else
    {
        // A real National ID was supplied.
        finalNationalId = requestedNationalId;

        // Check duplicate National ID.
        var nationalIdExists = await _context.Patients
            .IgnoreQueryFilters()
            .AnyAsync(p => p.NationalId == finalNationalId);

        if (nationalIdExists)
        {
            return Conflict(new CreatePatientResult
            {
                Success = false,
                Message = "A patient with this National ID already exists."
            });
        }
    }

    // ---------------------------------------------
    // Logging
    // ---------------------------------------------

    _logger.LogInformation(
        "[Automation] Registering new patient — name={Name}, phone={Phone}, telegramChatId={ChatId}",
        cleanName,
        cleanPhone,
        cleanChatId
    );

    // ---------------------------------------------
    // Create patient
    // ---------------------------------------------

    var patient = new Patient
    {
        PatientName = cleanName,
        PhoneNumber = cleanPhone,

        // Real National ID if provided.
        // Otherwise a unique internal value for Telegram.
        NationalId = finalNationalId,

        // Temporary DOB until patient updates profile.
        DOB = new DateTime(1990, 1, 1),

        TelegramChatId = string.IsNullOrWhiteSpace(cleanChatId)
            ? null
            : cleanChatId
    };

    _context.Patients.Add(patient);

    // ---------------------------------------------
    // Save
    // ---------------------------------------------

    await _context.SaveChangesAsync();

    _logger.LogInformation(
        "[Automation] Patient {Id} registered successfully via Telegram bot.",
        patient.PatientId
    );

    // ---------------------------------------------
    // Response
    // ---------------------------------------------

    return CreatedAtAction(
        nameof(GetPatientByPhone),
        new { phone = cleanPhone },
        new CreatePatientResult
        {
            Success = true,
            PatientId = patient.PatientId,
            Message = "Patient registered successfully."
        }
    );
}
