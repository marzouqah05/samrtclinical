using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin,Doctor,Receptionist")]
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
            var isDoctor = User.IsDoctor();
            int? currentDoctorId = User.GetDoctorId();
            Doctor? doctorProfile = null;

            if (isDoctor)
            {
                if (!currentDoctorId.HasValue)
                {
                    var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
                    var doc = await _context.Doctors.Include(d => d.Department).FirstOrDefaultAsync(d => 
                        (currentClinicId == Guid.Empty || d.ClinicId == currentClinicId) && 
                        (d.DoctorEmail == userEmail || d.DoctorName == User.Identity!.Name));
                    if (doc != null)
                    {
                        currentDoctorId = doc.DoctorId;
                        doctorProfile = doc;
                    }
                }
                else
                {
                    doctorProfile = await _context.Doctors.Include(d => d.Department).FirstOrDefaultAsync(d => d.DoctorId == currentDoctorId.Value);
                }
            }

            ViewBag.IsDoctor = isDoctor;
            ViewBag.DoctorProfile = doctorProfile;

            // ── Active Appointments Query ──────────────────────────────────
            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d!.Department)
                .Include(a => a.Patient)
                .Where(a => a.ClinicId == currentClinicId && a.Status != "Cancelled");

            if (isDoctor && currentDoctorId.HasValue)
            {
                // Doctor dashboard strictly filtered: Appointments.Where(a => a.DoctorId == currentDoctorId && a.Date == Today)
                appointments = appointments.Where(a => a.DoctorId == currentDoctorId.Value && a.AppointmentDate.Date == today);
            }
            else
            {
                appointments = appointments.Where(a => a.Status != "Completed" && a.AppointmentDate >= today);
            }

            appointments = appointments
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

            if (isDoctor && currentDoctorId.HasValue)
            {
                // ── Doctor-Scoped Clinical KPIs (No financial metrics exposed) ──
                ViewBag.DoctorTodayAppointments = await _context.Appointments
                    .Where(a => a.ClinicId == currentClinicId && a.DoctorId == currentDoctorId.Value && a.AppointmentDate.Date == today && a.Status != "Cancelled")
                    .CountAsync();

                ViewBag.DoctorPatientsCount = await _context.Appointments
                    .Where(a => a.ClinicId == currentClinicId && a.DoctorId == currentDoctorId.Value)
                    .Select(a => a.PatientId)
                    .Distinct()
                    .CountAsync();

                ViewBag.DoctorCompletedCount = await _context.Appointments
                    .Where(a => a.ClinicId == currentClinicId && a.DoctorId == currentDoctorId.Value && a.Status == "Completed")
                    .CountAsync();

                ViewBag.DoctorSpecialization = doctorProfile?.Specialization ?? "General Practice";
                ViewBag.DoctorDepartment = doctorProfile?.Department?.DepartmentName ?? "Clinic";

                ViewBag.TotalPatients     = ViewBag.DoctorPatientsCount;
                ViewBag.TotalDoctors      = 1;
                ViewBag.TotalDepartments  = 1;
                ViewBag.TodayAppointments = ViewBag.DoctorTodayAppointments;

                ViewBag.TotalInvoicesCount = 0;
                ViewBag.TotalRevenue       = 0.00m;
                ViewBag.PendingRevenue     = 0.00m;
                ViewBag.TotalExpenses      = 0.00m;
            }
            else
            {
                // ── Admin / Receptionist Core Statistics ─────────────────────────
                ViewBag.TotalPatients     = await _context.Patients.Where(p => p.ClinicId == currentClinicId).CountAsync();
                ViewBag.TotalDoctors      = await _context.Doctors.Where(d => d.ClinicId == currentClinicId).CountAsync();
                ViewBag.TotalDepartments  = await _context.Departments.Where(d => d.ClinicId == currentClinicId).CountAsync();

                ViewBag.TodayAppointments = await _context.Appointments
                    .Where(a => a.ClinicId == currentClinicId && a.AppointmentDate.Date == today && a.Status != "Cancelled")
                    .CountAsync();

                // Financial Analytics (Restricted to Owner & Admin; hidden from Doctors & Receptionists)
                ViewBag.TotalInvoicesCount = await _context.Invoices.Where(i => i.ClinicId == currentClinicId).CountAsync();

                if (User.IsAdminOrOwner())
                {
                    ViewBag.TotalRevenue = await _context.Invoices
                        .Where(i => i.ClinicId == currentClinicId && i.Status == "Paid")
                        .SumAsync(i => (decimal?)i.NetAmount) ?? 0.00m;

                    ViewBag.PendingRevenue = await _context.Invoices
                        .Where(i => i.ClinicId == currentClinicId && i.Status == "Unpaid")
                        .SumAsync(i => (decimal?)i.NetAmount) ?? 0.00m;

                    ViewBag.TotalExpenses = await _context.Expenses
                        .Where(e => e.ClinicId == currentClinicId)
                        .SumAsync(e => (decimal?)e.Amount) ?? 0.00m;
                }
                else
                {
                    ViewBag.TotalRevenue   = 0.00m;
                    ViewBag.PendingRevenue = 0.00m;
                    ViewBag.TotalExpenses  = 0.00m;
                }
            }

            ViewBag.SearchVal = search;

            var appointmentsList = await appointments.Take(8).ToListAsync();
            return View(appointmentsList);
        }

        // ── 2. Calendar Event API Feed ────────────────────────────────────────
        public async Task<IActionResult> GetAppointments()
        {
            var currentClinicId = User.GetClinicId();
            var isDoctor = User.IsDoctor();
            var currentDoctorId = User.GetDoctorId();

            var query = _context.Appointments.Where(a => a.ClinicId == currentClinicId);

            if (isDoctor && currentDoctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == currentDoctorId.Value);
            }

            var appointments = await query
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
            var isDoctor = User.IsDoctor();
            var currentDoctorId = User.GetDoctorId();

            var query = _context.Appointments.Where(a => a.ClinicId == currentClinicId);

            if (isDoctor && currentDoctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == currentDoctorId.Value);
            }

            var list = await query.ToListAsync();

            var data = list
                .GroupBy(a => a.AppointmentDate.ToString("ddd"))
                .Select(g => new { day = g.Key, count = g.Count() })
                .ToList();

            return Json(data);
        }

        // ── 4. Revenue Chart API (Last 7 Days) ────────────────────────────────
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> GetRevenueChart()
        {
            if (!User.IsAdminOrOwner()) return Forbid();

            var currentClinicId = User.GetClinicId();
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);

            var list = await _context.Invoices
                .Where(i => i.ClinicId == currentClinicId && i.InvoiceDate >= sevenDaysAgo && i.Status == "Paid")
                .ToListAsync();

            var todayUtc = DateTime.UtcNow.Date;
            var chartData = Enumerable.Range(0, 8)
                .Select(offset => todayUtc.AddDays(-7 + offset))
                .Select(date => new
                {
                    date    = date.ToString("MM/dd"),
                    revenue = list
                                .Where(i => i.InvoiceDate.Date == date)
                                .Sum(i => i.NetAmount)
                })
                .ToList();

            return Json(chartData);
        }
    }
}