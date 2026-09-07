using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    /// <summary>
    /// Handles UI language switching by writing the ASP.NET Core culture cookie.
    /// Route: /Language/SetLanguage?culture=ar&amp;returnUrl=/Dashboard
    /// </summary>
    public class LanguageController : Controller
    {
        private static readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            "ar", "en"
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            // Only accept white-listed cultures to prevent open-redirect via cookie value
            if (!_allowed.Contains(culture))
                culture = "ar";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires   = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite  = SameSiteMode.Lax
                });

            // Validate returnUrl to avoid open-redirect
            if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            return LocalRedirect(returnUrl);
        }
    }
}
