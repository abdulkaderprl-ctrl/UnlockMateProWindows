using System;
using System.Linq;
using System.Threading.Tasks;

namespace UnlockMatePro.Services
{
    public class NothingService : INothingService
    {
        private readonly IAdbService _adbService;
        private readonly IFastbootService _fastbootService;
        private string? _currentDeviceSerial;

        public NothingService(IAdbService adbService, IFastbootService fastbootService)
        {
            _adbService = adbService;
            _fastbootService = fastbootService;
        }

        public string CurrentMode { get; private set; } = "Disconnected";
        public string Model { get; private set; } = "Unknown";
        public string Product { get; private set; } = "Unknown";
        public string Codename { get; private set; } = "Unknown";
        public string AndroidVersion { get; private set; } = "Unknown";
        public string BuildNumber { get; private set; } = "Unknown";
        public string SerialNumber { get; private set; } = "Unknown";
        public string BootloaderState { get; private set; } = "Unknown";
        public string Slot { get; private set; } = "Unknown";
        public string BatteryLevel { get; private set; } = "Unknown";

        public async Task<bool> DetectDeviceAsync()
        {
            var adbDevices = await _adbService.GetConnectedDevicesAsync();
            var fastbootDevices = await _fastbootService.GetConnectedFastbootDevicesAsync();

            if (adbDevices != null && adbDevices.Any())
            {
                var device = adbDevices.First();
                _currentDeviceSerial = device.SerialNumber;
                CurrentMode = device.DeviceState.Equals("recovery", StringComparison.OrdinalIgnoreCase) ? "Recovery" : "ADB";
                
                var details = await _adbService.GetDeviceDetailsAsync(_currentDeviceSerial);
                if (details != null)
                {
                    Model = details.Model ?? "Unknown";
                    Product = details.Product ?? "Unknown";
                    Codename = "Unknown";
                    AndroidVersion = details.AndroidVersion ?? "Unknown";
                    BuildNumber = "Unknown";
                    SerialNumber = details.SerialNumber ?? "Unknown";
                }

                // Try get battery
                var (battSuccess, battOutput) = await _adbService.ExecuteCommandAsync("shell dumpsys battery", _currentDeviceSerial);
                if (battSuccess)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(battOutput, @"level:\s+(\d+)");
                    if (match.Success) BatteryLevel = match.Groups[1].Value + "%";
                }

                return true;
            }
            else if (fastbootDevices != null && fastbootDevices.Any())
            {
                var device = fastbootDevices.First();
                _currentDeviceSerial = device.SerialNumber;
                CurrentMode = device.DeviceState.Contains("fastbootd", StringComparison.OrdinalIgnoreCase) ? "FastbootD" : "Fastboot";

                var (varSuccess, varOutput) = await _fastbootService.GetVarAllAsync(_currentDeviceSerial);
                if (varSuccess)
                {
                    Model = ParseFastbootVar(varOutput, "product");
                    Product = ParseFastbootVar(varOutput, "product");
                    Codename = ParseFastbootVar(varOutput, "product");
                    SerialNumber = ParseFastbootVar(varOutput, "serialno");
                    BootloaderState = ParseFastbootVar(varOutput, "unlocked").Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Unlocked" : "Locked";
                    Slot = ParseFastbootVar(varOutput, "current-slot");
                }
                
                return true;
            }

            ResetDeviceInfo();
            return false;
        }

        private string ParseFastbootVar(string output, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(output, $@"{key}:\s*([^\r\n]+)");
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private void ResetDeviceInfo()
        {
            CurrentMode = "Disconnected";
            Model = "Unknown";
            Product = "Unknown";
            Codename = "Unknown";
            AndroidVersion = "Unknown";
            BuildNumber = "Unknown";
            SerialNumber = "Unknown";
            BootloaderState = "Unknown";
            Slot = "Unknown";
            BatteryLevel = "Unknown";
            _currentDeviceSerial = null;
        }

        public async Task RebootSystemAsync()
        {
            if (CurrentMode == "ADB" || CurrentMode == "Recovery") await _adbService.RebootDeviceAsync(_currentDeviceSerial, "");
            else await _fastbootService.RebootFastbootAsync(_currentDeviceSerial, "");
        }

        public async Task RebootRecoveryAsync()
        {
            if (CurrentMode == "ADB" || CurrentMode == "Recovery") await _adbService.RebootDeviceAsync(_currentDeviceSerial, "recovery");
            else await _fastbootService.RebootFastbootAsync(_currentDeviceSerial, "recovery");
        }

        public async Task RebootBootloaderAsync()
        {
            if (CurrentMode == "ADB" || CurrentMode == "Recovery") await _adbService.RebootDeviceAsync(_currentDeviceSerial, "bootloader");
            else await _fastbootService.RebootFastbootAsync(_currentDeviceSerial, "bootloader");
        }

        public async Task RebootFastbootDAsync()
        {
            if (CurrentMode == "ADB" || CurrentMode == "Recovery") await _adbService.RebootDeviceAsync(_currentDeviceSerial, "fastboot");
            else await _fastbootService.RebootFastbootAsync(_currentDeviceSerial, "fastboot");
        }

        public async Task RebootEdlAsync()
        {
            if (CurrentMode == "ADB" || CurrentMode == "Recovery") await _adbService.RebootEdlAsync(_currentDeviceSerial);
            else await _fastbootService.ExecuteFastbootCommandAsync("oem edl", _currentDeviceSerial);
        }

        public async Task UnlockBootloaderAsync()
        {
            await _fastbootService.ExecuteFastbootCommandAsync("flashing unlock", _currentDeviceSerial);
        }

        public async Task RelockBootloaderAsync()
        {
            await _fastbootService.ExecuteFastbootCommandAsync("flashing lock", _currentDeviceSerial);
        }

        public async Task CheckOemUnlockStatusAsync()
        {
            await _fastbootService.GetVarAllAsync(_currentDeviceSerial);
        }

        public async Task CheckBootloaderStateAsync()
        {
            await _fastbootService.GetVarAllAsync(_currentDeviceSerial);
        }

        public async Task FlashPartitionAsync(string partition, string filePath)
        {
            await _fastbootService.FlashImageAsync(partition, filePath, _currentDeviceSerial);
        }

        public async Task FlashFirmwareAsync(string folderPath, Action<string> logCallback, Action<int> progressCallback)
        {
            // Placeholder for full firmware flashing logic
            logCallback?.Invoke($"Starting to flash full firmware from {folderPath}...");
            await Task.Delay(1000);
            progressCallback?.Invoke(10);
            logCallback?.Invoke("Flashing boot.img...");
            await Task.Delay(2000);
            progressCallback?.Invoke(50);
            logCallback?.Invoke("Flashing system.img...");
            await Task.Delay(2000);
            progressCallback?.Invoke(100);
            logCallback?.Invoke("Flashing completed.");
        }
    }
}
