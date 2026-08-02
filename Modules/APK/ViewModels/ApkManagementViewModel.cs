using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class ApkManagementViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;
        private string _searchQuery = string.Empty;
        private bool _includeSystemApps = false;
        private bool _isLoading = false;
        private AppInfo? _selectedApp;
        private string _installProgressText = string.Empty;

        private ObservableCollection<AppInfo> _allApps = new ObservableCollection<AppInfo>();

        public ObservableCollection<AppInfo> FilteredApps { get; } = new ObservableCollection<AppInfo>();

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set
            {
                if (SetProperty(ref _targetSerialNumber, value))
                {
                    _ = LoadAppsAsync();
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplyFilter();
                }
            }
        }

        public bool IncludeSystemApps
        {
            get => _includeSystemApps;
            set
            {
                if (SetProperty(ref _includeSystemApps, value))
                {
                    _ = LoadAppsAsync();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public AppInfo? SelectedApp
        {
            get => _selectedApp;
            set => SetProperty(ref _selectedApp, value);
        }

        public string InstallProgressText
        {
            get => _installProgressText;
            set => SetProperty(ref _installProgressText, value);
        }

        public ICommand RefreshAppsCommand { get; }
        public ICommand InstallApkCommand { get; }
        public ICommand BackupApkCommand { get; }
        public ICommand UninstallAppCommand { get; }
        public ICommand EnableAppCommand { get; }
        public ICommand DisableAppCommand { get; }
        public ICommand ForceStopAppCommand { get; }
        public ICommand ClearDataCommand { get; }

        public ApkManagementViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            RefreshAppsCommand = new AsyncRelayCommand(LoadAppsAsync);
            InstallApkCommand = new AsyncRelayCommand(InstallApkAsync, () => !IsLoading);
            BackupApkCommand = new AsyncRelayCommand(BackupSelectedApkAsync, () => SelectedApp != null && !IsLoading);
            UninstallAppCommand = new AsyncRelayCommand(UninstallSelectedAppAsync, () => SelectedApp != null && !IsLoading);
            EnableAppCommand = new AsyncRelayCommand(EnableSelectedAppAsync, () => SelectedApp != null && !IsLoading);
            DisableAppCommand = new AsyncRelayCommand(DisableSelectedAppAsync, () => SelectedApp != null && !IsLoading);
            ForceStopAppCommand = new AsyncRelayCommand(ForceStopSelectedAppAsync, () => SelectedApp != null && !IsLoading);
            ClearDataCommand = new AsyncRelayCommand(ClearSelectedAppDataAsync, () => SelectedApp != null && !IsLoading);
        }

        public async Task LoadAppsAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetSerialNumber))
            {
                _allApps.Clear();
                FilteredApps.Clear();
                return;
            }

            IsLoading = true;
            try
            {
                var apps = await _adbService.GetInstalledAppsAsync(TargetSerialNumber, IncludeSystemApps);
                _allApps.Clear();
                foreach (var app in apps.OrderBy(a => a.PackageName))
                {
                    _allApps.Add(app);
                }
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            FilteredApps.Clear();
            var query = SearchQuery.Trim();

            var matches = string.IsNullOrWhiteSpace(query)
                ? _allApps
                : _allApps.Where(a => a.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var app in matches)
            {
                FilteredApps.Add(app);
            }
        }

        private async Task InstallApkAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "APK Files (*.apk)|*.apk|All Files (*.*)|*.*",
                Title = "Select APK to Install"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsLoading = true;
                InstallProgressText = "Installing...";

                var progress = new Progress<string>(s => InstallProgressText = s);
                
                var (success, msg, detailedLog) = await _adbService.InstallApkAsync(
                    openFileDialog.FileName, 
                    TargetSerialNumber,
                    reinstall: true,
                    grantPermissions: true,
                    allowDowngrade: true,
                    autoUninstallOnConflict: false,
                    logProgress: progress
                );

                InstallProgressText = string.Empty;
                IsLoading = false;

                if (success)
                {
                    _notificationService.ShowSuccess("Install Success", "APK installed successfully.");
                    await LoadAppsAsync();
                }
                else
                {
                    _notificationService.ShowError("Install Failed", msg);
                }
            }
        }

        private async Task BackupSelectedApkAsync()
        {
            if (SelectedApp == null) return;

            string backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ADB Easy Installer", "Backups");
            Directory.CreateDirectory(backupFolder);

            IsLoading = true;
            var (success, msg) = await _adbService.BackupApkAsync(SelectedApp.PackageName, backupFolder, TargetSerialNumber);
            IsLoading = false;

            if (success)
            {
                _notificationService.ShowSuccess("APK Backup Success", msg);
                try { System.Diagnostics.Process.Start("explorer.exe", backupFolder); } catch { }
            }
            else
            {
                _notificationService.ShowError("Backup Failed", msg);
            }
        }

        private async Task UninstallSelectedAppAsync()
        {
            if (SelectedApp == null) return;

            IsLoading = true;
            var (success, msg) = await _adbService.UninstallApkAsync(SelectedApp.PackageName, TargetSerialNumber);
            IsLoading = false;

            if (success)
            {
                _notificationService.ShowSuccess("Uninstalled", $"Package {SelectedApp.PackageName} removed.");
                await LoadAppsAsync();
            }
            else
            {
                _notificationService.ShowError("Uninstall Failed", msg);
            }
        }

        private async Task EnableSelectedAppAsync()
        {
            if (SelectedApp == null) return;
            IsLoading = true;
            var (success, msg) = await _adbService.EnableAppAsync(SelectedApp.PackageName, TargetSerialNumber);
            IsLoading = false;
            if (success) _notificationService.ShowSuccess("Enabled", $"Package {SelectedApp.PackageName} enabled.");
            else _notificationService.ShowError("Enable Failed", msg);
        }

        private async Task DisableSelectedAppAsync()
        {
            if (SelectedApp == null) return;
            IsLoading = true;
            var (success, msg) = await _adbService.DisableAppAsync(SelectedApp.PackageName, TargetSerialNumber);
            IsLoading = false;
            if (success) _notificationService.ShowSuccess("Disabled", $"Package {SelectedApp.PackageName} disabled.");
            else _notificationService.ShowError("Disable Failed", msg);
        }

        private async Task ForceStopSelectedAppAsync()
        {
            if (SelectedApp == null) return;
            IsLoading = true;
            var (success, msg) = await _adbService.ForceStopAppAsync(SelectedApp.PackageName, TargetSerialNumber);
            IsLoading = false;
            if (success) _notificationService.ShowSuccess("Force Stopped", $"Package {SelectedApp.PackageName} stopped.");
            else _notificationService.ShowError("Force Stop Failed", msg);
        }

        private async Task ClearSelectedAppDataAsync()
        {
            if (SelectedApp == null) return;
            IsLoading = true;
            var (success, msg) = await _adbService.ClearAppDataAsync(SelectedApp.PackageName, TargetSerialNumber);
            IsLoading = false;
            if (success) _notificationService.ShowSuccess("Data Cleared", $"Package {SelectedApp.PackageName} data cleared.");
            else _notificationService.ShowError("Clear Data Failed", msg);
        }
    }
}

