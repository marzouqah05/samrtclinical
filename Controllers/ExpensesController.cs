using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ExpensesController(ClinicDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env     = env;
        }

        // ── GET /Expenses ─────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index(
            string?   search      = null,
            string?   category    = null,
            string?   dateRange   = null,
            DateTime? dateFrom    = null,
            DateTime? dateTo      = null,
            int       page        = 1)
        {
            const int pageSize = 15;
            var currentClinicId = User.GetClinicId();

            // ── Base query ───────────────────────────────────────────────────
            var query = _context.Expenses.AsNoTracking().Where(e => e.ClinicId == currentClinicId).AsQueryable();

            // ── Category filter ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(category) &&
                Enum.TryParse<ExpenseCategory>(category, out var catEnum))
            {
                query = query.Where(e => e.Category == catEnum);
            }

            // ── Date range filter ────────────────────────────────────────────
            var today = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(dateRange))
            {
                switch (dateRange)
                {
                    case "today":
                        query = query.Where(e => e.ExpenseDate.Date == today);
                        break;
                    case "this_week":
                        var weekStart = today.AddDays(-(int)today.DayOfWeek);
                        query = query.Where(e => e.ExpenseDate >= weekStart && e.ExpenseDate <= today);
                        break;
                    case "this_month":
                        query = query.Where(e => e.ExpenseDate.Month == today.Month && e.ExpenseDate.Year == today.Year);
                        break;
                    case "this_year":
                        query = query.Where(e => e.ExpenseDate.Year == today.Year);
                        break;
                    case "custom":
                        if (dateFrom.HasValue) query = query.Where(e => e.ExpenseDate >= dateFrom.Value);
                        if (dateTo.HasValue)   query = query.Where(e => e.ExpenseDate <= dateTo.Value.AddDays(1));
                        break;
                }
            }

            // ── Text search ──────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(e =>
                    e.Title.ToLower().Contains(term) ||
                    (e.VendorOrPayee != null && e.VendorOrPayee.ToLower().Contains(term)) ||
                    (e.Notes != null && e.Notes.ToLower().Contains(term)));
            }

            // ── KPI totals (from filtered set before paging) ─────────────────
            var filteredExpenses = await query.ToListAsync();
            var totalExpenses    = filteredExpenses.Sum(e => e.Amount);

            // Total Income = paid invoices (not filtered by date to show overall P&L for visible period)
            var incomeQuery = _context.Invoices.AsNoTracking().Where(i => i.ClinicId == currentClinicId && i.Status == "Paid");
            if (!string.IsNullOrWhiteSpace(dateRange) && dateRange == "this_month")
                incomeQuery = incomeQuery.Where(i => i.InvoiceDate.Month == today.Month && i.InvoiceDate.Year == today.Year);
            else if (!string.IsNullOrWhiteSpace(dateRange) && dateRange == "this_year")
                incomeQuery = incomeQuery.Where(i => i.InvoiceDate.Year == today.Year);

            var totalIncome = await incomeQuery.SumAsync(i => i.NetAmount);
            var netProfit   = totalIncome - totalExpenses;

            // ── Category breakdown for chart ─────────────────────────────────
            var byCategory = filteredExpenses
                .GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key.ToString(), Total = g.Sum(e => e.Amount) })
                .OrderByDescending(g => g.Total)
                .ToList();

            // ── Paging ───────────────────────────────────────────────────────
            var totalCount   = filteredExpenses.Count;
            var totalPages   = (int)Math.Ceiling(totalCount / (double)pageSize);
            var pagedExpenses = filteredExpenses
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.TotalIncome    = totalIncome;
            ViewBag.TotalExpenses  = totalExpenses;
            ViewBag.NetProfit      = netProfit;
            ViewBag.Search         = search;
            ViewBag.Category       = category;
            ViewBag.DateRange      = dateRange;
            ViewBag.DateFrom       = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo         = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage    = page;
            ViewBag.TotalPages     = totalPages;
            ViewBag.TotalCount     = totalCount;
            ViewBag.ByCategoryJson = System.Text.Json.JsonSerializer.Serialize(
                byCategory.Select(b => new { label = b.Category, value = b.Total }));

            return View(pagedExpenses);
        }

        // ── POST /Expenses/Create ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Create(Expense model, IFormFile? receiptFile)
        {
            // Clear nav-property validation errors (none here, but defensive)
            ModelState.Remove("ReceiptAttachmentPath");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction(nameof(Index));
            }

            // ── Receipt file upload ──────────────────────────────────────────
            if (receiptFile != null && receiptFile.Length > 0)
            {
                var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp" };
                var ext = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    TempData["Error"] = "Invalid receipt file type. Allowed: PDF, PNG, JPG, WEBP.";
                    return RedirectToAction(nameof(Index));
                }
                if (receiptFile.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Receipt file too large. Maximum size is 5 MB.";
                    return RedirectToAction(nameof(Index));
                }

                var dir = Path.Combine(_env.WebRootPath, "uploads", "receipts");
                Directory.CreateDirectory(dir);
                var fileName = $"receipt_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
                var path = Path.Combine(dir, fileName);
                using (var fs = new FileStream(path, FileMode.Create))
                    await receiptFile.CopyToAsync(fs);

                model.ReceiptAttachmentPath = $"/uploads/receipts/{fileName}";
            }

            model.ClinicId = User.GetClinicId();
            model.CreatedAt = DateTime.UtcNow;
            _context.Expenses.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Expense \"{model.Title}\" added successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── POST /Expenses/Edit ───────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Expense model, IFormFile? receiptFile)
        {
            ModelState.Remove("ReceiptAttachmentPath");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _context.Expenses.FindAsync(model.Id);
            if (existing == null)
            {
                TempData["Error"] = "Expense record not found.";
                return RedirectToAction(nameof(Index));
            }

            // ── Handle receipt upload ────────────────────────────────────────
            if (receiptFile != null && receiptFile.Length > 0)
            {
                var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp" };
                var ext = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    TempData["Error"] = "Invalid receipt file type.";
                    return RedirectToAction(nameof(Index));
                }
                var dir = Path.Combine(_env.WebRootPath, "uploads", "receipts");
                Directory.CreateDirectory(dir);
                var fileName = $"receipt_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
                using (var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
                    await receiptFile.CopyToAsync(fs);
                existing.ReceiptAttachmentPath = $"/uploads/receipts/{fileName}";
            }
            // If keepReceipt is false and no new file, clear the receipt path
            else if (model.ReceiptAttachmentPath == null)
            {
                existing.ReceiptAttachmentPath = null;
            }

            existing.Title          = model.Title;
            existing.Category       = model.Category;
            existing.Amount         = model.Amount;
            existing.ExpenseDate    = model.ExpenseDate;
            existing.PaymentMethod  = model.PaymentMethod;
            existing.VendorOrPayee  = model.VendorOrPayee;
            existing.Notes          = model.Notes;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Expense \"{existing.Title}\" updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── POST /Expenses/Delete ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null)
            {
                TempData["Error"] = "Expense record not found.";
                return RedirectToAction(nameof(Index));
            }

            // Remove receipt file from disk if it exists
            if (!string.IsNullOrEmpty(expense.ReceiptAttachmentPath))
            {
                var filePath = Path.Combine(_env.WebRootPath, expense.ReceiptAttachmentPath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Expense \"{expense.Title}\" deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── GET /Expenses/ExportExcel ─────────────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> ExportExcel(
            string?   search    = null,
            string?   category  = null,
            string?   dateRange = null,
            DateTime? dateFrom  = null,
            DateTime? dateTo    = null)
        {
            var query = _context.Expenses.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) &&
                Enum.TryParse<ExpenseCategory>(category, out var catEnum))
                query = query.Where(e => e.Category == catEnum);

            var today = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(dateRange))
            {
                switch (dateRange)
                {
                    case "today":
                        query = query.Where(e => e.ExpenseDate.Date == today); break;
                    case "this_week":
                        var ws = today.AddDays(-(int)today.DayOfWeek);
                        query = query.Where(e => e.ExpenseDate >= ws && e.ExpenseDate <= today); break;
                    case "this_month":
                        query = query.Where(e => e.ExpenseDate.Month == today.Month && e.ExpenseDate.Year == today.Year); break;
                    case "this_year":
                        query = query.Where(e => e.ExpenseDate.Year == today.Year); break;
                    case "custom":
                        if (dateFrom.HasValue) query = query.Where(e => e.ExpenseDate >= dateFrom.Value);
                        if (dateTo.HasValue)   query = query.Where(e => e.ExpenseDate <= dateTo.Value.AddDays(1)); break;
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(e => e.Title.ToLower().Contains(term) ||
                    (e.VendorOrPayee != null && e.VendorOrPayee.ToLower().Contains(term)));
            }

            var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();
            var totalIncome = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Status == "Paid")
                .SumAsync(i => i.NetAmount);

            using var wb = new XLWorkbook();

            // ── Sheet 1: Expense Details ─────────────────────────────────────
            var ws1 = wb.Worksheets.Add("Expenses");
            var headers = new[] { "#", "Title", "Category", "Amount", "Date", "Payment Method", "Vendor / Payee", "Notes" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws1.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int r = 0; r < expenses.Count; r++)
            {
                var e  = expenses[r];
                var row = r + 2;
                ws1.Cell(row, 1).Value = r + 1;
                ws1.Cell(row, 2).Value = e.Title;
                ws1.Cell(row, 3).Value = e.Category.ToString();
                ws1.Cell(row, 4).Value = (double)e.Amount;
                ws1.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws1.Cell(row, 5).Value = e.ExpenseDate.ToString("yyyy-MM-dd");
                ws1.Cell(row, 6).Value = e.PaymentMethod;
                ws1.Cell(row, 7).Value = e.VendorOrPayee ?? "";
                ws1.Cell(row, 8).Value = e.Notes ?? "";

                if (r % 2 == 1)
                    ws1.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            }

            ws1.Columns().AdjustToContents();

            // ── Sheet 2: Summary ─────────────────────────────────────────────
            var ws2 = wb.Worksheets.Add("Financial Summary");
            ws2.Cell("A1").Value = "Metric"; ws2.Cell("B1").Value = "Amount";
            ws2.Row(1).Style.Font.Bold = true;
            ws2.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            ws2.Cell("B1").Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            ws2.Row(1).Style.Font.FontColor = XLColor.White;

            var totalExpenses = expenses.Sum(e => e.Amount);
            var summaryRows = new[] {
                ("Total Revenue (Paid Invoices)", (double)totalIncome),
                ("Total Expenses",                (double)totalExpenses),
                ("Net Profit",                   (double)(totalIncome - totalExpenses)),
            };

            for (int i = 0; i < summaryRows.Length; i++)
            {
                ws2.Cell(i + 2, 1).Value = summaryRows[i].Item1;
                ws2.Cell(i + 2, 2).Value = summaryRows[i].Item2;
                ws2.Cell(i + 2, 2).Style.NumberFormat.Format = "#,##0.00";
            }

            // Color-code Net Profit row
            var profitColor = totalIncome - totalExpenses >= 0
                ? XLColor.FromHtml("#dcfce7") : XLColor.FromHtml("#fee2e2");
            ws2.Row(4).Style.Fill.BackgroundColor = profitColor;
            ws2.Columns().AdjustToContents();

            // ── Stream the file ──────────────────────────────────────────────
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var fileName = $"Expenses_{DateTime.Today:yyyy-MM-dd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ── GET /Expenses/GetExpense/{id} (AJAX) ─────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetExpense(int id)
        {
            var e = await _context.Expenses.FindAsync(id);
            if (e == null) return NotFound();

            return Json(new
            {
                e.Id,
                e.Title,
                Category       = (int)e.Category,
                e.Amount,
                ExpenseDate    = e.ExpenseDate.ToString("yyyy-MM-dd"),
                e.PaymentMethod,
                e.VendorOrPayee,
                e.Notes,
                e.ReceiptAttachmentPath
            });
        }
    }
}
