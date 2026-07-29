using System;
using System.IO;

namespace UnlockMatePro.Models
{
    public class ApkInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string AppName { get; set; } = "Unknown App";
        public string PackageName { get; set; } = "Unknown Package";
        public string VersionName { get; set; } = "1.0";
        public string VersionCode { get; set; } = "1";
        public int MinSdkVersion { get; set; } = 21;
        public int TargetSdkVersion { get; set; } = 34;
        public string SupportedAbis { get; set; } = "arm64-v8a, armeabi-v7a, x86, x86_64";
        public long FileSizeBytes { get; set; } = 0;
        public string FormattedSize => $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
        public string Status { get; set; } = "Ready"; // "Ready", "PreCheck", "Extracting", "Installing", "Verified", "Success", "Error"
        public double Progress { get; set; } = 0;
        public string ErrorMessage { get; set; } = string.Empty;
        public string DetailedLog { get; set; } = string.Empty;
        public bool IsVerifiedInstalled { get; set; } = false;

        // Split APK Bundle attributes (.apks, .xapk, .apkm)
        public bool IsSplitBundle => FilePath.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase) ||
                                     FilePath.EndsWith(".apks", StringComparison.OrdinalIgnoreCase) ||
                                     FilePath.EndsWith(".apkm", StringComparison.OrdinalIgnoreCase);

        public string ExtensionBadge => Path.GetExtension(FilePath).ToUpperInvariant().TrimStart('.');
    }
}

