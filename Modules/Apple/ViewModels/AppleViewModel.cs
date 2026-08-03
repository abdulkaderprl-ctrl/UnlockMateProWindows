using System;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class AppleViewModel : ViewModelBase
    {
        private readonly IAppleService _appleService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string _logText = "";
        private bool _isBusy = false;
        private int _progress = 0;
        private string _statusText = "Idle";
        private DeviceInfo? _detectedDeviceInfo;
        private string _ipswPath = "";

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

        public string IpswPath
        {
            get => _ipswPath;
            set => SetProperty(ref _ipswPath, value);
        }

        public ICommand DetectDeviceCommand { get; }
        public ICommand ReadInfoCommand { get; }
        public ICommand EnterRecoveryCommand { get; }
        public ICommand ExitRecoveryCommand { get; }
        public ICommand RebootDeviceCommand { get; }
        public ICommand BrowseIpswCommand { get; }
        public ICommand FlashIpswCommand { get; }
        public ICommand RestoreFirmwareCommand { get; }
        public ICommand CheckActivationCommand { get; }
        public ICommand CheckFindMyIphoneCommand { get; }

        public AppleViewModel(IAppleService appleService, ILoggerService logger, INotificationService notificationService)
        {
            _appleService = appleService;
            _logger = logger;
            _notificationService = notificationService;

            DetectDeviceCommand = new AsyncRelayCommand(DetectDeviceAsync, () => !IsBusy);
            ReadInfoCommand = new AsyncRelayCommand(ReadInfoAsync, () => !IsBusy);
            EnterRecoveryCommand = new AsyncRelayCommand(EnterRecoveryAsync, () => !IsBusy);
            ExitRecoveryCommand = new AsyncRelayCommand(ExitRecoveryAsync, () => !IsBusy);
            RebootDeviceCommand = new AsyncRelayCommand(RebootDeviceAsync, () => !IsBusy);
            BrowseIpswCommand = new RelayCommand(BrowseIpsw);
            FlashIpswCommand = new AsyncRelayCommand(FlashIpswAsync, () => !IsBusy);
            RestoreFirmwareCommand = new AsyncRelayCommand(RestoreFirmwareAsync, () => !IsBusy);
            CheckActivationCommand = new AsyncRelayCommand(CheckActivationAsync, () => !IsBusy);
            CheckFindMyIphoneCommand = new AsyncRelayCommand(CheckFindMyIphoneAsync, () => !IsBusy);

            Log("Apple Professional Module Initialized.");
        }

        private void BrowseIpsw()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "IPSW Firmware (*.ipsw)|*.ipsw|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                IpswPath = dlg.FileName;
            }
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{time}] {message}\n";
            _logger.LogInfo($"[APPLE] {message}");
        }

        private async Task DetectDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Detecting Apple device...";
            Progress = 30;
            Log("Scanning for Apple devices (Normal/Recovery/DFU)...");

            _detectedDeviceInfo = await _appleService.DetectDeviceAsync();

            if (_detectedDeviceInfo != null)
            {
                Log($"Found: {_detectedDeviceInfo.Model} | Mode: {_detectedDeviceInfo.Mode} | ID: {_detectedDeviceInfo.Serial}");
                _notificationService.ShowNotification("Device Detected", $"Apple device found in {_detectedDeviceInfo.Mode}", NotificationType.Success);
            }
            else
            {
                Log("No Apple device detected. Please check connection and drivers.");
                _notificationService.ShowNotification("Not Found", "No Apple device detected.", NotificationType.Warning);
            }

            OnPropertyChanged(nameof(DeviceStatusText));
            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task ReadInfoAsync()
        {
            IsBusy = true;
            StatusText = "Reading Device Info...";
            Progress = 50;
            Log("Reading detailed Apple device information...");

            string info = await _appleService.ReadInfoAsync();
            Log("\n" + info);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task EnterRecoveryAsync()
        {
            IsBusy = true;
            StatusText = "Entering Recovery Mode...";
            Progress = 50;
            Log("Sending device to Recovery Mode...");

            string result = await _appleService.EnterRecoveryModeAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
            await DetectDeviceAsync();
        }

        private async Task ExitRecoveryAsync()
        {
            IsBusy = true;
            StatusText = "Exiting Recovery Mode...";
            Progress = 50;
            Log("Attempting to exit Recovery Mode...");

            string result = await _appleService.ExitRecoveryModeAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
            await DetectDeviceAsync();
        }

        private async Task RebootDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting Device...";
            Progress = 50;
            Log("Rebooting device...");

            string result = await _appleService.RebootDeviceAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task FlashIpswAsync()
        {
            if (string.IsNullOrEmpty(IpswPath))
            {
                Log("Please select an IPSW file first.");
                return;
            }

            IsBusy = true;
            StatusText = "Flashing IPSW...";
            Progress = 20;
            Log($"Starting Flash with IPSW: {IpswPath}");
            Log("WARNING: Do not disconnect device!");

            // In a real app, this would stream output to update progress.
            string result = await _appleService.FlashIpswAsync(IpswPath);
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task RestoreFirmwareAsync()
        {
            if (string.IsNullOrEmpty(IpswPath))
            {
                Log("Please select an IPSW file first.");
                return;
            }

            IsBusy = true;
            StatusText = "Restoring Firmware...";
            Progress = 20;
            Log($"Starting Restore (Erase Data) with IPSW: {IpswPath}");
            Log("WARNING: Do not disconnect device! All data will be erased.");

            string result = await _appleService.RestoreFirmwareAsync(IpswPath);
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task CheckActivationAsync()
        {
            IsBusy = true;
            StatusText = "Checking Activation...";
            Progress = 50;
            Log("Checking device activation status...");

            string result = await _appleService.CheckActivationStatusAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task CheckFindMyIphoneAsync()
        {
            IsBusy = true;
            StatusText = "Checking FMI...";
            Progress = 50;
            Log("Checking Find My iPhone status...");

            string result = await _appleService.CheckFindMyIphoneAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }
    }
}
