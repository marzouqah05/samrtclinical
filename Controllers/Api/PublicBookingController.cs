using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers.Api
{
    /// <summary>
    /// Public booking API for WhatsApp / n8n self-booking automation.
    /// All endpoints require the X-Api-Token header to match the stored ClinicSettings ApiToken.
    /// </summary>
    [Route("api/booking")]
    [ApiController]
    public class PublicBookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly ISettingsService _settings;
        private readonly ILogger<PublicBookingController> _logger;

        public PublicBookingController(
            IBookingService bookingService,
            ISettingsService settings,
            ILogger<PublicBookingController> logger)
        {
            _bookingService = bookingService;
            _settings       = settings;
            _logger         = logger;
        }

        // ── Token Auth Helper ─────────────────────────────────────────────────

        private async Task<bool> IsAuthorizedAsync()
        {
            var storedToken = await _settings.GetSettingValueAsync("ApiToken");
            if (string.IsNullOrWhiteSpace(storedToken)) return true; // No token configured → open access

            Request.Headers.TryGetValue("X-Api-Token", out var provided);
            return !string.IsNullOrWhiteSpace(provided) && provided.ToString() == storedToken;
        }

        // ── GET /api/booking/available-slots ─────────────────────────────────

        /// <summary>
        /// Returns available time slots for a specific doctor on a given date.
        /// </summary>
        /// <param name="doctorId">Target doctor ID.</param>
        /// <param name="date">Date in yyyy-MM-dd format.</param>
        [HttpGet("available-slots")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] int doctorId, [FromQuery] string date)
        {
            if (!await IsAuthorizedAsync())
                return Unauthorized(new { error = "Invalid or missing X-Api-Token header." });

            if (doctorId <= 0)
                return BadRequest(new { error = "doctorId must be a positive integer." });

            if (!DateTime.TryParseExact(date, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                return BadRequest(new { error = "Invalid date format. Expected yyyy-MM-dd." });
            }

            _logger.LogInformation("[API] GetAvailableSlots — doctorId={DoctorId}, date={Date}", doctorId, date);

            var slots = await _bookingService.GetAvailableSlotsAsync(doctorId, parsedDate);

            return Ok(new
            {
                doctorId,
                date,
                availableSlots = slots,
                totalSlots     = slots.Count,
            });
        }

        // ── POST /api/booking/auto-book ───────────────────────────────────────

        /// <summary>
        /// Books an appointment for the caller via the WhatsApp / n8n flow.
        /// Finds or auto-registers the patient, verifies slot availability, and creates a Confirmed appointment.
        /// </summary>
        [HttpPost("auto-book")]
        [ProducesResponseType(typeof(AutoBookResult), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> AutoBook([FromBody] AutoBookRequest request)
        {
            if (!await IsAuthorizedAsync())
                return Unauthorized(new { error = "Invalid or missing X-Api-Token header." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation("[API] AutoBook — patient={Phone}, doctorId={DoctorId}, date={Date}, slot={Slot}",
                request.PatientPhone, request.DoctorId, request.AppointmentDate, request.TimeSlot);

            var result = await _bookingService.AutoBookAsync(request);

            if (!result.Success)
            {
                // Slot conflict → 409; other errors → 400
                bool isConflict = result.Message.Contains("no longer available", StringComparison.OrdinalIgnoreCase);
                return isConflict
                    ? Conflict(result)
                    : BadRequest(result);
            }

            return Ok(result);
        }
    }
}
