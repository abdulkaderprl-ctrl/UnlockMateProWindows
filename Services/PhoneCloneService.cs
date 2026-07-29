using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
{
    public class PhoneCloneService : IPhoneCloneService
    {
        private readonly IAdbService _adbService;
        private readonly ISmartSwitchBackupService _smartSwitchService;
        private readonly ILoggerService _logger;

        public PhoneCloneService(
            IAdbService adbService,
            ISmartSwitchBackupService smartSwitchService,
            ILoggerService logger)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
            _smartSwitchService = smartSwitchService ?? throw new ArgumentNullException(nameof(smartSwitchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ClonePhoneToPhoneAsync(
            PhoneCloneOptions options,
            IProgress<PhoneCloneProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(options.SourceDeviceSerial))
            {
                _logger.LogError("[PhoneCloneService] Source device serial number is invalid or empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.TargetDeviceSerial))
            {
                _logger.LogError("[PhoneCloneService] Target device serial number is invalid or empty.");
                return false;
            }

            if (string.Equals(options.SourceDeviceSerial, options.TargetDeviceSerial, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[PhoneCloneService] Source and Target device cannot be the same device.");
                return false;
            }

            string stagingDir = Path.Combine(Path.GetTempPath(), "PhoneCloneStaging_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDir);

            var stopwatch = Stopwatch.StartNew();

            var progressInfo = new PhoneCloneProgressInfo
            {
                StepTitle = "Initializing Phone-to-Phone Clone...",
                OverallProgress = 2,
                LogMessage = $"Starting Clone: Source [{options.SourceDeviceSerial}] -> Target [{options.TargetDeviceSerial}]"
            };
            progress?.Report(progressInfo);

            try
            {
                _logger.LogInfo($"[PhoneCloneService] Initiating Phone Clone from Source [{options.SourceDeviceSerial}] to Target [{options.TargetDeviceSerial}] via staging directory '{stagingDir}'...");

                // -------------------------------------------------------------
                // Phase 1: Source Data Extraction (Backup)
                // -------------------------------------------------------------
                progressInfo.StepTitle = "Phase 1/2: Extracting Data from Source Device...";
                progressInfo.OverallProgress = 5;
                progressInfo.LogMessage = "Extracting contacts, messages, apps, and internal storage files from source phone...";
                progress?.Report(progressInfo);

                var backupProgress = new Progress<BackupProgressInfo>(p =>
                {
                    progressInfo.CurrentItemName = p.CurrentItemName;
                    progressInfo.StatusText = p.StatusText;
                    progressInfo.TransferSpeedText = p.TransferSpeedText;
                    progressInfo.RemainingTimeText = p.RemainingTimeText;
                    // Map Phase 1 to 5% - 50% overall progress
                    progressInfo.OverallProgress = 5 + (p.OverallProgress * 0.45);
                    progressInfo.LogMessage = $"Source Backup: {p.StatusText} ({p.CurrentItemName})";
                    progress?.Report(progressInfo);
                });

                bool backupSuccess = await _smartSwitchService.PerformFullBackupAsync(
                    options.SourceDeviceSerial,
                    stagingDir,
                    options.IncludeContacts,
                    options.IncludeSms,
                    options.IncludeCallLogs,
                    options.IncludeInternalStorage,
                    options.IncludeApps,
                    compressToZip: false,
                    backupProgress,
                    cancellationToken);

                if (!backupSuccess)
                {
                    _logger.LogError("[PhoneCloneService] Phase 1 failed: Source device data extraction was unsuccessful.");
                    progressInfo.LogMessage = "ERROR: Failed to extract data from source device.";
                    progress?.Report(progressInfo);
                    return false;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("[PhoneCloneService] Operation cancelled by user after Phase 1.");
                    return false;
                }

                // Locate generated backup directory inside stagingDir
                var backupSubDirs = Directory.GetDirectories(stagingDir);
                if (backupSubDirs.Length == 0)
                {
                    _logger.LogError("[PhoneCloneService] Staging backup directory was not found.");
                    return false;
                }
                string actualBackupPath = backupSubDirs[0];

                _logger.LogInfo($"[PhoneCloneService] Source data extraction complete. Staging folder: '{actualBackupPath}'. Starting Phase 2 (Target Restore & App Installation)...");

                // -------------------------------------------------------------
                // Phase 2: Target Device Restore & App Installation
                // -------------------------------------------------------------
                progressInfo.StepTitle = "Phase 2/2: Restoring & Installing Data on Target Device...";
                progressInfo.OverallProgress = 50;
                progressInfo.LogMessage = "Injecting files, installing APKs, and restoring contacts/SMS on target phone...";
                progress?.Report(progressInfo);

                var restoreProgress = new Progress<BackupProgressInfo>(p =>
                {
                    progressInfo.CurrentItemName = p.CurrentItemName;
                    progressInfo.StatusText = p.StatusText;
                    progressInfo.TransferSpeedText = p.TransferSpeedText;
                    progressInfo.RemainingTimeText = p.RemainingTimeText;
                    // Map Phase 2 to 50% - 98% overall progress
                    progressInfo.OverallProgress = 50 + (p.OverallProgress * 0.48);
                    progressInfo.LogMessage = $"Target Restore: {p.StatusText} ({p.CurrentItemName})";
                    progress?.Report(progressInfo);
                });

                bool restoreSuccess = await _smartSwitchService.PerformFullRestoreAsync(
                    options.TargetDeviceSerial,
                    actualBackupPath,
                    restoreProgress,
                    cancellationToken);

                if (!restoreSuccess)
                {
                    _logger.LogError("[PhoneCloneService] Phase 2 failed: Target device restore/installation encountered errors.");
                    progressInfo.LogMessage = "ERROR: Target device restore encountered errors.";
                    progress?.Report(progressInfo);
                    return false;
                }

                progressInfo.StepTitle = "Phone-to-Phone Clone Completed Successfully!";
                progressInfo.OverallProgress = 100;
                progressInfo.LogMessage = $"SUCCESS: Phone clone completed in {stopwatch.Elapsed:mm\\:ss}!";
                progress?.Report(progressInfo);

                _logger.LogInfo($"[PhoneCloneService] Phone-to-Phone Clone completed successfully in {stopwatch.Elapsed.TotalSeconds:F1}s.");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[PhoneCloneService] Phone clone operation was cancelled by user.");
                progressInfo.LogMessage = "CANCELLED: Phone clone operation was cancelled.";
                progress?.Report(progressInfo);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PhoneCloneService] Exception during phone clone: {ex.Message}");
                progressInfo.LogMessage = $"EXCEPTION: {ex.Message}";
                progress?.Report(progressInfo);
                return false;
            }
            finally
            {
                // Cleanup staging folder asynchronously
                try
                {
                    if (Directory.Exists(stagingDir))
                    {
                        Directory.Delete(stagingDir, recursive: true);
                        _logger.LogInfo($"[PhoneCloneService] Temporary staging folder deleted: '{stagingDir}'.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[PhoneCloneService] Could not delete temp staging folder '{stagingDir}': {ex.Message}");
                }
            }
        }
    }
}

