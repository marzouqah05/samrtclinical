using WebApplication1.Models;

namespace WebApplication1.Models
{
    public class StaffUserViewModel
    {
        public ApplicationUser User { get; set; } = null!;
        public string Role { get; set; } = string.Empty;
        public string CreatedDate { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool CanDelete { get; set; }
        public bool CanEdit { get; set; }
        public bool IsOwner { get; set; }
    }
}
