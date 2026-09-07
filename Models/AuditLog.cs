using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(128)]
        public string? UserId { get; set; }

        [MaxLength(128)]
        public string? UserName { get; set; }

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? EntityId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string? Details { get; set; }
    }
}
