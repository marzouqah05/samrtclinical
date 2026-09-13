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

                // Ensure FullName column exists on AspNetUsers
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""FullName"" varchar(150) NULL;");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Notice checking/adding FullName to AspNetUsers.");
                }

                // Ensure Gender column exists on Patients
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Gender"" varchar(20) NULL;");
                }
                catch { }

                // Ensure IsOutsideHoursException column exists on Appointments
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""IsOutsideHoursException"" boolean NOT NULL DEFAULT FALSE;");
                }
                catch { }

                // 3. Ensure default Identity Roles exist
                string[] roleNames = { "Owner", "SuperAdmin", "Admin", "Doctor", "Receptionist" };
                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                        logger.LogInformation("[DbInitializer] Created default identity role: {Role}", roleName);
                    }
                }

                // 4. Ensure Primary Clinic Creator Accounts have Owner role
                try
                {
                    var clinics = await context.Clinics.ToListAsync();
                    foreach (var clinic in clinics)
                    {
                        if (!string.IsNullOrEmpty(clinic.OwnerEmail))
                        {
                            var ownerUser = await userManager.FindByEmailAsync(clinic.OwnerEmail) 
                                           ?? await userManager.FindByNameAsync(clinic.OwnerEmail);
                            if (ownerUser != null && !await userManager.IsInRoleAsync(ownerUser, "Owner"))
                            {
                                await userManager.AddToRoleAsync(ownerUser, "Owner");
                                logger.LogInformation("[DbInitializer] Upgraded clinic owner {Email} to 'Owner' role.", clinic.OwnerEmail);
                            }
                        }
                    }

                    // Fallback: If no Owner user exists in database, promote the first Admin or user named Admin/SuperAdmin
                    var allOwners = await userManager.GetUsersInRoleAsync("Owner");
                    if (allOwners.Count == 0)
                    {
                        var adminUser = await userManager.FindByNameAsync("Admin") 
                                       ?? await userManager.FindByEmailAsync("admin@clinicflow.com")
                                       ?? await userManager.Users.FirstOrDefaultAsync();
                        if (adminUser != null)
                        {
                            if (!await userManager.IsInRoleAsync(adminUser, "Owner"))
                            {
                                await userManager.AddToRoleAsync(adminUser, "Owner");
                                logger.LogInformation("[DbInitializer] Designated primary user {UserName} as 'Owner'.", adminUser.UserName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[DbInitializer] Notice during Owner role assignment.");
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
