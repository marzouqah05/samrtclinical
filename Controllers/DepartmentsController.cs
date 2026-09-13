using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist,Doctor")]
    public class DepartmentsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IDataImportExportService _importExportService;

        public DepartmentsController(ClinicDbContext context, IDataImportExportService importExportService)
        {
            _context = context;
            _importExportService = importExportService;
        }

        #region Bulk Import & Template Download

        /// <summary>
        /// GET: /Departments/DownloadTemplate
        /// </summary>
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csvBytes = _importExportService.GenerateDepartmentTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Departments_Template.csv");
        }

        /// <summary>
        /// POST: /Departments/Import
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Import(IFormFile? file)
        {
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
                var result = await _importExportService.ImportDepartmentsFromCsvAsync(stream);

                if (result.ImportedCount > 0 && result.SkippedCount == 0)
                    TempData["Success"] = $"Successfully imported {result.ImportedCount} department(s).";
                else if (result.ImportedCount > 0 && result.SkippedCount > 0)
                    TempData["Warning"] = $"Imported {result.ImportedCount} department(s), but {result.SkippedCount} row(s) were skipped.";
                else if (result.ImportedCount == 0 && result.SkippedCount > 0)
                    TempData["Error"] = $"Import failed: 0 departments imported. {result.SkippedCount} row(s) had errors.";
                else
                    TempData["Warning"] = "The uploaded file contained no valid department rows.";

                if (result.ErrorMessages.Any())
                    TempData["ImportErrors"] = JsonSerializer.Serialize(result.ErrorMessages.Take(25));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred during import: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// GET: /Departments/DownloadSpecialtiesTemplate
        /// </summary>
        [HttpGet]
        public IActionResult DownloadSpecialtiesTemplate()
        {
            var csvBytes = _importExportService.GenerateSpecialtiesTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Specialties_Template.csv");
        }

        /// <summary>
        /// POST: /Departments/ImportSpecialties
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> ImportSpecialties(IFormFile? file)
        {
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
                var result = await _importExportService.ImportSpecialtiesFromCsvAsync(stream);

                if (result.ImportedCount > 0 && result.SkippedCount == 0)
                    TempData["Success"] = $"Successfully imported {result.ImportedCount} specialty item(s).";
                else if (result.ImportedCount > 0 && result.SkippedCount > 0)
                    TempData["Warning"] = $"Imported {result.ImportedCount} specialty item(s), but {result.SkippedCount} row(s) were skipped.";
                else if (result.ImportedCount == 0 && result.SkippedCount > 0)
                    TempData["Error"] = $"Import failed: 0 specialties imported. {result.SkippedCount} row(s) had errors.";
                else
                    TempData["Warning"] = "The uploaded file contained no valid specialty rows.";

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

        // GET: Departments
        public async Task<IActionResult> Index()
        {
            var currentClinicId = User.GetClinicId();
            return View(await _context.Departments.Where(d => d.ClinicId == currentClinicId).ToListAsync());
        }

        // GET: Departments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentClinicId = User.GetClinicId();
            var department = await _context.Departments
                .Include(d => d.Specialties)
                .Include(d => d.Doctors)
                    .ThenInclude(doc => doc.Specialty)
                .FirstOrDefaultAsync(m => m.DepartmentId == id && m.ClinicId == currentClinicId);

            if (department == null)
            {
                return NotFound();
            }

            return View(department);
        }

        // POST: Departments/AddSpecialty
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> AddSpecialty(int departmentId, string name, string? description)
        {
            var currentClinicId = User.GetClinicId();
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == departmentId && d.ClinicId == currentClinicId);
            if (department == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Specialty name is required.";
                return RedirectToAction(nameof(Details), new { id = departmentId });
            }

            var specialty = new Specialty
            {
                ClinicId = currentClinicId,
                DepartmentId = departmentId,
                Name = name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            };

            _context.Specialties.Add(specialty);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Sub-specialty '{specialty.Name}' added successfully.";
            return RedirectToAction(nameof(Details), new { id = departmentId });
        }

        // GET: Departments/GetSpecialtiesByDepartment?departmentId=5
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

        // GET: Departments/Create
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Departments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("DepartmentId,DepartmentName,DepartmentAbbr")] Department department)
        {
            if (ModelState.IsValid)
            {
                department.ClinicId = User.GetClinicId();
                _context.Add(department);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(department);
        }

        // GET: Departments/Edit/5
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentClinicId = User.GetClinicId();
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == id && d.ClinicId == currentClinicId);
            if (department == null)
            {
                return NotFound();
            }

            return View(department);
        }

        // POST: Departments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int id, [Bind("DepartmentId,DepartmentName,DepartmentAbbr")] Department department)
        {
            if (id != department.DepartmentId)
            {
                return NotFound();
            }

            var currentClinicId = User.GetClinicId();
            var existing = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == id && d.ClinicId == currentClinicId);
            if (existing == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existing.DepartmentName = department.DepartmentName;
                    existing.DepartmentAbbr = department.DepartmentAbbr;
                    _context.Update(existing);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DepartmentExists(department.DepartmentId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(department);
        }

        // GET: Departments/Delete/5
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentClinicId = User.GetClinicId();
            var department = await _context.Departments
                .Include(d => d.Doctors)
                .FirstOrDefaultAsync(m => m.DepartmentId == id && m.ClinicId == currentClinicId);

            if (department == null)
            {
                return NotFound();
            }

            return View(department);
        }

        // POST: Departments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var currentClinicId = User.GetClinicId();
            var department = await _context.Departments
                .Include(d => d.Doctors)
                .FirstOrDefaultAsync(d => d.DepartmentId == id && d.ClinicId == currentClinicId);

            if (department == null)
            {
                TempData["Error"] = "Department not found.";
                return RedirectToAction(nameof(Index));
            }

            if (department.Doctors != null && department.Doctors.Any())
            {
                TempData["Error"] = $"Cannot delete department '{department.DepartmentName}' because it has {department.Doctors.Count} assigned doctor(s). Please reassign or remove them first.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var deptName = department.DepartmentName;
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Department '{deptName}' was deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while deleting the department: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool DepartmentExists(int id)
        {
            return _context.Departments.Any(e => e.DepartmentId == id);
        }
    }
}