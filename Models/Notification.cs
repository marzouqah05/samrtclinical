using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid? ClinicId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "Referral"; // "Referral", "FollowUp", "Appointment"

        [Required]
        [MaxLength(50)]
        public string TargetRole { get; set; } = "Receptionist";

        public int? PatientId { get; set; }
        public Patient? Patient { get; set; }

        public int? ReferralRequestId { get; set; }
        public ReferralRequest? ReferralRequest { get; set; }

        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        [MaxLength(150)]
        public string? DoctorName { get; set; }

        [MaxLength(150)]
        public string? PatientName { get; set; }

        [MaxLength(150)]
        public string? DepartmentName { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
