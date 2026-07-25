namespace AdbEasyInstaller.Models
{
    public class SystemStats
    {
        public double CpuUsagePercent { get; set; } = 0;
        public double RamUsedMb { get; set; } = 0;
        public double RamTotalMb { get; set; } = 0;
        public double RamUsagePercent => RamTotalMb > 0 ? (RamUsedMb / RamTotalMb) * 100.0 : 0;

        public double StorageUsedGb { get; set; } = 0;
        public double StorageTotalGb { get; set; } = 0;
        public double StorageUsagePercent => StorageTotalGb > 0 ? (StorageUsedGb / StorageTotalGb) * 100.0 : 0;

        public double BatteryTempCelsius { get; set; } = 25.0;
        public string BatteryHealth { get; set; } = "Good";
        public bool IsRooted { get; set; } = false;

        public string NetworkType { get; set; } = "Wi-Fi / Cellular";
        public string IpAddress { get; set; } = "127.0.0.1";
        public string SecurityPatch { get; set; } = "2026-01-05";
        public string BuildFingerprint { get; set; } = "google/redfin/redfin:14/...";
        public int SensorCount { get; set; } = 18;
    }
}
