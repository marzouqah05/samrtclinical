using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly IBackupService _backupService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;

        public SettingsController(
            ISettingsService settingsService,
            IBackupService backupService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env)
        {
            _settingsService = settingsService;
            _backupService   = backupService;
            _userManager     = userManager;
            _roleManager     = roleManager;
            _env             = env;
        }

        // ── GET /Settings (Dashboard Hub) ─────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _settingsService.GetSettingsAsync();
            return View(model);
        }

        // ── GET /Settings/General (Clinic Profile & Branding) ─────────────────
        [HttpGet]
        public async Task<IActionResult> General()
        {
            var model = await _settingsService.GetSettingsAsync();
            return View(model);
        }

        // ── GET /Settings/Hours (Operating Hours & Scheduling) ────────────────
        [HttpGet]
        public async Task<IActionResult> Hours()
        {
            var model = await _settingsService.GetSettingsAsync();
            return View(model);
        }

        // ── GET /Settings/WorkingHours (Alias for Hours) ───────────────────────
        [HttpGet]
        public IActionResult WorkingHours() => RedirectToAction(nameof(Hours));

        // ── GET /Settings/Automation (WhatsApp & n8n Integration) ─────────────
        [HttpGet]
        public async Task<IActionResult> Automation()
        {
            var model = await _settingsService.GetSettingsAsync();
            return View(model);
        }

        // ── GET /Settings/Security (Backups, DB Tools & Password) ─────────────
        [HttpGet]
        public async Task<IActionResult> Security()
        {
            var model = await _settingsService.GetSettingsAsync();
            model.CloudSync = await _backupService.GetCloudSyncStatusAsync();
            model.RecentBackups = _backupService.GetRecentBackups();
            model.IsMonthlyReminderActive = _backupService.IsMonthlyReminderActive();
            return View(model);
        }

        // ── POST /Settings/UpdateClinicProfile ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateClinicProfile(
            ClinicSettingsViewModel model,
            IFormFile? logoFile)
        {
            // Server-side validation for required fields only
            if (string.IsNullOrWhiteSpace(model.ClinicName) ||
                string.IsNullOrWhiteSpace(model.ClinicPhone) ||
                string.IsNullOrWhiteSpace(model.ClinicEmail))
            {
                TempData["Error"] = "Please fill in all required clinic profile fields (Name, Phone, Email).";
                return RedirectToAction(nameof(General));
            }

            try
            {
                // Handle logo file upload
                if (logoFile != null && logoFile.Length > 0)
                {
                    var allowedExt = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
                    var ext = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
                    if (!allowedExt.Contains(ext))
                    {
                        TempData["Error"] = "Invalid logo file type. Please upload PNG, JPG, WEBP, or SVG.";
                        return RedirectToAction(nameof(General));
                    }

                    if (logoFile.Length > 2 * 1024 * 1024) // 2 MB limit
                    {
                        TempData["Error"] = "Logo file is too large. Maximum allowed size is 2 MB.";
                        return RedirectToAction(nameof(General));
                    }

                    var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "logos");
                    Directory.CreateDirectory(uploadsDir);

                    var fileName = $"clinic-logo{ext}";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await logoFile.CopyToAsync(stream);
                    }

                    model.LogoPath = $"/uploads/logos/{fileName}";
                }
                else if (string.IsNullOrEmpty(model.LogoPath))
                {
                    // Preserve existing logo if no new file uploaded
                    model.LogoPath = await _settingsService.GetSettingValueAsync("LogoUrl");
                }

                await _settingsService.SaveGeneralSettingsAsync(model);
                TempData["Success"] = "✅ Clinic profile & financial settings updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to save clinic profile: {ex.Message}";
            }

            return RedirectToAction(nameof(General));
        }

        // ── Backward-compatible alias for old action name ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGeneral(
            ClinicSettingsViewModel model,
            IFormFile? logoFile)
            => await UpdateClinicProfile(model, logoFile);

        // ── POST /Settings/UpdateWorkingHours ─────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateWorkingHours(ClinicSettingsViewModel model)
        {
            try
            {
                await _settingsService.SaveWorkingHoursSettingsAsync(model);
                TempData["Success"] = "✅ Operating hours & scheduling parameters saved successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to save working hours: {ex.Message}";
            }

            return RedirectToAction(nameof(Hours));
        }

        // ── POST /Settings/UpdateWhatsAppConfig ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateWhatsAppConfig(ClinicSettingsViewModel model)
        {
            try
            {
                await _settingsService.SaveIntegrationSettingsAsync(model);
                TempData["Success"] = "✅ WhatsApp & automation settings saved successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to save WhatsApp settings: {ex.Message}";
            }

            return RedirectToAction(nameof(Automation));
        }

        // ── Backward-compatible alias for old action name ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateIntegrations(ClinicSettingsViewModel model)
        {
            return await UpdateWhatsAppConfig(model);
        }

        // ── POST /Settings/TestWhatsAppWebhook ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestWhatsAppWebhook(string? webhookUrl)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                TempData["Error"] = "Please provide a valid Webhook URL to test.";
                return RedirectToAction(nameof(Automation));
            }

            var (success, message) = await _settingsService.TestWhatsAppWebhookAsync(webhookUrl);
            if (success)
                TempData["Success"] = message;
            else
                TempData["Error"] = message;

            return RedirectToAction(nameof(Automation));
        }

        // ── GET /Settings/DownloadDailySnapshot ───────────────────────────────
        [HttpGet]
        public async Task<IActionResult> DownloadDailySnapshot()
        {
            var zipBytes = await _backupService.GenerateDailySnapshotAsync();
            var fileName = $"Daily_Snapshot_{DateTime.Now:yyyyMMdd_HHmm}.zip";
            return File(zipBytes, "application/zip", fileName);
        }

        // ── GET /Settings/DownloadMonthlyArchive ──────────────────────────────
        [HttpGet]
        public async Task<IActionResult> DownloadMonthlyArchive()
        {
            var zipBytes = await _backupService.GenerateFullSystemArchiveAsync();
            var fileName = $"Full_Monthly_Archive_{DateTime.Now:yyyyMMdd_HHmm}.zip";
            return File(zipBytes, "application/zip", fileName);
        }

        // ── POST /Settings/SyncToGoogleDriveNow ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncToGoogleDriveNow()
        {
            try
            {
                var result = await _backupService.SyncToGoogleDriveAsync();
                if (result.IsSuccess)
                {
                    TempData["Success"] = $"☁️ Google Drive Sync Completed! Snapshot archive '{result.LastSyncedFile}' uploaded & encrypted.";
                }
                else
                {
                    TempData["Error"] = $"Cloud sync failed: {result.StatusMessage}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Cloud sync encountered an error: {ex.Message}";
            }

            return RedirectToAction(nameof(Security));
        }

        // ── POST /Settings/BackupDatabase (Legacy alias) ───────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BackupDatabase()
        {
            return RedirectToAction(nameof(DownloadDailySnapshot));
        }

        // ── POST /Settings/ChangePassword ─────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ClinicSettingsViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                TempData["Error"] = "Please provide your current password and a valid new password.";
                return RedirectToAction(nameof(Security));
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "The new password and confirmation do not match.";
                return RedirectToAction(nameof(Security));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "Unable to retrieve current user session.";
                return RedirectToAction(nameof(Security));
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["Success"] = "✅ Your account password has been changed successfully!";
            }
            else
            {
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                TempData["Error"] = $"Password change failed: {errors}";
            }

            return RedirectToAction(nameof(Security));
        }
    }
}
