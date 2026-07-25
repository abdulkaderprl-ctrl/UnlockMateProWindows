using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public interface ILoggerService
    {
        ObservableCollection<LogMessage> LogMessages { get; }

        void Log(LogLevel level, string message, string details = "");
        void LogInfo(string message);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message, string details = "");
        void LogCommand(string command);
        
        Task ExportLogsToFileAsync(string filePath);
        void ClearLogs();
    }
}
