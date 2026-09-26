using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers.Api
{
    /// <summary>
    /// Secure Automation Integration API for the Telegram Bot / n8n bridge.
    /// All endpoints are protected by the custom X-Automation-Key header.
    /// </summary>
    [Route("api/automation")]
    [ApiController]
    public class AutomationController : ControllerBase
    {
        private readonly ClinicDbContext _context;
        private readonly ISettingsService _settings;
        private readonly IConfiguration _config;
        private readonly ILogger<AutomationController> _logger;

        public AutomationController(
            ClinicDbContext context,
            ISettingsService settings,
            IConfiguration config,
            ILogger<AutomationController> logger)
        {
            _context = context;
            _settings = settings;
            _config = config;
            _logger = logger;
        }

        // ── Security Guard ────────────────────────────────────────────────────

        private bool IsAuthorized()
        {
            var configuredKey = _config["Automation:ApiKey"]
                             ?? _config["AutomationSettings:ApiKey"];

            if (string.IsNullOrWhiteSpace(configuredKey))
                return false;

            if (Request.Headers.TryGetValue("X-Automation-Key", out var providedKey))
            {
                return !string.IsNullOrWhiteSpace(providedKey)
                       && string.Equals(
                           providedKey.ToString().Trim(),
                           configuredKey.Trim(),
                           StringComparison.Ordinal);
            }

            return false;
        }

        // ── A1. GET /api/automation/patient/{phone} ───────────────────────────

        [HttpGet("patient/{phone}")]
        [ProducesResponseType(typeof(PatientLookupResult), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetPatientByPhone(string phone)
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            var cleanPhone = phone?.Trim() ?? string.Empty;

            _logger.LogInformation(
                "[Automation] Patient lookup — phone={Phone}",
                cleanPhone);

            var patient = await _context.Patients
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PhoneNumber == cleanPhone);

            if (patient == null)
                return Ok(new PatientLookupResult
                {
                    Exists = false
                });

            var lastAppointment = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(a => a.Doctor)
                .Where(a =>
                    a.PatientId == patient.PatientId &&
                    a.Status != "Cancelled")
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .FirstOrDefaultAsync();

            return Ok(new PatientLookupResult
            {
                Exists = true,
                PatientId = patient.PatientId,
                FullName = patient.PatientName,
                TelegramChatId = patient.TelegramChatId,
                LastDoctorId = lastAppointment?.DoctorId,
                LastDoctorName = lastAppointment?.Doctor?.DoctorName,
            });
        }

        // ── A2. POST /api/automation/patient ─────────────────────────────────

        /// <summary>
        /// Registers a new patient through the Telegram bot.
        /// Telegram only requires name and phone.
        /// If n8n sends 0000000000 as the placeholder National ID,
        /// the backend generates a unique internal value because
        /// Patients.NationalId has a UNIQUE constraint.
        /// </summary>
        [HttpPost("patient")]
        [ProducesResponseType(typeof(CreatePatientResult), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> CreatePatient(
            [FromBody] CreatePatientRequest request)
        {
            if (!IsAuthorized())
            {
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });
            }

            if (request == null)
            {
                return BadRequest(new
                {
                    error = "Request body cannot be empty."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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
                    Message =
                        $"A patient with phone {cleanPhone} already exists. Use the lookup endpoint instead."
                });
            }

            // ---------------------------------------------
            // National ID handling
            // ---------------------------------------------

            var requestedNationalId = request.NationalId?.Trim();

            string finalNationalId;

            // Telegram sends 0000000000 as a placeholder.
            // Because NationalId is UNIQUE, we generate a unique
            // internal 10-character value instead.
            if (string.IsNullOrWhiteSpace(requestedNationalId) ||
                requestedNationalId == "0000000000")
            {
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
                // Real National ID supplied.
                finalNationalId = requestedNationalId;

                var nationalIdExists = await _context.Patients
                    .IgnoreQueryFilters()
                    .AnyAsync(p => p.NationalId == finalNationalId);

                if (nationalIdExists)
                {
                    return Conflict(new CreatePatientResult
                    {
                        Success = false,
                        Message =
                            "A patient with this National ID already exists."
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
                cleanChatId);

            // ---------------------------------------------
            // Create patient
            // ---------------------------------------------

            var patient = new Patient
            {
                PatientName = cleanName,
                PhoneNumber = cleanPhone,
                NationalId = finalNationalId,
                DOB = new DateTime(1990, 1, 1),

                TelegramChatId =
                    string.IsNullOrWhiteSpace(cleanChatId)
                        ? null
                        : cleanChatId
            };

            _context.Patients.Add(patient);

            // ---------------------------------------------
            // Save patient
            // ---------------------------------------------

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Automation] Patient {Id} registered successfully via Telegram bot.",
                patient.PatientId);

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
                });
        }

        // ── B. GET /api/automation/doctors ───────────────────────────────────

        [HttpGet("doctors")]
        [ProducesResponseType(typeof(List<DoctorListDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetDoctors()
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            _logger.LogInformation("[Automation] GetDoctors called.");

            var cfg = await _settings.GetSettingsAsync();

            var doctors = await _context.Doctors
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(d => d.Department)
                .OrderBy(d => d.DepartmentId)
                .ThenBy(d => d.DoctorName)
                .Select(d => new DoctorListDto
                {
                    DoctorId = d.DoctorId,
                    DoctorName = d.DoctorName,
                    Specialization = d.Specialization,
                    ConsultationFee = d.ConsultationFee,
                    DepartmentName =
                        d.Department != null
                            ? d.Department.DepartmentName
                            : string.Empty,
                    TelegramChatId = d.TelegramChatId,
                    WorkingDays = cfg.WorkingDays,
                    OpeningTime = cfg.WorkingHoursStart,
                    ClosingTime = cfg.WorkingHoursEnd,
                    SlotDurationMinutes = cfg.DefaultSlotDurationMinutes,
                    // Clinic identity — same for all doctors
                    ClinicName  = cfg.ClinicName,
                    ClinicPhone = cfg.ClinicPhone,
                })
                .ToListAsync();

            return Ok(doctors);
        }

        // ── C. GET /api/automation/available-slots ────────────────────────────

        [HttpGet("available-slots")]
        [ProducesResponseType(typeof(AvailableSlotsResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetAvailableSlots(
            [FromQuery] int doctorId,
            [FromQuery] string date)
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            if (doctorId <= 0)
                return BadRequest(new
                {
                    error = "doctorId must be a positive integer."
                });

            if (!DateTime.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                return BadRequest(new
                {
                    error = "Invalid date format. Expected yyyy-MM-dd."
                });
            }

            var doctorExists = await _context.Doctors
                .IgnoreQueryFilters()
                .AnyAsync(d => d.DoctorId == doctorId);

            if (!doctorExists)
            {
                return NotFound(new
                {
                    error = $"Doctor with ID {doctorId} not found."
                });
            }

            _logger.LogInformation(
                "[Automation] AvailableSlots — doctorId={DoctorId}, date={Date}",
                doctorId,
                date);

            var cfg = await _settings.GetSettingsAsync();

            // ── Working-day validation ────────────────────────────────────────
            var configuredWorkingDays = cfg.WorkingDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool isWorkingDay = configuredWorkingDays.Contains(
                parsedDate.DayOfWeek.ToString());

            if (!isWorkingDay)
            {
                _logger.LogInformation(
                    "[Automation] AvailableSlots — {Date} is {DayOfWeek}, not a working day.",
                    date, parsedDate.DayOfWeek);

                return Ok(new AvailableSlotsResult
                {
                    DoctorId       = doctorId,
                    Date           = date,
                    IsWorkingDay   = false,
                    Slots          = new List<string>(),
                    TotalAvailable = 0,
                    IsFull         = false,
                    NextAvailableDate = null,
                    ClinicName     = cfg.ClinicName,
                    ClinicPhone    = cfg.ClinicPhone,
                });
            }

            // ── Shift boundaries ─────────────────────────────────────────────
            if (!TimeSpan.TryParse(cfg.WorkingHoursStart, out var shiftStart))
                shiftStart = TimeSpan.FromHours(8);

            if (!TimeSpan.TryParse(cfg.WorkingHoursEnd, out var shiftEnd))
                shiftEnd = TimeSpan.FromHours(20);

            int slotMinutes =
                cfg.DefaultSlotDurationMinutes > 0
                    ? cfg.DefaultSlotDurationMinutes
                    : 30;

            // Rule: first slot = OpeningTime + 15 minutes
            var firstSlot = shiftStart + TimeSpan.FromMinutes(15);

            // Rule: last slot must finish >= 15 min before ClosingTime
            //   lastSlotStart = ClosingTime - 15 - slotDuration
            var lastSlotCeiling = shiftEnd
                - TimeSpan.FromMinutes(15)
                - TimeSpan.FromMinutes(slotMinutes);

            var allSlots = new List<string>();

            for (var t = firstSlot; t <= lastSlotCeiling; t += TimeSpan.FromMinutes(slotMinutes))
            {
                allSlots.Add(t.ToString(@"hh\:mm"));
            }

            var dateOnly = parsedDate.Date;

            var bookedTimes = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a =>
                    a.DoctorId == doctorId &&
                    a.AppointmentDate.Date == dateOnly &&
                    a.Status != "Cancelled")
                .Select(a => a.AppointmentTime)
                .ToListAsync();

            var bookedSet = bookedTimes
                .Select(t => t.ToString(@"hh\:mm"))
                .ToHashSet();

            var freeSlots = allSlots
                .Where(s => !bookedSet.Contains(s))
                .ToList();

            string? nextAvailableDate = null;

            if (freeSlots.Count == 0)
            {
                for (int offset = 1; offset <= 30; offset++)
                {
                    var candidate = parsedDate.AddDays(offset);

                    if (!configuredWorkingDays.Contains(
                            candidate.DayOfWeek.ToString()))
                        continue;

                    var candidateBooked =
                        await _context.Appointments
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .CountAsync(a =>
                                a.DoctorId == doctorId &&
                                a.AppointmentDate.Date == candidate.Date &&
                                a.Status != "Cancelled");

                    if (candidateBooked < allSlots.Count)
                    {
                        nextAvailableDate = candidate.ToString("yyyy-MM-dd");
                        break;
                    }
                }
            }

            return Ok(new AvailableSlotsResult
            {
                DoctorId          = doctorId,
                Date              = date,
                IsWorkingDay      = true,
                Slots             = freeSlots,
                TotalAvailable    = freeSlots.Count,
                IsFull            = freeSlots.Count == 0,
                NextAvailableDate = nextAvailableDate,
                ClinicName        = cfg.ClinicName,
                ClinicPhone       = cfg.ClinicPhone,
            });
        }

        // ── D. POST /api/automation/book ──────────────────────────────────────

        [HttpPost("book")]
        [ProducesResponseType(typeof(BookAppointmentResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> BookAppointment(
            [FromBody] BookAppointmentRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!DateTime.TryParseExact(
                    request.Date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var appointmentDate))
            {
                return BadRequest(
                    new BookAppointmentResult
                    {
                        Success = false,
                        Message =
                            "Invalid date format. Expected yyyy-MM-dd."
                    });
            }

            if (!TimeSpan.TryParse(
                    request.Time,
                    out var timeSlot))
            {
                return BadRequest(
                    new BookAppointmentResult
                    {
                        Success = false,
                        Message =
                            "Invalid time format. Expected HH:mm."
                    });
            }

            var patient = await _context.Patients
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    p => p.PatientId == request.PatientId);

            if (patient == null)
            {
                return NotFound(
                    new BookAppointmentResult
                    {
                        Success = false,
                        Message =
                            $"Patient ID {request.PatientId} not found."
                    });
            }

            var doctor = await _context.Doctors
                .IgnoreQueryFilters()
                .Include(d => d.Department)
                .FirstOrDefaultAsync(
                    d => d.DoctorId == request.DoctorId);

            if (doctor == null)
            {
                return NotFound(
                    new BookAppointmentResult
                    {
                        Success = false,
                        Message =
                            $"Doctor ID {request.DoctorId} not found."
                    });
            }

            bool slotTaken = await _context.Appointments
                .IgnoreQueryFilters()
                .AnyAsync(a =>
                    a.DoctorId == request.DoctorId &&
                    a.AppointmentDate.Date == appointmentDate.Date &&
                    a.AppointmentTime == timeSlot &&
                    a.Status != "Cancelled");

            if (slotTaken)
            {
                return Conflict(
                    new BookAppointmentResult
                    {
                        Success = false,
                        Message =
                            $"Slot {request.Time} on {request.Date} is already taken. Please choose another slot."
                    });
            }

            var cfg = await _settings.GetSettingsAsync();

            // ── Working-day check ────────────────────────────────────────
            var bookWorkingDays = cfg.WorkingDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!bookWorkingDays.Contains(appointmentDate.DayOfWeek.ToString()))
                return BadRequest(new BookAppointmentResult
                {
                    Success = false,
                    Message = $"{appointmentDate:dddd} ({request.Date}) is not a working day. The clinic is open on: {cfg.WorkingDays}."
                });

            // ── Hours check ───────────────────────────────────────────
            if (TimeSpan.TryParse(cfg.WorkingHoursStart, out var bookShiftStart) &&
                TimeSpan.TryParse(cfg.WorkingHoursEnd, out var bookShiftEnd))
            {
                int bookSlotMin = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 30;
                var bookFirstSlot   = bookShiftStart + TimeSpan.FromMinutes(15);
                var bookLastCeiling = bookShiftEnd - TimeSpan.FromMinutes(15) - TimeSpan.FromMinutes(bookSlotMin);

                if (timeSlot < bookFirstSlot || timeSlot > bookLastCeiling)
                    return BadRequest(new BookAppointmentResult
                    {
                        Success = false,
                        Message = $"Time {request.Time} is outside clinic hours. Valid range: {bookFirstSlot:hh\\:mm} – {bookLastCeiling:hh\\:mm}."
                    });
            }

            string status =
                cfg.AutoConfirmAppointments
                    ? "Confirmed"
                    : "Confirmed";

            string channel = request.Channel ?? "Telegram";

            var targetClinicId =
                doctor.ClinicId ?? patient.ClinicId;

            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                DoctorId = doctor.DoctorId,
                DepartmentId = doctor.DepartmentId,
                AppointmentDate = appointmentDate.Date,
                AppointmentTime = timeSlot,
                Status = status,
                Notes =
                    string.IsNullOrWhiteSpace(request.Notes)
                        ? $"Booked via {channel}"
                        : $"[{channel}] {request.Notes}",
                ClinicId = targetClinicId,
            };

            _context.Appointments.Add(appointment);

            await _context.SaveChangesAsync();

            try
            {
                var notification = new Notification
                {
                    ClinicId = targetClinicId,
                    AppointmentId = appointment.AppointmentId,
                    PatientId = patient.PatientId,
                    PatientName = patient.PatientName,
                    DoctorName = doctor.DoctorName,
                    DepartmentName =
                        doctor.Department?.DepartmentName
                        ?? "العيادة",
                    Title = "طلب حجز جديد",
                    Message =
                        $"قام المريض {patient.PatientName} بحجز موعد مع د. {doctor.DoctorName} بتاريخ {request.Date} الساعة {request.Time}.",
                    TargetRole = "Receptionist",
                    Type = "AppointmentCreated",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[Automation] Failed to add Notification entry.");
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                UserName = "Telegram_Bot",
                Action = "CREATE",
                EntityName = "Appointment",
                EntityId = appointment.AppointmentId.ToString(),
                Timestamp = DateTime.UtcNow,
                Details =
                    $"Automated booking via {channel} | Patient: {patient.PatientName} (ID:{patient.PatientId}) | Doctor: {doctor.DoctorName} (ID:{doctor.DoctorId}) | Date: {request.Date} {request.Time} | Status: {status}",
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Automation] Appointment {ApptId} booked via {Channel} — Patient {PatientId}, Doctor {DoctorId}, {Date} {Time}.",
                appointment.AppointmentId,
                channel,
                patient.PatientId,
                doctor.DoctorId,
                request.Date,
                request.Time);

            return Ok(new BookAppointmentResult
            {
                Success = true,
                Message = "Appointment confirmed successfully.",
                AppointmentId = appointment.AppointmentId,
                DoctorName = doctor.DoctorName,
                DoctorTelegramChatId = doctor.TelegramChatId,
                PatientName = patient.PatientName,
                AppointmentDate = request.Date,
                AppointmentTime = request.Time,
                Status = status,
            });
        }

        // ── E. GET /api/automation/weekly-schedule/{doctorId} ─────────────────

        [HttpGet("weekly-schedule/{doctorId:int}")]
        [ProducesResponseType(typeof(WeeklyScheduleResult), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetWeeklySchedule(
            int doctorId)
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            var doctor = await _context.Doctors
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.DoctorId == doctorId);

            if (doctor == null)
            {
                return NotFound(new
                {
                    error = $"Doctor ID {doctorId} not found."
                });
            }

            _logger.LogInformation(
                "[Automation] WeeklySchedule — doctorId={DoctorId}",
                doctorId);

            var today = DateTime.UtcNow.Date;
            var weekEnd = today.AddDays(7);

            var appointments = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(a => a.Patient)
                .Where(a =>
                    a.DoctorId == doctorId &&
                    a.AppointmentDate.Date >= today &&
                    a.AppointmentDate.Date <= weekEnd &&
                    a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .ToListAsync();

            var days = appointments
                .GroupBy(a => a.AppointmentDate.Date)
                .Select(g => new WeeklyScheduleDayDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    DayName = g.Key.ToString(
                        "dddd",
                        CultureInfo.InvariantCulture),
                    Appointments = g.Select(
                        a => new WeeklyScheduleEntryDto
                        {
                            AppointmentId = a.AppointmentId,
                            Time = a.AppointmentTime
                                .ToString(@"hh\:mm"),
                            PatientName =
                                a.Patient?.PatientName
                                ?? "Unknown",
                            TelegramChatId =
                                a.Patient?.TelegramChatId,
                            VisitType =
                                ExtractVisitType(a.Notes),
                            Status = a.Status,
                        }).ToList()
                })
                .ToList();

            return Ok(new WeeklyScheduleResult
            {
                DoctorId = doctor.DoctorId,
                DoctorName = doctor.DoctorName,
                WeekStart = today.ToString("yyyy-MM-dd"),
                WeekEnd = weekEnd.ToString("yyyy-MM-dd"),
                TotalAppointments = appointments.Count,
                Days = days,
            });
        }

        // ── F1. GET /api/automation/due-reminders ─────────────────────────────

        [HttpGet("due-reminders")]
        [ProducesResponseType(typeof(List<DueReminderDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetDueReminders()
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            var tomorrow = DateTime.UtcNow.Date.AddDays(1);

            _logger.LogInformation(
                "[Automation] GetDueReminders called for date: {Date}",
                tomorrow.ToString("yyyy-MM-dd"));

            var appointments = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Department)
                .Where(a =>
                    a.AppointmentDate.Date == tomorrow &&
                    (a.Status == "Confirmed" ||
                     a.Status == "Pending") &&
                    !a.IsReminderSent)
                .OrderBy(a => a.AppointmentTime)
                .ToListAsync();

            var result = appointments
                .Select(a => new DueReminderDto
                {
                    AppointmentId = a.AppointmentId,
                    PatientName =
                        a.Patient?.PatientName ?? "Unknown",
                    TelegramChatId =
                        a.Patient?.TelegramChatId,
                    DoctorName =
                        a.Doctor?.DoctorName ?? "Unknown",
                    DepartmentName =
                        a.Department?.DepartmentName
                        ?? a.Doctor?.Department?.DepartmentName
                        ?? string.Empty,
                    AppointmentDate =
                        a.AppointmentDate.ToString("yyyy-MM-dd"),
                    AppointmentTime =
                        a.AppointmentTime.ToString(@"hh\:mm"),
                })
                .ToList();

            return Ok(result);
        }

        // ── F2. GET /api/automation/due-followups ─────────────────────────────

        [HttpGet("due-followups")]
        [ProducesResponseType(typeof(List<DueFollowupDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetDueFollowups()
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            _logger.LogInformation(
                "[Automation] GetDueFollowups called for date: {Date}",
                yesterday.ToString("yyyy-MM-dd"));

            var appointments = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a =>
                    a.AppointmentDate.Date == yesterday &&
                    a.Status == "Completed" &&
                    (!a.IsFollowUpSent ||
                     a.FollowUpSentAt == null))
                .OrderBy(a => a.AppointmentTime)
                .ToListAsync();

            var result = appointments
                .Select(a => new DueFollowupDto
                {
                    AppointmentId = a.AppointmentId,
                    PatientName =
                        a.Patient?.PatientName ?? "Unknown",
                    TelegramChatId =
                        a.Patient?.TelegramChatId,
                    DoctorName =
                        a.Doctor?.DoctorName ?? "Unknown",
                    DoctorPhone =
                        a.Doctor?.DoctorPhone,
                })
                .ToList();

            return Ok(result);
        }

        // ── F3. POST /api/automation/update-status ────────────────────────────

        [HttpPost("update-status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateStatus(
            [FromBody] UpdateStatusRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new
                {
                    error = "Invalid or missing X-Automation-Key header."
                });

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Action))
            {
                return BadRequest(new
                {
                    error =
                        "Missing or invalid payload: 'action' is required."
                });
            }

            int appointmentId = ParseId(request.AppointmentId);

            if (appointmentId <= 0)
            {
                return BadRequest(new
                {
                    error = "Invalid or missing 'appointmentId'."
                });
            }

            var appointment = await _context.Appointments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    a => a.AppointmentId == appointmentId);

            if (appointment == null)
            {
                return NotFound(new
                {
                    error =
                        $"Appointment with ID {appointmentId} not found."
                });
            }

            var action = request.Action.Trim();

            _logger.LogInformation(
                "[Automation] UpdateStatus called — appointmentId={Id}, action={Action}",
                appointmentId,
                action);

            if (string.Equals(
                    action,
                    "ReminderSent",
                    StringComparison.OrdinalIgnoreCase))
            {
                appointment.IsReminderSent = true;
            }
            else if (string.Equals(
                         action,
                         "FollowUpSent",
                         StringComparison.OrdinalIgnoreCase))
            {
                appointment.IsFollowUpSent = true;
                appointment.FollowUpSentAt = DateTime.UtcNow;
            }
            else if (string.Equals(
                         action,
                         "Confirmed",
                         StringComparison.OrdinalIgnoreCase))
            {
                appointment.Status = "Confirmed";
            }
            else if (string.Equals(
                         action,
                         "Cancelled",
                         StringComparison.OrdinalIgnoreCase))
            {
                appointment.Status = "Cancelled";
            }
            else
            {
                return BadRequest(new
                {
                    error =
                        $"Unsupported action '{request.Action}'. Allowed actions: ReminderSent, FollowUpSent, Confirmed, Cancelled."
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                appointmentId = appointment.AppointmentId,
                status = appointment.Status,
                isReminderSent = appointment.IsReminderSent,
                isFollowUpSent = appointment.IsFollowUpSent,
                followUpSentAt = appointment.FollowUpSentAt,
                message =
                    $"Action '{action}' processed successfully."
            });
        }

        // ── F4. POST /api/automation/book-slot ────────────────────────────────

        [HttpPost("book-slot")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> BookSlot(
            [FromBody] BookSlotRequest request)
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

            if (request == null)
            {
                return BadRequest(new
                {
                    error = "Request payload cannot be empty."
                });
            }

            if (string.IsNullOrWhiteSpace(request.PatientTelegramChatId))
            {
                return BadRequest(new
                {
                    error = "patientTelegramChatId is required."
                });
            }

            int doctorId = ParseId(request.DoctorId);

            if (doctorId <= 0)
            {
                return BadRequest(new
                {
                    error = "Invalid or missing doctorId."
                });
            }

            // ---------------------------------------------
            // Parse date
            // ---------------------------------------------

            if (string.IsNullOrWhiteSpace(request.Date) ||
                !DateTime.TryParse(
                    request.Date,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var apptDate))
            {
                return BadRequest(new
                {
                    error = "Invalid or missing 'date'. Expected format: yyyy-MM-dd or ISO 8601."
                });
            }

            // ---------------------------------------------
            // Parse time
            // ---------------------------------------------

            if (string.IsNullOrWhiteSpace(request.Time) ||
                !TimeSpan.TryParse(
                    request.Time,
                    out var apptTime))
            {
                return BadRequest(new
                {
                    error = "Invalid or missing 'time'. Expected format: HH:mm."
                });
            }

            // ---------------------------------------------
            // Find the correct Telegram patient
            // ---------------------------------------------

            var cleanChatId = request.PatientTelegramChatId.Trim();

            /*
             * IMPORTANT:
             *
             * One Telegram account can register more than one patient.
             * Therefore TelegramChatId alone is not unique.
             *
             * The previous code used FirstOrDefaultAsync(), which could
             * return the old patient (for example "sulaiman").
             *
             * We now explicitly select the newest patient registered
             * for this Telegram chat by descending PatientId.
             */

            var patient = await _context.Patients
                .IgnoreQueryFilters()
                .Where(p => p.TelegramChatId == cleanChatId)
                .OrderByDescending(p => p.PatientId)
                .FirstOrDefaultAsync();

            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        $"No patient found with TelegramChatId: {cleanChatId}."
                });
            }

            // ---------------------------------------------
            // Validate doctor
            // ---------------------------------------------

            var doctor = await _context.Doctors
                .IgnoreQueryFilters()
                .Include(d => d.Department)
                .FirstOrDefaultAsync(
                    d => d.DoctorId == doctorId);

            if (doctor == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        $"Doctor with ID {doctorId} not found."
                });
            }

            // ---------------------------------------------
            // Working-day and hours checks
            // ---------------------------------------------

            var slotCfg = await _settings.GetSettingsAsync();

            var slotWorkingDays = slotCfg.WorkingDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!slotWorkingDays.Contains(apptDate.DayOfWeek.ToString()))
                return BadRequest(new
                {
                    success = false,
                    message = $"{apptDate:dddd} ({apptDate:yyyy-MM-dd}) is not a working day. The clinic is open on: {slotCfg.WorkingDays}."
                });

            if (TimeSpan.TryParse(slotCfg.WorkingHoursStart, out var slotShiftStart) &&
                TimeSpan.TryParse(slotCfg.WorkingHoursEnd, out var slotShiftEnd))
            {
                int slotMin = slotCfg.DefaultSlotDurationMinutes > 0 ? slotCfg.DefaultSlotDurationMinutes : 30;
                var slotFirstSlot   = slotShiftStart + TimeSpan.FromMinutes(15);
                var slotLastCeiling = slotShiftEnd - TimeSpan.FromMinutes(15) - TimeSpan.FromMinutes(slotMin);

                if (apptTime < slotFirstSlot || apptTime > slotLastCeiling)
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Time {apptTime:hh\\:mm} is outside clinic hours. Valid range: {slotFirstSlot:hh\\:mm} – {slotLastCeiling:hh\\:mm}."
                    });
            }

            // ---------------------------------------------
            // Double-booking guard
            // ---------------------------------------------

            bool slotTaken = await _context.Appointments
                .IgnoreQueryFilters()
                .AnyAsync(a =>
                    a.DoctorId == doctor.DoctorId &&
                    a.AppointmentDate.Date == apptDate.Date &&
                    a.AppointmentTime == apptTime &&
                    a.Status != "Cancelled");

            if (slotTaken)
            {
                return Conflict(new
                {
                    success = false,
                    message =
                        $"Slot {apptTime:hh\\:mm} on {apptDate:yyyy-MM-dd} is already booked for Dr. {doctor.DoctorName}."
                });
            }

            // ---------------------------------------------
            // Clinic
            // ---------------------------------------------

            var targetClinicId =
                doctor.ClinicId ?? patient.ClinicId;

            // ---------------------------------------------
            // Create appointment
            // ---------------------------------------------

            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                DoctorId = doctor.DoctorId,
                DepartmentId = doctor.DepartmentId,
                AppointmentDate = apptDate.Date,
                AppointmentTime = apptTime,
                Status = "Pending",
                Notes =
                    string.IsNullOrWhiteSpace(request.Notes)
                        ? "[Telegram Automation]"
                        : $"[Telegram Automation] {request.Notes}",
                ClinicId = targetClinicId,
            };

            _context.Appointments.Add(appointment);

            await _context.SaveChangesAsync();

            // ---------------------------------------------
            // Reception notification
            // ---------------------------------------------

            try
            {
                var notification = new Notification
                {
                    ClinicId = targetClinicId,
                    AppointmentId = appointment.AppointmentId,
                    PatientId = patient.PatientId,
                    PatientName = patient.PatientName,
                    DoctorName = doctor.DoctorName,
                    DepartmentName =
                        doctor.Department?.DepartmentName
                        ?? "العيادة",
                    Title = "طلب حجز جديد (Telegram)",
                    Message =
                        $"قام المريض {patient.PatientName} بحجز موعد مع د. {doctor.DoctorName} بتاريخ {apptDate:yyyy-MM-dd} الساعة {apptTime:hh\\:mm}.",
                    TargetRole = "Receptionist",
                    Type = "TelegramBooking",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[Automation] Failed to dispatch Notification entity.");
            }

            // ---------------------------------------------
            // Audit log
            // ---------------------------------------------

            _context.AuditLogs.Add(new AuditLog
            {
                UserName = "Telegram_Automation",
                Action = "CREATE",
                EntityName = "Appointment",
                EntityId = appointment.AppointmentId.ToString(),
                Timestamp = DateTime.UtcNow,
                Details =
                    $"Automated book-slot via Telegram | " +
                    $"ChatId: {cleanChatId} | " +
                    $"Patient: {patient.PatientName} (ID:{patient.PatientId}) | " +
                    $"Doctor: Dr. {doctor.DoctorName} (ID:{doctor.DoctorId}) | " +
                    $"Slot: {apptDate:yyyy-MM-dd} {apptTime:hh\\:mm}",
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Automation] Telegram booking created successfully. " +
                "AppointmentId={AppointmentId}, PatientId={PatientId}, " +
                "PatientName={PatientName}, ChatId={ChatId}, " +
                "DoctorId={DoctorId}, Date={Date}, Time={Time}",
                appointment.AppointmentId,
                patient.PatientId,
                patient.PatientName,
                cleanChatId,
                doctor.DoctorId,
                apptDate.ToString("yyyy-MM-dd"),
                apptTime.ToString(@"hh\:mm")
            );

            // ---------------------------------------------
            // Response
            // ---------------------------------------------

            return Ok(new
            {
                success = true,
                appointmentId = appointment.AppointmentId,
                patientId = patient.PatientId,
                patientName = patient.PatientName,
                patientTelegramChatId = patient.TelegramChatId,
                doctorId = doctor.DoctorId,
                doctorName = doctor.DoctorName,
                departmentName =
                    doctor.Department?.DepartmentName
                    ?? string.Empty,
                appointmentDate =
                    appointment.AppointmentDate
                        .ToString("yyyy-MM-dd"),
                appointmentTime =
                    appointment.AppointmentTime
                        .ToString(@"hh\:mm"),
                status = appointment.Status,
                message =
                    "Appointment slot booked successfully and reception notified."
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ParseId(
            System.Text.Json.JsonElement element)
        {
            if (element.ValueKind ==
                    System.Text.Json.JsonValueKind.Number &&
                element.TryGetInt32(out int num))
            {
                return num;
            }

            if (element.ValueKind ==
                    System.Text.Json.JsonValueKind.String)
            {
                var str = element.GetString();

                if (int.TryParse(str, out int parsed))
                    return parsed;
            }

            return 0;
        }

        private static string? ExtractVisitType(
            string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return null;

            var text = notes.Contains(']')
                ? notes[(notes.IndexOf(']') + 1)..].Trim()
                : notes.Trim();

            return string.IsNullOrWhiteSpace(text)
                ? null
                : text;
        }
    }
}
