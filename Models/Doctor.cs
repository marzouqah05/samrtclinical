using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    public class Doctor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DoctorId { get; set; }

        [Required]
        [Column(TypeName = "varchar(50)")]
        [MaxLength(50)]
        public string DoctorNumber { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "varchar(150)")]
        public string DoctorName { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "varchar(100)")]
        public string Specialization { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal ConsultationFee { get; set; }

        [Required]
        public int DepartmentId { get; set; }

        public Department? Department { get; set; }

        public int? SpecialtyId { get; set; }

        public Specialty? Specialty { get; set; }

        /// <summary>Email address used for sending the weekly schedule digest.</summary>
        [Column(TypeName = "varchar(150)")]
        [MaxLength(150)]
        public string? DoctorEmail { get; set; }

        /// <summary>WhatsApp/phone number for the doctor (used in schedule payloads).</summary>
        [Column(TypeName = "varchar(30)")]
        [MaxLength(30)]
        public string? DoctorPhone { get; set; }

        /// <summary>Telegram Chat ID for the doctor — used by the Telegram Bot to dispatch direct appointment alert notifications.</summary>
        [Column(TypeName = "varchar(100)")]
        [MaxLength(100)]
        public string? TelegramChatId { get; set; }

        public Guid? ClinicId { get; set; }

        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}