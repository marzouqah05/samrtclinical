using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Middleware
{
    /// <summary>
    /// Middleware that updates the LastActivityTime for every authenticated request.
    /// Uses an isolated service scope so tracking database operations never share
    /// or conflict with the request's scoped DbContext in controllers.
    /// </summary>
    public class UserActivityMiddleware
    {
        private readonly RequestDelegate _next;

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
                        // Resolve tracking service inside an isolated scope so its database operations
                        // never share or conflict with the request's DbContext used by controllers
                        using var scope = serviceProvider.CreateScope();
                        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionTrackingService>();
                        await sessionService.UpdateActivityAsync(userId);
                    }
                    catch
                    {
                        // Gracefully swallow tracking errors so incoming HTTP requests are never broken
                    }
                }
            }

            await _next(context);
        }
    }
}
