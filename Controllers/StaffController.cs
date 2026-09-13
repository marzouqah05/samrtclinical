using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin")]
    public class StaffController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public StaffController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("StaffList", "Account");
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction("Register", "Account");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var targetUser = await _userManager.FindByIdAsync(id);
            if (targetUser != null)
            {
                var roles = await _userManager.GetRolesAsync(targetUser);
                if ((roles.Contains("Owner") || roles.Contains("SuperAdmin")) && !User.IsOwner())
                {
                    throw new UnauthorizedAccessException("Forbidden: Target user has role Owner and current user is not Owner.");
                }
            }

            return RedirectToAction("EditStaff", "Account", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var targetUser = await _userManager.FindByIdAsync(id);
            if (targetUser != null)
            {
                var roles = await _userManager.GetRolesAsync(targetUser);
                if (roles.Contains("Owner") || roles.Contains("SuperAdmin"))
                {
                    throw new UnauthorizedAccessException("Forbidden: Owner accounts are protected by system immunity and cannot be deleted.");
                }
            }

            return RedirectToAction("DeleteUser", "Account", new { id });
        }

        [HttpGet]
        [HttpPost]
        public IActionResult Logout()
        {
            return RedirectToAction("Logout", "Account");
        }
    }
}

