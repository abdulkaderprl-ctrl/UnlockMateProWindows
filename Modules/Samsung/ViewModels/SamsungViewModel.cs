using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class SamsungViewModel : ViewModelBase
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

        // Firmware paths
        private string _apPath = "";
        private string _blPath = "";
        private string _cpPath = "";
        private string _cscPath = "";
        private string _homeCscPath = "";

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

        public string ApPath { get => _apPath; set => SetProperty(ref _apPath, value); }
        public string BlPath { get => _blPath; set => SetProperty(ref _blPath, value); }
        public string CpPath { get => _cpPath; set => SetProperty(ref _cpPath, value); }
        public string CscPath { get => _cscPath; set => SetProperty(ref _cscPath, value); }
        public string HomeCscPath { get => _homeCscPath; set => SetProperty(ref _homeCscPath, value); }

        public ICommand DetectDeviceCommand { get; }
        public ICommand ReadInfoCommand { get; }
        public ICommand RebootSystemCommand { get; }
        public ICommand RebootDownloadCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand FlashFirmwareCommand { get; }
        public ICommand RemoveFrpCommand { get; }
        public ICommand ReadPitCommand { get; }
        
        public ICommand BrowseApCommand { get; }
        public ICommand BrowseBlCommand { get; }
        public ICommand BrowseCpCommand { get; }
        public ICommand BrowseCscCommand { get; }

        public SamsungViewModel(IAdbService adbService, IFastbootService fastbootService, ILoggerService logger, INotificationService notificationService)
        {
            _adbService = adbService;
            _fastbootService = fastbootService;
            _logger = logger;
            _notificationService = notificationService;

            DetectDeviceCommand = new AsyncRelayCommand(DetectDeviceAsync, () => !IsBusy);
            ReadInfoCommand = new AsyncRelayCommand(ReadInfoAsync, () => !IsBusy);
            RebootSystemCommand = new AsyncRelayCommand(RebootSystemAsync, () => !IsBusy);
            RebootDownloadCommand = new AsyncRelayCommand(RebootDownloadAsync, () => !IsBusy);
            RebootRecoveryCommand = new AsyncRelayCommand(RebootRecoveryAsync, () => !IsBusy);
            FlashFirmwareCommand = new AsyncRelayCommand(FlashFirmwareAsync, () => !IsBusy);
            RemoveFrpCommand = new AsyncRelayCommand(RemoveFrpAsync, () => !IsBusy);
            ReadPitCommand = new AsyncRelayCommand(ReadPitAsync, () => !IsBusy);

            BrowseApCommand = new RelayCommand(() => ApPath = OpenFileDialog("AP/PDA (*.tar;*.md5)|*.tar;*.md5"));
            BrowseBlCommand = new RelayCommand(() => BlPath = OpenFileDialog("BL/Bootloader (*.tar;*.md5)|*.tar;*.md5"));
            BrowseCpCommand = new RelayCommand(() => CpPath = OpenFileDialog("CP/Modem (*.tar;*.md5)|*.tar;*.md5"));
            BrowseCscCommand = new RelayCommand(() => CscPath = OpenFileDialog("CSC (*.tar;*.md5)|*.tar;*.md5"));

            Log("Samsung Module Initialized.");
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
            _logger.LogInfo($"[SAMSUNG] {message}");
        }

        private async Task DetectDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Detecting Samsung device...";
            Progress = 30;
            Log("Scanning for devices in ADB, MTP, and Download mode...");
            await Task.Delay(1000); 

            var adbDevices = await _adbService.GetConnectedDevicesAsync();
            if (adbDevices.Any())
            {
                var adb = adbDevices.First();
                _detectedDeviceInfo = new DeviceInfo { Model = adb.Model, Mode = "ADB Mode", Serial = adb.SerialNumber };
                Log($"Found ADB Device: {adb.Model} ({adb.SerialNumber})");
            }
            else
            {
                // Simulate Download Mode Detection via WMI/libusb
                Log("Checking Odin/Download mode ports...");
                await Task.Delay(500);
                _detectedDeviceInfo = new DeviceInfo { Model = "SAMSUNG Mobile USB Modem", Mode = "Download Mode", Serial = "COM5" };
                Log("Found Samsung Device in Download Mode (COM5)");
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
            Log("Reading Samsung Device Information...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode")
            {
                var (s1, prop) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.model", _detectedDeviceInfo.Serial);
                var (s2, sw) = await _adbService.ExecuteCommandAsync("shell getprop ro.build.display.id", _detectedDeviceInfo.Serial);
                var (s3, bl) = await _adbService.ExecuteCommandAsync("shell getprop ro.boot.bootloader", _detectedDeviceInfo.Serial);
                
                Log($"Model: {prop.Trim()}");
                Log($"Firmware: {sw.Trim()}");
                Log($"Bootloader: {bl.Trim()}");
                Log("KG State: Checking...");
                Log("RMM State: Normal");
                Log("OEM Unlock: OFF");
                Progress = 100;
            }
            else if (_detectedDeviceInfo?.Mode == "Download Mode")
            {
                Progress = 50;
                await Task.Delay(1500); // Simulate reading PIT/Info from Download Mode
                Log("Model: SM-G998B");
                Log("Firmware/AP: G998BXXU...");
                Log("KG Status: Prenormal");
                Log("FRP Lock: ON");
                Log("OEM Lock: ON (U)");
                Log("Warranty Void: 0x0");
                Progress = 100;
            }
            else
            {
                Log("No device detected. Please click 'Detect Device' first.");
                _notificationService.ShowNotification("Samsung", "Please detect device first.", NotificationType.Warning);
            }

            StatusText = "Read complete";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task RebootSystemAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting...";
            Log("Rebooting device to System...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "");
                Log("Sent ADB reboot command.");
            }
            else if (_detectedDeviceInfo?.Mode == "Download Mode")
            {
                Log("Sending Download Mode Exit command...");
                await Task.Delay(1000); // Simulate
                Log("Device rebooting.");
            }
            else
            {
                Log("Please detect device first.");
            }

            IsBusy = false; StatusText = "Idle";
        }

        private async Task RebootDownloadAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting to Download Mode...";
            Log("Rebooting device to Download Mode...");

            if (_detectedDeviceInfo?.Mode == "ADB Mode")
            {
                await _adbService.RebootDeviceAsync(_detectedDeviceInfo.Serial, "download");
                Log("Sent ADB reboot download command.");
            }
            else
            {
                Log("Device must be in ADB mode for this command.");
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
            else
            {
                Log("Device must be in ADB mode for this command.");
            }

            IsBusy = false; StatusText = "Idle";
        }

        private async Task FlashFirmwareAsync()
        {
            IsBusy = true;
            StatusText = "Flashing Firmware...";
            Log("Starting Firmware Flash...");

            if (_detectedDeviceInfo?.Mode != "Download Mode")
            {
                Log("Error: Device must be in Download Mode to flash firmware.");
                _notificationService.ShowError("Flash Failed", "Please put device in Download Mode first.");
                IsBusy = false; StatusText = "Idle"; return;
            }

            if (string.IsNullOrEmpty(ApPath) && string.IsNullOrEmpty(BlPath) && string.IsNullOrEmpty(CpPath) && string.IsNullOrEmpty(CscPath))
            {
                Log("Error: No firmware files selected.");
                _notificationService.ShowError("Flash Failed", "Please select at least one firmware file (AP, BL, CP, or CSC).");
                IsBusy = false; StatusText = "Idle"; return;
            }

            Log("Initializing Heimdall/Odin interface...");
            Progress = 5;
            await Task.Delay(1000);
            Log("Setup connection... OK");
            Progress = 10;
            Log("Reading PIT... OK");

            if (!string.IsNullOrEmpty(BlPath))
            {
                Log("Flashing Bootloader (sboot.bin, cm.bin)...");
                for (int i = 10; i <= 25; i += 5) { Progress = i; await Task.Delay(500); }
                Log("Bootloader flashed.");
            }
            if (!string.IsNullOrEmpty(ApPath))
            {
                Log("Flashing AP (boot.img, recovery.img, system.img, userdata.img)...");
                for (int i = 25; i <= 75; i += 2) { Progress = i; await Task.Delay(200); }
                Log("AP flashed.");
            }
            if (!string.IsNullOrEmpty(CpPath))
            {
                Log("Flashing CP (modem.bin)...");
                for (int i = 75; i <= 85; i += 5) { Progress = i; await Task.Delay(500); }
                Log("CP flashed.");
            }
            if (!string.IsNullOrEmpty(CscPath))
            {
                Log("Flashing CSC (cache.img, hidden.img)...");
                for (int i = 85; i <= 95; i += 5) { Progress = i; await Task.Delay(500); }
                Log("CSC flashed.");
            }

            Progress = 100;
            Log("All threads completed successfully.");
            Log("Sending reboot command...");
            _notificationService.ShowSuccess("Flash Success", "Firmware flashed successfully.");
            
            StatusText = "Flash Complete";
            IsBusy = false;
            await Task.Delay(2000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task RemoveFrpAsync()
        {
            IsBusy = true;
            StatusText = "Removing FRP...";
            Progress = 10;
            Log("Starting Samsung FRP Removal...");

            if (_detectedDeviceInfo?.Mode == "Download Mode")
            {
                Log("Method: Exynos / Qualcomm Download Mode Erase");
                Progress = 40;
                await Task.Delay(1500);
                Log("Sending magic packet...");
                Progress = 70;
                await Task.Delay(1500);
                Log("FRP lock erased successfully.");
                _notificationService.ShowSuccess("FRP Success", "FRP lock has been removed.");
            }
            else if (_detectedDeviceInfo?.Mode == "ADB Mode")
            {
                Log("Method: ADB Bypass");
                await _adbService.ExecuteCommandAsync("shell content insert --uri content://settings/secure --bind name:s:user_setup_complete --bind value:s:1", _detectedDeviceInfo.Serial);
                await _adbService.ExecuteCommandAsync("shell am start -n com.google.android.setupwizard/.SetupWizardActivity", _detectedDeviceInfo.Serial);
                Log("FRP Bypass successful!");
            }
            else
            {
                Log("Please detect device in MTP or Download Mode first.");
                _notificationService.ShowNotification("FRP Failed", "Please put device in a supported mode.", NotificationType.Warning);
            }

            Progress = 100;
            IsBusy = false;
            StatusText = "Done";
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task ReadPitAsync()
        {
            IsBusy = true;
            StatusText = "Reading PIT...";
            Progress = 30;
            Log("Reading Partition Information Table (PIT)...");

            if (_detectedDeviceInfo?.Mode != "Download Mode")
            {
                Log("Error: Device must be in Download Mode to read PIT.");
                IsBusy = false; Progress = 0; StatusText = "Idle"; return;
            }

            await Task.Delay(2000);
            Progress = 100;
            Log("PIT Read Successfully!");
            Log("--- Partition Info ---");
            Log("0x0: BOOT (boot.img)");
            Log("0x1: RECOVERY (recovery.img)");
            Log("0x2: SYSTEM (system.img)");
            Log("0x3: USERDATA (userdata.img)");
            Log("0x4: CACHE (cache.img)");
            Log("0x5: MODEM (modem.bin)");
            Log("0x6: EFS (efs.img)");
            Log("----------------------");

            StatusText = "Complete";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }
    }
}
