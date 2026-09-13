using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin,Receptionist,Doctor")]
    public class AppointmentsController : Controller
    {
        private readonly ClinicDbContext _context;
        private readonly IWhatsAppService _whatsApp;
        private readonly IDataImportExportService _importExportService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<AppointmentsController> _logger;

        public AppointmentsController(ClinicDbContext context, IWhatsAppService whatsApp, IDataImportExportService importExportService, ISettingsService settingsService, ILogger<AppointmentsController> logger)
        {
            _context  = context;
            _whatsApp = whatsApp;
            _importExportService = importExportService;
            _settingsService = settingsService;
            _logger = logger;
        }

        #region Dynamic Working Hours & Exception Validation

        public enum WorkingHoursExceptionType
        {
            None,
            OutsideHours,
            OpeningBuffer,
            ClosingBuffer
        }

        private (bool isException, WorkingHoursExceptionType type, string shiftStart, string shiftEnd, string openingBufferEnd, string closingBufferStart)
            EvaluateWorkingHours(TimeSpan appointmentTime, ClinicSettingsViewModel cfg)
        {
            var start = TimeSpan.TryParse(cfg.WorkingHoursStart, out var s) ? s : new TimeSpan(8, 0, 0);
            var end   = TimeSpan.TryParse(cfg.WorkingHoursEnd, out var e) ? e : new TimeSpan(20, 0, 0);
            var slotMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 15;

            var openingBufferEnd = start.Add(TimeSpan.FromMinutes(slotMinutes));
            var closingBufferStart = end.Subtract(TimeSpan.FromMinutes(slotMinutes));

            string startStr = start.ToString(@"hh\:mm");
            string endStr = end.ToString(@"hh\:mm");
            string opBufEndStr = openingBufferEnd.ToString(@"hh\:mm");
            string clBufStartStr = closingBufferStart.ToString(@"hh\:mm");

            // Outside working hours: < shift.StartTime OR > shift.EndTime
            if (appointmentTime < start || appointmentTime > end)
            {
                return (true, WorkingHoursExceptionType.OutsideHours, startStr, endStr, opBufEndStr, clBufStartStr);
            }
            // Opening buffer: [shift.StartTime, shift.StartTime + SlotDurationMinutes]
            if (appointmentTime >= start && appointmentTime <= openingBufferEnd)
            {
                return (true, WorkingHoursExceptionType.OpeningBuffer, startStr, endStr, opBufEndStr, clBufStartStr);
            }
            // Closing buffer: [shift.EndTime - SlotDurationMinutes, shift.EndTime]
            if (appointmentTime >= closingBufferStart && appointmentTime <= end)
            {
                return (true, WorkingHoursExceptionType.ClosingBuffer, startStr, endStr, opBufEndStr, clBufStartStr);
            }

            return (false, WorkingHoursExceptionType.None, startStr, endStr, opBufEndStr, clBufStartStr);
        }

        // GET: Appointments/GetWorkingHoursConfig
        [HttpGet]
        public async Task<IActionResult> GetWorkingHoursConfig(int? doctorId, string? date)
        {
            var cfg = await _settingsService.GetSettingsAsync();
            var start = TimeSpan.TryParse(cfg.WorkingHoursStart, out var s) ? s : new TimeSpan(8, 0, 0);
            var end   = TimeSpan.TryParse(cfg.WorkingHoursEnd, out var e) ? e : new TimeSpan(20, 0, 0);
            var slotMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 15;

            var openingBufferEnd = start.Add(TimeSpan.FromMinutes(slotMinutes));
            var closingBufferStart = end.Subtract(TimeSpan.FromMinutes(slotMinutes));

            return Json(new
            {
                shiftStart = start.ToString(@"hh\:mm"),
                shiftEnd = end.ToString(@"hh\:mm"),
                slotDurationMinutes = slotMinutes,
                openingBufferEnd = openingBufferEnd.ToString(@"hh\:mm"),
                closingBufferStart = closingBufferStart.ToString(@"hh\:mm")
            });
        }

        #endregion

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
                TempData["Error"] = "Please select a valid CSV or Excel file to upload.";
                return RedirectToAction(nameof(Index));
            }

            if (!ExcelImportHelper.IsSupportedFile(file))
            {
                TempData["Error"] = "Invalid file type. Only CSV (.csv) and Excel (.xlsx, .xls) files are supported for import.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = ExcelImportHelper.GetStreamAsCsv(file);
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
            var currentClinicId = User.GetClinicId();
            var isDoctor = User.IsDoctor();
            int? currentDoctorId = null;

            if (isDoctor)
            {
                currentDoctorId = await User.GetDoctorIdAsync(_context);
                doctorFilter = currentDoctorId;
            }

            // جلب المواعيد مع تضمين بيانات المريض والطبيب لمنع الـ Lazy Loading Nulls
            var query = _context.Appointments
                .Where(a => a.ClinicId == currentClinicId)
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsQueryable();

            // 1. الفلترة بحالة الموعد
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            // 2. الفلترة بالطبيب: للأطباء يتم تقييد الاستعلام بطبيبهم فقط، وللمشرفين وموظفي الاستقبال يُسمح بالاستعلام العام
            if (isDoctor)
            {
                if (currentDoctorId.HasValue)
                {
                    query = query.Where(a => a.DoctorId == currentDoctorId.Value);
                }
                else
                {
                    query = query.Where(a => false);
                }
            }
            else if (doctorFilter.HasValue && doctorFilter.Value > 0)
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
            var baseCountQuery = _context.Appointments.Where(a => a.ClinicId == currentClinicId);
            if (isDoctor && currentDoctorId.HasValue)
            {
                baseCountQuery = baseCountQuery.Where(a => a.DoctorId == currentDoctorId.Value);
            }

            ViewBag.TotalAll = await baseCountQuery.CountAsync();
            ViewBag.TotalPending = await baseCountQuery.CountAsync(a => a.Status == "Pending" || a.Status == "Pending Confirmation");
            ViewBag.TotalConfirmed = await baseCountQuery.CountAsync(a => a.Status == "Confirmed");
            ViewBag.TotalCompleted = await baseCountQuery.CountAsync(a => a.Status == "Completed");
            ViewBag.TotalCancelled = await baseCountQuery.CountAsync(a => a.Status == "Cancelled");

            // ترتيب المواعيد تصاعدياً حسب التاريخ الأقرب ثم الوقت
            var appointments = await query
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .ToListAsync();

            // تمرير الفلاتر الحالية للـ View للاحتفاظ بالحالة في شريط البحث
            ViewBag.CurrentStatus = statusFilter;
            ViewBag.CurrentSearch = searchPatient;
            ViewBag.CurrentDoctor = doctorFilter;
            ViewBag.IsDoctor = isDoctor;

            // قائمة الأطباء للفلتر
            if (isDoctor && currentDoctorId.HasValue)
            {
                var myDocs = await _context.Doctors.Where(d => d.ClinicId == currentClinicId && d.DoctorId == currentDoctorId.Value).ToListAsync();
                ViewBag.Doctors = new SelectList(myDocs, "DoctorId", "DoctorName", currentDoctorId);
                ViewBag.AllDoctors = myDocs;
                ViewBag.CurrentDoctorId = currentDoctorId.Value;
                ViewBag.CurrentDoctorName = myDocs.FirstOrDefault()?.DoctorName;

                var myAssignedPatients = await _context.Appointments
                    .Where(a => a.ClinicId == currentClinicId && a.DoctorId == currentDoctorId.Value)
                    .Select(a => a.PatientId)
                    .Union(_context.MedicalRecords
                        .Where(m => m.DoctorId == currentDoctorId.Value)
                        .Select(m => m.PatientId))
                    .Distinct()
                    .ToListAsync();
                ViewBag.AllPatients = await _context.Patients.Where(p => p.ClinicId == currentClinicId && myAssignedPatients.Contains(p.PatientId)).OrderBy(p => p.PatientName).ToListAsync();
            }
            else
            {
                ViewBag.Doctors = new SelectList(await _context.Doctors.Where(d => d.ClinicId == currentClinicId).OrderBy(d => d.DoctorName).ToListAsync(), "DoctorId", "DoctorName", doctorFilter);
                ViewBag.AllDoctors = await _context.Doctors.Where(d => d.ClinicId == currentClinicId).OrderBy(d => d.DoctorName).ToListAsync();
                ViewBag.AllPatients = await _context.Patients.Where(p => p.ClinicId == currentClinicId).OrderBy(p => p.PatientName).ToListAsync();
            }

            var cfg = await _settingsService.GetSettingsAsync();
            ViewBag.WorkingHoursStart = cfg.WorkingHoursStart ?? "08:00";
            ViewBag.WorkingHoursEnd = cfg.WorkingHoursEnd ?? "20:00";
            ViewBag.SlotDurationMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 15;

            return View(appointments);
        }

        // GET: Appointments/GetCalendarEvents — JSON endpoint for FullCalendar
        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(string? statusFilter, int? doctorFilter)
        {
            var currentClinicId = User.GetClinicId();
            var isDoctor = User.IsDoctor();
            var query = _context.Appointments
                .Where(a => a.ClinicId == currentClinicId)
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsQueryable();

            if (isDoctor)
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (currentDoctorId.HasValue)
                {
                    query = query.Where(a => a.DoctorId == currentDoctorId.Value);
                }
                else
                {
                    query = query.Where(a => false);
                }
            }
            else if (doctorFilter.HasValue && doctorFilter.Value > 0)
            {
                query = query.Where(a => a.DoctorId == doctorFilter.Value);
            }

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(a => a.Status == statusFilter);

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

                var rawDocName = a.Doctor?.DoctorName?.Trim() ?? "?";
                var cleanDoc = System.Text.RegularExpressions.Regex.Replace(rawDocName, @"^(Dr\.\s*|د\.\s*)+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                var docTitle = string.IsNullOrWhiteSpace(cleanDoc) || cleanDoc == "?" ? "?" : $"Dr. {cleanDoc}";

                var isExc = a.IsOutsideHoursException;

                return new
                {
                    id          = a.AppointmentId,
                    title       = $"{a.Patient?.PatientName ?? "Patient"} — {docTitle}" + (isExc ? " (⚠️ Exception)" : ""),
                    start       = startDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end         = endDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    color,
                    className   = (a.Status == "Cancelled" ? "event-cancelled " : "") + (isExc ? "event-exception" : ""),
                    extendedProps = new
                    {
                        status      = a.Status,
                        doctorName  = docTitle,
                        patientName = a.Patient?.PatientName ?? "",
                        notes       = a.Notes ?? "",
                        isException = isExc
                    }
                };
            });

            return Json(events);
        }

        // GET: Appointments/Create
        public async Task<IActionResult> Create()
        {
            var currentClinicId = User.GetClinicId();
            var isDoctor = User.IsDoctor();

            if (isDoctor)
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (currentDoctorId.HasValue)
                {
                    var myDocs = await _context.Doctors.Where(d => d.ClinicId == currentClinicId && d.DoctorId == currentDoctorId.Value).ToListAsync();
                    ViewBag.CurrentDoctorId = currentDoctorId.Value;
                    ViewBag.CurrentDoctorName = myDocs.FirstOrDefault()?.DoctorName;
                    ViewData["DoctorId"] = new SelectList(myDocs, "DoctorId", "DoctorName", currentDoctorId.Value);

                    var myPatientIds = await _context.Appointments
                        .Where(a => a.ClinicId == currentClinicId && a.DoctorId == currentDoctorId.Value)
                        .Select(a => a.PatientId)
                        .Union(_context.MedicalRecords
                            .Where(m => m.DoctorId == currentDoctorId.Value)
                            .Select(m => m.PatientId))
                        .Distinct()
                        .ToListAsync();

                    ViewData["PatientId"] = new SelectList(_context.Patients.Where(p => p.ClinicId == currentClinicId && myPatientIds.Contains(p.PatientId)).OrderBy(p => p.PatientName), "PatientId", "PatientName");
                }
                else
                {
                    ViewData["DoctorId"] = new SelectList(Enumerable.Empty<Doctor>(), "DoctorId", "DoctorName");
                    ViewData["PatientId"] = new SelectList(Enumerable.Empty<Patient>(), "PatientId", "PatientName");
                }
            }
            else
            {
                ViewData["DoctorId"] = new SelectList(_context.Doctors.Where(d => d.ClinicId == currentClinicId).OrderBy(d => d.DoctorName), "DoctorId", "DoctorName");
                ViewData["PatientId"] = new SelectList(_context.Patients.Where(p => p.ClinicId == currentClinicId).OrderBy(p => p.PatientName), "PatientId", "PatientName");
            }

            var cfg = await _settingsService.GetSettingsAsync();
            ViewBag.WorkingHoursStart = cfg.WorkingHoursStart ?? "08:00";
            ViewBag.WorkingHoursEnd = cfg.WorkingHoursEnd ?? "20:00";
            ViewBag.SlotDurationMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 15;
            ViewBag.IsDoctor = isDoctor;

            return View();
        }

        // POST: Appointments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AppointmentId,AppointmentDate,AppointmentTime,DoctorId,PatientId,Notes,IsWeekendOverride,IsOutsideHoursException")] Appointment appointment, bool isException = false)
        {
            var currentClinicId = User.GetClinicId();
            var isDoctor = User.IsDoctor();
            var cfg = await _settingsService.GetSettingsAsync();
            ViewBag.WorkingHoursStart = cfg.WorkingHoursStart ?? "08:00";
            ViewBag.WorkingHoursEnd = cfg.WorkingHoursEnd ?? "20:00";
            ViewBag.SlotDurationMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 15;
            ViewBag.IsDoctor = isDoctor;

            if (isDoctor)
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (currentDoctorId.HasValue)
                {
                    appointment.DoctorId = currentDoctorId.Value;

                    var isAssignedPatient = await _context.Appointments.AnyAsync(a => a.ClinicId == currentClinicId && a.DoctorId == currentDoctorId.Value && a.PatientId == appointment.PatientId)
                        || await _context.MedicalRecords.AnyAsync(m => m.DoctorId == currentDoctorId.Value && m.PatientId == appointment.PatientId);

                    if (!isAssignedPatient)
                    {
                        var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
                        ModelState.AddModelError("PatientId", isAr
                            ? "يمكن للطبيب حجز مواعيد متابعة لمرضاه المسجلين لديه فقط."
                            : "Doctors can only request/book follow-up appointments for their own assigned patients.");
                    }
                }
                else
                {
                    return Forbid();
                }
            }

            var eval = EvaluateWorkingHours(appointment.AppointmentTime, cfg);
            if (eval.isException && (isException || appointment.IsOutsideHoursException))
            {
                appointment.IsOutsideHoursException = true;
            }

            // Explicitly enforce AppointmentDate >= DateTime.Today
            if (appointment.AppointmentDate.Date < DateTime.Today)
            {
                string error = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                    ? "لا يمكن حجز موعد في تاريخ سابق لليوم (يجب أن يكون الموعد اليوم أو في تاريخ مستقبلي)."
                    : "Appointment date must be today or in the future.";
                ModelState.AddModelError(nameof(appointment.AppointmentDate), error);
                PopulateAppointmentDropdowns(appointment);
                return View(appointment);
            }

            // Ensure combined local or UTC time is compared properly
            DateTime appointmentDateTime = appointment.AppointmentDate.Date.Add(appointment.AppointmentTime);
            if (appointmentDateTime < DateTime.Now)
            {
                string error = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                    ? "لا يمكن حجز موعد في تاريخ أو وقت سابق عن الوقت الحالي."
                    : "Cannot schedule an appointment in the past.";
                ModelState.AddModelError("AppointmentTime", error);
                PopulateAppointmentDropdowns(appointment);
                return View(appointment);
            }

            var isWeekend = appointment.AppointmentDate.DayOfWeek == DayOfWeek.Friday || appointment.AppointmentDate.DayOfWeek == DayOfWeek.Saturday;
            if (isWeekend && !appointment.IsWeekendOverride)
            {
                var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
                var warningMsg = isAr
                    ? "التاريخ المحدد يوافق عطلة نهاية الأسبوع (الجمعة/السبت). يرجى تأكيد الاستثناء للمتابعة."
                    : "Selected date falls on a weekend (Friday/Saturday). Please confirm the exception to proceed.";
                ModelState.AddModelError(nameof(appointment.AppointmentDate), warningMsg);
                ModelState.AddModelError(string.Empty, warningMsg);
            }

            if (ModelState.IsValid)
            {
                appointment.ClinicId = currentClinicId;
                // Force status = AppointmentStatus.Pending on POST to AppointmentsController.Create if user has role Doctor
                appointment.Status = isDoctor ? AppointmentStatus.Pending : (string.IsNullOrWhiteSpace(appointment.Status) ? AppointmentStatus.Pending : appointment.Status);
                appointment.AppointmentDate = DateTime.SpecifyKind(appointment.AppointmentDate.Date, DateTimeKind.Utc);

                _context.Add(appointment);
                await _context.SaveChangesAsync();

                // ── Trigger WhatsApp reminder automatically after booking ────────
                try
                {
                    var patient = await _context.Patients.FindAsync(appointment.PatientId);
                    var doctor  = await _context.Doctors.FindAsync(appointment.DoctorId);

                    if (patient != null && !string.IsNullOrWhiteSpace(patient.PhoneNumber))
                    {
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
                    _logger?.LogWarning(whatsAppEx,
                        "[WhatsApp] Auto-reminder skipped for appointment #{Id} — webhook unreachable or misconfigured. Booking was still saved.",
                        appointment.AppointmentId);
                }

                TempData["Success"] = isDoctor
                    ? "Follow-up appointment requested successfully (Pending Confirmation by Reception)."
                    : "Appointment booked successfully.";
                return RedirectToAction(nameof(Index));
            }

            PopulateAppointmentDropdowns(appointment);
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

            var currentClinicId = User.GetClinicId();
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id && m.ClinicId == currentClinicId);

            if (appointment == null)
            {
                return NotFound();
            }

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (!currentDoctorId.HasValue || appointment.DoctorId != currentDoctorId.Value)
                {
                    return Forbid();
                }
            }

            // Disallow editing Completed or Cancelled appointments
            if (appointment.Status == "Cancelled" || appointment.Status == "Completed")
            {
                var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
                var warningMsg = isAr
                    ? "لا يمكن تعديل موعد مكتمل أو ملغى بالفعل."
                    : "Cannot edit an appointment that is already Completed or Cancelled.";
                TempData["Warning"] = warningMsg;
                TempData["Error"] = warningMsg;
                return RedirectToAction(nameof(Details), new { id = appointment.AppointmentId });
            }

            var cfg = await _settingsService.GetSettingsAsync();
            ViewBag.WorkingHoursStart = cfg.WorkingHoursStart ?? "08:00";
            ViewBag.WorkingHoursEnd = cfg.WorkingHoursEnd ?? "20:00";
            ViewBag.SlotDurationMinutes = cfg.DefaultSlotDurationMinutes > 0 ? cfg.DefaultSlotDurationMinutes : 15;

            PopulateAppointmentDropdowns(appointment);
            return View(appointment);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AppointmentId,AppointmentDate,AppointmentTime,DoctorId,PatientId,Status,Notes,IsWeekendOverride,IsOutsideHoursException")] Appointment appointment, bool isException = false)
        {
            if (id != appointment.AppointmentId)
            {
                return NotFound();
            }

            var currentClinicId = User.GetClinicId();
            var existing = await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == id && a.ClinicId == currentClinicId);
            if (existing == null)
            {
                return NotFound();
            }

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (!currentDoctorId.HasValue || existing.DoctorId != currentDoctorId.Value)
                {
                    return Forbid();
                }
                appointment.DoctorId = currentDoctorId.Value;
            }

            // Disallow editing Completed or Cancelled appointments
            if (existing.Status == "Cancelled" || existing.Status == "Completed")
            {
                var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
                var warningMsg = isAr
                    ? "لا يمكن تعديل موعد مكتمل أو ملغى بالفعل."
                    : "Cannot edit an appointment that is already Completed or Cancelled.";
                TempData["Warning"] = warningMsg;
                TempData["Error"] = warningMsg;
                return RedirectToAction(nameof(Details), new { id });
            }

            // Remove navigation properties from validation
            ModelState.Remove(nameof(appointment.Doctor));
            ModelState.Remove(nameof(appointment.Patient));
            ModelState.Remove(nameof(appointment.Treatment));

            // Explicitly enforce AppointmentDate >= DateTime.Today
            if (appointment.AppointmentDate.Date < DateTime.Today)
            {
                string error = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                    ? "لا يمكن حجز موعد في تاريخ سابق لليوم (يجب أن يكون الموعد اليوم أو في تاريخ مستقبلي)."
                    : "Appointment date must be today or in the future.";
                ModelState.AddModelError(nameof(appointment.AppointmentDate), error);
                PopulateAppointmentDropdowns(appointment);
                return View(appointment);
            }

            // Ensure combined local or UTC time is compared properly
            DateTime appointmentDateTime = appointment.AppointmentDate.Date.Add(appointment.AppointmentTime);
            if (appointmentDateTime < DateTime.Now)
            {
                string error = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                    ? "لا يمكن حجز موعد في تاريخ أو وقت سابق عن الوقت الحالي."
                    : "Cannot schedule an appointment in the past.";
                ModelState.AddModelError("AppointmentTime", error);
                PopulateAppointmentDropdowns(appointment);
                return View(appointment);
            }

            // Ensure AppointmentDate is handled cleanly in UTC for PostgreSQL
            appointment.AppointmentDate = DateTime.SpecifyKind(appointment.AppointmentDate.Date, DateTimeKind.Utc);

            var isWeekend = appointment.AppointmentDate.DayOfWeek == DayOfWeek.Friday || appointment.AppointmentDate.DayOfWeek == DayOfWeek.Saturday;
            if (isWeekend && !appointment.IsWeekendOverride)
            {
                var isAr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
                var warningMsg = isAr
                    ? "التاريخ المحدد يوافق عطلة نهاية الأسبوع (الجمعة/السبت). يرجى تأكيد الاستثناء للمتابعة."
                    : "Selected date falls on a weekend (Friday/Saturday). Please confirm the exception to proceed.";
                ModelState.AddModelError(nameof(appointment.AppointmentDate), warningMsg);
                ModelState.AddModelError(string.Empty, warningMsg);
            }

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

                    var cfg = await _settingsService.GetSettingsAsync();
                    var eval = EvaluateWorkingHours(appointment.AppointmentTime, cfg);
                    bool finalException = (isException || appointment.IsOutsideHoursException) && eval.isException;

                    // Only Receptionist or Admin can elevate status to Confirmed
                    if (User.IsDoctor() && appointment.Status == AppointmentStatus.Confirmed && existing.Status != AppointmentStatus.Confirmed)
                    {
                        appointment.Status = existing.Status;
                    }

                    existing.AppointmentDate = appointment.AppointmentDate;
                    existing.AppointmentTime = appointment.AppointmentTime;
                    existing.DoctorId        = appointment.DoctorId;
                    existing.PatientId       = appointment.PatientId;
                    existing.Status          = appointment.Status;
                    existing.Notes           = appointment.Notes;
                    existing.IsOutsideHoursException = finalException;

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

            var cfgFail = await _settingsService.GetSettingsAsync();
            ViewBag.WorkingHoursStart = cfgFail.WorkingHoursStart ?? "08:00";
            ViewBag.WorkingHoursEnd = cfgFail.WorkingHoursEnd ?? "20:00";
            ViewBag.SlotDurationMinutes = cfgFail.DefaultSlotDurationMinutes > 0 ? cfgFail.DefaultSlotDurationMinutes : 15;

            PopulateAppointmentDropdowns(appointment);
            return View(appointment);
        }

        private void PopulateAppointmentDropdowns(Appointment? appointment = null)
        {
            var currentClinicId = User.GetClinicId();
            var isDoctor = User.IsDoctor();
            int? currentDoctorId = User.GetDoctorId();

            if (isDoctor && !currentDoctorId.HasValue)
            {
                var userEmail = User.Identity?.Name;
                currentDoctorId = _context.Doctors.FirstOrDefault(d => d.ClinicId == currentClinicId && (d.DoctorEmail == userEmail || d.DoctorName == userEmail || d.DoctorNumber == userEmail))?.DoctorId;
            }

            if (isDoctor && currentDoctorId.HasValue)
            {
                var myDocs = _context.Doctors.Where(d => d.ClinicId == currentClinicId && d.DoctorId == currentDoctorId.Value).ToList();
                ViewBag.CurrentDoctorId = currentDoctorId.Value;
                ViewBag.CurrentDoctorName = myDocs.FirstOrDefault()?.DoctorName;
                ViewData["DoctorId"] = new SelectList(myDocs, "DoctorId", "DoctorName", currentDoctorId.Value);

                var myPatientIds = _context.Appointments
                    .Where(a => a.ClinicId == currentClinicId && a.DoctorId == currentDoctorId.Value)
                    .Select(a => a.PatientId)
                    .Union(_context.MedicalRecords
                        .Where(m => m.DoctorId == currentDoctorId.Value)
                        .Select(m => m.PatientId))
                    .Distinct()
                    .ToList();

                ViewData["PatientId"] = new SelectList(_context.Patients.Where(p => p.ClinicId == currentClinicId && myPatientIds.Contains(p.PatientId)).OrderBy(p => p.PatientName), "PatientId", "PatientName", appointment?.PatientId);
            }
            else
            {
                ViewData["DoctorId"] = new SelectList(_context.Doctors.Where(d => d.ClinicId == currentClinicId).OrderBy(d => d.DoctorName), "DoctorId", "DoctorName", appointment?.DoctorId);
                ViewData["PatientId"] = new SelectList(_context.Patients.Where(p => p.ClinicId == currentClinicId).OrderBy(p => p.PatientName), "PatientId", "PatientName", appointment?.PatientId);
            }

            var statusItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "Pending",   Text = "Pending" },
                new SelectListItem { Value = "Pending Confirmation", Text = "Pending Confirmation" },
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
        public async Task<IActionResult> QuickBook([Bind("AppointmentDate,AppointmentTime,DoctorId,PatientId,Notes,IsOutsideHoursException")] Appointment appointment, bool isException = false)
        {
            if (appointment.AppointmentDate.Date < DateTime.Today)
            {
                string error = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                    ? "لا يمكن حجز موعد في تاريخ سابق لليوم (يجب أن يكون الموعد اليوم أو في تاريخ مستقبلي)."
                    : "Appointment date must be today or in the future.";
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            DateTime appointmentDateTime = appointment.AppointmentDate.Date.Add(appointment.AppointmentTime);
            if (appointmentDateTime < DateTime.Now)
            {
                string error = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                    ? "لا يمكن حجز موعد في تاريخ أو وقت سابق عن الوقت الحالي."
                    : "Cannot schedule an appointment in the past.";
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

            var cfg = await _settingsService.GetSettingsAsync();
            var eval = EvaluateWorkingHours(appointment.AppointmentTime, cfg);
            if (eval.isException && (isException || appointment.IsOutsideHoursException))
            {
                appointment.IsOutsideHoursException = true;
            }

            if (ModelState.IsValid)
            {
                appointment.ClinicId = User.GetClinicId();

                if (User.IsDoctor())
                {
                    var currentDoctorId = await User.GetDoctorIdAsync(_context);
                    if (currentDoctorId.HasValue)
                    {
                        appointment.DoctorId = currentDoctorId.Value;
                        appointment.Status = AppointmentStatus.Pending;
                    }
                    else
                    {
                        return Forbid();
                    }
                }
                else
                {
                    appointment.Status = AppointmentStatus.Pending;
                }

                appointment.AppointmentDate = DateTime.SpecifyKind(appointment.AppointmentDate.Date, DateTimeKind.Utc);
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
            var currentClinicId = User.GetClinicId();
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == id && a.ClinicId == currentClinicId);
            if (appointment == null)
            {
                return NotFound();
            }

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (!currentDoctorId.HasValue || appointment.DoctorId != currentDoctorId.Value)
                {
                    return Forbid();
                }

                // Only Receptionist or Admin can update status to Confirmed
                if (newStatus == AppointmentStatus.Confirmed)
                {
                    return Forbid();
                }
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
            var currentClinicId = User.GetClinicId();

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id && m.ClinicId == currentClinicId);

            if (appointment == null) return NotFound();

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (!currentDoctorId.HasValue || appointment.DoctorId != currentDoctorId.Value)
                {
                    return Forbid();
                }
            }

            return View(appointment);
        }

        // GET: Appointments/PrintSlip/5
        [HttpGet]
        public async Task<IActionResult> PrintSlip(int? id)
        {
            if (id == null) return NotFound();
            var currentClinicId = User.GetClinicId();

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id && m.ClinicId == currentClinicId);

            if (appointment == null) return NotFound();

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (!currentDoctorId.HasValue || appointment.DoctorId != currentDoctorId.Value)
                {
                    return Forbid();
                }
            }

            return View(appointment);
        }

        // GET: Appointments/Complete/5
        public async Task<IActionResult> Complete(int? id)
        {
            if (id == null) return NotFound();
            var currentClinicId = User.GetClinicId();

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id && m.ClinicId == currentClinicId);

            if (appointment == null) return NotFound();

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (!currentDoctorId.HasValue || appointment.DoctorId != currentDoctorId.Value)
                {
                    return Forbid();
                }
            }

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

            if (User.IsDoctor())
            {
                var currentDoctorId = await User.GetDoctorIdAsync(_context);
                if (!currentDoctorId.HasValue || appointment.DoctorId != currentDoctorId.Value)
                {
                    return Forbid();
                }
            }

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

            var clinicSettings = await _settingsService.GetSettingsAsync();
            var currency = !string.IsNullOrWhiteSpace(clinicSettings.CurrencySymbol) ? clinicSettings.CurrencySymbol : "JOD";

            // عرض رسالة النجاح والمالية كاملة للمستخدم
            TempData["Success"] = $"Appointment marked as Completed! Invoice Total: {totalInvoice:N2} {currency} (Consultation: {consultationFee:N2} {currency} + Treatment: {treatmentCost:N2} {currency}).";

            return RedirectToAction(nameof(Index));
        }
    }
}