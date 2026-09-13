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
    [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist,Doctor")]
    public class ReferralsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly ILogger<ReferralsController> _logger;

        public ReferralsController(ClinicDbContext context, ILogger<ReferralsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Referrals/GetDepartments
        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            var currentClinicId = User.GetClinicId();
            var query = _context.Departments.AsQueryable();
            if (currentClinicId != Guid.Empty && !User.IsSuperAdmin())
            {
                query = query.Where(d => d.ClinicId == currentClinicId);
            }

            var departments = await query
                .OrderBy(d => d.DepartmentName)
                .Select(d => new
                {
                    id = d.DepartmentId,
                    name = d.DepartmentName,
                    abbr = d.DepartmentAbbr
                })
                .ToListAsync();

            return Json(departments);
        }

        // GET: Referrals/GetDoctorsByDepartment?departmentId=5
        [HttpGet]
        public async Task<IActionResult> GetDoctorsByDepartment(int departmentId)
        {
            var currentClinicId = User.GetClinicId();
            var query = _context.Doctors.Where(d => d.DepartmentId == departmentId).AsQueryable();
            if (currentClinicId != Guid.Empty && !User.IsSuperAdmin())
            {
                query = query.Where(d => d.ClinicId == currentClinicId);
            }

            var doctors = await query
                .OrderBy(d => d.DoctorName)
                .Select(d => new
                {
                    id = d.DoctorId,
                    name = d.DoctorName.StartsWith("Dr.") ? d.DoctorName : "Dr. " + d.DoctorName,
                    specialization = d.Specialization,
                    fee = d.ConsultationFee
                })
                .ToListAsync();

            return Json(doctors);
        }

        // POST: Referrals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int patientId, int targetDepartmentId, int? targetDoctorId, string referralReason)
        {
            if (patientId <= 0 || targetDepartmentId <= 0 || string.IsNullOrWhiteSpace(referralReason))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)
                {
                    return BadRequest(new { success = false, message = "Please fill in all required referral fields." });
                }
                TempData["Error"] = "Please fill in all required referral fields.";
                return RedirectToAction("Details", "Patients", new { id = patientId });
            }

            var currentClinicId = User.GetClinicId();

            // 1. Resolve Patient
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientId == patientId);
            if (patient == null)
            {
                return NotFound(new { success = false, message = "Patient record not found." });
            }

            // 2. Resolve FromDoctor
            int fromDoctorId = 0;
            string fromDoctorName = "Attending Physician";

            if (User.IsInRole("Doctor") || User.IsDoctor())
            {
                var docId = await User.GetDoctorIdAsync(_context);
                if (docId.HasValue)
                {
                    fromDoctorId = docId.Value;
                    var doc = await _context.Doctors.FindAsync(fromDoctorId);
                    if (doc != null) fromDoctorName = doc.DoctorName;
                }
            }

            if (fromDoctorId == 0)
            {
                // Fallback to first doctor matching clinic or associated
                var fallbackDoc = await _context.Doctors.FirstOrDefaultAsync(d => currentClinicId == Guid.Empty || d.ClinicId == currentClinicId);
                if (fallbackDoc != null)
                {
                    fromDoctorId = fallbackDoc.DoctorId;
                    fromDoctorName = fallbackDoc.DoctorName;
                }
            }

            // 3. Resolve Target Department & Target Doctor
            var targetDept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == targetDepartmentId);
            string targetDeptName = targetDept?.DepartmentName ?? "Department";

            string? targetDocName = null;
            if (targetDoctorId.HasValue && targetDoctorId.Value > 0)
            {
                var targetDoc = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorId == targetDoctorId.Value);
                targetDocName = targetDoc?.DoctorName;
            }

            // Clean doctor name formatting
            var cleanFromDoc = System.Text.RegularExpressions.Regex.Replace(fromDoctorName, @"^(Dr\.\s*|د\.\s*)+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

            // 4. Create ReferralRequest entity
            var referral = new ReferralRequest
            {
                ClinicId = currentClinicId != Guid.Empty ? currentClinicId : null,
                PatientId = patientId,
                FromDoctorId = fromDoctorId,
                TargetDepartmentId = targetDepartmentId,
                TargetDoctorId = targetDoctorId.HasValue && targetDoctorId.Value > 0 ? targetDoctorId.Value : null,
                ReferralReason = referralReason.Trim(),
                Status = ReferralStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.ReferralRequests.Add(referral);
            await _context.SaveChangesAsync();

            // 5. Create Notification for Receptionist & Admin
            var notifMessage = $"Dr. {cleanFromDoc} referred Patient {patient.PatientName} to {targetDeptName} - Reason: {referralReason.Trim()}";
            if (!string.IsNullOrEmpty(targetDocName))
            {
                var cleanTargetDoc = System.Text.RegularExpressions.Regex.Replace(targetDocName, @"^(Dr\.\s*|د\.\s*)+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                notifMessage += $" (Preferred: Dr. {cleanTargetDoc})";
            }

            var notification = new Notification
            {
                ClinicId = currentClinicId != Guid.Empty ? currentClinicId : null,
                Title = $"Referral: {patient.PatientName}",
                Message = notifMessage,
                Type = "Referral",
                TargetRole = "Receptionist",
                PatientId = patientId,
                ReferralRequestId = referral.Id,
                DoctorName = cleanFromDoc,
                PatientName = patient.PatientName,
                DepartmentName = targetDeptName,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Doctor {Doctor} created referral #{ReferralId} for patient {Patient} to {Department}.",
                cleanFromDoc, referral.Id, patient.PatientName, targetDeptName);

            const string successMsg = "Referral sent to reception for scheduling.";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)
            {
                return Json(new
                {
                    success = true,
                    message = successMsg,
                    referralId = referral.Id,
                    patientName = patient.PatientName,
                    departmentName = targetDeptName
                });
            }

            TempData["Success"] = successMsg;
            return RedirectToAction("Details", "Patients", new { id = patientId });
        }

        // POST: Referrals/RequestFollowUp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestFollowUp(int patientId, string? notes)
        {
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientId == patientId);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });

            var currentClinicId = User.GetClinicId();
            int fromDoctorId = 0;
            string fromDoctorName = User.Identity?.Name ?? "Doctor";

            if (User.IsInRole("Doctor") || User.IsDoctor())
            {
                var docId = await User.GetDoctorIdAsync(_context);
                if (docId.HasValue)
                {
                    fromDoctorId = docId.Value;
                    var doc = await _context.Doctors.FindAsync(fromDoctorId);
                    if (doc != null) fromDoctorName = doc.DoctorName;
                }
            }

            var cleanDocName = System.Text.RegularExpressions.Regex.Replace(fromDoctorName, @"^(Dr\.\s*|د\.\s*)+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

            var notification = new Notification
            {
                ClinicId = currentClinicId != Guid.Empty ? currentClinicId : null,
                Title = $"Follow-up: {patient.PatientName}",
                Message = $"Dr. {cleanDocName} requested a follow-up appointment for Patient {patient.PatientName}." + (!string.IsNullOrWhiteSpace(notes) ? $" - Notes: {notes.Trim()}" : ""),
                Type = "FollowUp",
                TargetRole = "Receptionist",
                PatientId = patientId,
                DoctorName = cleanDocName,
                PatientName = patient.PatientName,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Follow-up request sent to reception." });
        }

        // POST: Referrals/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist")]
        public async Task<IActionResult> UpdateStatus(int id, ReferralStatus status)
        {
            var referral = await _context.ReferralRequests.FindAsync(id);
            if (referral == null) return NotFound(new { success = false, message = "Referral not found." });

            referral.Status = status;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Referral status updated to {status}." });
        }
    }
}
