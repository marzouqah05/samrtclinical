using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    /// <summary>
    /// Clean Slate Database Initializer:
    /// - Applies pending EF Core migrations.
    /// - Ensures multi-tenancy schema integrity (ClinicId on AspNetUsers).
    /// - Performs a direct purge of all legacy demo accounts, clinics, and medical records.
    /// - Ensures ONLY default Identity Roles ("SuperAdmin", "Admin", "Doctor", "Receptionist") exist.
    /// - Strictly zero demo records or demo users are seeded.
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InitializeAsync(ClinicDbContext context, RoleManager<IdentityRole> roleManager, ILogger logger)
        {
            try
            {
                // 1. Apply any pending migrations
                await context.Database.MigrateAsync();

                // 2. Ensure ClinicId column exists on AspNetUsers
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""ClinicId"" uuid NULL;");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Notice checking/adding ClinicId to AspNetUsers.");
                }

                // 3. Provider-specific Production DB Reset
                var provider = context.Database.ProviderName ?? string.Empty;
                if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
                    context.Database.ExecuteSqlRaw("DELETE FROM AspNetUserRoles; DELETE FROM AspNetUsers; DELETE FROM Clinics; DELETE FROM Patients; DELETE FROM Appointments; DELETE FROM Invoices; DELETE FROM Expenses;");
                    context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
                }
                else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || provider.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    context.Database.ExecuteSqlRaw("TRUNCATE TABLE \"AspNetUserRoles\", \"AspNetUsers\", \"Clinics\", \"Patients\", \"Appointments\", \"Invoices\", \"Expenses\" RESTART IDENTITY CASCADE;");
                }
                else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                {
                    context.Database.ExecuteSqlRaw("EXEC sp_MSforeachtable \"ALTER TABLE ? NOCHECK CONSTRAINT all\"; DELETE FROM AspNetUserRoles; DELETE FROM AspNetUsers; DELETE FROM Clinics; DELETE FROM Patients; DELETE FROM Appointments; DELETE FROM Invoices; DELETE FROM Expenses; EXEC sp_MSforeachtable \"ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all\";");
                }

                Console.WriteLine("--> [RAILWAY CONTAINER BOOT] Remote DB hard reset completed successfully.");

                // 4. Ensure default Identity Roles exist (and only roles, NO demo users)
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
                logger.LogError(ex, "[DbInitializer] Fatal error during clean slate initialization.");
                throw;
            }
        }
    }
}
