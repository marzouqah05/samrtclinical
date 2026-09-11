using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize]
    public class SpecialtiesController : Controller
    {
        private readonly IDataImportExportService _importExportService;

        public SpecialtiesController(IDataImportExportService importExportService)
        {
            _importExportService = importExportService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Departments");
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csvBytes = _importExportService.GenerateSpecialtiesTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Specialties_Template.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV or Excel file to upload.";
                return RedirectToAction("Index", "Departments");
            }

            if (!ExcelImportHelper.IsSupportedFile(file))
            {
                TempData["Error"] = "Invalid file type. Only CSV (.csv) and Excel (.xlsx, .xls) files are supported for import.";
                return RedirectToAction("Index", "Departments");
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

            return RedirectToAction("Index", "Departments");
        }
    }
}
