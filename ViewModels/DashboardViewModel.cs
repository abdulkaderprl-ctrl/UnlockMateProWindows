using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;

        private string _adbStatus = "Checking ADB...";
        private string _deviceStatus = "No Device Connected";
        private string _androidVersion = "--";
        private string _deviceModel = "--";
        private string _batteryLevelText = "--%";
        private int _batteryLevel = 0;
        private bool _isDeviceConnected = false;
        private bool _isCharging = false;

        public string AdbStatus
        {
            get => _adbStatus;
            set => SetProperty(ref _adbStatus, value);
        }

        public string DeviceStatus
        {
            get => _deviceStatus;
            set => SetProperty(ref _deviceStatus, value);
        }

        public string AndroidVersion
        {
            get => _androidVersion;
            set => SetProperty(ref _androidVersion, value);
        }

        public string DeviceModel
        {
            get => _deviceModel;
            set => SetProperty(ref _deviceModel, value);
        }

        public string BatteryLevelText
        {
            get => _batteryLevelText;
            set => SetProperty(ref _batteryLevelText, value);
        }

        public int BatteryLevel
        {
            get => _batteryLevel;
            set => SetProperty(ref _batteryLevel, value);
        }

        public bool IsDeviceConnected
        {
            get => _isDeviceConnected;
            set => SetProperty(ref _isDeviceConnected, value);
        }

        public bool IsCharging
        {
            get => _isCharging;
            set => SetProperty(ref _isCharging, value);
        }

        public DashboardViewModel(IAdbService adbService, ILoggerService logger)
        {
            _adbService = adbService;
            _logger = logger;
        }

        public void UpdateDevice(AdbDevice? device, bool isAdbReady)
        {
            AdbStatus = isAdbReady ? "ADB Active & Connected" : "ADB Executable Missing";

            if (device != null && device.IsConnected)
            {
                IsDeviceConnected = true;
                DeviceStatus = $"Connected ({device.SerialNumber})";
                DeviceModel = string.IsNullOrWhiteSpace(device.Model) ? "Android Device" : device.Model;
                AndroidVersion = !string.IsNullOrWhiteSpace(device.AndroidVersion) 
                    ? $"Android {device.AndroidVersion} (API {device.ApiLevel})" 
                    : "Android OS";

                if (device.BatteryLevel >= 0)
                {
                    BatteryLevel = device.BatteryLevel;
                    BatteryLevelText = $"{device.BatteryLevel}%{(device.IsCharging ? " ⚡ Charging" : "")}";
                    IsCharging = device.IsCharging;
                }
                else
                {
                    BatteryLevel = 0;
                    BatteryLevelText = "N/A";
                    IsCharging = false;
                }
            }
            else if (device != null)
            {
                IsDeviceConnected = false;
                DeviceStatus = $"Device State: {device.DeviceState.ToUpper()}";
                DeviceModel = device.SerialNumber;
                AndroidVersion = "--";
                BatteryLevelText = "--%";
                BatteryLevel = 0;
                IsCharging = false;
            }
            else
            {
                IsDeviceConnected = false;
                DeviceStatus = "No Device Connected";
                DeviceModel = "--";
                AndroidVersion = "--";
                BatteryLevelText = "--%";
                BatteryLevel = 0;
                IsCharging = false;
            }
        }
    }
}

