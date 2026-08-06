using System;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Services;
using UnlockMatePro.Models; // Ensure we have RelayCommand or similar

namespace UnlockMatePro.ViewModels
{
    public class NothingViewModel : ViewModelBase
    {
        private readonly INothingService _nothingService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;
        private bool _isPolling = false;

        private string _logText = "";
        public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

        private bool _isBusy = false;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private int _progress = 0;
        public int Progress { get => _progress; set => SetProperty(ref _progress, value); }

        private string _statusText = "Idle";
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        // Device properties
        public string CurrentMode => _nothingService.CurrentMode;
        public string Model => _nothingService.Model;
        public string Product => _nothingService.Product;
        public string Codename => _nothingService.Codename;
        public string AndroidVersion => _nothingService.AndroidVersion;
        public string BuildNumber => _nothingService.BuildNumber;
        public string SerialNumber => _nothingService.SerialNumber;
        public string BootloaderState => _nothingService.BootloaderState;
        public string Slot => _nothingService.Slot;
        public string BatteryLevel => _nothingService.BatteryLevel;

        // Flash paths
        private string _bootPath = "";
        public string BootPath { get => _bootPath; set => SetProperty(ref _bootPath, value); }
        
        private string _initBootPath = "";
        public string InitBootPath { get => _initBootPath; set => SetProperty(ref _initBootPath, value); }

        private string _vendorBootPath = "";
        public string VendorBootPath { get => _vendorBootPath; set => SetProperty(ref _vendorBootPath, value); }
        
        private string _vbmetaPath = "";
        public string VbmetaPath { get => _vbmetaPath; set => SetProperty(ref _vbmetaPath, value); }
        
        private string _superPath = "";
        public string SuperPath { get => _superPath; set => SetProperty(ref _superPath, value); }

        private string _firmwarePath = "";
        public string FirmwarePath { get => _firmwarePath; set => SetProperty(ref _firmwarePath, value); }

        // Commands
        public ICommand RebootSystemCommand { get; }
        public ICommand RebootRecoveryCommand { get; }
        public ICommand RebootBootloaderCommand { get; }
        public ICommand RebootFastbootDCommand { get; }
        public ICommand RebootEdlCommand { get; }
        
        public ICommand UnlockBootloaderCommand { get; }
        public ICommand RelockBootloaderCommand { get; }

        public ICommand FlashBootCommand { get; }
        public ICommand FlashInitBootCommand { get; }
        public ICommand FlashVendorBootCommand { get; }
        public ICommand FlashVbmetaCommand { get; }
        public ICommand FlashSuperCommand { get; }
        public ICommand FlashFirmwareCommand { get; }

        public NothingViewModel(
            INothingService nothingService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _nothingService = nothingService;
            _logger = logger;
            _notificationService = notificationService;

            RebootSystemCommand = new RelayCommand(async () => await ExecuteTask("Reboot System", _nothingService.RebootSystemAsync));
            RebootRecoveryCommand = new RelayCommand(async () => await ExecuteTask("Reboot Recovery", _nothingService.RebootRecoveryAsync));
            RebootBootloaderCommand = new RelayCommand(async () => await ExecuteTask("Reboot Bootloader", _nothingService.RebootBootloaderAsync));
            RebootFastbootDCommand = new RelayCommand(async () => await ExecuteTask("Reboot FastbootD", _nothingService.RebootFastbootDAsync));
            RebootEdlCommand = new RelayCommand(async () => await ExecuteTask("Reboot EDL", _nothingService.RebootEdlAsync));

            UnlockBootloaderCommand = new RelayCommand(async () => await ExecuteTask("Unlock Bootloader", _nothingService.UnlockBootloaderAsync));
            RelockBootloaderCommand = new RelayCommand(async () => await ExecuteTask("Relock Bootloader", _nothingService.RelockBootloaderAsync));

            FlashBootCommand = new RelayCommand(async () => await ExecuteTask("Flash Boot", async () => await _nothingService.FlashPartitionAsync("boot", BootPath)));
            FlashInitBootCommand = new RelayCommand(async () => await ExecuteTask("Flash Init Boot", async () => await _nothingService.FlashPartitionAsync("init_boot", InitBootPath)));
            FlashVendorBootCommand = new RelayCommand(async () => await ExecuteTask("Flash Vendor Boot", async () => await _nothingService.FlashPartitionAsync("vendor_boot", VendorBootPath)));
            FlashVbmetaCommand = new RelayCommand(async () => await ExecuteTask("Flash Vbmeta", async () => await _nothingService.FlashPartitionAsync("vbmeta", VbmetaPath)));
            FlashSuperCommand = new RelayCommand(async () => await ExecuteTask("Flash Super", async () => await _nothingService.FlashPartitionAsync("super", SuperPath)));
            
            FlashFirmwareCommand = new RelayCommand(async () => await ExecuteTask("Flash Firmware", async () => {
                await _nothingService.FlashFirmwareAsync(FirmwarePath, (msg) => Log(msg), (prog) => Progress = prog);
            }));

            StartBackgroundPolling();
        }

        private async void StartBackgroundPolling()
        {
            if (_isPolling) return;
            _isPolling = true;

            while (_isPolling)
            {
                if (!IsBusy)
                {
                    bool stateChanged = await _nothingService.DetectDeviceAsync();
                    if (stateChanged)
                    {
                        OnPropertyChanged(nameof(CurrentMode));
                        OnPropertyChanged(nameof(Model));
                        OnPropertyChanged(nameof(Product));
                        OnPropertyChanged(nameof(Codename));
                        OnPropertyChanged(nameof(AndroidVersion));
                        OnPropertyChanged(nameof(BuildNumber));
                        OnPropertyChanged(nameof(SerialNumber));
                        OnPropertyChanged(nameof(BootloaderState));
                        OnPropertyChanged(nameof(Slot));
                        OnPropertyChanged(nameof(BatteryLevel));
                    }
                }
                await Task.Delay(2000);
            }
        }

        private async Task ExecuteTask(string taskName, Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusText = $"Executing: {taskName}";
            Progress = 0;
            Log($"--- Started: {taskName} ---");

            try
            {
                await action();
                Progress = 100;
                Log($"[SUCCESS] {taskName}");
                _notificationService.ShowSuccess(taskName, "Operation completed successfully.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] {taskName}: {ex.Message}");
                _notificationService.ShowError(taskName, ex.Message);
            }
            finally
            {
                IsBusy = false;
                StatusText = "Idle";
            }
        }

        private void Log(string message)
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            _logger.LogInfo(message);
        }
    }
}
