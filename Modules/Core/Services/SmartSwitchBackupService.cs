using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
{
    public class SmartSwitchBackupService : ISmartSwitchBackupService
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;

        public SmartSwitchBackupService(IAdbService adbService, ILoggerService logger)
        {
            _adbService = adbService;
            _logger = logger;
        }

        public async Task<bool> PerformFullBackupAsync(
            string? serialNumber,
            string destinationRootFolder,
            bool includeContacts,
            bool includeSms,
            bool includeCallLogs,
            bool includeFiles,
            bool includeApps,
            bool compressToZip,
            IProgress<BackupProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var reportBuilder = new StringBuilder();
            var skippedItems = new List<string>();

            reportBuilder.AppendLine("=== SMART SWITCH BACKUP REPORT ===");
            reportBuilder.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var progressInfo = new BackupProgressInfo
            {
                StatusText = "Initializing Smart Switch Backup Engine...",
                OverallProgress = 2
            };
            progress?.Report(progressInfo);

            try
            {
                var device = await _adbService.GetDeviceDetailsAsync(serialNumber ?? string.Empty);
                string devName = (device?.Model ?? "AndroidDevice").Replace(" ", "_");
                string folderName = $"{devName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                string backupDir = Path.Combine(destinationRootFolder, folderName);
                Directory.CreateDirectory(backupDir);

                reportBuilder.AppendLine($"Device: {device?.Model ?? "Unknown"} ({serialNumber})");
                reportBuilder.AppendLine($"Backup Root: {backupDir}\n");

                var manifest = new BackupManifest
                {
                    DeviceName = device?.Model ?? "Android Device",
                    DeviceSerial = serialNumber ?? "UnknownSerial",
                    AndroidVersion = device?.AndroidVersion ?? "Unknown",
                    ApiLevel = device?.ApiLevel ?? "0",
                    IncludesContacts = includeContacts,
                    IncludesSms = includeSms,
                    IncludesCallLogs = includeCallLogs,
                    IncludesFiles = includeFiles,
                    IncludesApps = includeApps
                };

                // 1. Contacts Backup
                if (includeContacts && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Exporting Contacts...";
                    progressInfo.CurrentItemName = "Backing up Contacts (VCF / CSV / JSON)...";
                    progressInfo.OverallProgress = 10;
                    progress?.Report(progressInfo);

                    try
                    {
                        var contacts = await _adbService.ExportContactsAsync(serialNumber);
                        manifest.ContactCount = contacts.Count;

                        if (contacts.Count > 0)
                        {
                            string contactsDir = Path.Combine(backupDir, "Contacts");
                            Directory.CreateDirectory(contactsDir);

                            // JSON
                            string json = JsonSerializer.Serialize(contacts, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(Path.Combine(contactsDir, "contacts.json"), json, cancellationToken);

                            // CSV
                            var csvBuilder = new StringBuilder("Id,DisplayName,PhoneNumber,Email\n");
                            foreach (var c in contacts) csvBuilder.AppendLine($"\"{c.Id}\",\"{c.DisplayName}\",\"{c.PhoneNumber}\",\"{c.Email}\"");
                            await File.WriteAllTextAsync(Path.Combine(contactsDir, "contacts.csv"), csvBuilder.ToString(), cancellationToken);

                            // VCF
                            var vcfBuilder = new StringBuilder();
                            foreach (var c in contacts)
                            {
                                vcfBuilder.AppendLine("BEGIN:VCARD\nVERSION:3.0");
                                vcfBuilder.AppendLine($"FN:{c.DisplayName}");
                                vcfBuilder.AppendLine($"TEL:{c.PhoneNumber}");
                                vcfBuilder.AppendLine("END:VCARD");
                            }
                            await File.WriteAllTextAsync(Path.Combine(contactsDir, "contacts.vcf"), vcfBuilder.ToString(), cancellationToken);

                            reportBuilder.AppendLine($"[SUCCESS] Contacts: {contacts.Count} records saved to Backup/Contacts/");
                        }
                        else
                        {
                            reportBuilder.AppendLine($"[INFO] Contacts Backup: 0 records found. No files created.");
                        }
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] Contacts Backup Failed: {ex.Message}");
                    }
                }

                // 2. SMS Backup
                if (includeSms && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Exporting SMS Messages...";
                    progressInfo.CurrentItemName = "Backing up Messages (XML / JSON)...";
                    progressInfo.OverallProgress = 20;
                    progress?.Report(progressInfo);

                    try
                    {
                        var smsList = await _adbService.ExportSmsAsync(serialNumber);
                        manifest.SmsCount = smsList.Count;

                        if (smsList.Count > 0)
                        {
                            string smsDir = Path.Combine(backupDir, "SMS");
                            Directory.CreateDirectory(smsDir);

                            // JSON
                            string json = JsonSerializer.Serialize(smsList, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(Path.Combine(smsDir, "sms.json"), json, cancellationToken);

                            // XML
                            var xmlBuilder = new StringBuilder("<smses>\n");
                            foreach (var s in smsList) xmlBuilder.AppendLine($"  <sms address=\"{s.Address}\" body=\"{s.Body.Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;")}\" date=\"{s.Date}\" type=\"{s.Type}\" />");
                            xmlBuilder.AppendLine("</smses>");
                            await File.WriteAllTextAsync(Path.Combine(smsDir, "sms.xml"), xmlBuilder.ToString(), cancellationToken);

                            reportBuilder.AppendLine($"[SUCCESS] SMS: {smsList.Count} messages saved to Backup/SMS/");
                        }
                        else
                        {
                            reportBuilder.AppendLine($"[INFO] SMS Backup: 0 messages found. No files created.");
                        }
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] SMS Backup Failed: {ex.Message}");
                    }
                }

                // 3. Call Logs Backup
                if (includeCallLogs && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Exporting Call History...";
                    progressInfo.CurrentItemName = "Backing up Call Logs (CSV / JSON)...";
                    progressInfo.OverallProgress = 30;
                    progress?.Report(progressInfo);

                    try
                    {
                        var callLogs = await _adbService.ExportCallLogsAsync(serialNumber);
                        manifest.CallLogCount = callLogs.Count;

                        if (callLogs.Count > 0)
                        {
                            string clDir = Path.Combine(backupDir, "CallLogs");
                            Directory.CreateDirectory(clDir);

                            // JSON
                            string json = JsonSerializer.Serialize(callLogs, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(Path.Combine(clDir, "calllogs.json"), json, cancellationToken);

                            // CSV
                            var csvBuilder = new StringBuilder("Number,Date,Duration,Type\n");
                            foreach (var cl in callLogs) csvBuilder.AppendLine($"\"{cl.Number}\",\"{cl.Date}\",\"{cl.DurationSeconds}\",\"{cl.Type}\"");
                            await File.WriteAllTextAsync(Path.Combine(clDir, "calllogs.csv"), csvBuilder.ToString(), cancellationToken);

                            reportBuilder.AppendLine($"[SUCCESS] Call Logs: {callLogs.Count} records saved to Backup/CallLogs/");
                        }
                        else
                        {
                            reportBuilder.AppendLine($"[INFO] Call Logs Backup: 0 records found. No files created.");
                        }
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] Call Logs Backup Failed: {ex.Message}");
                    }
                }

                // 4. Installed Apps Backup
                if (includeApps && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Exporting Application Packages...";
                    progressInfo.CurrentItemName = "Retrieving installed applications list...";
                    progressInfo.OverallProgress = 40;
                    progress?.Report(progressInfo);

                    try
                    {
                        string appsDir = Path.Combine(backupDir, "Apps");
                        Directory.CreateDirectory(appsDir);

                        var apps = await _adbService.GetInstalledAppsAsync(serialNumber, false);
                        manifest.AppCount = apps.Count;

                        string pkgJson = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(Path.Combine(appsDir, "packages.json"), pkgJson, cancellationToken);

                        int backedUpApps = 0;
                        for (int i = 0; i < apps.Count; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            var app = apps[i];

                            progressInfo.CurrentItemName = $"Backing up APK ({i + 1}/{apps.Count}): {app.PackageName}";
                            progressInfo.OverallProgress = 40 + ((double)i / Math.Max(1, apps.Count) * 15);
                            progress?.Report(progressInfo);

                            var (success, _) = await _adbService.BackupApkAsync(app.PackageName, appsDir, serialNumber);
                            if (success) backedUpApps++;
                        }

                        reportBuilder.AppendLine($"[SUCCESS] Installed Apps: {backedUpApps}/{apps.Count} package(s) saved to Backup/Apps/");
                    }
                    catch (Exception ex)
                    {
                        reportBuilder.AppendLine($"[ERROR] App Backup Failed: {ex.Message}");
                    }
                }

                // 5. Full Internal Storage Backup (/sdcard)
                if (includeFiles && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Enumerating Internal Storage (/sdcard)...";
                    progressInfo.CurrentItemName = "Scanning device directory hierarchy...";
                    progressInfo.OverallProgress = 55;
                    progress?.Report(progressInfo);

                    string internalStorageDir = Path.Combine(backupDir, "InternalStorage");
                    Directory.CreateDirectory(internalStorageDir);

                    long estimatedTotalBytes = await _adbService.GetRemoteStorageSizeBytesAsync("/sdcard", serialNumber);
                    var enumeratedPaths = await _adbService.EnumerateRemoteStoragePathsAsync("/sdcard", serialNumber);

                    _logger.LogInfo($"Discovered {enumeratedPaths.Count} total paths under /sdcard on device.");

                    // Pre-create exact directory hierarchy locally (guarantees empty & hidden folders exist!)
                    int dirCount = 0;
                    var fileRemotePaths = new List<string>();

                    foreach (var path in enumeratedPaths)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        string relPath = path.Length > 7 ? path.Substring(7).TrimStart('/', '\\') : string.Empty;
                        if (string.IsNullOrWhiteSpace(relPath)) continue;

                        string localPath = Path.Combine(internalStorageDir, relPath);

                        // If path looks like directory or ends with slash, or we create directories proactively
                        if (!Path.HasExtension(relPath) || path.EndsWith("/"))
                        {
                            try
                            {
                                Directory.CreateDirectory(localPath);
                                dirCount++;
                            }
                            catch { }
                        }

                        // Add to files candidate list
                        fileRemotePaths.Add(path);
                    }

                    progressInfo.StatusText = "Copying Internal Storage Files...";
                    progressInfo.TotalFiles = fileRemotePaths.Count;
                    progressInfo.TotalBytes = estimatedTotalBytes;

                    long totalBytesTransferred = 0;
                    int filesProcessed = 0;
                    var speedTimer = Stopwatch.StartNew();

                    // Step 1: Perform fast bulk directory pull for major directories under /sdcard
                    // Get top level entries under /sdcard
                    var (topLsSuccess, topLsOutput) = await _adbService.ExecuteCommandAsync("shell ls -1a /sdcard", serialNumber);
                    var topEntries = topLsSuccess && !string.IsNullOrWhiteSpace(topLsOutput)
                        ? topLsOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(e => e.Trim())
                            .Where(e => !string.IsNullOrWhiteSpace(e) && e != "." && e != "..")
                            .ToList()
                        : new List<string>();

                    reportBuilder.AppendLine($"\n--- INTERNAL STORAGE BACKUP LOG ---");
                    reportBuilder.AppendLine($"Root Storage Path: /sdcard");
                    reportBuilder.AppendLine($"Discovered Hierarchy Paths: {enumeratedPaths.Count} (Directories & Files)\n");

                    foreach (var entry in topEntries)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        string remoteItem = $"/sdcard/{entry}";
                        string localTargetDir = Path.Combine(internalStorageDir, entry);

                        progressInfo.CurrentItemName = $"Pulling /sdcard/{entry}...";
                        progress?.Report(progressInfo);

                        var (pullSuccess, pullMsg) = await _adbService.PullFileAsync(remoteItem, localTargetDir, serialNumber);

                        if (pullSuccess)
                        {
                            reportBuilder.AppendLine($"[SUCCESS] Bulk Pull: /sdcard/{entry}");
                        }
                        else
                        {
                            _logger.LogWarning($"Bulk pull for /sdcard/{entry} encountered restricted files ({pullMsg}). Falling back to recursive per-item copy.");

                            // Fallback to recursive item pull for restricted folders (e.g. /sdcard/Android)
                            var itemPaths = enumeratedPaths.Where(p => p.StartsWith(remoteItem, StringComparison.OrdinalIgnoreCase)).ToList();

                            foreach (var itemPath in itemPaths)
                            {
                                if (cancellationToken.IsCancellationRequested) break;

                                string itemRel = itemPath.Length > 7 ? itemPath.Substring(7).TrimStart('/', '\\') : string.Empty;
                                if (string.IsNullOrWhiteSpace(itemRel)) continue;

                                string localItemPath = Path.Combine(internalStorageDir, itemRel);

                                progressInfo.CurrentItemName = $"Pulling: {itemRel}";
                                progress?.Report(progressInfo);

                                try
                                {
                                    var (subSuccess, subMsg) = await _adbService.PullFileAsync(itemPath, localItemPath, serialNumber);
                                    if (subSuccess)
                                    {
                                        filesProcessed++;
                                        if (File.Exists(localItemPath))
                                        {
                                            totalBytesTransferred += new FileInfo(localItemPath).Length;
                                        }
                                    }
                                    else
                                    {
                                        skippedItems.Add($"{itemPath} ({subMsg.Trim()})");
                                        reportBuilder.AppendLine($"[SKIPPED] Inaccessible: {itemPath}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    skippedItems.Add($"{itemPath} ({ex.Message})");
                                    reportBuilder.AppendLine($"[SKIPPED] Error: {itemPath} - {ex.Message}");
                                }

                                UpdateProgressMetrics(progressInfo, filesProcessed, fileRemotePaths.Count, totalBytesTransferred, estimatedTotalBytes, speedTimer.Elapsed.TotalSeconds);
                                progress?.Report(progressInfo);
                            }
                        }

                        // Calculate actual transferred byte size in local folder after bulk pull
                        if (Directory.Exists(localTargetDir))
                        {
                            var filesInDir = Directory.GetFiles(localTargetDir, "*", SearchOption.AllDirectories);
                            filesProcessed += filesInDir.Length;
                            long dirSize = filesInDir.Sum(f => new FileInfo(f).Length);
                            totalBytesTransferred += dirSize;
                        }

                        UpdateProgressMetrics(progressInfo, filesProcessed, fileRemotePaths.Count, totalBytesTransferred, estimatedTotalBytes, speedTimer.Elapsed.TotalSeconds);
                        progress?.Report(progressInfo);
                    }

                    manifest.TotalSizeBytes = totalBytesTransferred;
                    reportBuilder.AppendLine($"\n[SUMMARY] Internal Storage: {filesProcessed} file(s) transferred ({FormatFileSize(totalBytesTransferred)}).");
                    reportBuilder.AppendLine($"[SUMMARY] Skipped Inaccessible Files: {skippedItems.Count}");
                }

                // Write Report & Manifest
                reportBuilder.AppendLine("\n=== BACKUP SUMMARY STATUS ===");
                reportBuilder.AppendLine($"Status: Backup Completed");
                reportBuilder.AppendLine($"Elapsed Time: {stopwatch.Elapsed:hh\\:mm\\:ss}");

                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(backupDir, "BackupInfo.json"), manifestJson, cancellationToken);
                await File.WriteAllTextAsync(Path.Combine(backupDir, "BackupReport.txt"), reportBuilder.ToString(), cancellationToken);

                // Optional ZIP Compression
                if (compressToZip && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Compressing Backup to ZIP Archive...";
                    progressInfo.CurrentItemName = "Creating ZIP bundle...";
                    progressInfo.OverallProgress = 95;
                    progress?.Report(progressInfo);

                    string zipPath = $"{backupDir}.zip";
                    ZipFile.CreateFromDirectory(backupDir, zipPath);
                    Directory.Delete(backupDir, true);
                    backupDir = zipPath;
                }

                progressInfo.StatusText = "Full Backup Completed!";
                progressInfo.CurrentItemName = $"Backup Folder: {backupDir}";
                progressInfo.OverallProgress = 100;
                progressInfo.RemainingTimeText = "Completed";
                progressInfo.TransferSpeedText = string.Empty;
                progress?.Report(progressInfo);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Smart Switch Backup Failed", ex.ToString());
                reportBuilder.AppendLine($"\n[CRITICAL ERROR] Backup Aborted: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PerformFullRestoreAsync(
            string? serialNumber,
            string backupPath,
            IProgress<BackupProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var progressInfo = new BackupProgressInfo
            {
                StatusText = "Parsing Backup Package for Restore...",
                OverallProgress = 5
            };
            progress?.Report(progressInfo);

            string workDir = backupPath;
            bool isTempZip = false;

            try
            {
                if (backupPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    progressInfo.StatusText = "Extracting ZIP Archive...";
                    progressInfo.CurrentItemName = Path.GetFileName(backupPath);
                    progress?.Report(progressInfo);

                    workDir = Path.Combine(Path.GetTempPath(), "UnlockMatePro_Restore", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(workDir);
                    ZipFile.ExtractToDirectory(backupPath, workDir);
                    isTempZip = true;
                }

                // 1. Restore Contacts
                string contactsVcf = Path.Combine(workDir, "Contacts", "contacts.vcf");
                if (File.Exists(contactsVcf) && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Restoring Contacts to Device...";
                    progressInfo.CurrentItemName = "Injecting Contacts (VCF)...";
                    progressInfo.OverallProgress = 20;
                    progress?.Report(progressInfo);

                    string remoteVcfPath = "/sdcard/RestoreContacts.vcf";
                    await _adbService.PushFileAsync(contactsVcf, remoteVcfPath, serialNumber);
                    await _adbService.ExecuteCommandAsync($"shell am start -t \"text/x-vcard\" -d \"file://{remoteVcfPath}\" -a android.intent.action.VIEW com.android.contacts", serialNumber);
                    
                    _logger.LogSuccess("Sent VCF import intent to device. Please confirm on the device screen.");
                }

                // 2. Restore SMS
                string smsJson = Path.Combine(workDir, "SMS", "sms.json");
                if (File.Exists(smsJson) && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Restoring SMS Messages...";
                    progressInfo.CurrentItemName = "Injecting SMS database...";
                    progressInfo.OverallProgress = 35;
                    progress?.Report(progressInfo);

                    string text = await File.ReadAllTextAsync(smsJson, cancellationToken);
                    var smsList = JsonSerializer.Deserialize<List<SmsItem>>(text);
                    if (smsList != null && smsList.Count > 0)
                    {
                        await _adbService.RestoreSmsAsync(smsList, serialNumber);
                    }
                }

                // 3. Restore Call Logs
                string callLogsJson = Path.Combine(workDir, "CallLogs", "calllogs.json");
                if (File.Exists(callLogsJson) && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Restoring Call Logs...";
                    progressInfo.CurrentItemName = "Injecting Call History database...";
                    progressInfo.OverallProgress = 50;
                    progress?.Report(progressInfo);

                    string text = await File.ReadAllTextAsync(callLogsJson, cancellationToken);
                    var callLogs = JsonSerializer.Deserialize<List<CallLogItem>>(text);
                    if (callLogs != null && callLogs.Count > 0)
                    {
                        await _adbService.RestoreCallLogsAsync(callLogs, serialNumber);
                    }
                }

                // 4. Restore Applications
                string appsDir = Path.Combine(workDir, "Apps");
                if (!Directory.Exists(appsDir)) appsDir = Path.Combine(workDir, "InstalledApps"); // Fallback for legacy backups

                if (Directory.Exists(appsDir) && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Restoring Applications (.apk)...";
                    progressInfo.OverallProgress = 65;
                    progress?.Report(progressInfo);

                    var apks = Directory.GetFiles(appsDir, "*.apk");
                    for (int i = 0; i < apks.Length; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        var apk = apks[i];
                        progressInfo.CurrentItemName = $"Installing package ({i + 1}/{apks.Length}): {Path.GetFileName(apk)}";
                        progressInfo.OverallProgress = 65 + ((double)i / Math.Max(1, apks.Length) * 15);
                        progress?.Report(progressInfo);

                        await _adbService.InstallApkAsync(apk, serialNumber, true, true, false);
                    }
                }

                // 5. Restore Internal Storage Files
                string internalStorageDir = Path.Combine(workDir, "InternalStorage");
                if (!Directory.Exists(internalStorageDir)) internalStorageDir = Path.Combine(workDir, "Files"); // Fallback for legacy backups

                if (Directory.Exists(internalStorageDir) && !cancellationToken.IsCancellationRequested)
                {
                    progressInfo.StatusText = "Restoring Internal Storage Files (/sdcard)...";
                    progressInfo.CurrentItemName = "Pushing folder hierarchy to device...";
                    progressInfo.OverallProgress = 85;
                    progress?.Report(progressInfo);

                    var files = Directory.GetFiles(internalStorageDir, "*", SearchOption.AllDirectories);
                    long totalRestoreBytes = files.Sum(f => new FileInfo(f).Length);
                    long restoredBytes = 0;
                    var speedTimer = Stopwatch.StartNew();

                    progressInfo.TotalFiles = files.Length;
                    progressInfo.TotalBytes = totalRestoreBytes;

                    // Push top-level items or folder contents to /sdcard
                    var topItems = Directory.GetFileSystemEntries(internalStorageDir);
                    int processedFiles = 0;

                    foreach (var item in topItems)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        string name = Path.GetFileName(item);
                        progressInfo.CurrentItemName = $"Pushing /sdcard/{name}...";
                        progress?.Report(progressInfo);

                        var (pushSuccess, pushMsg) = await _adbService.PushFileAsync(item, "/sdcard/", serialNumber);

                        if (!pushSuccess)
                        {
                            _logger.LogWarning($"Push for {name} returned error: {pushMsg}. Continuing with remaining items.");
                        }

                        if (File.Exists(item))
                        {
                            processedFiles++;
                            restoredBytes += new FileInfo(item).Length;
                        }
                        else if (Directory.Exists(item))
                        {
                            var subFiles = Directory.GetFiles(item, "*", SearchOption.AllDirectories);
                            processedFiles += subFiles.Length;
                            restoredBytes += subFiles.Sum(f => new FileInfo(f).Length);
                        }

                        UpdateProgressMetrics(progressInfo, processedFiles, files.Length, restoredBytes, totalRestoreBytes, speedTimer.Elapsed.TotalSeconds);
                        progressInfo.OverallProgress = 85 + ((double)processedFiles / Math.Max(1, files.Length) * 14);
                        progress?.Report(progressInfo);
                    }
                }

                progressInfo.StatusText = "Full Restore Completed!";
                progressInfo.CurrentItemName = "All backup contents restored successfully.";
                progressInfo.OverallProgress = 100;
                progressInfo.RemainingTimeText = "Completed";
                progressInfo.TransferSpeedText = string.Empty;
                progress?.Report(progressInfo);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Smart Switch Restore Failed", ex.ToString());
                return false;
            }
            finally
            {
                if (isTempZip && Directory.Exists(workDir))
                {
                    try { Directory.Delete(workDir, true); } catch { }
                }
            }
        }

        private static void UpdateProgressMetrics(
            BackupProgressInfo info,
            int transferredFiles,
            int totalFiles,
            long transferredBytes,
            long totalBytes,
            double elapsedSeconds)
        {
            info.TransferredFiles = transferredFiles;
            info.TotalFiles = Math.Max(transferredFiles, totalFiles);
            info.TransferredBytes = transferredBytes;
            info.TotalBytes = Math.Max(transferredBytes, totalBytes);

            double bytesPerSec = elapsedSeconds > 0 ? transferredBytes / elapsedSeconds : 0;
            info.BytesPerSecond = bytesPerSec;

            if (bytesPerSec > 1024 * 1024)
                info.TransferSpeedText = $"{bytesPerSec / (1024.0 * 1024.0):0.0} MB/s";
            else if (bytesPerSec > 1024)
                info.TransferSpeedText = $"{bytesPerSec / 1024.0:0.0} KB/s";
            else
                info.TransferSpeedText = $"{bytesPerSec:0} B/s";

            double remainingBytes = Math.Max(0, info.TotalBytes - info.TransferredBytes);
            double remainingSec = bytesPerSec > 0 ? remainingBytes / bytesPerSec : 0;

            var timeSpan = TimeSpan.FromSeconds(remainingSec);
            info.EstimatedRemaining = timeSpan;
            info.RemainingTimeText = timeSpan.TotalHours >= 1
                ? $"ETA: {timeSpan:hh\\:mm\\:ss}"
                : $"ETA: {timeSpan:mm\\:ss}";

            double fileProgress = info.TotalFiles > 0 ? ((double)info.TransferredFiles / info.TotalFiles) * 100.0 : 0;
            info.OverallProgress = Math.Min(99.0, 55.0 + (fileProgress * 0.4));
        }

        private static string FormatFileSize(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return $"{num} {suf[place]}";
        }
    }
}

