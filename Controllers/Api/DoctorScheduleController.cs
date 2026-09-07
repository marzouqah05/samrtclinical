using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers.Api
{
    /// <summary>
    /// Doctor weekly schedule API for n8n digest triggers and external schedule consumers.
    /// All endpoints require the X-Api-Token header to match the stored ClinicSettings ApiToken.
    /// </summary>
    [Route("api/doctors")]
    [ApiController]
    public class DoctorScheduleController : ControllerBase
    {
        private readonly IDoctorScheduleService _scheduleService;
        private readonly ISettingsService _settings;
        private readonly ILogger<DoctorScheduleController> _logger;

        public DoctorScheduleController(
            IDoctorScheduleService scheduleService,
            ISettingsService settings,
            ILogger<DoctorScheduleController> logger)
        {
            _scheduleService = scheduleService;
            _settings        = settings;
            _logger          = logger;
        }

        // ── Token Auth Helper ─────────────────────────────────────────────────

        private async Task<bool> IsAuthorizedAsync()
        {
            var storedToken = await _settings.GetSettingValueAsync("ApiToken");
            if (string.IsNullOrWhiteSpace(storedToken)) return true;

            Request.Headers.TryGetValue("X-Api-Token", out var provided);
            return !string.IsNullOrWhiteSpace(provided) && provided.ToString() == storedToken;
        }

        // ── GET /api/doctors/weekly-schedules ─────────────────────────────────

        /// <summary>
        /// Returns all active doctors' upcoming 7-day schedule with patient names,
        /// appointment times, reasons (notes), contact info, doctor email and WhatsApp number.
        /// </summary>
        [HttpGet("weekly-schedules")]
        [ProducesResponseType(typeof(List<DoctorWeeklyScheduleDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetWeeklySchedules()
        {
            if (!await IsAuthorizedAsync())
                return Unauthorized(new { error = "Invalid or missing X-Api-Token header." });

            _logger.LogInformation("[API] GetWeeklySchedules requested.");

            var schedules = await _scheduleService.GetWeeklySchedulesAsync();

            return Ok(new
            {
                generatedAt      = DateTime.UtcNow.ToString("o"),
                totalDoctors     = schedules.Count,
                weeklySchedules  = schedules,
            });
        }

        // ── POST /api/doctors/weekly-schedules/send-digest/{doctorId} ─────────

        /// <summary>
        /// Manually triggers the HTML weekly digest email for the specified doctor.
        /// </summary>
        [HttpPost("weekly-schedules/send-digest/{doctorId:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SendDigest(int doctorId)
        {
            if (!await IsAuthorizedAsync())
                return Unauthorized(new { error = "Invalid or missing X-Api-Token header." });

            _logger.LogInformation("[API] Manual digest trigger for DoctorId={DoctorId}.", doctorId);

            bool sent = await _scheduleService.SendDoctorWeeklyDigestEmailAsync(doctorId);

            if (!sent)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Digest could not be sent. Ensure the doctor exists, has an email configured, and SMTP is properly set up in appsettings.json.",
                });
            }

            return Ok(new { success = true, message = $"Weekly digest email dispatched for doctor ID {doctorId}." });
        }

        // ── POST /api/doctors/weekly-schedules/send-all-digests ───────────────

        /// <summary>
        /// Manually triggers weekly digest emails for ALL doctors who have an email address.
        /// </summary>
        [HttpPost("weekly-schedules/send-all-digests")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> SendAllDigests()
        {
            if (!await IsAuthorizedAsync())
                return Unauthorized(new { error = "Invalid or missing X-Api-Token header." });

            _logger.LogInformation("[API] Manual bulk digest trigger for all doctors.");

            await _scheduleService.SendAllDoctorDigestsAsync();

            return Ok(new { success = true, message = "Weekly digest emails dispatched to all configured doctors." });
        }
    }
}
