using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public interface ISettingsService
    {
        /// <summary>
        /// Retrieves the consolidated settings ViewModel from the database with all configuration values and stats.
        /// </summary>
        Task<ClinicSettingsViewModel> GetSettingsAsync();

        /// <summary>
        /// Saves General &amp; Clinic Profile settings (name, phone, emergency phone, email, address, logo, currency, tax).
        /// </summary>
        Task SaveGeneralSettingsAsync(ClinicSettingsViewModel model);

        /// <summary>
        /// Saves Working Hours &amp; Appointment parameters (slot duration, start/end time, working days, auto-confirm).
        /// </summary>
        Task SaveWorkingHoursSettingsAsync(ClinicSettingsViewModel model);

        /// <summary>
        /// Saves WhatsApp &amp; Integration settings (Webhook URL, toggle reminders, follow-ups, sender ID, reminder hours, template).
        /// </summary>
        Task SaveIntegrationSettingsAsync(ClinicSettingsViewModel model);

        /// <summary>
        /// Retrieves a specific setting value by its key with fallback.
        /// </summary>
        Task<string?> GetSettingValueAsync(string key, string? defaultValue = null);

        /// <summary>
        /// Sets or updates a single key-value setting in the database.
        /// </summary>
        Task SetSettingValueAsync(string key, string? value, string? description = null);

        /// <summary>
        /// Tests the connectivity of the specified WhatsApp / n8n Webhook URL.
        /// </summary>
        Task<(bool Success, string Message)> TestWhatsAppWebhookAsync(string webhookUrl);

        /// <summary>
        /// Seeds default settings into the database if no settings exist yet.
        /// </summary>
        Task SeedDefaultSettingsIfEmptyAsync();
    }
}
