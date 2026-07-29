using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class DeviceDetectionViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private AdbDevice? _selectedDevice;
        private bool _isRefreshing;
        private string _adbPathText = string.Empty;
        private bool _hasDevices = false;

        public ObservableCollection<AdbDevice> Devices { get; } = new ObservableCollection<AdbDevice>();

        public AdbDevice? SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public string AdbPathText
        {
            get => _adbPathText;
            set => SetProperty(ref _adbPathText, value);
        }

        public bool HasDevices
        {
            get => _hasDevices;
            set => SetProperty(ref _hasDevices, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand AutoDetectAdbCommand { get; }

        public DeviceDetectionViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            AdbPathText = _adbService.AdbExecutablePath;

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            AutoDetectAdbCommand = new AsyncRelayCommand(AutoDetectAdbAsync);
        }

        public void UpdateDevices(ObservableCollection<AdbDevice> devices, AdbDevice? selected)
        {
            Devices.Clear();
            foreach (var d in devices) Devices.Add(d);
            SelectedDevice = selected;
            HasDevices = Devices.Count > 0;
            AdbPathText = _adbService.AdbExecutablePath;
        }

        private async Task RefreshAsync()
        {
            IsRefreshing = true;
            try
            {
                var list = await _adbService.GetConnectedDevicesAsync();
                Devices.Clear();
                foreach (var dev in list) Devices.Add(dev);
                HasDevices = Devices.Count > 0;

                _notificationService.ShowSuccess("Device Refresh", $"Found {Devices.Count} connected device(s).");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task AutoDetectAdbAsync()
        {
            bool found = await _adbService.DetectAndSetAdbPathAsync();
            AdbPathText = _adbService.AdbExecutablePath;
            if (found)
            {
                _notificationService.ShowSuccess("ADB Detection", "ADB found and initialized successfully!");
                await RefreshAsync();
            }
            else
            {
                _notificationService.ShowError("ADB Detection", "Could not locate adb.exe automatically.");
            }
        }
    }
}

