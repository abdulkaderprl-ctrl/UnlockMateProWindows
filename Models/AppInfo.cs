using System.Collections.Generic;

namespace AdbEasyInstaller.Models
{
    public class AppInfo
    {
        public string PackageName { get; set; } = string.Empty;
        public string ApkPath { get; set; } = string.Empty;
        public bool IsSystemApp { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
        public string StateText => IsEnabled ? "Enabled" : "Disabled";
        public string DisplayName => PackageName;
        public string AppCategory => IsSystemApp ? "System" : "User";

        public List<string> GrantedPermissions { get; set; } = new List<string>();
        public List<string> RequestedPermissions { get; set; } = new List<string>();
    }
}
