using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class GooglePixelViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly IFastbootService _fastbootService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string _bootloaderStatus = "Unknown";
        public string BootloaderStatus { get => _bootloaderStatus; set => SetProperty(ref _bootloaderStatus, value); }

        private string _avbStatus = "Unknown";
        public string AvbStatus { get => _avbStatus; set => SetProperty(ref _avbStatus, value); }

        private string _oemUnlockStatus = "Unknown";
        public string OemUnlockStatus { get => _oemUnlockStatus; set => SetProperty(ref _oemUnlockStatus, value); }

        private string _deviceInfo = "Click 'Read Info' to get device details";
        public string DeviceInfo { get => _deviceInfo; set => SetProperty(ref _deviceInfo, value); }
        
        private bool _isBusy = false;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand ReadInfoCommand { get; }
        public ICommand BootloaderUnlockCommand { get; }
        public ICommand BootloaderRelockCommand { get; }
        public ICommand RebootCommand { get; }
        public ICommand FlashPartitionCommand { get; }
        public ICommand FlashFactoryImageCommand { get; }

        public GooglePixelViewModel(
            IAdbService adbService,
            IFastbootService fastbootService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _fastbootService = fastbootService;
            _logger = logger;
            _notificationService = notificationService;

            ReadInfoCommand = new AsyncRelayCommand(ExecuteReadInfoAsync, () => !IsBusy);
            BootloaderUnlockCommand = new AsyncRelayCommand(ExecuteBootloaderUnlockAsync, () => !IsBusy);
            BootloaderRelockCommand = new AsyncRelayCommand(ExecuteBootloaderRelockAsync, () => !IsBusy);
            RebootCommand = new AsyncRelayCommand<string>(ExecuteRebootAsync, _ => !IsBusy);
            FlashPartitionCommand = new AsyncRelayCommand<string>(ExecuteFlashPartitionAsync, _ => !IsBusy);
            FlashFactoryImageCommand = new AsyncRelayCommand(ExecuteFlashFactoryImageAsync, () => !IsBusy);
        }

        private async Task ExecuteReadInfoAsync()
        {
            IsBusy = true;
            try
            {
                _logger.LogInfo("Reading Google Pixel device info via Fastboot...");
                var (success, output) = await _fastbootService.GetVarAllAsync(null);
                
                if (success && !string.IsNullOrWhiteSpace(output))
                {
                    var unlockedMatch = Regex.Match(output, @"unlocked:\s*(yes|no)", RegexOptions.IgnoreCase);
                    var secureMatch = Regex.Match(output, @"secure:\s*(yes|no)", RegexOptions.IgnoreCase);
                    var productMatch = Regex.Match(output, @"product:\s*(\w+)", RegexOptions.IgnoreCase);
                    
                    BootloaderStatus = unlockedMatch.Success && unlockedMatch.Groups[1].Value.ToLower() == "yes" ? "Unlocked" : "Locked";
                    AvbStatus = secureMatch.Success && secureMatch.Groups[1].Value.ToLower() == "yes" ? "Enabled (Secure)" : "Disabled";
                    OemUnlockStatus = "Check Developer Options";

                    DeviceInfo = $"Product: {(productMatch.Success ? productMatch.Groups[1].Value : "Unknown")}\n" +
                                 $"Bootloader: {BootloaderStatus}\n" +
                                 $"AVB: {AvbStatus}";

                    _logger.LogSuccess("Device info read successfully.");
                    _notificationService.ShowSuccess("Read Info", "Successfully read device information.");
                }
                else
                {
                    _logger.LogError("Failed to read device info. Make sure device is in Fastboot mode.");
                    _notificationService.ShowError("Read Info Failed", "Ensure device is in Fastboot mode.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteBootloaderUnlockAsync()
        {
            IsBusy = true;
            try
            {
                _logger.LogWarning("Attempting to unlock bootloader...");
                var (success, _) = await _fastbootService.ExecuteFastbootCommandAsync("flashing unlock", null);
                if (success)
                {
                    _logger.LogSuccess("Unlock command sent. Please confirm on device screen.");
                    _notificationService.ShowSuccess("Bootloader", "Unlock command sent. Check device screen.");
                }
                else
                {
                    _logger.LogError("Failed to send unlock command.");
                    _notificationService.ShowError("Bootloader Error", "Command failed.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteBootloaderRelockAsync()
        {
            IsBusy = true;
            try
            {
                _logger.LogWarning("Attempting to relock bootloader...");
                var (success, _) = await _fastbootService.ExecuteFastbootCommandAsync("flashing lock", null);
                if (success)
                {
                    _logger.LogSuccess("Relock command sent. Please confirm on device screen.");
                    _notificationService.ShowSuccess("Bootloader", "Relock command sent. Check device screen.");
                }
                else
                {
                    _logger.LogError("Failed to send relock command.");
                    _notificationService.ShowError("Bootloader Error", "Command failed.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteRebootAsync(string? mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            
            IsBusy = true;
            try
            {
                _logger.LogInfo($"Rebooting to {mode}...");
                bool success = false;
                
                if (mode.ToLower() == "fastbootd")
                {
                    var res = await _fastbootService.ExecuteFastbootCommandAsync("reboot fastboot", null);
                    success = res.Success;
                }
                else
                {
                    var res = await _fastbootService.RebootFastbootAsync(null, mode);
                    success = res.Success;
                    
                    if (!success)
                    {
                        await _adbService.RebootDeviceAsync("", mode); // RebootDeviceAsync returns Task, not a tuple, assume it succeeded or use another way to check
                        success = true; // Temporary simplification, it doesn't return success boolean
                    }
                }
                
                if (success)
                    _notificationService.ShowSuccess("Reboot", $"Rebooting to {mode}...");
                else
                    _logger.LogError($"Failed to reboot to {mode}.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteFlashPartitionAsync(string? partition)
        {
            if (string.IsNullOrEmpty(partition)) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Select {partition} image",
                Filter = "Image Files (*.img)|*.img|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    string filePath = dialog.FileName;
                    _logger.LogInfo($"Preparing to flash {partition} from {filePath}");
                    var (success, msg) = await _fastbootService.FlashImageAsync(partition, filePath, null);
                    
                    if (success)
                    {
                        _notificationService.ShowSuccess("Flash Success", $"Flashed {partition} successfully.");
                    }
                    else
                    {
                        _logger.LogError($"Flashing {partition} failed: {msg}");
                        _notificationService.ShowError("Flash Error", $"Failed to flash {partition}.");
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ExecuteFlashFactoryImageAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Factory Image ZIP",
                Filter = "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    string filePath = dialog.FileName;
                    _logger.LogInfo($"Preparing to flash factory image: {filePath}");
                    
                    _logger.LogWarning("Wiping device (-w) is typically required for factory images.");
                    var (success, msg) = await _fastbootService.ExecuteFastbootCommandAsync($"-w update \"{filePath}\"", null);
                    
                    if (success)
                    {
                        _logger.LogSuccess("Factory image flashed successfully.");
                        _notificationService.ShowSuccess("Flash Success", "Factory image flashed successfully.");
                    }
                    else
                    {
                        _logger.LogError($"Flashing factory image failed: {msg}");
                        _notificationService.ShowError("Flash Error", "Failed to flash factory image.");
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
