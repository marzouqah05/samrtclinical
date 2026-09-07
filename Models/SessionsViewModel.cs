using System.Collections.Generic;

namespace WebApplication1.Models
{
    /// <summary>
    /// ViewModel passed to Views/Audit/Sessions.cshtml.
    /// </summary>
    public class SessionsViewModel
    {
        // ── KPI Aggregates ─────────────────────────────────────────────────────

        /// <summary>Number of distinct login events that occurred today (UTC).</summary>
        public int TotalLoginsToday { get; set; }

        /// <summary>Number of sessions currently marked as active.</summary>
        public int ActiveOnlineUsers { get; set; }

        /// <summary>Mean session duration (in minutes) across all completed sessions today.</summary>
        public double AverageSessionMinutes { get; set; }

        // ── Table Data ─────────────────────────────────────────────────────────

        /// <summary>Full list of session rows to render in the table (filtered/ordered by the controller).</summary>
        public List<UserSessionLog> Sessions { get; set; } = new();

        // ── Search / Filter ────────────────────────────────────────────────────

        /// <summary>Current search term (passed back so the input retains its value).</summary>
        public string? SearchTerm { get; set; }

        /// <summary>Current role filter ("" = all).</summary>
        public string? RoleFilter { get; set; }

        /// <summary>Current status filter ("" = all, "active", "ended").</summary>
        public string? StatusFilter { get; set; }
    }
}
