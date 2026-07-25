using System;

namespace AdbEasyInstaller.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Command
    }

    public class LogMessage
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");
        public string Header => $"[{FormattedTimestamp}] [{Level.ToString().ToUpper()}] {Message}";
    }
}
