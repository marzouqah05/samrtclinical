using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Public booking logic for the WhatsApp / n8n self-booking automation flow.
    /// </summary>
    public interface IBookingService
    {
        /// <summary>
        /// Returns a list of available HH:mm time slots for the specified doctor on the given date,
        /// taking into account clinic working hours and existing non-cancelled appointments.
        /// </summary>
        Task<List<string>> GetAvailableSlotsAsync(int doctorId, DateTime date);

        /// <summary>
        /// Finds or registers a patient by phone number, verifies the requested slot is still free,
        /// creates an Appointment with status "Confirmed", and returns the booking details.
        /// </summary>
        Task<AutoBookResult> AutoBookAsync(AutoBookRequest request);
    }
}
