using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
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
        private readonly IHttpClientFactory? _httpClientFactory;
        private static readonly HttpClient _defaultHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public EmailService(IConfiguration config, ILogger<EmailService> logger, IHttpClientFactory? httpClientFactory = null)
        {
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        async Task IEmailSender.SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return false;

            var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? _config["RESEND_API_KEY"];

            // ── 1. Resend REST API Delivery (Port 443 HTTPS) ───────────────
            if (!string.IsNullOrWhiteSpace(resendApiKey))
            {
                try
                {
                    _logger.LogInformation("[EmailService] Sending email to {To} via Resend REST API...", toEmail);
                    var client = _httpClientFactory?.CreateClient() ?? _defaultHttpClient;

                    using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resendApiKey.Trim());

                    var fromEmail = _config["Resend:FromEmail"] ?? "ClinicFlow <onboarding@resend.dev>";
                    var payload = new
                    {
                        from = fromEmail,
                        to = new[] { toEmail.Trim() },
                        subject = subject,
                        html = htmlBody
                    };

                    var json = JsonSerializer.Serialize(payload);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var response = await client.SendAsync(request, cts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("[EmailService] Resend email dispatched successfully to {To}. Response: {Response}", toEmail, responseBody);
                        return true;
                    }
                    else
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("[EmailService] Resend API error ({StatusCode}) for {To}: {Error}", response.StatusCode, toEmail, errorBody);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[EmailService] Exception during Resend API call for {To}: {Message}. Proceeding gracefully.", toEmail, ex.Message);
                    return false;
                }
            }

            // ── 2. Fallback to SMTP if RESEND_API_KEY is not configured ───
            try
            {
                var smtpHost = _config["Smtp:Host"] ?? _config["SmtpSettings:Server"] ?? _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPortStr = _config["Smtp:Port"] ?? _config["SmtpSettings:Port"] ?? _config["Email:SmtpPort"] ?? "587";
                var smtpUser = _config["Smtp:UserName"] ?? _config["SmtpSettings:SenderEmail"] ?? _config["Email:SmtpUser"] ?? "";
                var smtpPass = _config["Smtp:Password"] ?? _config["SmtpSettings:Password"] ?? _config["Email:SmtpPassword"] ?? "";
                var fromAddress = _config["Email:FromAddress"] ?? _config["SmtpSettings:SenderEmail"] ?? _config["Smtp:UserName"] ?? "noreply@clinicflow.local";
                var fromName = _config["Email:FromName"] ?? _config["SmtpSettings:SenderName"] ?? "ClinicFlow";
                var enableSslStr = _config["Smtp:EnableSsl"] ?? _config["SmtpSettings:EnableSsl"];
                var enableSsl = !bool.TryParse(enableSslStr, out var ssl) || ssl;

                if (string.IsNullOrWhiteSpace(smtpPass) || string.IsNullOrWhiteSpace(smtpUser))
                {
                    _logger.LogWarning("[EmailService] SMTP credentials (UserName/Password) are not configured. Email dispatch skipped.");
                    return false;
                }

                int.TryParse(smtpPortStr, out int smtpPort);
                if (smtpPort == 0) smtpPort = 587;

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = enableSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    Timeout = 3000 // 3 seconds timeout
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                // Disconnect quickly if network is unreachable (e.g. cloud host blocking outbound SMTP)
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                await client.SendMailAsync(mail, cts.Token);
                _logger.LogInformation("[EmailService] Successfully sent email to {To}. Subject: {Subject}", toEmail, subject);
                return true;
            }
            catch (System.Net.Sockets.SocketException sockEx)
            {
                _logger.LogWarning(sockEx, "[EmailService] Network unreachable on outbound SMTP (blocked by cloud host). Proceeding gracefully.");
                return false;
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogWarning(smtpEx, "[EmailService] SMTP exception ({Message}). Proceeding gracefully.", smtpEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[EmailService] Failed to dispatch email via SMTP to {To}: {Message}. Proceeding gracefully.", toEmail, ex.Message);
                return false;
            }
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string recipientName = "")
        {
            Console.WriteLine("=================================================");
            Console.WriteLine($"--> [LIVE OTP BACKUP] Email: {toEmail} | CODE: {otpCode}");
            Console.WriteLine("=================================================");

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

            try
            {
                _logger.LogInformation("[EmailService] Sending live OTP email to {To}", toEmail);
                var sent = await SendEmailAsync(toEmail, subject, body);
                return sent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[EmailService] Non-fatal exception sending OTP email to {To}", toEmail);
                return false;
            }
        }

        public async Task SendAppointmentReminder(string patientEmail, string patientName, string appointmentDate)
        {
            var subject = "تذكير بموعد العيادة · Clinic Appointment Reminder";
            var body = $"<p>عزيزي {patientName}، نود تذكيرك بموعدك القادم بتاريخ: <strong>{appointmentDate}</strong>.</p>";
            await SendEmailAsync(patientEmail, subject, body);
        }
    }
}