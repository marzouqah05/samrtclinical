// ── A2. POST /api/automation/patient ─────────────────────────────────

/// <summary>
/// Registers a new patient record via the Telegram bot flow.
/// Creates the patient with a generated PatientNumber and returns the new PatientId.
/// Telegram registrations do not require a real National ID.
/// If the Telegram flow sends the default placeholder 0000000000,
/// a unique internal placeholder is generated to satisfy the database unique constraint.
/// </summary>
[HttpPost("patient")]
[ProducesResponseType(typeof(CreatePatientResult), 201)]
[ProducesResponseType(400)]
[ProducesResponseType(401)]
[ProducesResponseType(409)]
public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request)
{
    if (!IsAuthorized())
        return Unauthorized(new
        {
            error = "Invalid or missing X-Automation-Key header."
        });

    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    // ---------------------------------------------
    // Clean input
    // ---------------------------------------------

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
    // Duplicate phone guard
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
    // National ID handling
    // ---------------------------------------------

    var requestedNationalId = request.NationalId?.Trim();

    string finalNationalId;

    // Telegram currently sends 0000000000 as a placeholder.
    // Because Patients.NationalId has a UNIQUE constraint,
    // we must NOT store the same placeholder for every patient.
    if (string.IsNullOrWhiteSpace(requestedNationalId) ||
        requestedNationalId == "0000000000")
    {
        if (!string.IsNullOrWhiteSpace(cleanChatId))
        {
            // Telegram Chat ID is unique per Telegram conversation,
            // so it provides a stable internal placeholder.
            finalNationalId = $"TG-{cleanChatId}";
        }
        else
        {
            // Fallback in case TelegramChatId was not supplied.
            finalNationalId = $"TG-{Guid.NewGuid():N}";
        }
    }
    else
    {
        // A real National ID was supplied.
        finalNationalId = requestedNationalId;

        // Check whether this real National ID already exists.
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
    // Final safety check
    // ---------------------------------------------

    var generatedNationalIdExists = await _context.Patients
        .IgnoreQueryFilters()
        .AnyAsync(p => p.NationalId == finalNationalId);

    if (generatedNationalIdExists)
    {
        // Extremely unlikely, but prevents a database unique-key crash.
        finalNationalId = $"TG-{Guid.NewGuid():N}";
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

        // Real National ID if supplied.
        // Otherwise a unique internal Telegram placeholder.
        NationalId = finalNationalId,

        // Placeholder DOB; patient can update it later through portal.
        DOB = new DateTime(1990, 1, 1),

        TelegramChatId = string.IsNullOrWhiteSpace(cleanChatId)
            ? null
            : cleanChatId
    };

    _context.Patients.Add(patient);

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
