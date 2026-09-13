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
            return user.IsInRole("SuperAdmin") || user.IsInRole("Owner");
        }

        public static bool IsOwner(this ClaimsPrincipal user)
        {
            if (user == null) return false;
            return user.IsInRole("Owner") || user.IsInRole("SuperAdmin");
        }

        public static bool IsAdmin(this ClaimsPrincipal user)
        {
            if (user == null) return false;
            return user.IsInRole("Admin");
        }

        public static bool IsAdminOrOwner(this ClaimsPrincipal user)
        {
            if (user == null) return false;
            return user.IsOwner() || user.IsInRole("Admin");
        }

        public static bool IsDoctor(this ClaimsPrincipal user)
        {
            if (user == null) return false;
            return user.IsInRole("Doctor");
        }

        public static bool IsReceptionist(this ClaimsPrincipal user)
        {
            if (user == null) return false;
            return user.IsInRole("Receptionist");
        }

        public static int? GetDoctorId(this ClaimsPrincipal user)
        {
            if (user == null) return null;
            var claimValue = user.FindFirst("DoctorId")?.Value;
            if (int.TryParse(claimValue, out var docId))
            {
                return docId;
            }
            return null;
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
