using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using WebApplication1.Services;

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
            var today = DateTime.UtcNow.Date;
            var currentClinicId = User.GetClinicId();

            // ── Upcoming / Active Appointments for Dashboard ─────────────────
            // Explicitly exclude Cancelled and Completed appointments and only include upcoming / active appointments
            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d!.Department)
                .Include(a => a.Patient)
                .Where(a => a.ClinicId == currentClinicId && a.Status != "Cancelled" && a.Status != "Completed" && a.AppointmentDate >= today)
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                appointments = appointments.Where(a =>
                    (a.Doctor != null && a.Doctor.DoctorName.Contains(search)) ||
                    (a.Patient != null && a.Patient.PatientName.Contains(search)) ||
                    (a.Doctor != null && a.Doctor.Department != null && a.Doctor.Department.DepartmentName.Contains(search)));
            }

            // ── Core Statistics (Strictly Sequential Execution) ──────────────
            ViewBag.TotalPatients     = await _context.Patients.Where(p => p.ClinicId == currentClinicId).CountAsync();
            ViewBag.TotalDoctors      = await _context.Doctors.Where(d => d.ClinicId == currentClinicId).CountAsync();
            ViewBag.TotalDepartments  = await _context.Departments.Where(d => d.ClinicId == currentClinicId).CountAsync();

            ViewBag.TodayAppointments = await _context.Appointments
                .Where(a => a.ClinicId == currentClinicId && a.AppointmentDate.Date == today && a.Status != "Cancelled")
                .CountAsync();

            // ── Financial Analytics (Strictly Sequential Execution) ──────────
            ViewBag.TotalInvoicesCount = await _context.Invoices.Where(i => i.ClinicId == currentClinicId).CountAsync();

            ViewBag.TotalRevenue = await _context.Invoices
                .Where(i => i.ClinicId == currentClinicId && i.Status == "Paid")
                .SumAsync(i => (decimal?)i.NetAmount) ?? 0.00m;

            ViewBag.PendingRevenue = await _context.Invoices
                .Where(i => i.ClinicId == currentClinicId && i.Status == "Unpaid")
                .SumAsync(i => (decimal?)i.NetAmount) ?? 0.00m;

            ViewBag.TotalExpenses = await _context.Expenses
                .Where(e => e.ClinicId == currentClinicId)
                .SumAsync(e => (decimal?)e.Amount) ?? 0.00m;

            ViewBag.SearchVal = search;

            var appointmentsList = await appointments.Take(8).ToListAsync();
            return View(appointmentsList);
        }

        // ── 2. Calendar Event API Feed ────────────────────────────────────────
        public async Task<IActionResult> GetAppointments()
        {
            var currentClinicId = User.GetClinicId();
            var appointments = await _context.Appointments
                .Where(a => a.ClinicId == currentClinicId)
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
            var currentClinicId = User.GetClinicId();
            var list = await _context.Appointments
                .Where(a => a.ClinicId == currentClinicId)
                .ToListAsync();

            var data = list
                .GroupBy(a => a.AppointmentDate.ToString("ddd"))
                .Select(g => new { day = g.Key, count = g.Count() })
                .ToList();

            return Json(data);
        }

        // ── 4. Revenue Chart API (Last 7 Days) ────────────────────────────────
        public async Task<IActionResult> GetRevenueChart()
        {
            var currentClinicId = User.GetClinicId();
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);

            var invoicesData = await _context.Invoices
                .Where(i => i.ClinicId == currentClinicId && i.InvoiceDate >= sevenDaysAgo && i.Status == "Paid")
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