using System;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class DeviceInfoViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly INotificationService _notificationService;
        private string? _targetSerialNumber;
        private SystemStats? _stats;
        private bool _isLoading;

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set
            {
                if (SetProperty(ref _targetSerialNumber, value))
                {
                    _ = RefreshInfoAsync();
                }
            }
        }

        public SystemStats? Stats
        {
            get => _stats;
            set => SetProperty(ref _stats, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand RefreshCommand { get; }

        public DeviceInfoViewModel(IAdbService adbService, INotificationService notificationService)
        {
            _adbService = adbService;
            _notificationService = notificationService;
            RefreshCommand = new AsyncRelayCommand(RefreshInfoAsync);
        }

        public async Task RefreshInfoAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetSerialNumber))
            {
                Stats = null;
                return;
            }

            IsLoading = true;
            try
            {
                Stats = await _adbService.GetSystemStatsAsync(TargetSerialNumber);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error", $"Failed to load device info: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
