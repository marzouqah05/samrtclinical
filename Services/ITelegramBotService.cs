using System.Text.Json.Serialization;

namespace WebApplication1.Services
{
    /// <summary>
    /// Defines the Telegram Bot webhook dispatcher and alert notification capabilities.
    /// </summary>
    public interface ITelegramBotService
    {
        /// <summary>
        /// Main entry point — processes an incoming Telegram Update object dispatched
        /// from the registered webhook. Routes to the appropriate conversation handler
        /// based on update type (Message, CallbackQuery).
        /// </summary>
        Task ProcessUpdateAsync(TelegramUpdate update);

        /// <summary>
        /// Sends a text alert message directly to a Telegram chat (e.g., a doctor's personal chat).
        /// Fire-and-forget; errors are logged but not thrown.
        /// </summary>
        Task SendAlertAsync(string chatId, string markdownMessage);

        /// <summary>
        /// Sends an Inline Keyboard message to a Telegram chat.
        /// Used for presenting doctor selection and slot confirmation prompts.
        /// </summary>
        Task SendInlineKeyboardAsync(string chatId, string text, List<List<TelegramInlineButton>> keyboard);

        /// <summary>
        /// Resets the conversation state for a specific chat ID.
        /// </summary>
        void ResetSession(string chatId);
    }

    // ── Conversation State Tracker Models ─────────────────────────────────────

    public enum BotStep
    {
        Idle = 0,
        AwaitingLanguage = 1,
        AwaitingPhone = 2,
        AwaitingPatientName = 3,
        AwaitingRegisterName = 4,
        Registering = 5,
        AwaitingRegisterNationalId = 6,
        SelectDoctor = 7,
        BookingDoctor = 8,
        SelectDate = 9,
        BookingDate = 10,
        SelectSlot = 11,
        BookingSlot = 12,
        ConfirmBooking = 13,
        ConfirmCancel = 14
    }

    public class BotSession
    {
        public string ChatId { get; set; } = string.Empty;
        public string Lang { get; set; } = "ar"; // "ar" or "en"
        public BotStep Step { get; set; } = BotStep.Idle;

        public int? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? PhoneNumber { get; set; }

        public int? SelectedDoctorId { get; set; }
        public string? SelectedDoctorName { get; set; }
        public decimal? SelectedDoctorFee { get; set; }

        public DateTime? SelectedDate { get; set; }
        public string? SelectedTime { get; set; }

        public int? CancelAppointmentId { get; set; }

        // Registration wizard scratchpad
        public string? TempFullName { get; set; }
        public string? TempNationalId { get; set; }

        public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;

        public void ResetBookingFlow()
        {
            SelectedDoctorId = null;
            SelectedDoctorName = null;
            SelectedDoctorFee = null;
            SelectedDate = null;
            SelectedTime = null;
            CancelAppointmentId = null;
            TempFullName = null;
            TempNationalId = null;
            Step = BotStep.Idle;
            LastActiveUtc = DateTime.UtcNow;
        }

        public void ResetAll()
        {
            Step = BotStep.Idle;
            PatientId = null;
            PatientName = null;
            PhoneNumber = null;
            ResetBookingFlow();
        }
    }

    public interface ITelegramSessionStore
    {
        BotSession GetOrCreate(string chatId);
        void Update(BotSession session);
        void Clear(string chatId);
    }

    // ── Telegram Update Model ─────────────────────────────────────────────────
    // Minimal subset of the Telegram Bot API Update object.
    // We intentionally avoid a third-party SDK dependency and instead use our own
    // lightweight DTOs. [JsonPropertyName] attributes map Telegram's snake_case JSON
    // wire format to PascalCase C# properties — without these the model silently binds
    // as null/empty because System.Text.Json is case-sensitive by default.

    public class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessage? Message { get; set; }

        [JsonPropertyName("callback_query")]
        public TelegramCallbackQuery? CallbackQuery { get; set; }
    }

    public class TelegramMessage
    {
        [JsonPropertyName("message_id")]
        public long MessageId { get; set; }

        [JsonPropertyName("chat")]
        public TelegramChat? Chat { get; set; }

        [JsonPropertyName("from")]
        public TelegramUser? From { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("contact")]
        public TelegramContact? Contact { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }
    }

    public class TelegramContact
    {
        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }

    public class TelegramCallbackQuery
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public TelegramUser? From { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessage? Message { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    public class TelegramChat
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    public class TelegramUser
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("is_bot")]
        public bool IsBot { get; set; }

        [JsonPropertyName("language_code")]
        public string? LanguageCode { get; set; }
    }

    public class TelegramInlineButton
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("callback_data")]
        public string? CallbackData { get; set; }
    }
}
