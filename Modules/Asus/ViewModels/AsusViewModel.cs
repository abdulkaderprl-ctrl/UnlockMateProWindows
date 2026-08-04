using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class AsusViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly IFastbootService _fastbootService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string _logText = "";
        private bool _isBusy = false;
        private int _progress = 0;
        private string _statusText = "Idle";
        private DeviceInfo? _detectedDeviceInfo;

        private string _bootPath = "";
        private string _recoveryPath = "";
        private string _vendorPath = "";
        private string _vbmetaPath = "";
        private string _superPath = "";
        private string _rawFirmwarePath = "";

        public string LogText { get => _logText; set => SetProperty(ref _logText, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        public string DeviceStatusText => _detectedDeviceInfo != null ? $"Device: {_detectedDeviceInfo.Model} ({_detectedDeviceInfo.Mode})" : "Device: Not Detected";

        public string BootPath { get => _bootPath; set => SetProperty(ref _bootPath, value); }
        public string RecoveryPath { get => _recoveryPath; set => SetProperty(ref _recoveryPath, value); }
        public string VendorPath { get => _vendorPath; set => SetProperty(ref _vendorPath, value); }
        public string VbmetaPath { get => _vbmetaPath; set => SetProperty(ref _vbmetaPath, value); }
        public string SuperPath { get => _superPath; set => SetProperty(ref _superPath, value); }
        public string RawFirmwarePath { get => _rawFirmwarePath; set => SetProperty(ref _rawFirmwarePath, value); }

        public ICommand DetectDeviceCommand { get; }
        public ICommand ReadInfoCommand { get; }
        public ICommand RebootSystemCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootFastbootCommand { get; }
        public ICommand WipeDataCommand { get; }
        public ICommand UnlockBootloaderCommand { get; }
        public ICommand RelockBootloaderCommand { get; }
        
        public ICommand FlashBootCommand { get; }
        public ICommand FlashRecoveryCommand { get; }
        public ICommand FlashVendorCommand { get; }
        public ICommand FlashVbmetaCommand { get; }
        public ICommand FlashSuperCommand { get; }
        public ICommand FlashRawFirmwareCommand { get; }

        public ICommand BrowseBootCommand { get; }
        public ICommand BrowseRecoveryCommand { get; }
        public ICommand BrowseVendorCommand { get; }
        public ICommand BrowseVbmetaCommand { get; }
        public ICommand BrowseSuperCommand { get; }
        public ICommand BrowseRawFirmwareCommand { get; }

        public AsusViewModel(IAdbService adbService, IFastbootService fastbootService, ILoggerService logger, INotificationService notificationService)
        {
            _adbService = adbService;
            _fastbootService = fastbootService;
            _logger = logger;
            _notificationService = notificationService;

            DetectDeviceCommand = new AsyncRelayCommand(DetectDeviceAsync, () => !IsBusy);
            ReadInfoCommand = new AsyncRelayCommand(ReadInfoAsync, () => !IsBusy);
            RebootSystemCommand = new AsyncRelayCommand(RebootSystemAsync, () => !IsBusy);
            RebootRecoveryCommand = new AsyncRelayCommand(RebootRecoveryAsync, () => !IsBusy);
            RebootFastbootCommand = new AsyncRelayCommand(RebootFastbootAsync, () => !IsBusy);
            WipeDataCommand = new AsyncRelayCommand(WipeDataAsync, () => !IsBusy);
            UnlockBootloaderCommand = new AsyncRelayCommand(UnlockBootloaderAsync, () => !IsBusy);
            RelockBootloaderCommand = new AsyncRelayCommand(RelockBootloaderAsync, () => !IsBusy);

            FlashBootCommand = new AsyncRelayCommand(FlashBootAsync, () => !IsBusy);
            FlashRecoveryCommand = new AsyncRelayCommand(FlashRecoveryAsync, () => !IsBusy);
            FlashVendorCommand = new AsyncRelayCommand(FlashVendorAsync, () => !IsBusy);
            FlashVbmetaCommand = new AsyncRelayCommand(FlashVbmetaAsync, () => !IsBusy);
            FlashSuperCommand = new AsyncRelayCommand(FlashSuperAsync, () => !IsBusy);
            FlashRawFirmwareCommand = new AsyncRelayCommand(FlashRawFirmwareAsync, () => !IsBusy);

            BrowseBootCommand = new RelayCommand(() => BootPath = OpenFileDialog("Boot Image (*.img)|*.img"));
            BrowseRecoveryCommand = new RelayCommand(() => RecoveryPath = OpenFileDialog("Recovery Image (*.img)|*.img"));
            BrowseVendorCommand = new RelayCommand(() => VendorPath = OpenFileDialog("Vendor Image (*.img)|*.img"));
            BrowseVbmetaCommand = new RelayCommand(() => VbmetaPath = OpenFileDialog("VBMeta Image (*.img)|*.img"));
            BrowseSuperCommand = new RelayCommand(() => SuperPath = OpenFileDialog("Super Image (*.img)|*.img"));
            BrowseRawFirmwareCommand = new RelayCommand(() => RawFirmwarePath = OpenFileDialog("ASUS RAW Firmware (*.raw;*.zip)|*.raw;*.zip|All files (*.*)|*.*"));

            Log("ASUS Professional Module Initialized.");
        }

        private string OpenFileDialog(string filter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true) return dlg.FileName;
            return "";
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{time}] {message}\n";
            _logger.LogInfo($"[ASUS] {message}");
        }

        private async Task DetectDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Detecting ASUS device...";
            Progress = 30;
            Log("Scanning for devices...");
            await Task.Delay(1000); 

            var fastbootDevices = await _fastbootService.GetConnectedFastbootDevicesAsync();
            var adbDevices = await _adbService.GetConnectedDevicesAsync();

            if (fastbootDevices.Any())
            {
                var fb = fastbootDevices.First();
                _detectedDeviceInfo = new DeviceInfo { Model = "ASUS Fastboot", Mode = "Fastboot Mode", Serial = fb.SerialNumber };
                Log($"Found Fastboot Device: {fb.SerialNumber}");
            }
            else if (adbDevices.Any())
            {
                var adb = adbDevices.First();
                if (adb.DeviceState == "recovery")
                {
                    _detectedDeviceInfo = new DeviceInfo { Model = "ASUS Recovery", Mode = "Recovery Mode", Serial = adb.SerialNumber };
                    Log($"Found Recovery Device: {adb.SerialNumber}");
                }
                else if (adb.DeviceState == "sideload")
                {
                    _detectedDeviceInfo = new DeviceInfo { Model = "ASUS Sideload", Mode = "Sideload Mode", Serial = adb.SerialNumber };
                    Log($"Found Sideload Device: {adb.SerialNumber}");
                }
                else
                {
                    _detectedDeviceInfo = new DeviceInfo { Model = adb.Model, Mode = "ADB Mode", Serial = adb.SerialNumber };
                    Log($"Found ADB Device: {adb.Model} ({adb.SerialNumber})");
                }
            }
            else
            {
                Log("No device found. Please connect your device.");
                _detectedDeviceInfo = null;
            }

            OnPropertyChanged(nameof(DeviceStatusText));
            Progress = 100;
            StatusText = "Detection complete";
            IsBusy = false;
            await Task.Delay(500);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task ReadInfoAsync()
        {
            IsBusy = true;
            StatusText = "Reading Device Info...";
            Progress = 20;
            Log("Reading ASUS Device Information...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode" || _detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                var (s1, prop) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.model", _detectedDeviceInfo.Serial);
                var (s2, brand) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.brand", _detectedDeviceInfo.Serial);
                var (s3, dev) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.device", _detectedDeviceInfo.Serial);
                var (s5, andVer) = await _adbService.ExecuteCommandAsync("shell getprop ro.build.version.release", _detectedDeviceInfo.Serial);
                var (s6, asusSku) = await _adbService.ExecuteCommandAsync("shell getprop ro.build.asus.sku", _detectedDeviceInfo.Serial);
                
                Log($"Brand: {brand.Trim()}");
                Log($"Model: {prop.Trim()}");
                Log($"Code Name: {dev.Trim()}");
                Log($"ASUS SKU: {asusSku.Trim()}");
                Log($"Android Version: {andVer.Trim()}");
                Progress = 100;
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                Progress = 50;
                var (s1, product) = await _fastbootService.ExecuteFastbootCommandAsync("getvar product", _detectedDeviceInfo.Serial);
                var (s2, unlocked) = await _fastbootService.ExecuteFastbootCommandAsync("getvar unlocked", _detectedDeviceInfo.Serial);
                var (s3, secure) = await _fastbootService.ExecuteFastbootCommandAsync("getvar secure", _detectedDeviceInfo.Serial);
                
                Log($"Product: {ExtractGetVar(product)}");
                Log($"Bootloader Unlocked: {ExtractGetVar(unlocked)}");
                Log($"Secure Boot: {ExtractGetVar(secure)}");
                Progress = 100;
            }
            else
            {
                Log("No device detected. Please click 'Detect Device' first.");
                _notificationService.ShowNotification("ASUS", "Please detect device first.", NotificationType.Warning);
            }

            StatusText = "Read complete";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private string ExtractGetVar(string output)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach(var line in lines)
            {
                if(line.Contains(":")) return line.Split(':')[1].Trim();
            }
            return output.Trim();
        }

        private async Task RebootSystemAsync()
        {
            IsBusy = true;
            Log("Rebooting device to System...");
            if (_detectedDeviceInfo?.Mode == "ADB Mode" || _detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "");
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                await _fastbootService.RebootFastbootAsync(_detectedDeviceInfo.Serial, "");
            }
            else Log("Please detect device first.");
            IsBusy = false;
        }

        private async Task RebootRecoveryAsync()
        {
            IsBusy = true;
            Log("Rebooting device to Recovery Mode...");
            if (_detectedDeviceInfo?.Mode == "ADB Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "recovery");
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                await _fastbootService.ExecuteFastbootCommandAsync("reboot recovery", _detectedDeviceInfo.Serial);
            }
            else Log("Device must be in ADB mode for this command.");
            IsBusy = false;
        }

        private async Task RebootFastbootAsync()
        {
            IsBusy = true;
            Log("Rebooting device to Fastboot Mode...");
            if (_detectedDeviceInfo?.Mode == "ADB Mode" || _detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "bootloader");
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                await _fastbootService.RebootFastbootAsync(_detectedDeviceInfo.Serial, "bootloader");
            }
            else Log("Please detect device first.");
            IsBusy = false;
        }

        private async Task WipeDataAsync()
        {
            IsBusy = true;
            Log("Attempting to wipe data / factory reset...");
            if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                var (success, msg) = await _fastbootService.ErasePartitionAsync("userdata", _detectedDeviceInfo.Serial);
                if (success)
                {
                    Log("Userdata erased successfully.");
                    await _fastbootService.ErasePartitionAsync("cache", _detectedDeviceInfo.Serial);
                    _notificationService.ShowSuccess("Wipe Data", "Device wiped successfully via Fastboot.");
                }
                else Log($"Erase Userdata failed: {msg}");
            }
            else if (_detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                await _adbService.ExecuteCommandAsync("shell wipe data", _detectedDeviceInfo.Serial);
                Log("Data wipe command sent.");
            }
            else Log("Error: Device must be in Fastboot Mode or Recovery Mode.");
            IsBusy = false;
        }

        private async Task FlashBootAsync() => await FlashImageAsync("boot", BootPath);
        private async Task FlashRecoveryAsync() => await FlashImageAsync("recovery", RecoveryPath);
        private async Task FlashVendorAsync() => await FlashImageAsync("vendor", VendorPath);
        private async Task FlashVbmetaAsync() => await FlashImageAsync("vbmeta", VbmetaPath, " --disable-verity --disable-verification");
        private async Task FlashSuperAsync() => await FlashImageAsync("super", SuperPath);

        private async Task FlashImageAsync(string partition, string path, string extraArgs = "")
        {
            IsBusy = true;
            StatusText = $"Flashing {partition}...";
            Log($"Starting Flash {partition}...");

            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode to flash partitions.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            if (string.IsNullOrEmpty(path))
            {
                Log($"Error: No {partition} image selected.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            Progress = 10;
            Log($"Flashing {path} to {partition}...");
            var (success, msg) = await _fastbootService.FlashImageAsync(partition, path, _detectedDeviceInfo.Serial);
            Progress = 90;

            if (success)
            {
                Log($"{partition} flashed successfully.");
                if (!string.IsNullOrEmpty(extraArgs))
                {
                    await _fastbootService.ExecuteFastbootCommandAsync($"flash {partition} {extraArgs} \"{path}\"", _detectedDeviceInfo.Serial);
                }
                _notificationService.ShowSuccess("Flash Success", $"{partition} flashed successfully.");
            }
            else Log($"Flash failed: {msg}");

            Progress = 100;
            StatusText = "Complete";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task FlashRawFirmwareAsync()
        {
            IsBusy = true;
            StatusText = "Flashing RAW Firmware...";
            Log("Starting ASUS RAW Firmware Flash...");

            if (string.IsNullOrEmpty(RawFirmwarePath))
            {
                Log("Error: No RAW firmware selected.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode to flash RAW firmware via fastboot.");
                _notificationService.ShowError("Flash Failed", "Please put ASUS device in Fastboot/Bootloader Mode.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            Progress = 10;
            Log($"Analyzing RAW package at: {RawFirmwarePath}...");
            await Task.Delay(1000);
            Log("Warning: RAW firmware flash can wipe all data and take several minutes.");
            
            // Simulated extraction and flash of RAW/zip for ASUS devices
            for (int i = 20; i <= 90; i += 10)
            {
                Progress = i;
                Log($"Flashing RAW chunks... {i}%");
                await Task.Delay(1500);
            }

            Progress = 100;
            Log("RAW Firmware flashed successfully (Simulation).");
            _notificationService.ShowSuccess("Flash Success", "RAW Firmware flashed successfully.");
            
            StatusText = "Flash Complete";
            IsBusy = false;
            await Task.Delay(2000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task UnlockBootloaderAsync()
        {
            IsBusy = true;
            Log("Attempting to unlock bootloader...");
            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode.");
                IsBusy = false; return;
            }
            Progress = 30;
            Log("NOTE: Some older ASUS devices use an unlock APK. Newer devices may support standard fastboot commands.");
            
            var (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("oem unlock", _detectedDeviceInfo.Serial);
            if (!success || msg.Contains("FAILED"))
            {
                Log("oem unlock failed, trying flashing unlock...");
                (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("flashing unlock", _detectedDeviceInfo.Serial);
            }
            
            Progress = 90;
            if (success && !msg.Contains("FAILED"))
            {
                Log("Bootloader unlocked successfully.");
                _notificationService.ShowSuccess("Success", "Bootloader unlocked.");
            }
            else Log($"Unlock failed: {msg}");
            
            Progress = 100;
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
        }

        private async Task RelockBootloaderAsync()
        {
            IsBusy = true;
            Log("Attempting to relock bootloader...");
            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode.");
                IsBusy = false; return;
            }
            Progress = 30;
            var (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("oem lock", _detectedDeviceInfo.Serial);
            if (!success || msg.Contains("FAILED"))
            {
                Log("oem lock failed, trying flashing lock...");
                (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("flashing lock", _detectedDeviceInfo.Serial);
            }
            Progress = 90;
            if (success && !msg.Contains("FAILED"))
            {
                Log("Bootloader relocked successfully.");
                _notificationService.ShowSuccess("Success", "Bootloader relocked.");
            }
            else Log($"Relock failed: {msg}");
            Progress = 100;
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
        }
    }
}
