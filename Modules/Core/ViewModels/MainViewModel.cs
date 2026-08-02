using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly IFastbootService _fastbootService;
        private readonly IScrcpyService _scrcpyService;
        private readonly IToolDownloaderService _toolDownloaderService;
        private readonly ISettingsService _settingsService;
        private readonly IAuthenticationService _authService;
        private readonly INavigationService _navigationService;
        private readonly INotificationService _notificationService;
        private readonly ILoggerService _logger;
        private readonly IUpdateService _updateService;

        private ViewModelBase _currentViewModel;
        private string _activeViewName = "Dashboard";
        private AdbDevice? _selectedDevice;
        private string _adbStatusText = "Initializing ADB...";
        private bool _isAdbReady = false;
        private bool _isToastVisible = false;
        private string _toastTitle = string.Empty;
        private string _toastMessage = string.Empty;
        private NotificationType _toastType = NotificationType.Info;

        private readonly DispatcherTimer _deviceMonitorTimer;
        private readonly DispatcherTimer _toastTimer;

        // Sub-ViewModels
        public DashboardViewModel DashboardVM { get; }
        public DeviceDetectionViewModel DeviceDetectionVM { get; }
        public ApkInstallViewModel ApkInstallVM { get; }
        public FastbootToolsViewModel FastbootToolsVM { get; }
        public ScrcpyViewModel ScrcpyVM { get; }
        public FileExplorerViewModel FileExplorerVM { get; }
        public PhoneCloneViewModel PhoneCloneVM { get; }
        public BackupRestoreViewModel BackupRestoreVM { get; }
        public TerminalViewModel TerminalVM { get; }
        public ScreenshotGalleryViewModel ScreenshotGalleryVM { get; }
        public ApkToolsViewModel ApkToolsVM { get; }
        public AdvancedToolsViewModel AdvancedToolsVM { get; }
        public AdbToolsViewModel AdbToolsVM { get; }
        public ApkManagementViewModel ApkManagementVM { get; }
        public AuthViewModel AuthVM { get; }
        public UserProfileViewModel UserProfileVM { get; }
        public SettingsViewModel SettingsVM { get; }
        public LogsViewModel LogsVM { get; }
        public AboutViewModel AboutVM { get; }

        // Brand Placeholders
        public BrandPlaceholderViewModel QualcommVM { get; } = new BrandPlaceholderViewModel("Qualcomm");
        public BrandPlaceholderViewModel MtkVM { get; } = new BrandPlaceholderViewModel("MTK");
        public BrandPlaceholderViewModel SamsungVM { get; } = new BrandPlaceholderViewModel("Samsung");
        public BrandPlaceholderViewModel XiaomiVM { get; } = new BrandPlaceholderViewModel("Xiaomi");
        public BrandPlaceholderViewModel OppoVM { get; } = new BrandPlaceholderViewModel("OPPO");
        public BrandPlaceholderViewModel VivoVM { get; } = new BrandPlaceholderViewModel("VIVO");
        public BrandPlaceholderViewModel RealmeVM { get; } = new BrandPlaceholderViewModel("Realme");
        public BrandPlaceholderViewModel HuaweiVM { get; } = new BrandPlaceholderViewModel("Huawei");
        public BrandPlaceholderViewModel HonorVM { get; } = new BrandPlaceholderViewModel("Honor");
        public BrandPlaceholderViewModel MotorolaVM { get; } = new BrandPlaceholderViewModel("Motorola");
        public BrandPlaceholderViewModel NokiaVM { get; } = new BrandPlaceholderViewModel("Nokia");
        public BrandPlaceholderViewModel SpdVM { get; } = new BrandPlaceholderViewModel("SPD");
        public BrandPlaceholderViewModel AppleVM { get; } = new BrandPlaceholderViewModel("Apple");
        public BrandPlaceholderViewModel FrpVM { get; } = new BrandPlaceholderViewModel("FRP");

        public ObservableCollection<AdbDevice> ConnectedDevices { get; } = new ObservableCollection<AdbDevice>();

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public string ActiveViewName
        {
            get => _activeViewName;
            set => SetProperty(ref _activeViewName, value);
        }

        public AdbDevice? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    UpdateActiveDeviceState();
                }
            }
        }

        public string AdbStatusText
        {
            get => _adbStatusText;
            set => SetProperty(ref _adbStatusText, value);
        }

        public bool IsAdbReady
        {
            get => _isAdbReady;
            set => SetProperty(ref _isAdbReady, value);
        }

        public bool IsUserAuthenticated => _authService.IsAuthenticated;
        public string UserFullName => _authService.CurrentUser?.FullName ?? "Account";
        public string UserPlanBadge => _authService.CurrentUser?.PlanName ?? "Free";

        public bool IsToastVisible
        {
            get => _isToastVisible;
            set => SetProperty(ref _isToastVisible, value);
        }

        public string ToastTitle
        {
            get => _toastTitle;
            set => SetProperty(ref _toastTitle, value);
        }

        public string ToastMessage
        {
            get => _toastMessage;
            set => SetProperty(ref _toastMessage, value);
        }

        public NotificationType ToastType
        {
            get => _toastType;
            set => SetProperty(ref _toastType, value);
        }

        // Navigation Commands
        public ICommand NavigateCommand { get; }
        public ICommand RefreshDevicesCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand OpenAccountCommand { get; }
        public ICommand DismissToastCommand { get; }

        public MainViewModel(
            IAdbService adbService,
            IFastbootService fastbootService,
            IScrcpyService scrcpyService,
            IToolDownloaderService toolDownloaderService,
            ISettingsService settingsService,
            IAuthenticationService authService,
            INavigationService navigationService,
            INotificationService notificationService,
            ILoggerService logger,
            IUpdateService updateService)
        {
            _adbService = adbService;
            _fastbootService = fastbootService;
            _scrcpyService = scrcpyService;
            _toolDownloaderService = toolDownloaderService;
            _settingsService = settingsService;
            _authService = authService;
            _navigationService = navigationService;
            _notificationService = notificationService;
            _logger = logger;
            _updateService = updateService;

            // Instantiate Sub-ViewModels
            DashboardVM = new DashboardViewModel(_adbService, _logger);
            DeviceDetectionVM = new DeviceDetectionViewModel(_adbService, _logger, _notificationService);
            ApkInstallVM = new ApkInstallViewModel(_adbService, _logger, _notificationService, _settingsService);
            FastbootToolsVM = new FastbootToolsViewModel(_fastbootService, _logger, _notificationService);
            ScrcpyVM = new ScrcpyViewModel(_scrcpyService, _settingsService, _logger, _notificationService);
            FileExplorerVM = new FileExplorerViewModel(_adbService, _logger, _notificationService);
            var smartSwitchBackupService = new SmartSwitchBackupService(_adbService, _logger);
            var phoneCloneService = new PhoneCloneService(_adbService, smartSwitchBackupService, _logger);
            PhoneCloneVM = new PhoneCloneViewModel(_adbService, phoneCloneService, _notificationService, _logger, async () => await RefreshDevicesAsync());
            BackupRestoreVM = new BackupRestoreViewModel(_adbService, _logger, _notificationService, smartSwitchBackupService);
            TerminalVM = new TerminalViewModel(_adbService, _logger);
            ScreenshotGalleryVM = new ScreenshotGalleryViewModel(_adbService, _logger, _notificationService);
            ApkToolsVM = new ApkToolsViewModel(_adbService, _logger, _notificationService);
            AdvancedToolsVM = new AdvancedToolsViewModel(_adbService, _logger, _notificationService);
            AdbToolsVM = new AdbToolsViewModel(_adbService, _logger, _notificationService);
            ApkManagementVM = new ApkManagementViewModel(_adbService, _logger, _notificationService);
            AuthVM = new AuthViewModel(_authService, _notificationService, _logger);
            UserProfileVM = new UserProfileViewModel(_authService, _notificationService);
            SettingsVM = new SettingsViewModel(_settingsService, _adbService, _scrcpyService, _toolDownloaderService, _logger, _notificationService);
            LogsVM = new LogsViewModel(_logger);
            AboutVM = new AboutViewModel(_updateService, _logger);

            _currentViewModel = DashboardVM;

            // Commands
            NavigateCommand = new RelayCommand(param => NavigateTo(param?.ToString() ?? "Dashboard"));
            RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
            OpenAccountCommand = new RelayCommand(OpenAccount);
            DismissToastCommand = new RelayCommand(() => IsToastVisible = false);

            _navigationService.ViewChanged += OnViewChanged;
            _notificationService.NotificationTriggered += OnNotificationTriggered;
            _authService.AuthStateChanged += OnAuthStateChanged;

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _toastTimer.Tick += (s, e) =>
            {
                IsToastVisible = false;
                _toastTimer.Stop();
            };

            _deviceMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _deviceMonitorTimer.Tick += async (s, e) => await PollDevicesAsync();

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await _settingsService.LoadSettingsAsync();
            ApplyTheme(_settingsService.Settings.Theme);
            ApplyAccentColor(_settingsService.Settings.AccentColor);

            bool success = await _adbService.DetectAndSetAdbPathAsync(_settingsService.Settings.CustomAdbPath);
            await _fastbootService.DetectAndSetFastbootPathAsync(_settingsService.Settings.CustomAdbPath);
            await _scrcpyService.DetectAndSetScrcpyPathAsync(_settingsService.Settings.CustomScrcpyPath);

            IsAdbReady = success;
            AdbStatusText = success ? "ADB Ready" : "ADB Not Found";

            await RefreshDevicesAsync();
            _deviceMonitorTimer.Start();

            if (_settingsService.Settings.AutoCheckUpdates)
            {
                _ = Task.Run(async () =>
                {
                    var update = await _updateService.CheckForUpdatesAsync();
                    if (update.IsUpdateAvailable)
                    {
                        _notificationService.ShowNotification("Update Available", $"New version v{update.LatestVersion} is available!", NotificationType.Info);
                    }
                });
            }
        }

        private void OnViewChanged(string viewName)
        {
            ActiveViewName = viewName;
            CurrentViewModel = viewName switch
            {
                "Dashboard" => DashboardVM,
                "DeviceDetection" => DeviceDetectionVM,
                "ApkInstall" => ApkInstallVM,
                "FileExplorer" => FileExplorerVM,
                "PhoneClone" => PhoneCloneVM,
                "BackupRestore" => BackupRestoreVM,
                "Terminal" => TerminalVM,
                "ScreenshotGallery" => ScreenshotGalleryVM,
                "ApkTools" => ApkToolsVM,
                "AdvancedTools" => AdvancedToolsVM,
                "Scrcpy" => ScrcpyVM,
                "FastbootTools" => FastbootToolsVM,
                "AdbTools" => AdbToolsVM,
                "ApkManagement" => ApkManagementVM,
                "Auth" => AuthVM,
                "UserProfile" => UserProfileVM,
                "Settings" => SettingsVM,
                "Logs" => LogsVM,
                "About" => AboutVM,
                "Qualcomm" => QualcommVM,
                "MTK" => MtkVM,
                "Samsung" => SamsungVM,
                "Xiaomi" => XiaomiVM,
                "OPPO" => OppoVM,
                "VIVO" => VivoVM,
                "Realme" => RealmeVM,
                "Huawei" => HuaweiVM,
                "Honor" => HonorVM,
                "Motorola" => MotorolaVM,
                "Nokia" => NokiaVM,
                "SPD" => SpdVM,
                "Apple" => AppleVM,
                "FRP" => FrpVM,
                _ => DashboardVM
            };
        }

        private void NavigateTo(string viewName)
        {
            _navigationService.NavigateTo(viewName);
        }

        private void OpenAccount()
        {
            if (_authService.IsAuthenticated)
            {
                UserProfileVM.RefreshProfile();
                NavigateTo("UserProfile");
            }
            else
            {
                NavigateTo("Auth");
            }
        }

        private void OnAuthStateChanged(bool isAuthenticated)
        {
            OnPropertyChanged(nameof(IsUserAuthenticated));
            OnPropertyChanged(nameof(UserFullName));
            OnPropertyChanged(nameof(UserPlanBadge));

            if (isAuthenticated)
            {
                NavigateTo("Dashboard");
            }
            else
            {
                NavigateTo("Auth");
            }
        }

        public async Task RefreshDevicesAsync()
        {
            if (!_adbService.IsAdbAvailable) return;

            var devices = await _adbService.GetConnectedDevicesAsync();

            ConnectedDevices.Clear();
            foreach (var dev in devices)
            {
                ConnectedDevices.Add(dev);
            }

            if (SelectedDevice == null || !ConnectedDevices.Any(d => d.SerialNumber == SelectedDevice.SerialNumber))
            {
                SelectedDevice = ConnectedDevices.FirstOrDefault();
            }
            else
            {
                SelectedDevice = ConnectedDevices.First(d => d.SerialNumber == SelectedDevice.SerialNumber);
            }

            DeviceDetectionVM.UpdateDevices(ConnectedDevices, SelectedDevice);
            PhoneCloneVM.UpdateDevices(ConnectedDevices);
            UpdateActiveDeviceState();
        }

        private async Task PollDevicesAsync()
        {
            await RefreshDevicesAsync();
        }

        private void UpdateActiveDeviceState()
        {
            DashboardVM.UpdateDevice(SelectedDevice, IsAdbReady);
            ApkInstallVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            FileExplorerVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            BackupRestoreVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            TerminalVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            ScreenshotGalleryVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            AdvancedToolsVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            ScrcpyVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            AdbToolsVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
            ApkManagementVM.TargetSerialNumber = SelectedDevice?.SerialNumber;
        }

        private void ToggleTheme()
        {
            string newTheme = _settingsService.Settings.Theme == "Dark" ? "Light" : "Dark";
            _settingsService.Settings.Theme = newTheme;
            _ = _settingsService.SaveSettingsAsync();
            ApplyTheme(newTheme);
            ApplyAccentColor(_settingsService.Settings.AccentColor);
        }

        public static void ApplyAccentColor(string hexColor)
        {
            try
            {
                if (string.IsNullOrEmpty(hexColor)) hexColor = "#0078D4";
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                System.Windows.Application.Current.Resources["AccentColor"] = color;
                System.Windows.Application.Current.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(color);
                
                // Adjust hover/pressed brushes automatically
                System.Windows.Application.Current.Resources["AccentHoverBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, (byte)Math.Min(255, color.R + 20), (byte)Math.Min(255, color.G + 20), (byte)Math.Min(255, color.B + 20)));
                System.Windows.Application.Current.Resources["AccentPressedBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, (byte)Math.Max(0, color.R - 20), (byte)Math.Max(0, color.G - 20), (byte)Math.Max(0, color.B - 20)));
            }
            catch { }
        }

        public static void ApplyTheme(string theme)
        {
            try
            {
                if (string.IsNullOrEmpty(theme) || theme.Equals("Auto", StringComparison.OrdinalIgnoreCase) || theme.Equals("System", StringComparison.OrdinalIgnoreCase))
                {
                    theme = "Dark"; // Fallback default
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                        {
                            if (key != null)
                            {
                                var val = key.GetValue("AppsUseLightTheme");
                                if (val != null && (int)val == 1)
                                {
                                    theme = "Light";
                                }
                            }
                        }
                    }
                    catch { }
                }

                var appResources = System.Windows.Application.Current.Resources;
                var existingTheme = appResources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("DarkTheme") || d.Source.OriginalString.Contains("LightTheme")));

                if (existingTheme != null)
                {
                    appResources.MergedDictionaries.Remove(existingTheme);
                }

                string newThemePath = theme == "Light"
                    ? "Styles/Themes/LightTheme.xaml"
                    : "Styles/Themes/DarkTheme.xaml";

                var newDict = new System.Windows.ResourceDictionary
                {
                    Source = new Uri(newThemePath, UriKind.Relative)
                };

                appResources.MergedDictionaries.Insert(0, newDict);
            }
            catch { }
        }

        private void OnNotificationTriggered(object? sender, NotificationEventArgs e)
        {
            ToastTitle = e.Title;
            ToastMessage = e.Message;
            ToastType = e.Type;
            IsToastVisible = true;

            _toastTimer.Stop();
            _toastTimer.Start();
        }
    }
}

