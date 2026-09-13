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

            // 1. Fetch unconfirmed doctor appointments (Status == Pending or Pending Confirmation)
            var pendingApptsQuery = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Department)
                .Include(a => a.Department)
                .Include(a => a.ReferredByDoctor)
                .Where(a => (a.Status == "Pending" || a.Status == "Pending Confirmation" || a.Status == AppointmentStatus.Pending) &&
                            (currentClinicId == Guid.Empty || a.ClinicId == currentClinicId));

            var pendingAppointments = await pendingApptsQuery
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .Take(25)
                .ToListAsync();

            var pendingApptsCount = await _context.Appointments
                .Where(a => (a.Status == "Pending" || a.Status == "Pending Confirmation" || a.Status == AppointmentStatus.Pending) &&
                            (currentClinicId == Guid.Empty || a.ClinicId == currentClinicId))
                .CountAsync();

            // 2. Fetch unresolved referrals (Status == Pending)
            var pendingReferralsQuery = _context.ReferralRequests
                .Include(r => r.Patient)
                .Include(r => r.FromDoctor)
                .Include(r => r.TargetDepartment)
                .Include(r => r.TargetDoctor)
                .Where(r => r.Status == ReferralStatus.Pending &&
                            (currentClinicId == Guid.Empty || r.ClinicId == currentClinicId));

            var pendingReferrals = await pendingReferralsQuery
                .OrderByDescending(r => r.CreatedAt)
                .Take(25)
                .ToListAsync();

            var pendingReferralsCount = await _context.ReferralRequests
                .Where(r => r.Status == ReferralStatus.Pending &&
                            (currentClinicId == Guid.Empty || r.ClinicId == currentClinicId))
                .CountAsync();

            // 3. Fetch unread notifications
            var notifQuery = _context.Notifications
                .Where(n => !n.IsRead && (currentClinicId == Guid.Empty || n.ClinicId == currentClinicId));

            var notifications = await notifQuery
                .OrderByDescending(n => n.CreatedAt)
                .Take(25)
                .ToListAsync();

            // Build unified items list for dropdown rendering
            var items = new List<object>();
            var trackedApptIds = new HashSet<int>();
            var trackedRefIds = new HashSet<int>();

            // A) Add unconfirmed doctor appointments
            foreach (var a in pendingAppointments)
            {
                trackedApptIds.Add(a.AppointmentId);
                var docName = a.Doctor?.DoctorName ?? "Doctor";
                if (!docName.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) && !docName.StartsWith("د.", StringComparison.OrdinalIgnoreCase))
                {
                    docName = "Dr. " + docName;
                }

                var timeFormatted = a.AppointmentTime.ToString(@"hh\:mm");
                var dateFormatted = a.AppointmentDate.ToString("yyyy-MM-dd");
                var pName = a.Patient?.PatientName ?? "Patient";
                var targetDeptName = a.Department?.DepartmentName ?? a.Doctor?.Department?.DepartmentName ?? "General Practice";
                var refDocName = a.ReferredByDoctor != null ? (a.ReferredByDoctor.DoctorName.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) || a.ReferredByDoctor.DoctorName.StartsWith("د.", StringComparison.OrdinalIgnoreCase) ? a.ReferredByDoctor.DoctorName : $"Dr. {a.ReferredByDoctor.DoctorName}") : null;
                var reason = a.ReferralReason ?? a.Notes ?? "";
                var isTransfer = !string.IsNullOrEmpty(refDocName) || !string.IsNullOrEmpty(a.ReferralReason);

                items.Add(new
                {
                    id = a.AppointmentId,
                    appointmentId = a.AppointmentId,
                    type = "Appointment",
                    title = "Doctor Booking Request",
                    badgeText = "Doctor Booking Request",
                    badgeClass = "amber",
                    patientName = pName,
                    patientId = a.PatientId,
                    doctorName = docName,
                    doctorId = a.DoctorId,
                    date = dateFormatted,
                    time = timeFormatted,
                    notes = a.Notes ?? "",
                    referralReason = reason,
                    referredByDoctorName = refDocName,
                    targetDepartmentName = targetDeptName,
                    isTransfer = isTransfer,
                    message = isTransfer && !string.IsNullOrEmpty(refDocName)
                        ? $"Transferred from {refDocName} -> {targetDeptName}: {reason}"
                        : $"{docName} requested booking for {pName} at {timeFormatted} on {dateFormatted}.",
                    status = a.Status,
                    timeAgo = a.AppointmentDate.Date == DateTime.Today ? $"Today at {timeFormatted}" : $"{dateFormatted} {timeFormatted}",
                    createdAt = a.AppointmentDate.Date.Add(a.AppointmentTime)
                });
            }

            // B) Add unresolved referrals
            foreach (var r in pendingReferrals)
            {
                trackedRefIds.Add(r.Id);
                var fromDoc = r.FromDoctor?.DoctorName ?? "Doctor";
                if (!fromDoc.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase) && !fromDoc.StartsWith("د.", StringComparison.OrdinalIgnoreCase))
                {
                    fromDoc = "Dr. " + fromDoc;
                }
                var deptName = r.TargetDepartment?.DepartmentName ?? "Specialty";
                var pName = r.Patient?.PatientName ?? "Patient";

                items.Add(new
                {
                    id = r.Id,
                    referralId = r.Id,
                    type = "Referral",
                    title = "Internal Referral",
                    badgeText = "Internal Referral",
                    badgeClass = "emerald",
                    patientName = pName,
                    patientId = r.PatientId,
                    doctorName = fromDoc,
                    referredByDoctorName = fromDoc,
                    departmentName = deptName,
                    targetDepartmentName = deptName,
                    targetDepartmentId = r.TargetDepartmentId,
                    targetDoctorId = r.TargetDoctorId,
                    notes = r.ReferralReason,
                    referralReason = r.ReferralReason,
                    isTransfer = true,
                    message = $"Transferred from {fromDoc} -> {deptName}: {r.ReferralReason}",
                    status = r.Status.ToString(),
                    timeAgo = GetTimeAgo(r.CreatedAt),
                    createdAt = r.CreatedAt
                });
            }

            // C) Add other notifications (avoid duplicates if already added via appointment or referral)
            foreach (var n in notifications)
            {
                if (n.AppointmentId.HasValue && trackedApptIds.Contains(n.AppointmentId.Value))
                    continue;
                if (n.ReferralRequestId.HasValue && trackedRefIds.Contains(n.ReferralRequestId.Value))
                    continue;

                items.Add(new
                {
                    id = n.Id,
                    notificationId = n.Id,
                    type = n.Type,
                    title = n.Title,
                    badgeText = n.Type == "Referral" ? "Internal Referral" : n.Type == "Appointment" ? "Doctor Booking Request" : "Notification",
                    badgeClass = n.Type == "Referral" ? "emerald" : n.Type == "Appointment" ? "amber" : "blue",
                    patientName = n.PatientName ?? "",
                    patientId = n.PatientId,
                    doctorName = n.DoctorName ?? "",
                    departmentName = n.DepartmentName ?? "",
                    referralId = n.ReferralRequestId,
                    appointmentId = n.AppointmentId,
                    targetDepartmentId = 0,
                    targetDoctorId = (int?)null,
                    notes = "",
                    message = n.Message,
                    status = "Unread",
                    timeAgo = GetTimeAgo(n.CreatedAt),
                    createdAt = n.CreatedAt
                });
            }

            var unreadNotifCount = notifications.Count(n => !n.AppointmentId.HasValue && !n.ReferralRequestId.HasValue);
            var totalBadgeCount = pendingApptsCount + pendingReferralsCount + unreadNotifCount;

            return Json(new
            {
                count = totalBadgeCount,
                unreadCount = notifications.Count,
                pendingReferrals = pendingReferralsCount,
                pendingAppointments = pendingApptsCount,
                items = items
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
                DepartmentId = doctor.DepartmentId,
                AppointmentDate = appointmentDate.Date,
                AppointmentTime = appointmentTime,
                Status = "Confirmed",
                Notes = notes ?? (referralId.HasValue ? $"Scheduled from internal referral #{referralId.Value}" : "Scheduled via reception notification")
            };

            // Update referral status if associated
            if (referralId.HasValue && referralId.Value > 0)
            {
                var referral = await _context.ReferralRequests.FindAsync(referralId.Value);
                if (referral != null)
                {
                    referral.Status = ReferralStatus.Scheduled;
                    appointment.ReferredByDoctorId = referral.FromDoctorId;
                    appointment.ReferralReason = referral.ReferralReason;
                    appointment.DepartmentId = referral.TargetDepartmentId;
                    if (string.IsNullOrWhiteSpace(notes))
                    {
                        appointment.Notes = $"Transferred from internal referral #{referral.Id}: {referral.ReferralReason}";
                    }
                }
            }

            _context.Appointments.Add(appointment);

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

        // POST: Notifications/ConfirmAppointment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAppointment(int id)
        {
            var currentClinicId = User.GetClinicId();
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == id && (currentClinicId == Guid.Empty || a.ClinicId == currentClinicId));

            if (appointment == null)
            {
                return NotFound(new { success = false, message = "Appointment not found." });
            }

            appointment.Status = AppointmentStatus.Confirmed;

            // Also mark any associated notification as read
            var relatedNotifs = await _context.Notifications
                .Where(n => n.AppointmentId == id && !n.IsRead)
                .ToListAsync();
            foreach (var n in relatedNotifs)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Appointment confirmed successfully!",
                appointmentId = id
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
