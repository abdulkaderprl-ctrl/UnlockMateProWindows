using System.Collections.Generic;

namespace AdbEasyInstaller.Models
{
    public class AppSettings
    {
        public string CustomAdbPath { get; set; } = string.Empty;
        public string CustomScrcpyPath { get; set; } = string.Empty;
        public bool AutoDetectAdb { get; set; } = true;
        public bool IsPortableMode { get; set; } = false;
        public string Theme { get; set; } = "Dark"; // "Dark", "Light"
        public string Language { get; set; } = "English"; // "English", "Bangla"
        public bool AutoCheckUpdates { get; set; } = true;
        public bool ReinstallByDefault { get; set; } = true;
        public bool GrantPermissionsByDefault { get; set; } = true;
        public bool AllowDowngrade { get; set; } = false;
        public int AutoRefreshIntervalSeconds { get; set; } = 5;

        // API & Backend Auth Architecture Settings
        public string ApiBaseUrl { get; set; } = "https://api.unlockmatepro.com/api";
        public string ApiEnvironment { get; set; } = "Production"; // "Development", "Production"
        public bool RememberMe { get; set; } = true;
        public bool AutoLoginOnStartup { get; set; } = true;

        // Scrcpy Mirroring Settings
        public bool ScrcpyControlEnabled { get; set; } = true;
        public bool ScrcpyStayAwake { get; set; } = true;
        public bool ScrcpyTurnScreenOff { get; set; } = false;
        public bool ScrcpyShowTouches { get; set; } = false;
        public bool ScrcpyFullscreen { get; set; } = false;
        public int ScrcpyMaxFps { get; set; } = 60;
        public string ScrcpyBitrateMbps { get; set; } = "8M";
        public string ScrcpyMaxResolution { get; set; } = "0";

        // Device Preferences
        public List<string> FavoriteDeviceSerials { get; set; } = new List<string>();
        public List<string> RecentDeviceSerials { get; set; } = new List<string>();
    }
}
