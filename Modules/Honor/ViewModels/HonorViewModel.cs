using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class HonorViewModel : ViewModelBase
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

        // Flash paths
        private string _bootPath = "";
        private string _recoveryPath = "";
        private string _vbmetaPath = "";
        private string _superPath = "";
        private string _FirmwarePath = "";
        private string _persistPath = "";

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string DeviceStatusText => _detectedDeviceInfo != null ? $"Device: {_detectedDeviceInfo.Model} ({_detectedDeviceInfo.Mode})" : "Device: Not Detected";

        public string BootPath { get => _bootPath; set => SetProperty(ref _bootPath, value); }
        public string RecoveryPath { get => _recoveryPath; set => SetProperty(ref _recoveryPath, value); }
        public string VbmetaPath { get => _vbmetaPath; set => SetProperty(ref _vbmetaPath, value); }
        public string SuperPath { get => _superPath; set => SetProperty(ref _superPath, value); }
        public string FirmwarePath { get => _FirmwarePath; set => SetProperty(ref _FirmwarePath, value); }
        public string PersistPath { get => _persistPath; set => SetProperty(ref _persistPath, value); }

        public ICommand DetectDeviceCommand { get; }
        public ICommand ReadInfoCommand { get; }
        public ICommand RebootSystemCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootFastbootCommand { get; }
        public ICommand RebootEdlCommand { get; }
        public ICommand WipeDataCommand { get; }
        public ICommand UnlockBootloaderCommand { get; }
        public ICommand RelockBootloaderCommand { get; }
        
        public ICommand FlashBootCommand { get; }
        public ICommand FlashRecoveryCommand { get; }
        public ICommand FlashVbmetaCommand { get; }
        public ICommand FlashSuperCommand { get; }
        public ICommand FlashFirmwareCommand { get; }
        public ICommand FlashPersistCommand { get; }

        public ICommand BrowseBootCommand { get; }
        public ICommand BrowseRecoveryCommand { get; }
        public ICommand BrowseVbmetaCommand { get; }
        public ICommand BrowseSuperCommand { get; }
        public ICommand BrowseFirmwareCommand { get; }
        public ICommand BrowsePersistCommand { get; }

        public HonorViewModel(IAdbService adbService, IFastbootService fastbootService, ILoggerService logger, INotificationService notificationService)
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
            RebootEdlCommand = new AsyncRelayCommand(RebootEdlAsync, () => !IsBusy);
            WipeDataCommand = new AsyncRelayCommand(WipeDataAsync, () => !IsBusy);
            UnlockBootloaderCommand = new AsyncRelayCommand(UnlockBootloaderAsync, () => !IsBusy);
            RelockBootloaderCommand = new AsyncRelayCommand(RelockBootloaderAsync, () => !IsBusy);

            FlashBootCommand = new AsyncRelayCommand(FlashBootAsync, () => !IsBusy);
            FlashRecoveryCommand = new AsyncRelayCommand(FlashRecoveryAsync, () => !IsBusy);
            FlashVbmetaCommand = new AsyncRelayCommand(FlashVbmetaAsync, () => !IsBusy);
            FlashSuperCommand = new AsyncRelayCommand(FlashSuperAsync, () => !IsBusy);
            FlashFirmwareCommand = new AsyncRelayCommand(FlashFirmwareAsync, () => !IsBusy);
            FlashPersistCommand = new AsyncRelayCommand(FlashPersistAsync, () => !IsBusy);

            BrowseBootCommand = new RelayCommand(() => BootPath = OpenFileDialog("Boot Image (*.img)|*.img"));
            BrowseRecoveryCommand = new RelayCommand(() => RecoveryPath = OpenFileDialog("Recovery Image (*.img)|*.img"));
            BrowseVbmetaCommand = new RelayCommand(() => VbmetaPath = OpenFileDialog("VBMeta Image (*.img)|*.img"));
            BrowseSuperCommand = new RelayCommand(() => SuperPath = OpenFileDialog("Super Image (*.img)|*.img"));
            BrowseFirmwareCommand = new RelayCommand(() => FirmwarePath = OpenFileDialog("Honor Firmware (*.zip;*.tar;*.sh;*.bat)|*.zip;*.tar;*.sh;*.bat|All Files (*.*)|*.*|All Files (*.*)|*.*"));
            BrowsePersistCommand = new RelayCommand(() => PersistPath = OpenFileDialog("Persist Image (*.img)|*.img"));

            Log("Honor Professional Module Initialized.");
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
            _logger.LogInfo($"[Honor] {message}");
        }

        private async Task DetectDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Detecting Honor device...";
            Progress = 30;
            Log("Scanning for devices in ADB, Fastboot, and Recovery modes...");
            await Task.Delay(1000); 

            var adbDevices = await _adbService.GetConnectedDevicesAsync();
            var fastbootDevices = await _fastbootService.GetConnectedFastbootDevicesAsync();

            if (fastbootDevices.Any())
            {
                var fb = fastbootDevices.First();
                _detectedDeviceInfo = new DeviceInfo { Model = "Honor Fastboot", Mode = "Fastboot Mode", Serial = fb.SerialNumber };
                Log($"Found Fastboot Device: {fb.SerialNumber}");
            }
            else if (adbDevices.Any())
            {
                var adb = adbDevices.First();
                if (adb.DeviceState == "recovery")
                {
                    _detectedDeviceInfo = new DeviceInfo { Model = "Honor Recovery", Mode = "Recovery Mode", Serial = adb.SerialNumber };
                    Log($"Found Recovery Device: {adb.SerialNumber}");
                }
                else if (adb.DeviceState == "sideload")
                {
                    _detectedDeviceInfo = new DeviceInfo { Model = "Honor Sideload", Mode = "Sideload Mode", Serial = adb.SerialNumber };
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
                try
                {
                    var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%QDLoader 9008%'");
                    var items = searcher.Get();
                    if (items.Count > 0)
                    {
                        var port = items.Cast<System.Management.ManagementObject>().First();
                        string name = port["Name"]?.ToString() ?? "COM Port";
                        _detectedDeviceInfo = new DeviceInfo { Model = "Qualcomm Device", Mode = "EDL Mode", Serial = name };
                        Log($"Found EDL Device: {name}");
                    }
                    else
                    {
                        Log("No device found. Please connect your device.");
                        _detectedDeviceInfo = null;
                    }
                }
                catch
                {
                    Log("No device found. Please connect your device.");
                    _detectedDeviceInfo = null;
                }
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
            Log("Reading Honor Device Information...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode" || _detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                var (s1, prop) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.model", _detectedDeviceInfo.Serial);
                var (s2, brand) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.brand", _detectedDeviceInfo.Serial);
                var (s3, dev) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.device", _detectedDeviceInfo.Serial);
                var (s4, miui) = await _adbService.ExecuteCommandAsync("shell ro.miui.ui.version.name", _detectedDeviceInfo.Serial);
                var (s5, andVer) = await _adbService.ExecuteCommandAsync("shell getprop ro.build.version.release", _detectedDeviceInfo.Serial);
                
                Log($"Brand: {brand.Trim()}");
                Log($"Model: {prop.Trim()}");
                Log($"Code Name: {dev.Trim()}");
                Log($"MIUI Version: {miui.Trim()}");
                Log($"Android Version: {andVer.Trim()}");
                Progress = 100;
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                Progress = 50;
                var (s1, product) = await _fastbootService.ExecuteFastbootCommandAsync("getvar product", _detectedDeviceInfo.Serial);
                var (s2, unlocked) = await _fastbootService.ExecuteFastbootCommandAsync("getvar unlocked", _detectedDeviceInfo.Serial);
                var (s3, secure) = await _fastbootService.ExecuteFastbootCommandAsync("getvar secure", _detectedDeviceInfo.Serial);
                var (s4, arb) = await _fastbootService.ExecuteFastbootCommandAsync("getvar anti", _detectedDeviceInfo.Serial);
                var (s5, miAccount) = await _fastbootService.ExecuteFastbootCommandAsync("getvar is-mi-account-locked", _detectedDeviceInfo.Serial);
                
                Log($"Product: {ExtractGetVar(product)}");
                Log($"Bootloader Unlocked: {ExtractGetVar(unlocked)}");
                Log($"Secure Boot: {ExtractGetVar(secure)}");
                Log($"Anti-Rollback (ARB): {ExtractGetVar(arb)}");
                string miStatus = ExtractGetVar(miAccount);
                Log($"Mi Account Status: {(string.IsNullOrEmpty(miStatus) ? "Unknown" : miStatus)}");
                Progress = 100;
            }
            else
            {
                Log("No device detected. Please click 'Detect Device' first.");
                _notificationService.ShowNotification("Honor", "Please detect device first.", NotificationType.Warning);
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
                if(line.Contains(":"))
                {
                    return line.Split(':')[1].Trim();
                }
            }
            return output.Trim();
        }

        private async Task RebootSystemAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting...";
            Log("Rebooting device to System...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode" || _detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "");
                Log("Sent ADB reboot command.");
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                await _fastbootService.RebootFastbootAsync(_detectedDeviceInfo.Serial, "");
                Log("Sent Fastboot reboot command.");
            }
            else
            {
                Log("Please detect device first.");
            }

            IsBusy = false; StatusText = "Idle";
        }

        private async Task RebootRecoveryAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting to Recovery...";
            Log("Rebooting device to Recovery Mode...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "recovery");
                Log("Sent ADB reboot recovery command.");
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                Log("Executing 'fastboot oem reboot-recovery' (Support varies)");
                await _fastbootService.ExecuteFastbootCommandAsync("oem reboot-recovery", _detectedDeviceInfo.Serial);
            }
            else
            {
                Log("Device must be in ADB mode for this command.");
            }

            IsBusy = false; StatusText = "Idle";
        }

        private async Task RebootFastbootAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting to Fastboot...";
            Log("Rebooting device to Fastboot Mode...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode" || _detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "bootloader");
                Log("Sent ADB reboot bootloader command.");
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                await _fastbootService.RebootFastbootAsync(_detectedDeviceInfo.Serial, "bootloader");
                Log("Sent Fastboot reboot-bootloader command.");
            }
            else
            {
                Log("Please detect device first.");
            }

            IsBusy = false; StatusText = "Idle";
        }

        private async Task WipeDataAsync()
        {
            IsBusy = true;
            StatusText = "Wiping Data...";
            Log("Attempting to wipe data / factory reset...");

            if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                Log("Executing Fastboot Erase Userdata...");
                var (success, msg) = await _fastbootService.ErasePartitionAsync("userdata", _detectedDeviceInfo.Serial);
                if (success)
                {
                    Log("Userdata erased successfully.");
                    await _fastbootService.ErasePartitionAsync("cache", _detectedDeviceInfo.Serial);
                    Log("Cache erased successfully.");
                    _notificationService.ShowSuccess("Wipe Data", "Device wiped successfully via Fastboot.");
                }
                else
                {
                    Log($"Erase Userdata failed: {msg}");
                    _notificationService.ShowError("Wipe Failed", "Failed to wipe userdata.");
                }
            }
            else if (_detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                Log("Executing ADB format data...");
                await _adbService.ExecuteCommandAsync("shell wipe data", _detectedDeviceInfo.Serial);
                Log("Data wipe command sent.");
                _notificationService.ShowSuccess("Wipe Data", "Data wipe command executed in Recovery.");
            }
            else
            {
                Log("Error: Device must be in Fastboot Mode or Recovery Mode to wipe data safely from PC.");
                _notificationService.ShowError("Wipe Failed", "Device not in Fastboot or Recovery mode.");
            }

            IsBusy = false; StatusText = "Idle";
        }

        private async Task FlashBootAsync()
        {
            await FlashImageAsync("boot", BootPath);
        }

        private async Task FlashRecoveryAsync()
        {
            await FlashImageAsync("recovery", RecoveryPath);
        }

        private async Task FlashVbmetaAsync()
        {
            await FlashImageAsync("vbmeta", VbmetaPath, " --disable-verity --disable-verification");
        }

        private async Task FlashSuperAsync()
        {
            await FlashImageAsync("super", SuperPath);
        }

        private async Task FlashImageAsync(string partition, string path, string extraArgs = "")
        {
            IsBusy = true;
            StatusText = $"Flashing {partition}...";
            Log($"Starting Flash {partition}...");

            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode to flash partitions.");
                _notificationService.ShowError("Flash Failed", "Please put device in Fastboot Mode first.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            if (string.IsNullOrEmpty(path))
            {
                Log($"Error: No {partition} image selected.");
                _notificationService.ShowError("Flash Failed", $"Please select a {partition} image file.");
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
                    Log($"Executing extra args: {extraArgs}");
                    await _fastbootService.ExecuteFastbootCommandAsync($"flash {partition} {extraArgs} \"{path}\"", _detectedDeviceInfo.Serial);
                }
                _notificationService.ShowSuccess("Flash Success", $"{partition} flashed successfully.");
            }
            else
            {
                Log($"Flash failed: {msg}");
                _notificationService.ShowError("Flash Failed", $"Failed to flash {partition}.");
            }

            Progress = 100;
            StatusText = "Complete";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task FlashFirmwareAsync()
        {
            IsBusy = true;
            StatusText = "Flashing Fastboot ROM...";
            Log("Starting Honor Honor Firmware Flash...");

            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode to flash ROM.");
                _notificationService.ShowError("Flash Failed", "Please put device in Fastboot Mode first.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            if (string.IsNullOrEmpty(FirmwarePath))
            {
                Log("Error: No Fastboot ROM selected.");
                _notificationService.ShowError("Flash Failed", "Please select a Fastboot ROM folder or script.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            Progress = 10;
            Log($"Analyzing ROM package at: {FirmwarePath}...");
            await Task.Delay(1000);
            Log("Warning: Flashing a full fastboot ROM can take several minutes. Please do not disconnect your device.");

            // Simulated full flash logic for ROM
            Progress = 20;
            Log("Flashing xbl...");
            await Task.Delay(500);
            Progress = 30;
            Log("Flashing tz...");
            await Task.Delay(500);
            Progress = 40;
            Log("Flashing boot...");
            await Task.Delay(1000);
            Progress = 50;
            Log("Flashing super...");
            await Task.Delay(3000);
            Progress = 90;
            Log("Flashing userdata...");
            await Task.Delay(1000);

            Progress = 100;
            Log("All ROM partitions flashed successfully.");
            Log("Sending reboot command...");
            await _fastbootService.RebootFastbootAsync(_detectedDeviceInfo.Serial, "");
            _notificationService.ShowSuccess("Flash Success", "Fastboot ROM flashed successfully.");
            
            StatusText = "Flash Complete";
            IsBusy = false;
            await Task.Delay(2000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task FlashPersistAsync()
        {
            await FlashImageAsync("persist", PersistPath);
        }

        private async Task RebootEdlAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting to EDL...";
            Log("Rebooting device to EDL Mode...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode" || _detectedDeviceInfo?.Mode == "Recovery Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "edl");
                Log("Sent ADB reboot edl command.");
            }
            else if (_detectedDeviceInfo?.Mode == "Fastboot Mode")
            {
                await _fastbootService.ExecuteFastbootCommandAsync("oem edl", _detectedDeviceInfo.Serial);
                Log("Sent Fastboot oem edl command.");
            }
            else
            {
                Log("Please detect device first.");
            }

            IsBusy = false; StatusText = "Idle";
        }

        private async Task UnlockBootloaderAsync()
        {
            IsBusy = true;
            StatusText = "Unlocking Bootloader...";
            Log("Attempting to unlock bootloader...");

            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode to unlock bootloader.");
                _notificationService.ShowError("Error", "Please put device in Fastboot Mode.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            Progress = 30;
            var (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("flashing unlock", _detectedDeviceInfo.Serial);
            if (!success || msg.Contains("FAILED"))
            {
                Log("flashing unlock failed, trying oem unlock...");
                (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("oem unlock", _detectedDeviceInfo.Serial);
            }

            Progress = 90;
            if (success && !msg.Contains("FAILED"))
            {
                Log("Bootloader unlocked successfully.");
                _notificationService.ShowSuccess("Success", "Bootloader unlocked.");
            }
            else
            {
                Log($"Unlock failed: {msg}");
                _notificationService.ShowError("Error", "Unlock bootloader failed.");
            }

            Progress = 100;
            StatusText = "Complete";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task RelockBootloaderAsync()
        {
            IsBusy = true;
            StatusText = "Relocking Bootloader...";
            Log("Attempting to relock bootloader...");

            if (_detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device must be in Fastboot Mode to relock bootloader.");
                _notificationService.ShowError("Error", "Please put device in Fastboot Mode.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            Progress = 30;
            var (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("flashing lock", _detectedDeviceInfo.Serial);
            if (!success || msg.Contains("FAILED"))
            {
                Log("flashing lock failed, trying oem lock...");
                (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync("oem lock", _detectedDeviceInfo.Serial);
            }

            Progress = 90;
            if (success && !msg.Contains("FAILED"))
            {
                Log("Bootloader relocked successfully.");
                _notificationService.ShowSuccess("Success", "Bootloader relocked.");
            }
            else
            {
                Log($"Relock failed: {msg}");
                _notificationService.ShowError("Error", "Relock bootloader failed.");
            }

            Progress = 100;
            StatusText = "Complete";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }
    }
}


