using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UnlockMatePro.Services;
using UnlockMatePro.ViewModels;
using UnlockMatePro.Views;

namespace UnlockMatePro
{
    public partial class App : Application
    {
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            SetupCrashHandlerAndLogRotation();

            var splashScreen = new SplashScreenWindow();
            splashScreen.Show();

            var logger = new LoggerService();
            var settingsService = new SettingsService();
            await settingsService.LoadSettingsAsync();

            var apiService = new ApiService(settingsService, logger);
            var authService = new AuthenticationService(apiService, logger);
            _ = await authService.AutoLoginAsync();

            var adbService = new AdbService(logger);
            var fastbootService = new FastbootService(logger);
            var scrcpyService = new ScrcpyService(logger);
            var toolDownloaderService = new ToolDownloaderService(logger);

            var navigationService = new NavigationService();
            var notificationService = new NotificationService();
            var updateService = new UpdateService(logger);

            var mainVm = new MainViewModel(
                adbService,
                fastbootService,
                scrcpyService,
                toolDownloaderService,
                settingsService,
                authService,
                navigationService,
                notificationService,
                logger,
                updateService);

            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            await Task.Delay(1000);

            splashScreen.Close();
            mainWindow.Show();
        }

        private void SetupCrashHandlerAndLogRotation()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unlock Mate Pro", "Logs");
                if (Directory.Exists(logDir))
                {
                    var files = Directory.GetFiles(logDir, "*.txt");
                    foreach (var file in files)
                    {
                        var fi = new FileInfo(file);
                        if (fi.LastWriteTime < DateTime.Now.AddDays(-7))
                        {
                            try { fi.Delete(); } catch { }
                        }
                    }
                }
                else
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            catch { }
        }

        private bool _isShowingCrashDialog = false;

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            if (_isShowingCrashDialog) return;

            try
            {
                _isShowingCrashDialog = true;
                LogCrash(e.Exception);
                MessageBox.Show($"Unlock Mate Pro encountered an unexpected exception:\n{e.Exception.Message}\n\nA diagnostic crash dump has been saved to %APPDATA%\\Unlock Mate Pro\\Logs.", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isShowingCrashDialog = false;
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash(ex);
            }
        }

        private void LogCrash(Exception ex)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unlock Mate Pro", "Logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                string crashFile = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(crashFile, $"CRASH TIME: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nMESSAGE: {ex.Message}\nSTACK TRACE:\n{ex.StackTrace}\n");
            }
            catch { }
        }
    }
}

