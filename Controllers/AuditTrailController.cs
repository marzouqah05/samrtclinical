using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditTrailController : Controller
    {
        private readonly ClinicDbContext _context;

        public AuditTrailController(ClinicDbContext context)
        {
            _context = context;
        }

        // GET: /AuditTrail
        public async Task<IActionResult> Index(string? search, string? actionType, string? entityName, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _context.AuditLogs.AsNoTracking().AsQueryable();

                // ── 1. Search Filter (User, Action, Entity, Details) ──
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToLower();
                    query = query.Where(a =>
                        (a.UserName != null && a.UserName.ToLower().Contains(term)) ||
                        (a.Action != null && a.Action.ToLower().Contains(term)) ||
                        (a.EntityName != null && a.EntityName.ToLower().Contains(term)) ||
                        (a.EntityId != null && a.EntityId.ToLower().Contains(term)) ||
                        (a.Details != null && a.Details.ToLower().Contains(term)));
                }

                // ── 2. Action Filter ──
                if (!string.IsNullOrWhiteSpace(actionType))
                {
                    query = query.Where(a => a.Action == actionType);
                }

                // ── 3. Entity Filter ──
                if (!string.IsNullOrWhiteSpace(entityName))
                {
                    query = query.Where(a => a.EntityName == entityName);
                }

                // ── 4. Date Range Filter ──
                if (fromDate.HasValue)
                {
                    var fromUtc = fromDate.Value.Date;
                    query = query.Where(a => a.Timestamp >= fromUtc);
                }

                if (toDate.HasValue)
                {
                    var toUtc = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(a => a.Timestamp <= toUtc);
                }

                var logs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .Take(500)
                    .ToListAsync();

                // Ensure non-null list
                logs ??= new List<AuditLog>();

                // ── 5. KPI Metrics ──
                var todayUtc = DateTime.UtcNow.Date;
                var totalEvents = await _context.AuditLogs.CountAsync();
                var todayEvents = await _context.AuditLogs.CountAsync(a => a.Timestamp >= todayUtc);
                var distinctUsers = await _context.AuditLogs.Select(a => a.UserName).Distinct().CountAsync();
                var criticalActions = await _context.AuditLogs.CountAsync(a => a.Action.Contains("DELETE") || a.Action.Contains("PURGE") || a.Action.Contains("SECURITY"));

                ViewBag.TotalEvents = totalEvents;
                ViewBag.TodayEvents = todayEvents;
                ViewBag.DistinctUsers = distinctUsers;
                ViewBag.CriticalActions = criticalActions;

                ViewBag.Search = search;
                ViewBag.ActionType = actionType;
                ViewBag.EntityName = entityName;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

                // Dropdown lists
                ViewBag.AvailableActions = await _context.AuditLogs
                    .Select(a => a.Action)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToListAsync() ?? new List<string>();

                ViewBag.AvailableEntities = await _context.AuditLogs
                    .Select(a => a.EntityName)
                    .Distinct()
                    .OrderBy(e => e)
                    .ToListAsync() ?? new List<string>();

                return View(logs);
            }
            catch (Exception)
            {
                // In case table does not exist or connection fails, return graceful empty list
                ViewBag.TotalEvents = 0;
                ViewBag.TodayEvents = 0;
                ViewBag.DistinctUsers = 0;
                ViewBag.CriticalActions = 0;
                ViewBag.AvailableActions = new List<string>();
                ViewBag.AvailableEntities = new List<string>();

                return View(new List<AuditLog>());
            }
        }
    }
}
