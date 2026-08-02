using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class DeviceMonitorViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly INotificationService _notificationService;
        private readonly DispatcherTimer _refreshTimer;
        
        private string? _targetSerialNumber;
        private SystemStats? _stats;
        private StorageInfo? _storage;
        private bool _isAutoRefreshEnabled;

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set
            {
                if (SetProperty(ref _targetSerialNumber, value))
                {
                    _ = RefreshAllAsync();
                }
            }
        }

        public SystemStats? Stats
        {
            get => _stats;
            set => SetProperty(ref _stats, value);
        }

        public StorageInfo? Storage
        {
            get => _storage;
            set => SetProperty(ref _storage, value);
        }

        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                if (SetProperty(ref _isAutoRefreshEnabled, value))
                {
                    if (value) _refreshTimer.Start();
                    else _refreshTimer.Stop();
                }
            }
        }

        public ICommand RefreshCommand { get; }

        public DeviceMonitorViewModel(IAdbService adbService, INotificationService notificationService)
        {
            _adbService = adbService;
            _notificationService = notificationService;
            RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
            
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += async (s, e) => await RefreshAllAsync();
        }

        public async Task RefreshAllAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetSerialNumber))
            {
                Stats = null;
                Storage = null;
                return;
            }

            try
            {
                Stats = await _adbService.GetSystemStatsAsync(TargetSerialNumber);
                Storage = await _adbService.GetStorageInfoAsync(TargetSerialNumber);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Monitor Error", $"Failed to fetch stats: {ex.Message}");
                IsAutoRefreshEnabled = false;
            }
        }
    }
}
