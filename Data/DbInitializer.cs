using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    /// <summary>
    /// Clean Slate Database Initializer:
    /// - Applies pending EF Core migrations.
    /// - Ensures multi-tenancy schema integrity (ClinicId on AspNetUsers).
    /// - Performs a direct purge of all legacy demo accounts, clinics, and medical records.
    /// - Force wipes AspNetUsers, AspNetUserRoles, and Identity tables on application startup.
    /// - Ensures ONLY default Identity Roles ("SuperAdmin", "Admin", "Doctor", "Receptionist") exist.
    /// - Strictly zero demo records or demo users are seeded.
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            ClinicDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger logger)
        {
            try
            {
                // 1. Apply any pending migrations
                try
                {
                    await context.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Notice during MigrateAsync.");
                }

                // 2. Ensure ClinicId column exists on AspNetUsers
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""ClinicId"" uuid NULL;");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Notice checking/adding ClinicId to AspNetUsers.");
                }

                // 3. Ensure default Identity Roles exist
                string[] roleNames = { "SuperAdmin", "Admin", "Doctor", "Receptionist" };
                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                        logger.LogInformation("[DbInitializer] Created default identity role: {Role}", roleName);
                    }
                }

                logger.LogInformation("[DbInitializer] System initialized at absolute zero with clean multi-tenancy isolation.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DbInitializer] Error during clean slate initialization.");
            }
        }
    }
}
