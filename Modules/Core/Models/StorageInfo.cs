using System;

namespace UnlockMatePro.Models
{
    public class StorageInfo
    {
        public long TotalBytes { get; set; } = 0;
        public long UsedBytes { get; set; } = 0;
        public long FreeBytes { get; set; } = 0;

        public double PercentageUsed
        {
            get
            {
                if (TotalBytes <= 0) return 0;
                return Math.Min(100.0, Math.Max(0.0, ((double)UsedBytes / TotalBytes) * 100.0));
            }
        }

        public string FormattedTotal => FormatBytes(TotalBytes);
        public string FormattedUsed => FormatBytes(UsedBytes);
        public string FormattedFree => FormatBytes(FreeBytes);

        public string SummaryText => TotalBytes > 0
            ? $"{FormattedUsed} used of {FormattedTotal} ({PercentageUsed:F1}%) — {FormattedFree} free"
            : "Storage Info Unavailable";

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1073741824) return $"{bytes / 1073741824.0:F1} GB";
            if (bytes >= 1048576) return $"{bytes / 1048576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}

