using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("Patients")]
    public class Patient
    {
        public Patient()
        {
            // توليد رقم مريض فريد عند إنشاء كائن جديد
            PatientNumber = GenerateUniquePatientNumber();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PatientId { get; set; }

        [Display(Name = "Patient ID")]
        [Column(TypeName = "varchar(50)")]
        [MaxLength(50)]
        public string PatientNumber { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 150 characters.")]
        [Display(Name = "Full Name")]
        [Column(TypeName = "varchar(150)")]
        public string PatientName { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "National ID must be valid.")]
        [Display(Name = "National ID")]
        [Column(TypeName = "varchar(50)")]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(50, MinimumLength = 7, ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone")]
        [Column(TypeName = "varchar(50)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of Birth is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DOB { get; set; }

        // --- الحقول الطبية الجديدة ---

        [Display(Name = "Blood Type")]
        [StringLength(10)]
        [Column(TypeName = "varchar(10)")]
        public string? BloodType { get; set; }

        [Display(Name = "Allergies")]
        [DataType(DataType.MultilineText)]
        [Column(TypeName = "text")]
        public string? Allergies { get; set; }

        [Display(Name = "Chronic Diseases")]
        [DataType(DataType.MultilineText)]
        [Column(TypeName = "text")]
        public string? ChronicDiseases { get; set; }

        [Display(Name = "Medical Notes")]
        [DataType(DataType.MultilineText)]
        [Column(TypeName = "text")]
        public string? Notes { get; set; }

        // --- العلاقات (Navigation Properties) ---
        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public List<Treatment> Treatments { get; set; } = new List<Treatment>();

        // دالة توليد رقم فريد لتجنب التكرار
        private static string GenerateUniquePatientNumber()
        {
            // نستخدم جزءاً من Guid لضمان العشوائية وعدم التكرار نهائياً
            return "P" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        }
    }
}