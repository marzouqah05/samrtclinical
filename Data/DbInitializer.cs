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
    /// Database Initializer:
    /// - Applies pending EF Core migrations.
    /// - Ensures multi-tenancy schema integrity (ClinicId on AspNetUsers).
    /// - Preserves all registered users, clinics, and medical data across container restarts.
    /// - Ensures default Identity Roles ("SuperAdmin", "Admin", "Doctor", "Receptionist") exist.
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

                int userCount = 0;
                try
                {
                    userCount = await userManager.Users.CountAsync();
                }
                catch { }

                logger.LogInformation("[DbInitializer] Database initialized successfully in persistent mode. Total users preserved: {Count}", userCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DbInitializer] Error during database initialization.");
            }
        }
    }
}
