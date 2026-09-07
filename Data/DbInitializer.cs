using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    /// <summary>
    /// Seeds comprehensive dummy data into the MediCare database on first run.
    /// Each section is idempotent - it only inserts records when the target table is empty.
    /// </summary>
    public static class DbInitializer
    {
        public static async Task SeedAsync(ClinicDbContext context, ILogger logger)
        {
            try
            {
                // Apply any pending migrations automatically
                await context.Database.MigrateAsync();

                // ── 0. AUDIT LOGS TABLE & SEED ─────────────────────────────────
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLogs' and xtype='U')
                        CREATE TABLE AuditLogs (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            UserId NVARCHAR(128) NULL,
                            UserName NVARCHAR(128) NULL,
                            Action NVARCHAR(100) NOT NULL,
                            EntityName NVARCHAR(100) NOT NULL,
                            EntityId NVARCHAR(100) NULL,
                            Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                            Details NVARCHAR(MAX) NULL
                        );
                    ");

                    if (!await context.AuditLogs.AnyAsync())
                    {
                        var nowUtc = DateTime.UtcNow;
                        var initialLogs = new List<AuditLog>
                        {
                            new() { UserName = "admin", Action = "SYSTEM_INIT", EntityName = "System", Timestamp = nowUtc.AddHours(-6), Details = "ClinicFlow Core Engine initialized successfully." },
                            new() { UserName = "admin", Action = "CREATE", EntityName = "Doctor", EntityId = "DR001", Timestamp = nowUtc.AddHours(-5), Details = "Added specialist Dr. James Anderson (Cardiology)." },
                            new() { UserName = "admin", Action = "CREATE", EntityName = "Patient", EntityId = "PT001", Timestamp = nowUtc.AddHours(-4), Details = "Registered new electronic patient record." },
                            new() { UserName = "admin", Action = "UPDATE", EntityName = "ClinicSetting", EntityId = "Theme", Timestamp = nowUtc.AddHours(-3), Details = "Configured clinic design tokens to Olive-Dark palette." },
                            new() { UserName = "admin", Action = "CREATE", EntityName = "Appointment", EntityId = "APT001", Timestamp = nowUtc.AddHours(-2), Details = "Confirmed clinical consultation appointment." },
                            new() { UserName = "admin", Action = "VIEW", EntityName = "Reports", EntityId = "Analytics", Timestamp = nowUtc.AddHours(-1), Details = "Accessed Executive Financial & Operational Intelligence Report." }
                        };
                        await context.AuditLogs.AddRangeAsync(initialLogs);
                        await context.SaveChangesAsync();
                        logger.LogInformation("[Seed] AuditLogs table created and seeded with {Count} entries.", initialLogs.Count);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Seed] Error initializing AuditLogs table.");
                }

                // 1. DEPARTMENTS
                if (!await context.Departments.AnyAsync())
                {
                    logger.LogInformation("[Seed] Seeding Departments...");

                    var departments = new List<Department>
                    {
                        new() { DepartmentName = "Cardiology",        DepartmentAbbr = "CARD" },
                        new() { DepartmentName = "Pediatrics",        DepartmentAbbr = "PEDI" },
                        new() { DepartmentName = "General Surgery",   DepartmentAbbr = "GSUR" },
                        new() { DepartmentName = "Neurology",         DepartmentAbbr = "NEUR" },
                        new() { DepartmentName = "Orthopedics",       DepartmentAbbr = "ORTH" },
                    };

                    await context.Departments.AddRangeAsync(departments);
                    await context.SaveChangesAsync();
                    logger.LogInformation("[Seed] Departments seeded: {Count}", departments.Count);
                }

                // Fetch department IDs
                var deptIds = await context.Departments
                    .OrderBy(d => d.DepartmentId)
                    .Select(d => d.DepartmentId)
                    .ToListAsync();

                // 2. DOCTORS
                if (!await context.Doctors.AnyAsync())
                {
                    logger.LogInformation("[Seed] Seeding Doctors...");

                    var doctors = new List<Doctor>
                    {
                        new()
                        {
                            DoctorNumber    = "DR001",
                            DoctorName      = "James Anderson",
                            Specialization  = "Interventional Cardiology",
                            ConsultationFee = 85.00m,
                            DepartmentId    = deptIds[0]
                        },
                        new()
                        {
                            DoctorNumber    = "DR002",
                            DoctorName      = "Sarah Mitchell",
                            Specialization  = "Pediatric Medicine",
                            ConsultationFee = 70.00m,
                            DepartmentId    = deptIds[1]
                        },
                        new()
                        {
                            DoctorNumber    = "DR003",
                            DoctorName      = "Robert Chen",
                            Specialization  = "General & Laparoscopic Surgery",
                            ConsultationFee = 95.00m,
                            DepartmentId    = deptIds[2]
                        },
                        new()
                        {
                            DoctorNumber    = "DR004",
                            DoctorName      = "Emily Watson",
                            Specialization  = "Clinical Neurology",
                            ConsultationFee = 90.00m,
                            DepartmentId    = deptIds[3]
                        },
                        new()
                        {
                            DoctorNumber    = "DR005",
                            DoctorName      = "Michael Torres",
                            Specialization  = "Orthopedic & Sports Medicine",
                            ConsultationFee = 80.00m,
                            DepartmentId    = deptIds[4]
                        },
                    };

                    await context.Doctors.AddRangeAsync(doctors);
                    await context.SaveChangesAsync();
                    logger.LogInformation("[Seed] Doctors seeded: {Count}", doctors.Count);
                }

                // Fetch doctor IDs
                var doctorIds = await context.Doctors
                    .OrderBy(d => d.DoctorId)
                    .Select(d => d.DoctorId)
                    .ToListAsync();

                // 3. PATIENTS
                if (!await context.Patients.AnyAsync())
                {
                    logger.LogInformation("[Seed] Seeding Patients...");

                    var patients = new List<Patient>
                    {
                        new()
                        {
                            PatientName     = "Oliver Harrison",
                            NationalId      = "1023456789",
                            PhoneNumber     = "0791234567",
                            DOB             = new DateTime(1980, 4, 15),
                            BloodType       = "A+",
                            Allergies       = "Penicillin, Sulfa drugs",
                            ChronicDiseases = "Hypertension, Type-2 Diabetes",
                            Notes           = "Patient follows a low-sodium diet. Monitor BP at every visit."
                        },
                        new()
                        {
                            PatientName     = "Sophia Grant",
                            NationalId      = "1034567890",
                            PhoneNumber     = "0792345678",
                            DOB             = new DateTime(1995, 8, 22),
                            BloodType       = "O-",
                            Allergies       = "None known",
                            ChronicDiseases = "Mild asthma",
                            Notes           = "Asthma inhaler required before strenuous exams."
                        },
                        new()
                        {
                            PatientName     = "Ethan Brooks",
                            NationalId      = "1045678901",
                            PhoneNumber     = "0793456789",
                            DOB             = new DateTime(1972, 12, 3),
                            BloodType       = "B+",
                            Allergies       = "Latex, Ibuprofen",
                            ChronicDiseases = "Chronic lower-back pain, Hypercholesterolemia",
                            Notes           = "Pre-authorisation required for MRI. History of prior lumbar surgery 2019."
                        },
                        new()
                        {
                            PatientName     = "Mia Patel",
                            NationalId      = "1056789012",
                            PhoneNumber     = "0794567890",
                            DOB             = new DateTime(2005, 3, 10),
                            BloodType       = "AB+",
                            Allergies       = "Shellfish",
                            ChronicDiseases = "None",
                            Notes           = "Minor, guardian must accompany during all procedures."
                        },
                        new()
                        {
                            PatientName     = "Noah Williams",
                            NationalId      = "1067890123",
                            PhoneNumber     = "0795678901",
                            DOB             = new DateTime(1960, 7, 29),
                            BloodType       = "A-",
                            Allergies       = "Aspirin, Codeine",
                            ChronicDiseases = "Coronary artery disease, Atrial fibrillation",
                            Notes           = "On warfarin. Check INR before any invasive procedure. Monthly cardiologist follow-up."
                        },
                        new()
                        {
                            PatientName     = "Lena Kovacs",
                            NationalId      = "1078901234",
                            PhoneNumber     = "0796789012",
                            DOB             = new DateTime(1988, 11, 18),
                            BloodType       = "O+",
                            Allergies       = "None known",
                            ChronicDiseases = "Migraine, Anxiety disorder",
                            Notes           = "Sensitive to bright lights during neurological exams."
                        },
                        new()
                        {
                            PatientName     = "Aiden Clarke",
                            NationalId      = "1089012345",
                            PhoneNumber     = "0797890123",
                            DOB             = new DateTime(2001, 6, 5),
                            BloodType       = "B-",
                            Allergies       = "Pollen seasonal",
                            ChronicDiseases = "None",
                            Notes           = "Athletic patient, recovering from ACL repair left knee March 2026."
                        },
                    };

                    await context.Patients.AddRangeAsync(patients);
                    await context.SaveChangesAsync();
                    logger.LogInformation("[Seed] Patients seeded: {Count}", patients.Count);
                }

                // Fetch patient IDs
                var patientIds = await context.Patients
                    .OrderBy(p => p.PatientId)
                    .Select(p => p.PatientId)
                    .ToListAsync();

                // 4. APPOINTMENTS
                if (!await context.Appointments.AnyAsync())
                {
                    logger.LogInformation("[Seed] Seeding Appointments...");

                    var today = DateTime.Today;

                    var appointments = new List<Appointment>
                    {
                        // --- Completed appointments ---
                        new()
                        {
                            DoctorId        = doctorIds[0],
                            PatientId       = patientIds[4],
                            AppointmentDate = today.AddDays(-30),
                            AppointmentTime = new TimeSpan(9, 0, 0),
                            Status          = "Completed",
                            Notes           = "Regular cardiac check-up. ECG and echo requested."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[2],
                            PatientId       = patientIds[2],
                            AppointmentDate = today.AddDays(-21),
                            AppointmentTime = new TimeSpan(10, 30, 0),
                            Status          = "Completed",
                            Notes           = "Post-operative follow-up after appendectomy."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[3],
                            PatientId       = patientIds[5],
                            AppointmentDate = today.AddDays(-14),
                            AppointmentTime = new TimeSpan(11, 0, 0),
                            Status          = "Completed",
                            Notes           = "Migraine management review, MRI results discussion."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[4],
                            PatientId       = patientIds[6],
                            AppointmentDate = today.AddDays(-7),
                            AppointmentTime = new TimeSpan(14, 0, 0),
                            Status          = "Completed",
                            Notes           = "ACL rehabilitation milestone review, physio progress check."
                        },
                        // --- Confirmed appointments ---
                        new()
                        {
                            DoctorId        = doctorIds[1],
                            PatientId       = patientIds[3],
                            AppointmentDate = today.AddDays(2),
                            AppointmentTime = new TimeSpan(9, 30, 0),
                            Status          = "Confirmed",
                            Notes           = "Routine annual paediatric check-up and immunisation review."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[0],
                            PatientId       = patientIds[0],
                            AppointmentDate = today.AddDays(3),
                            AppointmentTime = new TimeSpan(11, 0, 0),
                            Status          = "Confirmed",
                            Notes           = "Hypertension management and medication adjustment."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[3],
                            PatientId       = patientIds[5],
                            AppointmentDate = today.AddDays(5),
                            AppointmentTime = new TimeSpan(13, 30, 0),
                            Status          = "Confirmed",
                            Notes           = "Follow-up after starting new preventive migraine medication."
                        },
                        // --- Pending appointments ---
                        new()
                        {
                            DoctorId        = doctorIds[2],
                            PatientId       = patientIds[1],
                            AppointmentDate = today.AddDays(7),
                            AppointmentTime = new TimeSpan(10, 0, 0),
                            Status          = "Pending",
                            Notes           = "Consultation for recurring abdominal pain."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[4],
                            PatientId       = patientIds[2],
                            AppointmentDate = today.AddDays(10),
                            AppointmentTime = new TimeSpan(15, 0, 0),
                            Status          = "Pending",
                            Notes           = "Initial consultation for chronic lower-back pain management."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[1],
                            PatientId       = patientIds[3],
                            AppointmentDate = today.AddDays(14),
                            AppointmentTime = new TimeSpan(9, 0, 0),
                            Status          = "Pending",
                            Notes           = "Allergy testing for shellfish sensitivity follow-up."
                        },
                        // --- Cancelled appointments ---
                        new()
                        {
                            DoctorId        = doctorIds[0],
                            PatientId       = patientIds[4],
                            AppointmentDate = today.AddDays(-10),
                            AppointmentTime = new TimeSpan(8, 30, 0),
                            Status          = "Cancelled",
                            Notes           = "Patient requested cancellation. Rescheduled to next week."
                        },
                        new()
                        {
                            DoctorId        = doctorIds[3],
                            PatientId       = patientIds[0],
                            AppointmentDate = today.AddDays(-3),
                            AppointmentTime = new TimeSpan(16, 0, 0),
                            Status          = "Cancelled",
                            Notes           = "Doctor unavailable. Clinic emergency on the day."
                        },
                    };

                    await context.Appointments.AddRangeAsync(appointments);
                    await context.SaveChangesAsync();
                    logger.LogInformation("[Seed] Appointments seeded: {Count}", appointments.Count);
                }

                // 5. TREATMENTS
                if (!await context.Treatments.AnyAsync())
                {
                    logger.LogInformation("[Seed] Seeding Treatments...");

                    var completedAppts = await context.Appointments
                        .Where(a => a.Status == "Completed")
                        .OrderBy(a => a.AppointmentDate)
                        .Select(a => new { a.AppointmentId })
                        .ToListAsync();

                    if (completedAppts.Count >= 4)
                    {
                        var treatments = new List<Treatment>
                        {
                            new()
                            {
                                AppointmentId     = completedAppts[0].AppointmentId,
                                TreatmentDate     = DateTime.Today.AddDays(-30),
                                TreatmentDesc     = "12-lead ECG performed, echocardiogram scheduled. Medication review completed.",
                                TreatmentCost     = 120.00m,
                                Diagnosis         = "Stable coronary artery disease with controlled atrial fibrillation.",
                                PrescriptionNotes = "Continue Bisoprolol 5mg OD, Apixaban 5mg BD. Next review in 4 weeks."
                            },
                            new()
                            {
                                AppointmentId     = completedAppts[1].AppointmentId,
                                TreatmentDate     = DateTime.Today.AddDays(-21),
                                TreatmentDesc     = "Wound inspection and dressing change. Sutures removed. Abdominal palpation normal.",
                                TreatmentCost     = 75.00m,
                                Diagnosis         = "Successful recovery post-appendectomy. No signs of infection.",
                                PrescriptionNotes = "Amoxicillin 500mg TDS x5 days. Light activity only for 2 more weeks."
                            },
                            new()
                            {
                                AppointmentId     = completedAppts[2].AppointmentId,
                                TreatmentDate     = DateTime.Today.AddDays(-14),
                                TreatmentDesc     = "MRI brain reviewed. No structural abnormalities. Trigger diary analysed.",
                                TreatmentCost     = 100.00m,
                                Diagnosis         = "Chronic migraine with aura. Stress and screen time are key triggers.",
                                PrescriptionNotes = "Topiramate 25mg OD, titrate to 50mg after 2 weeks. Avoid caffeine and disrupted sleep."
                            },
                            new()
                            {
                                AppointmentId     = completedAppts[3].AppointmentId,
                                TreatmentDate     = DateTime.Today.AddDays(-7),
                                TreatmentDesc     = "Knee range-of-motion measured 0-120 degrees. Muscle strength 4/5. Gait analysis normal.",
                                TreatmentCost     = 90.00m,
                                Diagnosis         = "ACL reconstruction progressing well, Phase 3 rehab commenced.",
                                PrescriptionNotes = "Continue physio 3x per week. Avoid cutting and pivoting movements. Ice 20min post-exercise."
                            },
                        };

                        await context.Treatments.AddRangeAsync(treatments);
                        await context.SaveChangesAsync();
                        logger.LogInformation("[Seed] Treatments seeded: {Count}", treatments.Count);
                    }
                }

                // 6. INVOICES
                if (!await context.Invoices.AnyAsync())
                {
                    logger.LogInformation("[Seed] Seeding Invoices...");

                    var treatmentData = await context.Treatments
                        .Include(t => t.Appointment)
                            .ThenInclude(a => a!.Doctor)
                        .OrderBy(t => t.TreatmentId)
                        .ToListAsync();

                    if (treatmentData.Count >= 4)
                    {
                        static decimal CalcNet(decimal amount, decimal discount, decimal tax)
                            => Math.Round(amount - discount + (amount - discount) * tax / 100, 2);

                        var invoices = new List<Invoice>
                        {
                            new()
                            {
                                InvoiceNumber = "INV-2026-0001",
                                InvoiceDate   = treatmentData[0].TreatmentDate,
                                PatientId     = treatmentData[0].Appointment!.PatientId,
                                TreatmentId   = treatmentData[0].TreatmentId,
                                Amount        = treatmentData[0].TreatmentCost + 85.00m,
                                Discount      = 10.00m,
                                Tax           = 5.00m,
                                NetAmount     = CalcNet(treatmentData[0].TreatmentCost + 85.00m, 10.00m, 5.00m),
                                Status        = "Paid"
                            },
                            new()
                            {
                                InvoiceNumber = "INV-2026-0002",
                                InvoiceDate   = treatmentData[1].TreatmentDate,
                                PatientId     = treatmentData[1].Appointment!.PatientId,
                                TreatmentId   = treatmentData[1].TreatmentId,
                                Amount        = treatmentData[1].TreatmentCost + 95.00m,
                                Discount      = 0.00m,
                                Tax           = 5.00m,
                                NetAmount     = CalcNet(treatmentData[1].TreatmentCost + 95.00m, 0.00m, 5.00m),
                                Status        = "Paid"
                            },
                            new()
                            {
                                InvoiceNumber = "INV-2026-0003",
                                InvoiceDate   = treatmentData[2].TreatmentDate,
                                PatientId     = treatmentData[2].Appointment!.PatientId,
                                TreatmentId   = treatmentData[2].TreatmentId,
                                Amount        = treatmentData[2].TreatmentCost + 90.00m,
                                Discount      = 15.00m,
                                Tax           = 5.00m,
                                NetAmount     = CalcNet(treatmentData[2].TreatmentCost + 90.00m, 15.00m, 5.00m),
                                Status        = "Unpaid"
                            },
                            new()
                            {
                                InvoiceNumber = "INV-2026-0004",
                                InvoiceDate   = treatmentData[3].TreatmentDate,
                                PatientId     = treatmentData[3].Appointment!.PatientId,
                                TreatmentId   = treatmentData[3].TreatmentId,
                                Amount        = treatmentData[3].TreatmentCost + 80.00m,
                                Discount      = 0.00m,
                                Tax           = 0.00m,
                                NetAmount     = CalcNet(treatmentData[3].TreatmentCost + 80.00m, 0.00m, 0.00m),
                                Status        = "Unpaid"
                            },
                        };

                        await context.Invoices.AddRangeAsync(invoices);
                        await context.SaveChangesAsync();
                        logger.LogInformation("[Seed] Invoices seeded: {Count}", invoices.Count);
                    }
                }

                // Final flush — ensures any buffered changes are always persisted
                await context.SaveChangesAsync();
                logger.LogInformation("[Seed] Database seeding completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Seed] An error occurred while seeding the database.");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // ADDITIVE SEED  —  safe to run against an already-populated database.
        // Every record is checked by a unique business key before inserting,
        // so no duplicates are ever created and existing data is NEVER touched.
        // ══════════════════════════════════════════════════════════════════════════
        public static async Task SeedAdditionalAsync(ClinicDbContext context, ILogger logger)
        {
            try
            {
                // ── helper: only add when the unique key is absent ────────────────────────
                static async Task AddDoctorIfMissing(ClinicDbContext ctx, Doctor doc)
                {
                    if (!await ctx.Doctors.AnyAsync(d => d.DoctorNumber == doc.DoctorNumber))
                        await ctx.Doctors.AddAsync(doc);
                }

                static async Task AddPatientIfMissing(ClinicDbContext ctx, Patient pat)
                {
                    if (!await ctx.Patients.AnyAsync(p => p.NationalId == pat.NationalId))
                        await ctx.Patients.AddAsync(pat);
                }

                static async Task AddInvoiceIfMissing(ClinicDbContext ctx, Invoice inv)
                {
                    if (!await ctx.Invoices.AnyAsync(i => i.InvoiceNumber == inv.InvoiceNumber))
                        await ctx.Invoices.AddAsync(inv);
                }

                static decimal CalcNet(decimal amount, decimal discount, decimal tax)
                    => Math.Round(amount - discount + (amount - discount) * tax / 100, 2);

                // ── resolve lookup tables once ────────────────────────────────────────────
                var deptIds = await context.Departments
                    .OrderBy(d => d.DepartmentId)
                    .Select(d => d.DepartmentId)
                    .ToListAsync();

                if (deptIds.Count < 5)
                {
                    logger.LogWarning("[SeedExtra] Need at least 5 departments — skipping extra seed.");
                    return;
                }

                // ══════════════════════════════════════════════════
                // A. THREE NEW DOCTORS (checked by DoctorNumber)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedExtra] Checking new Doctors...");

                await AddDoctorIfMissing(context, new Doctor
                {
                    DoctorNumber    = "DR006",
                    DoctorName      = "Priya Sharma",
                    Specialization  = "Endocrinology & Diabetes",
                    ConsultationFee = 88.00m,
                    DepartmentId    = deptIds[0]   // Cardiology (nearest available)
                });

                await AddDoctorIfMissing(context, new Doctor
                {
                    DoctorNumber    = "DR007",
                    DoctorName      = "Lucas Fernandez",
                    Specialization  = "Gastroenterology",
                    ConsultationFee = 92.00m,
                    DepartmentId    = deptIds[2]   // General Surgery
                });

                await AddDoctorIfMissing(context, new Doctor
                {
                    DoctorNumber    = "DR008",
                    DoctorName      = "Hana Nakamura",
                    Specialization  = "Dermatology & Allergy",
                    ConsultationFee = 75.00m,
                    DepartmentId    = deptIds[1]   // Pediatrics
                });

                await context.SaveChangesAsync();
                logger.LogInformation("[SeedExtra] Doctor check done.");

                // Refresh doctor ID list now that the new rows exist
                var allDoctorIds = await context.Doctors
                    .OrderBy(d => d.DoctorId)
                    .Select(d => d.DoctorId)
                    .ToListAsync();

                // Resolve the new doctors by their unique number
                int drPriyaId   = await context.Doctors.Where(d => d.DoctorNumber == "DR006").Select(d => d.DoctorId).FirstAsync();
                int drLucasId   = await context.Doctors.Where(d => d.DoctorNumber == "DR007").Select(d => d.DoctorId).FirstAsync();
                int drHanaId    = await context.Doctors.Where(d => d.DoctorNumber == "DR008").Select(d => d.DoctorId).FirstAsync();
                // Keep a reference to the first seeded doctor (DR001) for appointment linking
                int drAndersonId = await context.Doctors.Where(d => d.DoctorNumber == "DR001").Select(d => d.DoctorId).FirstAsync();

                // ══════════════════════════════════════════════════
                // B. FOUR NEW PATIENTS (checked by NationalId)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedExtra] Checking new Patients...");

                await AddPatientIfMissing(context, new Patient
                {
                    PatientName     = "Charlotte Reed",
                    NationalId      = "2012345678",
                    PhoneNumber     = "0798001122",
                    DOB             = new DateTime(1993, 2, 14),
                    BloodType       = "A+",
                    Allergies       = "Penicillin",
                    ChronicDiseases = "Type-1 Diabetes",
                    Notes           = "Uses insulin pump. Check glucose before any sedation."
                });

                await AddPatientIfMissing(context, new Patient
                {
                    PatientName     = "Daniel Okonkwo",
                    NationalId      = "2023456789",
                    PhoneNumber     = "0798112233",
                    DOB             = new DateTime(1977, 9, 8),
                    BloodType       = "O+",
                    Allergies       = "None known",
                    ChronicDiseases = "Irritable bowel syndrome",
                    Notes           = "Avoid high-fibre oral preparations. Prefers morning appointments."
                });

                await AddPatientIfMissing(context, new Patient
                {
                    PatientName     = "Isabella Moreau",
                    NationalId      = "2034567890",
                    PhoneNumber     = "0798223344",
                    DOB             = new DateTime(2000, 5, 30),
                    BloodType       = "B+",
                    Allergies       = "Latex, Sulfa drugs",
                    ChronicDiseases = "Psoriasis",
                    Notes           = "Skin barrier compromised — use latex-free gloves during examination."
                });

                await AddPatientIfMissing(context, new Patient
                {
                    PatientName     = "George Lawson",
                    NationalId      = "2045678901",
                    PhoneNumber     = "0798334455",
                    DOB             = new DateTime(1955, 11, 2),
                    BloodType       = "AB-",
                    Allergies       = "Iodine contrast dye",
                    ChronicDiseases = "Chronic kidney disease stage 3, Hypertension",
                    Notes           = "Contrast CT contraindicated. eGFR must be checked before any nephrotoxic drug."
                });

                await context.SaveChangesAsync();
                logger.LogInformation("[SeedExtra] Patient check done.");

                // Resolve the new patient IDs by NationalId
                int patCharlotteId  = await context.Patients.Where(p => p.NationalId == "2012345678").Select(p => p.PatientId).FirstAsync();
                int patDanielId     = await context.Patients.Where(p => p.NationalId == "2023456789").Select(p => p.PatientId).FirstAsync();
                int patIsabellaId   = await context.Patients.Where(p => p.NationalId == "2034567890").Select(p => p.PatientId).FirstAsync();
                int patGeorgeId     = await context.Patients.Where(p => p.NationalId == "2045678901").Select(p => p.PatientId).FirstAsync();

                // ══════════════════════════════════════════════════
                // C. FIVE NEW APPOINTMENTS (checked by Doctor+Patient+Date+Time)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedExtra] Checking new Appointments...");

                var today = DateTime.Today;

                // Appointment 1 — Completed (endocrinology for Charlotte)
                var appt1Date = today.AddDays(-18);
                var appt1Time = new TimeSpan(10, 0, 0);
                int appt1Id = 0;
                if (!await context.Appointments.AnyAsync(a =>
                        a.DoctorId == drPriyaId && a.PatientId == patCharlotteId &&
                        a.AppointmentDate == appt1Date && a.AppointmentTime == appt1Time))
                {
                    var appt1 = new Appointment
                    {
                        DoctorId        = drPriyaId,
                        PatientId       = patCharlotteId,
                        AppointmentDate = appt1Date,
                        AppointmentTime = appt1Time,
                        Status          = "Completed",
                        Notes           = "HbA1c review and insulin dosage adjustment."
                    };
                    await context.Appointments.AddAsync(appt1);
                    await context.SaveChangesAsync();
                    appt1Id = appt1.AppointmentId;
                }
                else
                {
                    appt1Id = await context.Appointments
                        .Where(a => a.DoctorId == drPriyaId && a.PatientId == patCharlotteId &&
                                    a.AppointmentDate == appt1Date && a.AppointmentTime == appt1Time)
                        .Select(a => a.AppointmentId).FirstAsync();
                }

                // Appointment 2 — Completed (gastro for Daniel)
                var appt2Date = today.AddDays(-12);
                var appt2Time = new TimeSpan(11, 30, 0);
                int appt2Id = 0;
                if (!await context.Appointments.AnyAsync(a =>
                        a.DoctorId == drLucasId && a.PatientId == patDanielId &&
                        a.AppointmentDate == appt2Date && a.AppointmentTime == appt2Time))
                {
                    var appt2 = new Appointment
                    {
                        DoctorId        = drLucasId,
                        PatientId       = patDanielId,
                        AppointmentDate = appt2Date,
                        AppointmentTime = appt2Time,
                        Status          = "Completed",
                        Notes           = "Colonoscopy prep counselling and diet plan review."
                    };
                    await context.Appointments.AddAsync(appt2);
                    await context.SaveChangesAsync();
                    appt2Id = appt2.AppointmentId;
                }
                else
                {
                    appt2Id = await context.Appointments
                        .Where(a => a.DoctorId == drLucasId && a.PatientId == patDanielId &&
                                    a.AppointmentDate == appt2Date && a.AppointmentTime == appt2Time)
                        .Select(a => a.AppointmentId).FirstAsync();
                }

                // Appointment 3 — Confirmed (dermatology for Isabella)
                var appt3Date = today.AddDays(4);
                var appt3Time = new TimeSpan(9, 0, 0);
                if (!await context.Appointments.AnyAsync(a =>
                        a.DoctorId == drHanaId && a.PatientId == patIsabellaId &&
                        a.AppointmentDate == appt3Date && a.AppointmentTime == appt3Time))
                {
                    await context.Appointments.AddAsync(new Appointment
                    {
                        DoctorId        = drHanaId,
                        PatientId       = patIsabellaId,
                        AppointmentDate = appt3Date,
                        AppointmentTime = appt3Time,
                        Status          = "Confirmed",
                        Notes           = "Psoriasis flare-up assessment and biologic therapy review."
                    });
                    await context.SaveChangesAsync();
                }

                // Appointment 4 — Pending (nephrology-adjacent; cardiology dr for George)
                var appt4Date = today.AddDays(9);
                var appt4Time = new TimeSpan(14, 30, 0);
                if (!await context.Appointments.AnyAsync(a =>
                        a.DoctorId == drAndersonId && a.PatientId == patGeorgeId &&
                        a.AppointmentDate == appt4Date && a.AppointmentTime == appt4Time))
                {
                    await context.Appointments.AddAsync(new Appointment
                    {
                        DoctorId        = drAndersonId,
                        PatientId       = patGeorgeId,
                        AppointmentDate = appt4Date,
                        AppointmentTime = appt4Time,
                        Status          = "Pending",
                        Notes           = "Hypertension management in context of CKD. Avoid ACE inhibitors if eGFR < 30."
                    });
                    await context.SaveChangesAsync();
                }

                // Appointment 5 — Pending (endocrinology follow-up for Charlotte)
                var appt5Date = today.AddDays(21);
                var appt5Time = new TimeSpan(10, 0, 0);
                if (!await context.Appointments.AnyAsync(a =>
                        a.DoctorId == drPriyaId && a.PatientId == patCharlotteId &&
                        a.AppointmentDate == appt5Date && a.AppointmentTime == appt5Time))
                {
                    await context.Appointments.AddAsync(new Appointment
                    {
                        DoctorId        = drPriyaId,
                        PatientId       = patCharlotteId,
                        AppointmentDate = appt5Date,
                        AppointmentTime = appt5Time,
                        Status          = "Pending",
                        Notes           = "3-month HbA1c recheck and pump calibration review."
                    });
                    await context.SaveChangesAsync();
                }

                logger.LogInformation("[SeedExtra] Appointment check done.");

                // ══════════════════════════════════════════════════
                // D. TREATMENTS for the two new Completed appointments
                //    (checked: no treatment already linked to that appointment)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedExtra] Checking new Treatments...");

                int treat1Id = 0;
                if (appt1Id > 0 && !await context.Treatments.AnyAsync(t => t.AppointmentId == appt1Id))
                {
                    var treat1 = new Treatment
                    {
                        AppointmentId     = appt1Id,
                        TreatmentDate     = appt1Date,
                        TreatmentDesc     = "HbA1c result 8.1%. Basal insulin dose increased by 2 units. CGM data downloaded and reviewed.",
                        TreatmentCost     = 95.00m,
                        Diagnosis         = "Sub-optimal glycaemic control in Type-1 Diabetes. Dose adjustment required.",
                        PrescriptionNotes = "Increase Lantus from 18u to 20u at bedtime. Recheck HbA1c in 3 months."
                    };
                    await context.Treatments.AddAsync(treat1);
                    await context.SaveChangesAsync();
                    treat1Id = treat1.TreatmentId;
                }
                else if (appt1Id > 0)
                {
                    treat1Id = await context.Treatments
                        .Where(t => t.AppointmentId == appt1Id)
                        .Select(t => t.TreatmentId).FirstAsync();
                }

                int treat2Id = 0;
                if (appt2Id > 0 && !await context.Treatments.AnyAsync(t => t.AppointmentId == appt2Id))
                {
                    var treat2 = new Treatment
                    {
                        AppointmentId     = appt2Id,
                        TreatmentDate     = appt2Date,
                        TreatmentDesc     = "Low-residue diet explained. Bowel prep kit prescribed. Colonoscopy booked for 10 days.",
                        TreatmentCost     = 60.00m,
                        Diagnosis         = "Suspected irritable bowel syndrome with possible polyp — colonoscopy warranted.",
                        PrescriptionNotes = "Movicol bowel prep x2 sachets evening before procedure. Clear fluids only from midnight."
                    };
                    await context.Treatments.AddAsync(treat2);
                    await context.SaveChangesAsync();
                    treat2Id = treat2.TreatmentId;
                }
                else if (appt2Id > 0)
                {
                    treat2Id = await context.Treatments
                        .Where(t => t.AppointmentId == appt2Id)
                        .Select(t => t.TreatmentId).FirstAsync();
                }

                logger.LogInformation("[SeedExtra] Treatment check done.");

                // ══════════════════════════════════════════════════
                // E. INVOICES for the two new Treatments
                //    (checked by unique InvoiceNumber)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedExtra] Checking new Invoices...");

                if (treat1Id > 0)
                {
                    await AddInvoiceIfMissing(context, new Invoice
                    {
                        InvoiceNumber = "INV-2026-0005",
                        InvoiceDate   = appt1Date,
                        PatientId     = patCharlotteId,
                        TreatmentId   = treat1Id,
                        Amount        = 95.00m + 88.00m,    // treatment + consultation
                        Discount      = 5.00m,
                        Tax           = 5.00m,
                        NetAmount     = CalcNet(95.00m + 88.00m, 5.00m, 5.00m),
                        Status        = "Paid"
                    });
                }

                if (treat2Id > 0)
                {
                    await AddInvoiceIfMissing(context, new Invoice
                    {
                        InvoiceNumber = "INV-2026-0006",
                        InvoiceDate   = appt2Date,
                        PatientId     = patDanielId,
                        TreatmentId   = treat2Id,
                        Amount        = 60.00m + 92.00m,
                        Discount      = 0.00m,
                        Tax           = 5.00m,
                        NetAmount     = CalcNet(60.00m + 92.00m, 0.00m, 5.00m),
                        Status        = "Unpaid"
                    });
                }

                await context.SaveChangesAsync();
                logger.LogInformation("[SeedExtra] Invoice check done.");

                logger.LogInformation("[SeedExtra] Additive seeding completed — existing data untouched.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SeedExtra] An error occurred during additive seeding.");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // ESSENTIAL SEED  —  guarantees exactly 3 doctors and 5 patients are
        // always present in the database, regardless of prior seeding state.
        // Each record is checked individually by its unique business key
        // (DoctorNumber / NationalId) before inserting, so this method is fully
        // idempotent and safe to call on every application startup.
        // ══════════════════════════════════════════════════════════════════════════
        public static async Task SeedEssentialsAsync(ClinicDbContext context, ILogger logger)
        {
            try
            {
                // ── Resolve department IDs (required FK for doctors) ──────────────────────
                var deptIds = await context.Departments
                    .OrderBy(d => d.DepartmentId)
                    .Select(d => d.DepartmentId)
                    .ToListAsync();

                if (deptIds.Count < 1)
                {
                    logger.LogWarning("[SeedEssentials] No departments found — skipping essential doctor/patient seed.");
                    return;
                }

                // ══════════════════════════════════════════════════
                // A. THREE ESSENTIAL DOCTORS (by DoctorNumber)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedEssentials] Ensuring 3 essential doctors...");

                var essentialDoctors = new[]
                {
                    new Doctor
                    {
                        DoctorNumber    = "ESS-DR001",
                        DoctorName      = "Dr. Amelia Foster",
                        Specialization  = "Family Medicine & Primary Care",
                        ConsultationFee = 65.00m,
                        DepartmentId    = deptIds[0]
                    },
                    new Doctor
                    {
                        DoctorNumber    = "ESS-DR002",
                        DoctorName      = "Dr. Marcus Webb",
                        Specialization  = "Internal Medicine",
                        ConsultationFee = 78.00m,
                        DepartmentId    = deptIds.Count > 1 ? deptIds[1] : deptIds[0]
                    },
                    new Doctor
                    {
                        DoctorNumber    = "ESS-DR003",
                        DoctorName      = "Dr. Nadia Rahman",
                        Specialization  = "Obstetrics & Gynaecology",
                        ConsultationFee = 82.00m,
                        DepartmentId    = deptIds.Count > 2 ? deptIds[2] : deptIds[0]
                    },
                };

                foreach (var doc in essentialDoctors)
                {
                    if (!await context.Doctors.AnyAsync(d => d.DoctorNumber == doc.DoctorNumber))
                    {
                        await context.Doctors.AddAsync(doc);
                        logger.LogInformation("[SeedEssentials] Adding doctor: {DoctorName} ({DoctorNumber})",
                            doc.DoctorName, doc.DoctorNumber);
                    }
                }

                await context.SaveChangesAsync();
                logger.LogInformation("[SeedEssentials] Essential doctors check complete.");

                // ══════════════════════════════════════════════════
                // B. FIVE ESSENTIAL PATIENTS (by NationalId)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedEssentials] Ensuring 5 essential patients...");

                var essentialPatients = new[]
                {
                    new Patient
                    {
                        PatientName     = "James Thornton",
                        NationalId      = "ESS1000000001",
                        PhoneNumber     = "0711000001",
                        DOB             = new DateTime(1978, 3, 20),
                        BloodType       = "O+",
                        Allergies       = "None known",
                        ChronicDiseases = "Hypertension",
                        Notes           = "Routine blood-pressure monitoring every 3 months."
                    },
                    new Patient
                    {
                        PatientName     = "Clara Simmons",
                        NationalId      = "ESS1000000002",
                        PhoneNumber     = "0711000002",
                        DOB             = new DateTime(1990, 7, 5),
                        BloodType       = "A+",
                        Allergies       = "Penicillin",
                        ChronicDiseases = "Mild anaemia",
                        Notes           = "Iron supplements prescribed. Avoid penicillin-based antibiotics."
                    },
                    new Patient
                    {
                        PatientName     = "David Nguyen",
                        NationalId      = "ESS1000000003",
                        PhoneNumber     = "0711000003",
                        DOB             = new DateTime(1965, 11, 12),
                        BloodType       = "B+",
                        Allergies       = "Aspirin",
                        ChronicDiseases = "Type-2 Diabetes, Hyperlipidaemia",
                        Notes           = "On Metformin 1g BD and Atorvastatin 20mg OD. Annual HbA1c required."
                    },
                    new Patient
                    {
                        PatientName     = "Sophie Laurent",
                        NationalId      = "ESS1000000004",
                        PhoneNumber     = "0711000004",
                        DOB             = new DateTime(2003, 1, 28),
                        BloodType       = "AB+",
                        Allergies       = "None known",
                        ChronicDiseases = "None",
                        Notes           = "Young adult; preventive care only. Up-to-date on vaccinations."
                    },
                    new Patient
                    {
                        PatientName     = "Robert Osei",
                        NationalId      = "ESS1000000005",
                        PhoneNumber     = "0711000005",
                        DOB             = new DateTime(1953, 9, 17),
                        BloodType       = "A-",
                        Allergies       = "Iodine contrast dye, Codeine",
                        ChronicDiseases = "Chronic obstructive pulmonary disease, Osteoarthritis",
                        Notes           = "Requires spirometry at each visit. Avoid contrast imaging and opioids."
                    },
                };

                foreach (var patient in essentialPatients)
                {
                    if (!await context.Patients.AnyAsync(p => p.NationalId == patient.NationalId))
                    {
                        await context.Patients.AddAsync(patient);
                        logger.LogInformation("[SeedEssentials] Adding patient: {PatientName} ({NationalId})",
                            patient.PatientName, patient.NationalId);
                    }
                }

                await context.SaveChangesAsync();
                logger.LogInformation("[SeedEssentials] Essential patients check complete.");

                // ── Resolve the IDs we need for FK relationships ─────────────────────────
                int drAmeliaId  = await context.Doctors.Where(d => d.DoctorNumber == "ESS-DR001").Select(d => d.DoctorId).FirstOrDefaultAsync();
                int drMarcusId  = await context.Doctors.Where(d => d.DoctorNumber == "ESS-DR002").Select(d => d.DoctorId).FirstOrDefaultAsync();
                int drNadiaId   = await context.Doctors.Where(d => d.DoctorNumber == "ESS-DR003").Select(d => d.DoctorId).FirstOrDefaultAsync();

                int patJamesId   = await context.Patients.Where(p => p.NationalId == "ESS1000000001").Select(p => p.PatientId).FirstOrDefaultAsync();
                int patClaraId   = await context.Patients.Where(p => p.NationalId == "ESS1000000002").Select(p => p.PatientId).FirstOrDefaultAsync();
                int patDavidId   = await context.Patients.Where(p => p.NationalId == "ESS1000000003").Select(p => p.PatientId).FirstOrDefaultAsync();
                int patSophieId  = await context.Patients.Where(p => p.NationalId == "ESS1000000004").Select(p => p.PatientId).FirstOrDefaultAsync();
                int patRobertId  = await context.Patients.Where(p => p.NationalId == "ESS1000000005").Select(p => p.PatientId).FirstOrDefaultAsync();

                if (drAmeliaId == 0 || drMarcusId == 0 || drNadiaId == 0 ||
                    patJamesId == 0 || patClaraId == 0 || patDavidId == 0 || patSophieId == 0 || patRobertId == 0)
                {
                    logger.LogWarning("[SeedEssentials] Could not resolve all essential doctor/patient IDs — skipping appointments/treatments/invoices.");
                    return;
                }

                var today = DateTime.Today;

                // ══════════════════════════════════════════════════
                // C. EIGHT ESSENTIAL APPOINTMENTS
                //    Checked by Doctor + Patient + Date + Time combo
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedEssentials] Ensuring essential appointments...");

                // Helper: insert appointment only if the exact slot doesn't already exist; returns the saved AppointmentId.
                static async Task<int> EnsureAppointment(ClinicDbContext ctx, int doctorId, int patientId,
                    DateTime date, TimeSpan time, string status, string notes)
                {
                    var existing = await ctx.Appointments
                        .Where(a => a.DoctorId == doctorId && a.PatientId == patientId
                                 && a.AppointmentDate == date && a.AppointmentTime == time)
                        .Select(a => a.AppointmentId)
                        .FirstOrDefaultAsync();

                    if (existing != 0) return existing;

                    var appt = new Appointment
                    {
                        DoctorId        = doctorId,
                        PatientId       = patientId,
                        AppointmentDate = date,
                        AppointmentTime = time,
                        Status          = status,
                        Notes           = notes
                    };
                    await ctx.Appointments.AddAsync(appt);
                    await ctx.SaveChangesAsync();
                    return appt.AppointmentId;
                }

                // ── Past / Completed ─────────────────────────────────────────────────────
                // 1. James Thornton — hypertension check with Dr. Amelia Foster
                int apptJamesBP = await EnsureAppointment(context, drAmeliaId, patJamesId,
                    today.AddDays(-45), new TimeSpan(9, 0, 0), "Completed",
                    "Routine hypertension review. BP 148/92 on arrival. Medication adjustment discussed.");

                // 2. David Nguyen — diabetes quarterly review with Dr. Marcus Webb
                int apptDavidDM = await EnsureAppointment(context, drMarcusId, patDavidId,
                    today.AddDays(-30), new TimeSpan(10, 30, 0), "Completed",
                    "Quarterly HbA1c review. Patient reports occasional hypoglycaemia episodes in the mornings.");

                // 3. Robert Osei — COPD spirometry with Dr. Marcus Webb
                int apptRobertCOPD = await EnsureAppointment(context, drMarcusId, patRobertId,
                    today.AddDays(-20), new TimeSpan(11, 0, 0), "Completed",
                    "COPD follow-up spirometry. FEV1 58% predicted. Rescue inhaler usage increased over past month.");

                // 4. Robert Osei — osteoarthritis assessment with Dr. Amelia Foster
                int apptRobertOA = await EnsureAppointment(context, drAmeliaId, patRobertId,
                    today.AddDays(-10), new TimeSpan(14, 0, 0), "Completed",
                    "Osteoarthritis knee bilateral. Pain scale 6/10. Physiotherapy referral under discussion.");

                // ── Upcoming / Confirmed ─────────────────────────────────────────────────
                // 5. Clara Simmons — anaemia follow-up with Dr. Amelia Foster
                int apptClaraAnaemia = await EnsureAppointment(context, drAmeliaId, patClaraId,
                    today.AddDays(4), new TimeSpan(9, 30, 0), "Confirmed",
                    "Follow-up blood count after 8 weeks of iron supplementation.");

                // 6. Sophie Laurent — routine wellness check with Dr. Nadia Rahman
                int apptSophieWellness = await EnsureAppointment(context, drNadiaId, patSophieId,
                    today.AddDays(7), new TimeSpan(11, 0, 0), "Confirmed",
                    "Annual well-woman check. Cervical smear and routine bloods requested.");

                // ── Upcoming / Pending ───────────────────────────────────────────────────
                // 7. James Thornton — specialist internal medicine referral with Dr. Marcus Webb
                int apptJamesInternal = await EnsureAppointment(context, drMarcusId, patJamesId,
                    today.AddDays(12), new TimeSpan(13, 0, 0), "Pending",
                    "Referral from Dr. Foster for resistant hypertension workup — 24-hr ambulatory BP monitoring.");

                // 8. David Nguyen — lipid clinic with Dr. Marcus Webb
                int apptDavidLipid = await EnsureAppointment(context, drMarcusId, patDavidId,
                    today.AddDays(18), new TimeSpan(15, 30, 0), "Pending",
                    "Lipid profile review post statin dose increase. Liver function tests requested.");

                logger.LogInformation("[SeedEssentials] Essential appointments check complete.");

                // ══════════════════════════════════════════════════
                // D. FOUR TREATMENTS for the completed appointments
                //    Checked by AppointmentId (one treatment per appointment)
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedEssentials] Ensuring essential treatments...");

                static decimal CalcNet(decimal amount, decimal discount, decimal tax)
                    => Math.Round(amount - discount + (amount - discount) * tax / 100, 2);

                // Helper: insert treatment only if this appointment has none yet; returns TreatmentId.
                static async Task<int> EnsureTreatment(ClinicDbContext ctx, int appointmentId,
                    DateTime treatDate, string desc, decimal cost, string diagnosis, string prescription)
                {
                    var existing = await ctx.Treatments
                        .Where(t => t.AppointmentId == appointmentId)
                        .Select(t => t.TreatmentId)
                        .FirstOrDefaultAsync();

                    if (existing != 0) return existing;

                    var treat = new Treatment
                    {
                        AppointmentId     = appointmentId,
                        TreatmentDate     = treatDate,
                        TreatmentDesc     = desc,
                        TreatmentCost     = cost,
                        Diagnosis         = diagnosis,
                        PrescriptionNotes = prescription
                    };
                    await ctx.Treatments.AddAsync(treat);
                    await ctx.SaveChangesAsync();
                    return treat.TreatmentId;
                }

                int treatJamesBP = 0;
                if (apptJamesBP > 0)
                    treatJamesBP = await EnsureTreatment(context, apptJamesBP,
                        today.AddDays(-45),
                        "BP measured 148/92. 12-lead ECG normal sinus rhythm. Sodium and fluid intake counselled. " +
                        "Dietary DASH plan printed and reviewed with patient.",
                        75.00m,
                        "Stage 2 primary hypertension — suboptimal control on current regimen.",
                        "Increase Amlodipine from 5mg to 10mg OD. Add Indapamide 1.5mg OD. Recheck in 6 weeks.");

                int treatDavidDM = 0;
                if (apptDavidDM > 0)
                    treatDavidDM = await EnsureTreatment(context, apptDavidDM,
                        today.AddDays(-30),
                        "HbA1c result 8.4% (target < 7.0%). Fasting glucose 11.2 mmol/L. Foot examination — no ulcers. " +
                        "Lipid profile: LDL 3.8 mmol/L. Medication compliance discussed.",
                        85.00m,
                        "Type-2 Diabetes Mellitus — inadequate glycaemic and lipid control.",
                        "Add Empagliflozin 10mg OD. Increase Atorvastatin from 20mg to 40mg OD. " +
                        "Dietitian referral. Repeat HbA1c and LFTs in 3 months.");

                int treatRobertCOPD = 0;
                if (apptRobertCOPD > 0)
                    treatRobertCOPD = await EnsureTreatment(context, apptRobertCOPD,
                        today.AddDays(-20),
                        "Spirometry: FEV1 58% predicted, FVC 72%, FEV1/FVC ratio 0.61 — GOLD Stage 2. " +
                        "Oxygen saturation 94% at rest. Chest auscultation: mild end-expiratory wheeze bilaterally. " +
                        "Rescue inhaler technique reassessed and corrected.",
                        95.00m,
                        "Moderate COPD (GOLD Stage 2) with worsening exertional dyspnoea.",
                        "Add LAMA: Tiotropium 18mcg inhaled OD. Continue Salbutamol MDI PRN. " +
                        "Pulmonary rehab referral. Influenza and pneumococcal vaccines booked. Review in 8 weeks.");

                int treatRobertOA = 0;
                if (apptRobertOA > 0)
                    treatRobertOA = await EnsureTreatment(context, apptRobertOA,
                        today.AddDays(-10),
                        "Bilateral knee examination: crepitus present, ROM 0–100° left, 0–95° right. " +
                        "X-ray review shows grade 3 medial compartment narrowing. NSAID avoidance due to COPD. " +
                        "Weight and BMI discussed (BMI 28.4).",
                        70.00m,
                        "Bilateral osteoarthritis knees — grade 3, right > left. NSAID contraindicated (COPD).",
                        "Paracetamol 1g QDS regularly. Topical Diclofenac gel BD to knees. " +
                        "Physiotherapy referral (6 sessions). Review in 12 weeks or sooner if pain worsens.");

                logger.LogInformation("[SeedEssentials] Essential treatments check complete.");

                // ══════════════════════════════════════════════════
                // E. SIX INVOICES — paid and unpaid
                //    Checked by unique InvoiceNumber
                // ══════════════════════════════════════════════════
                logger.LogInformation("[SeedEssentials] Ensuring essential invoices...");

                var invoiceDefs = new[]
                {
                    // Paid
                    new { Num = "ESS-INV-001", Date = today.AddDays(-45), PatId = patJamesId,
                          TreatId = treatJamesBP,    Amount = 75.00m + 65.00m,  Disc = 10.00m, Tax = 5.0m, Status = "Paid"    },
                    // Paid
                    new { Num = "ESS-INV-002", Date = today.AddDays(-30), PatId = patDavidId,
                          TreatId = treatDavidDM,    Amount = 85.00m + 78.00m,  Disc =  0.00m, Tax = 5.0m, Status = "Paid"    },
                    // Unpaid
                    new { Num = "ESS-INV-003", Date = today.AddDays(-20), PatId = patRobertId,
                          TreatId = treatRobertCOPD, Amount = 95.00m + 78.00m,  Disc =  5.00m, Tax = 5.0m, Status = "Unpaid"  },
                    // Paid
                    new { Num = "ESS-INV-004", Date = today.AddDays(-10), PatId = patRobertId,
                          TreatId = treatRobertOA,   Amount = 70.00m + 65.00m,  Disc = 15.00m, Tax = 5.0m, Status = "Paid"    },
                };

                foreach (var inv in invoiceDefs)
                {
                    if (inv.TreatId == 0) continue;   // treatment wasn't inserted — skip

                    if (!await context.Invoices.AnyAsync(i => i.InvoiceNumber == inv.Num))
                    {
                        await context.Invoices.AddAsync(new Invoice
                        {
                            InvoiceNumber = inv.Num,
                            InvoiceDate   = inv.Date,
                            PatientId     = inv.PatId,
                            TreatmentId   = inv.TreatId,
                            Amount        = inv.Amount,
                            Discount      = inv.Disc,
                            Tax           = inv.Tax,
                            NetAmount     = CalcNet(inv.Amount, inv.Disc, inv.Tax),
                            Status        = inv.Status
                        });
                        logger.LogInformation("[SeedEssentials] Adding invoice {InvoiceNumber} ({Status})", inv.Num, inv.Status);
                    }
                }

                await context.SaveChangesAsync();
                logger.LogInformation("[SeedEssentials] Essential invoices check complete.");

                logger.LogInformation("[SeedEssentials] Essential seed finished — all UI tables populated.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SeedEssentials] An error occurred during essential seeding.");
                throw;
            }
        }
    }
}
