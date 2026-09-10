using System;

namespace WebApplication1.Services
{
    public static class UserAgentHelper
    {
        public static (string DeviceType, string DisplayName, string IconClass) Parse(string? userAgent)
        {
            var info = DeviceDetectionHelper.Parse(userAgent);
            return (info.DeviceType, info.FormattedSummary, info.BootstrapIconClass);
        }
    }
}
