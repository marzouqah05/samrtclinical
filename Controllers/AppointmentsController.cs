using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin,Receptionist,Doctor")]
    public class AppointmentsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IWhatsAppService _whatsApp;
        private readonly IDataImportExportService _importExportService;
        private readonly ILogger<AppointmentsController> _logger;

        public AppointmentsController(ClinicDbContext context, IWhatsAppService whatsApp, IDataImportExportService importExportService, ILogger<AppointmentsController> logger)
        {
            _context  = context;
            _whatsApp = whatsApp;
            _importExportService = importExportService;
            _logger = logger;
        }

        #region Bulk Import & Export

        /// <summary>
        /// GET /Appointments/DownloadTemplate
        /// Generates and returns a downloadable blank sample CSV template for appointment bulk import.
        /// </summary>
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csvBytes = _importExportService.GenerateAppointmentTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Appointments_Template.csv");
        }

        /// <summary>
        /// GET /Appointments/Export
        /// Exports all appointments to a downloadable CSV file.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var csvBytes = await _importExportService.ExportAppointmentsToCsvAsync();
            string fileName = $"Appointments_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }

        /// <summary>
        /// POST /Appointments/Import
        /// Accepts an uploaded appointments CSV file, validates rows, and performs bulk insertion.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV file to upload.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".csv")
            {
                TempData["Error"] = "Invalid file type. Only CSV (.csv) files are supported for import.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _importExportService.ImportAppointmentsFromCsvAsync(stream);

                if (result.ImportedCount > 0 && result.SkippedCount == 0)
                {
                    TempData["Success"] = $"Successfully imported {result.ImportedCount} appointment(s).";
                }
                else if (result.ImportedCount > 0 && result.SkippedCount > 0)
                {
                    TempData["Warning"] = $"Imported {result.ImportedCount} appointment(s) successfully, but {result.SkippedCount} row(s) were skipped due to validation errors.";
                }
                else if (result.ImportedCount == 0 && result.SkippedCount > 0)
                {
                    TempData["Error"] = $"Import failed: 0 appointments imported. {result.SkippedCount} row(s) had validation errors.";
                }
                else
                {
                    TempData["Warning"] = "The uploaded file contained no valid appointment rows.";
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

        // GET: Appointments
        public async Task<IActionResult> Index(string statusFilter, string searchPatient, int? doctorFilter)
        {
            // جلب المواعيد مع تضمين بيانات المريض والطبيب لمنع الـ Lazy Loading Nulls
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsQueryable();

            // 1. الفلترة بحالة الموعد
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            // 2. الفلترة بالطبيب
            if (doctorFilter.HasValue && doctorFilter.Value > 0)
            {
                query = query.Where(a => a.DoctorId == doctorFilter.Value);
            }

            // 3. الفلترة والبحث باسم المريض أو رقم هاتفه
            if (!string.IsNullOrEmpty(searchPatient))
            {
                query = query.Where(a => a.Patient != null &&
                    (a.Patient.PatientName.Contains(searchPatient) || a.Patient.PhoneNumber.Contains(searchPatient)));
            }

            // حساب إجمالي مواعيد العيادة لحفظ العدادات العلوية عند الفلترة
            ViewBag.TotalAll = await _context.Appointments.CountAsync();
            ViewBag.TotalPending = await _context.Appointments.CountAsync(a => a.Status == "Pending");
            ViewBag.TotalConfirmed = await _context.Appointments.CountAsync(a => a.Status == "Confirmed");
            ViewBag.TotalCompleted = await _context.Appointments.CountAsync(a => a.Status == "Completed");
            ViewBag.TotalCancelled = await _context.Appointments.CountAsync(a => a.Status == "Cancelled");

            // ترتيب المواعيد تصاعدياً حسب التاريخ الأقرب ثم الوقت
            var appointments = await query
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .ToListAsync();

            // تمرير الفلاتر الحالية للـ View للاحتفاظ بالحالة في شريط البحث
            ViewBag.CurrentStatus = statusFilter;
            ViewBag.CurrentSearch = searchPatient;
            ViewBag.CurrentDoctor = doctorFilter;

            // قائمة الأطباء للفلتر
            ViewBag.Doctors = new SelectList(await _context.Doctors.OrderBy(d => d.DoctorName).ToListAsync(), "DoctorId", "DoctorName", doctorFilter);
            ViewBag.AllDoctors = await _context.Doctors.OrderBy(d => d.DoctorName).ToListAsync();
            ViewBag.AllPatients = await _context.Patients.OrderBy(p => p.PatientName).ToListAsync();

            return View(appointments);
        }

        // GET: Appointments/GetCalendarEvents — JSON endpoint for FullCalendar
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(string? statusFilter, int? doctorFilter)
        {
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(a => a.Status == statusFilter);

            if (doctorFilter.HasValue && doctorFilter.Value > 0)
                query = query.Where(a => a.DoctorId == doctorFilter.Value);

            var appointments = await query.ToListAsync();

            var events = appointments.Select(a =>
            {
                string color = a.Status switch
                {
                    "Confirmed"  => "#3b82f6",
                    "Completed"  => "#10b981",
                    "Cancelled"  => "#ef4444",
                    _            => "#f59e0b"   // Pending
                };

                var startDt = a.AppointmentDate.Add(a.AppointmentTime);
                var endDt   = startDt.AddMinutes(30);

                return new
                {
                    id          = a.AppointmentId,
                    title       = $"{a.Patient?.PatientName ?? "Patient"} — Dr. {a.Doctor?.DoctorName ?? "?"}",
                    start       = startDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end         = endDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    color,
                    extendedProps = new
                    {
                        status      = a.Status,
                        doctorName  = a.Doctor?.DoctorName ?? "",
                        patientName = a.Patient?.PatientName ?? "",
                        notes       = a.Notes ?? ""
                    }
                };
            });

            return Json(events);
        }

        // GET: Appointments/Create
        public IActionResult Create()
        {
            // جلب قائمة الأطباء والمرضى لتعبئة القوائم المنسدلة في الواجهة
            ViewData["DoctorId"] = new SelectList(_context.Doctors, "DoctorId", "DoctorName");
            ViewData["PatientId"] = new SelectList(_context.Patients, "PatientId", "PatientName");
            return View();
        }

        // POST: Appointments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AppointmentId,AppointmentDate,AppointmentTime,DoctorId,PatientId,Notes")] Appointment appointment)
        {
            if (ModelState.IsValid)
            {
                // تعيين الحالة الافتراضية عند الإنشاء
                appointment.Status = "Pending";

                _context.Add(appointment);
                await _context.SaveChangesAsync();

                // ── Trigger WhatsApp reminder automatically after booking ────────
                // Wrapped in try/catch: a broken/offline n8n webhook must NEVER fail the booking.
                try
                {
                    var patient = await _context.Patients.FindAsync(appointment.PatientId);
                    var doctor  = await _context.Doctors.FindAsync(appointment.DoctorId);

                    if (patient != null && !string.IsNullOrWhiteSpace(patient.PhoneNumber))
                    {
                        var appointmentDateTime = appointment.AppointmentDate.Add(appointment.AppointmentTime);
                        bool sent = await _whatsApp.SendAppointmentReminderAsync(
                            patient.PhoneNumber,
                            patient.PatientName,
                            appointmentDateTime,
                            doctor?.DoctorName ?? "your doctor");

                        if (sent)
                        {
                            appointment.IsReminderSent = true;
                            _context.Update(appointment);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception whatsAppEx)
                {
                    // Log warning — do NOT rethrow. The appointment was already saved successfully.
                    _logger?.LogWarning(whatsAppEx,
                        "[WhatsApp] Auto-reminder skipped for appointment #{Id} — webhook unreachable or misconfigured. Booking was still saved.",
                        appointment.AppointmentId);
                }
                // ────────────────────────────────────────────────────────────────

                TempData["Success"] = "Appointment booked successfully.";
                return RedirectToAction(nameof(Index));
            }

            // إعادة بناء القوائم المنسدلة في حال وجود خطأ في البيانات لمنع الـ Crash
            ViewData["DoctorId"] = new SelectList(_context.Doctors, "DoctorId", "DoctorName", appointment.DoctorId);
            ViewData["PatientId"] = new SelectList(_context.Patients, "PatientId", "PatientName", appointment.PatientId);
            return View(appointment);
        }

        // GET: Appointments/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            PopulateAppointmentDropdowns(appointment);
            return View(appointment);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AppointmentId,AppointmentDate,AppointmentTime,DoctorId,PatientId,Status,Notes")] Appointment appointment)
        {
            if (id != appointment.AppointmentId)
            {
                return NotFound();
            }

            // Remove navigation properties from validation
            ModelState.Remove(nameof(appointment.Doctor));
            ModelState.Remove(nameof(appointment.Patient));
            ModelState.Remove(nameof(appointment.Treatment));

            // Ensure AppointmentDate is handled cleanly in UTC for PostgreSQL
            appointment.AppointmentDate = DateTime.SpecifyKind(appointment.AppointmentDate.Date, DateTimeKind.Utc);

            // Check for doctor schedule conflicts: ensure the selected doctor doesn't already have another appointment at the exact same slot (excluding current AppointmentId)
            var appointmentDateUtc = appointment.AppointmentDate.Date;
            bool hasConflict = await _context.Appointments
                .AnyAsync(a => a.AppointmentId != id &&
                               a.DoctorId == appointment.DoctorId &&
                               a.AppointmentDate.Date == appointmentDateUtc &&
                               a.AppointmentTime == appointment.AppointmentTime &&
                               a.Status != "Cancelled");

            if (hasConflict)
            {
                var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
                ModelState.AddModelError(string.Empty, isAr
                    ? "الطبيب المحدد لديه موعد آخر محجوز في نفس هذا الوقت تماماً. يرجى اختيار موعد آخر."
                    : "The selected doctor already has another appointment scheduled at this exact time slot.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Appointments.FindAsync(id);
                    if (existing == null)
                    {
                        return NotFound();
                    }

                    existing.AppointmentDate = appointment.AppointmentDate;
                    existing.AppointmentTime = appointment.AppointmentTime;
                    existing.DoctorId        = appointment.DoctorId;
                    existing.PatientId       = appointment.PatientId;
                    existing.Status          = appointment.Status;
                    existing.Notes           = appointment.Notes;

                    _context.Update(existing);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Appointment updated and rescheduled successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Appointments.AnyAsync(e => e.AppointmentId == id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }

            PopulateAppointmentDropdowns(appointment);
            return View(appointment);
        }

        private void PopulateAppointmentDropdowns(Appointment? appointment = null)
        {
            ViewData["DoctorId"] = new SelectList(_context.Doctors.OrderBy(d => d.DoctorName), "DoctorId", "DoctorName", appointment?.DoctorId);
            ViewData["PatientId"] = new SelectList(_context.Patients.OrderBy(p => p.PatientName), "PatientId", "PatientName", appointment?.PatientId);

            var statusItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "Pending",   Text = "Pending" },
                new SelectListItem { Value = "Confirmed", Text = "Confirmed" },
                new SelectListItem { Value = "Completed", Text = "Completed" },
                new SelectListItem { Value = "Cancelled", Text = "Cancelled" }
            };

            ViewData["Status"] = new SelectList(statusItems, "Value", "Text", appointment?.Status ?? "Pending");
            ViewBag.StatusList = new SelectList(statusItems, "Value", "Text", appointment?.Status ?? "Pending");
        }

        // POST: Appointments/SendQuickWhatsApp
        // Dispatches either a reminder or a post-visit follow-up and persists the tracking flags.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendQuickWhatsApp(int id, string type)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
                return NotFound();

            var patient = appointment.Patient;
            var doctor  = appointment.Doctor;

            if (patient == null || string.IsNullOrWhiteSpace(patient.PhoneNumber))
            {
                TempData["Error"] = "Cannot send WhatsApp message: patient phone number is missing.";
                return RedirectToAction(nameof(Details), new { id });
            }

            bool sent = false;

            if (type == "reminder")
            {
                var appointmentDateTime = appointment.AppointmentDate.Add(appointment.AppointmentTime);
                sent = await _whatsApp.SendAppointmentReminderAsync(
                    patient.PhoneNumber,
                    patient.PatientName,
                    appointmentDateTime,
                    doctor?.DoctorName ?? "your doctor");

                if (sent)
                {
                    appointment.IsReminderSent = true;
                    TempData["Success"] = "WhatsApp reminder sent successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to send WhatsApp reminder. Please check the service configuration.";
                }
            }
            else if (type == "followup")
            {
                var appointmentDateTime = appointment.AppointmentDate.Add(appointment.AppointmentTime);
                string doctorPhone = string.Empty;
                sent = await _whatsApp.SendPostVisitFollowUpAsync(
                    patient.PhoneNumber,
                    patient.PatientName,
                    doctorPhone,
                    doctor?.DoctorName ?? "your doctor",
                    appointmentDateTime);

                if (sent)
                {
                    appointment.IsFollowUpSent  = true;
                    appointment.FollowUpSentAt  = DateTime.UtcNow;
                    TempData["Success"] = "WhatsApp follow-up message sent successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to send WhatsApp follow-up. Please check the service configuration.";
                }
            }
            else
            {
                TempData["Error"] = $"Unknown message type: '{type}'.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Update(appointment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Appointments/QuickBook — AJAX quick-book from calendar modal
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickBook([Bind("AppointmentDate,AppointmentTime,DoctorId,PatientId,Notes")] Appointment appointment)
        {
            if (ModelState.IsValid)
            {
                appointment.Status = "Pending";
                _context.Add(appointment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Appointment booked successfully via Quick Book!";
                return RedirectToAction(nameof(Index));
            }
            TempData["Error"] = "Could not save appointment. Please check all fields.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Appointments/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            // تحديث الحالة فقط
            appointment.Status = newStatus;
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Appointment status updated to {newStatus} successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Appointments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        // GET: Appointments/Complete/5
        public async Task<IActionResult> Complete(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment == null) return NotFound();

            // تمرير قيمة كشفية الدكتور لعرضها في الفاتورة
            ViewBag.ConsultationFee = appointment.Doctor != null ? appointment.Doctor.ConsultationFee : 0;

            return View(appointment);
        }

        // POST: Appointments/Complete/5
        // 🛠️ تم تعديل الباراميترز وربطها لتطابق خصائص موديل الـ Treatment الجديد تماماً
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string treatmentDesc, decimal treatmentCost, string? diagnosis, string? prescriptionNotes)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment == null) return NotFound();

            // 1. تحديث حالة الموعد إلى مكتمل
            appointment.Status = "Completed";
            _context.Update(appointment);

            // 2. إنشاء سجل علاج جديد وربطه بالموعد بالأسماء الصحيحة للحقول
            var treatment = new Treatment
            {
                AppointmentId = id,
                TreatmentDate = DateTime.Now,
                TreatmentDesc = string.IsNullOrEmpty(treatmentDesc) ? "No description provided" : treatmentDesc,
                TreatmentCost = treatmentCost,
                Diagnosis = diagnosis,
                PrescriptionNotes = prescriptionNotes
            };
            _context.Treatments.Add(treatment);

            // 3. احتساب الإجمالي المالي تلقائياً (كشفية الدكتور + تكلفة العلاج المضاف)
            decimal consultationFee = appointment.Doctor != null ? appointment.Doctor.ConsultationFee : 0;
            decimal totalInvoice = consultationFee + treatmentCost;

            await _context.SaveChangesAsync();

            // عرض رسالة النجاح والمالية كاملة للمستخدم
            TempData["Success"] = $"Appointment marked as Completed! Invoice Total: {totalInvoice} JOD (Consultation: {consultationFee} JOD + Treatment: {treatmentCost} JOD).";

            return RedirectToAction(nameof(Index));
        }
    }
}