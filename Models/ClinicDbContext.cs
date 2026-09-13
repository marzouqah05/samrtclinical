using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Services;

namespace WebApplication1.Models
{
    public class ClinicDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ClinicDbContext(DbContextOptions<ClinicDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? CurrentClinicId
        {
            get
            {
                var user = _httpContextAccessor?.HttpContext?.User;
                if (user == null) return null;
                var id = user.GetClinicId();
                return id == Guid.Empty ? null : id;
            }
        }

        public bool IsSuperAdminUser
        {
            get
            {
                var user = _httpContextAccessor?.HttpContext?.User;
                if (user == null) return true; // Background tasks, migrations, and CLI bypass filter
                return user.IsSuperAdmin();
            }
        }

        // جداول مشروع العيادة (MediCare)
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Treatment> Treatments { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Clinic> Clinics { get; set; }
        public DbSet<ClinicSetting> ClinicSettings { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<UserSessionLog> UserSessionLogs { get; set; }
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<PatientAttachment> PatientAttachments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ReferralRequest> ReferralRequests { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ensure unique index on Email column for ApplicationUser
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // ── Global Query Filters for Strict Multi-Tenancy Isolation ────────
            modelBuilder.Entity<Patient>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));
            modelBuilder.Entity<Appointment>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));
            modelBuilder.Entity<Doctor>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));
            modelBuilder.Entity<Department>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));
            modelBuilder.Entity<Invoice>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));
            modelBuilder.Entity<Expense>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));
            modelBuilder.Entity<ReferralRequest>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));
            modelBuilder.Entity<Notification>().HasQueryFilter(e => IsSuperAdminUser || (CurrentClinicId != null && e.ClinicId == CurrentClinicId));

            // إعدادات جدول الـ Department
            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("Departments");
                entity.HasKey(e => e.DepartmentId);
                entity.Property(e => e.DepartmentName).IsRequired().HasColumnType("varchar(150)");
                entity.Property(e => e.DepartmentAbbr).HasColumnType("varchar(20)");
            });

            // إعدادات جدول الـ Doctor
            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.HasKey(e => e.DoctorId);
                entity.Property(e => e.DoctorName).IsRequired().HasColumnType("varchar(150)");
                entity.Property(e => e.DoctorNumber).IsRequired().HasColumnType("varchar(50)");
                entity.Property(e => e.Specialization).IsRequired().HasColumnType("varchar(100)");
                entity.Property(e => e.ConsultationFee).HasColumnType("decimal(12,2)");

                entity.HasOne(d => d.Department)
                      .WithMany(p => p.Doctors)
                      .HasForeignKey(d => d.DepartmentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // إعدادات جدول الـ Patient
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(e => e.PatientId);
                entity.Property(e => e.PatientName).IsRequired().HasMaxLength(150);
                entity.Property(e => e.PatientNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.NationalId).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.NationalId).IsUnique();
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.BloodType).HasMaxLength(10);
                entity.Property(e => e.Allergies).HasColumnType("text");
                entity.Property(e => e.ChronicDiseases).HasColumnType("text");
                entity.Property(e => e.Notes).HasColumnType("text");
            });

            // إعدادات جدول الـ Appointment
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasKey(e => e.AppointmentId);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Notes).HasColumnType("text");

                entity.HasOne(a => a.Doctor)
                      .WithMany(d => d.Appointments)
                      .HasForeignKey(a => a.DoctorId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Patient)
                      .WithMany(p => p.Appointments)
                      .HasForeignKey(a => a.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.ReferredByDoctor)
                      .WithMany()
                      .HasForeignKey(a => a.ReferredByDoctorId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(a => a.Department)
                      .WithMany()
                      .HasForeignKey(a => a.DepartmentId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // إعدادات جدول الـ Treatment
            modelBuilder.Entity<Treatment>(entity =>
            {
                entity.HasKey(e => e.TreatmentId);
                entity.Property(e => e.TreatmentDesc).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Diagnosis).HasColumnType("text");
                entity.Property(e => e.PrescriptionNotes).HasColumnType("text");
                entity.Property(e => e.TreatmentCost).HasColumnType("decimal(12,2)");

                entity.HasOne(t => t.Appointment)
                      .WithOne(a => a.Treatment)
                      .HasForeignKey<Treatment>(t => t.AppointmentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // إعدادات جدول الـ Invoice
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.InvoiceId);
                entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Amount).HasColumnType("decimal(12,2)");
                entity.Property(e => e.Discount).HasColumnType("decimal(12,2)");
                entity.Property(e => e.Tax).HasColumnType("decimal(12,2)");
                entity.Property(e => e.NetAmount).HasColumnType("decimal(12,2)");

                entity.HasOne(i => i.Patient)
                      .WithMany()
                      .HasForeignKey(i => i.PatientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Treatment)
                      .WithMany()
                      .HasForeignKey(i => i.TreatmentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // إعدادات جدول المصروفات
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);
                entity.Property(e => e.VendorOrPayee).HasMaxLength(200);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.ReceiptAttachmentPath).HasMaxLength(500);
                entity.Property(e => e.Amount).HasColumnType("decimal(12,2)");
            });

            // إعدادات جدول UserSessionLog
            modelBuilder.Entity<UserSessionLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.UserName).IsRequired().HasMaxLength(256);
                entity.Property(e => e.UserRole).HasMaxLength(100);
                entity.Property(e => e.IpAddress).HasMaxLength(50);
                entity.Property(e => e.UserAgent).HasMaxLength(512);
                entity.Property(e => e.SessionId).HasMaxLength(100);
                entity.Property(e => e.DeviceType).HasMaxLength(50);
                entity.Property(e => e.DurationMinutes).HasColumnType("double precision");

                // Composite index: looking up active sessions by user is the hottest query path
                entity.HasIndex(e => new { e.UserId, e.IsActive });
                entity.HasIndex(e => new { e.UserId, e.SessionId, e.IsActive });
                entity.HasIndex(e => e.LoginTime);
            });

            // ── MedicalRecord ───────────────────────────────────────────────
            modelBuilder.Entity<MedicalRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChiefComplaint).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Diagnosis).HasColumnType("text");
                entity.Property(e => e.TreatmentPlan).HasColumnType("text");
                entity.Property(e => e.Notes).HasColumnType("text");
                entity.HasIndex(e => e.PatientId);

                entity.HasOne(r => r.Patient)
                      .WithMany()
                      .HasForeignKey(r => r.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Doctor)
                      .WithMany()
                      .HasForeignKey(r => r.DoctorId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── PatientAttachment ───────────────────────────────────────────
            modelBuilder.Entity<PatientAttachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileType).HasMaxLength(100);
                entity.Property(e => e.Category).HasMaxLength(50);
                entity.HasIndex(e => e.PatientId);

                entity.HasOne(a => a.Patient)
                      .WithMany()
                      .HasForeignKey(a => a.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.MedicalRecord)
                      .WithMany(r => r.Attachments)
                      .HasForeignKey(a => a.MedicalRecordId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── AuditLog ─────────────────────────────────────────────────────
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
                entity.Property(e => e.EntityName).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.EntityName);
            });

            // ── Clinic ───────────────────────────────────────────────────────
            modelBuilder.Entity<Clinic>(entity =>
            {
                entity.ToTable("Clinics");
                entity.HasKey(e => e.ClinicId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.OwnerEmail).HasMaxLength(200);
                entity.HasIndex(e => e.OwnerEmail);
            });

            // ── ApplicationUser Tenant Mapping ────────────────────────────────
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.ClinicId).HasColumnType("uuid");
                entity.HasIndex(u => u.ClinicId);
            });

            // ── ReferralRequest ──────────────────────────────────────────────
            modelBuilder.Entity<ReferralRequest>(entity =>
            {
                entity.ToTable("ReferralRequests");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReferralReason).IsRequired().HasMaxLength(1000);
                entity.HasIndex(e => e.ClinicId);
                entity.HasIndex(e => e.Status);

                entity.HasOne(r => r.Patient)
                      .WithMany()
                      .HasForeignKey(r => r.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.FromDoctor)
                      .WithMany()
                      .HasForeignKey(r => r.FromDoctorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.TargetDepartment)
                      .WithMany()
                      .HasForeignKey(r => r.TargetDepartmentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.TargetDoctor)
                      .WithMany()
                      .HasForeignKey(r => r.TargetDoctorId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Notification ─────────────────────────────────────────────────
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TargetRole).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.ClinicId);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(n => n.Patient)
                      .WithMany()
                      .HasForeignKey(n => n.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.ReferralRequest)
                      .WithMany()
                      .HasForeignKey(n => n.ReferralRequestId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.Appointment)
                      .WithMany()
                      .HasForeignKey(n => n.AppointmentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var clinicId = CurrentClinicId;
            if (clinicId.HasValue && clinicId.Value != Guid.Empty)
            {
                foreach (var entry in ChangeTracker.Entries())
                {
                    if (entry.State == EntityState.Added)
                    {
                        var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "ClinicId");
                        if (prop != null && (prop.CurrentValue == null || (prop.CurrentValue is Guid g && g == Guid.Empty)))
                        {
                            prop.CurrentValue = clinicId.Value;
                        }
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}