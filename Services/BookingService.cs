using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Implements public self-booking logic for the WhatsApp / n8n automation flow.
    /// </summary>
    public class BookingService : IBookingService
    {
        private readonly ClinicDbContext _context;
        private readonly ISettingsService _settings;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            ClinicDbContext context,
            ISettingsService settings,
            ILogger<BookingService> logger)
        {
            _context  = context;
            _settings = settings;
            _logger   = logger;
        }

        // ── Available Slots ───────────────────────────────────────────────────

        public async Task<List<string>> GetAvailableSlotsAsync(int doctorId, DateTime date)
        {
            // 1. Validate doctor exists
            var doctorExists = await _context.Doctors.AnyAsync(d => d.DoctorId == doctorId);
            if (!doctorExists) return new List<string>();

            // 2. Load clinic settings
            var cfg = await _settings.GetSettingsAsync();

            // 3. Validate working day
            var workingDays = cfg.WorkingDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string dayName = date.DayOfWeek.ToString();
            if (!workingDays.Contains(dayName)) return new List<string>();

            // 4. Build all possible slots for the day
            if (!TimeSpan.TryParse(cfg.WorkingHoursStart, out var start))
                start = TimeSpan.FromHours(8);
            if (!TimeSpan.TryParse(cfg.WorkingHoursEnd, out var end))
                end = TimeSpan.FromHours(20);

            int slotMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 30;

            var allSlots = new List<string>();
            for (var t = start; t + TimeSpan.FromMinutes(slotMinutes) <= end; t += TimeSpan.FromMinutes(slotMinutes))
            {
                allSlots.Add(t.ToString(@"hh\:mm"));
            }

            // 5. Subtract already-booked (non-cancelled) slots
            var dateOnly = date.Date;
            var bookedTimes = await _context.Appointments
                .Where(a => a.DoctorId == doctorId
                         && a.AppointmentDate.Date == dateOnly
                         && a.Status != "Cancelled")
                .Select(a => a.AppointmentTime)
                .ToListAsync();

            var bookedSet = bookedTimes
                .Select(t => t.ToString(@"hh\:mm"))
                .ToHashSet();

            return allSlots.Where(s => !bookedSet.Contains(s)).ToList();
        }

        // ── Auto Book ─────────────────────────────────────────────────────────

        public async Task<AutoBookResult> AutoBookAsync(AutoBookRequest request)
        {
            try
            {
                // 1. Parse date
                if (!DateTime.TryParseExact(request.AppointmentDate, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var appointmentDate))
                {
                    return Fail("Invalid appointment date format. Expected yyyy-MM-dd.");
                }

                // 2. Parse time slot
                if (!TimeSpan.TryParse(request.TimeSlot, out var timeSlot))
                    return Fail("Invalid time slot format. Expected HH:mm.");

                // 3. Check doctor exists
                var doctor = await _context.Doctors.FindAsync(request.DoctorId);
                if (doctor == null)
                    return Fail($"Doctor with ID {request.DoctorId} was not found.");

                // 4. Verify slot is available (double-booking guard)
                bool slotTaken = await _context.Appointments.AnyAsync(a =>
                    a.DoctorId == request.DoctorId
                    && a.AppointmentDate.Date == appointmentDate.Date
                    && a.AppointmentTime == timeSlot
                    && a.Status != "Cancelled");

                if (slotTaken)
                    return Fail($"The slot {request.TimeSlot} on {request.AppointmentDate} is no longer available.");

                // 5. Find or register patient by phone number
                var phone = request.PatientPhone.Trim();
                var patient = await _context.Patients
                    .FirstOrDefaultAsync(p => p.PhoneNumber == phone);

                if (patient == null)
                {
                    // Auto-register: NationalId uses WA-prefixed placeholder (10 chars)
                    var placeholderNationalId = "WA" + new Random().Next(10_000_000, 99_999_999).ToString();
                    patient = new Patient
                    {
                        PatientName  = request.PatientName.Trim(),
                        PhoneNumber  = phone,
                        NationalId   = placeholderNationalId,
                        DOB          = new DateTime(1990, 1, 1),   // placeholder DOB
                    };
                    _context.Patients.Add(patient);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("[AutoBook] Auto-registered new patient '{Name}' ({Phone}).",
                        patient.PatientName, patient.PhoneNumber);
                }

                // 6. Create appointment
                var appointment = new Appointment
                {
                    DoctorId        = request.DoctorId,
                    PatientId       = patient.PatientId,
                    AppointmentDate = appointmentDate.Date,
                    AppointmentTime = timeSlot,
                    Status          = "Confirmed",
                    Notes           = request.Notes,
                };
                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[AutoBook] Appointment {Id} created for patient {PatientId} with Dr. {DoctorId} on {Date} at {Time}.",
                    appointment.AppointmentId, patient.PatientId, doctor.DoctorId,
                    appointmentDate.ToString("yyyy-MM-dd"), request.TimeSlot);

                return new AutoBookResult
                {
                    Success         = true,
                    Message         = "Appointment booked successfully.",
                    AppointmentId   = appointment.AppointmentId,
                    PatientName     = patient.PatientName,
                    PatientPhone    = patient.PhoneNumber,
                    DoctorName      = doctor.DoctorName,
                    AppointmentDate = appointmentDate.ToString("yyyy-MM-dd"),
                    AppointmentTime = request.TimeSlot,
                    Status          = "Confirmed",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AutoBook] Unexpected error during auto-booking.");
                return Fail("An unexpected error occurred. Please try again.");
            }
        }

        private static AutoBookResult Fail(string message) =>
            new AutoBookResult { Success = false, Message = message };
    }
}
