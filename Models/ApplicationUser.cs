using Microsoft.AspNetCore.Identity;
using System;

namespace WebApplication1.Models
{
    public class ApplicationUser : IdentityUser
    {
        public Guid? ClinicId { get; set; }
    }
}
