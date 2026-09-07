using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers.Api
{
    /// <summary>
    /// Webhook receiver for the Telegram Bot API.
    ///
    /// ROUTE: POST /api/telegram/webhook
    ///
    /// Telegram POSTs an Update JSON object to this endpoint every time a user
    /// sends a message or taps an Inline Keyboard button. The endpoint MUST:
    ///   1. Return HTTP 200 OK within ≤5 seconds or Telegram will retry.
    ///   2. Be reachable without authentication (AllowAnonymous).
    ///   3. Properly deserialise the snake_case JSON payload into our TelegramUpdate DTO.
    ///
    /// CONCURRENCY / DbContext SAFETY:
    ///   ITelegramBotService is Scoped — it owns the same DbContext instance as this
    ///   controller and is safe to use inline within the request scope.
    ///   If heavy processing is ever offloaded to a background Task (outside this scope),
    ///   create a new DI scope via IServiceScopeFactory — never capture the scoped service.
    /// </summary>
    [ApiController]
    [Route("api/telegram")]
    [AllowAnonymous]                           // Telegram calls this without any session/cookie
    public class TelegramController : ControllerBase
    {
        private readonly ITelegramBotService _botService;
        private readonly ISettingsService _settings;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TelegramController> _logger;

        public TelegramController(
            ITelegramBotService botService,
            ISettingsService settings,
            IServiceScopeFactory scopeFactory,
            ILogger<TelegramController> logger)
        {
            _botService   = botService;
            _settings     = settings;
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        // ── POST /api/telegram/webhook ────────────────────────────────────────
        //
        // This is the EXACT route registered with Telegram via:
        //   https://api.telegram.org/bot{TOKEN}/setWebhook?url=https://your-domain/api/telegram/webhook
        //
        // The endpoint validates that the Telegram bot is enabled in clinic settings,
        // then processes the update synchronously within the request scope so the
        // scoped DbContext is never accessed across thread boundaries.
        // HTTP 200 is returned immediately regardless of processing outcome — Telegram
        // will NOT retry if 2xx is returned, so we log errors internally instead of
        // surfacing them as 4xx/5xx which would trigger flood-control retries.

        [HttpPost("webhook")]
        [AllowAnonymous]
        [Consumes("application/json")]
        public async Task<IActionResult> HandleWebhook([FromBody] TelegramUpdate? update)
        {
            // ── Guard: bot not configured or update is malformed ──────────────
            if (update is null || update.UpdateId == 0)
            {
                _logger.LogWarning(
                    "[TelegramWebhook] Received null or empty update payload — possible JSON deserialisation failure. " +
                    "Ensure all DTO properties carry [JsonPropertyName] snake_case attributes.");
                // Still return 200 so Telegram does not retry endlessly
                return Ok();
            }

            // ── Guard: bot master-switch (ClinicSettings) ─────────────────────
            var enabled = await _settings.GetSettingValueAsync("EnableTelegramBot");
            if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[TelegramWebhook] Bot is disabled in clinic settings. Ignoring update {UpdateId}.", update.UpdateId);
                return Ok();
            }

            _logger.LogInformation("[TelegramWebhook] Received update {UpdateId} — type: {Type}.",
                update.UpdateId,
                update.Message != null ? "Message" : update.CallbackQuery != null ? "CallbackQuery" : "Unknown");

            // ── Process within the SAME request scope ─────────────────────────
            // ITelegramBotService is Scoped and owns the same DbContext as this
            // controller. ProcessUpdateAsync is awaited synchronously here so we
            // stay within a single scope with no concurrent DbContext access.
            try
            {
                await _botService.ProcessUpdateAsync(update);
            }
            catch (Exception ex)
            {
                // Log but always return 200 — avoids Telegram retry storms.
                _logger.LogError(ex, "[TelegramWebhook] Error processing update {UpdateId}.", update.UpdateId);
            }

            // Telegram requires 200 OK to acknowledge receipt and stop retries.
            return Ok();
        }

        // ── GET /api/telegram/health ──────────────────────────────────────────
        // Optional lightweight probe: confirms the route is reachable before
        // registering the webhook with Telegram. Call from a browser or curl.

        [HttpGet("health")]
        [AllowAnonymous]
        public async Task<IActionResult> Health()
        {
            var tokenSet = await _settings.GetSettingValueAsync("TelegramBotToken");
            var enabled  = await _settings.GetSettingValueAsync("EnableTelegramBot");

            return Ok(new
            {
                route          = "POST /api/telegram/webhook",
                botTokenSet    = !string.IsNullOrWhiteSpace(tokenSet),
                botEnabled     = string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase),
                utc            = DateTime.UtcNow.ToString("o"),
                hint           = "If botTokenSet=false, save the Bot Token in Settings → Automation."
            });
        }

        // ── Background processing helper (for future heavy work) ──────────────
        // If you ever need to process updates in a background thread (e.g. queuing),
        // use this pattern to create an independent DI scope so the background task
        // has its own DbContext instance — never capture the scoped _botService:
        //
        //   _ = Task.Run(async () =>
        //   {
        //       using var scope   = _scopeFactory.CreateScope();
        //       var svc           = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();
        //       await svc.ProcessUpdateAsync(update);
        //   });
        //
        // The _scopeFactory field is already injected for when you need this pattern.
    }
}
