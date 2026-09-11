using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Services
{
    public interface IEmailSenderService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string recipientName = "");
    }

    public class EmailService : IEmailSenderService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return false;

            try
            {
                var smtpHost = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPortStr = _config["Email:SmtpPort"] ?? "587";
                var smtpUser = _config["Email:SmtpUser"] ?? "";
                var smtpPass = _config["Email:SmtpPassword"] ?? "";
                var fromAddress = _config["Email:FromAddress"] ?? (!string.IsNullOrEmpty(smtpUser) ? smtpUser : "noreply@clinicflow.com");
                var fromName = _config["Email:FromName"] ?? "ClinicFlow Medical OS";

                int.TryParse(smtpPortStr, out int smtpPort);
                if (smtpPort == 0) smtpPort = 587;

                // If SMTP password is not configured, log OTP visibly so verification works smoothly in test/dev
                if (string.IsNullOrWhiteSpace(smtpPass) || string.IsNullOrWhiteSpace(smtpUser))
                {
                    _logger.LogWarning("[EmailService] SMTP credentials not configured. Mocking email delivery to {To}. Subject: {Subject}", toEmail, subject);
                    return true;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
                _logger.LogInformation("[EmailService] Successfully sent email to {To}. Subject: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailService] Error dispatching email to {To}. Subject: {Subject}", toEmail, subject);
                return false;
            }
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string recipientName = "")
        {
            var greeting = string.IsNullOrWhiteSpace(recipientName) ? "مرحباً بك" : $"مرحباً د. {recipientName}";
            var subject = $"رمز التحقق لتفعيل حساب العيادة: {otpCode}";

            var body = $@"
<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; padding: 24px; }}
        .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 16px; padding: 32px; border: 1px solid #e2e8f0; box-shadow: 0 4px 12px rgba(0,0,0,0.05); text-align: center; }}
        .header-badge {{ display: inline-block; background: #ecfdf5; color: #059669; font-weight: 700; font-size: 13px; padding: 6px 16px; border-radius: 20px; margin-bottom: 16px; }}
        .otp-box {{ background: #f0fdf4; border: 2px dashed #059669; border-radius: 12px; font-size: 36px; font-weight: 800; letter-spacing: 8px; color: #064e3b; padding: 18px 24px; margin: 24px 0; }}
        .footer {{ margin-top: 24px; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""header-badge"">ClinicFlow OS · نظام إدارة العيادات والمراكز الطبية</div>
        <h2 style=""color: #0f172a; margin-bottom: 8px;"">{greeting}</h2>
        <p style=""color: #64748b; font-size: 15px; margin-top: 0;"">شكراً لتسجيل عيادتك في التجربة المجانية (60 يوماً). رمز التحقق لتفعيل حساب المدير (Admin) الخاص بك هو:</p>
        
        <div class=""otp-box"">{otpCode}</div>
        
        <p style=""font-size: 13px; color: #64748b;"">ينتهي رمز التحقق هذا خلال <strong>10 دقائق</strong>. يرجى إدخاله في صفحة التحقق لإتمام التفعيل والبدء باستخدام النظام.</p>
        
        <div class=""footer"">
            إذا لم تقم بطلب هذا الرمز، يرجى تجاهل هذه الرسالة.<br>
            &copy; {DateTime.UtcNow.Year} ClinicFlow Health Systems.
        </div>
    </div>
</body>
</html>";

            _logger.LogInformation("[EmailService] OTP generated for {To}: {OTP}", toEmail, otpCode);
            return await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendAppointmentReminder(string patientEmail, string patientName, string appointmentDate)
        {
            var subject = "تذكير بموعد العيادة · Clinic Appointment Reminder";
            var body = $"<p>عزيزي {patientName}، نود تذكيرك بموعدك القادم بتاريخ: <strong>{appointmentDate}</strong>.</p>";
            await SendEmailAsync(patientEmail, subject, body);
        }
    }
}