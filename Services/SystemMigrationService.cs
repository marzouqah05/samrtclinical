using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class SystemMigrationService : ISystemMigrationService
    {
        private readonly ClinicDbContext _context;

        public SystemMigrationService(ClinicDbContext context)
        {
            _context = context;
        }

        #region Multi-Sheet Excel Template Generation

        /// <summary>
        /// Generates a styled, multi-sheet Excel (.xlsx) workbook containing template sheets
        /// for Departments, Doctors, Patients, Appointments, Treatments, and Invoices.
        /// </summary>
        public byte[] GenerateFullMigrationExcelTemplate()
        {
            using var workbook = new XLWorkbook();

            // Style Helper
            void StyleHeaderRow(IXLWorksheet ws, string[] headers, XLColor headerColor)
            {
                for (int col = 1; col <= headers.Length; col++)
                {
                    var cell = ws.Cell(1, col);
                    cell.Value = headers[col - 1];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = headerColor;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                ws.Row(1).Height = 28;
            }

            // ── Sheet 1: Departments ───────────────────────────────────────────
            var wsDepts = workbook.Worksheets.Add("Departments");
            string[] deptHeaders = { "DepartmentName", "DepartmentAbbr" };
            StyleHeaderRow(wsDepts, deptHeaders, XLColor.FromHtml("#2563eb"));
            wsDepts.Cell(2, 1).Value = "Cardiology";
            wsDepts.Cell(2, 2).Value = "CARD";
            wsDepts.Cell(3, 1).Value = "Pediatrics";
            wsDepts.Cell(3, 2).Value = "PED";
            wsDepts.Cell(4, 1).Value = "General Medicine";
            wsDepts.Cell(4, 2).Value = "GEN";
            wsDepts.Columns().AdjustToContents(15, 40);

            // ── Sheet 2: Doctors ───────────────────────────────────────────────
            var wsDocs = workbook.Worksheets.Add("Doctors");
            string[] docHeaders = { "DoctorName", "DoctorNumber", "Specialization", "ConsultationFee", "DepartmentName" };
            StyleHeaderRow(wsDocs, docHeaders, XLColor.FromHtml("#059669"));
            wsDocs.Cell(2, 1).Value = "Dr. Sarah Ahmed";
            wsDocs.Cell(2, 2).Value = "DOC101";
            wsDocs.Cell(2, 3).Value = "Cardiology";
            wsDocs.Cell(2, 4).Value = 250.00;
            wsDocs.Cell(2, 5).Value = "Cardiology";

            wsDocs.Cell(3, 1).Value = "Dr. Mohamed Ali";
            wsDocs.Cell(3, 2).Value = "DOC102";
            wsDocs.Cell(3, 3).Value = "Pediatrics";
            wsDocs.Cell(3, 4).Value = 180.00;
            wsDocs.Cell(3, 5).Value = "Pediatrics";
            wsDocs.Columns().AdjustToContents(15, 40);

            // ── Sheet 3: Patients ──────────────────────────────────────────────
            var wsPatients = workbook.Worksheets.Add("Patients");
            string[] patientHeaders = { "FullName", "NationalID", "PhoneNumber", "DateOfBirth", "BloodType", "Allergies", "ChronicDiseases", "Notes" };
            StyleHeaderRow(wsPatients, patientHeaders, XLColor.FromHtml("#4f46e5"));
            wsPatients.Cell(2, 1).Value = "Ahmed Mohamed Ali";
            wsPatients.Cell(2, 2).Value = "1098765432";
            wsPatients.Cell(2, 3).Value = "0501234567";
            wsPatients.Cell(2, 4).Value = "1992-05-15";
            wsPatients.Cell(2, 5).Value = "O+";
            wsPatients.Cell(2, 6).Value = "Penicillin";
            wsPatients.Cell(2, 7).Value = "None";
            wsPatients.Cell(2, 8).Value = "Referred by Dr. Khalid";

            wsPatients.Cell(3, 1).Value = "Sara Salem Omar";
            wsPatients.Cell(3, 2).Value = "1087654321";
            wsPatients.Cell(3, 3).Value = "0559876543";
            wsPatients.Cell(3, 4).Value = "1988-11-20";
            wsPatients.Cell(3, 5).Value = "A+";
            wsPatients.Cell(3, 6).Value = "None";
            wsPatients.Cell(3, 7).Value = "Diabetes Type 2";
            wsPatients.Cell(3, 8).Value = "Routine annual checkup";
            wsPatients.Columns().AdjustToContents(15, 40);

            // ── Sheet 4: Appointments ──────────────────────────────────────────
            var wsAppts = workbook.Worksheets.Add("Appointments");
            string[] apptHeaders = { "PatientNationalID", "DoctorNameOrNumber", "AppointmentDate", "AppointmentTime", "Status", "Notes" };
            StyleHeaderRow(wsAppts, apptHeaders, XLColor.FromHtml("#d97706"));
            wsAppts.Cell(2, 1).Value = "1098765432";
            wsAppts.Cell(2, 2).Value = "DOC101";
            wsAppts.Cell(2, 3).Value = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            wsAppts.Cell(2, 4).Value = "09:30";
            wsAppts.Cell(2, 5).Value = "Pending";
            wsAppts.Cell(2, 6).Value = "Initial consultation";

            wsAppts.Cell(3, 1).Value = "1087654321";
            wsAppts.Cell(3, 2).Value = "Dr. Mohamed Ali";
            wsAppts.Cell(3, 3).Value = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");
            wsAppts.Cell(3, 4).Value = "11:00";
            wsAppts.Cell(3, 5).Value = "Confirmed";
            wsAppts.Cell(3, 6).Value = "Follow-up visit";
            wsAppts.Columns().AdjustToContents(15, 40);

            // ── Sheet 5: Treatments ────────────────────────────────────────────
            var wsTreatments = workbook.Worksheets.Add("Treatments");
            string[] treatmentHeaders = { "PatientNationalID", "TreatmentDate", "TreatmentDesc", "Diagnosis", "PrescriptionNotes", "TreatmentCost" };
            StyleHeaderRow(wsTreatments, treatmentHeaders, XLColor.FromHtml("#7c3aed"));
            wsTreatments.Cell(2, 1).Value = "1098765432";
            wsTreatments.Cell(2, 2).Value = DateTime.Today.ToString("yyyy-MM-dd");
            wsTreatments.Cell(2, 3).Value = "Cardiac ECG & Blood Pressure Check";
            wsTreatments.Cell(2, 4).Value = "Mild Hypertension";
            wsTreatments.Cell(2, 5).Value = "Prescribed Amlodipine 5mg daily";
            wsTreatments.Cell(2, 6).Value = 350.00;
            wsTreatments.Columns().AdjustToContents(15, 40);

            // ── Sheet 6: Invoices ──────────────────────────────────────────────
            var wsInvoices = workbook.Worksheets.Add("Invoices");
            string[] invoiceHeaders = { "InvoiceNumber", "PatientNationalID", "InvoiceDate", "Amount", "Discount", "Tax", "NetAmount", "Status" };
            StyleHeaderRow(wsInvoices, invoiceHeaders, XLColor.FromHtml("#0891b2"));
            wsInvoices.Cell(2, 1).Value = "INV-2026-001";
            wsInvoices.Cell(2, 2).Value = "1098765432";
            wsInvoices.Cell(2, 3).Value = DateTime.Today.ToString("yyyy-MM-dd");
            wsInvoices.Cell(2, 4).Value = 350.00;
            wsInvoices.Cell(2, 5).Value = 0.00;
            wsInvoices.Cell(2, 6).Value = 52.50;
            wsInvoices.Cell(2, 7).Value = 402.50;
            wsInvoices.Cell(2, 8).Value = "Paid";
            wsInvoices.Columns().AdjustToContents(15, 40);

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }

        #endregion

        #region Full System Migration from Multi-Sheet Excel

        /// <summary>
        /// Orchestrates sequential migration across all clinical entities with smart FK resolution and dependency linking.
        /// </summary>
        public async Task<SystemMigrationResult> MigrateFromExcelAsync(Stream excelStream)
        {
            var result = new SystemMigrationResult();

            using var workbook = new XLWorkbook(excelStream);

            // Find sheet by multiple aliases
            IXLWorksheet? FindSheet(params string[] aliases)
            {
                foreach (var ws in workbook.Worksheets)
                {
                    string cleanName = NormalizeHeader(ws.Name);
                    foreach (var alias in aliases)
                    {
                        if (cleanName == NormalizeHeader(alias) || cleanName.Contains(NormalizeHeader(alias)))
                            return ws;
                    }
                }
                return null;
            }

            // ─────────────────────────────────────────────────────────────────
            // 1. MIGRATE DEPARTMENTS
            // ─────────────────────────────────────────────────────────────────
            var deptSummary = new MigrationEntitySummary { EntityName = "Departments" };
            result.EntitySummaries.Add(deptSummary);

            var departmentsMap = (await _context.Departments.ToListAsync())
                .ToDictionary(d => d.DepartmentName.Trim(), d => d, StringComparer.OrdinalIgnoreCase);

            var deptSheet = FindSheet("Departments", "Department", "الأقسام", "الاقسام");
            if (deptSheet != null)
            {
                var rows = deptSheet.RowsUsed().Skip(1); // Skip header
                foreach (var row in rows)
                {
                    deptSummary.TotalFound++;
                    string name = row.Cell(1).GetString().Trim();
                    string abbr = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        deptSummary.SkippedCount++;
                        deptSummary.Errors.Add($"Row {row.RowNumber()}: Department Name is required.");
                        continue;
                    }

                    if (!departmentsMap.ContainsKey(name))
                    {
                        var dept = new Department
                        {
                            DepartmentName = name,
                            DepartmentAbbr = string.IsNullOrWhiteSpace(abbr) ? (name.Length > 4 ? name.Substring(0, 4).ToUpper() : name.ToUpper()) : abbr
                        };
                        _context.Departments.Add(dept);
                        departmentsMap[name] = dept;
                        deptSummary.ImportedCount++;
                    }
                    else
                    {
                        deptSummary.SkippedCount++;
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Ensure a default department exists
            if (!departmentsMap.Any())
            {
                var defaultDept = new Department { DepartmentName = "General Medicine", DepartmentAbbr = "GEN" };
                _context.Departments.Add(defaultDept);
                await _context.SaveChangesAsync();
                departmentsMap["General Medicine"] = defaultDept;
            }

            var defaultDepartmentId = departmentsMap.Values.First().DepartmentId;

            // ─────────────────────────────────────────────────────────────────
            // 2. MIGRATE DOCTORS
            // ─────────────────────────────────────────────────────────────────
            var docSummary = new MigrationEntitySummary { EntityName = "Doctors" };
            result.EntitySummaries.Add(docSummary);

            var doctorsMap = (await _context.Doctors.Include(d => d.Department).ToListAsync())
                .ToDictionary(d => d.DoctorName.Trim(), d => d, StringComparer.OrdinalIgnoreCase);

            var existingDocNumbers = (await _context.Doctors.Select(d => d.DoctorNumber).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var docSheet = FindSheet("Doctors", "Doctor", "الأطباء", "الاطباء");
            if (docSheet != null)
            {
                var headerMap = GetHeaderMap(docSheet.Row(1));
                var rows = docSheet.RowsUsed().Skip(1);

                var newDoctors = new List<Doctor>();

                foreach (var row in rows)
                {
                    docSummary.TotalFound++;

                    string docName = GetCellValue(row, headerMap, "DoctorName", "Name", "FullName", "اسم الطبيب");
                    string docNumber = GetCellValue(row, headerMap, "DoctorNumber", "DoctorNo", "DocNo", "DoctorId", "رقم الطبيب");
                    string spec = GetCellValue(row, headerMap, "Specialization", "Specialty", "التخصص");
                    string feeRaw = GetCellValue(row, headerMap, "ConsultationFee", "Fee", "Price", "سعر الكشف");
                    string deptName = GetCellValue(row, headerMap, "DepartmentName", "Department", "القسم");

                    if (string.IsNullOrWhiteSpace(docName))
                    {
                        docSummary.SkippedCount++;
                        docSummary.Errors.Add($"Row {row.RowNumber()}: Doctor Name is required.");
                        continue;
                    }

                    if (doctorsMap.ContainsKey(docName))
                    {
                        docSummary.SkippedCount++;
                        docSummary.Errors.Add($"Row {row.RowNumber()}: Doctor '{docName}' already registered in system.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(spec)) spec = "General Practice";

                    decimal fee = 0;
                    if (!string.IsNullOrWhiteSpace(feeRaw))
                    {
                        decimal.TryParse(feeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out fee);
                    }

                    int deptId = defaultDepartmentId;
                    if (!string.IsNullOrWhiteSpace(deptName))
                    {
                        if (departmentsMap.TryGetValue(deptName, out var matchedDept))
                        {
                            deptId = matchedDept.DepartmentId;
                        }
                        else
                        {
                            var newDept = new Department { DepartmentName = deptName, DepartmentAbbr = deptName.Length > 4 ? deptName.Substring(0, 4).ToUpper() : deptName.ToUpper() };
                            _context.Departments.Add(newDept);
                            await _context.SaveChangesAsync();
                            departmentsMap[deptName] = newDept;
                            deptId = newDept.DepartmentId;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(docNumber) || existingDocNumbers.Contains(docNumber))
                    {
                        docNumber = "DOC" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
                    }
                    if (docNumber.Length > 10) docNumber = docNumber.Substring(0, 10);

                    var doctor = new Doctor
                    {
                        DoctorName = docName,
                        DoctorNumber = docNumber,
                        Specialization = spec,
                        ConsultationFee = fee,
                        DepartmentId = deptId
                    };

                    newDoctors.Add(doctor);
                    doctorsMap[docName] = doctor;
                    existingDocNumbers.Add(docNumber);
                }

                if (newDoctors.Any())
                {
                    await _context.Doctors.AddRangeAsync(newDoctors);
                    await _context.SaveChangesAsync();
                    docSummary.ImportedCount = newDoctors.Count;
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // 3. MIGRATE PATIENTS
            // ─────────────────────────────────────────────────────────────────
            var patientSummary = new MigrationEntitySummary { EntityName = "Patients" };
            result.EntitySummaries.Add(patientSummary);

            var existingPatients = await _context.Patients.ToListAsync();
            var nationalIdMap = existingPatients
                .Where(p => !string.IsNullOrEmpty(p.NationalId))
                .ToDictionary(p => p.NationalId.Trim(), p => p, StringComparer.OrdinalIgnoreCase);

            var phoneMap = existingPatients
                .Where(p => !string.IsNullOrEmpty(p.PhoneNumber))
                .ToDictionary(p => p.PhoneNumber.Trim(), p => p, StringComparer.OrdinalIgnoreCase);

            var patientSheet = FindSheet("Patients", "Patient", "المرضى", "مرضى");
            if (patientSheet != null)
            {
                var headerMap = GetHeaderMap(patientSheet.Row(1));
                var rows = patientSheet.RowsUsed().Skip(1);

                var newPatients = new List<Patient>();

                foreach (var row in rows)
                {
                    patientSummary.TotalFound++;

                    string fullName = GetCellValue(row, headerMap, "FullName", "PatientName", "Name", "اسم المريض");
                    string nationalId = GetCellValue(row, headerMap, "NationalID", "NationalId", "رقم الهوية");
                    string phone = GetCellValue(row, headerMap, "PhoneNumber", "Phone", "رقم الهاتف");
                    string dobRaw = GetCellValue(row, headerMap, "DateOfBirth", "DOB", "تاريخ الميلاد");
                    string bloodType = GetCellValue(row, headerMap, "BloodType", "فصيلة الدم");
                    string allergies = GetCellValue(row, headerMap, "Allergies", "الحساسية");
                    string chronicDiseases = GetCellValue(row, headerMap, "ChronicDiseases", "الأمراض المزمنة");
                    string notes = GetCellValue(row, headerMap, "Notes", "ملاحظات");

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        patientSummary.SkippedCount++;
                        patientSummary.Errors.Add($"Row {row.RowNumber()}: Full Name is required.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(nationalId))
                    {
                        patientSummary.SkippedCount++;
                        patientSummary.Errors.Add($"Row {row.RowNumber()} ({fullName}): National ID is required.");
                        continue;
                    }

                    if (nationalIdMap.ContainsKey(nationalId))
                    {
                        patientSummary.SkippedCount++;
                        patientSummary.Errors.Add($"Row {row.RowNumber()} ({fullName}): Patient with National ID '{nationalId}' already exists.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        phone = "0500000000";
                    }

                    DateTime dob = DateTime.Today.AddYears(-30);
                    if (!string.IsNullOrWhiteSpace(dobRaw))
                    {
                        DateTime.TryParse(dobRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out dob);
                    }

                    if (!string.IsNullOrWhiteSpace(bloodType) && bloodType.Length > 5)
                        bloodType = bloodType.Substring(0, 5);

                    var patient = new Patient
                    {
                        PatientName = fullName,
                        NationalId = nationalId,
                        PhoneNumber = phone,
                        DOB = dob,
                        BloodType = string.IsNullOrWhiteSpace(bloodType) ? null : bloodType,
                        Allergies = string.IsNullOrWhiteSpace(allergies) ? null : allergies,
                        ChronicDiseases = string.IsNullOrWhiteSpace(chronicDiseases) ? null : chronicDiseases,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                    };

                    newPatients.Add(patient);
                    nationalIdMap[nationalId] = patient;
                    if (!phoneMap.ContainsKey(phone)) phoneMap[phone] = patient;
                }

                if (newPatients.Any())
                {
                    await _context.Patients.AddRangeAsync(newPatients);
                    await _context.SaveChangesAsync();
                    patientSummary.ImportedCount = newPatients.Count;
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // 4. MIGRATE APPOINTMENTS (with Smart FK Resolution)
            // ─────────────────────────────────────────────────────────────────
            var apptSummary = new MigrationEntitySummary { EntityName = "Appointments" };
            result.EntitySummaries.Add(apptSummary);

            var allDoctors = await _context.Doctors.ToListAsync();
            var allPatients = await _context.Patients.ToListAsync();

            var apptSheet = FindSheet("Appointments", "Appointment", "المواعيد", "مواعيد");
            var createdAppointments = new List<Appointment>();

            if (apptSheet != null)
            {
                var headerMap = GetHeaderMap(apptSheet.Row(1));
                var rows = apptSheet.RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    apptSummary.TotalFound++;

                    string patientIdentifier = GetCellValue(row, headerMap, "PatientNationalID", "PatientNationalId", "NationalID", "PatientPhone", "PatientName", "رقم الهوية", "المريض");
                    string doctorIdentifier = GetCellValue(row, headerMap, "DoctorNameOrNumber", "DoctorNumber", "DoctorName", "DoctorID", "الطبيب");
                    string dateRaw = GetCellValue(row, headerMap, "AppointmentDate", "Date", "تاريخ الموعد");
                    string timeRaw = GetCellValue(row, headerMap, "AppointmentTime", "Time", "وقت الموعد");
                    string status = GetCellValue(row, headerMap, "Status", "الحالة");
                    string notes = GetCellValue(row, headerMap, "Notes", "ملاحظات");

                    if (string.IsNullOrWhiteSpace(patientIdentifier) && string.IsNullOrWhiteSpace(dateRaw))
                    {
                        apptSummary.SkippedCount++;
                        continue;
                    }

                    // 1. Resolve Patient
                    var matchedPatient = allPatients.FirstOrDefault(p =>
                        string.Equals(p.NationalId, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.PhoneNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.PatientName, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.PatientNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase));

                    if (matchedPatient == null)
                    {
                        // Auto-create lightweight patient so appointment is not lost!
                        matchedPatient = new Patient
                        {
                            PatientName = !string.IsNullOrWhiteSpace(patientIdentifier) ? patientIdentifier : "Migrated Patient",
                            NationalId = (patientIdentifier != null && patientIdentifier.Length == 10) ? patientIdentifier : "10" + Guid.NewGuid().ToString("N").Substring(0, 8),
                            PhoneNumber = "0500000000",
                            DOB = DateTime.Today.AddYears(-25)
                        };
                        _context.Patients.Add(matchedPatient);
                        await _context.SaveChangesAsync();
                        allPatients.Add(matchedPatient);
                    }

                    // 2. Resolve Doctor
                    var matchedDoctor = allDoctors.FirstOrDefault(d =>
                        string.Equals(d.DoctorNumber, doctorIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.DoctorName, doctorIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        d.DoctorName.Contains(doctorIdentifier ?? "", StringComparison.OrdinalIgnoreCase)) ?? allDoctors.FirstOrDefault();

                    if (matchedDoctor == null)
                    {
                        apptSummary.SkippedCount++;
                        apptSummary.Errors.Add($"Row {row.RowNumber()}: No doctors available to assign appointment.");
                        continue;
                    }

                    // 3. Parse Date & Time
                    DateTime apptDate = DateTime.Today;
                    if (!string.IsNullOrWhiteSpace(dateRaw))
                    {
                        DateTime.TryParse(dateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out apptDate);
                    }

                    TimeSpan apptTime = new TimeSpan(9, 0, 0);
                    if (!string.IsNullOrWhiteSpace(timeRaw))
                    {
                        if (!TimeSpan.TryParse(timeRaw, CultureInfo.InvariantCulture, out apptTime))
                        {
                            if (DateTime.TryParse(timeRaw, out DateTime dtTime))
                                apptTime = dtTime.TimeOfDay;
                        }
                    }

                    var appt = new Appointment
                    {
                        PatientId = matchedPatient.PatientId,
                        DoctorId = matchedDoctor.DoctorId,
                        AppointmentDate = apptDate,
                        AppointmentTime = apptTime,
                        Status = string.IsNullOrWhiteSpace(status) ? "Pending" : status,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                    };

                    createdAppointments.Add(appt);
                }

                if (createdAppointments.Any())
                {
                    await _context.Appointments.AddRangeAsync(createdAppointments);
                    await _context.SaveChangesAsync();
                    apptSummary.ImportedCount = createdAppointments.Count;
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // 5. MIGRATE TREATMENTS / MEDICAL RECORDS
            // ─────────────────────────────────────────────────────────────────
            var treatmentSummary = new MigrationEntitySummary { EntityName = "Treatments" };
            result.EntitySummaries.Add(treatmentSummary);

            var treatmentSheet = FindSheet("Treatments", "Treatment", "MedicalRecords", "العلاجات", "السجلات الطبية");
            if (treatmentSheet != null)
            {
                var headerMap = GetHeaderMap(treatmentSheet.Row(1));
                var rows = treatmentSheet.RowsUsed().Skip(1);

                var newTreatments = new List<Treatment>();

                var allAppts = await _context.Appointments.Include(a => a.Patient).ToListAsync();

                foreach (var row in rows)
                {
                    treatmentSummary.TotalFound++;

                    string patientIdentifier = GetCellValue(row, headerMap, "PatientNationalID", "NationalID", "PatientPhone", "PatientName", "رقم الهوية", "المريض");
                    string dateRaw = GetCellValue(row, headerMap, "TreatmentDate", "Date", "التاريخ");
                    string desc = GetCellValue(row, headerMap, "TreatmentDesc", "Description", "العلاج", "الوصف");
                    string diag = GetCellValue(row, headerMap, "Diagnosis", "التشخيص");
                    string rx = GetCellValue(row, headerMap, "PrescriptionNotes", "Prescription", "الوصفة", "الأدوية");
                    string costRaw = GetCellValue(row, headerMap, "TreatmentCost", "Cost", "Price", "التكلفة");

                    if (string.IsNullOrWhiteSpace(desc)) desc = "General Clinical Treatment";

                    decimal cost = 0;
                    if (!string.IsNullOrWhiteSpace(costRaw))
                        decimal.TryParse(costRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out cost);

                    DateTime tDate = DateTime.Today;
                    if (!string.IsNullOrWhiteSpace(dateRaw))
                        DateTime.TryParse(dateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out tDate);

                    // Match or create appointment
                    var matchingAppt = allAppts.FirstOrDefault(a =>
                        a.Patient != null &&
                        (string.Equals(a.Patient.NationalId, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a.Patient.PhoneNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a.Patient.PatientName, patientIdentifier, StringComparison.OrdinalIgnoreCase)) &&
                        a.Treatment == null);

                    if (matchingAppt == null)
                    {
                        var patient = allPatients.FirstOrDefault(p =>
                            string.Equals(p.NationalId, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.PhoneNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase)) ?? allPatients.FirstOrDefault();

                        if (patient == null)
                        {
                            treatmentSummary.SkippedCount++;
                            treatmentSummary.Errors.Add($"Row {row.RowNumber()}: Could not resolve patient for treatment.");
                            continue;
                        }

                        var doctor = allDoctors.FirstOrDefault();
                        matchingAppt = new Appointment
                        {
                            PatientId = patient.PatientId,
                            DoctorId = doctor?.DoctorId ?? 1,
                            AppointmentDate = tDate,
                            AppointmentTime = new TimeSpan(10, 0, 0),
                            Status = "Completed"
                        };
                        _context.Appointments.Add(matchingAppt);
                        await _context.SaveChangesAsync();
                        allAppts.Add(matchingAppt);
                    }

                    var treatment = new Treatment
                    {
                        AppointmentId = matchingAppt.AppointmentId,
                        TreatmentDate = tDate,
                        TreatmentDesc = desc.Length > 200 ? desc.Substring(0, 200) : desc,
                        Diagnosis = string.IsNullOrWhiteSpace(diag) ? null : diag,
                        PrescriptionNotes = string.IsNullOrWhiteSpace(rx) ? null : rx,
                        TreatmentCost = cost
                    };

                    newTreatments.Add(treatment);
                }

                if (newTreatments.Any())
                {
                    await _context.Treatments.AddRangeAsync(newTreatments);
                    await _context.SaveChangesAsync();
                    treatmentSummary.ImportedCount = newTreatments.Count;
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // 6. MIGRATE INVOICES
            // ─────────────────────────────────────────────────────────────────
            var invoiceSummary = new MigrationEntitySummary { EntityName = "Invoices" };
            result.EntitySummaries.Add(invoiceSummary);

            var invoiceSheet = FindSheet("Invoices", "Invoice", "Billing", "الفواتير", "فواتير");
            if (invoiceSheet != null)
            {
                var headerMap = GetHeaderMap(invoiceSheet.Row(1));
                var rows = invoiceSheet.RowsUsed().Skip(1);

                var newInvoices = new List<Invoice>();
                var existingInvoiceNumbers = (await _context.Invoices.Select(i => i.InvoiceNumber).ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var allTreatments = await _context.Treatments.Include(t => t.Appointment).ToListAsync();

                foreach (var row in rows)
                {
                    invoiceSummary.TotalFound++;

                    string invNum = GetCellValue(row, headerMap, "InvoiceNumber", "InvoiceNo", "رقم الفاتورة");
                    string patientIdentifier = GetCellValue(row, headerMap, "PatientNationalID", "NationalID", "PatientPhone", "رقم الهوية", "المريض");
                    string dateRaw = GetCellValue(row, headerMap, "InvoiceDate", "Date", "تاريخ الفاتورة");
                    string amountRaw = GetCellValue(row, headerMap, "Amount", "المبلغ", "القيمة");
                    string discountRaw = GetCellValue(row, headerMap, "Discount", "الخصم");
                    string taxRaw = GetCellValue(row, headerMap, "Tax", "الضريبة");
                    string netRaw = GetCellValue(row, headerMap, "NetAmount", "Net", "الصافي");
                    string status = GetCellValue(row, headerMap, "Status", "الحالة");

                    if (string.IsNullOrWhiteSpace(invNum) || existingInvoiceNumbers.Contains(invNum))
                    {
                        invNum = "INV-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                    }

                    var patient = allPatients.FirstOrDefault(p =>
                        string.Equals(p.NationalId, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.PhoneNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.PatientName, patientIdentifier, StringComparison.OrdinalIgnoreCase)) ?? allPatients.FirstOrDefault();

                    if (patient == null)
                    {
                        invoiceSummary.SkippedCount++;
                        invoiceSummary.Errors.Add($"Row {row.RowNumber()}: No patient found for invoice.");
                        continue;
                    }

                    // Find treatment or create default
                    var treatment = allTreatments.FirstOrDefault(t => t.Appointment != null && t.Appointment.PatientId == patient.PatientId) ?? allTreatments.FirstOrDefault();

                    if (treatment == null)
                    {
                        var appt = new Appointment { PatientId = patient.PatientId, DoctorId = allDoctors.FirstOrDefault()?.DoctorId ?? 1, AppointmentDate = DateTime.Today, AppointmentTime = new TimeSpan(9, 0, 0), Status = "Completed" };
                        _context.Appointments.Add(appt);
                        await _context.SaveChangesAsync();

                        treatment = new Treatment { AppointmentId = appt.AppointmentId, TreatmentDate = DateTime.Today, TreatmentDesc = "Consultation & Services", TreatmentCost = 100.00m };
                        _context.Treatments.Add(treatment);
                        await _context.SaveChangesAsync();
                        allTreatments.Add(treatment);
                    }

                    decimal amount = 0, discount = 0, tax = 0, net = 0;
                    decimal.TryParse(amountRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
                    decimal.TryParse(discountRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out discount);
                    decimal.TryParse(taxRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out tax);
                    if (!decimal.TryParse(netRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out net) || net == 0)
                    {
                        net = (amount - discount) + tax;
                    }

                    DateTime invDate = DateTime.Today;
                    if (!string.IsNullOrWhiteSpace(dateRaw))
                        DateTime.TryParse(dateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out invDate);

                    var inv = new Invoice
                    {
                        InvoiceNumber = invNum,
                        InvoiceDate = invDate,
                        Amount = amount,
                        Discount = discount,
                        Tax = tax,
                        NetAmount = net,
                        Status = string.IsNullOrWhiteSpace(status) ? "Paid" : status,
                        PatientId = patient.PatientId,
                        TreatmentId = treatment.TreatmentId
                    };

                    newInvoices.Add(inv);
                    existingInvoiceNumbers.Add(invNum);
                }

                if (newInvoices.Any())
                {
                    await _context.Invoices.AddRangeAsync(newInvoices);
                    await _context.SaveChangesAsync();
                    invoiceSummary.ImportedCount = newInvoices.Count;
                }
            }

            result.GeneralMessages.Add($"System migration completed. Successfully imported {result.TotalImported} entities across {result.EntitySummaries.Count(s => s.ImportedCount > 0)} module(s).");

            return result;
        }

        #endregion

        #region Full System Backup Export (.xlsx)

        /// <summary>
        /// Generates a comprehensive full-system multi-sheet Excel backup file.
        /// </summary>
        public async Task<byte[]> ExportFullSystemBackupExcelAsync()
        {
            using var workbook = new XLWorkbook();

            // Helper for header styling
            void SetupSheetHeaders(IXLWorksheet ws, string[] headers, XLColor color)
            {
                for (int col = 1; col <= headers.Length; col++)
                {
                    var cell = ws.Cell(1, col);
                    cell.Value = headers[col - 1];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = color;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                ws.Row(1).Height = 26;
            }

            // 1. Departments
            var wsDepts = workbook.Worksheets.Add("Departments");
            string[] deptsHdrs = { "DepartmentID", "DepartmentName", "DepartmentAbbr" };
            SetupSheetHeaders(wsDepts, deptsHdrs, XLColor.FromHtml("#2563eb"));
            var depts = await _context.Departments.AsNoTracking().ToListAsync();
            for (int i = 0; i < depts.Count; i++)
            {
                wsDepts.Cell(i + 2, 1).Value = depts[i].DepartmentId;
                wsDepts.Cell(i + 2, 2).Value = depts[i].DepartmentName;
                wsDepts.Cell(i + 2, 3).Value = depts[i].DepartmentAbbr;
            }
            wsDepts.Columns().AdjustToContents(15, 45);

            // 2. Doctors
            var wsDocs = workbook.Worksheets.Add("Doctors");
            string[] docsHdrs = { "DoctorID", "DoctorNumber", "DoctorName", "Specialization", "ConsultationFee", "DepartmentName" };
            SetupSheetHeaders(wsDocs, docsHdrs, XLColor.FromHtml("#059669"));
            var docs = await _context.Doctors.Include(d => d.Department).AsNoTracking().ToListAsync();
            for (int i = 0; i < docs.Count; i++)
            {
                wsDocs.Cell(i + 2, 1).Value = docs[i].DoctorId;
                wsDocs.Cell(i + 2, 2).Value = docs[i].DoctorNumber;
                wsDocs.Cell(i + 2, 3).Value = docs[i].DoctorName;
                wsDocs.Cell(i + 2, 4).Value = docs[i].Specialization;
                wsDocs.Cell(i + 2, 5).Value = (double)docs[i].ConsultationFee;
                wsDocs.Cell(i + 2, 6).Value = docs[i].Department?.DepartmentName;
            }
            wsDocs.Columns().AdjustToContents(15, 45);

            // 3. Patients
            var wsPatients = workbook.Worksheets.Add("Patients");
            string[] patientsHdrs = { "PatientID", "FullName", "NationalID", "PhoneNumber", "DateOfBirth", "BloodType", "Allergies", "ChronicDiseases", "Notes" };
            SetupSheetHeaders(wsPatients, patientsHdrs, XLColor.FromHtml("#4f46e5"));
            var patients = await _context.Patients.AsNoTracking().ToListAsync();
            for (int i = 0; i < patients.Count; i++)
            {
                wsPatients.Cell(i + 2, 1).Value = patients[i].PatientNumber ?? $"P{patients[i].PatientId}";
                wsPatients.Cell(i + 2, 2).Value = patients[i].PatientName;
                wsPatients.Cell(i + 2, 3).Value = patients[i].NationalId;
                wsPatients.Cell(i + 2, 4).Value = patients[i].PhoneNumber;
                wsPatients.Cell(i + 2, 5).Value = patients[i].DOB.ToString("yyyy-MM-dd");
                wsPatients.Cell(i + 2, 6).Value = patients[i].BloodType;
                wsPatients.Cell(i + 2, 7).Value = patients[i].Allergies;
                wsPatients.Cell(i + 2, 8).Value = patients[i].ChronicDiseases;
                wsPatients.Cell(i + 2, 9).Value = patients[i].Notes;
            }
            wsPatients.Columns().AdjustToContents(15, 45);

            // 4. Appointments
            var wsAppts = workbook.Worksheets.Add("Appointments");
            string[] apptsHdrs = { "AppointmentID", "PatientName", "PatientNationalID", "DoctorName", "AppointmentDate", "AppointmentTime", "Status", "Notes" };
            SetupSheetHeaders(wsAppts, apptsHdrs, XLColor.FromHtml("#d97706"));
            var appts = await _context.Appointments.Include(a => a.Patient).Include(a => a.Doctor).AsNoTracking().ToListAsync();
            for (int i = 0; i < appts.Count; i++)
            {
                wsAppts.Cell(i + 2, 1).Value = appts[i].AppointmentId;
                wsAppts.Cell(i + 2, 2).Value = appts[i].Patient?.PatientName;
                wsAppts.Cell(i + 2, 3).Value = appts[i].Patient?.NationalId;
                wsAppts.Cell(i + 2, 4).Value = appts[i].Doctor?.DoctorName;
                wsAppts.Cell(i + 2, 5).Value = appts[i].AppointmentDate.ToString("yyyy-MM-dd");
                wsAppts.Cell(i + 2, 6).Value = appts[i].AppointmentTime.ToString(@"hh\:mm");
                wsAppts.Cell(i + 2, 7).Value = appts[i].Status;
                wsAppts.Cell(i + 2, 8).Value = appts[i].Notes;
            }
            wsAppts.Columns().AdjustToContents(15, 45);

            // 5. Treatments
            var wsTreatments = workbook.Worksheets.Add("Treatments");
            string[] treatmentsHdrs = { "TreatmentID", "PatientName", "PatientNationalID", "TreatmentDate", "TreatmentDesc", "Diagnosis", "PrescriptionNotes", "TreatmentCost" };
            SetupSheetHeaders(wsTreatments, treatmentsHdrs, XLColor.FromHtml("#7c3aed"));
            var treatments = await _context.Treatments.Include(t => t.Appointment).ThenInclude(a => a.Patient).AsNoTracking().ToListAsync();
            for (int i = 0; i < treatments.Count; i++)
            {
                wsTreatments.Cell(i + 2, 1).Value = treatments[i].TreatmentId;
                wsTreatments.Cell(i + 2, 2).Value = treatments[i].Appointment?.Patient?.PatientName;
                wsTreatments.Cell(i + 2, 3).Value = treatments[i].Appointment?.Patient?.NationalId;
                wsTreatments.Cell(i + 2, 4).Value = treatments[i].TreatmentDate.ToString("yyyy-MM-dd");
                wsTreatments.Cell(i + 2, 5).Value = treatments[i].TreatmentDesc;
                wsTreatments.Cell(i + 2, 6).Value = treatments[i].Diagnosis;
                wsTreatments.Cell(i + 2, 7).Value = treatments[i].PrescriptionNotes;
                wsTreatments.Cell(i + 2, 8).Value = (double)treatments[i].TreatmentCost;
            }
            wsTreatments.Columns().AdjustToContents(15, 45);

            // 6. Invoices
            var wsInvoices = workbook.Worksheets.Add("Invoices");
            string[] invoicesHdrs = { "InvoiceNumber", "PatientName", "PatientNationalID", "InvoiceDate", "Amount", "Discount", "Tax", "NetAmount", "Status" };
            SetupSheetHeaders(wsInvoices, invoicesHdrs, XLColor.FromHtml("#0891b2"));
            var invoices = await _context.Invoices.Include(i => i.Patient).AsNoTracking().ToListAsync();
            for (int i = 0; i < invoices.Count; i++)
            {
                wsInvoices.Cell(i + 2, 1).Value = invoices[i].InvoiceNumber;
                wsInvoices.Cell(i + 2, 2).Value = invoices[i].Patient?.PatientName;
                wsInvoices.Cell(i + 2, 3).Value = invoices[i].Patient?.NationalId;
                wsInvoices.Cell(i + 2, 4).Value = invoices[i].InvoiceDate.ToString("yyyy-MM-dd");
                wsInvoices.Cell(i + 2, 5).Value = (double)invoices[i].Amount;
                wsInvoices.Cell(i + 2, 6).Value = (double)invoices[i].Discount;
                wsInvoices.Cell(i + 2, 7).Value = (double)invoices[i].Tax;
                wsInvoices.Cell(i + 2, 8).Value = (double)invoices[i].NetAmount;
                wsInvoices.Cell(i + 2, 9).Value = invoices[i].Status;
            }
            wsInvoices.Columns().AdjustToContents(15, 45);

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }

        #endregion

        #region Helper Methods

        private static Dictionary<string, int> GetHeaderMap(IXLRow headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int col = 1;
            foreach (var cell in headerRow.Cells())
            {
                string text = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    map[NormalizeHeader(text)] = col;
                }
                col++;
            }
            return map;
        }

        private static string GetCellValue(IXLRow row, Dictionary<string, int> headerMap, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                var normalized = NormalizeHeader(alias);
                if (headerMap.TryGetValue(normalized, out int colIndex))
                {
                    var cell = row.Cell(colIndex);
                    return cell.GetString().Trim();
                }
            }
            return string.Empty;
        }

        private static string NormalizeHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header)) return string.Empty;
            return Regex.Replace(header, @"[\s_\-\(\)]+", string.Empty).ToLowerInvariant();
        }

        #endregion
    }
}
