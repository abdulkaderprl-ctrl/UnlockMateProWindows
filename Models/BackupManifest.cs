using System;

namespace AdbEasyInstaller.Models
{
    public class BackupManifest
    {
        public string BackupId { get; set; } = Guid.NewGuid().ToString("N");
        public string DeviceName { get; set; } = "Android Device";
        public string DeviceSerial { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = "Unknown";
        public string ApiLevel { get; set; } = "0";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IncludesContacts { get; set; } = true;
        public bool IncludesSms { get; set; } = true;
        public bool IncludesCallLogs { get; set; } = true;
        public bool IncludesFiles { get; set; } = true;
        public bool IncludesApps { get; set; } = true;

        public int ContactCount { get; set; } = 0;
        public int SmsCount { get; set; } = 0;
        public int CallLogCount { get; set; } = 0;
        public int AppCount { get; set; } = 0;
        public long TotalSizeBytes { get; set; } = 0;
        public string AppVersion { get; set; } = "Unlock Mate Pro v2.0";
    }
}
