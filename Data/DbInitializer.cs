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

                // 3. Permanent database purge: reset all clinical records, users, and tenants to absolute ZERO
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        TRUNCATE TABLE 
                            ""AspNetUserRoles"",
                            ""AspNetUserClaims"",
                            ""AspNetUserLogins"",
                            ""AspNetUserTokens"",
                            ""AspNetRoleClaims"",
                            ""Invoices"",
                            ""Treatments"",
                            ""Appointments"",
                            ""PatientAttachments"",
                            ""MedicalRecords"",
                            ""Doctors"",
                            ""Patients"",
                            ""Expenses"",
                            ""Departments"",
                            ""Clinics"",
                            ""UserSessionLogs"",
                            ""AuditLogs"",
                            ""AspNetUsers""
                        CASCADE;
                    ");
                    logger.LogInformation("[DbInitializer] Database successfully purged to clean slate (zero records).");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Truncate cascade failed, falling back to individual table deletes.");
                    var tables = new[]
                    {
                        "Invoices", "Treatments", "Appointments", "PatientAttachments", "MedicalRecords",
                        "Doctors", "Patients", "Expenses", "Departments", "Clinics", "UserSessionLogs", "AuditLogs",
                        "AspNetUserRoles", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetRoleClaims",
                        "AspNetUsers"
                    };
                    foreach (var tbl in tables)
                    {
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync($"DELETE FROM \"{tbl}\";");
                        }
                        catch (Exception tableEx)
                        {
                            logger.LogWarning(tableEx, "[DbInitializer] Error deleting table {Table}", tbl);
                        }
                    }
                }

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
