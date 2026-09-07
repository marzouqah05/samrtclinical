using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    // ── Public Booking API ────────────────────────────────────────────────────

    /// <summary>Request body for the POST /api/booking/auto-book endpoint.</summary>
    public class AutoBookRequest
    {
        [Required]
        public string PatientName { get; set; } = string.Empty;

        [Required]
        public string PatientPhone { get; set; } = string.Empty;

        [Required]
        public int DoctorId { get; set; }

        /// <summary>ISO date string: yyyy-MM-dd</summary>
        [Required]
        public string AppointmentDate { get; set; } = string.Empty;

        /// <summary>Time slot string: HH:mm (e.g. "09:30")</summary>
        [Required]
        public string TimeSlot { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }

    /// <summary>Response payload returned by POST /api/booking/auto-book.</summary>
    public class AutoBookResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? AppointmentId { get; set; }
        public string? PatientName { get; set; }
        public string? PatientPhone { get; set; }
        public string? DoctorName { get; set; }
        public string? AppointmentDate { get; set; }
        public string? AppointmentTime { get; set; }
        public string? Status { get; set; }
    }

    // ── Doctor Schedule Digest ────────────────────────────────────────────────

    /// <summary>Summary of a single appointment as returned in the weekly digest.</summary>
    public class AppointmentSummaryDto
    {
        public int AppointmentId { get; set; }
        public string AppointmentDate { get; set; } = string.Empty;
        public string AppointmentTime { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>Doctor's full 7-day schedule payload for GET /api/doctors/weekly-schedules.</summary>
    public class DoctorWeeklyScheduleDto
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string? DoctorEmail { get; set; }
        public string? DoctorPhone { get; set; }
        public string WeekStart { get; set; } = string.Empty;
        public string WeekEnd { get; set; } = string.Empty;
        public List<AppointmentSummaryDto> Appointments { get; set; } = new();
        public int TotalAppointments => Appointments.Count;
    }
}
