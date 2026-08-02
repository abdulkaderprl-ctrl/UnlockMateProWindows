using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class FastbootToolsViewModel : ViewModelBase
    {
        private readonly IFastbootService _fastbootService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private FastbootDevice? _selectedDevice;
        private string _selectedPartition = "boot";
        private string _imagePath = string.Empty;
        private string _fastbootOutput = "Fastboot Suite Ready.\nConnect device in Fastboot mode or run 'adb reboot bootloader'.\n\n";
        private bool _isBusy = false;
        private string _frpStatusText = "Unknown";

        public ObservableCollection<FastbootDevice> FastbootDevices { get; } = new ObservableCollection<FastbootDevice>();

        public ObservableCollection<string> PartitionList { get; } = new ObservableCollection<string>
        {
            "boot",
            "recovery",
            "vbmeta",
            "vendor_boot",
            "init_boot",
            "super",
            "system",
            "vendor",
            "userdata"
        };

        public FastbootDevice? SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        public string SelectedPartition
        {
            get => _selectedPartition;
            set => SetProperty(ref _selectedPartition, value);
        }

        public string ImagePath
        {
            get => _imagePath;
            set => SetProperty(ref _imagePath, value);
        }

        public string FastbootOutput
        {
            get => _fastbootOutput;
            set => SetProperty(ref _fastbootOutput, value);
        }

        public string FrpStatusText
        {
            get => _frpStatusText;
            set => SetProperty(ref _frpStatusText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand RefreshDevicesCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand FlashImageCommand { get; }
        public ICommand BootImageCommand { get; }
        public ICommand ErasePartitionCommand { get; }
        public ICommand GetVarAllCommand { get; }
        public ICommand CheckFrpCommand { get; }
        public ICommand UnlockBootloaderCommand { get; }
        public ICommand LockBootloaderCommand { get; }
        public ICommand RebootBootloaderCommand { get; }
        public ICommand RebootSystemCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootFastbootdCommand { get; }
        public ICommand FlashAllPartitionsCommand { get; }

        public FastbootToolsViewModel(
            IFastbootService fastbootService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _fastbootService = fastbootService;
            _logger = logger;
            _notificationService = notificationService;

            RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
            BrowseImageCommand = new RelayCommand(BrowseImage);
            FlashImageCommand = new AsyncRelayCommand(FlashImageAsync, () => !string.IsNullOrWhiteSpace(ImagePath) && !IsBusy);
            BootImageCommand = new AsyncRelayCommand(BootImageAsync, () => !string.IsNullOrWhiteSpace(ImagePath) && !IsBusy);
            ErasePartitionCommand = new AsyncRelayCommand(ErasePartitionAsync, () => !IsBusy);
            GetVarAllCommand = new AsyncRelayCommand(GetVarAllAsync, () => !IsBusy);
            CheckFrpCommand = new AsyncRelayCommand(CheckFrpAsync, () => !IsBusy);
            UnlockBootloaderCommand = new AsyncRelayCommand(UnlockBootloaderAsync, () => !IsBusy);
            LockBootloaderCommand = new AsyncRelayCommand(LockBootloaderAsync, () => !IsBusy);
            RebootBootloaderCommand = new AsyncRelayCommand(() => RebootAsync("bootloader"));
            RebootSystemCommand = new AsyncRelayCommand(() => RebootAsync(""));
            RebootRecoveryCommand = new AsyncRelayCommand(() => RebootAsync("recovery"));
            RebootFastbootdCommand = new AsyncRelayCommand(() => RebootAsync("fastboot"));
            FlashAllPartitionsCommand = new AsyncRelayCommand(FlashAllPartitionsAsync, () => !IsBusy);

            _ = RefreshDevicesAsync();
        }

        public async Task RefreshDevicesAsync()
        {
            var devices = await _fastbootService.GetConnectedFastbootDevicesAsync();
            FastbootDevices.Clear();
            foreach (var dev in devices) FastbootDevices.Add(dev);

            SelectedDevice = FastbootDevices.FirstOrDefault();
            FastbootOutput += $"[INFO] Fastboot device scan complete. Found {FastbootDevices.Count} device(s).\n";
        }

        private void BrowseImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Disk Images (*.img;*.bin)|*.img;*.bin|All Files (*.*)|*.*",
                Title = "Select Image to Flash"
            };

            if (dialog.ShowDialog() == true)
            {
                ImagePath = dialog.FileName;
            }
        }

        private async Task FlashImageAsync()
        {
            if (MessageBox.Show($"Are you sure you want to flash '{Path.GetFileName(ImagePath)}' to partition '{SelectedPartition}'?", "Safety Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            FastbootOutput += $"[FLASHING] Flashing partition '{SelectedPartition}' with '{ImagePath}'...\n";

            var (success, msg) = await _fastbootService.FlashImageAsync(SelectedPartition, ImagePath, SelectedDevice?.SerialNumber);
            IsBusy = false;

            FastbootOutput += msg + "\n\n";
            if (success) _notificationService.ShowSuccess("Flash Complete", $"Successfully flashed {SelectedPartition} partition!");
            else _notificationService.ShowError("Flash Failed", msg);
        }

        private async Task FlashAllPartitionsAsync()
        {
            if (MessageBox.Show($"Are you sure you want to flash ALL partitions (requires images in current ADB folder)?", "Safety Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            FastbootOutput += $"[FLASHING] Flashing ALL partitions...\n";

            var (success, msg) = await _fastbootService.FlashAllPartitionsAsync(SelectedDevice?.SerialNumber);
            IsBusy = false;

            FastbootOutput += msg + "\n\n";
            if (success) _notificationService.ShowSuccess("Flash All Complete", $"Successfully flashed all partitions!");
            else _notificationService.ShowError("Flash All Failed", msg);
        }

        private async Task BootImageAsync()
        {
            IsBusy = true;
            FastbootOutput += $"[BOOT] Booting temporary image '{ImagePath}'...\n";
            var (success, msg) = await _fastbootService.BootImageAsync(ImagePath, SelectedDevice?.SerialNumber);
            IsBusy = false;

            FastbootOutput += msg + "\n\n";
            if (success) _notificationService.ShowSuccess("Boot Image", "Temporary image booted.");
            else _notificationService.ShowError("Boot Failed", msg);
        }

        private async Task ErasePartitionAsync()
        {
            if (MessageBox.Show($"DANGER: Erasing '{SelectedPartition}' partition will destroy data on that partition. Proceed?", "Danger Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Stop) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            FastbootOutput += $"[ERASE] Erasing partition '{SelectedPartition}'...\n";
            var (success, msg) = await _fastbootService.ErasePartitionAsync(SelectedPartition, SelectedDevice?.SerialNumber);
            IsBusy = false;

            FastbootOutput += msg + "\n\n";
        }

        private async Task GetVarAllAsync()
        {
            IsBusy = true;
            var (success, output) = await _fastbootService.GetVarAllAsync(SelectedDevice?.SerialNumber);
            IsBusy = false;
            FastbootOutput += output + "\n\n";
        }

        private async Task CheckFrpAsync()
        {
            IsBusy = true;
            var (success, status) = await _fastbootService.GetFrpStatusAsync(SelectedDevice?.SerialNumber);
            IsBusy = false;

            FrpStatusText = status;
            FastbootOutput += $"[FRP STATUS] {status}\n\n";
        }

        private async Task UnlockBootloaderAsync()
        {
            if (MessageBox.Show("WARNING: Unlocking bootloader will wipe all user data on the device! Continue?", "Bootloader Unlock Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            var (success, msg) = await _fastbootService.OemUnlockAsync(SelectedDevice?.SerialNumber);
            IsBusy = false;

            FastbootOutput += msg + "\n\n";
        }

        private async Task LockBootloaderAsync()
        {
            IsBusy = true;
            var (success, msg) = await _fastbootService.OemLockAsync(SelectedDevice?.SerialNumber);
            IsBusy = false;

            FastbootOutput += msg + "\n\n";
        }

        private async Task RebootAsync(string mode)
        {
            IsBusy = true;
            var (success, msg) = await _fastbootService.RebootFastbootAsync(SelectedDevice?.SerialNumber, mode);
            IsBusy = false;
            FastbootOutput += msg + "\n\n";
        }
    }
}

