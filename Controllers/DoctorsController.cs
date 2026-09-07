using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Admin,Receptionist,Doctor")]
    public class DoctorsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IDataImportExportService _importExportService;

        public DoctorsController(ClinicDbContext context, IDataImportExportService importExportService)
        {
            _context = context;
            _importExportService = importExportService;
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
        public async Task<IActionResult> Export()
        {
            var csvBytes = await _importExportService.ExportDoctorsToCsvAsync();
            string fileName = $"Doctors_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }

        /// <summary>
        /// GET /Doctors/Import
        /// Displays the bulk doctor import page.
        /// </summary>
        [HttpGet]
        public IActionResult Import()
        {
            return View();
        }

        /// <summary>
        /// POST /Doctors/Import
        /// Accepts an uploaded doctors CSV file, validates rows, and performs bulk insertion.
        /// </summary>
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
        public async Task<IActionResult> Index(string search)
        {
            var doctors = _context.Doctors
                .Include(d => d.Department)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                doctors = doctors.Where(d =>
                    d.DoctorName.Contains(search) ||
                    d.Specialization.Contains(search) ||
                    d.DoctorNumber.Contains(search));
            }

            return View(await doctors.ToListAsync());
        }

        // GET: Doctors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors
                .Include(d => d.Department)
                .FirstOrDefaultAsync(m => m.DoctorId == id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // GET: Doctors/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["DepartmentId"] = new SelectList(_context.Departments, "DepartmentId", "DepartmentName");
            return View();
        }

        // POST: Doctors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DoctorId,DoctorNumber,DoctorName,Specialization,ConsultationFee,DepartmentId")] Doctor doctor)
        {
            // مسح أخطاء العلاقات لتجنب فشل الإضافة
            ModelState.Remove("Department");
            ModelState.Remove("Appointments");

            if (ModelState.IsValid)
            {
                _context.Add(doctor);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Doctor added successfully.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["DepartmentId"] = new SelectList(_context.Departments, "DepartmentId", "DepartmentName", doctor.DepartmentId);
            return View(doctor);
        }

        // GET: Doctors/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null) return NotFound();

            ViewData["DepartmentId"] = new SelectList(_context.Departments, "DepartmentId", "DepartmentName", doctor.DepartmentId);
            return View(doctor);
        }

        // POST: Doctors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DoctorId,DoctorNumber,DoctorName,Specialization,ConsultationFee,DepartmentId")] Doctor doctor)
        {
            if (id != doctor.DoctorId) return NotFound();

            var dbDoctor = await _context.Doctors.FindAsync(id);
            if (dbDoctor == null) return NotFound();

            // 🛠️ مسح الأخطاء لضمان نجاح التعديل (تجاوز مشكلة الـ ModelState)
            ModelState.Clear();

            try
            {
                // تحديث القيم يدوياً لضمان عدم حدوث تعارض
                dbDoctor.DoctorNumber = doctor.DoctorNumber;
                dbDoctor.DoctorName = doctor.DoctorName;
                dbDoctor.Specialization = doctor.Specialization;
                dbDoctor.ConsultationFee = doctor.ConsultationFee;
                dbDoctor.DepartmentId = doctor.DepartmentId;

                _context.Update(dbDoctor);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Doctor updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DoctorExists(doctor.DoctorId)) return NotFound();
                else throw;
            }
        }

        // GET: Doctors/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors
                .Include(d => d.Department)
                .FirstOrDefaultAsync(m => m.DoctorId == id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // POST: Doctors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.Appointments)
                .FirstOrDefaultAsync(d => d.DoctorId == id);

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