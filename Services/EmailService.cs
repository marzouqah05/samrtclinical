using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }

    public interface IEmailSenderService : IEmailSender
    {
        new Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string recipientName = "");
        Task SendAppointmentReminder(string patientEmail, string patientName, string appointmentDate);
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

        async Task IEmailSender.SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return false;

            try
            {
                var smtpHost = _config["SmtpSettings:Server"] ?? "smtp.gmail.com";
                var smtpPortStr = _config["SmtpSettings:Port"] ?? "587";
                var smtpUser = _config["SmtpSettings:SenderEmail"] ?? "hadihaitham777@gmail.com";
                var smtpPass = _config["SmtpSettings:Password"] ?? "rzflzwnilhozdtwp";
                var fromAddress = _config["SmtpSettings:SenderEmail"] ?? "hadihaitham777@gmail.com";
                var fromName = _config["SmtpSettings:SenderName"] ?? "ClinicFlow";
                var enableSsl = !bool.TryParse(_config["SmtpSettings:EnableSsl"], out var ssl) || ssl;

                int.TryParse(smtpPortStr, out int smtpPort);
                if (smtpPort == 0) smtpPort = 587;

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = enableSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    Timeout = 15000 // 15 seconds timeout
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
                _logger.LogError(ex, "[EmailService] Error dispatching email via SMTP to {To}. Subject: {Subject}", toEmail, subject);
                return false;
            }
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string recipientName = "")
        {
            var subject = "رمز التحقق لتفعيل عيادتك | ClinicFlow OTP Verification";

            var greeting = string.IsNullOrWhiteSpace(recipientName) ? "مرحباً بك،" : $"مرحباً د. {recipientName}،";

            var body = $@"
<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>ClinicFlow OTP</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; color: #0f172a;'>
    <table width='100%' border='0' cellspacing='0' cellpadding='0' style='background-color: #f8fafc; padding: 40px 10px;'>
        <tr>
            <td align='center'>
                <table width='100%' border='0' cellspacing='0' cellpadding='0' style='max-width: 520px; background-color: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.05);'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #064e3b 0%, #059669 100%); padding: 32px 24px; text-align: center;'>
                            <h1 style='margin: 0; color: #ffffff; font-size: 26px; font-weight: 800; letter-spacing: -0.5px;'>ClinicFlow</h1>
                            <p style='margin: 6px 0 0; color: #a7f3d0; font-size: 14px;'>النظام الطبي المتكامل لإدارة العيادات والمراكز</p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style='padding: 36px 32px; direction: rtl; text-align: right;'>
                            <h2 style='margin: 0 0 12px; font-size: 18px; color: #0f172a; font-weight: 700;'>{greeting}</h2>
                            <p style='margin: 0 0 24px; font-size: 14.5px; color: #475569; line-height: 1.6;'>
                                شكراً لانضمامك إلى ClinicFlow. استخدم رمز التحقق التالي لتأكيد بريدك الإلكتروني وتفعيل حساب العيادة الخاص بك:
                            </p>
                            <!-- OTP Box -->
                            <div style='background-color: #f0fdf4; border: 2px dashed #10b981; border-radius: 12px; padding: 24px; text-align: center; margin: 24px 0;'>
                                <span style='font-family: monospace, Courier; font-size: 38px; font-weight: 800; letter-spacing: 12px; color: #065f46; display: inline-block; padding-left: 12px;'>{otpCode}</span>
                                <div style='margin-top: 10px; font-size: 13px; color: #059669; font-weight: 600;'>
                                    ⏱ هذا الرمز صالح لمدة 10 دقائق فقط
                                </div>
                            </div>
                            <p style='margin: 0 0 8px; font-size: 13.5px; color: #64748b; line-height: 1.5;'>
                                إذا لم تقم بطلب تسجيل هذا الحساب، يرجى تجاهل هذا البريد الإلكتروني. لن يتم تفعيل الحساب بدون إدخال هذا الرمز.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8fafc; border-top: 1px solid #e2e8f0; padding: 20px; text-align: center; font-size: 12px; color: #94a3b8;'>
                            &copy; {DateTime.UtcNow.Year} ClinicFlow Health Systems · جميع الحقوق محفوظة
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            _logger.LogInformation("[EmailService] Sending live OTP email to {To}", toEmail);
            var sent = await SendEmailAsync(toEmail, subject, body);
            return sent;
        }

        public async Task SendAppointmentReminder(string patientEmail, string patientName, string appointmentDate)
        {
            var subject = "تذكير بموعد العيادة · Clinic Appointment Reminder";
            var body = $"<p>عزيزي {patientName}، نود تذكيرك بموعدك القادم بتاريخ: <strong>{appointmentDate}</strong>.</p>";
            await SendEmailAsync(patientEmail, subject, body);
        }
    }
}