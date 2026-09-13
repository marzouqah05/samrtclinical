using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class EditStaffViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? NewPassword { get; set; }
        public bool IsOwner { get; set; }
        public bool CanChangeRole { get; set; }
        public List<string> AllowedRoles { get; set; } = new();
    }
}
