using System.Threading.Tasks;

namespace WebApplication1.Services
{
    /// <summary>
    /// Manages the lifecycle of user session logs:
    /// creation on login, multi-device activity heartbeats, explicit logout, and idle cleanup.
    /// </summary>
    public interface ISessionTrackingService
    {
        /// <summary>
        /// Creates a new active session record after a successful login for the specific device/connection.
        /// </summary>
        Task CreateSessionAsync(string userId, string userName, string userRole,
                                string ipAddress, string userAgent, string? sessionId = null);

        /// <summary>
        /// Updates the LastActivityTime for the active session matching the user and device/sessionId.
        /// If no active session exists for this device (e.g. mobile access), auto-registers it.
        /// Throttled to reduce DB writes.
        /// </summary>
        Task UpdateActivityAsync(string userId, string? sessionId = null, string? userAgent = null,
                                 string? ipAddress = null, string? userName = null, string? userRole = null);

        /// <summary>
        /// Marks the specific active device session as ended.
        /// </summary>
        Task EndSessionAsync(string userId, string? sessionId = null, string? userAgent = null);

        /// <summary>
        /// Scans all active sessions and marks any session where LastActivityTime is
        /// older than <paramref name="idleThresholdMinutes"/> minutes as inactive (idle-expired).
        /// </summary>
        Task CleanupExpiredSessionsAsync(int idleThresholdMinutes = 30);
    }
}
