using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin,Receptionist,Doctor")]
    public class PatientsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IDataImportExportService _importExportService;
        private readonly IWebHostEnvironment _env;

        // Allowed MIME types for patient file uploads
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "application/pdf"
        };
        private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB

        public PatientsController(
            ClinicDbContext context,
            IDataImportExportService importExportService,
            IWebHostEnvironment env)
        {
            _context             = context;
            _importExportService = importExportService;
            _env                 = env;
        }

        private static string T(string ar, string en)
            => CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ar" ? ar : en;

        #region Bulk Import & Export

        /// <summary>GET /Patients/DownloadTemplate</summary>
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csvBytes = _importExportService.GeneratePatientTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Patients_Template.csv");
        }

        /// <summary>GET /Patients/Export</summary>
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var csvBytes = await _importExportService.ExportPatientsToCsvAsync();
            string fileName = $"Patients_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }

        /// <summary>POST /Patients/Import</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV file to upload.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".csv")
            {
                TempData["Error"] = "Invalid file type. Only CSV (.csv) files are supported for import.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _importExportService.ImportPatientsFromCsvAsync(stream);

                if (result.ImportedCount > 0 && result.SkippedCount == 0)
                    TempData["Success"] = $"Successfully imported {result.ImportedCount} patient(s).";
                else if (result.ImportedCount > 0 && result.SkippedCount > 0)
                    TempData["Warning"] = $"Imported {result.ImportedCount} patient(s) successfully, but {result.SkippedCount} row(s) were skipped.";
                else if (result.ImportedCount == 0 && result.SkippedCount > 0)
                    TempData["Error"] = $"Import failed: 0 patients imported. {result.SkippedCount} row(s) had validation errors.";
                else
                    TempData["Warning"] = "The uploaded file contained no valid patient rows.";

                if (result.ErrorMessages.Any())
                    TempData["ImportErrors"] = JsonSerializer.Serialize(result.ErrorMessages.Take(25));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred during import: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        #endregion

        // ── CRUD ──────────────────────────────────────────────────────────────

        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Patients.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var all = await query.ToListAsync();
                all = all.Where(p =>
                    (p.PatientName != null && p.PatientName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (p.PhoneNumber != null && p.PhoneNumber.Contains(search)) ||
                    (p.NationalId  != null && p.NationalId.Contains(search))
                ).ToList();
                return View(all);
            }

            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var patient = await _context.Patients
                .Include(p => p.Appointments).ThenInclude(a => a.Doctor)
                .Include(p => p.Treatments)
                .FirstOrDefaultAsync(m => m.PatientId == id);

            if (patient == null) return NotFound();

            var medicalRecords = await _context.MedicalRecords
                .Where(r => r.PatientId == id)
                .Include(r => r.Doctor)
                .Include(r => r.Attachments)
                .OrderByDescending(r => r.VisitDate)
                .ToListAsync();

            var attachments = await _context.PatientAttachments
                .Where(a => a.PatientId == id)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();

            var doctors = await _context.Doctors
                .OrderBy(d => d.DoctorName)
                .ToListAsync();

            var vm = new PatientDetailsViewModel
            {
                Patient        = patient,
                Appointments   = patient.Appointments.OrderByDescending(a => a.AppointmentDate).ToList(),
                Treatments     = patient.Treatments.OrderByDescending(t => t.TreatmentDate).ToList(),
                MedicalRecords = medicalRecords,
                Attachments    = attachments,
                Doctors        = doctors
            };

            return View(vm);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("PatientId,PatientName,NationalId,PhoneNumber,DOB,BloodType,Allergies,ChronicDiseases,Notes")]
            Patient patient)
        {
            if (ModelState.IsValid)
            {
                bool exists = await _context.Patients.AnyAsync(p => p.NationalId == patient.NationalId);
                if (exists)
                {
                    ModelState.AddModelError("NationalId", "الرقم الوطني مسجل مسبقاً لمريض آخر.");
                    return View(patient);
                }

                _context.Add(patient);
                await _context.SaveChangesAsync();
                TempData["Success"] = T("تمت إضافة المريض بنجاح.", "Patient added successfully.");
                return RedirectToAction(nameof(Index));
            }
            return View(patient);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();
            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("PatientId,PatientName,NationalId,PhoneNumber,DOB,BloodType,Allergies,ChronicDiseases,Notes")]
            Patient patient)
        {
            if (id != patient.PatientId) return NotFound();

            var dbPatient = await _context.Patients.FindAsync(id);
            if (dbPatient == null) return NotFound();

            // Check NationalId uniqueness, excluding the current patient
            bool exists = await _context.Patients.AnyAsync(p => p.NationalId == patient.NationalId && p.PatientId != patient.PatientId);
            if (exists)
            {
                ModelState.AddModelError("NationalId", "الرقم الوطني مسجل مسبقاً لمريض آخر.");
                return View(patient);
            }

            ModelState.Clear();

            try
            {
                dbPatient.PatientName     = patient.PatientName;
                dbPatient.NationalId      = patient.NationalId;
                dbPatient.PhoneNumber     = patient.PhoneNumber;
                dbPatient.DOB             = patient.DOB;
                dbPatient.BloodType       = patient.BloodType;
                dbPatient.Allergies       = patient.Allergies;
                dbPatient.ChronicDiseases = patient.ChronicDiseases;
                dbPatient.Notes           = patient.Notes;

                _context.Update(dbPatient);
                await _context.SaveChangesAsync();

                TempData["Success"] = T("تم تحديث بيانات المريض بنجاح.", "Patient updated successfully.");
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PatientExists(patient.PatientId)) return NotFound();
                throw;
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var patient = await _context.Patients.FirstOrDefaultAsync(m => m.PatientId == id);
            if (patient == null) return NotFound();
            return View(patient);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.Appointments)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null) return NotFound();

            if (patient.Appointments?.Any() == true)
                _context.Appointments.RemoveRange(patient.Appointments);

            // Delete uploaded files from disk
            var uploads = await _context.PatientAttachments
                .Where(a => a.PatientId == id).ToListAsync();
            foreach (var att in uploads)
            {
                var physical = Path.Combine(_env.WebRootPath, att.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);
            }

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();

            TempData["Success"] = T("تم حذف المريض وجميع بياناته بنجاح.", "Patient and related data deleted successfully.");
            return RedirectToAction(nameof(Index));
        }

        // ── Medical Records ───────────────────────────────────────────────────

        /// <summary>POST /Patients/AddMedicalRecord</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMedicalRecord(
            int patientId, int? doctorId, DateTime visitDate,
            string chiefComplaint, string? diagnosis,
            string? treatmentPlan, string? notes)
        {
            if (!PatientExists(patientId)) return NotFound();

            if (string.IsNullOrWhiteSpace(chiefComplaint))
            {
                TempData["Error"] = T("الشكوى الرئيسية مطلوبة.", "Chief complaint is required.");
                return RedirectToAction(nameof(Details), new { id = patientId });
            }

            // Auto-detect DoctorId if user is logged in as Doctor and doctorId was not provided
            if (!doctorId.HasValue || doctorId == 0)
            {
                var doctorClaim = User.FindFirst("DoctorId")?.Value;
                if (int.TryParse(doctorClaim, out var parsedDocId))
                {
                    doctorId = parsedDocId;
                }
                else
                {
                    var userName = User.Identity?.Name;
                    if (!string.IsNullOrEmpty(userName))
                    {
                        var doc = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorName == userName);
                        if (doc != null) doctorId = doc.DoctorId;
                    }
                }
            }

            var record = new MedicalRecord
            {
                PatientId      = patientId,
                DoctorId       = doctorId == 0 ? null : doctorId,
                VisitDate      = visitDate == default ? DateTime.Today : visitDate,
                ChiefComplaint = chiefComplaint.Trim(),
                Diagnosis      = diagnosis?.Trim(),
                TreatmentPlan  = treatmentPlan?.Trim(),
                Notes          = notes?.Trim(),
                CreatedAt      = DateTime.UtcNow
            };

            _context.MedicalRecords.Add(record);
            await _context.SaveChangesAsync();

            TempData["Success"] = T("تمت إضافة السجل الطبي بنجاح.", "Medical record added successfully.");
            return RedirectToAction(nameof(Details), new { id = patientId, tab = "history" });
        }

        // ── File Attachments ──────────────────────────────────────────────────

        /// <summary>POST /Patients/UploadAttachment</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(
            int patientId, int? medicalRecordId,
            string category, IFormFile? file)
        {
            if (!PatientExists(patientId)) return NotFound();

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = T("الرجاء اختيار ملف.", "Please select a file.");
                return RedirectToAction(nameof(Details), new { id = patientId, tab = "attachments" });
            }

            if (file.Length > MaxFileBytes)
            {
                TempData["Error"] = T("حجم الملف يتجاوز الحد الأقصى (10 ميجابايت).",
                                      "File size exceeds the maximum allowed (10 MB).");
                return RedirectToAction(nameof(Details), new { id = patientId, tab = "attachments" });
            }

            if (!AllowedMimeTypes.Contains(file.ContentType))
            {
                TempData["Error"] = T("نوع الملف غير مدعوم. الأنواع المسموح بها: PDF، PNG، JPEG.",
                                      "Unsupported file type. Allowed: PDF, PNG, JPEG.");
                return RedirectToAction(nameof(Details), new { id = patientId, tab = "attachments" });
            }

            // Build save path: wwwroot/uploads/patients/{patientId}/{guid}{ext}
            var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
            var safeFile = $"{Guid.NewGuid():N}{ext}";
            var folder   = Path.Combine(_env.WebRootPath, "uploads", "patients", patientId.ToString());
            Directory.CreateDirectory(folder);

            var physical = Path.Combine(folder, safeFile);
            using (var fs = System.IO.File.Create(physical))
                await file.CopyToAsync(fs);

            var serverPath = $"/uploads/patients/{patientId}/{safeFile}";

            var attachment = new PatientAttachment
            {
                PatientId       = patientId,
                MedicalRecordId = medicalRecordId == 0 ? null : medicalRecordId,
                FileName        = Path.GetFileName(file.FileName),
                FilePath        = serverPath,
                FileType        = file.ContentType,
                Category        = string.IsNullOrWhiteSpace(category) ? "Other" : category,
                UploadedAt      = DateTime.UtcNow
            };

            _context.PatientAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            TempData["Success"] = T("تم رفع الملف بنجاح.", "File uploaded successfully.");
            return RedirectToAction(nameof(Details), new { id = patientId, tab = "attachments" });
        }

        /// <summary>POST /Patients/DeleteAttachment</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int attachmentId, int patientId)
        {
            var att = await _context.PatientAttachments.FindAsync(attachmentId);
            if (att == null) return NotFound();

            var physical = Path.Combine(_env.WebRootPath, att.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(physical))
                System.IO.File.Delete(physical);

            _context.PatientAttachments.Remove(att);
            await _context.SaveChangesAsync();

            TempData["Success"] = T("تم حذف الملف.", "Attachment deleted.");
            return RedirectToAction(nameof(Details), new { id = patientId, tab = "attachments" });
        }

        // ── PDF Export ────────────────────────────────────────────────────────

        /// <summary>GET /Patients/ExportMedicalReportPdf/{id}</summary>
        [HttpGet]
        public async Task<IActionResult> ExportMedicalReportPdf(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.Appointments).ThenInclude(a => a.Doctor)
                .Include(p => p.Treatments)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null) return NotFound();

            var medicalRecords = await _context.MedicalRecords
                .Where(r => r.PatientId == id)
                .Include(r => r.Doctor)
                .OrderByDescending(r => r.VisitDate)
                .ToListAsync();

            var attachments = await _context.PatientAttachments
                .Where(a => a.PatientId == id)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();

            // Read clinic settings for branding
            var settings = await _context.ClinicSettings
                .Where(s => s.Key == "ClinicName" || s.Key == "ClinicPhone" || s.Key == "ClinicAddress")
                .ToDictionaryAsync(s => s.Key, s => s.Value ?? "");

            settings.TryGetValue("ClinicName",    out var clinicName);
            settings.TryGetValue("ClinicPhone",   out var clinicPhone);
            settings.TryGetValue("ClinicAddress", out var clinicAddress);

            var pdfBytes = MedicalReportPdfService.Generate(
                patient,
                medicalRecords,
                patient.Appointments.ToList(),
                patient.Treatments.ToList(),
                attachments,
                clinicName    ?? "MediCare Clinic",
                clinicPhone   ?? "",
                clinicAddress ?? "");

            var safeName = patient.PatientName.Replace(" ", "_");
            var fileName = $"MedicalReport_{safeName}_{DateTime.Now:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private bool PatientExists(int id) => _context.Patients.Any(e => e.PatientId == id);
    }
}