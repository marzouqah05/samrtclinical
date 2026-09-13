using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("Departments", Schema = "dbo")]
    public class Department
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DepartmentId { get; set; }

        [Required]
        [Column(TypeName = "varchar(150)")]
        public string DepartmentName { get; set; } = string.Empty;

        [Column(TypeName = "varchar(20)")]
        [MaxLength(20)]
        public string? DepartmentAbbr { get; set; }

        public Guid? ClinicId { get; set; }

        public ICollection<Specialty> Specialties { get; set; } = new List<Specialty>();
        public List<Doctor> Doctors { get; set; } = new List<Doctor>();
    }
}