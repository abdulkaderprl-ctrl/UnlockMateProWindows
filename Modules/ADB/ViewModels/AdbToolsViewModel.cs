using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class AdbToolsViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;

        // Sub-modules
        public DeviceInfoViewModel DeviceInfoVM { get; }
        public DeviceMonitorViewModel DeviceMonitorVM { get; }
        public ApkManagementViewModel ApkManagementVM { get; }
        public FileExplorerViewModel FileExplorerVM { get; }
        public TerminalViewModel TerminalVM { get; }
        public LogcatViewModel LogcatVM { get; }

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set
            {
                if (SetProperty(ref _targetSerialNumber, value))
                {
                    DeviceInfoVM.TargetSerialNumber = value;
                    DeviceMonitorVM.TargetSerialNumber = value;
                    ApkManagementVM.TargetSerialNumber = value;
                    FileExplorerVM.TargetSerialNumber = value;
                    LogcatVM.TargetSerialNumber = value;
                    // TerminalVM handles its own global commands usually, or we can pass it if it needs it.
                }
            }
        }

        // Reboot & Advanced
        public ICommand RebootNormalCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootBootloaderCommand { get; }
        public ICommand RebootEdlCommand { get; }
        public ICommand TakeScreenshotCommand { get; }
        
        // Wireless ADB
        private string _ipAddress = "192.168.1.100";
        private int _port = 5555;
        
        public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }
        public int Port { get => _port; set => SetProperty(ref _port, value); }
        public ICommand EnableWirelessAdbCommand { get; }
        public ICommand ConnectWirelessAdbCommand { get; }

        public AdbToolsViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService,
            ApkManagementViewModel apkManagementVM,
            FileExplorerViewModel fileExplorerVM,
            TerminalViewModel terminalVM)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            // Initialize Sub-VMs
            DeviceInfoVM = new DeviceInfoViewModel(_adbService, _notificationService);
            DeviceMonitorVM = new DeviceMonitorViewModel(_adbService, _notificationService);
            ApkManagementVM = apkManagementVM;
            FileExplorerVM = fileExplorerVM;
            TerminalVM = terminalVM;
            LogcatVM = new LogcatViewModel(_adbService, _notificationService);

            EnableWirelessAdbCommand = new AsyncRelayCommand(EnableWirelessAdbAsync);
            ConnectWirelessAdbCommand = new AsyncRelayCommand(ConnectWirelessAdbAsync);
            RebootNormalCommand = new AsyncRelayCommand(() => RebootAsync(""));
            RebootRecoveryCommand = new AsyncRelayCommand(() => RebootAsync("recovery"));
            RebootBootloaderCommand = new AsyncRelayCommand(() => RebootAsync("bootloader"));
            RebootEdlCommand = new AsyncRelayCommand(() => RebootAsync("edl"));
            TakeScreenshotCommand = new AsyncRelayCommand(TakeScreenshotAsync);
        }

        private async Task EnableWirelessAdbAsync()
        {
            var (success, msg) = await _adbService.EnableWirelessAdbAsync(TargetSerialNumber, Port);
            if (success) _notificationService.ShowSuccess("Wireless ADB", msg);
            else _notificationService.ShowError("Wireless ADB Failed", msg);
        }

        private async Task ConnectWirelessAdbAsync()
        {
            var (success, msg) = await _adbService.ConnectWirelessDeviceAsync(IpAddress, Port);
            if (success) _notificationService.ShowSuccess("Connected", msg);
            else _notificationService.ShowError("Connection Failed", msg);
        }

        private async Task RebootAsync(string mode)
        {
            string modeName = string.IsNullOrWhiteSpace(mode) ? "Normal" : mode;
            if (mode == "edl") 
            {
                var (s, m) = await _adbService.RebootEdlAsync(TargetSerialNumber);
                if (s) _notificationService.ShowSuccess("Rebooting EDL", m);
                else _notificationService.ShowError("Reboot EDL Error", m);
                return;
            }

            var (success, msg) = await _adbService.RebootDeviceAsync(TargetSerialNumber, mode);
            if (success) _notificationService.ShowSuccess("Rebooting", $"Device reboot signal sent ({modeName}).");
            else _notificationService.ShowError("Reboot Error", msg);
        }

        private async Task TakeScreenshotAsync()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ADB Screenshots");
            Directory.CreateDirectory(folder);

            var (success, filePath) = await _adbService.TakeScreenshotAsync(TargetSerialNumber, folder);
            if (success)
            {
                _notificationService.ShowSuccess("Screenshot Saved", $"Saved to: {filePath}");
                try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\""); } catch { }
            }
            else _notificationService.ShowError("Screenshot Error", filePath);
        }
    }
}
