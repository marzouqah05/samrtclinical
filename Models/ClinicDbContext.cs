using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models
{
    public class ClinicDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public ClinicDbContext(DbContextOptions<ClinicDbContext> options)
            : base(options)
        {
        }

        // جداول مشروع العيادة (MediCare)
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Treatment> Treatments { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<ClinicSetting> ClinicSettings { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<UserSessionLog> UserSessionLogs { get; set; }
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<PatientAttachment> PatientAttachments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ensure unique index on Email column for IdentityUser
            modelBuilder.Entity<IdentityUser>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
            });

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
                entity.Property(e => e.DurationMinutes).HasColumnType("double precision");

                // Composite index: looking up active sessions by user is the hottest query path
                entity.HasIndex(e => new { e.UserId, e.IsActive });
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
        }
    }
}