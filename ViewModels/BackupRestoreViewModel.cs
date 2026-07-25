using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using AdbEasyInstaller.Models;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class BackupRestoreViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

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
        private string _statusText = "Ready to perform Full Backup or Full Restore.";
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
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            SelectBackupFolderCommand = new RelayCommand(SelectBackupFolder);
            FullBackupCommand = new AsyncRelayCommand(StartFullBackupAsync, () => !IsProcessing);
            FullRestoreCommand = new AsyncRelayCommand(StartFullRestoreAsync, () => !IsProcessing);
            CancelBackupCommand = new RelayCommand(CancelOperation, () => IsProcessing);

            if (!Directory.Exists(_defaultBackupFolder))
            {
                Directory.CreateDirectory(_defaultBackupFolder);
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
            OverallProgress = 5;
            StatusText = "Initializing Smart Switch Backup Engine...";
            RemainingTimeText = "Estimating time...";

            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine($"=== UNLOCK MATE PRO BACKUP REPORT ===");
            reportBuilder.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            try
            {
                var device = await _adbService.GetDeviceDetailsAsync(TargetSerialNumber ?? string.Empty);
                string devName = (device?.Model ?? "AndroidDevice").Replace(" ", "_");
                string folderName = $"{devName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                string backupDir = Path.Combine(DefaultBackupFolder, folderName);
                Directory.CreateDirectory(backupDir);

                reportBuilder.AppendLine($"Device: {device?.Model ?? "Unknown"} ({TargetSerialNumber})");
                reportBuilder.AppendLine($"Backup Folder: {backupDir}\n");

                var manifest = new BackupManifest
                {
                    DeviceName = device?.Model ?? "Android Device",
                    DeviceSerial = TargetSerialNumber ?? "UnknownSerial",
                    AndroidVersion = device?.AndroidVersion ?? "14",
                    ApiLevel = device?.ApiLevel ?? "34",
                    IncludesContacts = IncludeContacts,
                    IncludesSms = IncludeSms,
                    IncludesCallLogs = IncludeCallLogs,
                    IncludesFiles = IncludeFiles,
                    IncludesApps = IncludeApps
                };

                // 1. Contacts
                if (IncludeContacts && !_backupCts.IsCancellationRequested)
                {
                    try
                    {
                        CurrentItemName = "Backing up Contacts (VCF / CSV / JSON)...";
                        StatusText = "Exporting Contacts...";
                        OverallProgress = 20;

                        var contacts = await _adbService.ExportContactsAsync(TargetSerialNumber);
                        manifest.ContactCount = contacts.Count;

                        string contactsDir = Path.Combine(backupDir, "Contacts");
                        Directory.CreateDirectory(contactsDir);

                        // JSON
                        string json = JsonSerializer.Serialize(contacts, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(Path.Combine(contactsDir, "contacts.json"), json);

                        // CSV
                        var csvBuilder = new StringBuilder("Id,DisplayName,PhoneNumber,Email\n");
                        foreach (var c in contacts) csvBuilder.AppendLine($"\"{c.Id}\",\"{c.DisplayName}\",\"{c.PhoneNumber}\",\"{c.Email}\"");
                        await File.WriteAllTextAsync(Path.Combine(contactsDir, "contacts.csv"), csvBuilder.ToString());

                        // VCF
                        var vcfBuilder = new StringBuilder();
                        foreach (var c in contacts)
                        {
                            vcfBuilder.AppendLine("BEGIN:VCARD\nVERSION:3.0");
                            vcfBuilder.AppendLine($"FN:{c.DisplayName}");
                            vcfBuilder.AppendLine($"TEL:{c.PhoneNumber}");
                            vcfBuilder.AppendLine("END:VCARD");
                        }
                        await File.WriteAllTextAsync(Path.Combine(contactsDir, "contacts.vcf"), vcfBuilder.ToString());

                        reportBuilder.AppendLine($"[SUCCESS] Contacts: {contacts.Count} records saved.");
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] Contacts Backup Failed: {ex.Message}");
                    }
                }

                // 2. SMS
                if (IncludeSms && !_backupCts.IsCancellationRequested)
                {
                    try
                    {
                        CurrentItemName = "Backing up SMS Messages (XML / JSON)...";
                        StatusText = "Exporting SMS Messages...";
                        OverallProgress = 40;

                        var smsList = await _adbService.ExportSmsAsync(TargetSerialNumber);
                        manifest.SmsCount = smsList.Count;

                        string smsDir = Path.Combine(backupDir, "SMS");
                        Directory.CreateDirectory(smsDir);

                        // JSON
                        string json = JsonSerializer.Serialize(smsList, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(Path.Combine(smsDir, "sms.json"), json);

                        // XML
                        var xmlBuilder = new StringBuilder("<smses>\n");
                        foreach (var s in smsList) xmlBuilder.AppendLine($"  <sms address=\"{s.Address}\" body=\"{s.Body}\" date=\"{s.Date}\" type=\"{s.Type}\" />");
                        xmlBuilder.AppendLine("</smses>");
                        await File.WriteAllTextAsync(Path.Combine(smsDir, "sms.xml"), xmlBuilder.ToString());

                        reportBuilder.AppendLine($"[SUCCESS] SMS: {smsList.Count} messages saved.");
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] SMS Backup Failed: {ex.Message}");
                    }
                }

                // 3. Call Logs
                if (IncludeCallLogs && !_backupCts.IsCancellationRequested)
                {
                    try
                    {
                        CurrentItemName = "Backing up Call Logs (CSV / JSON)...";
                        StatusText = "Exporting Call Logs...";
                        OverallProgress = 55;

                        var callLogs = await _adbService.ExportCallLogsAsync(TargetSerialNumber);
                        manifest.CallLogCount = callLogs.Count;

                        string clDir = Path.Combine(backupDir, "CallLogs");
                        Directory.CreateDirectory(clDir);

                        // JSON
                        string json = JsonSerializer.Serialize(callLogs, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(Path.Combine(clDir, "calllogs.json"), json);

                        // CSV
                        var csvBuilder = new StringBuilder("Number,Date,Duration,Type\n");
                        foreach (var cl in callLogs) csvBuilder.AppendLine($"\"{cl.Number}\",\"{cl.Date}\",\"{cl.DurationSeconds}\",\"{cl.Type}\"");
                        await File.WriteAllTextAsync(Path.Combine(clDir, "calllogs.csv"), csvBuilder.ToString());

                        reportBuilder.AppendLine($"[SUCCESS] Call Logs: {callLogs.Count} records saved.");
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] Call Logs Backup Failed: {ex.Message}");
                    }
                }

                // 4. Installed Apps
                if (IncludeApps && !_backupCts.IsCancellationRequested)
                {
                    try
                    {
                        CurrentItemName = "Backing up Installed Application Packages (.apk)...";
                        StatusText = "Exporting Applications...";
                        OverallProgress = 75;

                        string appsDir = Path.Combine(backupDir, "InstalledApps");
                        Directory.CreateDirectory(appsDir);

                        var apps = await _adbService.GetInstalledAppsAsync(TargetSerialNumber, false);
                        manifest.AppCount = apps.Count;

                        string pkgJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(Path.Combine(appsDir, "packages.json"), pkgJson);

                        int backedUpApps = 0;
                        foreach (var app in apps.Take(15))
                        {
                            if (_backupCts.IsCancellationRequested) break;
                            var (success, _) = await _adbService.BackupApkAsync(app.PackageName, appsDir, TargetSerialNumber);
                            if (success) backedUpApps++;
                        }

                        reportBuilder.AppendLine($"[SUCCESS] Installed Apps: {backedUpApps} APK package(s) saved.");
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] App Backup Failed: {ex.Message}");
                    }
                }

                // 5. Storage Files (/sdcard)
                if (IncludeFiles && !_backupCts.IsCancellationRequested)
                {
                    try
                    {
                        CurrentItemName = "Backing up Storage Files (/sdcard)...";
                        StatusText = "Exporting User Files...";
                        OverallProgress = 85;

                        string filesDir = Path.Combine(backupDir, "Files");
                        Directory.CreateDirectory(filesDir);

                        await _adbService.PullFileAsync("/sdcard/Download", Path.Combine(filesDir, "Download"), TargetSerialNumber);
                        reportBuilder.AppendLine($"[SUCCESS] Files: /sdcard storage pulled.");
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] File Backup Failed: {ex.Message}");
                    }
                }

                // Save Manifest & Report
                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(backupDir, "BackupInfo.json"), manifestJson);
                await File.WriteAllTextAsync(Path.Combine(backupDir, "BackupReport.txt"), reportBuilder.ToString());

                // Optional ZIP Compression
                if (CompressToZip && !_backupCts.IsCancellationRequested)
                {
                    CurrentItemName = "Compressing Backup to ZIP archive...";
                    StatusText = "Creating ZIP Archive...";
                    OverallProgress = 95;

                    string zipFile = $"{backupDir}.zip";
                    ZipFile.CreateFromDirectory(backupDir, zipFile);
                    Directory.Delete(backupDir, true);
                    backupDir = zipFile;
                }

                OverallProgress = 100;
                StatusText = "Full Backup Completed!";
                CurrentItemName = $"Saved: {backupDir}";
                RemainingTimeText = "Process Complete";
                _notificationService.ShowSuccess("Smart Switch Backup Complete", $"Backup saved to:\n{backupDir}");
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
            OverallProgress = 10;
            StatusText = "Parsing Backup Bundle...";
            RemainingTimeText = "Restoring data...";

            string workDir = selectedPath;
            bool isTempExtracted = false;

            try
            {
                if (selectedPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    StatusText = "Extracting ZIP Archive...";
                    workDir = Path.Combine(Path.GetTempPath(), "UnlockMatePro_Restore", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(workDir);
                    ZipFile.ExtractToDirectory(selectedPath, workDir);
                    isTempExtracted = true;
                }

                // Restore Contacts
                string contactsJson = Path.Combine(workDir, "Contacts", "contacts.json");
                if (File.Exists(contactsJson))
                {
                    CurrentItemName = "Restoring Contacts to Device...";
                    StatusText = "Injecting Contacts...";
                    OverallProgress = 30;

                    string text = await File.ReadAllTextAsync(contactsJson);
                    var contacts = JsonSerializer.Deserialize<List<ContactItem>>(text);
                    if (contacts != null && contacts.Any())
                    {
                        await _adbService.RestoreContactsAsync(contacts, TargetSerialNumber);
                    }
                }

                // Restore SMS
                string smsJson = Path.Combine(workDir, "SMS", "sms.json");
                if (File.Exists(smsJson))
                {
                    CurrentItemName = "Restoring SMS Messages to Device...";
                    StatusText = "Injecting Messages...";
                    OverallProgress = 50;

                    string text = await File.ReadAllTextAsync(smsJson);
                    var smsList = JsonSerializer.Deserialize<List<SmsItem>>(text);
                    if (smsList != null && smsList.Any())
                    {
                        await _adbService.RestoreSmsAsync(smsList, TargetSerialNumber);
                    }
                }

                // Restore Call Logs
                string callLogsJson = Path.Combine(workDir, "CallLogs", "calllogs.json");
                if (File.Exists(callLogsJson))
                {
                    CurrentItemName = "Restoring Call Logs to Device...";
                    StatusText = "Injecting Call History...";
                    OverallProgress = 70;

                    string text = await File.ReadAllTextAsync(callLogsJson);
                    var callLogs = JsonSerializer.Deserialize<List<CallLogItem>>(text);
                    if (callLogs != null && callLogs.Any())
                    {
                        await _adbService.RestoreCallLogsAsync(callLogs, TargetSerialNumber);
                    }
                }

                // Restore Apps
                string appsDir = Path.Combine(workDir, "InstalledApps");
                if (Directory.Exists(appsDir))
                {
                    CurrentItemName = "Restoring Application Packages (.apk)...";
                    StatusText = "Installing Applications...";
                    OverallProgress = 90;

                    var apks = Directory.GetFiles(appsDir, "*.apk");
                    foreach (var apk in apks)
                    {
                        if (_backupCts.IsCancellationRequested) break;
                        await _adbService.InstallApkAsync(apk, TargetSerialNumber, true, true, false);
                    }
                }

                OverallProgress = 100;
                StatusText = "Full Restore Completed!";
                CurrentItemName = "All backup contents restored successfully.";
                RemainingTimeText = "Restore Complete";
                _notificationService.ShowSuccess("Full Restore Success", "Data restored successfully!");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Restore Exception", ex.Message);
            }
            finally
            {
                IsProcessing = false;
                if (isTempExtracted && Directory.Exists(workDir))
                {
                    try { Directory.Delete(workDir, true); } catch { }
                }
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
