using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Middleware
{
    /// <summary>
    /// Middleware that updates the LastActivityTime for every authenticated request.
    /// Throttling (60 s) is handled inside <see cref="ISessionTrackingService.UpdateActivityAsync"/>,
    /// so this middleware simply calls through on every request without extra overhead.
    /// </summary>
    public class UserActivityMiddleware
    {
        private readonly RequestDelegate _next;

        public UserActivityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISessionTrackingService sessionService)
        {
            // Only track activity for authenticated users
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    try
                    {
                        // Strictly await activity update to prevent concurrent operations on the shared scoped DbContext
                        await sessionService.UpdateActivityAsync(userId);
                    }
                    catch
                    {
                        // Gracefully swallow tracking errors so user request pipeline is never blocked
                    }
                }
            }

            await _next(context);
        }
    }
}
