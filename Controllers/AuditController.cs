using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    /// <summary>
    /// Admin-only controller for the User Activity &amp; Session Monitoring dashboard.
    /// </summary>
    [Authorize(Roles = "Owner,Admin,SuperAdmin")]
    public class AuditController : Controller
    {
        private readonly ClinicDbContext _db;

        public AuditController(ClinicDbContext db)
        {
            _db = db;
        }

        // GET: /Audit or /Audit/Index
        public IActionResult Index()
        {
            return RedirectToAction("Index", "AuditTrail");
        }

        // GET: /Audit/Sessions
        public async Task<IActionResult> Sessions(string? search, string? role, string? status)
        {
            var todayUtc = DateTime.UtcNow.Date;

            // ── Build base query ───────────────────────────────────────────────
            var query = _db.UserSessionLogs.AsQueryable();

            // ── Apply search filter ────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                query = query.Where(s =>
                    s.UserName.ToLower().Contains(lower) ||
                    s.UserRole.ToLower().Contains(lower) ||
                    s.IpAddress.ToLower().Contains(lower) ||
                    s.UserAgent.ToLower().Contains(lower));
            }

            // ── Apply role filter ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(s => s.UserRole == role);

            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

            // ── Apply status filter ────────────────────────────────────────────
            if (status == "active")
                query = query.Where(s => s.IsActive && s.LastActivityTime >= fiveMinutesAgo);
            else if (status == "ended")
                query = query.Where(s => !s.IsActive || s.LastActivityTime < fiveMinutesAgo);

            // ── Fetch sessions (newest first) ──────────────────────────────────
            var sessions = await query
                .OrderByDescending(s => s.LoginTime)
                .Take(500)          // cap at 500 rows for performance
                .ToListAsync();

            // ── KPI: Total logins today (unfiltered) ──────────────────────────
            var totalLoginsToday = await _db.UserSessionLogs
                .CountAsync(s => s.LoginTime >= todayUtc);

            // ── KPI: Active online users (unfiltered, active within last 5 minutes) ──
            var activeOnlineUsers = await _db.UserSessionLogs
                .Where(s => s.IsActive && s.LastActivityTime >= fiveMinutesAgo)
                .CountAsync();

            // ── KPI: Average session duration for completed sessions today ─────
            var completedToday = await _db.UserSessionLogs
                .Where(s => !s.IsActive && s.LoginTime >= todayUtc && s.DurationMinutes > 0)
                .Select(s => s.DurationMinutes)
                .ToListAsync();

            var averageSessionMinutes = completedToday.Count > 0
                ? completedToday.Average()
                : 0.0;

            var vm = new SessionsViewModel
            {
                TotalLoginsToday      = totalLoginsToday,
                ActiveOnlineUsers     = activeOnlineUsers,
                AverageSessionMinutes = averageSessionMinutes,
                Sessions              = sessions,
                SearchTerm            = search,
                RoleFilter            = role,
                StatusFilter          = status
            };

            return View(vm);
        }
    }
}
