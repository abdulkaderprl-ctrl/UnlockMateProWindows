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
        public ICommand StartScreenRecordingCommand { get; }
        public ICommand StopScreenRecordingCommand { get; }
        
        private bool _isRecording = false;
        public bool IsRecording
        {
            get => _isRecording;
            set => SetProperty(ref _isRecording, value);
        }
        
        private System.Threading.CancellationTokenSource? _recordingCts;
        
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
            StartScreenRecordingCommand = new AsyncRelayCommand(StartScreenRecordingAsync, () => !IsRecording);
            StopScreenRecordingCommand = new RelayCommand(StopScreenRecording, () => IsRecording);
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

        private async Task StartScreenRecordingAsync()
        {
            IsRecording = true;
            _recordingCts = new System.Threading.CancellationTokenSource();
            _notificationService.ShowSuccess("Recording", "Screen recording started (max 3 minutes or until stopped).");

            string remotePath = $"/sdcard/screen_record_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";

            try
            {
                // This command blocks until max duration or killed
                await _adbService.ExecuteCommandAsync($"shell screenrecord {remotePath}", TargetSerialNumber, _recordingCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when user stops recording
            }
            finally
            {
                IsRecording = false;
                _notificationService.ShowSuccess("Recording Stopped", "Pulling video to PC...");

                string localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ADB Screen Records");
                Directory.CreateDirectory(localFolder);
                string localFile = Path.Combine(localFolder, Path.GetFileName(remotePath));

                var (success, msg) = await _adbService.ExecuteCommandAsync($"pull {remotePath} \"{localFile}\"", TargetSerialNumber);
                if (success || File.Exists(localFile))
                {
                    _notificationService.ShowSuccess("Recording Saved", $"Saved to: {localFile}");
                    try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{localFile}\""); } catch { }
                    await _adbService.ExecuteCommandAsync($"shell rm {remotePath}", TargetSerialNumber);
                }
                else
                {
                    _notificationService.ShowError("Pull Error", "Failed to retrieve the recording.");
                }
            }
        }

        private void StopScreenRecording()
        {
            if (_recordingCts != null && !_recordingCts.IsCancellationRequested)
            {
                _recordingCts.Cancel();
            }
        }
    }
}
