using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        private readonly IUpdateService _updateService;
        private readonly ILoggerService _logger;

        private string _appVersion = "1.0.0";
        private string _updateStatusText = "Click button to check for updates";
        private bool _isCheckingUpdates = false;
        private string _releaseNotes = string.Empty;

        public string AppVersion
        {
            get => _appVersion;
            set => SetProperty(ref _appVersion, value);
        }

        public string UpdateStatusText
        {
            get => _updateStatusText;
            set => SetProperty(ref _updateStatusText, value);
        }

        public bool IsCheckingUpdates
        {
            get => _isCheckingUpdates;
            set => SetProperty(ref _isCheckingUpdates, value);
        }

        public string ReleaseNotes
        {
            get => _releaseNotes;
            set => SetProperty(ref _releaseNotes, value);
        }

        public ICommand CheckUpdatesCommand { get; }
        public ICommand OpenGitHubCommand { get; }

        public AboutViewModel(IUpdateService updateService, ILoggerService logger)
        {
            _updateService = updateService;
            _logger = logger;

            CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);
            OpenGitHubCommand = new RelayCommand(() => OpenUrl("https://github.com/adbeasyinstaller/adb-easy-installer"));
        }

        private async Task CheckUpdatesAsync()
        {
            IsCheckingUpdates = true;
            UpdateStatusText = "Checking GitHub releases...";

            var update = await _updateService.CheckForUpdatesAsync();
            IsCheckingUpdates = false;

            if (update.IsUpdateAvailable)
            {
                UpdateStatusText = $"New version v{update.LatestVersion} is available!";
                ReleaseNotes = update.ReleaseNotes;
            }
            else
            {
                UpdateStatusText = "You are using the latest version (v1.0.0).";
                ReleaseNotes = update.ReleaseNotes;
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
