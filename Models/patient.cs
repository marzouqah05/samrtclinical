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
        [Column(TypeName = "nvarchar(10)")]
        [MaxLength(10)]
        public string PatientNumber { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 150 characters.")]
        [Display(Name = "Full Name")]
        [Column(TypeName = "nvarchar(150)")]
        public string PatientName { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID is required.")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "National ID must be exactly 10 digits.")]
        [Display(Name = "National ID")]
        [Column(TypeName = "nvarchar(10)")]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(15, MinimumLength = 9, ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone")]
        [Column(TypeName = "nvarchar(15)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of Birth is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DOB { get; set; }

        // --- الحقول الطبية الجديدة ---

        [Display(Name = "Blood Type")]
        [StringLength(5)]
        [Column(TypeName = "nvarchar(5)")]
        public string? BloodType { get; set; }

        [Display(Name = "Allergies")]
        [DataType(DataType.MultilineText)]
        [Column(TypeName = "nvarchar(max)")]
        public string? Allergies { get; set; }

        [Display(Name = "Chronic Diseases")]
        [DataType(DataType.MultilineText)]
        [Column(TypeName = "nvarchar(max)")]
        public string? ChronicDiseases { get; set; }

        [Display(Name = "Medical Notes")]
        [DataType(DataType.MultilineText)]
        [Column(TypeName = "nvarchar(max)")]
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