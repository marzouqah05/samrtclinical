using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("ClinicSettings")]
    public class ClinicSetting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Value { get; set; }

        [MaxLength(250)]
        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
