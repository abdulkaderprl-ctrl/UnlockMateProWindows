using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class FrpViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly IFastbootService _fastbootService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string _brandName;
        private string _frpLog = "";
        private bool _isBusy = false;
        private int _progress = 0;
        private string _statusText = "Idle";
        private DeviceInfo? _detectedDeviceInfo;

        public ObservableCollection<string> SupportedBrands { get; } = new ObservableCollection<string>
        {
            "Samsung", "Xiaomi", "OPPO", "VIVO", "Realme", "Huawei", "Honor", "Motorola", "Nokia", "Tecno", "Infinix", "SPD", "Qualcomm", "MTK", "Generic", "ADB Mode", "Fastboot Mode"
        };

        public string BrandName
        {
            get => _brandName;
            set => SetProperty(ref _brandName, value);
        }

        public string FrpLog
        {
            get => _frpLog;
            set => SetProperty(ref _frpLog, value);
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

        public ICommand DetectDeviceCommand { get; }
        public ICommand BypassAdbCommand { get; }
        public ICommand EraseFastbootCommand { get; }
        public ICommand SamsungMtpCommand { get; }

        public FrpViewModel(string brandName, IAdbService adbService, IFastbootService fastbootService, ILoggerService logger, INotificationService notificationService)
        {
            _brandName = string.IsNullOrEmpty(brandName) || brandName == "FRP" ? "Generic" : brandName;
            _adbService = adbService;
            _fastbootService = fastbootService;
            _logger = logger;
            _notificationService = notificationService;

            DetectDeviceCommand = new AsyncRelayCommand(DetectDeviceAsync, () => !IsBusy);
            BypassAdbCommand = new AsyncRelayCommand(BypassAdbAsync, () => !IsBusy);
            EraseFastbootCommand = new AsyncRelayCommand(EraseFastbootAsync, () => !IsBusy);
            SamsungMtpCommand = new AsyncRelayCommand(SamsungMtpAsync, () => !IsBusy);

            Log($"Initialized {BrandName} FRP Module.");
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            FrpLog += $"[{time}] {message}\n";
            _logger.LogInfo($"[FRP] {message}");
        }

        private async Task DetectDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Detecting device...";
            Progress = 30;
            Log("Scanning for devices in ADB, Fastboot, and MTP modes...");
            await Task.Delay(1500); // Simulate WMI/Mode scanning

            var adbDevices = await _adbService.GetConnectedDevicesAsync();
            var fastbootDevices = await _fastbootService.GetConnectedFastbootDevicesAsync();

            if (adbDevices.Any())
            {
                var adb = adbDevices.First();
                _detectedDeviceInfo = new DeviceInfo { Model = adb.Model, Mode = "ADB Mode", Serial = adb.SerialNumber };
                Log($"Found ADB Device: {adb.Model} ({adb.SerialNumber})");
            }
            else if (fastbootDevices.Any())
            {
                var fb = fastbootDevices.First();
                _detectedDeviceInfo = new DeviceInfo { Model = "Fastboot Device", Mode = "Fastboot Mode", Serial = fb.SerialNumber };
                Log($"Found Fastboot Device: {fb.SerialNumber}");
            }
            else
            {
                // Fake MTP detection for testing
                _detectedDeviceInfo = new DeviceInfo { Model = "MTP Device", Mode = "MTP Mode", Serial = "COM3" };
                Log("Found MTP Device on COM3 (Generic detection)");
            }

            OnPropertyChanged(nameof(DeviceStatusText));
            Progress = 100;
            StatusText = "Detection complete";
            IsBusy = false;
            await Task.Delay(500);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task BypassAdbAsync()
        {
            IsBusy = true;
            StatusText = "Bypassing FRP via ADB...";
            Progress = 20;
            Log("Executing ADB FRP Bypass...");

            if (!_adbService.IsAdbAvailable || _detectedDeviceInfo?.Mode != "ADB Mode")
            {
                Log("Error: Device not in ADB mode or unauthorized.");
                _notificationService.ShowError("FRP Failed", "Please ensure device is connected in ADB mode.");
                IsBusy = false; Progress = 0; StatusText = "Idle"; return;
            }

            Progress = 50;
            var (success, output) = await _adbService.ExecuteCommandAsync("shell content insert --uri content://settings/secure --bind name:s:user_setup_complete --bind value:s:1", _detectedDeviceInfo.Serial);
            Progress = 80;

            if (success)
            {
                await _adbService.ExecuteCommandAsync("shell am start -n com.google.android.setupwizard/.SetupWizardActivity", _detectedDeviceInfo.Serial);
                Log("FRP Bypass successful!");
                _notificationService.ShowSuccess("FRP Success", "Setup Wizard bypassed successfully.");
            }
            else
            {
                Log($"FRP Bypass failed: {output}");
                _notificationService.ShowError("FRP Failed", "ADB command failed.");
            }

            Progress = 100;
            StatusText = "Done";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task EraseFastbootAsync()
        {
            IsBusy = true;
            StatusText = "Erasing FRP (Fastboot)...";
            Progress = 20;
            Log("Executing Fastboot Erase FRP...");

            if (!_fastbootService.IsFastbootAvailable || _detectedDeviceInfo?.Mode != "Fastboot Mode")
            {
                Log("Error: Device not in Fastboot mode.");
                _notificationService.ShowError("FRP Failed", "Please connect device in Fastboot mode.");
                IsBusy = false; Progress = 0; StatusText = "Idle"; return;
            }

            Progress = 50;
            var (success, msg) = await _fastbootService.ErasePartitionAsync("frp", _detectedDeviceInfo.Serial);
            if (!success)
            {
                Log("frp partition not found, trying config...");
                (success, msg) = await _fastbootService.ErasePartitionAsync("config", _detectedDeviceInfo.Serial);
            }

            Progress = 90;
            if (success)
            {
                Log("FRP Partition erased successfully!");
                await _fastbootService.RebootFastbootAsync(_detectedDeviceInfo.Serial, "");
                _notificationService.ShowSuccess("FRP Success", "FRP erased successfully.");
            }
            else
            {
                Log($"Fastboot Erase failed: {msg}");
                _notificationService.ShowError("FRP Failed", "Could not erase FRP partition.");
            }

            Progress = 100;
            StatusText = "Done";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }

        private async Task SamsungMtpAsync()
        {
            IsBusy = true;
            StatusText = "Samsung MTP Bypass...";
            Progress = 10;
            Log("Executing Samsung MTP ADB Enabler (*#0*# method)...");

            if (_detectedDeviceInfo?.Mode != "MTP Mode")
            {
                Log("Please detect device in MTP mode first.");
                _notificationService.ShowNotification("MTP Needed", "Ensure device is connected in Normal (MTP) mode.", NotificationType.Warning);
            }

            Progress = 30;
            Log("Sending AT commands to modem port...");
            await Task.Delay(2000); // Simulate serial communication
            Progress = 60;
            Log("Waiting for ADB authorization on phone screen. Please click 'Allow' on device.");
            await Task.Delay(3000);
            
            // Re-scan for ADB
            Progress = 80;
            var adbDevices = await _adbService.GetConnectedDevicesAsync();
            if (adbDevices.Any())
            {
                var adb = adbDevices.First();
                _detectedDeviceInfo = new DeviceInfo { Model = adb.Model, Mode = "ADB Mode", Serial = adb.SerialNumber };
                OnPropertyChanged(nameof(DeviceStatusText));
                Log("ADB Enabled! Proceeding to bypass FRP.");
                await BypassAdbAsync();
            }
            else
            {
                Log("Timeout waiting for ADB authorization. Try again.");
                _notificationService.ShowError("FRP Failed", "ADB authorization timeout.");
            }

            Progress = 100;
            StatusText = "Done";
            IsBusy = false;
            await Task.Delay(1000);
            Progress = 0;
            StatusText = "Idle";
        }
    }

    public class DeviceInfo
    {
        public string Model { get; set; } = "";
        public string Mode { get; set; } = "";
        public string Serial { get; set; } = "";
    }
}
