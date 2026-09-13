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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ISessionTrackingService _sessionTracking;
        private readonly ClinicDbContext _db;
        private readonly IEmailSenderService _emailSender;
        private readonly IMemoryCache _cache;

        // حقن خدمات الـ Identity والـ Email والـ MemoryCache
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
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
        public async Task<IActionResult> Login([FromForm] LoginViewModel? model, string? username = null, string? password = null, bool rememberMe = false)
        {
            // Fix session leakage: ensure any stale session or cookies are cleared before verifying new credentials
            await _signInManager.SignOutAsync();

            var identifier = model?.EmailOrUsername ?? model?.Username ?? username ?? string.Empty;
            var inputPassword = model?.Password ?? password ?? string.Empty;
            var isRememberMe = model?.RememberMe ?? rememberMe;

            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(inputPassword))
            {
                ViewBag.Error = T("الرجاء إدخال اسم المستخدم وكلمة المرور.", "Please enter your username and password.");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            var cleanIdentifier = identifier.Trim();

            // 1. Dual authentication: Find user by Username or Email seamlessly
            ApplicationUser? user = null;
            if (cleanIdentifier.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(cleanIdentifier) 
                       ?? await _userManager.FindByNameAsync(cleanIdentifier);
            }
            else
            {
                user = await _userManager.FindByNameAsync(cleanIdentifier) 
                       ?? await _userManager.FindByEmailAsync(cleanIdentifier);
            }

            if (user == null)
            {
                Console.WriteLine($"[LOGIN ATTEMPT] User: {cleanIdentifier} | Succeeded: False | Reason: UserNotFound");
                ViewBag.Error = T("اسم المستخدم أو كلمة المرور غير صحيحة.", "Invalid username or password.");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            // 2. If user exists but EmailConfirmed is false, force set it to true:
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            // Ensure user is not locked out from prior failed attempts
            if (await _userManager.IsLockedOutAsync(user))
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            // If user has Doctor role, ensure DoctorId claim is populated
            try
            {
                if (await _userManager.IsInRoleAsync(user, "Doctor"))
                {
                    var claims = await _userManager.GetClaimsAsync(user);
                    if (!claims.Any(c => c.Type == "DoctorId"))
                    {
                        var doc = await _db.Doctors.FirstOrDefaultAsync(d => 
                            (user.ClinicId == null || d.ClinicId == user.ClinicId) && 
                            (d.DoctorEmail == user.Email || d.DoctorName == user.UserName || d.DoctorNumber == user.PhoneNumber));
                        if (doc != null)
                        {
                            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("DoctorId", doc.DoctorId.ToString()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Login DoctorId Claim Notice]: {ex.Message}");
            }

            // Ensure tenant claim exists
            try
            {
                var existingClaims = await _userManager.GetClaimsAsync(user);
                if (!existingClaims.Any(c => c.Type == "ClinicId"))
                {
                    if (user.ClinicId.HasValue && user.ClinicId.Value != Guid.Empty)
                    {
                        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("ClinicId", user.ClinicId.Value.ToString()));
                    }
                    else
                    {
                        var clinic = await _db.Clinics.FirstOrDefaultAsync(c => c.OwnerEmail == user.Email);
                        if (clinic != null)
                        {
                            user.ClinicId = clinic.ClinicId;
                            await _userManager.UpdateAsync(user);
                            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("ClinicId", clinic.ClinicId.ToString()));
                        }
                        else if (!await _userManager.IsInRoleAsync(user, "SuperAdmin"))
                        {
                            var newClinicId = Guid.NewGuid();
                            var newClinic = new Clinic
                            {
                                ClinicId = newClinicId,
                                Name = $"{user.UserName}'s Clinic",
                                OwnerEmail = user.Email,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.Clinics.Add(newClinic);
                            await _db.SaveChangesAsync();

                            user.ClinicId = newClinicId;
                            await _userManager.UpdateAsync(user);
                            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("ClinicId", newClinicId.ToString()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> [LOGIN CLAIMS NOTICE] {ex.Message}");
            }

            // 3. Authenticate using the resolved user.UserName:
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, 
                inputPassword, 
                isRememberMe, 
                lockoutOnFailure: false
            );

            // 4. If failed, print exact reason to console:
            if (!result.Succeeded)
            {
                Console.WriteLine($"[LOGIN ATTEMPT] User: {user?.Email} | Succeeded: {result.Succeeded} | IsLocked: {result.IsLockedOut} | NotAllowed: {result.IsNotAllowed}");

                // Direct check via UserManager as fallback in case of sign-in policy discrepancies
                var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, inputPassword);
                if (isPasswordCorrect)
                {
                    await _signInManager.SignInAsync(user, isPersistent: isRememberMe);
                    result = Microsoft.AspNetCore.Identity.SignInResult.Success;
                }
            }

            if (result.Succeeded)
            {
                Console.WriteLine($"--> [LOGIN SUCCESS] User '{user.UserName}' logged in successfully.");

                // ── Session Tracking: create a new session record on successful login ──
                try
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var role  = roles.Count > 0 ? roles[0] : "Admin";
                    var ip    = WebApplication1.Middleware.UserActivityMiddleware.GetClientIp(HttpContext);
                    var ua    = Request.Headers["User-Agent"].ToString();
                    var sessionId = WebApplication1.Middleware.UserActivityMiddleware.GetOrCreateDeviceId(HttpContext);
                    await _sessionTracking.CreateSessionAsync(user.Id, user.UserName!, role, ip, ua, sessionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"--> [LOGIN SESSION NOTICE] {ex.Message}");
                }

                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                ViewBag.Error = T("تم قفل الحساب مؤقتاً بسبب محاولات دخول خاطئة متكررة.",
                                  "This account has been temporarily locked due to repeated failed login attempts.");
                ModelState.AddModelError(string.Empty, "This account has been temporarily locked.");
                return View(model);
            }

            ViewBag.Error = T("اسم المستخدم أو كلمة المرور غير صحيحة.",
                              "Invalid username or password.");
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        // POST: RegisterClinic (تسجيل عيادة جديدة - تجربة مجانية 60 يوماً)
        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RegisterClinic([FromForm] RegisterClinicViewModel model)
        {
            var clinicName = model?.ClinicName;
            var adminName  = model?.AdminName;
            var email      = model?.Email;
            var phone      = model?.Phone;
            var password   = model?.Password;

            Console.WriteLine($"--> [REGISTER START] Received clinic registration: ClinicName='{clinicName}', AdminName='{adminName}', Email='{email}'");

            if (string.IsNullOrWhiteSpace(clinicName) || string.IsNullOrWhiteSpace(adminName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("--> [REGISTER REJECTED] One or more required fields are empty.");
                TempData["RegisterError"] = T("يرجى تعبئة جميع الحقول المطلوبة لتسجيل العيادة.", "Please fill in all required fields to register the clinic.");
                return RedirectToAction(nameof(Login));
            }

            var cleanEmail = email.Trim().ToLowerInvariant();
            Console.WriteLine($"--> [REGISTER CHECK] Querying _userManager.FindByEmailAsync('{cleanEmail}')...");

            var existingUser = await _userManager.FindByEmailAsync(cleanEmail);
            if (existingUser != null)
            {
                Console.WriteLine($"--> [REGISTER CHECK] User found: ID='{existingUser.Id}', EmailConfirmed={existingUser.EmailConfirmed}");
                if (existingUser.EmailConfirmed)
                {
                    Console.WriteLine($"--> [REGISTER REJECTED] Email '{cleanEmail}' is already confirmed. Returning duplicate email notice.");
                    TempData["RegisterError"] = T("البريد الإلكتروني مسجل مسبقاً، يرجى تسجيل الدخول أو استخدام بريد إلكتروني آخر.", "Email is already registered. Please sign in or use another email.");
                    return RedirectToAction(nameof(Login));
                }
                else
                {
                    Console.WriteLine($"--> [REGISTER CLEANUP] Email '{cleanEmail}' was previously registered but unconfirmed. Removing incomplete user to start fresh.");
                    await _userManager.DeleteAsync(existingUser);
                    var oldClinic = await _db.Clinics.FirstOrDefaultAsync(c => c.OwnerEmail == cleanEmail);
                    if (oldClinic != null)
                    {
                        _db.Clinics.Remove(oldClinic);
                        await _db.SaveChangesAsync();
                    }
                }
            }
            else
            {
                Console.WriteLine($"--> [REGISTER CHECK] Email '{cleanEmail}' does not exist in AspNetUsers. Proceeding with registration.");
            }

            // ── 1. Multi-Tenancy: Create a new isolated Clinic record ───────
            var newClinicId = Guid.NewGuid();
            var clinic = new Clinic
            {
                ClinicId = newClinicId,
                Name = clinicName.Trim(),
                OwnerEmail = cleanEmail,
                CreatedAt = DateTime.UtcNow
            };
            _db.Clinics.Add(clinic);
            await _db.SaveChangesAsync();
            Console.WriteLine($"--> [REGISTER] Created clinic '{clinic.Name}' (ID: {newClinicId})");

            // ── 2. Create ApplicationUser with ClinicId ────────────────────
            var user = new ApplicationUser
            {
                UserName = cleanEmail,
                FullName = adminName?.Trim() ?? clinicName.Trim(),
                Email = cleanEmail,
                PhoneNumber = phone?.Trim(),
                ClinicId = newClinicId,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errorSummary = string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                Console.WriteLine($"--> [REGISTER ERROR] UserManager.CreateAsync failed: {errorSummary}");

                // Rollback clinic creation
                _db.Clinics.Remove(clinic);
                await _db.SaveChangesAsync();

                TempData["RegisterError"] = string.Join(" | ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Login));
            }

            Console.WriteLine($"--> [REGISTER] ApplicationUser created successfully (ID: {user.Id})");

            // ── 3. Assign Owner & Admin Role & Tenant Claim ────────────────────────
            if (!await _roleManager.RoleExistsAsync("Owner"))
                await _roleManager.CreateAsync(new IdentityRole("Owner"));
            if (!await _roleManager.RoleExistsAsync("Admin"))
                await _roleManager.CreateAsync(new IdentityRole("Admin"));

            await _userManager.AddToRoleAsync(user, "Owner");
            await _userManager.AddToRoleAsync(user, "Admin");
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("ClinicId", newClinicId.ToString()));

            // ── 4. Generate cryptographically secure 6-digit numeric OTP ──
            var otpCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            Console.WriteLine($"--> [REGISTER] Generated 6-digit OTP code.");

            // Store OTP in cache for 10 minutes
            var cacheKey = $"clinic_reg_otp_{cleanEmail}";
            _cache.Set(cacheKey, otpCode, TimeSpan.FromMinutes(10));

            // Also persist in ClinicSettings as secondary fallback
            var settingKey = $"OTP_{cleanEmail}";
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
                    Description = $"Registration OTP for {cleanEmail}",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync();

            // ── 5. Dispatch OTP email via Gmail SMTP ───────────────────────
            Console.WriteLine("=================================================");
            Console.WriteLine($"--> [LIVE OTP BACKUP] Email: {cleanEmail} | CODE: {otpCode}");
            Console.WriteLine("=================================================");

            try
            {
                Console.WriteLine($"--> [REGISTER] Attempting OTP email to '{cleanEmail}' via Gmail SMTP...");
                await _emailSender.SendOtpEmailAsync(cleanEmail, otpCode, adminName?.Trim() ?? "");
                Console.WriteLine("--> [REGISTER] OTP dispatch call completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> [REGISTER NON-FATAL] EmailService failed ({ex.Message}). User and OTP preserved.");
            }

            // ── 6. Redirect to VerifyOtp ───────────────────────────────────
            TempData["OtpSent"] = T($"تم إرسال رمز التحقق (OTP) إلى {cleanEmail}. يرجى إدخال الرمز لإتمام تفعيل حساب المدير.",
                                    $"A verification code (OTP) was sent to {cleanEmail}. Please enter the code to activate your Admin account.");
            return RedirectToAction(nameof(VerifyOtp), new { email = cleanEmail });
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
        [IgnoreAntiforgeryToken]
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
                            try
                            {
                                _db.ClinicSettings.Remove(setting);
                                await _db.SaveChangesAsync();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[VerifyOtp Non-Fatal] Could not clean up OTP setting: {ex.Message}");
                            }
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
            try 
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Reload fresh entity from database and retry/bypass
                var freshUser = await _userManager.FindByEmailAsync(email) ?? await _userManager.FindByNameAsync(email);
                if (freshUser != null)
                {
                    freshUser.EmailConfirmed = true;
                    await _userManager.UpdateAsync(freshUser);
                    user = freshUser;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VerifyOtp Non-Fatal] UserManager.UpdateAsync notice: {ex.Message}");
            }

            // Re-verify Owner and Admin role assignment
            try
            {
                if (!await _userManager.IsInRoleAsync(user, "Owner"))
                {
                    if (!await _roleManager.RoleExistsAsync("Owner"))
                        await _roleManager.CreateAsync(new IdentityRole("Owner"));
                    await _userManager.AddToRoleAsync(user, "Owner");
                }
                if (!await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    if (!await _roleManager.RoleExistsAsync("Admin"))
                        await _roleManager.CreateAsync(new IdentityRole("Admin"));
                    await _userManager.AddToRoleAsync(user, "Admin");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VerifyOtp Non-Fatal] Role check/assignment notice: {ex.Message}");
            }

            // Log user in as Admin
            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(user, isPersistent: false);

            // Track session
            try
            {
                var ip = WebApplication1.Middleware.UserActivityMiddleware.GetClientIp(HttpContext);
                var ua = Request.Headers["User-Agent"].ToString();
                var sessionId = WebApplication1.Middleware.UserActivityMiddleware.GetOrCreateDeviceId(HttpContext);
                await _sessionTracking.CreateSessionAsync(user.Id, user.UserName ?? email, "Admin", ip, ua, sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VerifyOtp Non-Fatal] Session tracking notice: {ex.Message}");
            }

            TempData["Success"] = T("تم تفعيل حسابك بنجاح! مرحباً بك في نظام ClinicFlow OS.",
                                    "Your account has been activated successfully! Welcome to ClinicFlow OS.");

            return RedirectToAction("Index", "Dashboard");
        }

        // POST: ResendOtp
        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
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
                var newOtp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
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

                Console.WriteLine("=================================================");
                Console.WriteLine($"--> [LIVE OTP BACKUP] Email: {email} | CODE: {newOtp}");
                Console.WriteLine("=================================================");

                try
                {
                    await _emailSender.SendOtpEmailAsync(email, newOtp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EmailService Non-Fatal] Failed to resend OTP via SMTP: {ex.Message}");
                }

                TempData["OtpSent"] = T($"تمت إعادة إرسال رمز تحقق جديد إلى {email}.",
                                        $"A new verification code was sent to {email}.");
            }

            return RedirectToAction(nameof(VerifyOtp), new { email = email });
        }

        // GET: Register Page (لإنشاء حسابات الموظفين والأطباء)
        [Authorize(Roles = "Owner,Admin,SuperAdmin")] // فقط الأدمن والمالك يستطيع إنشاء حسابات جديدة بالنظام
        public async Task<IActionResult> Register()
        {
            var isSuperAdmin = User.IsSuperAdmin();
            var isOwner = User.IsOwner();
            var currentClinicId = User.GetClinicId();

            var docQuery = _db.Doctors.AsQueryable();
            if (!isSuperAdmin)
            {
                docQuery = docQuery.Where(d => d.ClinicId == currentClinicId);
            }

            var doctors = await docQuery
                .OrderBy(d => d.DoctorName)
                .Select(d => new { d.DoctorId, d.DoctorName, d.Specialization })
                .ToListAsync();

            ViewBag.Doctors = doctors;
            ViewBag.IsOwner = isOwner;
            return View();
        }

        // POST: Register
        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Register(
            [FromForm] RegisterClinicViewModel? model,
            string? username, string? fullName, string? email, string? password, string? role,
            int? doctorId)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                // Anonymous user registration: route to clinic onboarding
                var clinicModel = model ?? new RegisterClinicViewModel
                {
                    ClinicName = username ?? "",
                    AdminName = fullName ?? username ?? "",
                    Email = email ?? "",
                    Password = password ?? ""
                };
                return await RegisterClinic(clinicModel);
            }

            if (!User.IsAdminOrOwner())
            {
                return Forbid();
            }

            var isSuperAdmin = User.IsSuperAdmin();
            var isOwner = User.IsOwner();
            var currentClinicId = User.GetClinicId();

            // ── Reload doctors list for the view in case we return early ──────
            async Task ReloadDoctors()
            {
                var docQuery = _db.Doctors.AsQueryable();
                if (!isSuperAdmin)
                {
                    docQuery = docQuery.Where(d => d.ClinicId == currentClinicId);
                }

                ViewBag.Doctors = await docQuery
                    .OrderBy(d => d.DoctorName)
                    .Select(d => new { d.DoctorId, d.DoctorName, d.Specialization })
                    .ToListAsync();
                ViewBag.IsOwner = isOwner;
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(role))
            {
                ViewBag.Error = T("جميع الحقول الإلزامية مطلوبة.", "All required fields must be filled.");
                await ReloadDoctors();
                return View();
            }

            // ── Role Hierarchy Rule: Only Owner can create Admin or Owner accounts ──
            if ((role == "Admin" || role == "Owner") && !isOwner)
            {
                ViewBag.Error = T("لا يملك حساب المشرف صلاحية إنشاء حسابات إدارية برتبة مدير أو مالك.",
                                  "Only the Owner account can create Admin or Owner users.");
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

            var cleanUsername = username.Trim();

            // ── Auto-assign default email alias if omitted ──
            var cleanEmail = string.IsNullOrWhiteSpace(email) 
                ? $"{cleanUsername.ToLowerInvariant()}@clinic.local" 
                : email.Trim().ToLowerInvariant();

            // ── Backend Duplicate Check for Username Uniqueness ──
            var existingByName = await _userManager.FindByNameAsync(cleanUsername);
            if (existingByName != null)
            {
                ViewBag.Error = T(
                    "اسم المستخدم هذا محجوز مسبقاً، يرجى اختيار اسم مستخدم آخر.",
                    "This username is already taken. Please choose another username.");
                await ReloadDoctors();
                return View();
            }

            // ── Backend Duplicate Check for Email Uniqueness ──
            var existingByEmail = await _userManager.FindByEmailAsync(cleanEmail);
            if (existingByEmail != null)
            {
                ViewBag.Error = T(
                    "هذا البريد الإلكتروني مسجل مسبقاً لمستخدم آخر، يرجى استخدام بريد إلكتروني مختلف.",
                    "This email is already registered to another user. Please use a different email.");
                await ReloadDoctors();
                return View();
            }

            // ── 1. Create the Identity user ───────────────────────────────────
            var adminClinicId = User.GetClinicId();
            var user = new ApplicationUser 
            { 
                UserName = cleanUsername, 
                FullName = !string.IsNullOrWhiteSpace(fullName) ? fullName.Trim() : cleanUsername,
                Email = cleanEmail, 
                ClinicId = adminClinicId,
                EmailConfirmed = true
            };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // 2. Ensure the role exists
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));

                // 3. Assign role
                await _userManager.AddToRoleAsync(user, role);

                if (role == "Owner")
                {
                    if (!await _userManager.IsInRoleAsync(user, "Admin"))
                        await _userManager.AddToRoleAsync(user, "Admin");
                }

                // 4. If Doctor: store DoctorId claim so the rest of the app can correlate
                if (role == "Doctor" && doctorId.HasValue)
                {
                    await _userManager.AddClaimAsync(user,
                        new System.Security.Claims.Claim("DoctorId", doctorId.Value.ToString()));
                }

                // 5. Inherit current admin's ClinicId for multi-tenancy isolation
                await _userManager.AddClaimAsync(user,
                    new System.Security.Claims.Claim("ClinicId", adminClinicId.ToString()));

                // ── Bilingual success toast ───────────────────────────────────
                ViewBag.Success = T(
                    $"تمت إضافة المستخدم '{cleanUsername}' بصلاحية {role} بنجاح ✓",
                    $"User '{cleanUsername}' added successfully with the '{role}' role ✓");

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

        // GET: Logout (Permits direct navigation or links)
        [HttpGet]
        [ActionName("Logout")]
        public async Task<IActionResult> LogoutGet()
        {
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

        // ── GET: Index (Alias for StaffList) ────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(StaffList));
        }

        // ── GET: Staff & User Management List ───────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> StaffList()
        {
            var isSuperAdmin = User.IsSuperAdmin();
            var isOwner = User.IsOwner();
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentClinicId = User.GetClinicId();

            var allUsers = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();
            var staffList = new List<StaffUserViewModel>();

            foreach (var u in allUsers)
            {
                var claims = await _userManager.GetClaimsAsync(u);
                var userClinicClaim = claims.FirstOrDefault(c => c.Type == "ClinicId")?.Value;

                // SuperAdmin/Owner sees staff belonging to clinic
                if (isSuperAdmin || (Guid.TryParse(userClinicClaim, out var uClinicGuid) && uClinicGuid == currentClinicId))
                {
                    var roles = await _userManager.GetRolesAsync(u);
                    string roleLabel;
                    if (roles.Contains("Owner") || roles.Contains("SuperAdmin")) roleLabel = "Owner";
                    else if (roles.Contains("Admin")) roleLabel = "Admin";
                    else if (roles.Contains("Doctor")) roleLabel = "Doctor";
                    else if (roles.Contains("Receptionist")) roleLabel = "Receptionist";
                    else roleLabel = roles.FirstOrDefault() ?? "Unknown";

                    var isTargetOwner = roleLabel == "Owner" || roles.Contains("Owner") || roles.Contains("SuperAdmin");
                    var isCurrent = u.Id == currentUserId;

                    // Security & Hierarchy Rules:
                    // 1. Owner account can NEVER be deleted (permanent deletion immunity)
                    // 2. No user can delete themselves
                    // 3. Regular Admin cannot delete or edit Owner or other Admins
                    // 4. Regular Admin CAN edit/delete Doctors and Receptionists
                    // 5. Owner CAN edit anyone (including themselves and other staff)
                    // 6. Owner CAN delete anyone except Owner accounts and themselves
                    bool canDelete = false;
                    bool canEdit = false;

                    if (isOwner)
                    {
                        canEdit = true; // Owner can edit all accounts (themselves, admins, doctors, receptionists)
                        canDelete = !isTargetOwner && !isCurrent; // Owner can delete non-owner accounts
                    }
                    else if (User.IsInRole("Admin"))
                    {
                        if (!isTargetOwner && roleLabel != "Admin" && !isCurrent)
                        {
                            canEdit = true; // Admin can edit doctors and receptionists
                            canDelete = true; // Admin can delete doctors and receptionists
                        }
                    }

                    staffList.Add(new StaffUserViewModel
                    {
                        User = u,
                        Role = roleLabel,
                        CreatedDate = "",
                        FullName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : (u.UserName ?? ""),
                        CanDelete = canDelete,
                        CanEdit = canEdit,
                        IsOwner = isTargetOwner
                    });
                }
            }

            ViewBag.StaffList = staffList;
            ViewBag.IsOwner = isOwner;
            return View();
        }

        // ── AJAX: Return doctor details by id (for auto-fill) ─────────────────
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> GetDoctorDetails(int id)
        {
            var currentClinicId = User.GetClinicId();
            var isSuperAdmin = User.IsSuperAdmin();

            var query = _db.Doctors.AsQueryable();
            if (!isSuperAdmin)
            {
                query = query.Where(d => d.ClinicId == currentClinicId);
            }

            var doctor = await query
                .Where(d => d.DoctorId == id)
                .Select(d => new { d.DoctorId, d.DoctorName, d.Specialization, d.DoctorNumber })
                .FirstOrDefaultAsync();

            if (doctor == null)
                return NotFound();

            return Json(doctor);
        }

        // ── POST: Delete a staff user ──────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
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

            var targetRoles = await _userManager.GetRolesAsync(user);

            // ── PROTECTION GUARD 1: Owner / SuperAdmin cannot be deleted by anyone ──
            if (targetRoles.Contains("Owner") || targetRoles.Contains("SuperAdmin"))
            {
                TempData["Error"] = T("حساب المالك الرئيسي (Owner / SuperAdmin) محمي بحصانة النظام ولا يمكن حذفه نهائياً.",
                                      "The Owner / SuperAdmin account is protected by system immunity and cannot be deleted.");
                return RedirectToAction(nameof(StaffList));
            }

            // ── PROTECTION GUARD 2: Admin cannot delete other Admins or Owners ──
            if (!User.IsOwner())
            {
                if (targetRoles.Contains("Admin"))
                {
                    TempData["Error"] = T("لا تملك الصلاحية الكافية لحذف حساب مدير نظام آخر.",
                                          "You do not have permission to delete another Administrator account.");
                    return RedirectToAction(nameof(StaffList));
                }

                // Enforce tenant boundary
                var userClaims = await _userManager.GetClaimsAsync(user);
                var userClinicClaim = userClaims.FirstOrDefault(c => c.Type == "ClinicId")?.Value;
                var currentClinicId = User.GetClinicId();

                if (!Guid.TryParse(userClinicClaim, out var uClinicGuid) || uClinicGuid != currentClinicId)
                {
                    TempData["Error"] = T("غير مصرح لك بحذف مستخدم تابع لعيادة أخرى.", "You are not authorized to delete a user from another clinic.");
                    return RedirectToAction(nameof(StaffList));
                }
            }

            // Remove foreign-key linked sessions before deletion to prevent constraint violations
            try
            {
                var sessions = await _db.UserSessionLogs.Where(s => s.UserId == id).ToListAsync();
                if (sessions.Count > 0)
                {
                    _db.UserSessionLogs.RemoveRange(sessions);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteUser] Session cleanup exception: {ex.Message}");
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = T(
                    $"تم حذف حساب '{user.UserName}' نهائياً بنجاح.",
                    $"Account '{user.UserName}' was permanently deleted successfully.");
            }
            else
            {
                TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(StaffList));
        }

        // ── GET: Edit Staff Profile View ──────────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public async Task<IActionResult> EditStaff(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = T("معرّف المستخدم غير صالح.", "Invalid user ID.");
                return RedirectToAction(nameof(StaffList));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = T("المستخدم غير موجود.", "User not found.");
                return RedirectToAction(nameof(StaffList));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isTargetOwner = roles.Contains("Owner") || roles.Contains("SuperAdmin");
            var isCallerOwner = User.IsOwner();

            // Permission Check:
            if (!isCallerOwner)
            {
                if (isTargetOwner)
                {
                    TempData["Error"] = T("لا تملك الصلاحية لتعديل حساب المالك (Owner).", "You do not have permission to edit the Owner account.");
                    return RedirectToAction(nameof(StaffList));
                }
                if (roles.Contains("Admin"))
                {
                    TempData["Error"] = T("لا تملك الصلاحية لتعديل حسابات المديرين (Admin).", "You do not have permission to edit Admin accounts.");
                    return RedirectToAction(nameof(StaffList));
                }
            }

            string primaryRole = isTargetOwner ? "Owner" : roles.FirstOrDefault() ?? "Receptionist";

            var allowedRoles = new List<string>();
            if (isTargetOwner)
            {
                allowedRoles.Add("Owner");
            }
            else
            {
                if (isCallerOwner)
                {
                    allowedRoles.Add("Admin");
                    allowedRoles.Add("Doctor");
                    allowedRoles.Add("Receptionist");
                }
                else
                {
                    allowedRoles.Add("Doctor");
                    allowedRoles.Add("Receptionist");
                }
            }

            var model = new EditStaffViewModel
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.UserName ?? string.Empty),
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = primaryRole,
                IsOwner = isTargetOwner,
                CanChangeRole = !isTargetOwner,
                AllowedRoles = allowedRoles
            };

            return View("~/Views/Staff/Edit.cshtml", model);
        }

        // Alias for EditStaff
        [HttpGet]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        public Task<IActionResult> Edit(string id) => EditStaff(id);

        // ── POST: Update staff complete profile & credentials ──────────────────
        [HttpPost]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStaff(EditStaffViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
            {
                TempData["Error"] = T("معرّف المستخدم غير صالح.", "Invalid user ID.");
                return RedirectToAction(nameof(StaffList));
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                TempData["Error"] = T("المستخدم غير موجود.", "User not found.");
                return RedirectToAction(nameof(StaffList));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isTargetOwner = roles.Contains("Owner") || roles.Contains("SuperAdmin");
            var isCallerOwner = User.IsOwner();
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Permission Check:
            if (!isCallerOwner)
            {
                if (isTargetOwner)
                {
                    TempData["Error"] = T("لا تملك الصلاحية لتعديل حساب المالك.", "You do not have permission to modify the Owner account.");
                    return RedirectToAction(nameof(StaffList));
                }
                if (roles.Contains("Admin"))
                {
                    TempData["Error"] = T("لا تملك الصلاحية لتعديل حسابات المديرين.", "You do not have permission to modify Admin accounts.");
                    return RedirectToAction(nameof(StaffList));
                }
            }

            // 1. Validate & Update Username
            var cleanUsername = model.Username?.Trim();
            if (string.IsNullOrWhiteSpace(cleanUsername))
            {
                TempData["Error"] = T("اسم المستخدم مطلوب ولا يمكن أن يكون فارغاً.", "Username is required.");
                return RedirectToAction(nameof(EditStaff), new { id = model.Id });
            }

            if (!string.Equals(user.UserName, cleanUsername, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _userManager.FindByNameAsync(cleanUsername);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    TempData["Error"] = T($"اسم المستخدم '{cleanUsername}' مستخدم بالفعل.", $"Username '{cleanUsername}' is already in use.");
                    return RedirectToAction(nameof(EditStaff), new { id = model.Id });
                }
                user.UserName = cleanUsername;
            }

            // 2. Validate & Update Full Name
            user.FullName = string.IsNullOrWhiteSpace(model.FullName) ? cleanUsername : model.FullName.Trim();

            // 3. Validate & Update Email
            var cleanEmail = string.IsNullOrWhiteSpace(model.Email) ? $"{cleanUsername}@clinic.local" : model.Email.Trim();
            if (!string.Equals(user.Email, cleanEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existingEmailUser = await _userManager.FindByEmailAsync(cleanEmail);
                if (existingEmailUser != null && existingEmailUser.Id != user.Id)
                {
                    TempData["Error"] = T($"البريد الإلكتروني '{cleanEmail}' مستخدم بالفعل.", $"Email '{cleanEmail}' is already registered.");
                    return RedirectToAction(nameof(EditStaff), new { id = model.Id });
                }
                user.Email = cleanEmail;
            }

            // 4. Update Phone Number
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(EditStaff), new { id = model.Id });
            }

            // 5. Update Role (if not Owner)
            if (!isTargetOwner && !string.IsNullOrWhiteSpace(model.Role))
            {
                var targetRole = model.Role.Trim();
                if (!isCallerOwner && (targetRole == "Admin" || targetRole == "Owner"))
                {
                    TempData["Error"] = T("لا تملك الصلاحية لتعيين صلاحية مدير أو مالك.", "You do not have permission to assign Admin or Owner roles.");
                    return RedirectToAction(nameof(StaffList));
                }

                if (!roles.Contains(targetRole))
                {
                    var removable = roles.Where(r => r != "Owner" && r != "SuperAdmin").ToList();
                    if (removable.Any())
                    {
                        await _userManager.RemoveFromRolesAsync(user, removable);
                    }

                    if (!await _roleManager.RoleExistsAsync(targetRole))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(targetRole));
                    }

                    await _userManager.AddToRoleAsync(user, targetRole);
                }
            }

            // 6. Password Reset (if new password specified)
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var newPwd = model.NewPassword.Trim();
                if (newPwd.Length < 6)
                {
                    TempData["Error"] = T("كلمة المرور الجديدة يجب ألا تقل عن 6 خانات.", "New password must be at least 6 characters.");
                    return RedirectToAction(nameof(EditStaff), new { id = model.Id });
                }

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPwd);
                if (!resetResult.Succeeded)
                {
                    await _userManager.RemovePasswordAsync(user);
                    var addResult = await _userManager.AddPasswordAsync(user, newPwd);
                    if (!addResult.Succeeded)
                    {
                        TempData["Error"] = string.Join(" | ", addResult.Errors.Select(e => e.Description));
                        return RedirectToAction(nameof(EditStaff), new { id = model.Id });
                    }
                }
            }

            // If the user modified their own credentials/username, refresh cookie
            if (user.Id == currentUserId)
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            TempData["Success"] = T($"تم تحديث بيانات المستخدم '{user.UserName}' بنجاح.",
                                    $"User '{user.UserName}' updated successfully.");
            return RedirectToAction(nameof(StaffList));
        }

        // Alias for UpdateStaff
        [HttpPost]
        [Authorize(Roles = "Owner,Admin,SuperAdmin")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Edit(EditStaffViewModel model) => UpdateStaff(model);
    }
}