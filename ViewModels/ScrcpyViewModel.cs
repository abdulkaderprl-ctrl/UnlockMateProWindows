using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using AdbEasyInstaller.Models;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class ScrcpyViewModel : ViewModelBase
    {
        private readonly IScrcpyService _scrcpyService;
        private readonly ISettingsService _settingsService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;
        private bool _isControlEnabled = true;
        private bool _isStayAwake = true;
        private bool _isTurnScreenOff = false;
        private bool _isShowTouches = false;
        private int _maxFps = 60;
        private string _bitrate = "8M";
        private bool _isRecording = false;
        private string _statusText = "Ready to mirror device screen";

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set => SetProperty(ref _targetSerialNumber, value);
        }

        public bool IsControlEnabled
        {
            get => _isControlEnabled;
            set
            {
                if (SetProperty(ref _isControlEnabled, value))
                {
                    _settingsService.Settings.ScrcpyControlEnabled = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public bool IsStayAwake
        {
            get => _isStayAwake;
            set
            {
                if (SetProperty(ref _isStayAwake, value))
                {
                    _settingsService.Settings.ScrcpyStayAwake = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public bool IsTurnScreenOff
        {
            get => _isTurnScreenOff;
            set
            {
                if (SetProperty(ref _isTurnScreenOff, value))
                {
                    _settingsService.Settings.ScrcpyTurnScreenOff = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public bool IsShowTouches
        {
            get => _isShowTouches;
            set
            {
                if (SetProperty(ref _isShowTouches, value))
                {
                    _settingsService.Settings.ScrcpyShowTouches = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public int MaxFps
        {
            get => _maxFps;
            set
            {
                if (SetProperty(ref _maxFps, value))
                {
                    _settingsService.Settings.ScrcpyMaxFps = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public string Bitrate
        {
            get => _bitrate;
            set => SetProperty(ref _bitrate, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            set => SetProperty(ref _isRecording, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ICommand StartMirroringCommand { get; }
        public ICommand StopMirroringCommand { get; }
        public ICommand StartRecordingCommand { get; }

        public ScrcpyViewModel(
            IScrcpyService scrcpyService,
            ISettingsService settingsService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _scrcpyService = scrcpyService;
            _settingsService = settingsService;
            _logger = logger;
            _notificationService = notificationService;

            LoadSettings();

            StartMirroringCommand = new AsyncRelayCommand(StartMirroringAsync);
            StopMirroringCommand = new RelayCommand(StopMirroring);
            StartRecordingCommand = new AsyncRelayCommand(StartRecordingAsync);
        }

        private void LoadSettings()
        {
            var s = _settingsService.Settings;
            IsControlEnabled = s.ScrcpyControlEnabled;
            IsStayAwake = s.ScrcpyStayAwake;
            IsTurnScreenOff = s.ScrcpyTurnScreenOff;
            IsShowTouches = s.ScrcpyShowTouches;
            MaxFps = s.ScrcpyMaxFps;
            Bitrate = s.ScrcpyBitrateMbps;
        }

        private async Task StartMirroringAsync()
        {
            await _scrcpyService.DetectAndSetScrcpyPathAsync(_settingsService.Settings.CustomScrcpyPath);

            if (!_scrcpyService.IsScrcpyAvailable)
            {
                _notificationService.ShowError("Scrcpy Missing", "scrcpy.exe executable was not found. Please click Download Scrcpy in Settings.");
                StatusText = "scrcpy.exe missing";
                return;
            }

            StatusText = "Launching screen mirroring session...";
            var (success, msg) = await _scrcpyService.LaunchMirroringAsync(TargetSerialNumber, _settingsService.Settings);

            if (success)
            {
                StatusText = "Screen Mirroring Active";
                _notificationService.ShowSuccess("Screen Mirroring", "Live display mirror active with remote control!");
            }
            else
            {
                StatusText = $"Mirroring failed: {msg}";
                _notificationService.ShowError("Mirroring Error", msg);
            }
        }

        private void StopMirroring()
        {
            _scrcpyService.StopMirroring();
            StatusText = "Screen mirroring stopped.";
            _notificationService.ShowSuccess("Mirroring Stopped", "Terminated scrcpy mirroring session.");
        }

        private async Task StartRecordingAsync()
        {
            await _scrcpyService.DetectAndSetScrcpyPathAsync(_settingsService.Settings.CustomScrcpyPath);

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ADB Recordings");
            Directory.CreateDirectory(folder);
            string recordFile = Path.Combine(folder, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

            StatusText = "Starting screen recording...";
            var (success, msg) = await _scrcpyService.LaunchMirroringAsync(TargetSerialNumber, _settingsService.Settings, recordFile);

            if (success)
            {
                StatusText = $"Recording to: {Path.GetFileName(recordFile)}";
                _notificationService.ShowSuccess("Screen Recording Started", $"Saving MP4 video to {recordFile}");
            }
            else
            {
                _notificationService.ShowError("Recording Error", msg);
            }
        }
    }
}
