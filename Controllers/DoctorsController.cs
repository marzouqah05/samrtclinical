using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist,Doctor")]
    public class DoctorsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IDataImportExportService _importExportService;
        private readonly ILogger<DoctorsController> _logger;

        public DoctorsController(ClinicDbContext context, IDataImportExportService importExportService, ILogger<DoctorsController> logger)
        {
            _context = context;
            _importExportService = importExportService;
            _logger = logger;
        }

        #region Bulk Import & Export

        /// <summary>
        /// GET /Doctors/DownloadTemplate
        /// Generates and returns a downloadable blank sample CSV template for doctor bulk import.
        /// </summary>
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csvBytes = _importExportService.GenerateDoctorTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Doctors_Template.csv");
        }

        /// <summary>
        /// GET /Doctors/Export
        /// Exports all doctors to a downloadable CSV file.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist")]
        public async Task<IActionResult> Export()
        {
            if (User.IsDoctor()) return Forbid();
            var csvBytes = await _importExportService.ExportDoctorsToCsvAsync();
            string fileName = $"Doctors_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }

        /// <summary>
        /// GET /Doctors/Import
        /// Displays the bulk doctor import page.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist")]
        public IActionResult Import()
        {
            if (User.IsDoctor()) return Forbid();
            return View();
        }

        /// <summary>
        /// POST /Doctors/Import
        /// Accepts an uploaded doctors CSV file, validates rows, and performs bulk insertion.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist")]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (User.IsDoctor()) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV or Excel file to upload.";
                return RedirectToAction(nameof(Index));
            }

            if (!ExcelImportHelper.IsSupportedFile(file))
            {
                TempData["Error"] = "Invalid file type. Only CSV (.csv) and Excel (.xlsx, .xls) files are supported for import.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = ExcelImportHelper.GetStreamAsCsv(file);
                var result = await _importExportService.ImportDoctorsFromCsvAsync(stream);

                if (result.ImportedCount > 0 && result.SkippedCount == 0)
                {
                    TempData["Success"] = $"Successfully imported {result.ImportedCount} doctor(s).";
                }
                else if (result.ImportedCount > 0 && result.SkippedCount > 0)
                {
                    TempData["Warning"] = $"Imported {result.ImportedCount} doctor(s) successfully, but {result.SkippedCount} row(s) were skipped due to validation errors.";
                }
                else if (result.ImportedCount == 0 && result.SkippedCount > 0)
                {
                    TempData["Error"] = $"Import failed: 0 doctors imported. {result.SkippedCount} row(s) had validation errors.";
                }
                else
                {
                    TempData["Warning"] = "The uploaded file contained no valid doctor rows.";
                }

                if (result.ErrorMessages.Any())
                {
                    TempData["ImportErrors"] = JsonSerializer.Serialize(result.ErrorMessages.Take(25));
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred during import: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        #endregion

        // GET: Doctors
        public async Task<IActionResult> Index(string search, int? departmentId)
        {
            var currentClinicId = User.GetClinicId();
            var doctors = _context.Doctors
                .Where(d => d.ClinicId == currentClinicId)
                .Include(d => d.Department)
                .Include(d => d.Specialty)
                .AsQueryable();

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (currentDoctorId.HasValue)
                {
                    var currentDoctor = await _context.Doctors
                        .Include(d => d.Department)
                        .FirstOrDefaultAsync(d => d.DoctorId == currentDoctorId.Value && d.ClinicId == currentClinicId);

                    if (currentDoctor != null)
                    {
                        if (currentDoctor.DepartmentId > 0)
                        {
                            doctors = doctors.Where(d => d.DepartmentId == currentDoctor.DepartmentId);
                            ViewBag.IsDoctorScoped = true;
                            ViewBag.ScopedDepartmentName = currentDoctor.Department?.DepartmentName ?? "My Department";
                        }
                        else if (!string.IsNullOrEmpty(currentDoctor.Specialization))
                        {
                            doctors = doctors.Where(d => d.Specialization == currentDoctor.Specialization);
                            ViewBag.IsDoctorScoped = true;
                            ViewBag.ScopedDepartmentName = currentDoctor.Specialization;
                        }
                        else
                        {
                            doctors = doctors.Where(d => d.DoctorId == currentDoctorId.Value);
                        }
                    }
                    else
                    {
                        doctors = doctors.Where(d => false);
                    }
                }
                else
                {
                    doctors = doctors.Where(d => false);
                }
            }
            else
            {
                // Owner, Admin, Receptionist: global view across all departments with department filter tabs
                var departments = await _context.Departments
                    .Where(d => d.ClinicId == currentClinicId)
                    .OrderBy(d => d.DepartmentName)
                    .ToListAsync();

                ViewBag.Departments = departments;
                ViewBag.SelectedDepartmentId = departmentId;

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    doctors = doctors.Where(d => d.DepartmentId == departmentId.Value);
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                doctors = doctors.Where(d =>
                    d.DoctorName.Contains(search) ||
                    d.Specialization.Contains(search) ||
                    d.DoctorNumber.Contains(search) ||
                    (d.Specialty != null && d.Specialty.Name.Contains(search)));
            }

            return View(await doctors.ToListAsync());
        }

        // GET: Doctors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            if (User.IsDoctor()) return Forbid();
            var currentClinicId = User.GetClinicId();

            var doctor = await _context.Doctors
                .Include(d => d.Department)
                .Include(d => d.Specialty)
                .FirstOrDefaultAsync(m => m.DoctorId == id && m.ClinicId == currentClinicId);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // GET: Doctors/Create
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Create()
        {
            var currentClinicId = User.GetClinicId();
            var departments = await _context.Departments.Where(d => d.ClinicId == currentClinicId).OrderBy(d => d.DepartmentName).ToListAsync();
            ViewData["DepartmentId"] = new SelectList(departments, "DepartmentId", "DepartmentName");
            ViewData["SpecialtyId"] = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
            return View();
        }

        // POST: Doctors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("DoctorId,DoctorNumber,DoctorName,Specialization,ConsultationFee,DepartmentId,SpecialtyId")] Doctor doctor)
        {
            // مسح أخطاء العلاقات لتجنب فشل الإضافة
            ModelState.Remove("Department");
            ModelState.Remove("Specialty");
            ModelState.Remove("Appointments");

            var currentClinicId = User.GetClinicId();
            if (ModelState.IsValid)
            {
                doctor.ClinicId = currentClinicId;
                _context.Add(doctor);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Doctor added successfully.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["DepartmentId"] = new SelectList(_context.Departments.Where(d => d.ClinicId == currentClinicId), "DepartmentId", "DepartmentName", doctor.DepartmentId);
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties.Where(s => s.ClinicId == currentClinicId && s.DepartmentId == doctor.DepartmentId), "Id", "Name", doctor.SpecialtyId);
            return View(doctor);
        }

        // GET: Doctors/Edit/5
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var currentClinicId = User.GetClinicId();

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorId == id && d.ClinicId == currentClinicId);
            if (doctor == null) return NotFound();

            ViewData["DepartmentId"] = new SelectList(_context.Departments.Where(d => d.ClinicId == currentClinicId), "DepartmentId", "DepartmentName", doctor.DepartmentId);
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties.Where(s => s.ClinicId == currentClinicId && s.DepartmentId == doctor.DepartmentId), "Id", "Name", doctor.SpecialtyId);
            return View(doctor);
        }

        // POST: Doctors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int id, [Bind("DoctorId,DoctorNumber,DoctorName,Specialization,ConsultationFee,DepartmentId,SpecialtyId")] Doctor doctor)
        {
            if (id != doctor.DoctorId) return NotFound();
            var currentClinicId = User.GetClinicId();

            var dbDoctor = await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorId == id && d.ClinicId == currentClinicId);
            if (dbDoctor == null) return NotFound();

            // 🛠️ مسح الأخطاء لضمان نجاح التعديل (تجاوز مشكلة الـ ModelState)
            ModelState.Clear();

            try
            {
                bool isTransfer = dbDoctor.DepartmentId > 0 && dbDoctor.DepartmentId != doctor.DepartmentId;
                var oldDept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == dbDoctor.DepartmentId && d.ClinicId == currentClinicId);
                var newDept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == doctor.DepartmentId && d.ClinicId == currentClinicId);

                // تحديث القيم يدوياً لضمان عدم حدوث تعارض
                dbDoctor.DoctorNumber = doctor.DoctorNumber;
                dbDoctor.DoctorName = doctor.DoctorName;
                dbDoctor.Specialization = doctor.Specialization;
                dbDoctor.ConsultationFee = doctor.ConsultationFee;
                dbDoctor.DepartmentId = doctor.DepartmentId;
                dbDoctor.SpecialtyId = doctor.SpecialtyId;

                _context.Update(dbDoctor);
                await _context.SaveChangesAsync();

                if (isTransfer && newDept != null)
                {
                    _logger.LogInformation("Doctor {DoctorId} ({DoctorName}) was transferred from department '{OldDept}' to '{NewDept}'",
                        dbDoctor.DoctorId, dbDoctor.DoctorName, oldDept?.DepartmentName ?? "None", newDept.DepartmentName);
                    TempData["Success"] = $"Dr. {dbDoctor.DoctorName} was successfully transferred to {newDept.DepartmentName}.";
                }
                else
                {
                    TempData["Success"] = "Doctor updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DoctorExists(doctor.DoctorId)) return NotFound();
                else throw;
            }
        }

        // GET: Doctors/GetSpecialtiesByDepartment?departmentId=5
        [HttpGet]
        public async Task<IActionResult> GetSpecialtiesByDepartment(int departmentId)
        {
            var currentClinicId = User.GetClinicId();
            var specialties = await _context.Specialties
                .Where(s => s.DepartmentId == departmentId && s.ClinicId == currentClinicId)
                .OrderBy(s => s.Name)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Json(specialties);
        }

        // GET: Doctors/Delete/5
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var currentClinicId = User.GetClinicId();

            var doctor = await _context.Doctors
                .Include(d => d.Department)
                .FirstOrDefaultAsync(m => m.DoctorId == id && m.ClinicId == currentClinicId);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // POST: Doctors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var currentClinicId = User.GetClinicId();
            var doctor = await _context.Doctors
                .Include(d => d.Appointments)
                .FirstOrDefaultAsync(d => d.DoctorId == id && d.ClinicId == currentClinicId);

            if (doctor == null) return NotFound();

            // 🛠️ الحل الجذري للحذف: مسح المواعيد المرتبطة بالطبيب أولاً بشكل إجباري
            if (doctor.Appointments != null && doctor.Appointments.Any())
            {
                _context.Appointments.RemoveRange(doctor.Appointments);
            }

            // ثم مسح الطبيب
            _context.Doctors.Remove(doctor);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Doctor and related appointments deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private bool DoctorExists(int id)
        {
            return _context.Doctors.Any(e => e.DoctorId == id);
        }
    }
}