using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ClinicDbContext _context;
        // IHttpClientFactory is injected instead of HttpClient directly.
        // HttpClient typed-client registration (AddHttpClient<T>) creates a TRANSIENT factory
        // entry that conflicts with the Scoped DI registration for ISettingsService, causing
        // the DbContext concurrency error when both entries share the same scope.
        private readonly IHttpClientFactory _httpClientFactory;

        public SettingsService(ClinicDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        // ── Default values (single source of truth) ──────────────────────────
        private static readonly Dictionary<string, string> _defaults = new(StringComparer.OrdinalIgnoreCase)
        {
            // Clinic Profile
            ["ClinicName"]                       = "MediCare Specialized Medical Center",
            ["ClinicPhone"]                      = "+962 6 234 5678",
            ["EmergencyPhone"]                   = "+962 799 000 000",
            ["ClinicEmail"]                      = "info@medicare-clinic.com",
            ["ClinicAddress"]                    = "Amman, Jordan",
            ["LogoUrl"]                          = "",
            ["CurrencySymbol"]                   = "JOD",
            ["TaxPercentage"]                    = "16",
            // Scheduling
            ["DefaultAppointmentDurationMinutes"]= "30",
            ["WorkingHoursStart"]                = "08:00",
            ["WorkingHoursEnd"]                  = "20:00",
            ["WorkingDays"]                      = "Sunday, Monday, Tuesday, Wednesday, Thursday",
            ["AutoConfirmAppointments"]          = "False",
            // WhatsApp
            ["WhatsAppWebhookUrl"]               = "",
            ["EnableWhatsAppReminders"]          = "False",
            ["EnableFollowUpMessages"]           = "False",
            ["ReminderHoursBeforeAppointment"]   = "24",
            ["WhatsAppSenderNumber"]             = "",
            ["CustomWhatsAppTemplate"]           = "Dear {PatientName}, this is a reminder from our clinic for your appointment with Dr. {DoctorName} on {AppointmentDate} at {AppointmentTime}.",
            ["EnableWhatsAppSelfBooking"]        = "False",
            ["WeeklyDigestDayTime"]              = "Sunday 07:00",
            ["ApiToken"]                         = "",
            // Telegram Bot
            ["TelegramBotToken"]                  = "",
            ["TelegramWebhookUrl"]                = "",
            ["EnableTelegramBot"]                 = "False",
        };

        public async Task SeedDefaultSettingsIfEmptyAsync()
        {
            var count = await _context.ClinicSettings.CountAsync();
            if (count > 0) return; // Already seeded

            foreach (var kvp in _defaults)
            {
                _context.ClinicSettings.Add(new ClinicSetting
                {
                    Key = kvp.Key,
                    Value = kvp.Value,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task<ClinicSettingsViewModel> GetSettingsAsync()
        {
            // Fetch all key-values from database into a dictionary
            var settingsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var settingsList = await _context.ClinicSettings.AsNoTracking().ToListAsync();
                foreach (var s in settingsList)
                {
                    if (!string.IsNullOrEmpty(s.Key) && s.Value != null)
                        settingsDict[s.Key] = s.Value;
                }
            }
            catch { /* Table may not exist yet during initial migration */ }

            string Get(string key) =>
                settingsDict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val)
                    ? val
                    : (_defaults.TryGetValue(key, out var def) ? def : string.Empty);

            decimal GetDecimal(string key, decimal fallback) =>
                decimal.TryParse(Get(key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

            int GetInt(string key, int fallback) =>
                int.TryParse(Get(key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

            bool GetBool(string key) =>
                bool.TryParse(Get(key), out var v) && v;

            var model = new ClinicSettingsViewModel
            {
                // Clinic Profile
                ClinicName                     = Get("ClinicName"),
                ClinicPhone                    = Get("ClinicPhone"),
                EmergencyPhone                 = Get("EmergencyPhone"),
                ClinicEmail                    = Get("ClinicEmail"),
                ClinicAddress                  = Get("ClinicAddress"),
                LogoPath                       = Get("LogoUrl"),
                CurrencySymbol                 = Get("CurrencySymbol"),
                TaxPercentage                  = GetDecimal("TaxPercentage", 16m),
                // Scheduling
                DefaultSlotDurationMinutes     = GetInt("DefaultAppointmentDurationMinutes", 30),
                WorkingHoursStart              = Get("WorkingHoursStart"),
                WorkingHoursEnd                = Get("WorkingHoursEnd"),
                WorkingDays                    = Get("WorkingDays"),
                AutoConfirmAppointments        = GetBool("AutoConfirmAppointments"),
                // WhatsApp
                WhatsAppWebhookUrl             = Get("WhatsAppWebhookUrl"),
                EnableWhatsAppReminders        = GetBool("EnableWhatsAppReminders"),
                EnableFollowUpMessages         = GetBool("EnableFollowUpMessages"),
                ReminderHoursBefore            = GetInt("ReminderHoursBeforeAppointment", 24),
                WhatsAppSenderNumber           = Get("WhatsAppSenderNumber"),
                CustomWhatsAppTemplate         = Get("CustomWhatsAppTemplate"),
                EnableWhatsAppSelfBooking      = GetBool("EnableWhatsAppSelfBooking"),
                WeeklyDigestDayTime            = Get("WeeklyDigestDayTime") is { Length: > 0 } v ? v : "Sunday 07:00",
                ApiToken                       = Get("ApiToken"),
                // Telegram
                TelegramBotToken               = Get("TelegramBotToken"),
                TelegramWebhookUrl             = Get("TelegramWebhookUrl"),
                EnableTelegramBot              = GetBool("EnableTelegramBot"),
            };

            // Database metrics for Backup tab
            try
            {
                model.TotalPatients     = await _context.Patients.CountAsync();
                model.TotalDoctors      = await _context.Doctors.CountAsync();
                model.TotalAppointments = await _context.Appointments.CountAsync();
                model.TotalInvoices     = await _context.Invoices.CountAsync();
            }
            catch { }

            return model;
        }

        public async Task SaveGeneralSettingsAsync(ClinicSettingsViewModel model)
        {
            // ── CONCURRENCY FIX ────────────────────────────────────────────────────────
            // Load ALL relevant keys in a single query, apply all changes to the EF
            // change tracker in-memory via the synchronous UpsertTracked helper, then
            // flush with a single SaveChangesAsync. This replaces the previous pattern of
            // calling SetSettingValueAsync (which does FirstOrDefaultAsync + SaveChangesAsync)
            // N times, which caused 2N DB operations and triggered the "second operation
            // started on this context" concurrency exception from shared middleware access.
            var keys = new[]
            {
                "ClinicName", "ClinicPhone", "EmergencyPhone", "ClinicEmail",
                "ClinicAddress", "LogoUrl", "CurrencySymbol", "TaxPercentage"
            };
            var existing = await _context.ClinicSettings
                .Where(s => keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key);

            UpsertTracked(existing, "ClinicName",     model.ClinicName,     "Official name of the medical clinic");
            UpsertTracked(existing, "ClinicPhone",    model.ClinicPhone,    "Primary contact phone number");
            UpsertTracked(existing, "EmergencyPhone", model.EmergencyPhone, "Emergency contact phone number");
            UpsertTracked(existing, "ClinicEmail",    model.ClinicEmail,    "Official clinic email address");
            UpsertTracked(existing, "ClinicAddress",  model.ClinicAddress,  "Physical address location");
            UpsertTracked(existing, "LogoUrl",        model.LogoPath,       "Clinic logo path or URL");
            UpsertTracked(existing, "CurrencySymbol", model.CurrencySymbol, "Billing and invoice currency symbol");
            UpsertTracked(existing, "TaxPercentage",  model.TaxPercentage.ToString(CultureInfo.InvariantCulture),
                          "Default VAT / Tax percentage");

            await _context.SaveChangesAsync(); // Single flush — 1 DB write instead of 16
        }

        public async Task SaveWorkingHoursSettingsAsync(ClinicSettingsViewModel model)
        {
            var keys = new[]
            {
                "DefaultAppointmentDurationMinutes", "WorkingHoursStart",
                "WorkingHoursEnd", "WorkingDays", "AutoConfirmAppointments"
            };
            var existing = await _context.ClinicSettings
                .Where(s => keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key);

            UpsertTracked(existing, "DefaultAppointmentDurationMinutes",
                          model.DefaultSlotDurationMinutes.ToString(), "Default appointment slot duration in minutes");
            UpsertTracked(existing, "WorkingHoursStart",       model.WorkingHoursStart,                    "Daily clinic opening time");
            UpsertTracked(existing, "WorkingHoursEnd",         model.WorkingHoursEnd,                      "Daily clinic closing time");
            UpsertTracked(existing, "WorkingDays",             model.WorkingDays,                          "Weekly working days");
            UpsertTracked(existing, "AutoConfirmAppointments", model.AutoConfirmAppointments.ToString(),    "Whether online bookings are confirmed automatically");

            await _context.SaveChangesAsync(); // Single flush — 1 DB write instead of 10
        }

        public async Task SaveIntegrationSettingsAsync(ClinicSettingsViewModel model)
        {
            // PRIMARY FIX: Previous pattern called SetSettingValueAsync 12 times, each
            // issuing FirstOrDefaultAsync + SaveChangesAsync = 24 DB operations. This caused
            // "A second operation was started on this context instance" because UserActivity
            // middleware and background digest services share the same scoped DbContext and
            // can touch it while one of those 24 async state-machine continuations is
            // suspended waiting on the DB. Now: one query, in-memory upserts, one flush.
            var keys = new[]
            {
                "WhatsAppWebhookUrl", "EnableWhatsAppReminders", "EnableFollowUpMessages",
                "ReminderHoursBeforeAppointment", "WhatsAppSenderNumber", "CustomWhatsAppTemplate",
                "EnableWhatsAppSelfBooking", "WeeklyDigestDayTime", "ApiToken",
                "TelegramBotToken", "TelegramWebhookUrl", "EnableTelegramBot"
            };
            var existing = await _context.ClinicSettings
                .Where(s => keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key);

            UpsertTracked(existing, "WhatsAppWebhookUrl",             model.WhatsAppWebhookUrl,
                          "n8n / WhatsApp automated webhook destination");
            UpsertTracked(existing, "EnableWhatsAppReminders",        model.EnableWhatsAppReminders.ToString(),
                          "Master toggle for WhatsApp reminder notifications");
            UpsertTracked(existing, "EnableFollowUpMessages",         model.EnableFollowUpMessages.ToString(),
                          "Toggle for post-visit follow-up messages");
            UpsertTracked(existing, "ReminderHoursBeforeAppointment", model.ReminderHoursBefore.ToString(),
                          "Timing in hours before appointment to send automated reminders");
            UpsertTracked(existing, "WhatsAppSenderNumber",           model.WhatsAppSenderNumber,
                          "Clinic WhatsApp sender identification phone number");
            UpsertTracked(existing, "CustomWhatsAppTemplate",         model.CustomWhatsAppTemplate,
                          "Message template body for WhatsApp reminders");
            UpsertTracked(existing, "EnableWhatsAppSelfBooking",      model.EnableWhatsAppSelfBooking.ToString(),
                          "Toggle for WhatsApp AI/Bot self-booking via n8n");
            UpsertTracked(existing, "WeeklyDigestDayTime",            model.WeeklyDigestDayTime,
                          "Day and time for weekly doctor schedule digest emails (e.g. Sunday 07:00)");
            UpsertTracked(existing, "ApiToken",                       model.ApiToken,
                          "Shared secret token for securing the public booking API endpoints");
            UpsertTracked(existing, "TelegramBotToken",               model.TelegramBotToken,
                          "Telegram Bot API token from BotFather");
            UpsertTracked(existing, "TelegramWebhookUrl",             model.TelegramWebhookUrl,
                          "Telegram webhook endpoint URL registered with BotFather");
            UpsertTracked(existing, "EnableTelegramBot",              model.EnableTelegramBot.ToString(),
                          "Master toggle for the Telegram AI Assistant bot");

            await _context.SaveChangesAsync(); // Single flush — 1 DB write instead of 24
        }

        public async Task<string?> GetSettingValueAsync(string key, string? defaultValue = null)
        {
            var setting = await _context.ClinicSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value ?? defaultValue;
        }

        public async Task SetSettingValueAsync(string key, string? value, string? description = null)
        {
            // Individual single-key save — kept for external callers that update one key at
            // a time (e.g., background services). Safe because it issues exactly one
            // query and one SaveChangesAsync with no concurrent sibling awaits.
            var setting = await _context.ClinicSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                setting = new ClinicSetting
                {
                    Key         = key,
                    Value       = value,
                    Description = description,
                    UpdatedAt   = DateTime.UtcNow
                };
                _context.ClinicSettings.Add(setting);
            }
            else
            {
                setting.Value = value;
                if (!string.IsNullOrEmpty(description))
                    setting.Description = description;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        // ── Private Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Synchronous upsert helper — operates ONLY on the EF change tracker.
        /// Makes zero DB calls. Call <c>SaveChangesAsync</c> once after all upserts.
        /// </summary>
        private void UpsertTracked(
            Dictionary<string, ClinicSetting> existing,
            string key,
            string? value,
            string? description = null)
        {
            if (existing.TryGetValue(key, out var setting))
            {
                // Entity is already tracked — mutate in-place
                setting.Value     = value;
                if (!string.IsNullOrEmpty(description))
                    setting.Description = description;
                setting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // New key — add to tracker; EF will INSERT on SaveChanges
                _context.ClinicSettings.Add(new ClinicSetting
                {
                    Key         = key,
                    Value       = value,
                    Description = description,
                    UpdatedAt   = DateTime.UtcNow,
                });
            }
        }

        public async Task<(bool Success, string Message)> TestWhatsAppWebhookAsync(string webhookUrl)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl) || !Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri))
                return (false, "Invalid Webhook URL format. Please provide a valid HTTP/HTTPS endpoint.");

            try
            {
                var payload = new
                {
                    @event    = "test_ping",
                    timestamp = DateTime.UtcNow.ToString("o"),
                    clinic    = "MediCare Clinic System",
                    message   = "Ping test from MediCare Settings Hub",
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                // Create a short-lived HttpClient from the factory — avoids injecting
                // HttpClient directly (which conflicts with Scoped DI registration).
                using var httpClient = _httpClientFactory.CreateClient(nameof(SettingsService));
                var response = await httpClient.PostAsync(uri, content, cts.Token);

                return response.IsSuccessStatusCode
                    ? (true,  $"✅ Webhook verified! Server responded HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).")
                    : (false, $"⚠️ Webhook reached but returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }
            catch (TaskCanceledException)
            {
                return (false, "⏱️ Webhook timed out (no response in 8 seconds). Please check the server address.");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Failed to reach webhook endpoint: {ex.Message}");
            }
        }
    }
}
