using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class RegisterClinicViewModel
    {
        [Required]
        public string ClinicName { get; set; } = string.Empty;

        [Required]
        public string AdminName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
