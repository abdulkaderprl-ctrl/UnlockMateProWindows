using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly string _logFolderPath;

        public ObservableCollection<LogMessage> LogMessages { get; } = new ObservableCollection<LogMessage>();

        public LoggerService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logFolderPath = Path.Combine(appData, "AdbEasyInstaller", "logs");
            Directory.CreateDirectory(_logFolderPath);

            LogInfo("Application logger initialized.");
        }

        public void Log(LogLevel level, string message, string details = "")
        {
            var logEntry = new LogMessage
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Details = details
            };

            // Dispatch to UI thread safely
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LogMessages.Add(logEntry);
                    // Keep buffer under 1000 lines
                    if (LogMessages.Count > 1000)
                    {
                        LogMessages.RemoveAt(0);
                    }
                });
            }

            // Append to daily log file
            _ = AppendToFileAsync(logEntry);
        }

        public void LogInfo(string message) => Log(LogLevel.Info, message);
        public void LogSuccess(string message) => Log(LogLevel.Success, message);
        public void LogWarning(string message) => Log(LogLevel.Warning, message);
        public void LogError(string message, string details = "") => Log(LogLevel.Error, message, details);
        public void LogCommand(string command) => Log(LogLevel.Command, command);

        public async Task ExportLogsToFileAsync(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ADB EASY INSTALLER EXECUTION LOG ===");
            sb.AppendLine($"Export Date: {DateTime.Now}");
            sb.AppendLine("----------------------------------------");

            foreach (var log in LogMessages)
            {
                sb.AppendLine(log.Header);
                if (!string.IsNullOrWhiteSpace(log.Details))
                {
                    sb.AppendLine($"  Details: {log.Details}");
                }
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());
        }

        public void ClearLogs()
        {
            LogMessages.Clear();
            LogInfo("Logs cleared.");
        }

        private async Task AppendToFileAsync(LogMessage log)
        {
            try
            {
                string logFile = Path.Combine(_logFolderPath, $"log_{DateTime.Now:yyyyMMdd}.txt");
                string line = $"{log.Header}\n";
                await File.AppendAllTextAsync(logFile, line);
            }
            catch
            {
                // Ignore file lock conflicts during logging
            }
        }
    }
}
