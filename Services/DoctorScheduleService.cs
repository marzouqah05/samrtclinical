using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Text;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Implements doctor weekly schedule retrieval and HTML email digest dispatch.
    /// </summary>
    public class DoctorScheduleService : IDoctorScheduleService
    {
        private readonly ClinicDbContext _context;
        private readonly ISettingsService _settings;
        private readonly IConfiguration _config;
        private readonly ILogger<DoctorScheduleService> _logger;

        public DoctorScheduleService(
            ClinicDbContext context,
            ISettingsService settings,
            IConfiguration config,
            ILogger<DoctorScheduleService> logger)
        {
            _context  = context;
            _settings = settings;
            _config   = config;
            _logger   = logger;
        }

        // ── Weekly Schedules ──────────────────────────────────────────────────

        public async Task<List<DoctorWeeklyScheduleDto>> GetWeeklySchedulesAsync()
        {
            var today    = DateTime.UtcNow.Date;
            var weekEnd  = today.AddDays(7);

            var doctors = await _context.Doctors
                .Include(d => d.Appointments)
                    .ThenInclude(a => a.Patient)
                .ToListAsync();

            var result = new List<DoctorWeeklyScheduleDto>();

            foreach (var doctor in doctors)
            {
                var weekAppointments = doctor.Appointments
                    .Where(a => a.AppointmentDate.Date >= today
                             && a.AppointmentDate.Date <= weekEnd
                             && a.Status != "Cancelled")
                    .OrderBy(a => a.AppointmentDate)
                    .ThenBy(a => a.AppointmentTime)
                    .Select(a => new AppointmentSummaryDto
                    {
                        AppointmentId   = a.AppointmentId,
                        AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                        AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
                        PatientName     = a.Patient?.PatientName ?? "Unknown",
                        PatientPhone    = a.Patient?.PhoneNumber ?? "",
                        Reason          = a.Notes,
                        Status          = a.Status,
                    })
                    .ToList();

                result.Add(new DoctorWeeklyScheduleDto
                {
                    DoctorId       = doctor.DoctorId,
                    DoctorName     = doctor.DoctorName,
                    Specialization = doctor.Specialization,
                    DoctorEmail    = doctor.DoctorEmail,
                    DoctorPhone    = doctor.DoctorPhone,
                    WeekStart      = today.ToString("yyyy-MM-dd"),
                    WeekEnd        = weekEnd.ToString("yyyy-MM-dd"),
                    Appointments   = weekAppointments,
                });
            }

            return result;
        }

        // ── Send Digest for One Doctor ─────────────────────────────────────────

        public async Task<bool> SendDoctorWeeklyDigestEmailAsync(int doctorId)
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            if (doctor == null)
            {
                _logger.LogWarning("[WeeklyDigest] Doctor {DoctorId} not found.", doctorId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(doctor.DoctorEmail))
            {
                _logger.LogWarning("[WeeklyDigest] Doctor {Name} has no email configured — skipping digest.", doctor.DoctorName);
                return false;
            }

            var allSchedules = await GetWeeklySchedulesAsync();
            var schedule = allSchedules.FirstOrDefault(s => s.DoctorId == doctorId);
            if (schedule == null) return false;

            var clinicSettings = await _settings.GetSettingsAsync();
            var htmlBody = BuildHtmlEmail(schedule, clinicSettings);

            return await SendEmailAsync(
                to: doctor.DoctorEmail,
                subject: $"📅 Your Weekly Schedule — {schedule.WeekStart} to {schedule.WeekEnd} | {clinicSettings.ClinicName}",
                htmlBody: htmlBody,
                clinicSettings: clinicSettings);
        }

        // ── Send Digest for All Doctors ────────────────────────────────────────

        public async Task SendAllDoctorDigestsAsync()
        {
            var doctors = await _context.Doctors
                .Where(d => !string.IsNullOrEmpty(d.DoctorEmail))
                .ToListAsync();

            foreach (var doctor in doctors)
            {
                try
                {
                    await SendDoctorWeeklyDigestEmailAsync(doctor.DoctorId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WeeklyDigest] Failed to send digest to Dr. {Name}.", doctor.DoctorName);
                }
            }

            _logger.LogInformation("[WeeklyDigest] Digest cycle complete. Sent to {Count} doctor(s).", doctors.Count);
        }

        // ── HTML Email Builder ─────────────────────────────────────────────────

        private static string BuildHtmlEmail(DoctorWeeklyScheduleDto schedule, ClinicSettingsViewModel clinic)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.AppendLine("<title>Weekly Schedule Digest</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("  body{font-family:'Segoe UI',Arial,sans-serif;background:#f1f5f9;margin:0;padding:0;}");
            sb.AppendLine("  .container{max-width:700px;margin:32px auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,.1);}");
            sb.AppendLine("  .header{background:linear-gradient(135deg,#1e293b 0%,#312e81 100%);padding:32px 36px;color:#fff;}");
            sb.AppendLine("  .header h1{margin:0 0 6px;font-size:22px;font-weight:700;}");
            sb.AppendLine("  .header p{margin:0;color:rgba(255,255,255,.65);font-size:14px;}");
            sb.AppendLine("  .body{padding:28px 36px;}");
            sb.AppendLine("  .meta-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;padding:16px 20px;margin-bottom:24px;display:flex;flex-wrap:wrap;gap:16px;}");
            sb.AppendLine("  .meta-item label{font-size:11px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.05em;display:block;margin-bottom:3px;}");
            sb.AppendLine("  .meta-item span{font-size:14px;font-weight:600;color:#0f172a;}");
            sb.AppendLine("  table{width:100%;border-collapse:collapse;font-size:13.5px;}");
            sb.AppendLine("  th{background:#f8fafc;padding:10px 14px;text-align:left;font-weight:700;color:#64748b;text-transform:uppercase;font-size:11px;letter-spacing:.05em;border-bottom:2px solid #e2e8f0;}");
            sb.AppendLine("  td{padding:12px 14px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:top;}");
            sb.AppendLine("  tr:last-child td{border-bottom:none;}");
            sb.AppendLine("  .badge{display:inline-block;padding:3px 10px;border-radius:20px;font-size:11px;font-weight:700;}");
            sb.AppendLine("  .badge-confirmed{background:#dcfce7;color:#166534;}");
            sb.AppendLine("  .badge-pending{background:#fef9c3;color:#854d0e;}");
            sb.AppendLine("  .badge-completed{background:#dbeafe;color:#1e40af;}");
            sb.AppendLine("  .no-appts{text-align:center;padding:40px;color:#94a3b8;font-style:italic;}");
            sb.AppendLine("  .footer{background:#f8fafc;border-top:1px solid #e2e8f0;padding:20px 36px;text-align:center;font-size:12px;color:#94a3b8;}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<div class=\"container\">");

            // Header
            sb.AppendLine("  <div class=\"header\">");
            sb.AppendLine($"    <h1>📅 Weekly Schedule Digest</h1>");
            sb.AppendLine($"    <p>{clinic.ClinicName} — {schedule.WeekStart} to {schedule.WeekEnd}</p>");
            sb.AppendLine("  </div>");

            // Body
            sb.AppendLine("  <div class=\"body\">");

            // Doctor meta card
            sb.AppendLine("  <div class=\"meta-card\">");
            sb.AppendLine($"    <div class=\"meta-item\"><label>Doctor</label><span>Dr. {schedule.DoctorName}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><label>Specialization</label><span>{schedule.Specialization}</span></div>");
            sb.AppendLine($"    <div class=\"meta-item\"><label>Total Appointments</label><span>{schedule.TotalAppointments}</span></div>");
            if (!string.IsNullOrEmpty(schedule.DoctorPhone))
                sb.AppendLine($"    <div class=\"meta-item\"><label>WhatsApp</label><span>{schedule.DoctorPhone}</span></div>");
            sb.AppendLine("  </div>");

            // Appointments table
            if (schedule.Appointments.Count == 0)
            {
                sb.AppendLine("  <div class=\"no-appts\">🎉 No appointments scheduled for this week.</div>");
            }
            else
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead><tr>");
                sb.AppendLine("      <th>Date</th><th>Time</th><th>Patient</th><th>Phone</th><th>Reason</th><th>Status</th>");
                sb.AppendLine("    </tr></thead>");
                sb.AppendLine("    <tbody>");

                foreach (var appt in schedule.Appointments)
                {
                    var badgeClass = appt.Status.ToLower() switch
                    {
                        "confirmed"  => "badge-confirmed",
                        "completed"  => "badge-completed",
                        _            => "badge-pending",
                    };
                    sb.AppendLine("      <tr>");
                    sb.AppendLine($"        <td><strong>{appt.AppointmentDate}</strong></td>");
                    sb.AppendLine($"        <td>{appt.AppointmentTime}</td>");
                    sb.AppendLine($"        <td>{WebUtility.HtmlEncode(appt.PatientName)}</td>");
                    sb.AppendLine($"        <td>{WebUtility.HtmlEncode(appt.PatientPhone)}</td>");
                    sb.AppendLine($"        <td>{WebUtility.HtmlEncode(appt.Reason ?? "—")}</td>");
                    sb.AppendLine($"        <td><span class=\"badge {badgeClass}\">{appt.Status}</span></td>");
                    sb.AppendLine("      </tr>");
                }

                sb.AppendLine("    </tbody></table>");
            }

            sb.AppendLine("  </div>"); // body

            // Footer
            sb.AppendLine("  <div class=\"footer\">");
            sb.AppendLine($"    <p>{clinic.ClinicName} · {clinic.ClinicPhone} · {clinic.ClinicEmail}</p>");
            sb.AppendLine("    <p>This is an automated weekly schedule digest. Please do not reply to this email.</p>");
            sb.AppendLine("  </div>");

            sb.AppendLine("</div></body></html>");

            return sb.ToString();
        }

        // ── SMTP Dispatch ──────────────────────────────────────────────────────

        private async Task<bool> SendEmailAsync(
            string to, string subject, string htmlBody,
            ClinicSettingsViewModel clinicSettings)
        {
            try
            {
                var smtpHost     = _config["Smtp:Host"] ?? _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPortStr  = _config["Smtp:Port"] ?? _config["Email:SmtpPort"] ?? "587";
                var smtpUser     = _config["Smtp:UserName"] ?? _config["Email:SmtpUser"] ?? clinicSettings.ClinicEmail;
                var smtpPass     = _config["Smtp:Password"] ?? _config["Email:SmtpPassword"] ?? "";
                var fromAddress  = _config["Email:FromAddress"] ?? _config["Smtp:UserName"] ?? clinicSettings.ClinicEmail;
                var fromName     = _config["Email:FromName"] ?? clinicSettings.ClinicName;

                if (string.IsNullOrWhiteSpace(smtpPass) || string.IsNullOrWhiteSpace(smtpUser))
                {
                    _logger.LogWarning("[WeeklyDigest] SMTP credentials are not configured. Email not sent.");
                    return false;
                }

                int.TryParse(smtpPortStr, out int smtpPort);
                if (smtpPort == 0) smtpPort = 587;

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl   = true,
                };

                var mail = new MailMessage
                {
                    From       = new MailAddress(fromAddress, fromName),
                    Subject    = subject,
                    Body       = htmlBody,
                    IsBodyHtml = true,
                };
                mail.To.Add(to);

                await client.SendMailAsync(mail);
                _logger.LogInformation("[WeeklyDigest] Email digest sent to {To}.", to);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WeeklyDigest] Failed to send email to {To}.", to);
                return false;
            }
        }
    }
}
