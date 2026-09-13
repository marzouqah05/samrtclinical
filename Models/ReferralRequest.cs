using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    public enum ReferralStatus
    {
        Pending,
        Scheduled,
        Rejected
    }

    [Table("ReferralRequests")]
    public class ReferralRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid? ClinicId { get; set; }

        [Required]
        public int PatientId { get; set; }
        public Patient? Patient { get; set; }

        [Required]
        public int FromDoctorId { get; set; }
        public Doctor? FromDoctor { get; set; }

        [Required]
        public int TargetDepartmentId { get; set; }
        public Department? TargetDepartment { get; set; }

        public int? TargetDoctorId { get; set; }
        public Doctor? TargetDoctor { get; set; }

        [Required]
        [MaxLength(1000)]
        public string ReferralReason { get; set; } = string.Empty;

        public ReferralStatus Status { get; set; } = ReferralStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
