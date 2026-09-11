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
                var fromName = _config["SmtpSettings:SenderName"] ?? "ClinicFlow Systems";
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
            var subject = "رمز التحقق لتفعيل حساب العيادة - ClinicFlow";

            var body = $@"
                <div style='direction: rtl; font-family: sans-serif; padding: 20px; border: 1px solid #e5e7eb; border-radius: 8px;'>
                    <h2 style='color: #059669;'>مرحباً بك في ClinicFlow</h2>
                    <p>رمز التحقق لتفعيل حساب العيادة الخاص بك هو:</p>
                    <div style='font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #059669; padding: 10px 0;'>{otpCode}</div>
                    <p style='color: #6b7280; font-size: 13px;'>هذا الرمز صالح لمدة 10 دقائق فقط.</p>
                </div>";

            // Always log OTP to Console / Terminal as a safety fallback
            Console.WriteLine($"\n[REGISTRATION OTP]: {otpCode} for {toEmail}\n");

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