using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Owner,Admin,SuperAdmin")]
    public class StaffController : Controller
    {
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
        public IActionResult Edit(string id)
        {
            return RedirectToAction("EditStaff", "Account", new { id });
        }

        [HttpGet]
        [HttpPost]
        public IActionResult Logout()
        {
            return RedirectToAction("Logout", "Account");
        }
    }
}
