using System.Threading.Tasks;

namespace WebApplication1.Services
{
    /// <summary>
    /// Manages the lifecycle of user session logs:
    /// creation on login, activity heartbeats, explicit logout, and idle cleanup.
    /// </summary>
    public interface ISessionTrackingService
    {
        /// <summary>
        /// Creates a new active session record after a successful login.
        /// </summary>
        Task CreateSessionAsync(string userId, string userName, string userRole,
                                string ipAddress, string userAgent);

        /// <summary>
        /// Updates the LastActivityTime for the active session belonging to <paramref name="userId"/>.
        /// Internally throttled to at most once every 60 seconds to reduce DB writes.
        /// </summary>
        Task UpdateActivityAsync(string userId);

        /// <summary>
        /// Marks the most-recent active session for <paramref name="userId"/> as ended,
        /// sets LogoutTime, computes DurationMinutes, and sets IsActive = false.
        /// </summary>
        Task EndSessionAsync(string userId);

        /// <summary>
        /// Scans all active sessions and marks any session where LastActivityTime is
        /// older than <paramref name="idleThresholdMinutes"/> minutes as inactive (idle-expired).
        /// Called periodically by the background cleanup service.
        /// </summary>
        Task CleanupExpiredSessionsAsync(int idleThresholdMinutes = 30);
    }
}
