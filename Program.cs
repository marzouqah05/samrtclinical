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

        // ── Email Notification & OTP Dispatch Service ───────────────────────────────
        builder.Services.AddScoped<EmailService>();
        builder.Services.AddScoped<IEmailSenderService>(sp => sp.GetRequiredService<EmailService>());
        builder.Services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<EmailService>());

        // ── In-Memory Cache (used by SessionTrackingService for activity throttling and OTP storage) ────────
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

        // Add HttpContextAccessor for multi-tenancy context resolution
        builder.Services.AddHttpContextAccessor();

        // 1. Database Connection
        builder.Services.AddDbContext<ClinicDbContext>(options =>
             options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // 2. ASP.NET Core Identity Configuration
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
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
            options.Lockout.AllowedForNewUsers = false;
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
        app.UseDeveloperExceptionPage();
        if (!app.Environment.IsDevelopment())
        {
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
        // Clean Slate Initialization: Schema verification, permanent database purge & default roles only
        // Strictly ZERO demo accounts or demo clinics seeded.
        // ==========================================
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<ClinicDbContext>();
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                var logger = services.GetRequiredService<ILogger<Program>>();

                // 1. Apply migrations
                try { await context.Database.MigrateAsync(); } catch (Exception ex) { logger.LogWarning(ex, "MigrateAsync notice"); }

                // 2. Ensure ClinicId column exists on AspNetUsers
                try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""ClinicId"" uuid NULL;"); } catch { }

                // 3. Provider-specific raw SQL purge
                var provider = context.Database.ProviderName ?? string.Empty;
                try
                {
                    if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || provider.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Database.ExecuteSqlRaw(@"TRUNCATE TABLE ""AspNetUserRoles"", ""AspNetUserClaims"", ""AspNetUserLogins"", ""AspNetUserTokens"", ""AspNetUsers"", ""Clinics"" RESTART IDENTITY CASCADE;");
                    }
                    else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
                        context.Database.ExecuteSqlRaw("DELETE FROM AspNetUserRoles; DELETE FROM AspNetUsers; DELETE FROM Clinics;");
                        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
                    }
                    else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Database.ExecuteSqlRaw("EXEC sp_MSforeachtable \"ALTER TABLE ? NOCHECK CONSTRAINT all\"; DELETE FROM AspNetUserRoles; DELETE FROM AspNetUsers; DELETE FROM Clinics; EXEC sp_MSforeachtable \"ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all\";");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[StartupPurge] Truncate notice, falling back to individual deletes.");
                }

                // 4. Also call DbInitializer for any additional cleanup & re-seeding the 4 default roles
                await DbInitializer.InitializeAsync(context, userManager, roleManager, logger);

                Console.WriteLine("--> [PROD RESET] AspNetUsers completely wiped. Ready for clean registration.");
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "[CleanSlate] Error during database clean slate initialization.");
            }
        }
        // ==========================================

        app.Run();
    }
}