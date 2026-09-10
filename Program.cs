using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebApplication1.Data;
using WebApplication1.Middleware;
using WebApplication1.Models;
using WebApplication1.Services;

internal class Program
{
    private static async Task Main(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // PropertyNameCaseInsensitive = true ensures Telegram's snake_case JSON payload
        // (update_id, callback_query, etc.) is correctly deserialized by [ApiController]
        // endpoints even as a defence-in-depth measure alongside explicit [JsonPropertyName]
        // attributes on the TelegramUpdate DTOs.
        builder.Services.AddControllersWithViews()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);
        builder.Services.AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);

        // ── Clinic Settings & Configuration Service ──────────────────────────────────
        // SettingsService is Scoped only. It injects IHttpClientFactory (which is a
        // Singleton registered by the ASP.NET Core host) instead of HttpClient directly.
        // The previous AddHttpClient<SettingsService>() created a conflicting TRANSIENT
        // factory registration that shared the same Scoped DbContext from a different
        // lifetime scope, causing "second operation started on this context" errors.
        builder.Services.AddScoped<ISettingsService, SettingsService>();

        // ── WhatsApp Notification Service ────────────────────────────────────────────
        // WhatsAppService depends on ISettingsService (to read DB webhook URL & toggles)
        // so we register it with AddHttpClient + factory to inject ISettingsService.
        builder.Services.AddHttpClient<WhatsAppService>();
        builder.Services.AddScoped<IWhatsAppService>(sp =>
        {
            var http            = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WhatsAppService));
            var config          = sp.GetRequiredService<IConfiguration>();
            var logger          = sp.GetRequiredService<ILogger<WhatsAppService>>();
            var settingsService = sp.GetRequiredService<ISettingsService>();
            return new WhatsAppService(http, config, logger, settingsService);
        });

        // ── Bulk Data Import & Export Service ─────────────────────────────────────────
        builder.Services.AddScoped<IDataImportExportService, DataImportExportService>();

        // ── Full System Migration Service ────────────────────────────────────────────
        builder.Services.AddScoped<ISystemMigrationService, SystemMigrationService>();

        // ── In-Memory Cache (used by SessionTrackingService for activity throttling) ────────
        builder.Services.AddMemoryCache();

        // ── User Activity & Session Tracking ──────────────────────────────────────
        builder.Services.AddScoped<ISessionTrackingService, SessionTrackingService>();
        builder.Services.AddHostedService<SessionCleanupService>();

        // ── Automated Daily Backup & Google Drive Cloud Sync ──────────────────────
        builder.Services.AddScoped<IBackupService, BackupService>();
        builder.Services.AddHostedService<DailyBackupBackgroundService>();

        // ── Public Self-Booking Service (WhatsApp / n8n flow) ────────────────────
        builder.Services.AddScoped<IBookingService, BookingService>();

        // ── Doctor Weekly Schedule & Email Digest Service ─────────────────────────
        builder.Services.AddScoped<IDoctorScheduleService, DoctorScheduleService>();
        builder.Services.AddHostedService<WeeklyDigestBackgroundService>();

        // ── Telegram Bot Webhook Dispatcher ──────────────────────────────────────────
        // TelegramBotService is Scoped only. It injects IHttpClientFactory directly
        // (same fix as SettingsService) to avoid the transient/scoped DI conflict.
        builder.Services.AddSingleton<ITelegramSessionStore, TelegramSessionStore>();
        builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();

        // 1. Database Connection
        builder.Services.AddDbContext<ClinicDbContext>(options =>
             options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // 2. ASP.NET Core Identity Configuration
        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            // Requirement for unique email across all user accounts
            options.User.RequireUniqueEmail = true;

            // إعدادات كلمة المرور تسهيلاً للفحص والتطوير
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;

            // إعدادات القفل التلقائي للحساب عند الإدخال الخاطئ المتكرر
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = false; // Prevent lockout for seeded admin account
        })
            .AddEntityFrameworkStores<ClinicDbContext>()
            .AddDefaultTokenProviders();

        // 3. Application Cookie Settings (Identity Security Paths)
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(5);
            options.SlidingExpiration = true;
        });

        // 4. Session Configuration
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // 5. Localization — Arabic (default) + English
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[]
            {
                new CultureInfo("ar"),
                new CultureInfo("en")
            };
            options.DefaultRequestCulture = new RequestCulture(culture: "ar", uiCulture: "ar");
            options.SupportedCultures   = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            // Accept culture from cookie first, then query string, then browser
            options.RequestCultureProviders = new List<IRequestCultureProvider>
            {
                new CookieRequestCultureProvider(),
                new QueryStringRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            };
        });

        var app = builder.Build();

        // ── Forwarded Headers (Reverse Proxy / Railway / Cloudflare support) ──────────
        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        forwardedOptions.KnownNetworks.Clear();
        forwardedOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedOptions);

        // Error Handling
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        // 6. Request Localization (must come before Routing/Auth)
        app.UseRequestLocalization();

        app.UseRouting();

        // 5. Authentication & Authorization Middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Session Middleware
        app.UseSession();

        // ── User Activity Middleware (must come after UseAuthentication & UseAuthorization) ──
        app.UseMiddleware<UserActivityMiddleware>();

        // 6. Routes Setup
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Account}/{action=Login}/{id?}");

        // API controller routes (attribute-routed, no view convention)
        app.MapControllers();

        // ==========================================
        // 🔥 تلقيم قاعدة البيانات بالبيانات الأساسية (Data Seeding)
        // ==========================================
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

                // 1. إنشاء الأدوار الأساسية إذا لم تكن موجودة
                string[] roleNames = { "Admin", "Doctor", "Receptionist" };
                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }

                // 2. إنشاء حساب Admin افتراضي للدخول الأول
                string adminUsername = "admin";
                string adminEmail = "admin@medicare.com";
                string adminPassword = "Admin123!"; // Must match Identity password policy

                var adminUser = await userManager.FindByNameAsync(adminUsername);
                if (adminUser == null)
                {
                    // ── Create new admin user ────────────────────────────────
                    var newAdmin = new IdentityUser
                    {
                        UserName = adminUsername,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(newAdmin, adminPassword);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newAdmin, "Admin");
                    }
                }
                else
                {
                    // ── Reset existing admin: unlock + force-set password ──── 
                    await userManager.SetLockoutEndDateAsync(adminUser, null);
                    await userManager.ResetAccessFailedCountAsync(adminUser);

                    await userManager.RemovePasswordAsync(adminUser);
                    await userManager.AddPasswordAsync(adminUser, adminPassword);

                    // Ensure Admin role is still assigned
                    if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                    }
                }
            }
            catch (Exception ex)
            {
                // يمكن تسجيل الخطأ هنا في حال حدوث مشكلة أثناء التشغيل
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "حدث خطأ أثناء تلقيم قاعدة البيانات بالبيانات الافتراضية.");
            }
        }
        // ==========================================

        // ==========================================
        // Seed comprehensive demo data on every startup.
        // SeedAsync:           Departments, Doctors, Patients, Appointments, Treatments, Invoices
        //                      (each table seeded independently; skips tables that already have rows)
        // SeedAdditionalAsync: Extra Doctors, Patients, Appointments, Treatments, Invoices
        //                      (row-level checks by unique business key — never duplicates)
        // SeedEssentialsAsync: 3 essential Doctors + 5 essential Patients
        //                      (always checked by unique business key on every startup)
        // ==========================================
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<ClinicDbContext>();
                var logger = services.GetRequiredService<ILogger<Program>>();

                // Step 1: seed base tables (Departments → Doctors → Patients → Appointments → Treatments → Invoices)
                await DbInitializer.SeedAsync(context, logger);

                // Step 2: seed additional demo records (idempotent row-level checks)
                await DbInitializer.SeedAdditionalAsync(context, logger);

                // Step 3: always ensure 3 essential doctors and 5 essential patients exist
                await DbInitializer.SeedEssentialsAsync(context, logger);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "[Seed] Fatal error during clinic data seeding.");
            }

            // Step 4: Seed default ClinicSettings key-value pairs (only if table is empty)
            try
            {
                var settingsService = services.GetRequiredService<ISettingsService>();
                await settingsService.SeedDefaultSettingsIfEmptyAsync();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "[Seed] Error seeding default ClinicSettings.");
            }
        }
        // ==========================================

        app.Run();
    }
}