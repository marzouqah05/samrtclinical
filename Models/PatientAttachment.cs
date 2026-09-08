using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    /// <summary>
    /// Stores metadata for files uploaded against a patient (images, PDFs, lab results, etc.).
    /// The physical file lives under wwwroot/uploads/patients/{PatientId}/.
    /// </summary>
    [Table("PatientAttachments")]
    public class PatientAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        /// <summary>Optional link to a specific MedicalRecord entry.</summary>
        public int? MedicalRecordId { get; set; }

        /// <summary>Original file name (sanitised before saving).</summary>
        [Required]
        [MaxLength(260)]
        [Column(TypeName = "varchar(260)")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>Server-relative path from wwwroot, e.g. /uploads/patients/3/abc.pdf</summary>
        [Required]
        [MaxLength(500)]
        [Column(TypeName = "varchar(500)")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Mime / content type (image/jpeg, image/png, application/pdf).</summary>
        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string FileType { get; set; } = string.Empty;

        /// <summary>Human-facing category tag.</summary>
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Category { get; set; } = "Other"; // X-Ray | Lab Result | Prescription | Other

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation Properties ──────────────────────────────────────────────
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        [ForeignKey("MedicalRecordId")]
        public MedicalRecord? MedicalRecord { get; set; }
    }
}
