using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Provides doctor weekly schedule data and the weekly HTML email digest service.
    /// </summary>
    public interface IDoctorScheduleService
    {
        /// <summary>
        /// Returns each active doctor's upcoming 7-day schedule including patient names,
        /// appointment times, reasons (notes), contact info, doctor email and WhatsApp number.
        /// </summary>
        Task<List<DoctorWeeklyScheduleDto>> GetWeeklySchedulesAsync();

        /// <summary>
        /// Formats and sends an HTML weekly timetable email to the specified doctor.
        /// Returns true if the email was dispatched successfully.
        /// </summary>
        Task<bool> SendDoctorWeeklyDigestEmailAsync(int doctorId);

        /// <summary>
        /// Sends weekly digest emails to ALL doctors who have an email address configured.
        /// </summary>
        Task SendAllDoctorDigestsAsync();
    }
}
