using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [Authorize]
    public class TreatmentsController : Controller
    {
        private readonly ClinicDbContext _context;

        public TreatmentsController(ClinicDbContext context)
        {
            _context = context;
        }

        // GET: Treatments
        public async Task<IActionResult> Index(string searchPatient, DateTime? filterDate)
        {
            // جلب المعالجات مع تضمين الموعد والمريض المرتبط به لمنع الـ Null Reference
            var query = _context.Treatments
                .Include(t => t.Appointment)
                    .ThenInclude(a => a != null ? a.Patient : null)
                .Include(t => t.Appointment)
                    .ThenInclude(a => a != null ? a.Doctor : null)
                .AsQueryable();

            // 1. البحث باسم المريض
            if (!string.IsNullOrEmpty(searchPatient))
            {
                query = query.Where(t => t.Appointment != null && t.Appointment.Patient != null &&
                    t.Appointment.Patient.PatientName.Contains(searchPatient));
            }

            // 2. الفلترة بتاريخ المعالجة
            if (filterDate.HasValue)
            {
                query = query.Where(t => t.TreatmentDate.Date == filterDate.Value.Date);
            }

            // ترتيب المعالجات من الأحدث إلى الأقدم
            var treatments = await query
                .OrderByDescending(t => t.TreatmentDate)
                .ToListAsync();

            ViewBag.CurrentSearch = searchPatient;
            ViewBag.CurrentDate = filterDate?.ToString("yyyy-MM-dd");

            return View(treatments);
        }

        // GET: Treatments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var treatment = await _context.Treatments
                .Include(t => t.Appointment)
                    .ThenInclude(a => a != null ? a.Patient : null)
                .Include(t => t.Appointment)
                    .ThenInclude(a => a != null ? a.Doctor : null)
                .FirstOrDefaultAsync(m => m.TreatmentId == id);

            if (treatment == null)
                return NotFound();

            return View(treatment);
        }

        // GET: Treatments/Create
        public IActionResult Create(int? appointmentId)
        {
            // جلب المواعيد التي لم تكتمل بعد، أو تضمين الموعد المحدد إذا تم تمريره
            var appointmentsQuery = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsQueryable();

            if (appointmentId.HasValue)
            {
                // تمرير رقم الموعد المختار للـ View لتحديده تلقائياً
                ViewBag.SelectedAppointmentId = appointmentId.Value;
            }
            else
            {
                // إذا فتح الشاشة بشكل عام، يعرض المواعيد غير المكتملة فقط لتسهيل الاختيار
                appointmentsQuery = appointmentsQuery.Where(a => a.Status != "Completed");
            }

            ViewBag.Appointments = appointmentsQuery.ToList();
            return View();
        }

        // POST: Treatments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Treatment treatment)
        {
            // الحل السحري: إزالة التحقق من كائن الـ Appointment لأننا نرسل فقط الـ ID من الشاشة
            ModelState.Remove("Appointment");

            if (ModelState.IsValid)
            {
                _context.Add(treatment);

                // تحديث حالة الموعد المرتبط إلى Completed
                var appointment = await _context.Appointments.FindAsync(treatment.AppointmentId);
                if (appointment != null)
                {
                    appointment.Status = "Completed";
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Treatment logged successfully!"; // رسالة نجاح للواجهة
                return RedirectToAction(nameof(Index));
            }

            // في حال وجود خطأ، نعيد تحميل المواعيد غير المكتملة فقط حتى لا تنهار الواجهة
            var appointments = (_context.Appointments?
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a => a.Status != "Completed")
                .ToList()) ?? new List<Appointment>();

            ViewBag.Appointments = appointments;

            return View(treatment);
        }

        // GET: Treatments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var treatment = await _context.Treatments.FindAsync(id);
            if (treatment == null)
                return NotFound();

            var appointments = (_context.Appointments?
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .ToList()) ?? new List<Appointment>();

            ViewBag.Appointments = appointments;

            return View(treatment);
        }

        // POST: Treatments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Treatment treatment)
        {
            if (id != treatment.TreatmentId)
                return NotFound();

            // إزالة التحقق لتجنب مشكلة الـ Validation عند التعديل أيضاً
            ModelState.Remove("Appointment");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(treatment);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Treatment updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TreatmentExists(treatment.TreatmentId))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            var appointments = (_context.Appointments?
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .ToList()) ?? new List<Appointment>();

            ViewBag.Appointments = appointments;

            return View(treatment);
        }

        // GET: Treatments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var treatment = await _context.Treatments
                .Include(t => t.Appointment)
                    .ThenInclude(a => a != null ? a.Patient : null)
                .Include(t => t.Appointment)
                    .ThenInclude(a => a != null ? a.Doctor : null)
                .FirstOrDefaultAsync(m => m.TreatmentId == id);

            if (treatment == null)
                return NotFound();

            return View(treatment);
        }

        // POST: Treatments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var treatment = await _context.Treatments.FindAsync(id);
            if (treatment != null)
            {
                _context.Treatments.Remove(treatment);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Treatment deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool TreatmentExists(int id)
        {
            return (_context.Treatments?.Any(e => e.TreatmentId == id)).GetValueOrDefault();
        }
    }
}