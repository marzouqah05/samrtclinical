using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    public enum ExpenseCategory
    {
        [Display(Name = "Medical Supplies")]
        MedicalSupplies,

        [Display(Name = "Equipment & Maintenance")]
        EquipmentMaintenance,

        [Display(Name = "Rent & Utilities")]
        RentUtilities,

        [Display(Name = "Salaries")]
        Salaries,

        [Display(Name = "Lab Fees")]
        LabFees,

        [Display(Name = "Other")]
        Other
    }

    [Table("Expenses")]
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [MaxLength(200)]
        [Display(Name = "Expense Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Category")]
        public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        [Range(0.01, 9999999, ErrorMessage = "Amount must be greater than 0")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Expense Date")]
        public DateTime ExpenseDate { get; set; } = DateTime.Today;

        [Required]
        [MaxLength(50)]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        [MaxLength(200)]
        [Display(Name = "Vendor / Payee")]
        public string? VendorOrPayee { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [MaxLength(500)]
        [Display(Name = "Receipt Attachment")]
        public string? ReceiptAttachmentPath { get; set; }

        public Guid? ClinicId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
