namespace UnlockMatePro.Models
{
    public class AdbDevice
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string Model { get; set; } = "Unknown Device";
        public string Product { get; set; } = string.Empty;
        public string DeviceState { get; set; } = "device"; // "device", "unauthorized", "offline", "recovery", "bootloader"
        public string AndroidVersion { get; set; } = "Unknown";
        public string ApiLevel { get; set; } = "Unknown";
        public int BatteryLevel { get; set; } = -1;
        public bool IsCharging { get; set; } = false;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsRooted { get; set; } = false;
        public bool IsFavorite { get; set; } = false;

        public SystemStats Stats { get; set; } = new SystemStats();

        public bool IsConnected => DeviceState.Equals("device", System.StringComparison.OrdinalIgnoreCase);

        public string DisplayName => string.IsNullOrWhiteSpace(Model) || Model == "Unknown Device"
            ? SerialNumber
            : $"{Model} ({SerialNumber})";

        public override string ToString() => DisplayName;
    }
}

