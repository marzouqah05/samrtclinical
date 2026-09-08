using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("Treatments")]
    public class Treatment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TreatmentId { get; set; }

        [Required]
        public int AppointmentId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime TreatmentDate { get; set; }

        [Required]
        [Column(TypeName = "varchar(200)")]
        [MaxLength(200)]
        public string TreatmentDesc { get; set; } = string.Empty;

        [Column(TypeName = "decimal(12,2)")]
        public decimal TreatmentCost { get; set; }

        [Column(TypeName = "text")]
        public string? Diagnosis { get; set; }

        [Column(TypeName = "text")]
        public string? PrescriptionNotes { get; set; }

        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }
    }
}