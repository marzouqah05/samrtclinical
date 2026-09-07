using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="ISessionTrackingService"/>.
    /// Uses an IMemoryCache to throttle per-user LastActivityTime DB writes to once per 60 seconds.
    /// </summary>
    public class SessionTrackingService : ISessionTrackingService
    {
        private readonly ClinicDbContext _db;
        private readonly IMemoryCache _cache;

        // Cache key prefix to track when each user's activity was last persisted
        private const string CacheKeyPrefix = "session_activity_";
        private static readonly TimeSpan ActivityThrottle = TimeSpan.FromSeconds(60);

        public SessionTrackingService(ClinicDbContext db, IMemoryCache cache)
        {
            _db    = db;
            _cache = cache;
        }

        /// <inheritdoc />
        public async Task CreateSessionAsync(string userId, string userName, string userRole,
                                             string ipAddress, string userAgent)
        {
            // End any lingering active session for this user first (e.g., browser crash recovery)
            var existingActive = await _db.UserSessionLogs
                .Where(s => s.UserId == userId && s.IsActive)
                .ToListAsync();

            foreach (var old in existingActive)
            {
                old.IsActive       = false;
                old.LogoutTime     = DateTime.UtcNow;
                old.DurationMinutes = (DateTime.UtcNow - old.LoginTime).TotalMinutes;
            }

            var session = new UserSessionLog
            {
                UserId           = userId,
                UserName         = userName,
                UserRole         = userRole,
                IpAddress        = ipAddress,
                UserAgent        = userAgent,
                LoginTime        = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IsActive         = true
            };

            _db.UserSessionLogs.Add(session);
            await _db.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task UpdateActivityAsync(string userId)
        {
            var cacheKey = CacheKeyPrefix + userId;

            // Throttle: only write to DB once per 60 seconds per user
            if (_cache.TryGetValue(cacheKey, out _))
                return;

            var session = await _db.UserSessionLogs
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.LoginTime)
                .FirstOrDefaultAsync();

            if (session != null)
            {
                session.LastActivityTime = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // Set cache entry so the next 60 seconds are skipped
            _cache.Set(cacheKey, true, ActivityThrottle);
        }

        /// <inheritdoc />
        public async Task EndSessionAsync(string userId)
        {
            var session = await _db.UserSessionLogs
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.LoginTime)
                .FirstOrDefaultAsync();

            if (session != null)
            {
                var now = DateTime.UtcNow;
                session.LogoutTime      = now;
                session.IsActive        = false;
                session.DurationMinutes = (now - session.LoginTime).TotalMinutes;
                await _db.SaveChangesAsync();
            }

            // Remove the throttle cache entry so the next login starts fresh
            _cache.Remove(CacheKeyPrefix + userId);
        }

        /// <inheritdoc />
        public async Task CleanupExpiredSessionsAsync(int idleThresholdMinutes = 30)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-idleThresholdMinutes);

            var idleSessions = await _db.UserSessionLogs
                .Where(s => s.IsActive && s.LastActivityTime < cutoff)
                .ToListAsync();

            foreach (var session in idleSessions)
            {
                session.IsActive        = false;
                session.LogoutTime      = session.LastActivityTime; // last known activity = effective end
                session.DurationMinutes = (session.LastActivityTime - session.LoginTime).TotalMinutes;
            }

            if (idleSessions.Count > 0)
                await _db.SaveChangesAsync();
        }
    }
}
