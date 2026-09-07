namespace WebApplication1.Services
{
    /// <summary>
    /// Abstraction for sending WhatsApp notifications to patients and doctors via n8n webhook or direct provider.
    /// </summary>
    public interface IWhatsAppService
    {
        /// <summary>
        /// Sends a pre-appointment reminder to the patient via WhatsApp / n8n webhook.
        /// </summary>
        Task<bool> SendAppointmentReminderAsync(
            string patientPhone,
            string patientName,
            DateTime appointmentDate,
            string doctorName,
            string doctorPhone = "");

        /// <summary>
        /// Sends a post-visit follow-up message to the patient via WhatsApp / n8n webhook.
        /// </summary>
        Task<bool> SendPostVisitFollowUpAsync(
            string patientPhone,
            string patientName,
            string doctorPhone = "",
            string doctorName = "",
            DateTime? appointmentDate = null);
    }
}