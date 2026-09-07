using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    /// <summary>
    /// Background service that triggers daily automated backup snapshot creation
    /// and cloud sync to Google Drive at 02:00 AM UTC.
    /// </summary>
    public class DailyBackupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyBackupBackgroundService> _logger;

        public DailyBackupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DailyBackupBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DailyBackupBackgroundService initialized. Scheduled for 02:00 AM daily.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddHours(2); // 02:00 AM today
                if (now >= nextRun)
                {
                    nextRun = nextRun.AddDays(1); // 02:00 AM tomorrow
                }

                var delay = nextRun - now;
                _logger.LogInformation("Next automated backup snapshot scheduled in {Hours:F1} hours at {NextRun:yyyy-MM-dd HH:mm} UTC.",
                    delay.TotalHours, nextRun);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (stoppingToken.IsCancellationRequested) break;

                await RunAutomatedDailyBackupAsync();
            }
        }

        public async Task RunAutomatedDailyBackupAsync()
        {
            _logger.LogInformation("Starting automated daily snapshot & Google Drive cloud backup...");
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

                // 1. Generate local daily snapshot
                var snapshotBytes = await backupService.GenerateDailySnapshotAsync();
                _logger.LogInformation("Automated daily snapshot successfully generated ({Bytes} bytes).", snapshotBytes.Length);

                // 2. Sync to cloud vault
                var syncResult = await backupService.SyncToGoogleDriveAsync();
                _logger.LogInformation("Automated Google Drive cloud sync finished: {StatusMessage}", syncResult.StatusMessage);

                // 3. If today is the 1st of the month, generate full monthly archive as well
                if (DateTime.UtcNow.Day == 1)
                {
                    _logger.LogInformation("First day of the month detected. Generating full monthly archive...");
                    var fullArchiveBytes = await backupService.GenerateFullSystemArchiveAsync();
                    _logger.LogInformation("Full monthly archive generated ({Bytes} bytes).", fullArchiveBytes.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automated daily backup execution.");
            }
        }
    }
}
