using System;
using UAParser;

namespace WebApplication1.Services
{
    public class DeviceInfo
    {
        public string DeviceType { get; set; } = "Desktop"; // "Mobile", "Tablet", "Desktop"
        public string OsName { get; set; } = "Unknown OS";
        public string OsVersion { get; set; } = string.Empty;
        public string BrowserName { get; set; } = "Browser";
        public string DeviceModel { get; set; } = string.Empty;
        public string FormattedSummary { get; set; } = "Desktop";
        public string BootstrapIconClass { get; set; } = "bi bi-display";
        public string FontAwesomeIconClass { get; set; } = "fa-solid fa-desktop";
    }

    public static class DeviceDetectionHelper
    {
        private static readonly Parser _parser = Parser.GetDefault();

        public static DeviceInfo Parse(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return new DeviceInfo
                {
                    DeviceType = "Desktop",
                    OsName = "Unknown",
                    BrowserName = "Client",
                    FormattedSummary = "Desktop",
                    BootstrapIconClass = "bi bi-display",
                    FontAwesomeIconClass = "fa-solid fa-desktop"
                };
            }

            var ua = userAgent.Trim();
            var clientInfo = _parser.Parse(ua);

            // 1. Determine Device Type
            string deviceType = "Desktop";
            string bootstrapIcon = "bi bi-display";
            string fontAwesomeIcon = "fa-solid fa-desktop";

            bool isTablet = ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
                            (ua.Contains("Android", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) ||
                            ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase);

            bool isMobile = !isTablet && (
                            ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                            ua.Contains("iPod", StringComparison.OrdinalIgnoreCase) ||
                            ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                            ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                            ua.Contains("webOS", StringComparison.OrdinalIgnoreCase) ||
                            ua.Contains("BlackBerry", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(clientInfo.OS.Family, "iOS", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(clientInfo.OS.Family, "Android", StringComparison.OrdinalIgnoreCase));

            if (isTablet)
            {
                deviceType = "Tablet";
                bootstrapIcon = "bi bi-tablet";
                fontAwesomeIcon = "fa-solid fa-tablet-screen-button";
            }
            else if (isMobile)
            {
                deviceType = "Mobile";
                bootstrapIcon = "bi bi-phone";
                fontAwesomeIcon = "fa-solid fa-mobile-screen";
            }
            else
            {
                deviceType = "Desktop";
                bootstrapIcon = "bi bi-laptop";
                fontAwesomeIcon = "fa-solid fa-desktop";
            }

            // 2. Format OS Name & Version
            string osFamily = clientInfo.OS.Family ?? string.Empty;
            string osMajor = clientInfo.OS.Major ?? string.Empty;
            string osMinor = clientInfo.OS.Minor ?? string.Empty;

            string friendlyOs = osFamily;
            if (osFamily.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                if (osMajor == "10" || ua.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase))
                    friendlyOs = "Windows 10/11";
                else if (osMajor == "6" && osMinor == "3")
                    friendlyOs = "Windows 8.1";
                else if (osMajor == "6" && osMinor == "1")
                    friendlyOs = "Windows 7";
                else if (!string.IsNullOrEmpty(osMajor))
                    friendlyOs = $"Windows {osMajor}";
                else
                    friendlyOs = "Windows";
            }
            else if (osFamily.Contains("Mac OS", StringComparison.OrdinalIgnoreCase))
            {
                friendlyOs = "macOS";
            }
            else if (osFamily.Contains("iOS", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            {
                friendlyOs = !string.IsNullOrEmpty(osMajor) ? $"iOS {osMajor}" : "iOS";
            }
            else if (osFamily.Contains("Android", StringComparison.OrdinalIgnoreCase) || ua.Contains("Android", StringComparison.OrdinalIgnoreCase))
            {
                friendlyOs = !string.IsNullOrEmpty(osMajor) ? $"Android {osMajor}" : "Android";
            }
            else if (osFamily.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            {
                friendlyOs = "Linux";
            }
            else if (string.IsNullOrWhiteSpace(friendlyOs) || friendlyOs.Equals("Other", StringComparison.OrdinalIgnoreCase))
            {
                friendlyOs = deviceType;
            }

            // 3. Format Browser Name
            string browserFamily = clientInfo.UA.Family ?? string.Empty;
            string friendlyBrowser = browserFamily;

            if (string.IsNullOrWhiteSpace(friendlyBrowser) || friendlyBrowser.Equals("Other", StringComparison.OrdinalIgnoreCase))
            {
                if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
                    friendlyBrowser = "Edge";
                else if (ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chromium", StringComparison.OrdinalIgnoreCase))
                    friendlyBrowser = isMobile ? "Chrome Mobile" : "Chrome";
                else if (ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
                    friendlyBrowser = isMobile ? "Mobile Safari" : "Safari";
                else if (ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                    friendlyBrowser = "Firefox";
                else
                    friendlyBrowser = "Browser";
            }

            // 4. Device Model (if meaningful and not generic)
            string rawDeviceModel = clientInfo.Device.Model ?? clientInfo.Device.Family ?? string.Empty;
            string deviceModel = string.Empty;
            if (!string.IsNullOrWhiteSpace(rawDeviceModel) &&
                !rawDeviceModel.Equals("Other", StringComparison.OrdinalIgnoreCase) &&
                !rawDeviceModel.Equals("Generic", StringComparison.OrdinalIgnoreCase) &&
                !rawDeviceModel.Equals("Generic Smartphone", StringComparison.OrdinalIgnoreCase))
            {
                deviceModel = rawDeviceModel;
            }

            // 5. Construct Clean Formatted Summary
            // e.g. "Android 10 (Chrome Mobile)" or "Windows 11 (Chrome)" or "iPhone (Safari)"
            string formattedSummary;
            if (!string.IsNullOrEmpty(deviceModel) && (deviceModel.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || deviceModel.Contains("iPad", StringComparison.OrdinalIgnoreCase)))
            {
                formattedSummary = $"{deviceModel} ({friendlyBrowser})";
            }
            else if (!string.IsNullOrEmpty(friendlyOs) && !string.IsNullOrEmpty(friendlyBrowser))
            {
                formattedSummary = $"{friendlyOs} ({friendlyBrowser})";
            }
            else if (!string.IsNullOrEmpty(friendlyOs))
            {
                formattedSummary = friendlyOs;
            }
            else
            {
                formattedSummary = $"{deviceType} ({friendlyBrowser})";
            }

            return new DeviceInfo
            {
                DeviceType = deviceType,
                OsName = friendlyOs,
                OsVersion = osMajor,
                BrowserName = friendlyBrowser,
                DeviceModel = deviceModel,
                FormattedSummary = formattedSummary,
                BootstrapIconClass = bootstrapIcon,
                FontAwesomeIconClass = fontAwesomeIcon
            };
        }
    }
}
