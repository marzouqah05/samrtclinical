using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ISessionTrackingService _sessionTracking;
        private readonly ClinicDbContext _db;

        // حقن خدمات الـ Identity الأساسية عبر الـ Constructor
        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ISessionTrackingService sessionTracking,
            ClinicDbContext db)
        {
            _userManager     = userManager;
            _signInManager   = signInManager;
            _roleManager     = roleManager;
            _sessionTracking = sessionTracking;
            _db              = db;
        }

        // ── Bilingual helper ──────────────────────────────────────────────────
        private static string T(string ar, string en)
            => CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ar" ? ar : en;

        // GET: Login Page
        [AllowAnonymous]
        public IActionResult Login()
        {
            // إذا كان المستخدم مسجل دخوله مسبقاً، يتم توجيهه مباشرة للوحة التحكم
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        // POST: Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken] // حماية ضد هجمات CSRF
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = T("الرجاء إدخال اسم المستخدم وكلمة المرور.", "Please enter your username and password.");
                return View();
            }

            // تسجيل الدخول باستخدام الـ SignInManager الخاص بالـ Identity
            var result = await _signInManager.PasswordSignInAsync(username, password, rememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // ── Session Tracking: create a new session record on successful login ──
                var loggedInUser = await _userManager.FindByNameAsync(username);
                if (loggedInUser != null)
                {
                    var roles = await _userManager.GetRolesAsync(loggedInUser);
                    var role  = roles.Count > 0 ? roles[0] : "Unknown";
                    var ip    = WebApplication1.Middleware.UserActivityMiddleware.GetClientIp(HttpContext);
                    var ua    = Request.Headers["User-Agent"].ToString();
                    var sessionId = WebApplication1.Middleware.UserActivityMiddleware.GetOrCreateDeviceId(HttpContext);
                    await _sessionTracking.CreateSessionAsync(loggedInUser.Id, username, role, ip, ua, sessionId);
                }

                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                ViewBag.Error = T("تم قفل الحساب مؤقتاً بسبب محاولات دخول خاطئة متكررة.",
                                  "This account has been temporarily locked due to repeated failed login attempts.");
                return View();
            }

            ViewBag.Error = T("اسم المستخدم أو كلمة المرور غير صحيحة.",
                              "Invalid username or password.");
            return View();
        }

        // GET: Register Page (لإنشاء حسابات الموظفين والأطباء)
        [Authorize(Roles = "Admin")] // فقط الأدمن يستطيع إنشاء حسابات جديدة بالنظام
        public async Task<IActionResult> Register()
        {
            // Pass list of doctors that do NOT yet have a linked Identity user account.
            // We detect "no linked account" by checking whether any IdentityUser's UserName
            // matches the doctor's name (simple heuristic). The dropdown lets admins bind
            // an existing doctor record to the new account via a hidden DoctorId field.
            var doctors = await _db.Doctors
                .OrderBy(d => d.DoctorName)
                .Select(d => new { d.DoctorId, d.DoctorName, d.Specialization })
                .ToListAsync();

            ViewBag.Doctors = doctors;
            return View();
        }

        // POST: Register
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string username, string email, string password, string role,
            int? doctorId)
        {
            // ── Reload doctors list for the view in case we return early ──────
            async Task ReloadDoctors()
            {
                ViewBag.Doctors = await _db.Doctors
                    .OrderBy(d => d.DoctorName)
                    .Select(d => new { d.DoctorId, d.DoctorName, d.Specialization })
                    .ToListAsync();
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(role))
            {
                ViewBag.Error = T("جميع الحقول مطلوبة.", "All fields are required.");
                await ReloadDoctors();
                return View();
            }

            // ── For the Doctor role, validate that a doctor was selected ──────
            if (role == "Doctor" && !doctorId.HasValue)
            {
                ViewBag.Error = T("الرجاء اختيار طبيب من قائمة الأطباء المسجلين.",
                                  "Please select a doctor from the registered doctors list.");
                await ReloadDoctors();
                return View();
            }

            // ── Backend Duplicate Check for Email Uniqueness ──────────────────
            if (!string.IsNullOrEmpty(email))
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null)
                {
                    ViewBag.Error = T(
                        "هذا البريد الإلكتروني مسجل مسبقاً لمستخدم آخر، يرجى استخدام بريد إلكتروني مختلف.",
                        "This email is already registered to another user. Please use a different email.");
                    await ReloadDoctors();
                    return View();
                }
            }

            // ── 1. Create the Identity user ───────────────────────────────────
            var user = new IdentityUser { UserName = username, Email = email };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // 2. Ensure the role exists
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));

                // 3. Assign role
                await _userManager.AddToRoleAsync(user, role);

                // 4. If Doctor: store DoctorId claim so the rest of the app can
                //    correlate Identity user ↔ Doctor record without schema changes.
                if (role == "Doctor" && doctorId.HasValue)
                {
                    await _userManager.AddClaimAsync(user,
                        new System.Security.Claims.Claim("DoctorId", doctorId.Value.ToString()));
                }

                // ── Bilingual success toast ───────────────────────────────────
                ViewBag.Success = T(
                    $"تمت إضافة المستخدم '{username}' بصلاحية {role} بنجاح ✓",
                    $"User '{username}' added successfully with the '{role}' role ✓");

                await ReloadDoctors();
                return View();
            }

            // ── Surface Identity errors (weak password, duplicate username, …) ─
            ViewBag.Error = string.Join(" | ", System.Linq.Enumerable.Select(result.Errors, e => e.Description));
            await ReloadDoctors();
            return View();
        }

        // POST: Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // ── Session Tracking: mark session as ended before signing out ──
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var sessionId = WebApplication1.Middleware.UserActivityMiddleware.GetOrCreateDeviceId(HttpContext);
                var ua = Request.Headers["User-Agent"].ToString();
                await _sessionTracking.EndSessionAsync(userId, sessionId, ua);
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        // GET: Access Denied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ── GET: Staff & User Management List ───────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> StaffList()
        {
            var allUsers = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();
            var staffList = new List<(IdentityUser User, string Role, string CreatedDate)>();
            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var roleLabel = roles.FirstOrDefault() ?? "Unknown";
                staffList.Add((u, roleLabel, ""));
            }
            ViewBag.StaffList = staffList;
            return View();
        }

        // ── AJAX: Return doctor details by id (for auto-fill) ─────────────────
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDoctorDetails(int id)
        {
            var doctor = await _db.Doctors
                .Where(d => d.DoctorId == id)
                .Select(d => new { d.DoctorId, d.DoctorName, d.Specialization, d.DoctorNumber })
                .FirstOrDefaultAsync();

            if (doctor == null)
                return NotFound();

            return Json(doctor);
        }

        // ── POST: Delete a staff user ──────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = T("معرّف المستخدم غير صالح.", "Invalid user ID.");
                return RedirectToAction(nameof(StaffList));
            }

            // Prevent self-deletion
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentUserId)
            {
                TempData["Error"] = T("لا يمكنك حذف حسابك الخاص.", "You cannot delete your own account.");
                return RedirectToAction(nameof(StaffList));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = T("المستخدم غير موجود.", "User not found.");
                return RedirectToAction(nameof(StaffList));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = T(
                    $"تم حذف حساب '{user.UserName}' بنجاح.",
                    $"Account '{user.UserName}' was deleted successfully.");
            }
            else
            {
                TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(StaffList));
        }
    }
}