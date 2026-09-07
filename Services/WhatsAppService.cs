using System.Text;
using System.Text.Json;

namespace WebApplication1.Services
{
    /// <summary>
    /// Implementation of <see cref="IWhatsAppService"/> that delivers WhatsApp notifications
    /// by dispatching events to an n8n webhook workflow.
    /// The webhook URL and enabled/disabled state are read dynamically from the database settings
    /// (via <see cref="ISettingsService"/>) at dispatch time, with appsettings.json as a fallback.
    /// </summary>
    public sealed class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly ISettingsService _settingsService;

        public WhatsAppService(
            HttpClient http,
            IConfiguration config,
            ILogger<WhatsAppService> logger,
            ISettingsService settingsService)
        {
            _http            = http;
            _config          = config;
            _logger          = logger;
            _settingsService = settingsService;
        }

        /// <inheritdoc/>
        public async Task<bool> SendAppointmentReminderAsync(
            string patientPhone,
            string patientName,
            DateTime appointmentDate,
            string doctorName,
            string doctorPhone = "")
        {
            // Check toggle from DB settings; fall back to enabled if not set
            var enabledStr = await _settingsService.GetSettingValueAsync("EnableWhatsAppReminders", "False");
            if (!bool.TryParse(enabledStr, out var enabled) || !enabled)
            {
                _logger.LogInformation("[WhatsApp] Reminders are disabled in clinic settings. Skipping Reminder for {Phone}.", patientPhone);
                return false;
            }

            var cleanPhone = patientPhone ?? string.Empty;
            var payload = new
            {
                patientName     = patientName ?? string.Empty,
                patientPhone    = cleanPhone,
                doctorName      = doctorName ?? string.Empty,
                doctorPhone     = doctorPhone ?? string.Empty,
                appointmentDate = appointmentDate.ToString("yyyy-MM-dd HH:mm"),
                type            = "Reminder"
            };

            return await DispatchWebhookAsync(payload, "Reminder", cleanPhone);
        }

        /// <inheritdoc/>
        public async Task<bool> SendPostVisitFollowUpAsync(
            string patientPhone,
            string patientName,
            string doctorPhone = "",
            string doctorName  = "",
            DateTime? appointmentDate = null)
        {
            // Check follow-up toggle from DB settings
            var enabledStr = await _settingsService.GetSettingValueAsync("EnableFollowUpMessages", "False");
            if (!bool.TryParse(enabledStr, out var enabled) || !enabled)
            {
                _logger.LogInformation("[WhatsApp] Follow-up messages are disabled in clinic settings. Skipping for {Phone}.", patientPhone);
                return false;
            }

            var cleanPhone = patientPhone ?? string.Empty;
            var payload = new
            {
                patientName     = patientName ?? string.Empty,
                patientPhone    = cleanPhone,
                doctorName      = doctorName ?? string.Empty,
                doctorPhone     = doctorPhone ?? string.Empty,
                appointmentDate = appointmentDate.HasValue
                    ? appointmentDate.Value.ToString("yyyy-MM-dd HH:mm")
                    : DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                type = "FollowUp"
            };

            return await DispatchWebhookAsync(payload, "FollowUp", cleanPhone);
        }

        /// <summary>
        /// Resolves the effective webhook URL: DB settings first, then appsettings.json as fallback.
        /// </summary>
        private async Task<string> ResolveWebhookUrlAsync()
        {
            var dbUrl = await _settingsService.GetSettingValueAsync("WhatsAppWebhookUrl");
            if (!string.IsNullOrWhiteSpace(dbUrl))
                return dbUrl;

            // Fallback to appsettings.json
            return _config["WhatsAppConfig:N8nWebhookUrl"]
                   ?? _config["WhatsApp:N8nWebhookUrl"]
                   ?? string.Empty;
        }

        /// <summary>
        /// Dispatches a JSON POST request to the configured n8n Webhook URL.
        /// Includes comprehensive fallback error logging.
        /// </summary>
        private async Task<bool> DispatchWebhookAsync(object payload, string type, string recipientPhone)
        {
            var webhookUrl = await ResolveWebhookUrlAsync();

            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                _logger.LogWarning(
                    "[WhatsApp] WhatsAppWebhookUrl is not configured in DB settings or appsettings.json. Cannot dispatch {Type} to {Phone}.",
                    type, recipientPhone);
                return false;
            }

            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented        = false
                });

                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation(
                    "[WhatsApp] Dispatching {Type} notification via n8n webhook to {Url} for {Phone}...",
                    type, webhookUrl, recipientPhone);

                var response     = await _http.PostAsync(webhookUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "[WhatsApp] Successfully dispatched {Type} for {Phone}. Response: {Response}",
                        type, recipientPhone, responseBody);
                    return true;
                }

                _logger.LogError(
                    "[WhatsApp] n8n Webhook failed with HTTP {StatusCode} ({Reason}) for {Type} to {Phone}. Body: {Response}",
                    (int)response.StatusCode, response.ReasonPhrase, type, recipientPhone, responseBody);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[WhatsApp] HTTP exception calling webhook for {Type} to {Phone} at {Url}.", type, recipientPhone, webhookUrl);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "[WhatsApp] Timeout calling webhook for {Type} to {Phone} at {Url}.", type, recipientPhone, webhookUrl);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WhatsApp] Unexpected exception dispatching {Type} to {Phone}.", type, recipientPhone);
                return false;
            }
        }
    }
}