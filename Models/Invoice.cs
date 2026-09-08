using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [Required]
        [Column(TypeName = "varchar(20)")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Discount { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal Tax { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal NetAmount { get; set; }

        [Required]
        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string Status { get; set; } = "Unpaid";

        // العلاقات
        [Required]
        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        public virtual Patient? Patient { get; set; }

        [Required]
        public int TreatmentId { get; set; }
        [ForeignKey("TreatmentId")]
        public virtual Treatment? Treatment { get; set; }
    }
}