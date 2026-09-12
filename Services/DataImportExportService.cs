using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class DataImportExportService : IDataImportExportService
    {
        private readonly ClinicDbContext _context;

        public DataImportExportService(ClinicDbContext context)
        {
            _context = context;
        }

        #region Patient Methods

        /// <summary>
        /// Generates a downloadable sample CSV template with clear headers and representative example rows.
        /// Uses UTF-8 BOM encoding for seamless Arabic/special character rendering in Excel.
        /// </summary>
        public byte[] GeneratePatientTemplateCsv()
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // Write Header Row
                csv.WriteField("NationalID");
                csv.WriteField("FullName");
                csv.WriteField("Phone");
                csv.WriteField("DateOfBirth");
                csv.WriteField("Gender");
                csv.NextRecord();

                // Example Row 1
                csv.WriteField("1098765432");
                csv.WriteField("Ahmed Mohamed Ali");
                csv.WriteField("0501234567");
                csv.WriteField("1992-05-15");
                csv.WriteField("Male");
                csv.NextRecord();

                // Example Row 2
                csv.WriteField("1087654321");
                csv.WriteField("Sara Salem Omar");
                csv.WriteField("0559876543");
                csv.WriteField("1988-11-20");
                csv.WriteField("Female");
                csv.NextRecord();
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Exports all existing patients in the database to a CSV file with full details.
        /// </summary>
        public async Task<byte[]> ExportPatientsToCsvAsync()
        {
            var patients = await _context.Patients
                .AsNoTracking()
                .OrderBy(p => p.PatientId)
                .ToListAsync();

            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // Write headers
                csv.WriteField("PatientID");
                csv.WriteField("FullName");
                csv.WriteField("NationalID");
                csv.WriteField("PhoneNumber");
                csv.WriteField("DateOfBirth");
                csv.WriteField("BloodType");
                csv.WriteField("Allergies");
                csv.WriteField("ChronicDiseases");
                csv.WriteField("Notes");
                csv.NextRecord();

                foreach (var p in patients)
                {
                    csv.WriteField(p.PatientNumber ?? $"P{p.PatientId}");
                    csv.WriteField(p.PatientName ?? string.Empty);
                    csv.WriteField(p.NationalId ?? string.Empty);
                    csv.WriteField(p.PhoneNumber ?? string.Empty);
                    csv.WriteField(p.DOB != default ? p.DOB.ToString("yyyy-MM-dd") : string.Empty);
                    csv.WriteField(p.BloodType ?? string.Empty);
                    csv.WriteField(p.Allergies ?? string.Empty);
                    csv.WriteField(p.ChronicDiseases ?? string.Empty);
                    csv.WriteField(p.Notes ?? string.Empty);
                    csv.NextRecord();
                }
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Parses CSV file stream, validates patient fields, performs bulk database insertion, and logs errors for invalid rows.
        /// </summary>
        public async Task<ImportResult<Patient>> ImportPatientsFromCsvAsync(Stream stream)
        {
            var result = new ImportResult<Patient>();
            var validPatients = new List<Patient>();

            // Pre-load existing National IDs to prevent duplicate database constraints
            var nationalIdsList = await _context.Patients
                .AsNoTracking()
                .Select(p => p.NationalId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToListAsync();

            var existingNationalIds = new HashSet<string>(nationalIdsList, StringComparer.OrdinalIgnoreCase);

            var batchNationalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null
            };

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            if (!await csv.ReadAsync() || !csv.ReadHeader())
            {
                result.ErrorMessages.Add("The uploaded CSV file is empty or missing a valid header row.");
                return result;
            }

            var headers = csv.HeaderRecord;
            if (headers == null || headers.Length == 0)
            {
                result.ErrorMessages.Add("Could not detect column headers in the CSV file.");
                return result;
            }

            int rowNumber = 1; // Header is row 1

            while (await csv.ReadAsync())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    // Helper to get field value matching several possible header aliases (English / Arabic)
                    string GetValue(params string[] aliases)
                    {
                        foreach (var alias in aliases)
                        {
                            if (csv.TryGetField(alias, out string? value) && !string.IsNullOrWhiteSpace(value))
                                return value.Trim();

                            // Also try case-insensitive and stripped match
                            var normalizedAlias = NormalizeHeader(alias);
                            foreach (var header in headers)
                            {
                                if (NormalizeHeader(header) == normalizedAlias)
                                {
                                    if (csv.TryGetField(header, out string? matchVal) && !string.IsNullOrWhiteSpace(matchVal))
                                        return matchVal.Trim();
                                }
                            }
                        }
                        return string.Empty;
                    }

                    string fullName = GetValue("FullName", "PatientName", "Name", "Full Name", "Patient Name", "اسم المريض", "الاسم");
                    string nationalId = GetValue("NationalID", "NationalId", "National ID", "NationalIdNumber", "رقم الهوية", "الهوية");
                    string phoneNumber = GetValue("PhoneNumber", "Phone", "Phone Number", "Mobile", "رقم الهاتف", "الهاتف", "الجوال");
                    string dobRaw = GetValue("DateOfBirth", "DOB", "BirthDate", "Date of Birth", "Birth Date", "تاريخ الميلاد");
                    string bloodType = GetValue("BloodType", "Blood Type", "فصيلة الدم");
                    string allergies = GetValue("Allergies", "Allergy", "الحساسية");
                    string chronicDiseases = GetValue("ChronicDiseases", "Chronic Diseases", "ChronicDisease", "الأمراض المزمنة");
                    string gender = GetValue("Gender", "Sex", "الجنس");
                    string notes = GetValue("Notes", "MedicalNotes", "Medical Notes", "ملاحظات", "الملاحظات");

                    if (!string.IsNullOrWhiteSpace(gender))
                    {
                        notes = string.IsNullOrWhiteSpace(notes) ? $"الجنس: {gender}" : $"{notes} | الجنس: {gender}";
                    }

                    // If entire row is blank, skip without counting as a failed patient
                    if (string.IsNullOrWhiteSpace(fullName) &&
                        string.IsNullOrWhiteSpace(nationalId) &&
                        string.IsNullOrWhiteSpace(phoneNumber) &&
                        string.IsNullOrWhiteSpace(dobRaw))
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: Skipped empty row.");
                        continue;
                    }

                    var rowErrors = new List<string>();

                    // 1. Validate Full Name
                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        rowErrors.Add("Full Name is required.");
                    }
                    else if (fullName.Length < 3 || fullName.Length > 150)
                    {
                        rowErrors.Add($"Full Name must be between 3 and 150 characters (Current: '{fullName}').");
                    }

                    // 2. Validate National ID
                    if (string.IsNullOrWhiteSpace(nationalId))
                    {
                        rowErrors.Add("National ID is required.");
                    }
                    else if (nationalId.Length != 10)
                    {
                        rowErrors.Add($"National ID must be exactly 10 characters (Current: '{nationalId}').");
                    }
                    else if (existingNationalIds.Contains(nationalId))
                    {
                        rowErrors.Add($"National ID '{nationalId}' already exists in database.");
                    }
                    else if (batchNationalIds.Contains(nationalId))
                    {
                        rowErrors.Add($"Duplicate National ID '{nationalId}' found in this CSV batch.");
                    }

                    // 3. Validate Phone Number
                    if (string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        rowErrors.Add("Phone Number is required.");
                    }
                    else if (phoneNumber.Length < 9 || phoneNumber.Length > 15)
                    {
                        rowErrors.Add($"Phone Number must be between 9 and 15 digits (Current: '{phoneNumber}').");
                    }

                    // 4. Validate Date of Birth
                    DateTime dob = default;
                    if (string.IsNullOrWhiteSpace(dobRaw))
                    {
                        rowErrors.Add("Date of Birth is required.");
                    }
                    else
                    {
                        string[] formats = {
                            "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd",
                            "dd-MM-yyyy", "MM-dd-yyyy", "d/M/yyyy", "M/d/yyyy",
                            "yyyy.MM.dd", "dd.MM.yyyy", "yyyyMMdd"
                        };

                        if (!DateTime.TryParseExact(dobRaw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dob) &&
                            !DateTime.TryParse(dobRaw, CultureInfo.CurrentCulture, DateTimeStyles.None, out dob))
                        {
                            rowErrors.Add($"Date of Birth '{dobRaw}' is invalid. Please use format YYYY-MM-DD or DD/MM/YYYY.");
                        }
                        else if (dob > DateTime.Today)
                        {
                            rowErrors.Add($"Date of Birth cannot be in the future ('{dobRaw}').");
                        }
                        else if (dob < DateTime.Today.AddYears(-130))
                        {
                            rowErrors.Add($"Date of Birth '{dobRaw}' is unrealistically far in the past.");
                        }
                    }

                    // 5. Validate Blood Type (Optional, max 5 chars)
                    if (!string.IsNullOrWhiteSpace(bloodType) && bloodType.Length > 5)
                    {
                        bloodType = bloodType.Substring(0, 5);
                    }

                    if (rowErrors.Any())
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber} ({fullName ?? "Unknown"}): {string.Join(" | ", rowErrors)}");
                        continue;
                    }

                    // Build valid patient entity
                    var patient = new Patient
                    {
                        PatientName = fullName,
                        NationalId = nationalId,
                        PhoneNumber = phoneNumber,
                        DOB = dob,
                        BloodType = string.IsNullOrWhiteSpace(bloodType) ? null : bloodType,
                        Allergies = string.IsNullOrWhiteSpace(allergies) ? null : allergies,
                        ChronicDiseases = string.IsNullOrWhiteSpace(chronicDiseases) ? null : chronicDiseases,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                    };

                    validPatients.Add(patient);
                    batchNationalIds.Add(nationalId);
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.ErrorMessages.Add($"Row {rowNumber}: Unexpected parse error: {ex.Message}");
                }
            }

            // Bulk Database Insertion using EF Core
            if (validPatients.Any())
            {
                await _context.Patients.AddRangeAsync(validPatients);
                await _context.SaveChangesAsync();

                result.ImportedCount = validPatients.Count;
                result.ImportedRecords = validPatients;
            }

            return result;
        }

        #endregion

        #region Doctor Methods

        /// <summary>
        /// Generates a downloadable sample CSV template with clear headers and example rows for Doctors.
        /// </summary>
        public byte[] GenerateDoctorTemplateCsv()
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("FullName");
                csv.WriteField("Specialization");
                csv.WriteField("Phone");
                csv.WriteField("DepartmentName");
                csv.NextRecord();

                csv.WriteField("Dr. Sarah Ahmed");
                csv.WriteField("Cardiology");
                csv.WriteField("0501122334");
                csv.WriteField("Cardiology");
                csv.NextRecord();

                csv.WriteField("Dr. Mohamed Ali");
                csv.WriteField("Pediatrics");
                csv.WriteField("0559988776");
                csv.WriteField("Pediatrics");
                csv.NextRecord();
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Exports all doctors in the database to a CSV file.
        /// </summary>
        public async Task<byte[]> ExportDoctorsToCsvAsync()
        {
            var doctors = await _context.Doctors
                .Include(d => d.Department)
                .AsNoTracking()
                .OrderBy(d => d.DoctorId)
                .ToListAsync();

            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("DoctorID");
                csv.WriteField("DoctorNumber");
                csv.WriteField("DoctorName");
                csv.WriteField("Specialization");
                csv.WriteField("ConsultationFee");
                csv.WriteField("Department");
                csv.NextRecord();

                foreach (var d in doctors)
                {
                    csv.WriteField(d.DoctorId);
                    csv.WriteField(d.DoctorNumber);
                    csv.WriteField(d.DoctorName);
                    csv.WriteField(d.Specialization);
                    csv.WriteField(d.ConsultationFee.ToString("F2", CultureInfo.InvariantCulture));
                    csv.WriteField(d.Department?.DepartmentName ?? "General");
                    csv.NextRecord();
                }
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Imports doctors from CSV with validation and department resolution/auto-creation.
        /// </summary>
        public async Task<ImportResult<Doctor>> ImportDoctorsFromCsvAsync(Stream stream)
        {
            var result = new ImportResult<Doctor>();
            var validDoctors = new List<Doctor>();

            var existingDepartments = await _context.Departments.ToListAsync();
            var departmentsMap = existingDepartments.ToDictionary(d => d.DepartmentName.Trim(), d => d.DepartmentId, StringComparer.OrdinalIgnoreCase);

            var existingDoctorNumbers = (await _context.Doctors
                .AsNoTracking()
                .Select(d => d.DoctorNumber)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null
            };

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            if (!await csv.ReadAsync() || !csv.ReadHeader())
            {
                result.ErrorMessages.Add("The uploaded CSV file is empty or missing a valid header row.");
                return result;
            }

            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            int rowNumber = 1;

            while (await csv.ReadAsync())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    string GetValue(params string[] aliases)
                    {
                        foreach (var alias in aliases)
                        {
                            if (csv.TryGetField(alias, out string? value) && !string.IsNullOrWhiteSpace(value))
                                return value.Trim();

                            var normalizedAlias = NormalizeHeader(alias);
                            foreach (var header in headers)
                            {
                                if (NormalizeHeader(header) == normalizedAlias)
                                {
                                    if (csv.TryGetField(header, out string? matchVal) && !string.IsNullOrWhiteSpace(matchVal))
                                        return matchVal.Trim();
                                }
                            }
                        }
                        return string.Empty;
                    }

                    string doctorName = GetValue("FullName", "DoctorName", "Name", "Doctor", "اسم الطبيب", "الاسم", "الطبيب");
                    string doctorNumber = GetValue("DoctorNumber", "DoctorNo", "DocNo", "DoctorId", "رقم الطبيب", "الرقم الوظيفي");
                    string specialization = GetValue("Specialization", "Specialty", "التخصص");
                    string phone = GetValue("Phone", "DoctorPhone", "PhoneNumber", "Mobile", "رقم الهاتف", "الهاتف", "الجوال");
                    string feeRaw = GetValue("ConsultationFee", "Fee", "Price", "سعر الكشف", "رسوم الكشف");
                    string departmentName = GetValue("DepartmentName", "Department", "Dept", "القسم", "عيادة");

                    if (string.IsNullOrWhiteSpace(doctorName) && string.IsNullOrWhiteSpace(specialization))
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: Skipped empty row.");
                        continue;
                    }

                    var rowErrors = new List<string>();

                    if (string.IsNullOrWhiteSpace(doctorName))
                    {
                        rowErrors.Add("Doctor Name is required.");
                    }
                    else if (doctorName.Length < 3 || doctorName.Length > 150)
                    {
                        rowErrors.Add("Doctor Name must be between 3 and 150 characters.");
                    }

                    if (string.IsNullOrWhiteSpace(specialization))
                    {
                        specialization = "General Practice";
                    }

                    decimal fee = 0;
                    if (!string.IsNullOrWhiteSpace(feeRaw))
                    {
                        if (!decimal.TryParse(feeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out fee) &&
                            !decimal.TryParse(feeRaw, NumberStyles.Any, CultureInfo.CurrentCulture, out fee))
                        {
                            rowErrors.Add($"Invalid consultation fee '{feeRaw}'.");
                        }
                    }

                    // Resolve or create department
                    int deptId = 0;
                    if (string.IsNullOrWhiteSpace(departmentName))
                    {
                        departmentName = "General Medicine";
                    }

                    if (departmentsMap.TryGetValue(departmentName, out int existingDeptId))
                    {
                        deptId = existingDeptId;
                    }
                    else
                    {
                        var newDept = new Department { DepartmentName = departmentName, DepartmentAbbr = departmentName.Length > 4 ? departmentName.Substring(0, 4).ToUpper() : departmentName.ToUpper() };
                        _context.Departments.Add(newDept);
                        await _context.SaveChangesAsync();
                        departmentsMap[departmentName] = newDept.DepartmentId;
                        deptId = newDept.DepartmentId;
                    }

                    // Resolve DoctorNumber
                    if (string.IsNullOrWhiteSpace(doctorNumber))
                    {
                        doctorNumber = "DOC" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
                    }
                    else if (doctorNumber.Length > 10)
                    {
                        doctorNumber = doctorNumber.Substring(0, 10);
                    }

                    if (existingDoctorNumbers.Contains(doctorNumber))
                    {
                        doctorNumber = "DOC" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
                    }

                    if (rowErrors.Any())
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber} ({doctorName ?? "Unknown"}): {string.Join(" | ", rowErrors)}");
                        continue;
                    }

                    var doctor = new Doctor
                    {
                        DoctorName = doctorName,
                        DoctorNumber = doctorNumber,
                        Specialization = specialization,
                        DoctorPhone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                        ConsultationFee = fee,
                        DepartmentId = deptId
                    };

                    validDoctors.Add(doctor);
                    existingDoctorNumbers.Add(doctorNumber);
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.ErrorMessages.Add($"Row {rowNumber}: Unexpected parse error: {ex.Message}");
                }
            }

            if (validDoctors.Any())
            {
                await _context.Doctors.AddRangeAsync(validDoctors);
                await _context.SaveChangesAsync();

                result.ImportedCount = validDoctors.Count;
                result.ImportedRecords = validDoctors;
            }

            return result;
        }

        #endregion

        #region Appointment Methods

        /// <summary>
        /// Generates a downloadable sample CSV template for bulk appointment import.
        /// </summary>
        public byte[] GenerateAppointmentTemplateCsv()
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("PatientNationalID");
                csv.WriteField("DoctorName");
                csv.WriteField("AppointmentDate");
                csv.WriteField("AppointmentTime");
                csv.WriteField("Notes");
                csv.NextRecord();

                // Example Row 1
                csv.WriteField("1098765432");
                csv.WriteField("Dr. Khalid");
                csv.WriteField(DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"));
                csv.WriteField("09:30");
                csv.WriteField("General checkup appointment");
                csv.NextRecord();

                // Example Row 2
                csv.WriteField("1087654321");
                csv.WriteField("Dr. Sarah");
                csv.WriteField(DateTime.Today.AddDays(2).ToString("yyyy-MM-dd"));
                csv.WriteField("11:00");
                csv.WriteField("Follow-up consultation");
                csv.NextRecord();
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Exports all existing appointments to CSV.
        /// </summary>
        public async Task<byte[]> ExportAppointmentsToCsvAsync()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsNoTracking()
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .ToListAsync();

            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("AppointmentID");
                csv.WriteField("PatientName");
                csv.WriteField("PatientNationalID");
                csv.WriteField("PatientPhone");
                csv.WriteField("DoctorName");
                csv.WriteField("AppointmentDate");
                csv.WriteField("AppointmentTime");
                csv.WriteField("Status");
                csv.WriteField("Notes");
                csv.NextRecord();

                foreach (var a in appointments)
                {
                    csv.WriteField(a.AppointmentId);
                    csv.WriteField(a.Patient?.PatientName ?? string.Empty);
                    csv.WriteField(a.Patient?.NationalId ?? string.Empty);
                    csv.WriteField(a.Patient?.PhoneNumber ?? string.Empty);
                    csv.WriteField(a.Doctor?.DoctorName ?? string.Empty);
                    csv.WriteField(a.AppointmentDate.ToString("yyyy-MM-dd"));
                    csv.WriteField(a.AppointmentTime.ToString(@"hh\:mm"));
                    csv.WriteField(a.Status ?? "Pending");
                    csv.WriteField(a.Notes ?? string.Empty);
                    csv.NextRecord();
                }
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Imports appointments in bulk from CSV, validating patient & doctor existence, dates, and times.
        /// </summary>
        public async Task<ImportResult<Appointment>> ImportAppointmentsFromCsvAsync(Stream stream)
        {
            var result = new ImportResult<Appointment>();
            var validAppointments = new List<Appointment>();

            // Pre-load patients and doctors map
            var patients = await _context.Patients
                .AsNoTracking()
                .Select(p => new { p.PatientId, p.NationalId, p.PhoneNumber, p.PatientNumber })
                .ToListAsync();

            var doctors = await _context.Doctors
                .AsNoTracking()
                .Select(d => new { d.DoctorId, d.DoctorNumber, d.DoctorName })
                .ToListAsync();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null
            };

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            if (!await csv.ReadAsync() || !csv.ReadHeader())
            {
                result.ErrorMessages.Add("The uploaded appointments CSV file is empty or missing a header row.");
                return result;
            }

            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            int rowNumber = 1;

            while (await csv.ReadAsync())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    string GetValue(params string[] aliases)
                    {
                        foreach (var alias in aliases)
                        {
                            if (csv.TryGetField(alias, out string? value) && !string.IsNullOrWhiteSpace(value))
                                return value.Trim();

                            var normalizedAlias = NormalizeHeader(alias);
                            foreach (var header in headers)
                            {
                                if (NormalizeHeader(header) == normalizedAlias)
                                {
                                    if (csv.TryGetField(header, out string? matchVal) && !string.IsNullOrWhiteSpace(matchVal))
                                        return matchVal.Trim();
                                }
                            }
                        }
                        return string.Empty;
                    }

                    string patientIdentifier = GetValue("PatientNationalID", "PatientNationalId", "NationalID", "NationalId", "PatientID", "PatientNumber", "PatientPhone", "رقم الهوية", "المريض");
                    string doctorIdentifier = GetValue("DoctorIDOrNumber", "DoctorID", "DoctorId", "DoctorNumber", "DoctorName", "Doctor", "الطبيب", "رقم الطبيب");
                    string dateRaw = GetValue("AppointmentDate", "Date", "تاريخ الموعد", "التاريخ");
                    string timeRaw = GetValue("AppointmentTime", "Time", "وقت الموعد", "الوقت");
                    string status = GetValue("Status", "الحالة");
                    string notes = GetValue("Notes", "ملاحظات");

                    if (string.IsNullOrWhiteSpace(patientIdentifier) &&
                        string.IsNullOrWhiteSpace(doctorIdentifier) &&
                        string.IsNullOrWhiteSpace(dateRaw))
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: Skipped empty row.");
                        continue;
                    }

                    var rowErrors = new List<string>();

                    // 1. Resolve Patient
                    int matchedPatientId = 0;
                    if (string.IsNullOrWhiteSpace(patientIdentifier))
                    {
                        rowErrors.Add("Patient identifier (National ID / Patient Number / Phone) is required.");
                    }
                    else
                    {
                        var patientMatch = patients.FirstOrDefault(p =>
                            string.Equals(p.NationalId, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.PatientNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.PhoneNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            (int.TryParse(patientIdentifier, out int pid) && p.PatientId == pid));

                        if (patientMatch == null)
                        {
                            rowErrors.Add($"No patient found matching '{patientIdentifier}'.");
                        }
                        else
                        {
                            matchedPatientId = patientMatch.PatientId;
                        }
                    }

                    // 2. Resolve Doctor
                    int matchedDoctorId = 0;
                    if (string.IsNullOrWhiteSpace(doctorIdentifier))
                    {
                        rowErrors.Add("Doctor identifier (Doctor ID / Doctor Number / Doctor Name) is required.");
                    }
                    else
                    {
                        var doctorMatch = doctors.FirstOrDefault(d =>
                            string.Equals(d.DoctorNumber, doctorIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(d.DoctorName, doctorIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            (int.TryParse(doctorIdentifier, out int docId) && d.DoctorId == docId));

                        if (doctorMatch == null)
                        {
                            rowErrors.Add($"No doctor found matching '{doctorIdentifier}'.");
                        }
                        else
                        {
                            matchedDoctorId = doctorMatch.DoctorId;
                        }
                    }

                    // 3. Parse Date
                    DateTime apptDate = default;
                    if (string.IsNullOrWhiteSpace(dateRaw))
                    {
                        rowErrors.Add("Appointment Date is required.");
                    }
                    else
                    {
                        string[] formats = { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "dd-MM-yyyy", "d/M/yyyy" };
                        if (!DateTime.TryParseExact(dateRaw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out apptDate) &&
                            !DateTime.TryParse(dateRaw, CultureInfo.CurrentCulture, DateTimeStyles.None, out apptDate))
                        {
                            rowErrors.Add($"Appointment Date '{dateRaw}' is invalid (use YYYY-MM-DD).");
                        }
                        else if (apptDate.Date < DateTime.Today)
                        {
                            rowErrors.Add($"Appointment Date '{dateRaw}' cannot be in the past. Must be today or a future date.");
                        }
                    }

                    // 4. Parse Time
                    TimeSpan apptTime = default;
                    if (string.IsNullOrWhiteSpace(timeRaw))
                    {
                        rowErrors.Add("Appointment Time is required.");
                    }
                    else
                    {
                        if (!TimeSpan.TryParse(timeRaw, CultureInfo.InvariantCulture, out apptTime) &&
                            !DateTime.TryParse(timeRaw, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime parsedTime))
                        {
                            rowErrors.Add($"Appointment Time '{timeRaw}' is invalid (use HH:mm).");
                        }
                        else if (apptTime == default && DateTime.TryParse(timeRaw, out DateTime dtTime))
                        {
                            apptTime = dtTime.TimeOfDay;
                        }
                    }

                    // Check if slot is in the past:
                    var slotDt = apptDate.Date.Add(apptTime);
                    if (slotDt < DateTime.Now)
                    {
                        rowErrors.Add("Appointment slot cannot be scheduled in the past.");
                    }

                    // Check for active slot conflict for the doctor:
                    bool hasConflict = await _context.Appointments.AnyAsync(a => a.DoctorId == matchedDoctorId && a.AppointmentDate == apptDate.Date && a.AppointmentTime == apptTime && a.Status != "Cancelled")
                        || validAppointments.Any(a => a.DoctorId == matchedDoctorId && a.AppointmentDate == apptDate.Date && a.AppointmentTime == apptTime && a.Status != "Cancelled");

                    if (hasConflict)
                    {
                        rowErrors.Add($"Doctor already has an active appointment on {apptDate:yyyy-MM-dd} at {apptTime:hh\\:mm}.");
                    }

                    if (rowErrors.Any())
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: {string.Join(" | ", rowErrors)}");
                        continue;
                    }

                    var appt = new Appointment
                    {
                        PatientId = matchedPatientId,
                        DoctorId = matchedDoctorId,
                        AppointmentDate = apptDate,
                        AppointmentTime = apptTime,
                        Status = string.IsNullOrWhiteSpace(status) ? "Pending" : status,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                    };

                    validAppointments.Add(appt);
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.ErrorMessages.Add($"Row {rowNumber}: Unexpected parse error: {ex.Message}");
                }
            }

            if (validAppointments.Any())
            {
                await _context.Appointments.AddRangeAsync(validAppointments);
                await _context.SaveChangesAsync();

                result.ImportedCount = validAppointments.Count;
                result.ImportedRecords = validAppointments;
            }

            return result;
        }

        #endregion

        #region Invoice Methods

        /// <summary>
        /// Generates a downloadable sample CSV template for bulk invoice import.
        /// </summary>
        public byte[] GenerateInvoiceTemplateCsv()
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("InvoiceNumber");
                csv.WriteField("PatientNationalID");
                csv.WriteField("InvoiceDate");
                csv.WriteField("Amount");
                csv.WriteField("Discount");
                csv.WriteField("Tax");
                csv.WriteField("NetAmount");
                csv.WriteField("Status");
                csv.NextRecord();

                // Example Row 1
                csv.WriteField("INV-2026-001");
                csv.WriteField("1098765432");
                csv.WriteField(DateTime.Today.ToString("yyyy-MM-dd"));
                csv.WriteField("350.00");
                csv.WriteField("0.00");
                csv.WriteField("52.50");
                csv.WriteField("402.50");
                csv.WriteField("Paid");
                csv.NextRecord();

                // Example Row 2
                csv.WriteField("INV-2026-002");
                csv.WriteField("1087654321");
                csv.WriteField(DateTime.Today.ToString("yyyy-MM-dd"));
                csv.WriteField("200.00");
                csv.WriteField("20.00");
                csv.WriteField("27.00");
                csv.WriteField("207.00");
                csv.WriteField("Unpaid");
                csv.NextRecord();
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Exports all invoices to a downloadable CSV file.
        /// </summary>
        public async Task<byte[]> ExportInvoicesToCsvAsync()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Treatment)
                .AsNoTracking()
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("InvoiceNumber");
                csv.WriteField("PatientName");
                csv.WriteField("PatientNationalID");
                csv.WriteField("PatientPhone");
                csv.WriteField("InvoiceDate");
                csv.WriteField("Amount");
                csv.WriteField("Discount");
                csv.WriteField("Tax");
                csv.WriteField("NetAmount");
                csv.WriteField("Status");
                csv.NextRecord();

                foreach (var inv in invoices)
                {
                    csv.WriteField(inv.InvoiceNumber);
                    csv.WriteField(inv.Patient?.PatientName ?? string.Empty);
                    csv.WriteField(inv.Patient?.NationalId ?? string.Empty);
                    csv.WriteField(inv.Patient?.PhoneNumber ?? string.Empty);
                    csv.WriteField(inv.InvoiceDate.ToString("yyyy-MM-dd"));
                    csv.WriteField(inv.Amount.ToString("F2", CultureInfo.InvariantCulture));
                    csv.WriteField(inv.Discount.ToString("F2", CultureInfo.InvariantCulture));
                    csv.WriteField(inv.Tax.ToString("F2", CultureInfo.InvariantCulture));
                    csv.WriteField(inv.NetAmount.ToString("F2", CultureInfo.InvariantCulture));
                    csv.WriteField(inv.Status ?? "Unpaid");
                    csv.NextRecord();
                }
            }

            return memoryStream.ToArray();
        }

        /// <summary>
        /// Imports invoices from CSV, resolving patient and treatment relationships and validating billing amounts.
        /// </summary>
        public async Task<ImportResult<Invoice>> ImportInvoicesFromCsvAsync(Stream stream)
        {
            var result = new ImportResult<Invoice>();
            var validInvoices = new List<Invoice>();

            var allPatients = await _context.Patients.ToListAsync();
            var allTreatments = await _context.Treatments.Include(t => t.Appointment).ToListAsync();
            var existingInvoiceNumbers = (await _context.Invoices.Select(i => i.InvoiceNumber).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var firstDoctor = await _context.Doctors.FirstOrDefaultAsync();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null
            };

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            if (!await csv.ReadAsync() || !csv.ReadHeader())
            {
                result.ErrorMessages.Add("The uploaded invoices CSV file is empty or missing a valid header row.");
                return result;
            }

            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            int rowNumber = 1;

            while (await csv.ReadAsync())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    string GetValue(params string[] aliases)
                    {
                        foreach (var alias in aliases)
                        {
                            if (csv.TryGetField(alias, out string? value) && !string.IsNullOrWhiteSpace(value))
                                return value.Trim();

                            var normalizedAlias = NormalizeHeader(alias);
                            foreach (var header in headers)
                            {
                                if (NormalizeHeader(header) == normalizedAlias)
                                {
                                    if (csv.TryGetField(header, out string? matchVal) && !string.IsNullOrWhiteSpace(matchVal))
                                        return matchVal.Trim();
                                }
                            }
                        }
                        return string.Empty;
                    }

                    string invNumber = GetValue("InvoiceNumber", "InvoiceNo", "InvNumber", "رقم الفاتورة", "الفاتورة");
                    string patientIdentifier = GetValue("PatientNationalID", "PatientNationalId", "NationalID", "NationalId", "PatientPhone", "PatientName", "رقم الهوية", "المريض");
                    string dateRaw = GetValue("InvoiceDate", "Date", "تاريخ الفاتورة", "التاريخ");
                    string amountRaw = GetValue("Amount", "Total", "المبلغ", "القيمة");
                    string discountRaw = GetValue("Discount", "الخصم");
                    string taxRaw = GetValue("Tax", "الضريبة");
                    string netRaw = GetValue("NetAmount", "Net", "الصافي");
                    string status = GetValue("Status", "الحالة");

                    if (string.IsNullOrWhiteSpace(patientIdentifier) && string.IsNullOrWhiteSpace(amountRaw))
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: Skipped empty row.");
                        continue;
                    }

                    var rowErrors = new List<string>();

                    // 1. Resolve Patient
                    var patient = allPatients.FirstOrDefault(p =>
                        string.Equals(p.NationalId, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.PhoneNumber, patientIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.PatientName, patientIdentifier, StringComparison.OrdinalIgnoreCase));

                    if (patient == null)
                    {
                        rowErrors.Add($"No patient found matching '{patientIdentifier}'.");
                    }

                    // 2. Parse Amounts
                    decimal amount = 0, discount = 0, tax = 0, net = 0;
                    if (string.IsNullOrWhiteSpace(amountRaw) || (!decimal.TryParse(amountRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out amount) && !decimal.TryParse(amountRaw, NumberStyles.Any, CultureInfo.CurrentCulture, out amount)))
                    {
                        rowErrors.Add($"Invalid or missing invoice Amount ('{amountRaw}').");
                    }

                    if (!string.IsNullOrWhiteSpace(discountRaw))
                    {
                        decimal.TryParse(discountRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out discount);
                    }

                    if (!string.IsNullOrWhiteSpace(taxRaw))
                    {
                        decimal.TryParse(taxRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out tax);
                    }

                    if (!string.IsNullOrWhiteSpace(netRaw) && decimal.TryParse(netRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedNet) && parsedNet > 0)
                    {
                        net = parsedNet;
                    }
                    else
                    {
                        net = (amount - discount) + tax;
                    }

                    // 3. Parse Date
                    DateTime invDate = DateTime.Today;
                    if (!string.IsNullOrWhiteSpace(dateRaw))
                    {
                        string[] formats = { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "dd-MM-yyyy" };
                        if (!DateTime.TryParseExact(dateRaw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out invDate) &&
                            !DateTime.TryParse(dateRaw, CultureInfo.CurrentCulture, DateTimeStyles.None, out invDate))
                        {
                            rowErrors.Add($"Invalid Invoice Date '{dateRaw}'. Use format YYYY-MM-DD.");
                        }
                    }

                    if (rowErrors.Any() || patient == null)
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: {string.Join(" | ", rowErrors)}");
                        continue;
                    }

                    // 4. Resolve or create Treatment foreign key
                    var treatment = allTreatments.FirstOrDefault(t => t.Appointment != null && t.Appointment.PatientId == patient.PatientId) ?? allTreatments.FirstOrDefault();

                    if (treatment == null)
                    {
                        var appt = new Appointment
                        {
                            PatientId = patient.PatientId,
                            DoctorId = firstDoctor?.DoctorId ?? 1,
                            AppointmentDate = invDate,
                            AppointmentTime = new TimeSpan(9, 0, 0),
                            Status = "Completed",
                            Notes = "Auto-created for invoice billing"
                        };
                        _context.Appointments.Add(appt);
                        await _context.SaveChangesAsync();

                        treatment = new Treatment
                        {
                            AppointmentId = appt.AppointmentId,
                            TreatmentDate = invDate,
                            TreatmentDesc = "General Consultation & Services",
                            TreatmentCost = amount
                        };
                        _context.Treatments.Add(treatment);
                        await _context.SaveChangesAsync();
                        allTreatments.Add(treatment);
                    }

                    // 5. Unique Invoice Number
                    if (string.IsNullOrWhiteSpace(invNumber) || existingInvoiceNumbers.Contains(invNumber))
                    {
                        invNumber = "INV-" + DateTime.Now.Year + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
                    }

                    var invoice = new Invoice
                    {
                        InvoiceNumber = invNumber,
                        InvoiceDate = invDate,
                        Amount = amount,
                        Discount = discount,
                        Tax = tax,
                        NetAmount = net,
                        Status = string.IsNullOrWhiteSpace(status) ? "Unpaid" : status,
                        PatientId = patient.PatientId,
                        TreatmentId = treatment.TreatmentId
                    };

                    validInvoices.Add(invoice);
                    existingInvoiceNumbers.Add(invNumber);
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.ErrorMessages.Add($"Row {rowNumber}: Unexpected parse error: {ex.Message}");
                }
            }

            if (validInvoices.Any())
            {
                await _context.Invoices.AddRangeAsync(validInvoices);
                await _context.SaveChangesAsync();

                result.ImportedCount = validInvoices.Count;
                result.ImportedRecords = validInvoices;
            }

            return result;
        }

        #endregion

        #region Department & Specialty Methods

        /// <summary>
        /// Generates a downloadable sample CSV template for bulk department import with UTF-8 BOM.
        /// </summary>
        public byte[] GenerateDepartmentTemplateCsv()
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("DepartmentName");
                csv.WriteField("DepartmentCode");
                csv.NextRecord();

                csv.WriteField("Cardiology");
                csv.WriteField("CARD");
                csv.NextRecord();

                csv.WriteField("Pediatrics");
                csv.WriteField("PED");
                csv.NextRecord();

                csv.WriteField("Orthopedics");
                csv.WriteField("ORTH");
                csv.NextRecord();
            }

            return memoryStream.ToArray();
        }

        public async Task<ImportResult<Department>> ImportDepartmentsFromCsvAsync(Stream stream)
        {
            var result = new ImportResult<Department>();
            var validDepts = new List<Department>();

            var existingDepts = await _context.Departments.AsNoTracking().ToListAsync();
            var existingNames = new HashSet<string>(existingDepts.Select(d => d.DepartmentName.Trim()), StringComparer.OrdinalIgnoreCase);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null
            };

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            if (!await csv.ReadAsync() || !csv.ReadHeader())
            {
                result.ErrorMessages.Add("The uploaded departments file is empty or missing a header row.");
                return result;
            }

            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            int rowNumber = 1;

            while (await csv.ReadAsync())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    string GetValue(params string[] aliases)
                    {
                        foreach (var alias in aliases)
                        {
                            if (csv.TryGetField(alias, out string? value) && !string.IsNullOrWhiteSpace(value))
                                return value.Trim();

                            var normalizedAlias = NormalizeHeader(alias);
                            foreach (var header in headers)
                            {
                                if (NormalizeHeader(header) == normalizedAlias)
                                {
                                    if (csv.TryGetField(header, out string? matchVal) && !string.IsNullOrWhiteSpace(matchVal))
                                        return matchVal.Trim();
                                }
                            }
                        }
                        return string.Empty;
                    }

                    string deptName = GetValue("DepartmentName", "Name", "DeptName", "اسم القسم", "القسم");
                    string deptCode = GetValue("DepartmentCode", "DepartmentAbbr", "Code", "Abbr", "رمز القسم", "كود القسم");

                    if (string.IsNullOrWhiteSpace(deptName))
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: Department Name is required.");
                        continue;
                    }

                    if (existingNames.Contains(deptName) || validDepts.Any(d => string.Equals(d.DepartmentName, deptName, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: Department '{deptName}' already exists.");
                        continue;
                    }

                    var dept = new Department
                    {
                        DepartmentName = deptName,
                        DepartmentAbbr = string.IsNullOrWhiteSpace(deptCode) ? (deptName.Length >= 4 ? deptName.Substring(0, 4).ToUpperInvariant() : deptName.ToUpperInvariant()) : deptCode
                    };

                    validDepts.Add(dept);
                    existingNames.Add(deptName);
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.ErrorMessages.Add($"Row {rowNumber}: Unexpected parse error: {ex.Message}");
                }
            }

            if (validDepts.Any())
            {
                await _context.Departments.AddRangeAsync(validDepts);
                await _context.SaveChangesAsync();
                result.ImportedCount = validDepts.Count;
                result.ImportedRecords = validDepts;
            }

            return result;
        }

        /// <summary>
        /// Generates a downloadable sample CSV template for bulk specialties import with UTF-8 BOM.
        /// </summary>
        public byte[] GenerateSpecialtiesTemplateCsv()
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteField("SpecializationName");
                csv.WriteField("Description");
                csv.NextRecord();

                csv.WriteField("Cardiology");
                csv.WriteField("Heart and cardiovascular medical care");
                csv.NextRecord();

                csv.WriteField("Dermatology");
                csv.WriteField("Skin treatments and aesthetic medicine");
                csv.NextRecord();

                csv.WriteField("Pediatrics");
                csv.WriteField("Infant and child healthcare");
                csv.NextRecord();
            }

            return memoryStream.ToArray();
        }

        public async Task<ImportResult<string>> ImportSpecialtiesFromCsvAsync(Stream stream)
        {
            var result = new ImportResult<string>();
            var validSpecs = new List<string>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null
            };

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            if (!await csv.ReadAsync() || !csv.ReadHeader())
            {
                result.ErrorMessages.Add("The uploaded specialties file is empty or missing a header row.");
                return result;
            }

            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            int rowNumber = 1;

            while (await csv.ReadAsync())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    string GetValue(params string[] aliases)
                    {
                        foreach (var alias in aliases)
                        {
                            if (csv.TryGetField(alias, out string? value) && !string.IsNullOrWhiteSpace(value))
                                return value.Trim();

                            var normalizedAlias = NormalizeHeader(alias);
                            foreach (var header in headers)
                            {
                                if (NormalizeHeader(header) == normalizedAlias)
                                {
                                    if (csv.TryGetField(header, out string? matchVal) && !string.IsNullOrWhiteSpace(matchVal))
                                        return matchVal.Trim();
                                }
                            }
                        }
                        return string.Empty;
                    }

                    string specName = GetValue("SpecializationName", "SpecialtyName", "Specialization", "Specialty", "اسم التخصص", "التخصص");
                    string desc = GetValue("Description", "Desc", "Notes", "الوصف", "ملاحظات");

                    if (string.IsNullOrWhiteSpace(specName))
                    {
                        result.SkippedCount++;
                        result.ErrorMessages.Add($"Row {rowNumber}: Specialization Name is required.");
                        continue;
                    }

                    validSpecs.Add(specName);
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.ErrorMessages.Add($"Row {rowNumber}: Unexpected parse error: {ex.Message}");
                }
            }

            if (validSpecs.Any())
            {
                var setting = await _context.ClinicSettings.FirstOrDefaultAsync(s => s.Key == "Clinic_Specialties");
                if (setting == null)
                {
                    setting = new ClinicSetting
                    {
                        Key = "Clinic_Specialties",
                        Value = string.Join(";", validSpecs.Distinct()),
                        Description = "List of clinic specialties",
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ClinicSettings.Add(setting);
                }
                else
                {
                    var existingList = (setting.Value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                    existingList.AddRange(validSpecs);
                    setting.Value = string.Join(";", existingList.Distinct());
                    setting.UpdatedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();

                result.ImportedCount = validSpecs.Count;
                result.ImportedRecords = validSpecs;
            }

            return result;
        }

        #endregion

        #region Helper Utilities

        private static string NormalizeHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header)) return string.Empty;
            return Regex.Replace(header, @"[\s_\-\(\)]+", string.Empty).ToLowerInvariant();
        }

        #endregion
    }
}
