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

        public ICommand RefreshAppsCommand { get; }
        public ICommand BackupApkCommand { get; }
        public ICommand UninstallAppCommand { get; }

        public ApkManagementViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            RefreshAppsCommand = new AsyncRelayCommand(LoadAppsAsync);
            BackupApkCommand = new AsyncRelayCommand(BackupSelectedApkAsync, () => SelectedApp != null && !IsLoading);
            UninstallAppCommand = new AsyncRelayCommand(UninstallSelectedAppAsync, () => SelectedApp != null && !IsLoading);
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
    }
}
