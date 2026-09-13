using System;

namespace WebApplication1.Models
{
    public class PatientDto
    {
        public int PatientId { get; set; }
        public string PatientNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime DOB { get; set; }
        public string? Gender { get; set; }
        public string? BloodType { get; set; }
        public string? Allergies { get; set; }
        public string? ChronicDiseases { get; set; }
        public string? Notes { get; set; }
        public string? TelegramChatId { get; set; }
        public Guid? ClinicId { get; set; }
    }

    public class CreatePatientDto
    {
        public string PatientName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime DOB { get; set; }
        public string? Gender { get; set; }
        public string? BloodType { get; set; }
        public string? Allergies { get; set; }
        public string? ChronicDiseases { get; set; }
        public string? Notes { get; set; }
        public string? TelegramChatId { get; set; }
    }

    public class UpdatePatientDto
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime DOB { get; set; }
        public string? Gender { get; set; }
        public string? BloodType { get; set; }
        public string? Allergies { get; set; }
        public string? ChronicDiseases { get; set; }
        public string? Notes { get; set; }
        public string? TelegramChatId { get; set; }
    }
}
