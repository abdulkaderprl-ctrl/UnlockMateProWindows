using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class BackupRestoreViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;
        private readonly ISmartSwitchBackupService _smartSwitchService;

        private string? _targetSerialNumber;
        private string _defaultBackupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UnlockMatePro_Backups");
        private bool _includeContacts = true;
        private bool _includeSms = true;
        private bool _includeCallLogs = true;
        private bool _includeFiles = true;
        private bool _includeApps = true;
        private bool _compressToZip = false;

        private bool _isProcessing = false;
        private double _overallProgress = 0;
        private string _currentItemName = string.Empty;
        private string _statusText = "Ready to perform Smart Switch Backup or Restore.";
        private string _transferSpeedText = string.Empty;
        private string _remainingTimeText = string.Empty;

        private CancellationTokenSource? _backupCts;

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set => SetProperty(ref _targetSerialNumber, value);
        }

        public string DefaultBackupFolder
        {
            get => _defaultBackupFolder;
            set => SetProperty(ref _defaultBackupFolder, value);
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

        public bool IncludeFiles
        {
            get => _includeFiles;
            set => SetProperty(ref _includeFiles, value);
        }

        public bool IncludeApps
        {
            get => _includeApps;
            set => SetProperty(ref _includeApps, value);
        }

        public bool CompressToZip
        {
            get => _compressToZip;
            set => SetProperty(ref _compressToZip, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public double OverallProgress
        {
            get => _overallProgress;
            set => SetProperty(ref _overallProgress, value);
        }

        public string CurrentItemName
        {
            get => _currentItemName;
            set => SetProperty(ref _currentItemName, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string TransferSpeedText
        {
            get => _transferSpeedText;
            set => SetProperty(ref _transferSpeedText, value);
        }

        public string RemainingTimeText
        {
            get => _remainingTimeText;
            set => SetProperty(ref _remainingTimeText, value);
        }

        public ICommand SelectBackupFolderCommand { get; }
        public ICommand FullBackupCommand { get; }
        public ICommand FullRestoreCommand { get; }
        public ICommand CancelBackupCommand { get; }

        public BackupRestoreViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService,
            ISmartSwitchBackupService? smartSwitchService = null)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;
            _smartSwitchService = smartSwitchService ?? new SmartSwitchBackupService(adbService, logger);

            SelectBackupFolderCommand = new RelayCommand(SelectBackupFolder);
            FullBackupCommand = new AsyncRelayCommand(StartFullBackupAsync, () => !IsProcessing);
            FullRestoreCommand = new AsyncRelayCommand(StartFullRestoreAsync, () => !IsProcessing);
            CancelBackupCommand = new RelayCommand(CancelOperation, () => IsProcessing);

            if (!Directory.Exists(_defaultBackupFolder))
            {
                try { Directory.CreateDirectory(_defaultBackupFolder); } catch { }
            }
        }

        private void SelectBackupFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Default Backup Storage Directory"
            };

            if (dialog.ShowDialog() == true)
            {
                DefaultBackupFolder = dialog.FolderName;
            }
        }

        public async Task StartFullBackupAsync()
        {
            IsProcessing = true;
            _backupCts = new CancellationTokenSource();
            OverallProgress = 2;
            StatusText = "Initializing Smart Switch Backup Engine...";
            RemainingTimeText = "Estimating metrics...";
            TransferSpeedText = string.Empty;
            CurrentItemName = string.Empty;

            var progress = new Progress<BackupProgressInfo>(p =>
            {
                if (!string.IsNullOrWhiteSpace(p.StatusText)) StatusText = p.StatusText;
                if (!string.IsNullOrWhiteSpace(p.CurrentItemName)) CurrentItemName = p.CurrentItemName;
                OverallProgress = p.OverallProgress;
                TransferSpeedText = p.TransferSpeedText;
                RemainingTimeText = p.RemainingTimeText;
            });

            try
            {
                bool success = await _smartSwitchService.PerformFullBackupAsync(
                    TargetSerialNumber,
                    DefaultBackupFolder,
                    IncludeContacts,
                    IncludeSms,
                    IncludeCallLogs,
                    IncludeFiles,
                    IncludeApps,
                    CompressToZip,
                    progress,
                    _backupCts.Token);

                if (success)
                {
                    _notificationService.ShowSuccess("Smart Switch Backup Complete", "Full backup completed successfully!");
                }
                else
                {
                    _notificationService.ShowError("Backup Failed", "Smart Switch backup encountered errors. Check BackupReport.txt.");
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "Backup operation cancelled by user.";
                _notificationService.ShowNotification("Cancelled", "Backup operation cancelled.", NotificationType.Warning);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Backup Exception", ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task StartFullRestoreAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Backup Folders or ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*",
                Title = "Select Backup Folder or ZIP Archive to Restore"
            };

            string selectedPath = string.Empty;
            if (dialog.ShowDialog() == true)
            {
                selectedPath = dialog.FileName;
            }
            else
            {
                var folderDialog = new OpenFolderDialog { Title = "Select Backup Directory" };
                if (folderDialog.ShowDialog() == true)
                {
                    selectedPath = folderDialog.FolderName;
                }
            }

            if (string.IsNullOrWhiteSpace(selectedPath)) return;

            IsProcessing = true;
            _backupCts = new CancellationTokenSource();
            OverallProgress = 5;
            StatusText = "Parsing Backup Bundle...";
            RemainingTimeText = "Restoring data...";

            var progress = new Progress<BackupProgressInfo>(p =>
            {
                if (!string.IsNullOrWhiteSpace(p.StatusText)) StatusText = p.StatusText;
                if (!string.IsNullOrWhiteSpace(p.CurrentItemName)) CurrentItemName = p.CurrentItemName;
                OverallProgress = p.OverallProgress;
                TransferSpeedText = p.TransferSpeedText;
                RemainingTimeText = p.RemainingTimeText;
            });

            try
            {
                bool success = await _smartSwitchService.PerformFullRestoreAsync(
                    TargetSerialNumber,
                    selectedPath,
                    progress,
                    _backupCts.Token);

                if (success)
                {
                    _notificationService.ShowSuccess("Full Restore Success", "Data restored successfully!");
                }
                else
                {
                    _notificationService.ShowError("Restore Failed", "Smart Switch restore encountered errors.");
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "Restore operation cancelled by user.";
                _notificationService.ShowNotification("Cancelled", "Restore operation cancelled.", NotificationType.Warning);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Restore Exception", ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void CancelOperation()
        {
            _backupCts?.Cancel();
            IsProcessing = false;
            StatusText = "Operation cancelled by user.";
            _notificationService.ShowNotification("Cancelled", "Operation cancelled by user.", NotificationType.Warning);
        }
    }
}

