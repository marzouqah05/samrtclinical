using System;
using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class PatientDetailsViewModel
    {
        public Patient Patient { get; set; } = new Patient();
        public List<Appointment> Appointments { get; set; } = new();
        public List<Treatment> Treatments { get; set; } = new();
        public List<MedicalRecord> MedicalRecords { get; set; } = new();
        public List<PatientAttachment> Attachments { get; set; } = new();
        public List<Doctor> Doctors { get; set; } = new(); // for AddMedicalRecord dropdown
    }
}