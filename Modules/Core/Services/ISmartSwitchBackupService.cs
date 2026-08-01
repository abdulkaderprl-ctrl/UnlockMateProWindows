using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnlockMatePro.Services
{
    public class BackupProgressInfo
    {
        public string StatusText { get; set; } = string.Empty;
        public string CurrentItemName { get; set; } = string.Empty;
        public int TransferredFiles { get; set; }
        public int TotalFiles { get; set; }
        public long TransferredBytes { get; set; }
        public long TotalBytes { get; set; }
        public double BytesPerSecond { get; set; }
        public TimeSpan EstimatedRemaining { get; set; }
        public double OverallProgress { get; set; }
        public string TransferSpeedText { get; set; } = string.Empty;
        public string RemainingTimeText { get; set; } = string.Empty;
    }

    public interface ISmartSwitchBackupService
    {
        Task<bool> PerformFullBackupAsync(
            string? serialNumber,
            string destinationRootFolder,
            bool includeContacts,
            bool includeSms,
            bool includeCallLogs,
            bool includeFiles,
            bool includeApps,
            bool compressToZip,
            IProgress<BackupProgressInfo> progress,
            CancellationToken cancellationToken);

        Task<bool> PerformFullRestoreAsync(
            string? serialNumber,
            string backupPath,
            IProgress<BackupProgressInfo> progress,
            CancellationToken cancellationToken);
    }
}

