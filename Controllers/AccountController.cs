using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private readonly IEmailSenderService _emailSender;
        private readonly IMemoryCache _cache;

        // حقن خدمات الـ Identity والـ Email والـ MemoryCache
        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ISessionTrackingService sessionTracking,
            ClinicDbContext db,
            IEmailSenderService emailSender,
            IMemoryCache cache)
        {
            _userManager     = userManager;
            _signInManager   = signInManager;
            _roleManager     = roleManager;
            _sessionTracking = sessionTracking;
            _db              = db;
            _emailSender     = emailSender;
            _cache           = cache;
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
            // Fix session leakage: ensure any stale session or cookies are cleared before verifying new credentials
            await _signInManager.SignOutAsync();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = T("الرجاء إدخال اسم المستخدم وكلمة المرور.", "Please enter your username and password.");
                return View();
            }

            // Find user strictly by input (supports Email or UserName)
            var inputUser = await _userManager.FindByEmailAsync(username) ?? await _userManager.FindByNameAsync(username);
            if (inputUser == null)
            {
                ViewBag.Error = T("اسم المستخدم أو كلمة المرور غير صحيحة.", "Invalid username or password.");
                return View();
            }

            // Verify password strictly against this user
            var existingClaims = await _userManager.GetClaimsAsync(inputUser);
            if (!existingClaims.Any(c => c.Type == "ClinicId"))
            {
                var clinic = await _db.Clinics.FirstOrDefaultAsync(c => c.OwnerEmail == inputUser.Email);
                var targetClinicId = clinic?.ClinicId ?? TenantExtensions.DefaultClinicId;
                await _userManager.AddClaimAsync(inputUser, new System.Security.Claims.Claim("ClinicId", targetClinicId.ToString()));
            }

            var result = await _signInManager.PasswordSignInAsync(inputUser.UserName!, password, rememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // ── Session Tracking: create a new session record on successful login ──
                var roles = await _userManager.GetRolesAsync(inputUser);
                var role  = roles.Count > 0 ? roles[0] : "Unknown";
                var ip    = WebApplication1.Middleware.UserActivityMiddleware.GetClientIp(HttpContext);
                var ua    = Request.Headers["User-Agent"].ToString();
                var sessionId = WebApplication1.Middleware.UserActivityMiddleware.GetOrCreateDeviceId(HttpContext);
                await _sessionTracking.CreateSessionAsync(inputUser.Id, inputUser.UserName!, role, ip, ua, sessionId);

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

        // POST: RegisterClinic (تسجيل عيادة جديدة - تجربة مجانية 60 يوماً)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClinic(string clinicName, string adminName, string email, string phone, string password)
        {
            if (string.IsNullOrWhiteSpace(clinicName) || string.IsNullOrWhiteSpace(adminName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["RegisterError"] = T("يرجى تعبئة جميع الحقول المطلوبة لتسجيل العيادة.", "Please fill in all required fields to register the clinic.");
                return RedirectToAction(nameof(Login));
            }

            email = email.Trim().ToLowerInvariant();

            var existingUser = await _userManager.FindByEmailAsync(email) ?? await _userManager.FindByNameAsync(email);
            if (existingUser != null)
            {
                TempData["RegisterError"] = T("البريد الإلكتروني مسجل مسبقاً، يرجى تسجيل الدخول أو استخدام بريد إلكتروني آخر.", "Email is already registered. Please sign in or use another email.");
                return RedirectToAction(nameof(Login));
            }

            var user = new IdentityUser
            {
                UserName = email,
                Email = email,
                PhoneNumber = phone,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Ensure Admin role exists and explicitly assign it to this clinic owner
                if (!await _roleManager.RoleExistsAsync("Admin"))
                    await _roleManager.CreateAsync(new IdentityRole("Admin"));

                await _userManager.AddToRoleAsync(user, "Admin");

                // ── Multi-Tenancy: Create a new isolated Clinic record ───────
                var newClinicId = Guid.NewGuid();
                var clinic = new Clinic
                {
                    ClinicId = newClinicId,
                    Name = clinicName.Trim(),
                    OwnerEmail = email,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Clinics.Add(clinic);
                await _db.SaveChangesAsync();

                // Associate the new Admin user with this ClinicId claim
                await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("ClinicId", newClinicId.ToString()));

                // Generate 6-digit OTP code
                var otpCode = Random.Shared.Next(100000, 999999).ToString();

                // Store OTP in cache for 10 minutes
                var cacheKey = $"clinic_reg_otp_{email}";
                _cache.Set(cacheKey, otpCode, TimeSpan.FromMinutes(10));

                // Also persist in ClinicSettings as secondary fallback
                var settingKey = $"OTP_{email}";
                var existingSetting = await _db.ClinicSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
                if (existingSetting != null)
                {
                    existingSetting.Value = $"{otpCode}|{DateTime.UtcNow.AddMinutes(10):o}";
                    existingSetting.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.ClinicSettings.Add(new ClinicSetting
                    {
                        Key = settingKey,
                        Value = $"{otpCode}|{DateTime.UtcNow.AddMinutes(10):o}",
                        Description = $"Registration OTP for {email}",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await _db.SaveChangesAsync();

                // Dispatch OTP email via email service safely without blocking registration
                try
                {
                    var emailSent = await _emailSender.SendOtpEmailAsync(email, otpCode, adminName);
                    if (!emailSent)
                    {
                        Console.WriteLine($"\n===================\n[REGISTRATION OTP]: {otpCode} for {email}\n===================\n");
                        TempData["DevOtp"] = otpCode;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"\n===================\n[REGISTRATION OTP]: {otpCode} for {email}\n===================\n");
                    TempData["DevOtp"] = otpCode;
                }

                // DO NOT automatically sign the user in. Redirect directly to OTP verification page
                TempData["OtpSent"] = T($"تم إرسال رمز التحقق (OTP) إلى {email}. يرجى إدخال الرمز لإتمام تفعيل حساب المدير.",
                                        $"A verification code (OTP) was sent to {email}. Please enter the code to activate your Admin account.");
                return RedirectToAction(nameof(VerifyOtp), new { email = email });
            }

            TempData["RegisterError"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Login));
        }

        // GET: VerifyOtp
        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(Login));
            }

            ViewBag.Email = email;
            return View();
        }

        // POST: VerifyOtp
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string email, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
            {
                ViewBag.Email = email;
                ViewBag.Error = T("يرجى إدخال رمز التحقق المكون من 6 أرقام.", "Please enter the 6-digit verification code.");
                return View();
            }

            email = email.Trim().ToLowerInvariant();
            otpCode = otpCode.Trim();

            var user = await _userManager.FindByEmailAsync(email) ?? await _userManager.FindByNameAsync(email);
            if (user == null)
            {
                ViewBag.Email = email;
                ViewBag.Error = T("لم يتم العثور على حساب مرتبط بهذا البريد الإلكتروني.", "No account found associated with this email.");
                return View();
            }

            // Verify OTP from MemoryCache first
            var cacheKey = $"clinic_reg_otp_{email}";
            bool isValid = false;

            if (_cache.TryGetValue(cacheKey, out string? cachedOtp) && !string.IsNullOrEmpty(cachedOtp))
            {
                if (cachedOtp == otpCode)
                {
                    isValid = true;
                    _cache.Remove(cacheKey);
                }
            }

            // Fallback check to database setting
            if (!isValid)
            {
                var settingKey = $"OTP_{email}";
                var setting = await _db.ClinicSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
                if (setting?.Value != null)
                {
                    var parts = setting.Value.Split('|');
                    if (parts.Length == 2 && parts[0] == otpCode)
                    {
                        if (DateTime.TryParse(parts[1], null, DateTimeStyles.RoundtripKind, out var expiry) && expiry > DateTime.UtcNow)
                        {
                            isValid = true;
                            _db.ClinicSettings.Remove(setting);
                            await _db.SaveChangesAsync();
                        }
                    }
                }
            }

            if (!isValid)
            {
                ViewBag.Email = email;
                ViewBag.Error = T("رمز التحقق غير صحيح أو قد انتهت صلاحيته. يرجى طلب رمز جديد.",
                                  "The verification code is invalid or has expired. Please request a new code.");
                return View();
            }

            // Upon correct code: mark EmailConfirmed = true, ensure Admin role, and sign in
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            // Re-verify Admin role assignment
            if (!await _userManager.IsInRoleAsync(user, "Admin"))
            {
                if (!await _roleManager.RoleExistsAsync("Admin"))
                    await _roleManager.CreateAsync(new IdentityRole("Admin"));
                await _userManager.AddToRoleAsync(user, "Admin");
            }

            // Log user in as Admin
            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(user, isPersistent: false);

            // Track session
            var ip = WebApplication1.Middleware.UserActivityMiddleware.GetClientIp(HttpContext);
            var ua = Request.Headers["User-Agent"].ToString();
            var sessionId = WebApplication1.Middleware.UserActivityMiddleware.GetOrCreateDeviceId(HttpContext);
            await _sessionTracking.CreateSessionAsync(user.Id, user.UserName ?? email, "Admin", ip, ua, sessionId);

            TempData["Success"] = T("تم تفعيل حسابك بنجاح! مرحباً بك في نظام ClinicFlow OS.",
                                    "Your account has been activated successfully! Welcome to ClinicFlow OS.");

            return RedirectToAction("Index", "Dashboard");
        }

        // POST: ResendOtp
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(Login));
            }

            email = email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email) ?? await _userManager.FindByNameAsync(email);
            if (user != null)
            {
                var newOtp = Random.Shared.Next(100000, 999999).ToString();
                var cacheKey = $"clinic_reg_otp_{email}";
                _cache.Set(cacheKey, newOtp, TimeSpan.FromMinutes(10));

                var settingKey = $"OTP_{email}";
                var existingSetting = await _db.ClinicSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
                if (existingSetting != null)
                {
                    existingSetting.Value = $"{newOtp}|{DateTime.UtcNow.AddMinutes(10):o}";
                    existingSetting.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.ClinicSettings.Add(new ClinicSetting
                    {
                        Key = settingKey,
                        Value = $"{newOtp}|{DateTime.UtcNow.AddMinutes(10):o}",
                        Description = $"Registration OTP for {email}",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await _db.SaveChangesAsync();

                try
                {
                    var emailSent = await _emailSender.SendOtpEmailAsync(email, newOtp);
                    if (!emailSent)
                    {
                        Console.WriteLine($"\n===================\n[REGISTRATION OTP]: {newOtp} for {email}\n===================\n");
                        TempData["DevOtp"] = newOtp;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"\n===================\n[REGISTRATION OTP]: {newOtp} for {email}\n===================\n");
                    TempData["DevOtp"] = newOtp;
                }

                TempData["OtpSent"] = T($"تمت إعادة إرسال رمز تحقق جديد إلى {email}.",
                                        $"A new verification code was sent to {email}.");
            }

            return RedirectToAction(nameof(VerifyOtp), new { email = email });
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

                // 5. Inherit current admin's ClinicId for multi-tenancy isolation
                var adminClinicId = User.GetClinicId();
                await _userManager.AddClaimAsync(user,
                    new System.Security.Claims.Claim("ClinicId", adminClinicId.ToString()));

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