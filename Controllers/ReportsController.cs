using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin")]
    public class ReportsController : Controller
    {
        private readonly ClinicDbContext _context;

        public ReportsController(ClinicDbContext context)
        {
            _context = context;
        }

        // GET: Reports
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Today;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var endOfLastMonth = startOfMonth.AddDays(-1);

            // 1. حساب أعداد المواعيد وحالاتها
            var totalAppts = await _context.Appointments.CountAsync();
            var completedAppts = await _context.Appointments.CountAsync(a => a.Status == "Completed");
            var pendingAppts = await _context.Appointments.CountAsync(a => a.Status == "Pending");
            var cancelledAppts = await _context.Appointments.CountAsync(a => a.Status == "Cancelled");

            ViewBag.TotalAppointments = totalAppts;
            ViewBag.CompletedAppointments = completedAppts;
            ViewBag.PendingAppointments = pendingAppts;
            ViewBag.CancelledAppointments = cancelledAppts;

            // 2. إجمالي المرضى المسجلين
            var totalPatients = await _context.Patients.CountAsync();
            ViewBag.TotalPatients = totalPatients;

            // 3. جلب الأطباء والمواعيد والعلاجات للتحليل الشامل
            var doctors = await _context.Doctors
                .Include(d => d.Department)
                .ToListAsync();

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                .ToListAsync();

            var treatments = await _context.Treatments
                .Include(t => t.Appointment)
                    .ThenInclude(a => a != null ? a.Doctor : null)
                        .ThenInclude(d => d != null ? d.Department : null)
                .ToListAsync();

            decimal totalConsultations = appointments
                .Where(a => a.Status == "Completed" && a.Doctor != null)
                .Sum(a => a.Doctor!.ConsultationFee);

            decimal totalTreatments = treatments.Sum(t => t.TreatmentCost);
            decimal netRevenue = totalConsultations + totalTreatments;

            ViewBag.ConsultationEarnings = totalConsultations;
            ViewBag.TreatmentEarnings = totalTreatments;
            ViewBag.TotalRevenue = netRevenue;
            ViewBag.CompletedTreatmentsCount = treatments.Count;
            ViewBag.NetOperationalYield = totalAppts > 0 ? (int)Math.Round((double)completedAppts / totalAppts * 100) : 100;
            ViewBag.GeneratedDate = DateTime.Now.ToString("dd MMM yyyy, hh:mm tt");
            ViewBag.CurrentAdmin = User.Identity?.Name ?? "Administrator";

            // 4. تقرير إنتاجية وإيرادات الأطباء
            var doctorPerformance = new List<DoctorPerformanceReportDto>();
            foreach (var doc in doctors)
            {
                var docAppts = appointments.Where(a => a.DoctorId == doc.DoctorId && a.Status == "Completed").ToList();
                var docTreatments = treatments.Where(t => t.Appointment?.DoctorId == doc.DoctorId).ToList();

                var consultEarnings = docAppts.Count * doc.ConsultationFee;
                var treatEarnings = docTreatments.Sum(t => t.TreatmentCost);
                var totalDocRev = consultEarnings + treatEarnings;

                doctorPerformance.Add(new DoctorPerformanceReportDto
                {
                    DoctorName = doc.DoctorName,
                    DepartmentName = doc.Department?.DepartmentName ?? "General Practice",
                    AppointmentsCount = docAppts.Count,
                    ConsultationEarnings = consultEarnings,
                    TreatmentEarnings = treatEarnings,
                    Revenue = totalDocRev,
                    ContributionPercentage = netRevenue > 0 ? Math.Round((totalDocRev / netRevenue) * 100, 1) : 0
                });
            }
            doctorPerformance = doctorPerformance.OrderByDescending(d => d.Revenue).ToList();
            ViewBag.DoctorPerformance = doctorPerformance;

            // 5. تقرير الإجراءات الطبية والعلاجات الأكثر طلباً
            var topProcedures = treatments
                .GroupBy(t => string.IsNullOrWhiteSpace(t.TreatmentDesc) ? "General Consultation & Care" : t.TreatmentDesc)
                .Select(g =>
                {
                    var sampleAppt = g.FirstOrDefault()?.Appointment;
                    var dept = sampleAppt?.Doctor?.Department?.DepartmentName ?? "Clinical Services";
                    var count = g.Count();
                    var totalRev = g.Sum(x => x.TreatmentCost);
                    var unitPrice = count > 0 ? totalRev / count : 0m;

                    return new ProcedureReportDto
                    {
                        ProcedureName = g.Key,
                        DepartmentName = dept,
                        Frequency = count,
                        UnitPrice = Math.Round(unitPrice, 2),
                        TotalRevenue = totalRev
                    };
                })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(10)
                .ToList();

            ViewBag.TopProcedures = topProcedures;

            // 6. جلب أحدث العلاجات المسجلة في العيادة
            var recentTreatments = treatments
                .OrderByDescending(t => t.TreatmentDate)
                .Take(6)
                .ToList();

            return View(recentTreatments);
        }
    }

    // كلاس نقل بيانات (DTO) لتنظيم البيانات المسترجعة للأطباء
    public class DoctorPerformanceReportDto
    {
        public string DoctorName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int AppointmentsCount { get; set; }
        public decimal ConsultationEarnings { get; set; }
        public decimal TreatmentEarnings { get; set; }
        public decimal Revenue { get; set; }
        public decimal ContributionPercentage { get; set; }
    }

    // كلاس نقل بيانات (DTO) لتحليل الإجراءات السريرية
    public class ProcedureReportDto
    {
        public string ProcedureName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}