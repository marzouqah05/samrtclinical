using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    /// <summary>
    /// Standalone clinical visit / medical record entry for a patient.
    /// Kept separate from the Appointment+Treatment chain so free-form
    /// history (imported records, older visits, external referrals) can
    /// be recorded without requiring a scheduled appointment.
    /// </summary>
    [Table("MedicalRecords")]
    public class MedicalRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        /// <summary>DoctorId – nullable so a record can exist before a doctor is assigned.</summary>
        public int? DoctorId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime VisitDate { get; set; } = DateTime.Today;

        [Required]
        [MaxLength(500)]
        [Column(TypeName = "varchar(500)")]
        public string ChiefComplaint { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Diagnosis { get; set; }

        [Column(TypeName = "text")]
        public string? TreatmentPlan { get; set; }

        [Column(TypeName = "text")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation Properties ──────────────────────────────────────────────
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }

        public List<PatientAttachment> Attachments { get; set; } = new();
    }
}
