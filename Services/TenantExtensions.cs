using System;
using System.Security.Claims;

namespace WebApplication1.Services
{
    public static class TenantExtensions
    {
        public static readonly Guid DefaultClinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public static bool IsSuperAdmin(this ClaimsPrincipal user)
        {
            if (user == null) return false;
            return user.IsInRole("SuperAdmin");
        }

        public static Guid GetClinicId(this ClaimsPrincipal user)
        {
            if (user == null) return Guid.Empty;

            var claimValue = user.FindFirst("ClinicId")?.Value;
            if (!string.IsNullOrEmpty(claimValue) && Guid.TryParse(claimValue, out var clinicGuid))
            {
                return clinicGuid;
            }

            return Guid.Empty;
        }
    }
}
