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
    }
}
