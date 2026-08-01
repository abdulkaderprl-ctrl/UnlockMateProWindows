using System;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class AdvancedToolsViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;

        // Device Diagnostics Info
        private string _brand = "Unknown";
        private string _model = "Unknown";
        private string _androidVersion = "Unknown";
        private string _sdkVersion = "Unknown";
        private string _buildNumber = "Unknown";
        private string _cpuAbi = "Unknown";
        private string _imei = "Unknown";
        private string _serialNumberDisplay = "Unknown";
        private string _batteryLevelText = "Unknown";
        private string _storageStatsText = "Unknown";
        private string _ramStatsText = "Unknown";

        // Root & Security Diagnostics
        private bool _isRooted = false;
        private string _rootStatusText = "Checking Root Access...";
        private string _magiskStatusText = "Checking Magisk...";
        private string _superSuStatusText = "Checking SuperSU...";
        private string _busyBoxStatusText = "Checking BusyBox...";
        private string _seLinuxStatusText = "Checking SELinux Enforce...";
        private string _dmVerityStatusText = "Checking dm-verity...";

        private bool _isBusy = false;

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set
            {
                if (SetProperty(ref _targetSerialNumber, value))
                {
                    _ = RefreshDiagnosticsAsync();
                }
            }
        }

        public string Brand { get => _brand; set => SetProperty(ref _brand, value); }
        public string Model { get => _model; set => SetProperty(ref _model, value); }
        public string AndroidVersion { get => _androidVersion; set => SetProperty(ref _androidVersion, value); }
        public string SdkVersion { get => _sdkVersion; set => SetProperty(ref _sdkVersion, value); }
        public string BuildNumber { get => _buildNumber; set => SetProperty(ref _buildNumber, value); }
        public string CpuAbi { get => _cpuAbi; set => SetProperty(ref _cpuAbi, value); }
        public string Imei { get => _imei; set => SetProperty(ref _imei, value); }
        public string SerialNumberDisplay { get => _serialNumberDisplay; set => SetProperty(ref _serialNumberDisplay, value); }
        public string BatteryLevelText { get => _batteryLevelText; set => SetProperty(ref _batteryLevelText, value); }
        public string StorageStatsText { get => _storageStatsText; set => SetProperty(ref _storageStatsText, value); }
        public string RamStatsText { get => _ramStatsText; set => SetProperty(ref _ramStatsText, value); }

        public bool IsRooted { get => _isRooted; set => SetProperty(ref _isRooted, value); }
        public string RootStatusText { get => _rootStatusText; set => SetProperty(ref _rootStatusText, value); }
        public string MagiskStatusText { get => _magiskStatusText; set => SetProperty(ref _magiskStatusText, value); }
        public string SuperSuStatusText { get => _superSuStatusText; set => SetProperty(ref _superSuStatusText, value); }
        public string BusyBoxStatusText { get => _busyBoxStatusText; set => SetProperty(ref _busyBoxStatusText, value); }
        public string SELinuxStatusText { get => _seLinuxStatusText; set => SetProperty(ref _seLinuxStatusText, value); }
        public string DmVerityStatusText { get => _dmVerityStatusText; set => SetProperty(ref _dmVerityStatusText, value); }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand RefreshDiagnosticsCommand { get; }
        public ICommand RebootSystemCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootBootloaderCommand { get; }
        public ICommand RebootFastbootDCommand { get; }
        public ICommand RebootSafeModeCommand { get; }
        public ICommand RebootSideloadCommand { get; }

        public AdvancedToolsViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            RefreshDiagnosticsCommand = new AsyncRelayCommand(RefreshDiagnosticsAsync);
            RebootSystemCommand = new AsyncRelayCommand(() => RebootDeviceInternalAsync(""));
            RebootRecoveryCommand = new AsyncRelayCommand(() => RebootDeviceInternalAsync("recovery"));
            RebootBootloaderCommand = new AsyncRelayCommand(() => RebootDeviceInternalAsync("bootloader"));
            RebootFastbootDCommand = new AsyncRelayCommand(() => RebootDeviceInternalAsync("fastboot"));
            RebootSafeModeCommand = new AsyncRelayCommand(RebootSafeModeAsync);
            RebootSideloadCommand = new AsyncRelayCommand(() => RebootDeviceInternalAsync("sideload"));

            _ = RefreshDiagnosticsAsync();
        }

        public async Task RefreshDiagnosticsAsync()
        {
            IsBusy = true;

            try
            {
                // Device Hardware Info
                var (_, brandOut) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.brand", TargetSerialNumber);
                Brand = !string.IsNullOrWhiteSpace(brandOut) ? brandOut.Trim() : "Generic";

                var (_, modelOut) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.model", TargetSerialNumber);
                Model = !string.IsNullOrWhiteSpace(modelOut) ? modelOut.Trim() : "Android Device";

                var (_, verOut) = await _adbService.ExecuteCommandAsync("shell getprop ro.build.version.release", TargetSerialNumber);
                AndroidVersion = !string.IsNullOrWhiteSpace(verOut) ? verOut.Trim() : "Android 14";

                var (_, sdkOut) = await _adbService.ExecuteCommandAsync("shell getprop ro.build.version.sdk", TargetSerialNumber);
                SdkVersion = !string.IsNullOrWhiteSpace(sdkOut) ? sdkOut.Trim() : "34";

                var (_, buildOut) = await _adbService.ExecuteCommandAsync("shell getprop ro.build.display.id", TargetSerialNumber);
                BuildNumber = !string.IsNullOrWhiteSpace(buildOut) ? buildOut.Trim() : "Build 1.0";

                var (_, abiOut) = await _adbService.ExecuteCommandAsync("shell getprop ro.product.cpu.abi", TargetSerialNumber);
                CpuAbi = !string.IsNullOrWhiteSpace(abiOut) ? abiOut.Trim() : "arm64-v8a";

                SerialNumberDisplay = TargetSerialNumber ?? "UnknownSerial";

                // Battery
                var details = await _adbService.GetDeviceDetailsAsync(TargetSerialNumber ?? string.Empty);
                if (details != null)
                {
                    BatteryLevelText = $"{details.BatteryLevel}% {(details.IsCharging ? "(Charging)" : "(Discharging)")}";
                    StorageStatsText = $"{details.Stats.StorageUsedGb:F1} GB / {details.Stats.StorageTotalGb:F1} GB";
                    RamStatsText = $"{details.Stats.RamUsedMb / 1024.0:F1} GB / {details.Stats.RamTotalMb / 1024.0:F1} GB";
                }

                // Root & Security Status Diagnostics
                IsRooted = await _adbService.CheckRootAsync(TargetSerialNumber);
                RootStatusText = IsRooted ? "Rooted (SU UID 0 Active)" : "Not Rooted";

                var (_, magiskOut) = await _adbService.ExecuteCommandAsync("shell pm list packages com.topjohnwu.magisk", TargetSerialNumber);
                MagiskStatusText = magiskOut.Contains("magisk") ? "Magisk Installed" : "Not Installed";

                var (_, suOut) = await _adbService.ExecuteCommandAsync("shell which su", TargetSerialNumber);
                SuperSuStatusText = suOut.Contains("su") ? $"Binary Present ({suOut.Trim()})" : "No SU Binary";

                var (_, bbOut) = await _adbService.ExecuteCommandAsync("shell which busybox", TargetSerialNumber);
                BusyBoxStatusText = bbOut.Contains("busybox") ? "BusyBox Active" : "Not Found";

                var (_, seOut) = await _adbService.ExecuteCommandAsync("shell getenforce", TargetSerialNumber);
                SELinuxStatusText = !string.IsNullOrWhiteSpace(seOut) ? seOut.Trim() : "Enforcing";

                var (_, verityOut) = await _adbService.ExecuteCommandAsync("shell getprop ro.boot.veritymode", TargetSerialNumber);
                DmVerityStatusText = !string.IsNullOrWhiteSpace(verityOut) ? verityOut.Trim() : "Enforcing";
            }
            catch { }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RebootDeviceInternalAsync(string mode)
        {
            var (success, msg) = await _adbService.RebootDeviceAsync(TargetSerialNumber, mode);
            if (success) _notificationService.ShowSuccess("Reboot Triggered", $"Rebooting into {mode} mode...");
            else _notificationService.ShowError("Reboot Failed", msg);
        }

        private async Task RebootSafeModeAsync()
        {
            await _adbService.ExecuteCommandAsync("shell setprop persist.sys.safemode 1", TargetSerialNumber);
            await RebootDeviceInternalAsync("");
        }
    }
}

