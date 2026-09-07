using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    /// <summary>
    /// Background hosted service that periodically expires idle user sessions.
    /// Runs every 5 minutes and marks sessions with no activity in the last 30 minutes as inactive.
    /// </summary>
    public class SessionCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SessionCleanupService> _logger;
        private static readonly TimeSpan CleanupInterval    = TimeSpan.FromMinutes(5);
        private const int IdleThresholdMinutes              = 30;

        public SessionCleanupService(IServiceScopeFactory scopeFactory,
                                     ILogger<SessionCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[SessionCleanup] Background service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(CleanupInterval, stoppingToken);

                try
                {
                    using var scope   = _scopeFactory.CreateScope();
                    var trackingService = scope.ServiceProvider
                                              .GetRequiredService<ISessionTrackingService>();

                    await trackingService.CleanupExpiredSessionsAsync(IdleThresholdMinutes);
                    _logger.LogDebug("[SessionCleanup] Idle session cleanup completed.");
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "[SessionCleanup] Error during session cleanup.");
                }
            }

            _logger.LogInformation("[SessionCleanup] Background service stopped.");
        }
    }
}
