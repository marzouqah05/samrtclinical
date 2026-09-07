using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin,Receptionist")]
    public class MigrationController : Controller
    {
        private readonly ISystemMigrationService _migrationService;

        public MigrationController(ISystemMigrationService migrationService)
        {
            _migrationService = migrationService;
        }

        // GET: /Migration
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// GET: /Migration/DownloadTemplate
        /// Returns a downloadable multi-sheet Excel (.xlsx) workbook preformatted for full data migration.
        /// </summary>
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var excelBytes = _migrationService.GenerateFullMigrationExcelTemplate();
            string fileName = "Clinic_Full_System_Migration_Template.xlsx";
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// GET: /Migration/ExportBackup
        /// Exports all database tables across all entities into a single multi-sheet Excel (.xlsx) backup file.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportBackup()
        {
            var excelBytes = await _migrationService.ExportFullSystemBackupExcelAsync();
            string fileName = $"Clinic_Full_System_Backup_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// POST: /Migration/UploadMigration
        /// Processes uploaded multi-sheet Excel file and executes system-wide migration.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadMigration(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid Excel (.xlsx) migration file to upload.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx")
            {
                TempData["Error"] = "Invalid file type. Only Excel (.xlsx) workbooks are supported for multi-entity migration.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _migrationService.MigrateFromExcelAsync(stream);

                TempData["MigrationResultJson"] = JsonSerializer.Serialize(result);

                if (result.TotalImported > 0)
                {
                    TempData["Success"] = $"Migration completed! Successfully imported {result.TotalImported} entities into the clinic database.";
                }
                else
                {
                    TempData["Warning"] = "The uploaded workbook did not result in any imported entities. Please check the sheet headers and formatting.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Migration error: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
