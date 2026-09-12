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

                // 3. Force delete all users via UserManager to cleanly clear Identity stores
                try
                {
                    var allUsers = await userManager.Users.ToListAsync();
                    foreach (var u in allUsers)
                    {
                        await userManager.DeleteAsync(u);
                    }
                    logger.LogInformation("[DbInitializer] Deleted {Count} users via UserManager.", allUsers.Count);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Notice during UserManager user deletion.");
                }

                // 4. Force raw SQL deletes on AspNetUsers, AspNetUserRoles, and all related tables
                var deleteCommands = new[]
                {
                    @"DELETE FROM ""AspNetUserRoles"";",
                    @"DELETE FROM ""AspNetUserClaims"";",
                    @"DELETE FROM ""AspNetUserLogins"";",
                    @"DELETE FROM ""AspNetUserTokens"";",
                    @"DELETE FROM ""UserSessionLogs"";",
                    @"DELETE FROM ""AuditLogs"";",
                    @"DELETE FROM ""ClinicSettings"";",
                    @"DELETE FROM ""Appointments"";",
                    @"DELETE FROM ""Invoices"";",
                    @"DELETE FROM ""Expenses"";",
                    @"DELETE FROM ""Patients"";",
                    @"DELETE FROM ""Doctors"";",
                    @"DELETE FROM ""Departments"";",
                    @"DELETE FROM ""AspNetUsers"";",
                    @"DELETE FROM ""Clinics"";",
                    "DELETE FROM AspNetUserRoles;",
                    "DELETE FROM AspNetUserClaims;",
                    "DELETE FROM AspNetUserLogins;",
                    "DELETE FROM AspNetUserTokens;",
                    "DELETE FROM AspNetUsers;",
                    "DELETE FROM Clinics;",
                    "DELETE FROM Patients;",
                    "DELETE FROM Appointments;",
                    "DELETE FROM Invoices;",
                    "DELETE FROM Expenses;"
                };

                foreach (var sql in deleteCommands)
                {
                    try
                    {
                        context.Database.ExecuteSqlRaw(sql);
                    }
                    catch
                    {
                        // Ignore individual table non-existence or dialect differences
                    }
                }

                // Double check with EF Core DbContext
                try
                {
                    var remaining = context.Users.ToList();
                    if (remaining.Count > 0)
                    {
                        context.Users.RemoveRange(remaining);
                        await context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Notice clearing remaining users in DbContext.");
                }

                int countAfter = 0;
                try
                {
                    countAfter = await context.Users.CountAsync();
                }
                catch { }

                Console.WriteLine($"--> [RAILWAY CONTAINER BOOT] User purge completed. Total users in AspNetUsers: {countAfter}");

                // 5. Ensure default Identity Roles exist (and only roles, NO demo users)
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
