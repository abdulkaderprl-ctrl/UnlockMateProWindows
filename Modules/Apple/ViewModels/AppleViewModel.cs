using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class AppleViewModel : ViewModelBase, IDisposable
    {
        private readonly IAppleService _appleService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string _logText = "";
        private bool _isBusy = false;
        private int _progress = 0;
        private string _statusText = "Idle";
        private DeviceInfo? _detectedDeviceInfo;
        private string _ipswPath = "";
        
        // DFU Helper Properties
        private string _selectedDfuModel = "iPhone 8 to 16 / iPad with Face ID";
        private int _dfuStep = 0;
        private string _dfuInstructionText = "Select your device model and click Start DFU Helper.";
        private int _dfuCountdown = 0;
        private bool _isDfuWizardActive = false;
        private CancellationTokenSource? _dfuCts;
        private CancellationTokenSource? _pollingCts;

        public ObservableCollection<string> SupportedDfuModels { get; } = new ObservableCollection<string>
        {
            "iPhone 4 / 5 / 6 / SE (1st) / iPad (Home Button)",
            "iPhone 7 / 7 Plus",
            "iPhone 8 / X / 11 / 12 / 13 / 14 / 15 / 16 / 17 Pro Max"
        };

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string DeviceStatusText => _detectedDeviceInfo != null ? $"Device: {_detectedDeviceInfo.Model} ({_detectedDeviceInfo.Mode})" : "Device: Not Detected";

        public string IpswPath
        {
            get => _ipswPath;
            set => SetProperty(ref _ipswPath, value);
        }

        public string SelectedDfuModel
        {
            get => _selectedDfuModel;
            set => SetProperty(ref _selectedDfuModel, value);
        }

        public int DfuStep
        {
            get => _dfuStep;
            set => SetProperty(ref _dfuStep, value);
        }

        public string DfuInstructionText
        {
            get => _dfuInstructionText;
            set => SetProperty(ref _dfuInstructionText, value);
        }

        public int DfuCountdown
        {
            get => _dfuCountdown;
            set => SetProperty(ref _dfuCountdown, value);
        }

        public bool IsDfuWizardActive
        {
            get => _isDfuWizardActive;
            set
            {
                if (SetProperty(ref _isDfuWizardActive, value))
                {
                    OnPropertyChanged(nameof(IsNotDfuWizardActive));
                }
            }
        }
        
        public bool IsNotDfuWizardActive => !IsDfuWizardActive;

        private string _activeSubMenu = "FUNCTIONS";
        
        public ObservableCollection<string> SubMenuItems { get; } = new ObservableCollection<string>
        {
            "FUNCTIONS",
            "PASSCODE",
            "HELLO SCREEN",
            "DIAG [PURPLE]",
            "PROXY",
            "RAMDISK (A9)",
            "RAMDISK (A12-13)"
        };

        public string ActiveSubMenu
        {
            get => _activeSubMenu;
            set => SetProperty(ref _activeSubMenu, value);
        }

        public ICommand SwitchSubMenuCommand { get; }
        
        public ICommand DetectDeviceCommand { get; }
        public ICommand ReadInfoCommand { get; }
        public ICommand EnterRecoveryCommand { get; }
        public ICommand ExitRecoveryCommand { get; }
        public ICommand RebootDeviceCommand { get; }
        public ICommand BrowseIpswCommand { get; }
        public ICommand FlashIpswCommand { get; }
        public ICommand RestoreFirmwareCommand { get; }
        public ICommand CheckActivationCommand { get; }
        public ICommand CheckFindMyIphoneCommand { get; }
        public ICommand StartDfuWizardCommand { get; }
        public ICommand CancelDfuWizardCommand { get; }

        // New Function Commands
        public ICommand ReadHwInfoCommand { get; }
        public ICommand DisableFactoryResetCommand { get; }
        public ICommand RestoreFactoryResetCommand { get; }
        public ICommand DisableOtaUpdatesFactoryResetCommand { get; }
        public ICommand DisableOtaUpdatesCommand { get; }
        public ICommand RestoreOtaUpdatesCommand { get; }
        public ICommand UnlockSimCommand { get; }
        public ICommand BypassMdmCommand { get; }
        public ICommand FixBankAppsCommand { get; }
        public ICommand FakeIosVersionCommand { get; }
        public ICommand RestoreIosVersionCommand { get; }
        public ICommand JailbreakCommand { get; }

        private string _fakeIosVersionText = "";
        public string FakeIosVersionText
        {
            get => _fakeIosVersionText;
            set => SetProperty(ref _fakeIosVersionText, value);
        }

        private bool _isAutoFakeVersion = true;
        public bool IsAutoFakeVersion
        {
            get => _isAutoFakeVersion;
            set => SetProperty(ref _isAutoFakeVersion, value);
        }

        public AppleViewModel(IAppleService appleService, ILoggerService logger, INotificationService notificationService)
        {
            _appleService = appleService;
            _logger = logger;
            _notificationService = notificationService;

            DetectDeviceCommand = new AsyncRelayCommand(DetectDeviceAsync, () => !IsBusy);
            ReadInfoCommand = new AsyncRelayCommand(ReadInfoAsync, () => !IsBusy);
            EnterRecoveryCommand = new AsyncRelayCommand(EnterRecoveryAsync, () => !IsBusy);
            ExitRecoveryCommand = new AsyncRelayCommand(ExitRecoveryAsync, () => !IsBusy);
            RebootDeviceCommand = new AsyncRelayCommand(RebootDeviceAsync, () => !IsBusy);
            BrowseIpswCommand = new RelayCommand(BrowseIpsw);
            FlashIpswCommand = new AsyncRelayCommand(FlashIpswAsync, () => !IsBusy);
            RestoreFirmwareCommand = new AsyncRelayCommand(RestoreFirmwareAsync, () => !IsBusy);
            CheckActivationCommand = new AsyncRelayCommand(CheckActivationAsync, () => !IsBusy);
            CheckFindMyIphoneCommand = new AsyncRelayCommand(CheckFindMyIphoneAsync, () => !IsBusy);
            StartDfuWizardCommand = new AsyncRelayCommand(StartDfuWizardAsync, () => !IsBusy && !IsDfuWizardActive);
            CancelDfuWizardCommand = new RelayCommand(CancelDfuWizard);

            // Initialize new commands with placeholders
            ReadHwInfoCommand = new RelayCommand(() => Log("Read HW Info - Not Implemented"));
            DisableFactoryResetCommand = new RelayCommand(() => Log("Disable Factory Reset - Not Implemented"));
            RestoreFactoryResetCommand = new RelayCommand(() => Log("Restore Factory Reset - Not Implemented"));
            DisableOtaUpdatesFactoryResetCommand = new RelayCommand(() => Log("Disable OTA Updates + Factory Reset - Not Implemented"));
            DisableOtaUpdatesCommand = new RelayCommand(() => Log("Disable OTA Updates - Not Implemented"));
            RestoreOtaUpdatesCommand = new RelayCommand(() => Log("Restore OTA Updates - Not Implemented"));
            UnlockSimCommand = new RelayCommand(() => Log("Unlock SIM (ICCD) - Not Implemented"));
            BypassMdmCommand = new RelayCommand(() => Log("Bypass MDM - Not Implemented"));
            FixBankAppsCommand = new RelayCommand(() => Log("Fix Bank Apps - Not Implemented"));
            FakeIosVersionCommand = new RelayCommand(() => Log($"Fake iOS Version ({FakeIosVersionText}) - Not Implemented"));
            RestoreIosVersionCommand = new RelayCommand(() => Log("Restore iOS Version - Not Implemented"));
            JailbreakCommand = new RelayCommand(() => Log("Jailbreak iPhone - Not Implemented"));

            SwitchSubMenuCommand = new RelayCommand<string>(SwitchSubMenu);

            Log("Apple Professional Module Initialized.");
            StartBackgroundPolling();
        }

        private void StartBackgroundPolling()
        {
            _pollingCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!_pollingCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var device = await _appleService.DetectDeviceAsync();
                        if (device != null && (_detectedDeviceInfo == null || _detectedDeviceInfo.Mode != device.Mode || _detectedDeviceInfo.Serial != device.Serial))
                        {
                            _detectedDeviceInfo = device;
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                OnPropertyChanged(nameof(DeviceStatusText));
                                if (device.Mode == "DFU Mode" && IsDfuWizardActive && DfuStep < 4)
                                {
                                    DfuStep = 4;
                                    DfuInstructionText = "🎉 DFU Mode Detected Successfully!";
                                    Log("Device successfully entered DFU Mode.");
                                    _dfuCts?.Cancel();
                                    IsDfuWizardActive = false;
                                }
                            });
                        }
                        else if (device == null && _detectedDeviceInfo != null)
                        {
                            _detectedDeviceInfo = null;
                            System.Windows.Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(DeviceStatusText)));
                        }
                    }
                    catch { }
                    await Task.Delay(2000, _pollingCts.Token);
                }
            }, _pollingCts.Token);
        }

        public void Dispose()
        {
            _pollingCts?.Cancel();
            CancelDfuWizard();
        }

        private void SwitchSubMenu(string? menuName)
        {
            if (!string.IsNullOrEmpty(menuName))
            {
                ActiveSubMenu = menuName;
            }
        }

        private void BrowseIpsw()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "IPSW Firmware (*.ipsw)|*.ipsw|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                IpswPath = dlg.FileName;
            }
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{time}] {message}\n";
            _logger.LogInfo($"[APPLE] {message}");
        }

        private async Task DetectDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Detecting Apple device...";
            Progress = 30;
            Log("Scanning for Apple devices (Normal/Recovery/DFU)...");

            _detectedDeviceInfo = await _appleService.DetectDeviceAsync();

            if (_detectedDeviceInfo != null)
            {
                Log($"Found: {_detectedDeviceInfo.Model} | Mode: {_detectedDeviceInfo.Mode} | ID: {_detectedDeviceInfo.Serial}");
                _notificationService.ShowNotification("Device Detected", $"Apple device found in {_detectedDeviceInfo.Mode}", NotificationType.Success);
            }
            else
            {
                Log("No Apple device detected. Please check connection and drivers.");
                _notificationService.ShowNotification("Not Found", "No Apple device detected.", NotificationType.Warning);
            }

            OnPropertyChanged(nameof(DeviceStatusText));
            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task ReadInfoAsync()
        {
            IsBusy = true;
            StatusText = "Reading Device Info...";
            Progress = 50;
            Log("Reading detailed Apple device information...");

            string info = await _appleService.ReadInfoAsync();
            Log("\n" + info);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task EnterRecoveryAsync()
        {
            IsBusy = true;
            StatusText = "Entering Recovery Mode...";
            Progress = 50;
            Log("Sending device to Recovery Mode...");

            string result = await _appleService.EnterRecoveryModeAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
            await DetectDeviceAsync();
        }

        private async Task ExitRecoveryAsync()
        {
            IsBusy = true;
            StatusText = "Exiting Recovery Mode...";
            Progress = 50;
            Log("Attempting to exit Recovery Mode...");

            string result = await _appleService.ExitRecoveryModeAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
            await DetectDeviceAsync();
        }

        private async Task RebootDeviceAsync()
        {
            IsBusy = true;
            StatusText = "Rebooting Device...";
            Progress = 50;
            Log("Rebooting device...");

            string result = await _appleService.RebootDeviceAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task FlashIpswAsync()
        {
            if (string.IsNullOrEmpty(IpswPath))
            {
                Log("Please select an IPSW file first.");
                return;
            }

            IsBusy = true;
            StatusText = "Flashing IPSW...";
            Progress = 20;
            Log($"Starting Flash with IPSW: {IpswPath}");
            Log("WARNING: Do not disconnect device!");

            // In a real app, this would stream output to update progress.
            string result = await _appleService.FlashIpswAsync(IpswPath);
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task RestoreFirmwareAsync()
        {
            if (string.IsNullOrEmpty(IpswPath))
            {
                Log("Please select an IPSW file first.");
                return;
            }

            IsBusy = true;
            StatusText = "Restoring Firmware...";
            Progress = 20;
            Log($"Starting Restore (Erase Data) with IPSW: {IpswPath}");
            Log("WARNING: Do not disconnect device! All data will be erased.");

            string result = await _appleService.RestoreFirmwareAsync(IpswPath);
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task CheckActivationAsync()
        {
            IsBusy = true;
            StatusText = "Checking Activation...";
            Progress = 50;
            Log("Checking device activation status...");

            string result = await _appleService.CheckActivationStatusAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task CheckFindMyIphoneAsync()
        {
            IsBusy = true;
            StatusText = "Checking FMI...";
            Progress = 50;
            Log("Checking Find My iPhone status...");

            string result = await _appleService.CheckFindMyIphoneAsync();
            Log(result);

            Progress = 100;
            StatusText = "Idle";
            IsBusy = false;
        }

        private async Task StartDfuWizardAsync()
        {
            IsDfuWizardActive = true;
            DfuStep = 1;
            DfuCountdown = 3;
            _dfuCts = new CancellationTokenSource();
            var token = _dfuCts.Token;

            try
            {
                Log($"Starting DFU Helper for {SelectedDfuModel}.");
                DfuInstructionText = "Get ready. Ensure device is connected to PC.";
                
                for (int i = 3; i > 0; i--)
                {
                    DfuCountdown = i;
                    await Task.Delay(1000, token);
                }

                DfuStep = 2;
                if (SelectedDfuModel.Contains("Home Button") || SelectedDfuModel.Contains("4 / 5 / 6"))
                {
                    DfuInstructionText = "Press and hold Power + Home buttons together.";
                    await RunCountdownAsync(10, token);
                    
                    DfuStep = 3;
                    DfuInstructionText = "Release Power button, keep holding Home button.";
                    await RunCountdownAsync(10, token);
                }
                else if (SelectedDfuModel.Contains("7"))
                {
                    DfuInstructionText = "Press and hold Power + Volume Down together.";
                    await RunCountdownAsync(10, token);
                    
                    DfuStep = 3;
                    DfuInstructionText = "Release Power button, keep holding Volume Down.";
                    await RunCountdownAsync(10, token);
                }
                else
                {
                    // iPhone 8 and newer
                    DfuInstructionText = "Quick press Vol Up, Quick press Vol Down, then Hold Power until screen goes black.";
                    await RunCountdownAsync(5, token);
                    
                    DfuStep = 2; // Actually step 2.5
                    DfuInstructionText = "Hold Power + Volume Down together.";
                    await RunCountdownAsync(5, token);

                    DfuStep = 3;
                    DfuInstructionText = "Release Power button, keep holding Volume Down.";
                    await RunCountdownAsync(10, token);
                }

                if (!token.IsCancellationRequested && DfuStep != 4)
                {
                    DfuInstructionText = "Timeout. If device is not in DFU, please try again.";
                    Log("DFU Helper finished, but device not detected in DFU mode.");
                    IsDfuWizardActive = false;
                }
            }
            catch (TaskCanceledException)
            {
                if (DfuStep != 4)
                {
                    DfuInstructionText = "DFU Helper cancelled.";
                    Log("DFU Helper wizard cancelled by user.");
                }
            }
            finally
            {
                if (DfuStep != 4) IsDfuWizardActive = false;
            }
        }

        private async Task RunCountdownAsync(int seconds, CancellationToken token)
        {
            for (int i = seconds; i > 0; i--)
            {
                DfuCountdown = i;
                await Task.Delay(1000, token);
            }
            DfuCountdown = 0;
        }

        private void CancelDfuWizard()
        {
            if (IsDfuWizardActive)
            {
                _dfuCts?.Cancel();
                IsDfuWizardActive = false;
            }
        }
    }
}
