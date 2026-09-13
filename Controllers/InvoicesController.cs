using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using WebApplication1.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebApplication1.Controllers
{
    // Secure the controller: Only Owner, Admin and Receptionist can access financial billing data (Doctors strictly forbidden)
    [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist")]
    public class InvoicesController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IDataImportExportService _importExportService;
        private readonly ISettingsService _settingsService;

        public InvoicesController(ClinicDbContext context, IDataImportExportService importExportService, ISettingsService settingsService)
        {
            _context = context;
            _importExportService = importExportService;
            _settingsService = settingsService;
        }

        #region Bulk Import & Export

        /// <summary>
        /// GET /Invoices/DownloadTemplate
        /// Generates and returns a downloadable blank sample CSV template for invoice bulk import.
        /// </summary>
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csvBytes = _importExportService.GenerateInvoiceTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Invoices_Template.csv");
        }

        /// <summary>
        /// GET /Invoices/Export
        /// Exports all invoices to a downloadable CSV file.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var csvBytes = await _importExportService.ExportInvoicesToCsvAsync();
            string fileName = $"Invoices_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }

        /// <summary>
        /// GET /Invoices/Import
        /// Displays the bulk import view for invoices.
        /// </summary>
        [HttpGet]
        public IActionResult Import() => View();

        /// <summary>
        /// POST /Invoices/Import
        /// Accepts an uploaded invoices CSV file, validates rows, and performs bulk insertion.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV or Excel file to upload.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".csv" && extension != ".xlsx" && extension != ".xls")
            {
                TempData["Error"] = "Invalid file type. Only CSV (.csv) and Excel (.xlsx, .xls) files are supported.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = ExcelImportHelper.GetStreamAsCsv(file);
                var result = await _importExportService.ImportInvoicesFromCsvAsync(stream);

                if (result.ImportedCount > 0 && result.SkippedCount == 0)
                {
                    TempData["Success"] = $"Successfully imported {result.ImportedCount} invoice(s).";
                }
                else if (result.ImportedCount > 0 && result.SkippedCount > 0)
                {
                    TempData["Warning"] = $"Imported {result.ImportedCount} invoice(s) successfully, but {result.SkippedCount} row(s) were skipped due to validation errors.";
                }
                else if (result.ImportedCount == 0 && result.SkippedCount > 0)
                {
                    TempData["Error"] = $"Import failed: 0 invoices imported. {result.SkippedCount} row(s) had validation errors.";
                }
                else
                {
                    TempData["Warning"] = "The uploaded file contained no valid invoice rows.";
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

        // 1. GET: Invoices
        public async Task<IActionResult> Index()
        {
            var currentClinicId = User.GetClinicId();
            var invoices = _context.Invoices
                .Where(i => i.ClinicId == currentClinicId)
                .Include(i => i.Patient)
                .Include(i => i.Treatment)
                    .ThenInclude(t => t!.Appointment)
                        .ThenInclude(a => a!.Doctor);

            var treatmentsList = await _context.Treatments
                .Include(t => t.Appointment)
                    .ThenInclude(a => a!.Patient)
                .Where(t => t.Appointment != null && t.Appointment.ClinicId == currentClinicId)
                .Select(t => new {
                    TreatmentId = t.TreatmentId,
                    DisplayText = $"Patient: {t.Appointment.Patient.PatientName} | Date: {t.Appointment.AppointmentDate.ToString("yyyy/MM/dd")} | Treatment: {t.TreatmentDesc}"
                }).ToListAsync();

            ViewData["TreatmentId"] = new SelectList(treatmentsList, "TreatmentId", "DisplayText");

            var settings = await _settingsService.GetSettingsAsync();
            ViewBag.CurrencySymbol = settings.CurrencySymbol;
            ViewBag.TaxPercentage = settings.TaxPercentage;

            return View(await invoices.ToListAsync());
        }

        // 2. GET: Invoices/Print/5
        public async Task<IActionResult> Print(int id)
        {
            var currentClinicId = User.GetClinicId();
            var invoice = await _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Treatment)
                    .ThenInclude(t => t!.Appointment)
                        .ThenInclude(a => a!.Doctor)
                .FirstOrDefaultAsync(i => i.InvoiceId == id && i.ClinicId == currentClinicId);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        // 3. GET: Invoices/Create
        public IActionResult Create()
        {
            var currentClinicId = User.GetClinicId();
            // Fetch treatments and establish a complete relational chain (Treatment -> Appointment -> Patient)
            var treatmentsList = _context.Treatments
                .Include(t => t.Appointment)
                    .ThenInclude(a => a.Patient)
                .Where(t => t.Appointment != null && t.Appointment.ClinicId == currentClinicId)
                .Select(t => new {
                    TreatmentId = t.TreatmentId,
                    // English display formatting connecting Patient name, visit date, and treatment description
                    DisplayText = $"Patient: {t.Appointment.Patient.PatientName} | Date: {t.Appointment.AppointmentDate.ToString("yyyy/MM/dd")} | Treatment: {t.TreatmentDesc}"
                }).ToList();

            ViewData["TreatmentId"] = new SelectList(treatmentsList, "TreatmentId", "DisplayText");
            return View();
        }

        // 4. POST: Invoices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TreatmentId,Discount,Tax,Status")] Invoice invoice)
        {
            var currentClinicId = User.GetClinicId();
            // Pull full relational chain to ensure dynamic cross-referencing and background price calculation
            var treatment = await _context.Treatments
                .Include(t => t.Appointment)
                    .ThenInclude(a => a!.Patient)
                .Include(t => t.Appointment)
                    .ThenInclude(a => a!.Doctor)
                .FirstOrDefaultAsync(t => t.TreatmentId == invoice.TreatmentId && t.Appointment != null && t.Appointment.ClinicId == currentClinicId);

            if (treatment != null && treatment.Appointment != null)
            {
                // Dynamic Linkage: Automatically assign PatientId and ClinicId from the associated appointment / tenant
                invoice.ClinicId = currentClinicId;
                invoice.PatientId = treatment.Appointment.PatientId;
                invoice.InvoiceDate = DateTime.Now;

                // Automatic Calculations: Combine Doctor's Consultation Fee + Specific Treatment Cost
                decimal doctorFee = treatment.Appointment.Doctor?.ConsultationFee ?? 0.00m;
                decimal treatmentCost = treatment.TreatmentCost;

                invoice.Amount = doctorFee + treatmentCost;

                var settings = await _settingsService.GetSettingsAsync();
                if (invoice.Tax == 0 && settings.TaxPercentage > 0)
                {
                    invoice.Tax = Math.Round(invoice.Amount * (settings.TaxPercentage / 100m), 2);
                }

                // Calculate final Net Amount (Gross Amount + Tax - Discount)
                invoice.NetAmount = invoice.Amount + invoice.Tax - invoice.Discount;

                // Auto-generate a unique invoice reference number
                invoice.InvoiceNumber = $"INV-{DateTime.Now.Year}-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

                _context.Add(invoice);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Invoice created and issued successfully.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Please select a valid medical visit / patient.";
            return RedirectToAction(nameof(Index));
        }

        // 5. GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var currentClinicId = User.GetClinicId();

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && i.ClinicId == currentClinicId);
            if (invoice == null) return NotFound();

            return View(invoice);
        }

        // POST: Invoices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("InvoiceId,InvoiceNumber,InvoiceDate,Amount,Discount,Tax,NetAmount,Status,PatientId,TreatmentId")] Invoice invoice)
        {
            if (id != invoice.InvoiceId) return NotFound();
            var currentClinicId = User.GetClinicId();

            var existing = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && i.ClinicId == currentClinicId);
            if (existing == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    existing.Discount = invoice.Discount;
                    existing.Tax = invoice.Tax;
                    existing.Status = invoice.Status;
                    existing.NetAmount = existing.Amount + existing.Tax - existing.Discount;

                    _context.Update(existing);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(invoice.InvoiceId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(invoice);
        }

        // 6. GET: Invoices/Delete/5
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var currentClinicId = User.GetClinicId();

            var invoice = await _context.Invoices
                .Include(i => i.Patient)
                .FirstOrDefaultAsync(m => m.InvoiceId == id && m.ClinicId == currentClinicId);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        // POST: Invoices/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var currentClinicId = User.GetClinicId();
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && i.ClinicId == currentClinicId);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id) => _context.Invoices.Any(e => e.InvoiceId == id);
    }
}