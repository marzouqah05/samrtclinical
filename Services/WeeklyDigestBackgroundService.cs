using System.Globalization;

namespace WebApplication1.Services
{
    /// <summary>
    /// BackgroundService that fires the weekly doctor schedule digest email
    /// at the day/time configured in ClinicSettings → WeeklyDigestDayTime (e.g. "Sunday 07:00").
    /// </summary>
    public class WeeklyDigestBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WeeklyDigestBackgroundService> _logger;

        // How often the loop ticks to check whether it is time to fire.
        private static readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public WeeklyDigestBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<WeeklyDigestBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[WeeklyDigest] Background service started.");

            DateTime? lastFiredDate = null;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var settingsService  = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                    var scheduleService  = scope.ServiceProvider.GetRequiredService<IDoctorScheduleService>();

                    var cfg            = await settingsService.GetSettingsAsync();
                    var dayTimeRaw     = cfg.WeeklyDigestDayTime ?? "Sunday 07:00";
                    var (targetDay, targetTime) = ParseDayTime(dayTimeRaw);

                    var now = DateTime.UtcNow;
                    bool isCorrectDay  = now.DayOfWeek == targetDay;
                    bool isCorrectHour = now.TimeOfDay >= targetTime
                                      && now.TimeOfDay < targetTime + TimeSpan.FromMinutes(10); // 10-min window
                    bool notFiredToday = lastFiredDate == null || lastFiredDate.Value.Date != now.Date;

                    if (isCorrectDay && isCorrectHour && notFiredToday)
                    {
                        _logger.LogInformation("[WeeklyDigest] Firing weekly digest emails — {Now:u}.", now);
                        await scheduleService.SendAllDoctorDigestsAsync();
                        lastFiredDate = now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WeeklyDigest] Error in background service tick.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("[WeeklyDigest] Background service stopped.");
        }

        /// <summary>Parses "Sunday 07:00" → (DayOfWeek.Sunday, 07:00 TimeSpan).</summary>
        private static (DayOfWeek Day, TimeSpan Time) ParseDayTime(string raw)
        {
            try
            {
                var parts = raw.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var day   = Enum.TryParse<DayOfWeek>(parts[0], ignoreCase: true, out var d) ? d : DayOfWeek.Sunday;
                var time  = parts.Length > 1 && TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out var t)
                            ? t
                            : TimeSpan.FromHours(7);
                return (day, time);
            }
            catch
            {
                return (DayOfWeek.Sunday, TimeSpan.FromHours(7));
            }
        }
    }
}
