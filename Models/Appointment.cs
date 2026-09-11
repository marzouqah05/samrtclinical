using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("Appointments")]
    public class Appointment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AppointmentId { get; set; }

        // Appointment Date
        [Required]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        // Appointment Time
        [Required]
        [DataType(DataType.Time)]
        public TimeSpan AppointmentTime { get; set; }

        // Doctor
        [Required]
        public int DoctorId { get; set; }

        // Patient
        [Required]
        public int PatientId { get; set; }

        // Status
        [Required]
        [Column(TypeName = "varchar(50)")]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        // Notes
        [Column(TypeName = "text")]
        public string? Notes { get; set; }

        // ── Weekend / Holiday Exception Override ────────────────────────────
        [NotMapped]
        public bool IsWeekendOverride { get; set; } = false;

        // ── WhatsApp Notification Tracking ──────────────────────────────────
        /// <summary>True once a pre-appointment reminder has been dispatched via WhatsApp.</summary>
        public bool IsReminderSent { get; set; } = false;

        /// <summary>True once a post-visit follow-up message has been dispatched via WhatsApp.</summary>
        public bool IsFollowUpSent { get; set; } = false;

        /// <summary>UTC timestamp of when the follow-up message was sent; null if not yet sent.</summary>
        public DateTime? FollowUpSentAt { get; set; }

        // Navigation Properties
        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }

        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        public Treatment? Treatment { get; set; }
    }
}