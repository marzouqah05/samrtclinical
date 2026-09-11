using System;
using System.Security.Claims;

namespace WebApplication1.Services
{
    public static class TenantExtensions
    {
        public static readonly Guid DefaultClinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public static Guid GetClinicId(this ClaimsPrincipal user)
        {
            if (user == null) return DefaultClinicId;

            var claimValue = user.FindFirst("ClinicId")?.Value;
            if (!string.IsNullOrEmpty(claimValue) && Guid.TryParse(claimValue, out var clinicGuid))
            {
                return clinicGuid;
            }

            return DefaultClinicId;
        }
    }
}
