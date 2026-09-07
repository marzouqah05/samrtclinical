using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class ClinicSettingsViewModel
    {
        // ── 🏥 1. Clinic Profile & Branding ─────────────────────────────────
        [Required(ErrorMessage = "Clinic name is required")]
        [Display(Name = "Clinic Name")]
        [MaxLength(150)]
        public string ClinicName { get; set; } = "MediCare Specialized Medical Center";

        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Clinic Phone")]
        [MaxLength(30)]
        public string ClinicPhone { get; set; } = "+962 6 234 5678";

        [Display(Name = "Emergency Phone")]
        [MaxLength(30)]
        public string EmergencyPhone { get; set; } = "+962 799 000 000";

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Clinic Email")]
        [MaxLength(100)]
        public string ClinicEmail { get; set; } = "info@medicare-clinic.com";

        [Display(Name = "Clinic Address")]
        [MaxLength(250)]
        public string ClinicAddress { get; set; } = "Amman, Jordan";

        [Display(Name = "Logo Path")]
        public string? LogoPath { get; set; }

        [Required(ErrorMessage = "Currency symbol is required")]
        [Display(Name = "Currency Symbol")]
        [MaxLength(10)]
        public string CurrencySymbol { get; set; } = "JOD";

        [Range(0, 100, ErrorMessage = "Tax percentage must be between 0 and 100")]
        [Display(Name = "Tax Percentage (%)")]
        public decimal TaxPercentage { get; set; } = 16.0m;


        // ── ⏰ 2. Operating Hours & Scheduling ──────────────────────────────
        [Range(5, 240, ErrorMessage = "Appointment duration must be between 5 and 240 minutes")]
        [Display(Name = "Default Slot Duration (Minutes)")]
        public int DefaultSlotDurationMinutes { get; set; } = 30;

        [Required]
        [Display(Name = "Working Hours Start")]
        public string WorkingHoursStart { get; set; } = "08:00";

        [Required]
        [Display(Name = "Working Hours End")]
        public string WorkingHoursEnd { get; set; } = "20:00";

        [Display(Name = "Working Days")]
        public string WorkingDays { get; set; } = "Sunday, Monday, Tuesday, Wednesday, Thursday";

        [Display(Name = "Auto-Confirm New Bookings")]
        public bool AutoConfirmAppointments { get; set; } = false;


        // ── 💬 3. WhatsApp & Automation ──────────────────────────────────────
        [Display(Name = "WhatsApp / n8n Webhook URL")]
        public string? WhatsAppWebhookUrl { get; set; }

        [Display(Name = "Enable Automated WhatsApp Reminders")]
        public bool EnableWhatsAppReminders { get; set; } = false;

        [Display(Name = "Enable Follow-Up Messages")]
        public bool EnableFollowUpMessages { get; set; } = false;

        [Range(1, 168, ErrorMessage = "Reminder timing must be between 1 and 168 hours")]
        [Display(Name = "Reminder Hours Before Appointment")]
        public int ReminderHoursBefore { get; set; } = 24;

        [Display(Name = "WhatsApp Sender Number")]
        [MaxLength(30)]
        public string? WhatsAppSenderNumber { get; set; }

        [Display(Name = "Custom Reminder Template")]
        public string? CustomWhatsAppTemplate { get; set; } = "Dear {PatientName}, this is a reminder from our clinic for your appointment with Dr. {DoctorName} on {AppointmentDate} at {AppointmentTime}.";

        [Display(Name = "Enable WhatsApp Self-Booking (AI/Bot)")]
        public bool EnableWhatsAppSelfBooking { get; set; } = false;

        [Display(Name = "Weekly Schedule Digest Day/Time")]
        [MaxLength(20)]
        public string WeeklyDigestDayTime { get; set; } = "Sunday 07:00";

        [Display(Name = "API Token (for n8n / Webhook Security)")]
        [MaxLength(100)]
        public string? ApiToken { get; set; }


        // ── 🤖 5. Telegram Bot Integration ─────────────────────────────
        [Display(Name = "Telegram Bot Token")]
        [MaxLength(200)]
        public string? TelegramBotToken { get; set; }

        [Display(Name = "Telegram Clinic Webhook URL")]
        [MaxLength(500)]
        public string? TelegramWebhookUrl { get; set; }

        [Display(Name = "Enable Telegram AI Assistant")]
        public bool EnableTelegramBot { get; set; } = false;


        // ── 🔒 4. Security & Password Change ────────────────────────────────
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 6)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation do not match.")]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmPassword { get; set; }


        // ── System Stats Helpers ─────────────────────────────────────────────
        public int TotalPatients { get; set; }
        public int TotalDoctors { get; set; }
        public int TotalAppointments { get; set; }
        public int TotalInvoices { get; set; }
        public string DatabaseSizeInfo { get; set; } = "SQL Server LocalDB — ClinicDB";

        // ── Backup & Cloud Sync Status ───────────────────────────────────────
        public WebApplication1.Services.CloudSyncStatus CloudSync { get; set; } = new();
        public List<WebApplication1.Services.BackupFileInfo> RecentBackups { get; set; } = new();
        public bool IsMonthlyReminderActive { get; set; }

        // Backward-compat aliases (used by existing SettingsService)
        [Display(Name = "Default Appointment Duration (Minutes)")]
        public int DefaultAppointmentDurationMinutes
        {
            get => DefaultSlotDurationMinutes;
            set => DefaultSlotDurationMinutes = value;
        }

        [Display(Name = "Logo URL")]
        public string LogoUrl
        {
            get => LogoPath ?? string.Empty;
            set => LogoPath = value;
        }

        [Display(Name = "Reminder Hours Before Appointment")]
        public int ReminderHoursBeforeAppointment
        {
            get => ReminderHoursBefore;
            set => ReminderHoursBefore = value;
        }
    }
}
