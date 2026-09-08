using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin,Receptionist")]
    public class DashboardController : Controller
    {
        private readonly ClinicDbContext _context;

        public DashboardController(ClinicDbContext context)
        {
            _context = context;
        }

        // ── 1. Dashboard Main Page ─────────────────────────────────────────────
        public async Task<IActionResult> Index(string? search)
        {
            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d!.Department)
                .Include(a => a.Patient)
                .OrderByDescending(a => a.AppointmentDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                appointments = appointments.Where(a =>
                    (a.Doctor != null && a.Doctor.DoctorName.Contains(search)) ||
                    (a.Patient != null && a.Patient.PatientName.Contains(search)) ||
                    (a.Doctor != null && a.Doctor.Department != null && a.Doctor.Department.DepartmentName.Contains(search)));
            }

            // ── Core Statistics (Strictly Sequential Execution) ──────────────
            ViewBag.TotalPatients     = await _context.Patients.CountAsync();
            ViewBag.TotalDoctors      = await _context.Doctors.CountAsync();
            ViewBag.TotalDepartments  = await _context.Departments.CountAsync();

            var today = DateTime.UtcNow.Date;
            ViewBag.TodayAppointments = await _context.Appointments
                .Where(a => a.AppointmentDate.Date == today)
                .CountAsync();

            // ── Financial Analytics (Strictly Sequential Execution) ──────────
            ViewBag.TotalInvoicesCount = await _context.Invoices.CountAsync();

            ViewBag.TotalRevenue = await _context.Invoices
                .Where(i => i.Status == "Paid")
                .SumAsync(i => (decimal?)i.NetAmount) ?? 0.00m;

            ViewBag.PendingRevenue = await _context.Invoices
                .Where(i => i.Status == "Unpaid")
                .SumAsync(i => (decimal?)i.NetAmount) ?? 0.00m;

            ViewBag.TotalExpenses = await _context.Expenses
                .SumAsync(e => (decimal?)e.Amount) ?? 0.00m;

            ViewBag.SearchVal = search;

            var appointmentsList = await appointments.Take(8).ToListAsync();
            return View(appointmentsList);
        }

        // ── 2. Calendar Event API Feed ────────────────────────────────────────
        public async Task<IActionResult> GetAppointments()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Select(a => new
                {
                    id    = a.AppointmentId,
                    title = $"{a.Patient!.PatientName} - Dr. {a.Doctor!.DoctorName}",
                    start = a.AppointmentDate.Add(a.AppointmentTime).ToString("yyyy-MM-ddTHH:mm:ss"),
                    color = "#8b5cf6"
                })
                .ToListAsync();

            return Json(appointments);
        }

        // ── 3. Appointments Chart API ─────────────────────────────────────────
        public async Task<IActionResult> GetAppointmentsChart()
        {
            var list = await _context.Appointments.ToListAsync();

            var data = list
                .GroupBy(a => a.AppointmentDate.ToString("ddd"))
                .Select(g => new { day = g.Key, count = g.Count() })
                .ToList();

            return Json(data);
        }

        // ── 4. Revenue Chart API (Last 7 Days) ────────────────────────────────
        public async Task<IActionResult> GetRevenueChart()
        {
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);

            var invoicesData = await _context.Invoices
                .Where(i => i.InvoiceDate >= sevenDaysAgo && i.Status == "Paid")
                .ToListAsync();

            var todayUtc = DateTime.UtcNow.Date;
            var chartData = Enumerable.Range(0, 8)
                .Select(offset => todayUtc.AddDays(-7 + offset))
                .Select(date => new
                {
                    date    = date.ToString("MM/dd"),
                    revenue = invoicesData
                                .Where(i => i.InvoiceDate.Date == date)
                                .Sum(i => i.NetAmount)
                })
                .ToList();

            return Json(chartData);
        }
    }
}