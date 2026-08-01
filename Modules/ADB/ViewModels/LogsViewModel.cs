using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class LogsViewModel : ViewModelBase
    {
        private readonly ILoggerService _logger;

        private string _logLevelFilter = "ALL";
        private string _searchFilter = string.Empty;
        private string _logOutput = "ADB System Log & Logcat Stream Reader v2.0\n--------------------------------------------\n";

        public ObservableCollection<string> LogLevels { get; } = new ObservableCollection<string>
        {
            "ALL",
            "VERBOSE",
            "DEBUG",
            "INFO",
            "WARNING",
            "ERROR",
            "FATAL"
        };

        public string LogLevelFilter
        {
            get => _logLevelFilter;
            set
            {
                if (SetProperty(ref _logLevelFilter, value))
                {
                    RefreshLogView();
                }
            }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                {
                    RefreshLogView();
                }
            }
        }

        public string LogOutput
        {
            get => _logOutput;
            set => SetProperty(ref _logOutput, value);
        }

        public ICommand ClearLogCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand SaveLogCommand { get; }

        public LogsViewModel(ILoggerService logger)
        {
            _logger = logger;

            ClearLogCommand = new RelayCommand(ClearLogs);
            CopyLogCommand = new RelayCommand(() => Clipboard.SetText(LogOutput));
            SaveLogCommand = new AsyncRelayCommand(SaveLogToFileAsync);

            RefreshLogView();
        }

        private void RefreshLogView()
        {
            var logs = _logger.LogMessages.Select(m => $"[{m.Timestamp:HH:mm:ss}] [{m.Level}] {m.Message}").ToList();

            if (!string.IsNullOrWhiteSpace(LogLevelFilter) && LogLevelFilter != "ALL")
            {
                logs = logs.Where(l => l.Contains($"[{LogLevelFilter}]", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                logs = logs.Where(l => l.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            LogOutput = string.Join(Environment.NewLine, logs);
        }

        private void ClearLogs()
        {
            _logger.ClearLogs();
            LogOutput = "Logs cleared.\n";
        }

        private async Task SaveLogToFileAsync()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text Log (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"logcat_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                await _logger.ExportLogsToFileAsync(dialog.FileName);
            }
        }
    }
}

