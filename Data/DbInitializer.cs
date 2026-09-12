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
                        DO $$ 
                        DECLARE 
                            t text;
                        BEGIN
                            FOR t IN 
                                SELECT tablename FROM pg_tables 
                                WHERE schemaname = 'public' 
                                  AND tablename NOT IN ('__EFMigrationsHistory', 'AspNetRoles')
                            LOOP
                                BEGIN
                                    EXECUTE 'TRUNCATE TABLE public.\""' || t || '\"" CASCADE;';
                                EXCEPTION WHEN OTHERS THEN
                                    BEGIN
                                        EXECUTE 'DELETE FROM public.\""' || t || '\"";';
                                    EXCEPTION WHEN OTHERS THEN
                                        NULL;
                                    END;
                                END;
                            END LOOP;
                        END $$;
                    ");
                    logger.LogInformation("[DbInitializer] Dynamic PostgreSQL table truncate completed.");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Dynamic truncate block failed, executing explicit table deletions.");
                }

                // Explicit sequential table cleanup as foolproof guarantee
                var explicitCleanup = @"
                    DELETE FROM ""AspNetUserRoles"";
                    DELETE FROM ""AspNetUserClaims"";
                    DELETE FROM ""AspNetUserLogins"";
                    DELETE FROM ""AspNetUserTokens"";
                    DELETE FROM ""AspNetRoleClaims"";
                    DELETE FROM ""PatientAttachments"";
                    DELETE FROM ""MedicalRecords"";
                    DELETE FROM ""Treatments"";
                    DELETE FROM ""Invoices"";
                    DELETE FROM ""Expenses"";
                    DELETE FROM ""Appointments"";
                    DELETE FROM ""Patients"";
                    DELETE FROM ""Doctors"";
                    DELETE FROM ""Departments"";
                    DELETE FROM ""UserSessionLogs"";
                    DELETE FROM ""AuditLogs"";
                    DELETE FROM ""ClinicSettings"";
                    DELETE FROM ""AspNetUsers"";
                    DELETE FROM ""Clinics"";
                ";

                try
                {
                    await context.Database.ExecuteSqlRawAsync(explicitCleanup);
                    logger.LogInformation("[DbInitializer] Explicit sequential table purge executed successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Explicit SQL delete encountered a warning, falling back to individual deletes.");
                    var tables = new[]
                    {
                        "AspNetUserRoles", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetRoleClaims",
                        "PatientAttachments", "MedicalRecords", "Treatments", "Invoices", "Expenses", "Appointments",
                        "Patients", "Doctors", "Departments", "UserSessionLogs", "AuditLogs", "ClinicSettings",
                        "AspNetUsers", "Clinics"
                    };
                    foreach (var tbl in tables)
                    {
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync($"DELETE FROM \"{tbl}\";");
                        }
                        catch (Exception tableEx)
                        {
                            logger.LogWarning(tableEx, "[DbInitializer] Failed deleting table {Table}", tbl);
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
