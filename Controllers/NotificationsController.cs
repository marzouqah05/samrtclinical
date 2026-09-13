using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist")]
    public class NotificationsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(ClinicDbContext context, ILogger<NotificationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Notifications/GetPending
        [HttpGet]
        public async Task<IActionResult> GetPending()
        {
            var currentClinicId = User.GetClinicId();

            // 1. Fetch unread notifications
            var notifQuery = _context.Notifications
                .Where(n => !n.IsRead)
                .AsQueryable();

            if (currentClinicId != Guid.Empty && !User.IsSuperAdmin())
            {
                notifQuery = notifQuery.Where(n => n.ClinicId == currentClinicId);
            }

            var notifications = await notifQuery
                .OrderByDescending(n => n.CreatedAt)
                .Take(25)
                .Select(n => new
                {
                    id = n.Id,
                    type = n.Type,
                    title = n.Title,
                    message = n.Message,
                    doctorName = n.DoctorName,
                    patientName = n.PatientName,
                    departmentName = n.DepartmentName,
                    patientId = n.PatientId,
                    referralId = n.ReferralRequestId,
                    appointmentId = n.AppointmentId,
                    createdAt = n.CreatedAt,
                    timeAgo = GetTimeAgo(n.CreatedAt)
                })
                .ToListAsync();

            // Also retrieve target department IDs for referrals if present
            var referralIds = notifications.Where(n => n.referralId.HasValue).Select(n => n.referralId!.Value).ToList();
            var referralDict = await _context.ReferralRequests
                .Where(r => referralIds.Contains(r.Id))
                .Select(r => new { r.Id, r.TargetDepartmentId, r.TargetDoctorId })
                .ToDictionaryAsync(r => r.Id);

            var enrichedList = notifications.Select(n =>
            {
                int targetDeptId = 0;
                int? targetDocId = null;
                if (n.referralId.HasValue && referralDict.TryGetValue(n.referralId.Value, out var refInfo))
                {
                    targetDeptId = refInfo.TargetDepartmentId;
                    targetDocId = refInfo.TargetDoctorId;
                }

                return new
                {
                    n.id,
                    n.type,
                    n.title,
                    n.message,
                    n.doctorName,
                    n.patientName,
                    n.departmentName,
                    n.patientId,
                    n.referralId,
                    n.appointmentId,
                    targetDepartmentId = targetDeptId,
                    targetDoctorId = targetDocId,
                    n.timeAgo,
                    n.createdAt
                };
            }).ToList();

            // 2. Count Pending Referrals
            var pendingReferralsCount = await _context.ReferralRequests
                .Where(r => r.Status == ReferralStatus.Pending && (currentClinicId == Guid.Empty || r.ClinicId == currentClinicId))
                .CountAsync();

            // 3. Count Pending Doctor Appointments
            var pendingApptsCount = await _context.Appointments
                .Where(a => (a.Status == "Pending" || a.Status == "Pending Confirmation") && (currentClinicId == Guid.Empty || a.ClinicId == currentClinicId))
                .CountAsync();

            // Total badge items = unread notifications count or pending referrals + pending appts
            var unreadCount = enrichedList.Count;
            var totalBadgeCount = Math.Max(unreadCount, pendingReferralsCount + pendingApptsCount);

            return Json(new
            {
                count = totalBadgeCount,
                unreadCount = unreadCount,
                pendingReferrals = pendingReferralsCount,
                pendingAppointments = pendingApptsCount,
                items = enrichedList
            });
        }

        // POST: Notifications/Dismiss/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id)
        {
            var notif = await _context.Notifications.FindAsync(id);
            if (notif == null) return NotFound(new { success = false, message = "Notification not found." });

            notif.IsRead = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = id });
        }

        // POST: Notifications/MarkAllAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var currentClinicId = User.GetClinicId();
            var unread = await _context.Notifications
                .Where(n => !n.IsRead && (currentClinicId == Guid.Empty || n.ClinicId == currentClinicId))
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, count = unread.Count });
        }

        // POST: Notifications/BookSlot
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookSlot(int patientId, int doctorId, DateTime appointmentDate, TimeSpan appointmentTime, int? referralId, int? notificationId, string? notes)
        {
            var currentClinicId = User.GetClinicId();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientId == patientId);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (patient == null || doctor == null)
            {
                return BadRequest(new { success = false, message = "Invalid patient or doctor." });
            }

            var appointment = new Appointment
            {
                ClinicId = currentClinicId != Guid.Empty ? currentClinicId : null,
                PatientId = patientId,
                DoctorId = doctorId,
                AppointmentDate = appointmentDate.Date,
                AppointmentTime = appointmentTime,
                Status = "Confirmed",
                Notes = notes ?? (referralId.HasValue ? $"Scheduled from internal referral #{referralId.Value}" : "Scheduled via reception notification")
            };

            _context.Appointments.Add(appointment);

            // Update referral status if associated
            if (referralId.HasValue && referralId.Value > 0)
            {
                var referral = await _context.ReferralRequests.FindAsync(referralId.Value);
                if (referral != null)
                {
                    referral.Status = ReferralStatus.Scheduled;
                }
            }

            // Mark notification as read
            if (notificationId.HasValue && notificationId.Value > 0)
            {
                var notif = await _context.Notifications.FindAsync(notificationId.Value);
                if (notif != null)
                {
                    notif.IsRead = true;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Appointment booked and referral scheduled successfully!",
                appointmentId = appointment.AppointmentId
            });
        }

        private static string GetTimeAgo(DateTime dt)
        {
            var span = DateTime.UtcNow - dt;
            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} mins ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} {(span.TotalHours < 2 ? "hr" : "hrs")} ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} {(span.TotalDays < 2 ? "day" : "days")} ago";
            return dt.ToString("dd MMM");
        }
    }
}
