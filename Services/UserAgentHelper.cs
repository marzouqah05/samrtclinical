using System;

namespace WebApplication1.Services
{
    public static class UserAgentHelper
    {
        public static (string DeviceType, string DisplayName, string IconClass) Parse(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return ("Desktop", "Desktop", "fa-solid fa-desktop");

            var ua = userAgent;

            // Tablets
            if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                return ("Tablet", "Tablet (iPad)", "fa-solid fa-tablet-screen-button");

            // Mobile devices
            if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
                return ("Mobile", "Mobile (iPhone)", "fa-solid fa-mobile-screen");

            if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase))
            {
                if (ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
                    return ("Mobile", "Mobile (Android)", "fa-solid fa-mobile-screen");
                return ("Tablet", "Tablet (Android)", "fa-solid fa-tablet-screen-button");
            }

            if (ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) || ua.Contains("webOS", StringComparison.OrdinalIgnoreCase))
                return ("Mobile", "Mobile Device", "fa-solid fa-mobile-screen");

            // Desktop OS
            if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                return ("Desktop", "Desktop (Windows)", "fa-solid fa-desktop");

            if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase))
                return ("Desktop", "Desktop (macOS)", "fa-solid fa-desktop");

            if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                return ("Desktop", "Desktop (Linux)", "fa-solid fa-desktop");

            return ("Desktop", "Desktop", "fa-solid fa-desktop");
        }
    }
}
