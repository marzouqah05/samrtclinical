using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    /// <summary>
    /// Tracks every authenticated user session: login, activity, and logout events.
    /// </summary>
    public class UserSessionLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>ASP.NET Identity user ID (GUID string).</summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>The username / login name.</summary>
        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>The primary role of the user at the time of login (Admin, Doctor, Receptionist, …).</summary>
        [MaxLength(100)]
        public string UserRole { get; set; } = string.Empty;

        /// <summary>IPv4 / IPv6 address of the client.</summary>
        [MaxLength(50)]
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>Raw User-Agent string from the request headers.</summary>
        [MaxLength(512)]
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the session was created (login).</summary>
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp of the most recent authenticated request.</summary>
        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp when the user explicitly logged out. Null if still active or expired.</summary>
        public DateTime? LogoutTime { get; set; }

        /// <summary>
        /// Total session duration in minutes.
        /// Updated on explicit logout or when the cleanup service expires an idle session.
        /// </summary>
        public double DurationMinutes { get; set; }

        /// <summary>True while the session is considered live (not logged-out and not idle-expired).</summary>
        public bool IsActive { get; set; } = true;
    }
}
