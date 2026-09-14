using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers.Api
{
    /// <summary>
    /// Secure Automation Integration API for the Telegram Bot / n8n bridge.
    /// All endpoints are protected by the custom <c>X-Automation-Key</c> header,
    /// validated against <c>AutomationSettings:ApiKey</c> in appsettings.json.
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
            _context  = context;
            _settings = settings;
            _config   = config;
            _logger   = logger;
        }

        // ── Security Guard ────────────────────────────────────────────────────

        /// <summary>
        /// Validates the X-Automation-Key header against the configured API key.
        /// Returns true if the request is authorized; false otherwise.
        /// </summary>
        private bool IsAuthorized()
        {
            var configuredKey = _config["Automation:ApiKey"]
                             ?? _config["AutomationSettings:ApiKey"];

            // If no key is configured, deny all requests (fail-secure)
            if (string.IsNullOrWhiteSpace(configuredKey))
                return false;

            if (Request.Headers.TryGetValue("X-Automation-Key", out var providedKey))
            {
                return !string.IsNullOrWhiteSpace(providedKey)
                       && string.Equals(providedKey.ToString().Trim(), configuredKey.Trim(), StringComparison.Ordinal);
            }

            return false;
        }

        // ── A1. GET /api/automation/patient/{phone} ───────────────────────────

        /// <summary>
        /// Looks up a patient by phone number.
        /// Returns full profile and last doctor info if found, or { exists: false } if not.
        /// </summary>
        [HttpGet("patient/{phone}")]
        [ProducesResponseType(typeof(PatientLookupResult), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetPatientByPhone(string phone)
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            var cleanPhone = phone?.Trim() ?? string.Empty;
            _logger.LogInformation("[Automation] Patient lookup — phone={Phone}", cleanPhone);

            var patient = await _context.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PhoneNumber == cleanPhone);

            if (patient == null)
                return Ok(new PatientLookupResult { Exists = false });

            // Find the most recent non-cancelled appointment to get last doctor
            var lastAppointment = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == patient.PatientId && a.Status != "Cancelled")
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .FirstOrDefaultAsync();

            return Ok(new PatientLookupResult
            {
                Exists        = true,
                PatientId     = patient.PatientId,
                FullName      = patient.PatientName,
                TelegramChatId = patient.TelegramChatId,
                LastDoctorId  = lastAppointment?.DoctorId,
                LastDoctorName = lastAppointment?.Doctor?.DoctorName,
            });
        }

        // ── A2. POST /api/automation/patient ─────────────────────────────────

        /// <summary>
        /// Registers a new patient record via the Telegram bot flow.
        /// Creates the patient with a generated PatientNumber and returns the new PatientId.
        /// </summary>
        [HttpPost("patient")]
        [ProducesResponseType(typeof(CreatePatientResult), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var cleanPhone = request.Phone.Trim();

            // Duplicate phone guard
            var existing = await _context.Patients.AnyAsync(p => p.PhoneNumber == cleanPhone);
            if (existing)
                return Conflict(new CreatePatientResult
                {
                    Success = false,
                    Message = $"A patient with phone {cleanPhone} already exists. Use the lookup endpoint instead."
                });

            _logger.LogInformation("[Automation] Registering new patient — name={Name}, phone={Phone}", request.FullName, cleanPhone);

            var patient = new Patient
            {
                PatientName    = request.FullName.Trim(),
                PhoneNumber    = cleanPhone,
                NationalId     = request.NationalId.Trim(),
                DOB            = new DateTime(1990, 1, 1),  // Placeholder; patient can update via portal
                TelegramChatId = string.IsNullOrWhiteSpace(request.TelegramChatId) ? null : request.TelegramChatId.Trim(),
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Automation] Patient {Id} registered via Telegram bot.", patient.PatientId);

            return CreatedAtAction(nameof(GetPatientByPhone),
                new { phone = cleanPhone },
                new CreatePatientResult
                {
                    Success   = true,
                    PatientId = patient.PatientId,
                    Message   = "Patient registered successfully."
                });
        }

        // ── B. GET /api/automation/doctors ───────────────────────────────────

        /// <summary>
        /// Returns the list of all doctors with their specialization, department,
        /// and the clinic-wide operating shift boundaries and slot duration.
        /// </summary>
        [HttpGet("doctors")]
        [ProducesResponseType(typeof(List<DoctorListDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetDoctors()
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            _logger.LogInformation("[Automation] GetDoctors called.");

            var cfg = await _settings.GetSettingsAsync();

            var doctors = await _context.Doctors
                .IgnoreQueryFilters()   // Bypass multi-tenancy filter: API key requests have no session ClinicId
                .AsNoTracking()
                .Include(d => d.Department)
                .OrderBy(d => d.DepartmentId)
                .ThenBy(d => d.DoctorName)
                .Select(d => new DoctorListDto
                {
                    DoctorId            = d.DoctorId,
                    DoctorName          = d.DoctorName,
                    Specialization      = d.Specialization,
                    ConsultationFee     = d.ConsultationFee,
                    DepartmentName      = d.Department != null ? d.Department.DepartmentName : string.Empty,
                    TelegramChatId      = d.TelegramChatId,
                    // Clinic-wide scheduling parameters (same for all doctors)
                    WorkingDays         = cfg.WorkingDays,
                    OpeningTime         = cfg.WorkingHoursStart,
                    ClosingTime         = cfg.WorkingHoursEnd,
                    SlotDurationMinutes = cfg.DefaultSlotDurationMinutes,
                })
                .ToListAsync();

            return Ok(doctors);
        }

        // ── C. GET /api/automation/available-slots ────────────────────────────

        /// <summary>
        /// Returns available free time slots for a doctor on a given date.
        /// Reads clinic shift config, subtracts booked (non-cancelled) appointments,
        /// and returns free slots. If all slots are full, returns the next available date.
        /// </summary>
        [HttpGet("available-slots")]
        [ProducesResponseType(typeof(AvailableSlotsResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetAvailableSlots(
            [FromQuery] int doctorId,
            [FromQuery] string date)
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            if (doctorId <= 0)
                return BadRequest(new { error = "doctorId must be a positive integer." });

            if (!DateTime.TryParseExact(date, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                return BadRequest(new { error = "Invalid date format. Expected yyyy-MM-dd." });

            var doctorExists = await _context.Doctors.AnyAsync(d => d.DoctorId == doctorId);
            if (!doctorExists)
                return NotFound(new { error = $"Doctor with ID {doctorId} not found." });

            _logger.LogInformation("[Automation] AvailableSlots — doctorId={DoctorId}, date={Date}", doctorId, date);

            var cfg = await _settings.GetSettingsAsync();

            // Build all possible slots
            if (!TimeSpan.TryParse(cfg.WorkingHoursStart, out var shiftStart))
                shiftStart = TimeSpan.FromHours(8);
            if (!TimeSpan.TryParse(cfg.WorkingHoursEnd, out var shiftEnd))
                shiftEnd = TimeSpan.FromHours(20);

            int slotMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 30;

            var allSlots = new List<string>();
            for (var t = shiftStart; t + TimeSpan.FromMinutes(slotMinutes) <= shiftEnd; t += TimeSpan.FromMinutes(slotMinutes))
                allSlots.Add(t.ToString(@"hh\:mm"));

            // Load booked slots for this day
            var dateOnly = parsedDate.Date;
            var bookedTimes = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                         && a.AppointmentDate.Date == dateOnly
                         && a.Status != "Cancelled")
                .Select(a => a.AppointmentTime)
                .ToListAsync();

            var bookedSet = bookedTimes
                .Select(t => t.ToString(@"hh\:mm"))
                .ToHashSet();

            var freeSlots = allSlots.Where(s => !bookedSet.Contains(s)).ToList();

            // If full, find next available date (up to 30 days forward)
            string? nextAvailableDate = null;
            if (freeSlots.Count == 0)
            {
                var workingDays = cfg.WorkingDays
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                for (int offset = 1; offset <= 30; offset++)
                {
                    var candidate = parsedDate.AddDays(offset);
                    if (!workingDays.Contains(candidate.DayOfWeek.ToString()))
                        continue;

                    var candidateBooked = await _context.Appointments
                        .AsNoTracking()
                        .CountAsync(a => a.DoctorId == doctorId
                                      && a.AppointmentDate.Date == candidate.Date
                                      && a.Status != "Cancelled");

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
                Slots             = freeSlots,
                TotalAvailable    = freeSlots.Count,
                IsFull            = freeSlots.Count == 0,
                NextAvailableDate = nextAvailableDate,
            });
        }

        // ── D. POST /api/automation/book ──────────────────────────────────────

        /// <summary>
        /// Books a confirmed appointment via the Telegram bot.
        /// Creates the Appointment record, writes an AuditLog entry,
        /// and returns the doctor's TelegramChatId for instant alert dispatch.
        /// </summary>
        [HttpPost("book")]
        [ProducesResponseType(typeof(BookAppointmentResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Parse and validate date
            if (!DateTime.TryParseExact(request.Date, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var appointmentDate))
                return BadRequest(new BookAppointmentResult { Success = false, Message = "Invalid date format. Expected yyyy-MM-dd." });

            // Parse time slot
            if (!TimeSpan.TryParse(request.Time, out var timeSlot))
                return BadRequest(new BookAppointmentResult { Success = false, Message = "Invalid time format. Expected HH:mm." });

            // Validate patient
            var patient = await _context.Patients.FindAsync(request.PatientId);
            if (patient == null)
                return NotFound(new BookAppointmentResult { Success = false, Message = $"Patient ID {request.PatientId} not found." });

            // Validate doctor
            var doctor = await _context.Doctors
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.DoctorId == request.DoctorId);
            if (doctor == null)
                return NotFound(new BookAppointmentResult { Success = false, Message = $"Doctor ID {request.DoctorId} not found." });

            // Double-booking guard
            bool slotTaken = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == request.DoctorId
                && a.AppointmentDate.Date == appointmentDate.Date
                && a.AppointmentTime == timeSlot
                && a.Status != "Cancelled");

            if (slotTaken)
                return Conflict(new BookAppointmentResult
                {
                    Success = false,
                    Message = $"Slot {request.Time} on {request.Date} is already taken. Please choose another slot."
                });

            // Read AutoConfirm setting
            var cfg = await _settings.GetSettingsAsync();
            string status = cfg.AutoConfirmAppointments ? "Confirmed" : "Confirmed"; // Telegram bot always confirms
            string channel = request.Channel ?? "Telegram";

            // Create appointment
            var appointment = new Appointment
            {
                PatientId       = patient.PatientId,
                DoctorId        = doctor.DoctorId,
                AppointmentDate = appointmentDate.Date,
                AppointmentTime = timeSlot,
                Status          = status,
                Notes           = string.IsNullOrWhiteSpace(request.Notes)
                                    ? $"Booked via {channel}"
                                    : $"[{channel}] {request.Notes}",
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // Write structured audit log entry
            _context.AuditLogs.Add(new AuditLog
            {
                UserId     = null,
                UserName   = "Telegram_Bot",
                Action     = "CREATE",
                EntityName = "Appointment",
                EntityId   = appointment.AppointmentId.ToString(),
                Timestamp  = DateTime.UtcNow,
                Details    = $"Automated booking via {channel} | Patient: {patient.PatientName} (ID:{patient.PatientId}) | Doctor: {doctor.DoctorName} (ID:{doctor.DoctorId}) | Date: {request.Date} {request.Time} | Status: {status}",
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Automation] Appointment {ApptId} booked via {Channel} — Patient {PatientId}, Doctor {DoctorId}, {Date} {Time}.",
                appointment.AppointmentId, channel, patient.PatientId, doctor.DoctorId, request.Date, request.Time);

            return Ok(new BookAppointmentResult
            {
                Success               = true,
                Message               = "Appointment confirmed successfully.",
                AppointmentId         = appointment.AppointmentId,
                DoctorName            = doctor.DoctorName,
                DoctorTelegramChatId  = doctor.TelegramChatId,
                PatientName           = patient.PatientName,
                AppointmentDate       = request.Date,
                AppointmentTime       = request.Time,
                Status                = status,
            });
        }

        // ── E. GET /api/automation/weekly-schedule/{doctorId} ─────────────────

        /// <summary>
        /// Returns all non-cancelled appointments for the upcoming 7 days for a doctor,
        /// grouped by day with patient name, time, and visit type.
        /// </summary>
        [HttpGet("weekly-schedule/{doctorId:int}")]
        [ProducesResponseType(typeof(WeeklyScheduleResult), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetWeeklySchedule(int doctorId)
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            var doctor = await _context.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctor == null)
                return NotFound(new { error = $"Doctor ID {doctorId} not found." });

            _logger.LogInformation("[Automation] WeeklySchedule — doctorId={DoctorId}", doctorId);

            var today   = DateTime.UtcNow.Date;
            var weekEnd = today.AddDays(7);

            var appointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient)
                .Where(a => a.DoctorId == doctorId
                         && a.AppointmentDate.Date >= today
                         && a.AppointmentDate.Date <= weekEnd
                         && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .ToListAsync();

            // Group by day
            var days = appointments
                .GroupBy(a => a.AppointmentDate.Date)
                .Select(g => new WeeklyScheduleDayDto
                {
                    Date    = g.Key.ToString("yyyy-MM-dd"),
                    DayName = g.Key.ToString("dddd", CultureInfo.InvariantCulture),
                    Appointments = g.Select(a => new WeeklyScheduleEntryDto
                    {
                        AppointmentId  = a.AppointmentId,
                        Time           = a.AppointmentTime.ToString(@"hh\:mm"),
                        PatientName    = a.Patient?.PatientName ?? "Unknown",
                        TelegramChatId = a.Patient?.TelegramChatId,
                        // VisitType extracted from first line of Notes (if set by bot)
                        VisitType      = ExtractVisitType(a.Notes),
                        Status         = a.Status,
                    }).ToList()
                })
                .ToList();

            return Ok(new WeeklyScheduleResult
            {
                DoctorId          = doctor.DoctorId,
                DoctorName        = doctor.DoctorName,
                WeekStart         = today.ToString("yyyy-MM-dd"),
                WeekEnd           = weekEnd.ToString("yyyy-MM-dd"),
                TotalAppointments = appointments.Count,
                Days              = days,
            });
        }

        // ── F. n8n Workflow Automation Endpoints ───────────────────────────────
        // ── 1. GET /api/automation/due-reminders ──────────────────────────────
        /// <summary>
        /// Returns appointments scheduled for tomorrow with status Confirmed or Pending,
        /// where a reminder has not yet been sent.
        /// </summary>
        [HttpGet("due-reminders")]
        [ProducesResponseType(typeof(List<DueReminderDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetDueReminders()
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            _logger.LogInformation("[Automation] GetDueReminders called for date: {Date}", tomorrow.ToString("yyyy-MM-dd"));

            var appointments = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Department)
                .Where(a => a.AppointmentDate.Date == tomorrow
                         && (a.Status == "Confirmed" || a.Status == "Pending")
                         && !a.IsReminderSent)
                .OrderBy(a => a.AppointmentTime)
                .ToListAsync();

            var result = appointments.Select(a => new DueReminderDto
            {
                AppointmentId   = a.AppointmentId,
                PatientName     = a.Patient?.PatientName ?? "Unknown",
                TelegramChatId  = a.Patient?.TelegramChatId,
                DoctorName      = a.Doctor?.DoctorName ?? "Unknown",
                DepartmentName  = a.Department?.DepartmentName ?? a.Doctor?.Department?.DepartmentName ?? string.Empty,
                AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
            }).ToList();

            return Ok(result);
        }

        // ── 2. GET /api/automation/due-followups ──────────────────────────────
        /// <summary>
        /// Returns completed appointments from yesterday where a follow-up message has not yet been dispatched.
        /// </summary>
        [HttpGet("due-followups")]
        [ProducesResponseType(typeof(List<DueFollowupDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetDueFollowups()
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            _logger.LogInformation("[Automation] GetDueFollowups called for date: {Date}", yesterday.ToString("yyyy-MM-dd"));

            var appointments = await _context.Appointments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a => a.AppointmentDate.Date == yesterday
                         && a.Status == "Completed"
                         && (!a.IsFollowUpSent || a.FollowUpSentAt == null))
                .OrderBy(a => a.AppointmentTime)
                .ToListAsync();

            var result = appointments.Select(a => new DueFollowupDto
            {
                AppointmentId  = a.AppointmentId,
                PatientName    = a.Patient?.PatientName ?? "Unknown",
                TelegramChatId = a.Patient?.TelegramChatId,
                DoctorName     = a.Doctor?.DoctorName ?? "Unknown",
                DoctorPhone    = a.Doctor?.DoctorPhone,
            }).ToList();

            return Ok(result);
        }

        // ── 3. POST /api/automation/update-status ─────────────────────────────
        /// <summary>
        /// Updates the status or reminder/follow-up flags of an appointment.
        /// Action can be: "ReminderSent" | "FollowUpSent" | "Confirmed" | "Cancelled"
        /// </summary>
        [HttpPost("update-status")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            if (request == null || string.IsNullOrWhiteSpace(request.Action))
                return BadRequest(new { error = "Missing or invalid payload: 'action' is required." });

            int appointmentId = ParseId(request.AppointmentId);
            if (appointmentId <= 0)
                return BadRequest(new { error = "Invalid or missing 'appointmentId'." });

            var appointment = await _context.Appointments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
                return NotFound(new { error = $"Appointment with ID {appointmentId} not found." });

            var action = request.Action.Trim();
            _logger.LogInformation("[Automation] UpdateStatus called — appointmentId={Id}, action={Action}", appointmentId, action);

            if (string.Equals(action, "ReminderSent", StringComparison.OrdinalIgnoreCase))
            {
                appointment.IsReminderSent = true;
            }
            else if (string.Equals(action, "FollowUpSent", StringComparison.OrdinalIgnoreCase))
            {
                appointment.IsFollowUpSent = true;
                appointment.FollowUpSentAt = DateTime.UtcNow;
            }
            else if (string.Equals(action, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                appointment.Status = "Confirmed";
            }
            else if (string.Equals(action, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                appointment.Status = "Cancelled";
            }
            else
            {
                return BadRequest(new { error = $"Unsupported action '{request.Action}'. Allowed actions: ReminderSent, FollowUpSent, Confirmed, Cancelled." });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success         = true,
                appointmentId   = appointment.AppointmentId,
                status          = appointment.Status,
                isReminderSent  = appointment.IsReminderSent,
                isFollowUpSent  = appointment.IsFollowUpSent,
                followUpSentAt  = appointment.FollowUpSentAt,
                message         = $"Action '{action}' processed successfully."
            });
        }

        // ── 4. POST /api/automation/book-slot ─────────────────────────────────
        /// <summary>
        /// Books an appointment slot for a patient identified by their TelegramChatId.
        /// Validates doctor availability and records the new appointment.
        /// </summary>
        [HttpPost("book-slot")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> BookSlot([FromBody] BookSlotRequest request)
        {
            if (!IsAuthorized())
                return Unauthorized(new { error = "Invalid or missing X-Automation-Key header." });

            if (request == null)
                return BadRequest(new { error = "Request payload cannot be empty." });

            if (string.IsNullOrWhiteSpace(request.PatientTelegramChatId))
                return BadRequest(new { error = "patientTelegramChatId is required." });

            int doctorId = ParseId(request.DoctorId);
            if (doctorId <= 0)
                return BadRequest(new { error = "Invalid or missing doctorId." });

            if (string.IsNullOrWhiteSpace(request.Date) || !DateTime.TryParse(request.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var apptDate))
                return BadRequest(new { error = "Invalid or missing 'date'. Expected format: yyyy-MM-dd or ISO 8601." });

            if (string.IsNullOrWhiteSpace(request.Time) || !TimeSpan.TryParse(request.Time, out var apptTime))
                return BadRequest(new { error = "Invalid or missing 'time'. Expected format: HH:mm." });

            // Validate Patient by TelegramChatId
            var cleanChatId = request.PatientTelegramChatId.Trim();
            var patient = await _context.Patients
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TelegramChatId == cleanChatId);

            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"No patient found with TelegramChatId: {cleanChatId}."
                });
            }

            // Validate Doctor
            var doctor = await _context.Doctors
                .IgnoreQueryFilters()
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctor == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Doctor with ID {doctorId} not found."
                });
            }

            // Check availability (double-booking guard)
            bool slotTaken = await _context.Appointments
                .IgnoreQueryFilters()
                .AnyAsync(a => a.DoctorId == doctor.DoctorId
                            && a.AppointmentDate.Date == apptDate.Date
                            && a.AppointmentTime == apptTime
                            && a.Status != "Cancelled");

            if (slotTaken)
            {
                return Conflict(new
                {
                    success = false,
                    message = $"Slot {apptTime:hh\\:mm} on {apptDate:yyyy-MM-dd} is already booked for Dr. {doctor.DoctorName}."
                });
            }

            var appointment = new Appointment
            {
                PatientId       = patient.PatientId,
                DoctorId        = doctor.DoctorId,
                DepartmentId    = doctor.DepartmentId,
                AppointmentDate = apptDate.Date,
                AppointmentTime = apptTime,
                Status          = "Confirmed",
                Notes           = string.IsNullOrWhiteSpace(request.Notes)
                                    ? "[Telegram Automation]"
                                    : $"[Telegram Automation] {request.Notes}",
                ClinicId        = doctor.ClinicId ?? patient.ClinicId,
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                UserName   = "Telegram_Automation",
                Action     = "CREATE",
                EntityName = "Appointment",
                EntityId   = appointment.AppointmentId.ToString(),
                Timestamp  = DateTime.UtcNow,
                Details    = $"Automated book-slot via Telegram | ChatId: {cleanChatId} | Patient: {patient.PatientName} | Doctor: Dr. {doctor.DoctorName} | Slot: {apptDate:yyyy-MM-dd} {apptTime:hh\\:mm}",
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success         = true,
                appointmentId   = appointment.AppointmentId,
                patientName     = patient.PatientName,
                doctorName      = doctor.DoctorName,
                departmentName  = doctor.Department?.DepartmentName ?? string.Empty,
                appointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
                appointmentTime = appointment.AppointmentTime.ToString(@"hh\:mm"),
                status          = appointment.Status,
                message         = "Appointment slot booked successfully."
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Extracts numeric integer ID from JSON number or string token.</summary>
        private static int ParseId(System.Text.Json.JsonElement element)
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetInt32(out int num))
                return num;
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var str = element.GetString();
                if (int.TryParse(str, out int parsed))
                    return parsed;
            }
            return 0;
        }

        /// <summary>Extracts the visit type token from a bracketed channel prefix like "[Telegram] Dental checkup".</summary>
        private static string? ExtractVisitType(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return null;
            // Strip channel prefix e.g. "[Telegram] " or "[WhatsApp] "
            var text = notes.Contains(']')
                ? notes[(notes.IndexOf(']') + 1)..].Trim()
                : notes.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
