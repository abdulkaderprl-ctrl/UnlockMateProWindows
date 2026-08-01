using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnlockMatePro.Services
{
    public class PhoneCloneOptions
    {
        public string SourceDeviceSerial { get; set; } = string.Empty;
        public string TargetDeviceSerial { get; set; } = string.Empty;
        public bool IncludeContacts { get; set; } = true;
        public bool IncludeSms { get; set; } = true;
        public bool IncludeCallLogs { get; set; } = true;
        public bool IncludeApps { get; set; } = true;
        public bool IncludeInternalStorage { get; set; } = true;
        public bool IncludeFiles
        {
            get => IncludeInternalStorage;
            set => IncludeInternalStorage = value;
        }
    }

    public class PhoneCloneProgressInfo
    {
        public string StepTitle { get; set; } = string.Empty;
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
        public string LogMessage { get; set; } = string.Empty;
    }

    public interface IPhoneCloneService
    {
        Task<bool> ClonePhoneToPhoneAsync(
            PhoneCloneOptions options,
            IProgress<PhoneCloneProgressInfo> progress,
            CancellationToken cancellationToken);
    }
}

