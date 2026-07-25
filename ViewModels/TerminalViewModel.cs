using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class TerminalViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;

        private string? _targetSerialNumber;
        private string _inputCommand = string.Empty;
        private string _terminalOutput = "Unlock Mate Pro Interactive ADB Console v2.0\nType 'help' or any ADB command (e.g. 'shell getprop', 'devices', 'pm list packages')...\n\n";
        private bool _isExecuting = false;

        private readonly List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;

        public ObservableCollection<string> AutoCompleteSuggestions { get; } = new ObservableCollection<string>
        {
            "shell getprop",
            "shell getprop ro.product.model",
            "shell pm list packages",
            "shell pm list packages -3",
            "shell dumpsys battery",
            "shell df -h /sdcard",
            "shell cat /proc/meminfo",
            "devices -l",
            "reboot",
            "reboot bootloader",
            "reboot recovery"
        };

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set => SetProperty(ref _targetSerialNumber, value);
        }

        public string InputCommand
        {
            get => _inputCommand;
            set => SetProperty(ref _inputCommand, value);
        }

        public string TerminalOutput
        {
            get => _terminalOutput;
            set => SetProperty(ref _terminalOutput, value);
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }

        public ICommand ExecuteCommand { get; }
        public ICommand ClearTerminalCommand { get; }
        public ICommand CopyOutputCommand { get; }
        public ICommand SaveLogCommand { get; }
        public ICommand PreviousHistoryCommand { get; }
        public ICommand NextHistoryCommand { get; }

        public TerminalViewModel(IAdbService adbService, ILoggerService logger)
        {
            _adbService = adbService;
            _logger = logger;

            ExecuteCommand = new AsyncRelayCommand(ExecuteConsoleCommandAsync, () => !IsExecuting);
            ClearTerminalCommand = new RelayCommand(() => TerminalOutput = "Console cleared.\n\n");
            CopyOutputCommand = new RelayCommand(() => Clipboard.SetText(TerminalOutput));
            SaveLogCommand = new AsyncRelayCommand(SaveLogToFileAsync);
            PreviousHistoryCommand = new RelayCommand(NavigatePreviousHistory);
            NextHistoryCommand = new RelayCommand(NavigateNextHistory);
        }

        private async Task ExecuteConsoleCommandAsync()
        {
            if (string.IsNullOrWhiteSpace(InputCommand)) return;

            string cmd = InputCommand.Trim();
            InputCommand = string.Empty;

            _commandHistory.Add(cmd);
            _historyIndex = _commandHistory.Count;

            AppendOutput($"$ adb {cmd}\n");
            IsExecuting = true;

            try
            {
                var (success, output) = await _adbService.ExecuteCommandAsync(cmd, TargetSerialNumber);
                AppendOutput(output + "\n\n");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error: {ex.Message}\n\n");
            }
            finally
            {
                IsExecuting = false;
            }
        }

        private void AppendOutput(string text)
        {
            TerminalOutput += text;
        }

        private void NavigatePreviousHistory()
        {
            if (_commandHistory.Count > 0 && _historyIndex > 0)
            {
                _historyIndex--;
                InputCommand = _commandHistory[_historyIndex];
            }
        }

        private void NavigateNextHistory()
        {
            if (_commandHistory.Count > 0 && _historyIndex < _commandHistory.Count - 1)
            {
                _historyIndex++;
                InputCommand = _commandHistory[_historyIndex];
            }
            else
            {
                _historyIndex = _commandHistory.Count;
                InputCommand = string.Empty;
            }
        }

        private async Task SaveLogToFileAsync()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text Log File (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"adb_terminal_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, TerminalOutput);
            }
        }
    }
}
