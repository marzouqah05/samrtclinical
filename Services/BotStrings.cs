using System.Text.RegularExpressions;

namespace WebApplication1.Services
{
    /// <summary>
    /// Centralized bilingual dictionary and localization helper for the Telegram Bot.
    /// Provides consistent Arabic and English copy for all conversation prompts,
    /// buttons, summaries, and error states.
    /// </summary>
    public static class BotStrings
    {
        private static readonly Regex ArabicRegex = new(@"[\u0600-\u06FF]", RegexOptions.Compiled);

        /// <summary>
        /// Detects if text contains Arabic characters. Defaults to "ar" if detected, else "en".
        /// </summary>
        public static string DetectLanguage(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "ar";
            return ArabicRegex.IsMatch(text) ? "ar" : "en";
        }

        public static string NormalizeLang(string? lang) =>
            string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";

        public static string Get(string key, string lang, params object[] args)
        {
            var l = NormalizeLang(lang);
            var format = Strings.TryGetValue(key, out var dict) && dict.TryGetValue(l, out var val)
                ? val
                : $"[{key}]";

            return args.Length > 0 ? string.Format(format, args) : format;
        }

        // ── String Resource Table ─────────────────────────────────────────────
        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            // Language selection
            ["ChooseLanguagePrompt"] = new()
            {
                ["ar"] = "مرحباً بك في *عيادة ميدكير الطبية*! 🏥\nيرجى اختيار لغة المحادثة المفضلة:",
                ["en"] = "Welcome to *MediCare Clinic*! 🏥\nPlease choose your preferred conversation language:"
            },
            ["BtnArabic"] = new() { ["ar"] = "العربية 🇯🇴", ["en"] = "العربية 🇯🇴" },
            ["BtnEnglish"] = new() { ["ar"] = "English 🇬🇧", ["en"] = "English 🇬🇧" },

            // Phone prompt
            ["PromptPhone"] = new()
            {
                ["ar"] = "📱 يرجى إرسال رقم هاتفك للبحث عن ملفك الطبي:\n\nمثال: `0791234567` أو `+962791234567`",
                ["en"] = "📱 Please send your phone number to find your medical file:\n\nExample: `0791234567` or `+962791234567`"
            },
            ["InvalidPhone"] = new()
            {
                ["ar"] = "⚠️ رقم الهاتف غير صحيح. يرجى إدخال رقم هاتف صالح يتكون من 9 إلى 15 رقماً:",
                ["en"] = "⚠️ Invalid phone number. Please enter a valid phone number (9 to 15 digits):"
            },

            // Patient Greeting & Main Menu
            ["GreetingExisting"] = new()
            {
                ["ar"] = "أهلاً بك يا *{0}*! 👋\n📋 رقم الملف الطبي: `#{1}`\n\nكيف يمكننا مساعدتك اليوم؟",
                ["en"] = "Welcome back, *{0}*! 👋\n📋 Medical File ID: `#{1}`\n\nHow can we help you today?"
            },
            ["MainMenuPrompt"] = new()
            {
                ["ar"] = "اختر من القائمة أدناه:",
                ["en"] = "Please choose an option below:"
            },
            ["BtnBookAppt"] = new() { ["ar"] = "📅 حجز موعد جديد", ["en"] = "📅 Book Appointment" },
            ["BtnMyAppts"] = new() { ["ar"] = "📋 مواعيدي القادمة", ["en"] = "📋 My Appointments" },
            ["BtnCancelAppt"] = new() { ["ar"] = "❌ إلغاء موعد", ["en"] = "❌ Cancel Appointment" },
            ["BtnChangeLang"] = new() { ["ar"] = "🌐 تغيير اللغة / Language", ["en"] = "🌐 Change Language" },
            ["BtnBack"] = new() { ["ar"] = "🔙 العودة للقائمة", ["en"] = "🔙 Back to Menu" },

            // Patient Not Found & Registration
            ["PatientNotFound"] = new()
            {
                ["ar"] = "لم يتم العثور على ملف بهذا الرقم. هل ترغب في تسجيل ملف مريض جديد؟",
                ["en"] = "No record found for this number. Would you like to register as a new patient?"
            },
            ["BtnRegisterNew"] = new() { ["ar"] = "📝 تسجيل ملف جديد", ["en"] = "📝 Register" },
            ["BtnReenterPhone"] = new() { ["ar"] = "🔄 تجربة رقم آخر", ["en"] = "🔄 Retry" },
            ["PromptPatientName"] = new()
            {
                ["ar"] = "يرجى كتابة اسمك الكامل (الاسم الثلاثي):",
                ["en"] = "Please send your full name:"
            },
            ["PromptRegisterName"] = new()
            {
                ["ar"] = "يرجى كتابة اسمك الكامل (الاسم الثلاثي):",
                ["en"] = "Please send your full name:"
            },
            ["PromptRegisterNationalId"] = new()
            {
                ["ar"] = "🆔 يرجى إرسال الرقم الوطني (أو رقم الهوية / جواز السفر - 10 أرقام):",
                ["en"] = "🆔 Please enter your National ID / Passport Number (10 digits):"
            },
            ["InvalidNationalId"] = new()
            {
                ["ar"] = "⚠️ يجب أن يتكون الرقم الوطني من 10 أرقام. يرجى المحاولة مرة أخرى:",
                ["en"] = "⚠️ National ID must be exactly 10 digits. Please try again:"
            },
            ["RegisterSuccessAutoBook"] = new()
            {
                ["ar"] = "تم فتح ملفك الطبي بنجاح! رقم الملف: #{0}\nدعنا نقوم بحجز موعدك الأول الآن.",
                ["en"] = "Medical profile created successfully! Patient ID: #{0}\nLet's book your first appointment now."
            },
            ["RegisterSuccess"] = new()
            {
                ["ar"] = "🎉 تم إنشاء ملفك الطبي بنجاح!\n👤 الاسم: *{0}*\n📋 رقم الملف: `#{1}`\n🆔 الرقم الوطني: `{2}`\n📱 الهاتف: `{3}`",
                ["en"] = "🎉 Your medical record has been created successfully!\n👤 Name: *{0}*\n📋 File ID: `#{1}`\n🆔 National ID: `{2}`\n📱 Phone: `{3}`"
            },

            // Doctor Selection
            ["SelectDoctorHeader"] = new()
            {
                ["ar"] = "👨‍⚕️ *اختر الطبيب المعالج:*\nيرجى اختيار الطبيب المطلوب لحجز الموعد:",
                ["en"] = "👨‍⚕️ *Select Doctor:*\nPlease choose the doctor you wish to visit:"
            },
            ["NoDoctorsAvailable"] = new()
            {
                ["ar"] = "⚠️ لا يوجد أطباء متاحون حالياً في النظام. يرجى التواصل مع العيادة هاتفياً.",
                ["en"] = "⚠️ No doctors are currently available. Please contact the clinic directly."
            },

            // Date Selection
            ["SelectDateHeader"] = new()
            {
                ["ar"] = "🗓️ *اختر اليوم المطلوب للكشف لدى د. {0}:*",
                ["en"] = "🗓️ *Select appointment day with Dr. {0}:*"
            },
            ["BtnToday"] = new() { ["ar"] = "اليوم", ["en"] = "Today" },
            ["BtnTomorrow"] = new() { ["ar"] = "غداً", ["en"] = "Tomorrow" },

            // Slot Selection
            ["SelectSlotHeader"] = new()
            {
                ["ar"] = "⏰ *الأوقات المتاحة لدى د. {0}*\n📅 التاريخ: *{1}*\n\nيرجى اختيار الموعد المناسب:",
                ["en"] = "⏰ *Available Slots for Dr. {0}*\n📅 Date: *{1}*\n\nSelect your preferred time:"
            },
            ["NoSlotsOnDate"] = new()
            {
                ["ar"] = "😔 عذراً، جميع المواعيد محجوزة لدى د. *{0}* في تاريخ *{1}*.\nيرجى اختيار تاريخ آخر أو طبيب آخر.",
                ["en"] = "😔 Sorry, all slots for Dr. *{0}* on *{1}* are booked.\nPlease select another date or physician."
            },
            ["DoctorNoWorkDays"] = new()
            {
                ["ar"] = "⚠️ عذراً، لا توجد أيام عمل متاحة للطبيب في الأيام القادمة. يرجى الاتصال بالعيادة.",
                ["en"] = "⚠️ No active working days found for this doctor in the near future. Please call the clinic."
            },

            // Confirmation
            ["BookingSummary"] = new()
            {
                ["ar"] = "📋 *ملخص تأكيد الموعد:*\n\n👤 المريض: *{0}*\n👨‍⚕️ الطبيب: *د. {1}* ({2})\n📅 التاريخ: *{3}*\n⏰ الوقت: *{4}*\n💵 كشفية الاستشارة: *{5:F2} د.أ*\n\nهل تؤكد حجز هذا الموعد؟",
                ["en"] = "📋 *Appointment Confirmation Summary:*\n\n👤 Patient: *{0}*\n👨‍⚕️ Doctor: *Dr. {1}* ({2})\n📅 Date: *{3}*\n⏰ Time: *{4}*\n💵 Consultation Fee: *{5:F2} JOD*\n\nDo you confirm this booking?"
            },
            ["BtnConfirmBooking"] = new() { ["ar"] = "✅ تأكيد الحجز", ["en"] = "✅ Confirm Booking" },
            ["BtnAbortBooking"] = new() { ["ar"] = "❌ إلغاء العملية", ["en"] = "❌ Cancel" },
            ["BookingSuccess"] = new()
            {
                ["ar"] = "🎉 *تم تأكيد موعدك بنجاح!*\n\n🔖 رقم الموعد: `#{0}`\n👨‍⚕️ الطبيب: *د. {1}*\n📅 التاريخ: *{2}*\n⏰ الوقت: *{3}*\n\n📍 نرجو التواجد في العيادة قبل الموعد بـ 10 دقائق.\nشكراً لاختيارك ميدكير! 🏥",
                ["en"] = "🎉 *Appointment Confirmed Successfully!*\n\n🔖 Appointment ID: `#{0}`\n👨‍⚕️ Doctor: *Dr. {1}*\n📅 Date: *{2}*\n⏰ Time: *{3}*\n\n📍 Please arrive at the clinic 10 minutes early.\nThank you for choosing MediCare! 🏥"
            },
            ["AppointmentConfirmedSummary"] = new()
            {
                ["ar"] = "🎉 *تم تأكيد موعدك بنجاح!*\n\n🔖 رقم الموعد: `#{0}`\n👤 اسم المريض: *{1}*\n👨‍⚕️ الطبيب: *د. {2}*\n📅 التاريخ: *{3}*\n⏰ الوقت: *{4}*\n📋 الحالة: *مؤكد (Confirmed)*\n\n📍 نرجو التواجد في العيادة قبل الموعد بـ 10 دقائق.\nشكراً لاختيارك عيادة ميدكير! 🏥",
                ["en"] = "🎉 *Appointment Confirmed Successfully!*\n\n🔖 Appointment ID: `#{0}`\n👤 Patient: *{1}*\n👨‍⚕️ Doctor: *Dr. {2}*\n📅 Date: *{3}*\n⏰ Time: *{4}*\n📋 Status: *Confirmed*\n\n📍 Please arrive 10 minutes prior to your appointment.\nThank you for choosing MediCare Clinic! 🏥"
            },
            ["BtnCancelThisAppt"] = new() { ["ar"] = "❌ إلغاء الموعد", ["en"] = "❌ Cancel Appointment" },
            ["BtnHomeMenu"] = new() { ["ar"] = "🏠 القائمة الرئيسية", ["en"] = "🏠 Main Menu" },
            ["BookingFailed"] = new()
            {
                ["ar"] = "⚠️ لم نتمكن من إتمام الحجز (ربما تم حجز الموعد من مريض آخر للتو). يرجى المحاولة واختيار وقت آخر.",
                ["en"] = "⚠️ Booking could not be completed (the slot may have just been booked by another patient). Please choose another time."
            },

            // My Appointments & Cancellation
            ["MyAppointmentsHeader"] = new()
            {
                ["ar"] = "📋 *مواعيدك المسجلة لدى العيادة:*\n",
                ["en"] = "📋 *Your Scheduled Appointments:*\n"
            },
            ["NoAppointmentsFound"] = new()
            {
                ["ar"] = "ℹ️ ليس لديك أي مواعيد نشطة أو قادمة حالياً.",
                ["en"] = "ℹ️ You do not have any active or upcoming appointments."
            },
            ["AppointmentRow"] = new()
            {
                ["ar"] = "• موعد #{0}: د. {1} | 📅 {2} | ⏰ {3} | الحالة: *{4}*\n",
                ["en"] = "• Appt #{0}: Dr. {1} | 📅 {2} | ⏰ {3} | Status: *{4}*\n"
            },
            ["CancelSelectHeader"] = new()
            {
                ["ar"] = "❌ *إلغاء موعد:*\nاختر الموعد الذي ترغب في إلغائه:",
                ["en"] = "❌ *Cancel Appointment:*\nSelect the appointment you wish to cancel:"
            },
            ["CancelConfirmPrompt"] = new()
            {
                ["ar"] = "⚠️ *تأكيد الإلغاء:*\nهل أنت متأكد من رغبتك في إلغاء الموعد #{0} مع د. {1} بتاريخ {2} الساعة {3}؟",
                ["en"] = "⚠️ *Cancellation Confirmation:*\nAre you sure you want to cancel appointment #{0} with Dr. {1} on {2} at {3}?"
            },
            ["BtnConfirmCancel"] = new() { ["ar"] = "نعم، قم بالإلغاء", ["en"] = "Yes, Cancel It" },
            ["CancelSuccess"] = new()
            {
                ["ar"] = "✅ تم إلغاء الموعد رقم #{0} بنجاح. نتمنى لك دوام الصحة والعافية!",
                ["en"] = "✅ Appointment #{0} has been cancelled successfully. We wish you good health!"
            },
            ["CancelFailed"] = new()
            {
                ["ar"] = "⚠️ تعذر إلغاء الموعد أو أن الموعد ملغي مسبقاً.",
                ["en"] = "⚠️ Failed to cancel the appointment or it was already cancelled."
            },

            // Doctor alert
            ["DoctorAlertNewAppt"] = new()
            {
                ["ar"] = "🔔 *إشعار حجز موعد جديد عبر تيليجرام*\n\n👤 المريض: *{0}*\n📱 الهاتف: `{1}`\n📋 رقم الملف: `#{2}`\n📅 التاريخ: *{3}*\n⏰ الوقت: *{4}*\n🔖 رقم الموعد: `#{5}`",
                ["en"] = "🔔 *New Appointment Notification (Telegram)*\n\n👤 Patient: *{0}*\n📱 Phone: `{1}`\n📋 File ID: `#{2}`\n📅 Date: *{3}*\n⏰ Time: *{4}*\n🔖 Appointment ID: `#{5}`"
            },
            ["DoctorAlertCancelledAppt"] = new()
            {
                ["ar"] = "⚠️ *إشعار إلغاء موعد*\n\nقام المريض *{0}* بإلغاء موعده رقم `#{1}` المقرر بتاريخ {2} الساعة {3}.",
                ["en"] = "⚠️ *Appointment Cancellation Notification*\n\nPatient *{0}* has cancelled appointment `#{1}` scheduled for {2} at {3}."
            },

            // General / Fallback
            ["SessionReset"] = new()
            {
                ["ar"] = "🔄 تم إعادة ضبط المحادثة. أرسل /start في أي وقت للبدء من جديد.",
                ["en"] = "🔄 Your session has been reset. Send /start anytime to begin again."
            },
            ["OperationCancelled"] = new()
            {
                ["ar"] = "🚫 تم إلغاء العملية الحالية.",
                ["en"] = "🚫 The current operation was cancelled."
            },
            ["GeneralError"] = new()
            {
                ["ar"] = "⚠️ حدث خطأ غير متوقع. يرجى إرسال /start والمحاولة مرة أخرى.",
                ["en"] = "⚠️ An unexpected error occurred. Please send /start and try again."
            }
        };
    }
}
