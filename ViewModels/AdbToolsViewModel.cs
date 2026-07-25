using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class AdbToolsViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;
        private string _ipAddress = "192.168.1.100";
        private int _port = 5555;
        private bool _isProcessing = false;
        private string _logcatOutput = string.Empty;
        private bool _isLogcatStreaming = false;

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set => SetProperty(ref _targetSerialNumber, value);
        }

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public string LogcatOutput
        {
            get => _logcatOutput;
            set => SetProperty(ref _logcatOutput, value);
        }

        public bool IsLogcatStreaming
        {
            get => _isLogcatStreaming;
            set => SetProperty(ref _isLogcatStreaming, value);
        }

        public ICommand EnableWirelessAdbCommand { get; }
        public ICommand ConnectWirelessAdbCommand { get; }
        public ICommand RebootNormalCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootBootloaderCommand { get; }
        public ICommand TakeScreenshotCommand { get; }
        public ICommand OpenStorageCommand { get; }
        public ICommand FetchLogcatCommand { get; }

        public AdbToolsViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            EnableWirelessAdbCommand = new AsyncRelayCommand(EnableWirelessAdbAsync);
            ConnectWirelessAdbCommand = new AsyncRelayCommand(ConnectWirelessAdbAsync);
            RebootNormalCommand = new AsyncRelayCommand(() => RebootAsync(""));
            RebootRecoveryCommand = new AsyncRelayCommand(() => RebootAsync("recovery"));
            RebootBootloaderCommand = new AsyncRelayCommand(() => RebootAsync("bootloader"));
            TakeScreenshotCommand = new AsyncRelayCommand(TakeScreenshotAsync);
            OpenStorageCommand = new AsyncRelayCommand(OpenStorageAsync);
            FetchLogcatCommand = new AsyncRelayCommand(FetchLogcatAsync);
        }

        private async Task EnableWirelessAdbAsync()
        {
            IsProcessing = true;
            var (success, msg) = await _adbService.EnableWirelessAdbAsync(TargetSerialNumber, Port);
            IsProcessing = false;

            if (success)
            {
                _notificationService.ShowSuccess("Wireless ADB", msg);
            }
            else
            {
                _notificationService.ShowError("Wireless ADB Failed", msg);
            }
        }

        private async Task ConnectWirelessAdbAsync()
        {
            IsProcessing = true;
            var (success, msg) = await _adbService.ConnectWirelessDeviceAsync(IpAddress, Port);
            IsProcessing = false;

            if (success)
            {
                _notificationService.ShowSuccess("Connected", msg);
            }
            else
            {
                _notificationService.ShowError("Connection Failed", msg);
            }
        }

        private async Task RebootAsync(string mode)
        {
            string modeName = string.IsNullOrWhiteSpace(mode) ? "Normal" : mode;
            IsProcessing = true;
            var (success, msg) = await _adbService.RebootDeviceAsync(TargetSerialNumber, mode);
            IsProcessing = false;

            if (success)
            {
                _notificationService.ShowSuccess("Rebooting", $"Device reboot signal sent ({modeName}).");
            }
            else
            {
                _notificationService.ShowError("Reboot Error", msg);
            }
        }

        private async Task TakeScreenshotAsync()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ADB Screenshots");
            Directory.CreateDirectory(folder);

            IsProcessing = true;
            var (success, filePath) = await _adbService.TakeScreenshotAsync(TargetSerialNumber, folder);
            IsProcessing = false;

            if (success)
            {
                _notificationService.ShowSuccess("Screenshot Saved", $"Saved to: {filePath}");
                try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\""); } catch { }
            }
            else
            {
                _notificationService.ShowError("Screenshot Error", filePath);
            }
        }

        private async Task OpenStorageAsync()
        {
            await _adbService.OpenDeviceStorageAsync(TargetSerialNumber);
        }

        private async Task FetchLogcatAsync()
        {
            IsProcessing = true;
            LogcatOutput = "Fetching recent logcat entries...";

            var (success, output) = await _adbService.ExecuteCommandAsync("shell logcat -d -t 100", TargetSerialNumber);
            IsProcessing = false;

            if (success)
            {
                LogcatOutput = output;
                _notificationService.ShowSuccess("Logcat Fetched", "Retrieved last 100 log lines.");
            }
            else
            {
                LogcatOutput = $"Error fetching logcat: {output}";
                _notificationService.ShowError("Logcat Error", output);
            }
        }
    }
}
