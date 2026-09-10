using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Middleware
{
    /// <summary>
    /// Middleware that updates the LastActivityTime for every authenticated request per device.
    /// Supports multi-device tracking (mobile, desktop) and reverse proxy IP extraction.
    /// Uses an isolated service scope so tracking database operations never share
    /// or conflict with the request's scoped DbContext in controllers.
    /// </summary>
    public class UserActivityMiddleware
    {
        private readonly RequestDelegate _next;
        private const string DeviceCookieName = "cf_device_id";

        public UserActivityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            // Only track activity for authenticated users
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    try
                    {
                        var userName = context.User.Identity?.Name ?? "User";
                        var userRole = context.User.FindFirstValue(ClaimTypes.Role) ?? "User";
                        var ip = GetClientIp(context);
                        var userAgent = context.Request.Headers["User-Agent"].ToString();
                        var sessionId = GetOrCreateDeviceId(context);

                        // Resolve tracking service inside an isolated scope so its database operations
                        // never share or conflict with the request's DbContext used by controllers
                        using var scope = serviceProvider.CreateScope();
                        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionTrackingService>();
                        await sessionService.UpdateActivityAsync(userId, sessionId, userAgent, ip, userName, userRole);
                    }
                    catch
                    {
                        // Gracefully swallow tracking errors so incoming HTTP requests are never broken
                    }
                }
            }

            await _next(context);
        }

        public static string GetClientIp(HttpContext context)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var ip = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ip))
                    return ip;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        public static string GetOrCreateDeviceId(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue(DeviceCookieName, out var existingId) && !string.IsNullOrWhiteSpace(existingId))
            {
                return existingId;
            }

            try
            {
                if (context.Session.IsAvailable && !string.IsNullOrEmpty(context.Session.Id))
                {
                    var sessId = context.Session.Id;
                    context.Response.Cookies.Append(DeviceCookieName, sessId, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                        Expires  = DateTimeOffset.UtcNow.AddYears(1)
                    });
                    return sessId;
                }
            }
            catch { }

            var newId = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(DeviceCookieName, newId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires  = DateTimeOffset.UtcNow.AddYears(1)
            });
            return newId;
        }
    }
}
