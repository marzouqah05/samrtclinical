using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Interactive bilingual Telegram Bot service for the Clinic Management System.
    /// Manages patient identification, multi-step appointment booking, schedule lookup,
    /// and cancellation workflows with full Arabic and English support.
    /// </summary>
    public class TelegramBotService : ITelegramBotService
    {
        private readonly ClinicDbContext _context;
        private readonly ISettingsService _settings;
        private readonly ITelegramSessionStore _sessionStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TelegramBotService> _logger;

        private const string TelegramApiBase = "https://api.telegram.org/bot";

        // Callback Query Routing Prefixes
        private const string PrefixLang = "LANG:";
        private const string PrefixMenu = "MENU:";
        private const string PrefixReg = "REG:";
        private const string PrefixDoctor = "DOC:";
        private const string PrefixDate = "DATE:";
        private const string PrefixSlot = "SLOT:";
        private const string PrefixConfirm = "CONFIRM:";
        private const string PrefixAbort = "ABORT:";
        private const string PrefixCancelAppt = "CXAPPT:";

        public TelegramBotService(
            ClinicDbContext context,
            ISettingsService settings,
            ITelegramSessionStore sessionStore,
            IHttpClientFactory httpClientFactory,
            ILogger<TelegramBotService> logger)
        {
            _context = context;
            _settings = settings;
            _sessionStore = sessionStore;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // ── Main Webhook Entry Point ──────────────────────────────────────────

        public async Task ProcessUpdateAsync(TelegramUpdate update)
        {
            try
            {
                if (update.CallbackQuery != null)
                {
                    await HandleCallbackQueryAsync(update.CallbackQuery);
                    return;
                }

                if (update.Message != null)
                {
                    await HandleMessageAsync(update.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TelegramBot] Error processing update {UpdateId}", update.UpdateId);
            }
        }

        public void ResetSession(string chatId)
        {
            _sessionStore.Clear(chatId);
        }

        // ── Message Handler ────────────────────────────────────────────────────

        private async Task HandleMessageAsync(TelegramMessage message)
        {
            var chatId = message.Chat?.Id.ToString();
            if (string.IsNullOrEmpty(chatId)) return;

            var session = _sessionStore.GetOrCreate(chatId);
            var text = message.Text?.Trim() ?? string.Empty;

            _logger.LogInformation("[TelegramBot] Chat {ChatId} (Step {Step}): Text='{Text}'", chatId, session.Step, text);

            // Handle Contact sharing card if present
            if (message.Contact != null && !string.IsNullOrWhiteSpace(message.Contact.PhoneNumber))
            {
                await HandlePhoneInputAsync(session, message.Contact.PhoneNumber);
                return;
            }

            // Global slash commands
            if (text.StartsWith("/"))
            {
                var cmd = text.ToLowerInvariant().Split(' ')[0];
                switch (cmd)
                {
                    case "/start":
                        await HandleStartCommandAsync(session, text);
                        return;

                    case "/cancel":
                    case "/reset":
                        session.ResetBookingFlow();
                        _sessionStore.Update(session);
                        await SendTextAsync(chatId, BotStrings.Get("OperationCancelled", session.Lang));
                        if (session.PatientId.HasValue)
                        {
                            await SendMainMenuAsync(session);
                        }
                        return;

                    case "/language":
                    case "/lang":
                        await SendLanguageSelectorAsync(chatId);
                        return;

                    case "/menu":
                        if (session.PatientId.HasValue)
                        {
                            await SendMainMenuAsync(session);
                        }
                        else
                        {
                            await PromptForPhoneAsync(session);
                        }
                        return;
                }
            }

            // Route based on current conversation state
            switch (session.Step)
            {
                case BotStep.AwaitingLanguage:
                    var detected = BotStrings.DetectLanguage(text);
                    session.Lang = detected;
                    session.Step = BotStep.AwaitingPhone;
                    _sessionStore.Update(session);
                    await PromptForPhoneAsync(session);
                    break;

                case BotStep.AwaitingPhone:
                    await HandlePhoneInputAsync(session, text);
                    break;

                case BotStep.AwaitingPatientName:
                case BotStep.AwaitingRegisterName:
                    await HandleRegisterNameInputAsync(session, text);
                    break;

                default:
                    // If text looks like a phone number, verify it
                    if (IsLikelyPhoneNumber(text))
                    {
                        await HandlePhoneInputAsync(session, text);
                    }
                    else if (session.PatientId.HasValue)
                    {
                        await SendMainMenuAsync(session);
                    }
                    else
                    {
                        // Check language from text and prompt phone
                        session.Lang = BotStrings.DetectLanguage(text);
                        session.Step = BotStep.AwaitingPhone;
                        _sessionStore.Update(session);
                        await PromptForPhoneAsync(session);
                    }
                    break;
            }
        }

        // ── Callback Query Handler ─────────────────────────────────────────────

        private async Task HandleCallbackQueryAsync(TelegramCallbackQuery callback)
        {
            var chatId = callback.Message?.Chat?.Id.ToString() ?? callback.From?.Id.ToString();
            if (string.IsNullOrEmpty(chatId)) return;

            // Acknowledge callback query to stop loading spinner
            await AnswerCallbackQueryAsync(callback.Id);

            var session = _sessionStore.GetOrCreate(chatId);
            var data = callback.Data ?? string.Empty;

            _logger.LogInformation("[TelegramBot] Callback Chat {ChatId}: Data='{Data}'", chatId, data);

            // 1. Language Selection: LANG:ar | LANG:en
            if (data.StartsWith(PrefixLang))
            {
                var lang = data[PrefixLang.Length..];
                session.Lang = BotStrings.NormalizeLang(lang);
                session.Step = session.PatientId.HasValue ? BotStep.Idle : BotStep.AwaitingPhone;
                _sessionStore.Update(session);

                if (session.PatientId.HasValue)
                {
                    await SendMainMenuAsync(session);
                }
                else
                {
                    await PromptForPhoneAsync(session);
                }
                return;
            }

            // 2. Menu navigation: MENU:BOOK | MENU:MYAPPTS | MENU:CANCEL | MENU:LANG | MENU:REENTER
            if (data.StartsWith(PrefixMenu))
            {
                var action = data[PrefixMenu.Length..];
                switch (action)
                {
                    case "BOOK":
                        await StartBookingFlowAsync(session);
                        break;
                    case "MYAPPTS":
                        await ShowMyAppointmentsAsync(session);
                        break;
                    case "CANCEL":
                        await StartCancellationFlowAsync(session);
                        break;
                    case "LANG":
                        await SendLanguageSelectorAsync(chatId);
                        break;
                    case "MENU":
                        session.ResetBookingFlow();
                        _sessionStore.Update(session);
                        await SendMainMenuAsync(session);
                        break;
                    case "REENTER":
                        session.ResetAll();
                        session.Step = BotStep.AwaitingPhone;
                        _sessionStore.Update(session);
                        await PromptForPhoneAsync(session);
                        break;
                }
                return;
            }

            // 3. Registration wizard: REG:START | REG:REENTER
            if (data.StartsWith(PrefixReg))
            {
                var action = data[PrefixReg.Length..];
                if (action == "START")
                {
                    session.Step = BotStep.AwaitingPatientName;
                    _sessionStore.Update(session);
                    await SendTextAsync(chatId, BotStrings.Get("PromptPatientName", session.Lang));
                }
                else if (action == "REENTER")
                {
                    session.Step = BotStep.AwaitingPhone;
                    _sessionStore.Update(session);
                    await PromptForPhoneAsync(session);
                }
                return;
            }

            // 4. Doctor Selection: DOC:{doctorId}
            if (data.StartsWith(PrefixDoctor))
            {
                var idStr = data[PrefixDoctor.Length..];
                if (int.TryParse(idStr, out var docId))
                {
                    await HandleDoctorChosenAsync(session, docId);
                }
                return;
            }

            // 5. Date Selection: DATE:{yyyy-MM-dd}
            if (data.StartsWith(PrefixDate))
            {
                var dateStr = data[PrefixDate.Length..];
                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    await HandleDateChosenAsync(session, date);
                }
                return;
            }

            // 6. Slot Selection: SLOT:{HH:mm}
            if (data.StartsWith(PrefixSlot))
            {
                var slot = data[PrefixSlot.Length..];
                await HandleSlotChosenAsync(session, slot);
                return;
            }

            // 7. Confirm or Abort Booking: CONFIRM:BOOK | ABORT:BOOK
            if (data == $"{PrefixConfirm}BOOK")
            {
                await FinalizeBookingAsync(session);
                return;
            }
            if (data == $"{PrefixAbort}BOOK")
            {
                session.ResetBookingFlow();
                _sessionStore.Update(session);
                await SendTextAsync(chatId, BotStrings.Get("OperationCancelled", session.Lang));
                await SendMainMenuAsync(session);
                return;
            }

            // 8. Cancellation flow: CXAPPT:{appointmentId}
            if (data.StartsWith(PrefixCancelAppt))
            {
                var apptIdStr = data[PrefixCancelAppt.Length..];
                if (int.TryParse(apptIdStr, out var apptId))
                {
                    await HandleCancelApptChosenAsync(session, apptId);
                }
                return;
            }

            // 9. Confirm or Abort Cancellation: CONFIRM:CANCEL | ABORT:CANCEL
            if (data == $"{PrefixConfirm}CANCEL")
            {
                await FinalizeCancellationAsync(session);
                return;
            }
            if (data == $"{PrefixAbort}CANCEL")
            {
                session.CancelAppointmentId = null;
                session.Step = BotStep.Idle;
                _sessionStore.Update(session);
                await SendTextAsync(chatId, BotStrings.Get("OperationCancelled", session.Lang));
                await SendMainMenuAsync(session);
                return;
            }
        }

        // ── Conversation Flows ─────────────────────────────────────────────────

        private async Task HandleStartCommandAsync(BotSession session, string rawText)
        {
            // If the user sent "/start" with Arabic characters or subsequent text, detect
            var remainder = rawText.Length > 6 ? rawText[6..].Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(remainder))
            {
                session.Lang = BotStrings.DetectLanguage(remainder);
            }

            // Always present bilingual language selector on /start
            await SendLanguageSelectorAsync(session.ChatId);
        }

        private async Task SendLanguageSelectorAsync(string chatId)
        {
            var keyboard = new List<List<TelegramInlineButton>>
            {
                new()
                {
                    new() { Text = BotStrings.Get("BtnArabic", "ar"), CallbackData = $"{PrefixLang}ar" },
                    new() { Text = BotStrings.Get("BtnEnglish", "en"), CallbackData = $"{PrefixLang}en" }
                }
            };

            await SendInlineKeyboardAsync(chatId, BotStrings.Get("ChooseLanguagePrompt", "ar"), keyboard);
        }

        private async Task PromptForPhoneAsync(BotSession session)
        {
            session.Step = BotStep.AwaitingPhone;
            _sessionStore.Update(session);
            await SendTextAsync(session.ChatId, BotStrings.Get("PromptPhone", session.Lang));
        }

        private async Task HandlePhoneInputAsync(BotSession session, string phoneRaw)
        {
            var cleanPhone = CleanPhoneNumber(phoneRaw);
            if (cleanPhone.Length < 7)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("InvalidPhone", session.Lang));
                return;
            }

            session.PhoneNumber = cleanPhone;

            // Search DB for patient
            var phoneNoLeadingZero = cleanPhone.TrimStart('0');
            var patient = await _context.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.PhoneNumber == cleanPhone ||
                    p.PhoneNumber == phoneRaw ||
                    p.PhoneNumber.EndsWith(phoneNoLeadingZero) ||
                    cleanPhone.EndsWith(p.PhoneNumber.TrimStart('0')));

            if (patient != null)
            {
                session.PatientId = patient.PatientId;
                session.PatientName = patient.PatientName;
                session.Step = BotStep.Idle;
                _sessionStore.Update(session);

                var greeting = BotStrings.Get("GreetingExisting", session.Lang, patient.PatientName, patient.PatientId);
                await SendTextAsync(session.ChatId, greeting);
                await SendMainMenuAsync(session);
            }
            else
            {
                session.Step = BotStep.Idle;
                _sessionStore.Update(session);

                var notFoundMsg = BotStrings.Get("PatientNotFound", session.Lang);
                var keyboard = new List<List<TelegramInlineButton>>
                {
                    new()
                    {
                        new() { Text = BotStrings.Get("BtnRegisterNew", session.Lang), CallbackData = $"{PrefixReg}START" },
                        new() { Text = BotStrings.Get("BtnReenterPhone", session.Lang), CallbackData = $"{PrefixReg}REENTER" }
                    }
                };

                await SendInlineKeyboardAsync(session.ChatId, notFoundMsg, keyboard);
            }
        }

        // ── Registration Intake & Auto-Transition ──────────────────────────────

        private async Task HandleRegisterNameInputAsync(BotSession session, string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 3)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("PromptPatientName", session.Lang));
                return;
            }

            var cleanName = fullName.Trim();
            var cleanPhone = CleanPhoneNumber(session.PhoneNumber ?? "");
            var digitsOnly = cleanPhone.Length > 0 ? cleanPhone : "1234567890";
            var nationalId = digitsOnly.Length >= 10
                ? digitsOnly.Substring(digitsOnly.Length - 10)
                : digitsOnly.PadLeft(10, '9');

            // Create new Patient entity
            var newPatient = new Patient
            {
                PatientName = cleanName,
                PhoneNumber = session.PhoneNumber ?? string.Empty,
                NationalId = nationalId,
                DOB = new DateTime(1995, 1, 1)
            };

            _context.Patients.Add(newPatient);
            await _context.SaveChangesAsync();

            // Store newly generated PatientId into current ConversationState
            session.PatientId = newPatient.PatientId;
            session.PatientName = newPatient.PatientName;
            session.Step = BotStep.BookingDoctor;
            session.TempFullName = null;
            session.TempNationalId = null;
            _sessionStore.Update(session);

            // Notify user:
            // AR: "تم فتح ملفك الطبي بنجاح! رقم الملف: #{Patient.Id}\nدعنا نقوم بحجز موعدك الأول الآن."
            // EN: "Medical profile created successfully! Patient ID: #{Patient.Id}\nLet's book your first appointment now."
            var notifyMsg = BotStrings.Get("RegisterSuccessAutoBook", session.Lang, newPatient.PatientId);
            await SendTextAsync(session.ChatId, notifyMsg);

            // Automatically trigger the doctor selection step without requiring user to re-enter anything
            await StartBookingFlowAsync(session);
        }

        // ── Main Menu ──────────────────────────────────────────────────────────

        private async Task SendMainMenuAsync(BotSession session)
        {
            var keyboard = new List<List<TelegramInlineButton>>
            {
                new()
                {
                    new() { Text = BotStrings.Get("BtnBookAppt", session.Lang), CallbackData = $"{PrefixMenu}BOOK" }
                },
                new()
                {
                    new() { Text = BotStrings.Get("BtnMyAppts", session.Lang), CallbackData = $"{PrefixMenu}MYAPPTS" },
                    new() { Text = BotStrings.Get("BtnCancelAppt", session.Lang), CallbackData = $"{PrefixMenu}CANCEL" }
                },
                new()
                {
                    new() { Text = BotStrings.Get("BtnChangeLang", session.Lang), CallbackData = $"{PrefixMenu}LANG" }
                }
            };

            await SendInlineKeyboardAsync(session.ChatId, BotStrings.Get("MainMenuPrompt", session.Lang), keyboard);
        }

        // ── Booking Flow ───────────────────────────────────────────────────────

        private async Task StartBookingFlowAsync(BotSession session)
        {
            if (!session.PatientId.HasValue)
            {
                await PromptForPhoneAsync(session);
                return;
            }

            session.ResetBookingFlow();
            session.Step = BotStep.BookingDoctor;
            _sessionStore.Update(session);

            var doctors = await _context.Doctors
                .AsNoTracking()
                .Include(d => d.Department)
                .OrderBy(d => d.DoctorName)
                .ToListAsync();

            if (doctors.Count == 0)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("NoDoctorsAvailable", session.Lang));
                return;
            }

            var keyboard = doctors.Select(d => new List<TelegramInlineButton>
            {
                new()
                {
                    Text = $"👨‍⚕️ {d.DoctorName} ({d.Specialization})",
                    CallbackData = $"{PrefixDoctor}{d.DoctorId}"
                }
            }).ToList();

            keyboard.Add(new List<TelegramInlineButton>
            {
                new() { Text = BotStrings.Get("BtnBack", session.Lang), CallbackData = $"{PrefixMenu}MENU" }
            });

            await SendInlineKeyboardAsync(session.ChatId, BotStrings.Get("SelectDoctorHeader", session.Lang), keyboard);
        }

        private async Task HandleDoctorChosenAsync(BotSession session, int doctorId)
        {
            var doctor = await _context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.DoctorId == doctorId);
            if (doctor == null)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("GeneralError", session.Lang));
                return;
            }

            session.SelectedDoctorId = doctor.DoctorId;
            session.SelectedDoctorName = doctor.DoctorName;
            session.SelectedDoctorFee = doctor.ConsultationFee;
            session.Step = BotStep.BookingDate;
            _sessionStore.Update(session);

            // Build Date Picker buttons: Today, Tomorrow, and next 5 days
            var today = DateTime.UtcNow.Date;
            var keyboard = new List<List<TelegramInlineButton>>();

            var isArabic = session.Lang == "ar";
            var culture = isArabic ? new CultureInfo("ar-JO") : CultureInfo.InvariantCulture;

            // Row 1: Today & Tomorrow
            keyboard.Add(new List<TelegramInlineButton>
            {
                new()
                {
                    Text = $"{BotStrings.Get("BtnToday", session.Lang)} ({today:MM/dd})",
                    CallbackData = $"{PrefixDate}{today:yyyy-MM-dd}"
                },
                new()
                {
                    Text = $"{BotStrings.Get("BtnTomorrow", session.Lang)} ({today.AddDays(1):MM/dd})",
                    CallbackData = $"{PrefixDate}{today.AddDays(1):yyyy-MM-dd}"
                }
            });

            // Rows 2 & 3: Next 4 days
            var nextDays = new List<TelegramInlineButton>();
            for (int i = 2; i <= 5; i++)
            {
                var candidate = today.AddDays(i);
                var dayName = candidate.ToString("ddd", culture);
                nextDays.Add(new TelegramInlineButton
                {
                    Text = $"{dayName} ({candidate:MM/dd})",
                    CallbackData = $"{PrefixDate}{candidate:yyyy-MM-dd}"
                });
            }

            keyboard.Add(nextDays.Take(2).ToList());
            keyboard.Add(nextDays.Skip(2).Take(2).ToList());

            // Back button
            keyboard.Add(new List<TelegramInlineButton>
            {
                new() { Text = BotStrings.Get("BtnBack", session.Lang), CallbackData = $"{PrefixMenu}BOOK" }
            });

            var header = BotStrings.Get("SelectDateHeader", session.Lang, doctor.DoctorName);
            await SendInlineKeyboardAsync(session.ChatId, header, keyboard);
        }

        private async Task HandleDateChosenAsync(BotSession session, DateTime date)
        {
            if (!session.SelectedDoctorId.HasValue)
            {
                await StartBookingFlowAsync(session);
                return;
            }

            session.SelectedDate = date.Date;
            session.Step = BotStep.BookingSlot;
            _sessionStore.Update(session);

            var doctorId = session.SelectedDoctorId.Value;
            var cfg = await _settings.GetSettingsAsync();

            if (!TimeSpan.TryParse(cfg.WorkingHoursStart, out var shiftStart))
                shiftStart = TimeSpan.FromHours(9);
            if (!TimeSpan.TryParse(cfg.WorkingHoursEnd, out var shiftEnd))
                shiftEnd = TimeSpan.FromHours(18);

            int slotMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 30;

            // Retrieve booked appointments for doctor on chosen date
            var bookedTimes = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.AppointmentDate.Date == date.Date && a.Status != "Cancelled")
                .Select(a => a.AppointmentTime)
                .ToListAsync();

            var bookedSet = bookedTimes.Select(t => t.ToString(@"hh\:mm")).ToHashSet();

            var freeSlots = new List<string>();
            var nowTime = DateTime.UtcNow.TimeOfDay;
            var isToday = date.Date == DateTime.UtcNow.Date;

            for (var t = shiftStart; t + TimeSpan.FromMinutes(slotMinutes) <= shiftEnd; t += TimeSpan.FromMinutes(slotMinutes))
            {
                var slotStr = t.ToString(@"hh\:mm");
                // If today, filter out past slots
                if (isToday && t <= nowTime) continue;

                if (!bookedSet.Contains(slotStr))
                {
                    freeSlots.Add(slotStr);
                }
            }

            if (freeSlots.Count == 0)
            {
                var noSlotMsg = BotStrings.Get("NoSlotsOnDate", session.Lang, session.SelectedDoctorName ?? "", date.ToString("yyyy-MM-dd"));
                var retryKeyboard = new List<List<TelegramInlineButton>>
                {
                    new()
                    {
                        new() { Text = BotStrings.Get("BtnBack", session.Lang), CallbackData = $"{PrefixDoctor}{doctorId}" }
                    }
                };
                await SendInlineKeyboardAsync(session.ChatId, noSlotMsg, retryKeyboard);
                return;
            }

            // Render free slots as 2-column inline keyboard (cap at 12 to fit screen)
            var displaySlots = freeSlots.Take(12).ToList();
            var keyboard = displaySlots
                .Select((s, i) => (s, i))
                .GroupBy(x => x.i / 2)
                .Select(g => g.Select(x => new TelegramInlineButton
                {
                    Text = $"🕐 {x.s}",
                    CallbackData = $"{PrefixSlot}{x.s}"
                }).ToList())
                .ToList();

            keyboard.Add(new List<TelegramInlineButton>
            {
                new() { Text = BotStrings.Get("BtnBack", session.Lang), CallbackData = $"{PrefixDoctor}{doctorId}" }
            });

            var header = BotStrings.Get("SelectSlotHeader", session.Lang,
                session.SelectedDoctorName ?? "", date.ToString("yyyy-MM-dd"));
            await SendInlineKeyboardAsync(session.ChatId, header, keyboard);
        }

        private async Task HandleSlotChosenAsync(BotSession session, string slot)
        {
            if (!session.PatientId.HasValue || !session.SelectedDoctorId.HasValue || !session.SelectedDate.HasValue)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("BookingFailed", session.Lang));
                await SendMainMenuAsync(session);
                return;
            }

            session.SelectedTime = slot;
            var doctorId = session.SelectedDoctorId.Value;
            var dateOnly = session.SelectedDate.Value.Date;

            if (!TimeSpan.TryParse(slot, out var apptTime))
            {
                apptTime = TimeSpan.FromHours(10);
            }

            // Concurrency guard: double-check slot hasn't just been booked
            var conflict = await _context.Appointments
                .AsNoTracking()
                .AnyAsync(a => a.DoctorId == doctorId &&
                               a.AppointmentDate.Date == dateOnly &&
                               a.AppointmentTime == apptTime &&
                               a.Status != "Cancelled");

            if (conflict)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("BookingFailed", session.Lang));
                await HandleDoctorChosenAsync(session, doctorId);
                return;
            }

            // Create Appointment entity with status Confirmed
            var appointment = new Appointment
            {
                PatientId = session.PatientId.Value,
                DoctorId = doctorId,
                AppointmentDate = dateOnly,
                AppointmentTime = apptTime,
                Status = "Confirmed",
                Notes = "Booked via Telegram Bot"
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var doctorName = session.SelectedDoctorName ?? "Doctor";
            var patientName = session.PatientName ?? "Patient";
            var dateStr = dateOnly.ToString("yyyy-MM-dd");

            // Bilingual confirmation summary (Doctor Name, Date, Time, Patient Name)
            // with options to cancel or return to the main menu
            var summaryMsg = BotStrings.Get("AppointmentConfirmedSummary", session.Lang,
                appointment.AppointmentId, patientName, doctorName, dateStr, slot);

            var keyboard = new List<List<TelegramInlineButton>>
            {
                new()
                {
                    new() { Text = BotStrings.Get("BtnCancelThisAppt", session.Lang), CallbackData = $"{PrefixCancelAppt}{appointment.AppointmentId}" }
                },
                new()
                {
                    new() { Text = BotStrings.Get("BtnHomeMenu", session.Lang), CallbackData = $"{PrefixMenu}MENU" }
                }
            };

            await SendInlineKeyboardAsync(session.ChatId, summaryMsg, keyboard);

            // Alert Doctor if personal Telegram Chat ID or phone is configured
            var doctor = await _context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.DoctorId == doctorId);
            if (doctor != null && !string.IsNullOrWhiteSpace(doctor.DoctorPhone))
            {
                var alertMsg = BotStrings.Get("DoctorAlertNewAppt", "ar",
                    patientName,
                    session.PhoneNumber ?? "",
                    session.PatientId.Value,
                    dateStr,
                    slot,
                    appointment.AppointmentId);

                _ = SendAlertAsync(doctor.DoctorPhone, alertMsg);
            }

            // Reset booking flow
            session.ResetBookingFlow();
            _sessionStore.Update(session);
        }

        private async Task FinalizeBookingAsync(BotSession session)
        {
            if (!session.PatientId.HasValue || !session.SelectedDoctorId.HasValue ||
                !session.SelectedDate.HasValue || string.IsNullOrEmpty(session.SelectedTime))
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("BookingFailed", session.Lang));
                await SendMainMenuAsync(session);
                return;
            }

            if (!TimeSpan.TryParse(session.SelectedTime, out var apptTime))
            {
                apptTime = TimeSpan.FromHours(10);
            }

            var dateOnly = session.SelectedDate.Value.Date;
            var doctorId = session.SelectedDoctorId.Value;

            // Concurrency guard: double-check slot hasn't just been booked
            var conflict = await _context.Appointments
                .AsNoTracking()
                .AnyAsync(a => a.DoctorId == doctorId &&
                               a.AppointmentDate.Date == dateOnly &&
                               a.AppointmentTime == apptTime &&
                               a.Status != "Cancelled");

            if (conflict)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("BookingFailed", session.Lang));
                await StartBookingFlowAsync(session);
                return;
            }

            // Create appointment
            var appointment = new Appointment
            {
                PatientId = session.PatientId.Value,
                DoctorId = doctorId,
                AppointmentDate = dateOnly,
                AppointmentTime = apptTime,
                Status = "Confirmed",
                Notes = "Booked via Telegram Bot"
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var doctorName = session.SelectedDoctorName ?? "Doctor";
            var patientName = session.PatientName ?? "Patient";
            var dateStr = dateOnly.ToString("yyyy-MM-dd");
            var timeStr = session.SelectedTime ?? apptTime.ToString(@"hh\:mm");

            var summaryMsg = BotStrings.Get("AppointmentConfirmedSummary", session.Lang,
                appointment.AppointmentId, patientName, doctorName, dateStr, timeStr);

            var keyboard = new List<List<TelegramInlineButton>>
            {
                new()
                {
                    new() { Text = BotStrings.Get("BtnCancelThisAppt", session.Lang), CallbackData = $"{PrefixCancelAppt}{appointment.AppointmentId}" }
                },
                new()
                {
                    new() { Text = BotStrings.Get("BtnHomeMenu", session.Lang), CallbackData = $"{PrefixMenu}MENU" }
                }
            };

            await SendInlineKeyboardAsync(session.ChatId, summaryMsg, keyboard);

            // Alert Doctor if personal Telegram Chat ID or phone is configured
            var doctor = await _context.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.DoctorId == doctorId);
            if (doctor != null && !string.IsNullOrWhiteSpace(doctor.DoctorPhone))
            {
                var alertMsg = BotStrings.Get("DoctorAlertNewAppt", "ar",
                    patientName,
                    session.PhoneNumber ?? "",
                    session.PatientId.Value,
                    dateStr,
                    timeStr,
                    appointment.AppointmentId);

                _ = SendAlertAsync(doctor.DoctorPhone, alertMsg);
            }

            // Reset booking flow
            session.ResetBookingFlow();
            _sessionStore.Update(session);
        }

        // ── Cancellation Flow ──────────────────────────────────────────────────

        private async Task StartCancellationFlowAsync(BotSession session)
        {
            if (!session.PatientId.HasValue)
            {
                await PromptForPhoneAsync(session);
                return;
            }

            var today = DateTime.UtcNow.Date;
            var upcoming = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == session.PatientId.Value &&
                            a.Status != "Cancelled" &&
                            a.AppointmentDate.Date >= today)
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .ToListAsync();

            if (upcoming.Count == 0)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("NoAppointmentsFound", session.Lang));
                await SendMainMenuAsync(session);
                return;
            }

            var keyboard = upcoming.Select(a => new List<TelegramInlineButton>
            {
                new()
                {
                    Text = $"📅 {a.AppointmentDate:MM/dd} {a.AppointmentTime:hh\\:mm} — د. {a.Doctor?.DoctorName}",
                    CallbackData = $"{PrefixCancelAppt}{a.AppointmentId}"
                }
            }).ToList();

            keyboard.Add(new List<TelegramInlineButton>
            {
                new() { Text = BotStrings.Get("BtnBack", session.Lang), CallbackData = $"{PrefixMenu}MENU" }
            });

            await SendInlineKeyboardAsync(session.ChatId, BotStrings.Get("CancelSelectHeader", session.Lang), keyboard);
        }

        private async Task HandleCancelApptChosenAsync(BotSession session, int appointmentId)
        {
            var appt = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && a.PatientId == session.PatientId);

            if (appt == null)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("CancelFailed", session.Lang));
                await SendMainMenuAsync(session);
                return;
            }

            session.CancelAppointmentId = appointmentId;
            session.Step = BotStep.ConfirmCancel;
            _sessionStore.Update(session);

            var prompt = BotStrings.Get("CancelConfirmPrompt", session.Lang,
                appt.AppointmentId,
                appt.Doctor?.DoctorName ?? "",
                appt.AppointmentDate.ToString("yyyy-MM-dd"),
                appt.AppointmentTime.ToString(@"hh\:mm"));

            var keyboard = new List<List<TelegramInlineButton>>
            {
                new()
                {
                    new() { Text = BotStrings.Get("BtnConfirmCancel", session.Lang), CallbackData = $"{PrefixConfirm}CANCEL" },
                    new() { Text = BotStrings.Get("BtnBack", session.Lang), CallbackData = $"{PrefixAbort}CANCEL" }
                }
            };

            await SendInlineKeyboardAsync(session.ChatId, prompt, keyboard);
        }

        private async Task FinalizeCancellationAsync(BotSession session)
        {
            if (!session.CancelAppointmentId.HasValue)
            {
                await SendMainMenuAsync(session);
                return;
            }

            var apptId = session.CancelAppointmentId.Value;
            var appt = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == apptId && a.PatientId == session.PatientId);

            if (appt == null)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("CancelFailed", session.Lang));
                session.CancelAppointmentId = null;
                session.Step = BotStep.Idle;
                _sessionStore.Update(session);
                await SendMainMenuAsync(session);
                return;
            }

            appt.Status = "Cancelled";
            await _context.SaveChangesAsync();

            await SendTextAsync(session.ChatId, BotStrings.Get("CancelSuccess", session.Lang, apptId));

            // Notify Doctor of cancellation
            if (appt.Doctor != null && !string.IsNullOrWhiteSpace(appt.Doctor.DoctorPhone))
            {
                var alertMsg = BotStrings.Get("DoctorAlertCancelledAppt", "ar",
                    session.PatientName ?? "Patient",
                    apptId,
                    appt.AppointmentDate.ToString("yyyy-MM-dd"),
                    appt.AppointmentTime.ToString(@"hh\:mm"));

                _ = SendAlertAsync(appt.Doctor.DoctorPhone, alertMsg);
            }

            session.CancelAppointmentId = null;
            session.Step = BotStep.Idle;
            _sessionStore.Update(session);

            await SendMainMenuAsync(session);
        }

        // ── View My Appointments ───────────────────────────────────────────────

        private async Task ShowMyAppointmentsAsync(BotSession session)
        {
            if (!session.PatientId.HasValue)
            {
                await PromptForPhoneAsync(session);
                return;
            }

            var appts = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == session.PatientId.Value)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .Take(5)
                .ToListAsync();

            if (appts.Count == 0)
            {
                await SendTextAsync(session.ChatId, BotStrings.Get("NoAppointmentsFound", session.Lang));
                await SendMainMenuAsync(session);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(BotStrings.Get("MyAppointmentsHeader", session.Lang));

            foreach (var a in appts)
            {
                sb.Append(BotStrings.Get("AppointmentRow", session.Lang,
                    a.AppointmentId,
                    a.Doctor?.DoctorName ?? "-",
                    a.AppointmentDate.ToString("yyyy-MM-dd"),
                    a.AppointmentTime.ToString(@"hh\:mm"),
                    a.Status));
            }

            await SendTextAsync(session.ChatId, sb.ToString());
            await SendMainMenuAsync(session);
        }

        // ── Telegram Bot API HTTP Primitives ───────────────────────────────────

        public async Task SendAlertAsync(string chatId, string markdownMessage)
        {
            try
            {
                await SendTextAsync(chatId, markdownMessage);
                _logger.LogInformation("[TelegramBot] Alert sent to {ChatId}", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TelegramBot] Failed to send alert to {ChatId}", chatId);
            }
        }

        public async Task SendInlineKeyboardAsync(string chatId, string text, List<List<TelegramInlineButton>> keyboard)
        {
            var token = await GetBotTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("[TelegramBot] Bot token not configured; cannot send keyboard to {ChatId}", chatId);
                return;
            }

            var inlineKeyboard = keyboard.Select(row =>
                row.Select(btn => new { text = btn.Text, callback_data = btn.CallbackData }).ToList()
            ).ToList();

            var payload = new
            {
                chat_id = chatId,
                text = text,
                parse_mode = "Markdown",
                reply_markup = new { inline_keyboard = inlineKeyboard }
            };

            await PostToTelegramAsync(token, "sendMessage", payload);
        }

        private async Task SendTextAsync(string chatId, string text)
        {
            var token = await GetBotTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("[TelegramBot] Bot token not configured; cannot send text to {ChatId}", chatId);
                return;
            }

            var payload = new
            {
                chat_id = chatId,
                text = text,
                parse_mode = "Markdown"
            };

            await PostToTelegramAsync(token, "sendMessage", payload);
        }

        private async Task AnswerCallbackQueryAsync(string callbackQueryId)
        {
            if (string.IsNullOrWhiteSpace(callbackQueryId)) return;

            var token = await GetBotTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return;

            var payload = new { callback_query_id = callbackQueryId };
            await PostToTelegramAsync(token, "answerCallbackQuery", payload);
        }

        private async Task PostToTelegramAsync(string token, string method, object payload)
        {
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var url = $"{TelegramApiBase}{token}/{method}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var httpClient = _httpClientFactory.CreateClient(nameof(TelegramBotService));
                var response = await httpClient.PostAsync(url, content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token);
                    _logger.LogWarning("[TelegramBot] {Method} returned {Status}: {Body}", method, response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TelegramBot] HTTP call to Telegram {Method} failed.", method);
            }
        }

        private async Task<string?> GetBotTokenAsync()
        {
            return await _settings.GetSettingValueAsync("TelegramBotToken");
        }

        private static string CleanPhoneNumber(string raw) =>
            new string(raw.Where(char.IsDigit).ToArray());

        private static bool IsLikelyPhoneNumber(string text)
        {
            var digitsOnly = CleanPhoneNumber(text);
            return digitsOnly.Length >= 7 && digitsOnly.Length <= 15;
        }
    }
}
