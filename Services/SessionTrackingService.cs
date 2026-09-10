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
    /// Supports multi-device session tracking (Desktop, Mobile, Tablet) differentiated by SessionId and User-Agent.
    /// Uses an IMemoryCache to throttle per-device LastActivityTime DB writes to once per 60 seconds.
    /// </summary>
    public class SessionTrackingService : ISessionTrackingService
    {
        private readonly ClinicDbContext _db;
        private readonly IMemoryCache _cache;

        private const string CacheKeyPrefix = "session_act_";
        private static readonly TimeSpan ActivityThrottle = TimeSpan.FromSeconds(60);

        public SessionTrackingService(ClinicDbContext db, IMemoryCache cache)
        {
            _db    = db;
            _cache = cache;
        }

        /// <inheritdoc />
        public async Task CreateSessionAsync(string userId, string userName, string userRole,
                                             string ipAddress, string userAgent, string? sessionId = null)
        {
            var (deviceType, _, _) = UserAgentHelper.Parse(userAgent);

            // End only previous active sessions for THIS specific device/session connection,
            // preserving concurrent sessions on other devices (e.g. mobile + desktop simultaneously).
            var query = _db.UserSessionLogs
                .Where(s => s.UserId == userId && s.IsActive);

            if (!string.IsNullOrEmpty(sessionId))
            {
                query = query.Where(s => s.SessionId == sessionId);
            }
            else if (!string.IsNullOrEmpty(userAgent))
            {
                query = query.Where(s => s.UserAgent == userAgent);
            }

            var existingSameDevice = await query.ToListAsync();
            foreach (var old in existingSameDevice)
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
                SessionId        = sessionId,
                DeviceType       = deviceType,
                LoginTime        = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IsActive         = true
            };

            _db.UserSessionLogs.Add(session);
            await _db.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task UpdateActivityAsync(string userId, string? sessionId = null, string? userAgent = null,
                                             string? ipAddress = null, string? userName = null, string? userRole = null)
        {
            var cacheKey = CacheKeyPrefix + userId + "_" + (sessionId ?? userAgent ?? "default");

            // Throttle: only write to DB once per 60 seconds per device connection
            if (_cache.TryGetValue(cacheKey, out _))
                return;

            UserSessionLog? session = null;

            if (!string.IsNullOrEmpty(sessionId))
            {
                session = await _db.UserSessionLogs
                    .Where(s => s.UserId == userId && s.IsActive && s.SessionId == sessionId)
                    .OrderByDescending(s => s.LoginTime)
                    .FirstOrDefaultAsync();
            }

            if (session == null && !string.IsNullOrEmpty(userAgent))
            {
                session = await _db.UserSessionLogs
                    .Where(s => s.UserId == userId && s.IsActive && s.UserAgent == userAgent)
                    .OrderByDescending(s => s.LoginTime)
                    .FirstOrDefaultAsync();
            }

            if (session != null)
            {
                session.LastActivityTime = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(ipAddress) && session.IpAddress != ipAddress)
                    session.IpAddress = ipAddress;
                if (!string.IsNullOrEmpty(sessionId) && string.IsNullOrEmpty(session.SessionId))
                    session.SessionId = sessionId;

                await _db.SaveChangesAsync();
            }
            else
            {
                // Auto-register session for this device connection (e.g. mobile browser request)
                var (deviceType, _, _) = UserAgentHelper.Parse(userAgent);
                session = new UserSessionLog
                {
                    UserId           = userId,
                    UserName         = userName ?? "User",
                    UserRole         = userRole ?? "User",
                    IpAddress        = ipAddress ?? "Unknown",
                    UserAgent        = userAgent ?? "Unknown",
                    SessionId        = sessionId,
                    DeviceType       = deviceType,
                    LoginTime        = DateTime.UtcNow,
                    LastActivityTime = DateTime.UtcNow,
                    IsActive         = true
                };

                _db.UserSessionLogs.Add(session);
                await _db.SaveChangesAsync();
            }

            // Set cache throttle
            _cache.Set(cacheKey, true, ActivityThrottle);
        }

        /// <inheritdoc />
        public async Task EndSessionAsync(string userId, string? sessionId = null, string? userAgent = null)
        {
            var query = _db.UserSessionLogs
                .Where(s => s.UserId == userId && s.IsActive);

            if (!string.IsNullOrEmpty(sessionId))
            {
                query = query.Where(s => s.SessionId == sessionId);
            }
            else if (!string.IsNullOrEmpty(userAgent))
            {
                query = query.Where(s => s.UserAgent == userAgent);
            }

            var session = await query
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

            var cacheKey = CacheKeyPrefix + userId + "_" + (sessionId ?? userAgent ?? "default");
            _cache.Remove(cacheKey);
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
                session.LogoutTime      = session.LastActivityTime;
                session.DurationMinutes = (session.LastActivityTime - session.LoginTime).TotalMinutes;
            }

            if (idleSessions.Count > 0)
                await _db.SaveChangesAsync();
        }
    }
}
