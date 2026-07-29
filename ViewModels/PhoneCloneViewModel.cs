using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class PhoneCloneViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly IPhoneCloneService _phoneCloneService;
        private readonly INotificationService _notificationService;
        private readonly ILoggerService _logger;

        private AdbDevice? _sourceDevice;
        private AdbDevice? _targetDevice;
        private int _currentStep = 1; // 1 = Devices, 2 = Content, 3 = Transfer, 4 = Finish
        private bool _includeContacts = true;
        private bool _includeSms = true;
        private bool _includeCallLogs = true;
        private bool _includeApps = true;
        private bool _includeFiles = true;

        private bool _isCloning = false;
        private string _stepTitle = "Step 1: Select Source & Target Phones";
        private string _statusText = "Ready to select devices.";
        private string _currentFileText = string.Empty;
        private string _transferSpeedText = string.Empty;
        private string _etaText = string.Empty;
        private double _overallProgress = 0;
        private string _cloneSummaryReport = string.Empty;
        private string _connectionWarningMessage = string.Empty;

        private CancellationTokenSource? _cloneCts;
        private readonly Func<Task>? _requestGlobalRefresh;

        public ObservableCollection<AdbDevice> ConnectedDevices { get; } = new ObservableCollection<AdbDevice>();
        public ObservableCollection<string> LogConsole { get; } = new ObservableCollection<string>();

        public AdbDevice? SourceDevice
        {
            get => _sourceDevice;
            set
            {
                if (SetProperty(ref _sourceDevice, value))
                {
                    OnPropertyChanged(nameof(CanProceedToStep2));
                }
            }
        }

        public AdbDevice? TargetDevice
        {
            get => _targetDevice;
            set
            {
                if (SetProperty(ref _targetDevice, value))
                {
                    OnPropertyChanged(nameof(CanProceedToStep2));
                }
            }
        }

        public string ConnectionWarningMessage
        {
            get => _connectionWarningMessage;
            set => SetProperty(ref _connectionWarningMessage, value);
        }

        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    UpdateStepTitle();
                    OnPropertyChanged(nameof(CanProceedToStep2));
                }
            }
        }

        public bool IncludeContacts
        {
            get => _includeContacts;
            set => SetProperty(ref _includeContacts, value);
        }

        public bool IncludeSms
        {
            get => _includeSms;
            set => SetProperty(ref _includeSms, value);
        }

        public bool IncludeCallLogs
        {
            get => _includeCallLogs;
            set => SetProperty(ref _includeCallLogs, value);
        }

        public bool IncludeApps
        {
            get => _includeApps;
            set => SetProperty(ref _includeApps, value);
        }

        public bool IncludeFiles
        {
            get => _includeFiles;
            set => SetProperty(ref _includeFiles, value);
        }

        public bool IsCloning
        {
            get => _isCloning;
            set
            {
                if (SetProperty(ref _isCloning, value))
                {
                    OnPropertyChanged(nameof(CanProceedToStep2));
                }
            }
        }

        public string StepTitle
        {
            get => _stepTitle;
            set => SetProperty(ref _stepTitle, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string CurrentFileText
        {
            get => _currentFileText;
            set => SetProperty(ref _currentFileText, value);
        }

        public string TransferSpeedText
        {
            get => _transferSpeedText;
            set => SetProperty(ref _transferSpeedText, value);
        }

        public string EtaText
        {
            get => _etaText;
            set => SetProperty(ref _etaText, value);
        }

        public double OverallProgress
        {
            get => _overallProgress;
            set => SetProperty(ref _overallProgress, value);
        }

        public string CloneSummaryReport
        {
            get => _cloneSummaryReport;
            set => SetProperty(ref _cloneSummaryReport, value);
        }

        public bool CanProceedToStep2 =>
            SourceDevice != null &&
            TargetDevice != null &&
            !string.Equals(SourceDevice.SerialNumber, TargetDevice.SerialNumber, StringComparison.OrdinalIgnoreCase) &&
            !IsCloning;

        public ICommand RefreshDevicesCommand { get; }
        public ICommand NextStepCommand { get; }
        public ICommand PreviousStepCommand { get; }
        public ICommand StartCloneCommand { get; }
        public ICommand CancelCloneCommand { get; }
        public ICommand ResetWizardCommand { get; }

        public PhoneCloneViewModel(
            IAdbService adbService,
            IPhoneCloneService phoneCloneService,
            INotificationService notificationService,
            ILoggerService logger,
            Func<Task>? requestGlobalRefresh = null)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
            _phoneCloneService = phoneCloneService ?? throw new ArgumentNullException(nameof(phoneCloneService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requestGlobalRefresh = requestGlobalRefresh;

            RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
            NextStepCommand = new RelayCommand(NextStep, () => CanProceedToStep2 && CurrentStep == 1 || CurrentStep == 2 && !IsCloning);
            PreviousStepCommand = new RelayCommand(PreviousStep, () => CurrentStep > 1 && !IsCloning && CurrentStep < 4);
            StartCloneCommand = new AsyncRelayCommand(StartPhoneCloneAsync, () => CanProceedToStep2 && !IsCloning);
            CancelCloneCommand = new RelayCommand(CancelPhoneClone, () => IsCloning);
            ResetWizardCommand = new RelayCommand(ResetWizard, () => !IsCloning);
        }

        public async Task RefreshDevicesAsync()
        {
            if (_requestGlobalRefresh != null)
            {
                Log("Requesting global device refresh...");
                await _requestGlobalRefresh();
            }
            else
            {
                Log("Global refresh not configured, cannot refresh manually.");
            }
        }

        public void UpdateDevices(ObservableCollection<AdbDevice> devices)
        {
            try
            {
                ConnectedDevices.Clear();

                if (devices.Count == 0)
                {
                    Log("No devices detected.");
                    ConnectionWarningMessage = string.Empty;
                    return;
                }

                Log($"Updating phone clone list with {devices.Count} device(s)...");
                foreach (var dev in devices)
                {
                    ConnectedDevices.Add(dev);
                    Log($"- Detected Device: {dev.Model} (Serial: {dev.SerialNumber}) [Android {dev.AndroidVersion}]");
                }

                if (ConnectedDevices.Count >= 2)
                {
                    ConnectionWarningMessage = string.Empty;
                    if (SourceDevice == null || !ConnectedDevices.Contains(SourceDevice))
                    {
                        SourceDevice = ConnectedDevices[0];
                    }
                    if (TargetDevice == null || !ConnectedDevices.Contains(TargetDevice))
                    {
                        var target = ConnectedDevices.FirstOrDefault(d => d.SerialNumber != SourceDevice?.SerialNumber);
                        TargetDevice = target ?? ConnectedDevices[1];
                    }
                    Log($"Selected Source [{SourceDevice?.DisplayName}] and Target [{TargetDevice?.DisplayName}].");
                }
                else if (ConnectedDevices.Count == 1)
                {
                    if (SourceDevice == null || !ConnectedDevices.Contains(SourceDevice))
                    {
                        SourceDevice = ConnectedDevices[0];
                    }
                    TargetDevice = null;
                    ConnectionWarningMessage = "Connect another Android device to continue.";
                    Log($"Found 1 device [{SourceDevice?.DisplayName}]. Please connect a 2nd phone as target.");
                }
                else
                {
                    SourceDevice = null;
                    TargetDevice = null;
                    ConnectionWarningMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PhoneCloneViewModel] Exception updating devices: {ex.Message}");
            }
        }

        private void NextStep()
        {
            if (CurrentStep == 1)
            {
                if (SourceDevice == null || TargetDevice == null)
                {
                    _notificationService.ShowNotification("Select Devices", "Please select both Source and Target phones.", NotificationType.Warning);
                    return;
                }

                if (string.Equals(SourceDevice.SerialNumber, TargetDevice.SerialNumber, StringComparison.OrdinalIgnoreCase))
                {
                    _notificationService.ShowNotification("Invalid Selection", "Source and Target phone cannot be the same device.", NotificationType.Error);
                    return;
                }

                CurrentStep = 2;
            }
            else if (CurrentStep == 2)
            {
                if (!IncludeContacts && !IncludeSms && !IncludeCallLogs && !IncludeApps && !IncludeFiles)
                {
                    _notificationService.ShowNotification("Select Content", "Please select at least one item to clone.", NotificationType.Warning);
                    return;
                }

                CurrentStep = 3;
                _ = StartPhoneCloneAsync();
            }
        }

        private void PreviousStep()
        {
            if (CurrentStep > 1 && !IsCloning)
            {
                CurrentStep--;
            }
        }

        public async Task StartPhoneCloneAsync()
        {
            if (SourceDevice == null || TargetDevice == null || IsCloning) return;

            CurrentStep = 3;
            IsCloning = true;
            OverallProgress = 0;
            StatusText = "Preparing Phone-to-Phone Clone Engine...";
            LogConsole.Clear();

            Log($"Starting Smart Switch Phone Clone...");
            Log($"Source: {SourceDevice.Model} ({SourceDevice.SerialNumber}) - Android {SourceDevice.AndroidVersion}");
            Log($"Target: {TargetDevice.Model} ({TargetDevice.SerialNumber}) - Android {TargetDevice.AndroidVersion}");

            _cloneCts = new CancellationTokenSource();
            var stopwatch = Stopwatch.StartNew();

            var options = new PhoneCloneOptions
            {
                SourceDeviceSerial = SourceDevice.SerialNumber,
                TargetDeviceSerial = TargetDevice.SerialNumber,
                IncludeContacts = IncludeContacts,
                IncludeSms = IncludeSms,
                IncludeCallLogs = IncludeCallLogs,
                IncludeApps = IncludeApps,
                IncludeFiles = IncludeFiles
            };

            var progress = new Progress<PhoneCloneProgressInfo>(p =>
            {
                if (!string.IsNullOrWhiteSpace(p.StepTitle)) StepTitle = p.StepTitle;
                if (!string.IsNullOrWhiteSpace(p.StatusText)) StatusText = p.StatusText;
                if (!string.IsNullOrWhiteSpace(p.CurrentItemName)) CurrentFileText = p.CurrentItemName;
                if (!string.IsNullOrWhiteSpace(p.TransferSpeedText)) TransferSpeedText = p.TransferSpeedText;
                if (!string.IsNullOrWhiteSpace(p.RemainingTimeText)) EtaText = p.RemainingTimeText;
                if (p.OverallProgress > 0) OverallProgress = p.OverallProgress;
                if (!string.IsNullOrWhiteSpace(p.LogMessage)) Log(p.LogMessage);
            });

            bool success = await _phoneCloneService.ClonePhoneToPhoneAsync(options, progress, _cloneCts.Token);

            IsCloning = false;
            stopwatch.Stop();

            if (success)
            {
                OverallProgress = 100;
                CurrentStep = 4;
                StatusText = "Phone Clone completed successfully!";

                var sb = new StringBuilder();
                sb.AppendLine("==========================================");
                sb.AppendLine("         PHONE CLONE SUCCESS REPORT       ");
                sb.AppendLine("==========================================");
                sb.AppendLine($"Date & Time     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Source Phone    : {SourceDevice.Model} ({SourceDevice.SerialNumber})");
                sb.AppendLine($"Target Phone    : {TargetDevice.Model} ({TargetDevice.SerialNumber})");
                sb.AppendLine($"Duration        : {stopwatch.Elapsed:mm\\:ss}");
                sb.AppendLine("------------------------------------------");
                sb.AppendLine($"[✓] Contacts Cloned    : {(IncludeContacts ? "Yes" : "Skipped")}");
                sb.AppendLine($"[✓] SMS Messages Cloned: {(IncludeSms ? "Yes" : "Skipped")}");
                sb.AppendLine($"[✓] Call Logs Cloned   : {(IncludeCallLogs ? "Yes" : "Skipped")}");
                sb.AppendLine($"[✓] Apps Installed     : {(IncludeApps ? "Yes" : "Skipped")}");
                sb.AppendLine($"[✓] Internal Storage   : {(IncludeFiles ? "Yes" : "Skipped")}");
                sb.AppendLine("==========================================");

                CloneSummaryReport = sb.ToString();
                Log("Phone clone process finished cleanly.");
                _notificationService.ShowSuccess("Phone Clone Complete", $"Successfully cloned data to '{TargetDevice.Model}'!");
            }
            else
            {
                StatusText = "Phone Clone failed or was cancelled.";
                _notificationService.ShowError("Phone Clone Failed", "Phone clone operation encountered errors or was cancelled.");
            }
        }

        private void CancelPhoneClone()
        {
            if (IsCloning)
            {
                _cloneCts?.Cancel();
                Log("User pressed Cancel. Aborting phone clone...");
                StatusText = "Cancelling phone clone operation...";
            }
        }

        private void ResetWizard()
        {
            CurrentStep = 1;
            OverallProgress = 0;
            StatusText = "Ready to select devices.";
            CurrentFileText = string.Empty;
            TransferSpeedText = string.Empty;
            EtaText = string.Empty;
            LogConsole.Clear();
            _ = RefreshDevicesAsync();
        }

        private void UpdateStepTitle()
        {
            switch (CurrentStep)
            {
                case 1:
                    StepTitle = "Step 1: Select Source & Target Phones";
                    break;
                case 2:
                    StepTitle = "Step 2: Select Content to Clone";
                    break;
                case 3:
                    StepTitle = "Step 3: Transferring Data (Smart Switch)";
                    break;
                case 4:
                    StepTitle = "Step 4: Phone Clone Complete";
                    break;
            }
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogConsole.Add(line);
            _logger.LogInfo($"[PhoneCloneVM] {message}");
        }
    }
}

