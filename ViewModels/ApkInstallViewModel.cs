using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using AdbEasyInstaller.Models;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class ApkInstallViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;
        private readonly ISettingsService _settingsService;

        private string? _targetSerialNumber;
        private ApkInfo? _selectedApk;
        private bool _isReinstall = true;
        private bool _grantPermissions = true;
        private bool _allowDowngrade = false;
        private bool _autoUninstallOnConflict = true;

        private bool _isInstalling = false;
        private double _overallProgress = 0;
        private string _statusMessage = "Ready to install APK or Split archives.";
        private string _detailedLog = string.Empty;

        public ObservableCollection<ApkInfo> ApkQueue { get; } = new ObservableCollection<ApkInfo>();

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set => SetProperty(ref _targetSerialNumber, value);
        }

        public ApkInfo? SelectedApk
        {
            get => _selectedApk;
            set => SetProperty(ref _selectedApk, value);
        }

        public bool IsReinstall
        {
            get => _isReinstall;
            set => SetProperty(ref _isReinstall, value);
        }

        public bool GrantPermissions
        {
            get => _grantPermissions;
            set => SetProperty(ref _grantPermissions, value);
        }

        public bool AllowDowngrade
        {
            get => _allowDowngrade;
            set => SetProperty(ref _allowDowngrade, value);
        }

        public bool AutoUninstallOnConflict
        {
            get => _autoUninstallOnConflict;
            set => SetProperty(ref _autoUninstallOnConflict, value);
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            set => SetProperty(ref _isInstalling, value);
        }

        public double OverallProgress
        {
            get => _overallProgress;
            set => SetProperty(ref _overallProgress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string DetailedLog
        {
            get => _detailedLog;
            set => SetProperty(ref _detailedLog, value);
        }

        public ICommand BrowseApkCommand { get; }
        public ICommand ClearQueueCommand { get; }
        public ICommand InstallSingleCommand { get; }
        public ICommand InstallAllCommand { get; }
        public ICommand UninstallPackageCommand { get; }

        public ApkInstallViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService,
            ISettingsService settingsService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;
            _settingsService = settingsService;

            BrowseApkCommand = new AsyncRelayCommand(BrowseApkAsync);
            ClearQueueCommand = new RelayCommand(() => { ApkQueue.Clear(); StatusMessage = "Queue cleared."; DetailedLog = string.Empty; });
            InstallSingleCommand = new AsyncRelayCommand(InstallSelectedAsync, () => SelectedApk != null && !IsInstalling);
            InstallAllCommand = new AsyncRelayCommand(InstallAllAsync, () => ApkQueue.Any() && !IsInstalling);
            UninstallPackageCommand = new AsyncRelayCommand(UninstallSelectedPackageAsync, () => SelectedApk != null && !IsInstalling);
        }

        public async Task AddApkFilesAsync(string[] paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".apk" || ext == ".xapk" || ext == ".apks" || ext == ".apkm")
                    {
                        var info = await _adbService.GetApkInfoAsync(path);
                        ApkQueue.Add(info);
                    }
                }
            }

            if (SelectedApk == null && ApkQueue.Any())
            {
                SelectedApk = ApkQueue.First();
            }

            StatusMessage = $"Added {paths.Length} package(s) to queue.";
        }

        private async Task BrowseApkAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Android Packages (*.apk;*.xapk;*.apks;*.apkm)|*.apk;*.xapk;*.apks;*.apkm|All Files (*.*)|*.*",
                Multiselect = true,
                Title = "Select APK Files or Split Archives"
            };

            if (dialog.ShowDialog() == true)
            {
                await AddApkFilesAsync(dialog.FileNames);
            }
        }

        private async Task InstallSelectedAsync()
        {
            if (SelectedApk == null) return;
            await InstallPackageInternalAsync(SelectedApk);
        }

        private async Task InstallAllAsync()
        {
            if (!ApkQueue.Any()) return;

            IsInstalling = true;
            int total = ApkQueue.Count;
            int current = 0;

            foreach (var apk in ApkQueue.ToList())
            {
                current++;
                StatusMessage = $"Installing package [{current}/{total}]: {apk.FileName}...";
                await InstallPackageInternalAsync(apk);
                OverallProgress = (double)current / total * 100;
            }

            IsInstalling = false;
            _notificationService.ShowSuccess("Batch Installation Complete", $"Finished installing {total} package(s).");
        }

        private async Task InstallPackageInternalAsync(ApkInfo apk)
        {
            IsInstalling = true;
            apk.Status = "Installing";
            StatusMessage = $"Installing {apk.FileName}...";

            var progressReporter = new Progress<double>(val => apk.Progress = val);
            var logReporter = new Progress<string>(line => DetailedLog += line + Environment.NewLine);

            var (success, message, detailedLog) = await _adbService.InstallApkAsync(
                apk.FilePath,
                TargetSerialNumber,
                IsReinstall,
                GrantPermissions,
                AllowDowngrade,
                AutoUninstallOnConflict,
                progressReporter,
                logReporter);

            IsInstalling = false;
            apk.ErrorMessage = message;
            apk.DetailedLog = detailedLog;

            if (success)
            {
                apk.Status = "Success";
                apk.IsVerifiedInstalled = true;
                StatusMessage = $"Success: {apk.FileName}";
                _notificationService.ShowSuccess("Installation Success", $"{apk.FileName} installed and verified!");
            }
            else
            {
                apk.Status = "Error";
                StatusMessage = $"Failed: {apk.FileName}";
                _notificationService.ShowError("Installation Failed", message);
            }
        }

        private async Task UninstallSelectedPackageAsync()
        {
            if (SelectedApk == null) return;

            IsInstalling = true;
            StatusMessage = $"Uninstalling {SelectedApk.PackageName}...";

            var (success, message) = await _adbService.UninstallApkAsync(SelectedApk.PackageName, TargetSerialNumber);
            IsInstalling = false;

            if (success)
            {
                SelectedApk.Status = "Uninstalled";
                _notificationService.ShowSuccess("Uninstall Complete", message);
            }
            else
            {
                _notificationService.ShowError("Uninstall Failed", message);
            }
        }
    }
}
