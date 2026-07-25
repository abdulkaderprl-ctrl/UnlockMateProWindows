using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using AdbEasyInstaller.Models;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IAdbService _adbService;
        private readonly IScrcpyService _scrcpyService;
        private readonly IToolDownloaderService _toolDownloader;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string _customAdbPath = string.Empty;
        private string _customScrcpyPath = string.Empty;
        private bool _autoDetectAdb = true;
        private bool _isPortableMode = false;
        private string _selectedTheme = "Dark";
        private string _selectedLanguage = "English";
        private bool _autoCheckUpdates = true;
        private bool _reinstallByDefault = true;
        private bool _grantPermissionsByDefault = true;
        private bool _allowDowngrade = false;

        private bool _isDownloading = false;
        private double _downloadProgress = 0;
        private string _downloadStatusText = string.Empty;

        public string CustomAdbPath
        {
            get => _customAdbPath;
            set => SetProperty(ref _customAdbPath, value);
        }

        public string CustomScrcpyPath
        {
            get => _customScrcpyPath;
            set => SetProperty(ref _customScrcpyPath, value);
        }

        public bool AutoDetectAdb
        {
            get => _autoDetectAdb;
            set => SetProperty(ref _autoDetectAdb, value);
        }

        public bool IsPortableMode
        {
            get => _isPortableMode;
            set => SetProperty(ref _isPortableMode, value);
        }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value))
                {
                    MainViewModel.ApplyTheme(value);
                }
            }
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value);
        }

        public bool AutoCheckUpdates
        {
            get => _autoCheckUpdates;
            set => SetProperty(ref _autoCheckUpdates, value);
        }

        public bool ReinstallByDefault
        {
            get => _reinstallByDefault;
            set => SetProperty(ref _reinstallByDefault, value);
        }

        public bool GrantPermissionsByDefault
        {
            get => _grantPermissionsByDefault;
            set => SetProperty(ref _grantPermissionsByDefault, value);
        }

        public bool AllowDowngrade
        {
            get => _allowDowngrade;
            set => SetProperty(ref _allowDowngrade, value);
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        public string DownloadStatusText
        {
            get => _downloadStatusText;
            set => SetProperty(ref _downloadStatusText, value);
        }

        public ICommand BrowseAdbPathCommand { get; }
        public ICommand BrowseScrcpyPathCommand { get; }
        public ICommand AutoDetectSdkCommand { get; }
        public ICommand DownloadAdbCommand { get; }
        public ICommand DownloadScrcpyCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel(
            ISettingsService settingsService,
            IAdbService adbService,
            IScrcpyService scrcpyService,
            IToolDownloaderService toolDownloader,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _settingsService = settingsService;
            _adbService = adbService;
            _scrcpyService = scrcpyService;
            _toolDownloader = toolDownloader;
            _logger = logger;
            _notificationService = notificationService;

            LoadSettingsFromService();

            BrowseAdbPathCommand = new RelayCommand(BrowseAdbPath);
            BrowseScrcpyPathCommand = new RelayCommand(BrowseScrcpyPath);
            AutoDetectSdkCommand = new AsyncRelayCommand(AutoDetectSdkAsync);
            DownloadAdbCommand = new AsyncRelayCommand(DownloadAdbAsync, () => !IsDownloading);
            DownloadScrcpyCommand = new AsyncRelayCommand(DownloadScrcpyAsync, () => !IsDownloading);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        }

        private void LoadSettingsFromService()
        {
            var s = _settingsService.Settings;
            CustomAdbPath = s.CustomAdbPath;
            CustomScrcpyPath = s.CustomScrcpyPath;
            AutoDetectAdb = s.AutoDetectAdb;
            IsPortableMode = s.IsPortableMode;
            SelectedTheme = s.Theme;
            SelectedLanguage = s.Language;
            AutoCheckUpdates = s.AutoCheckUpdates;
            ReinstallByDefault = s.ReinstallByDefault;
            GrantPermissionsByDefault = s.GrantPermissionsByDefault;
            AllowDowngrade = s.AllowDowngrade;
        }

        private void BrowseAdbPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "adb.exe (adb.exe)|adb.exe|Executable Files (*.exe)|*.exe",
                Title = "Locate adb.exe Executable"
            };

            if (dialog.ShowDialog() == true) CustomAdbPath = dialog.FileName;
        }

        private void BrowseScrcpyPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "scrcpy.exe (scrcpy.exe)|scrcpy.exe|Executable Files (*.exe)|*.exe",
                Title = "Locate scrcpy.exe Executable"
            };

            if (dialog.ShowDialog() == true) CustomScrcpyPath = dialog.FileName;
        }

        private async Task AutoDetectSdkAsync()
        {
            bool found = await _adbService.DetectAndSetAdbPathAsync();
            await _scrcpyService.DetectAndSetScrcpyPathAsync();

            if (found)
            {
                CustomAdbPath = _adbService.AdbExecutablePath;
                _notificationService.ShowSuccess("SDK Auto-Detect", "Found adb.exe successfully.");
            }
            else
            {
                _notificationService.ShowError("SDK Auto-Detect", "Could not locate Android SDK or platform-tools.");
            }
        }

        private async Task DownloadAdbAsync()
        {
            IsDownloading = true;
            DownloadStatusText = "Downloading Android Platform Tools...";

            var progressReporter = new Progress<double>(val => DownloadProgress = val);
            bool success = await _toolDownloader.DownloadPlatformToolsAsync(progressReporter);

            IsDownloading = false;
            if (success)
            {
                await _adbService.DetectAndSetAdbPathAsync();
                CustomAdbPath = _adbService.AdbExecutablePath;
                _notificationService.ShowSuccess("ADB Download", "Downloaded and installed platform-tools successfully!");
            }
            else
            {
                _notificationService.ShowError("ADB Download Failed", "Could not download platform-tools from Google servers.");
            }
        }

        private async Task DownloadScrcpyAsync()
        {
            IsDownloading = true;
            DownloadStatusText = "Downloading Scrcpy Release...";

            var progressReporter = new Progress<double>(val => DownloadProgress = val);
            bool success = await _toolDownloader.DownloadScrcpyAsync(progressReporter);

            IsDownloading = false;
            if (success)
            {
                await _scrcpyService.DetectAndSetScrcpyPathAsync();
                CustomScrcpyPath = _scrcpyService.ScrcpyExecutablePath;
                _notificationService.ShowSuccess("Scrcpy Download", "Downloaded and installed scrcpy successfully!");
            }
            else
            {
                _notificationService.ShowError("Scrcpy Download Failed", "Could not download scrcpy release.");
            }
        }

        private async Task SaveSettingsAsync()
        {
            var newSettings = new AppSettings
            {
                CustomAdbPath = CustomAdbPath,
                CustomScrcpyPath = CustomScrcpyPath,
                AutoDetectAdb = AutoDetectAdb,
                IsPortableMode = IsPortableMode,
                Theme = SelectedTheme,
                Language = SelectedLanguage,
                AutoCheckUpdates = AutoCheckUpdates,
                ReinstallByDefault = ReinstallByDefault,
                GrantPermissionsByDefault = GrantPermissionsByDefault,
                AllowDowngrade = AllowDowngrade
            };

            _settingsService.UpdateSettings(newSettings);
            await _adbService.DetectAndSetAdbPathAsync(CustomAdbPath);
            await _scrcpyService.DetectAndSetScrcpyPathAsync(CustomScrcpyPath);

            _notificationService.ShowSuccess("Settings Saved", "Your application preferences have been updated.");
        }
    }
}
