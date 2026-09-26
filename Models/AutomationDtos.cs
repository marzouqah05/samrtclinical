using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    // ── Automation API DTOs ───────────────────────────────────────────────────
    // Used exclusively by Controllers/Api/AutomationController.cs
    // and backed by TelegramBotService for conversational flow.

    // ── A. Patient Lookup & Registration ─────────────────────────────────────

    /// <summary>Response for GET /api/automation/patient/{phone}</summary>
    public class PatientLookupResult
    {
        public bool Exists { get; set; }
        public int? PatientId { get; set; }
        public string? FullName { get; set; }
        public string? TelegramChatId { get; set; }
        public int? LastDoctorId { get; set; }
        public string? LastDoctorName { get; set; }
    }

    /// <summary>Request body for POST /api/automation/patient</summary>
    public class CreatePatientRequest
    {
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(15)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string NationalId { get; set; } = string.Empty;

        public string? TelegramChatId { get; set; }
    }

    /// <summary>Response for POST /api/automation/patient</summary>
    public class CreatePatientResult
    {
        public bool Success { get; set; }
        public int? PatientId { get; set; }
        public string? Message { get; set; }
    }

    // ── B. Doctor Listing ─────────────────────────────────────────────────────

    /// <summary>Response item for GET /api/automation/doctors</summary>
    public class DoctorListDto
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public decimal ConsultationFee { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string WorkingDays { get; set; } = string.Empty;
        public string OpeningTime { get; set; } = string.Empty;
        public string ClosingTime { get; set; } = string.Empty;
        public int SlotDurationMinutes { get; set; }
        public string? TelegramChatId { get; set; }
    }

    // ── B2. Doctors List Response (wraps DoctorListDto with clinic identity) ─

    /// <summary>Response for GET /api/automation/doctors</summary>
    public class DoctorsListResponse
    {
        public string ClinicName { get; set; } = string.Empty;
        public string ClinicPhone { get; set; } = string.Empty;
        public List<DoctorListDto> Doctors { get; set; } = new();
    }

    // ── C. Slot Availability ──────────────────────────────────────────────────

    /// <summary>Response for GET /api/automation/available-slots</summary>
    public class AvailableSlotsResult
    {
        public int DoctorId { get; set; }
        public string Date { get; set; } = string.Empty;
        public List<string> Slots { get; set; } = new();
        public int TotalAvailable { get; set; }
        public bool IsFull { get; set; }
        public string? NextAvailableDate { get; set; }
    }

    // ── D. Booking ────────────────────────────────────────────────────────────

    /// <summary>Request body for POST /api/automation/book</summary>
    public class BookAppointmentRequest
    {
        [Required]
        public int PatientId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        /// <summary>ISO date: yyyy-MM-dd</summary>
        [Required]
        public string Date { get; set; } = string.Empty;

        /// <summary>Time slot: HH:mm</summary>
        [Required]
        public string Time { get; set; } = string.Empty;

        public string? Notes { get; set; }

        /// <summary>Booking channel identifier, e.g. "Telegram", "WhatsApp", "Web".</summary>
        public string Channel { get; set; } = "Telegram";
    }

    /// <summary>Response for POST /api/automation/book</summary>
    public class BookAppointmentResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? AppointmentId { get; set; }
        public string? DoctorName { get; set; }
        public string? DoctorTelegramChatId { get; set; }
        public string? PatientName { get; set; }
        public string? AppointmentDate { get; set; }
        public string? AppointmentTime { get; set; }
        public string? Status { get; set; }
    }

    // ── E. Weekly Schedule ────────────────────────────────────────────────────

    /// <summary>Single appointment entry in a weekly schedule day.</summary>
    public class WeeklyScheduleEntryDto
    {
        public int AppointmentId { get; set; }
        public string Time { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string? TelegramChatId { get; set; }
        public string? VisitType { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>A single day's appointments in the weekly schedule response.</summary>
    public class WeeklyScheduleDayDto
    {
        public string Date { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public List<WeeklyScheduleEntryDto> Appointments { get; set; } = new();
    }

    /// <summary>Full weekly schedule response for GET /api/automation/weekly-schedule/{doctorId}</summary>
    public class WeeklyScheduleResult
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string WeekStart { get; set; } = string.Empty;
        public string WeekEnd { get; set; } = string.Empty;
        public int TotalAppointments { get; set; }
        public List<WeeklyScheduleDayDto> Days { get; set; } = new();
    }

    // ── F. n8n Workflow Automation DTOs ───────────────────────────────────────

    /// <summary>Response item for GET /api/automation/due-reminders</summary>
    public class DueReminderDto
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string? TelegramChatId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;
        public string AppointmentTime { get; set; } = string.Empty;
    }

    /// <summary>Response item for GET /api/automation/due-followups</summary>
    public class DueFollowupDto
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string? TelegramChatId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string? DoctorPhone { get; set; }
    }

    /// <summary>Request body for POST /api/automation/update-status</summary>
    public class UpdateStatusRequest
    {
        public System.Text.Json.JsonElement AppointmentId { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty;
    }

    /// <summary>Request body for POST /api/automation/book-slot</summary>
    public class BookSlotRequest
    {
        [Required]
        public string PatientTelegramChatId { get; set; } = string.Empty;

        [Required]
        public System.Text.Json.JsonElement DoctorId { get; set; }

        public string? Date { get; set; }

        public string? Time { get; set; }

        public string? Notes { get; set; }
    }
}
