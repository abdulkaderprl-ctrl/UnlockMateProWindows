namespace UnlockMatePro.Models
{
    public class FastbootDevice
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string DeviceState { get; set; } = "fastboot";
        public string Product { get; set; } = "Unknown";
        public string DisplayName => $"{SerialNumber} [{DeviceState.ToUpper()}]";

        public override string ToString() => DisplayName;
    }
}

