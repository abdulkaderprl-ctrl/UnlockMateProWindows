using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class LogcatViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly INotificationService _notificationService;

        private CancellationTokenSource? _logcatCts;
        private bool _isStreaming = false;
        private string? _targetSerialNumber;
        private string _logOutput = string.Empty;

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set => SetProperty(ref _targetSerialNumber, value);
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set => SetProperty(ref _isStreaming, value);
        }

        public string LogOutput
        {
            get => _logOutput;
            set => SetProperty(ref _logOutput, value);
        }

        public ICommand StartLogcatCommand { get; }
        public ICommand StopLogcatCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand SaveLogCommand { get; }

        public LogcatViewModel(IAdbService adbService, INotificationService notificationService)
        {
            _adbService = adbService;
            _notificationService = notificationService;

            StartLogcatCommand = new AsyncRelayCommand(StartLogcatAsync, () => !IsStreaming);
            StopLogcatCommand = new RelayCommand(StopLogcat, () => IsStreaming);
            ClearLogCommand = new RelayCommand(ClearLog);
            SaveLogCommand = new AsyncRelayCommand(SaveLogAsync, () => !string.IsNullOrEmpty(LogOutput));
        }

        private async Task StartLogcatAsync()
        {
            if (string.IsNullOrWhiteSpace(TargetSerialNumber))
            {
                _notificationService.ShowError("Error", "No device selected.");
                return;
            }

            IsStreaming = true;
            _logcatCts = new CancellationTokenSource();
            LogOutput = "Starting logcat...\n";

            try
            {
                // In a real live log, we would read the stream continuously.
                // For simplicity here, we'll fetch logs periodically or execute a command that streams.
                // Assuming IAdbService ExecuteCommandAsync completes when process ends, we can't easily stream here unless we build it.
                // But we can just fetch the last 1000 lines.
                var (success, output) = await _adbService.ExecuteCommandAsync("shell logcat -d -t 1000", TargetSerialNumber, _logcatCts.Token);
                if (success)
                {
                    LogOutput = output;
                }
                else
                {
                    LogOutput = $"Error: {output}";
                }
            }
            catch (OperationCanceledException)
            {
                LogOutput += "\n[Logcat stopped by user]";
            }
            finally
            {
                IsStreaming = false;
            }
        }

        private void StopLogcat()
        {
            if (_logcatCts != null && !_logcatCts.IsCancellationRequested)
            {
                _logcatCts.Cancel();
            }
        }

        private void ClearLog()
        {
            LogOutput = string.Empty;
        }

        private async Task SaveLogAsync()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ADB Easy Installer", "Logs");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"Logcat_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            
            await File.WriteAllTextAsync(file, LogOutput);
            _notificationService.ShowSuccess("Log Saved", $"Saved to {file}");
        }
    }
}
