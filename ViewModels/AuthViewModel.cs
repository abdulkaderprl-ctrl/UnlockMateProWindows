using System;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class AuthViewModel : ViewModelBase
    {
        private readonly IAuthenticationService _authService;
        private readonly INotificationService _notificationService;
        private readonly ILoggerService _logger;

        private string _activeTab = "Login"; // "Login", "Register", "ForgotPassword", "VerifyEmail"
        private string _email = "admin@unlockmatepro.com";
        private string _password = "Password123!";
        private string _fullName = "John Doe";
        private string _confirmPassword = "Password123!";
        private string _verificationCode = "123456";
        private bool _rememberMe = true;
        private bool _isLoading = false;
        private string _statusMessage = string.Empty;

        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (SetProperty(ref _activeTab, value))
                {
                    OnPropertyChanged(nameof(IsLoginTab));
                    OnPropertyChanged(nameof(IsRegisterTab));
                    OnPropertyChanged(nameof(IsForgotPasswordTab));
                    OnPropertyChanged(nameof(IsVerifyEmailTab));
                }
            }
        }

        public bool IsLoginTab => ActiveTab == "Login";
        public bool IsRegisterTab => ActiveTab == "Register";
        public bool IsForgotPasswordTab => ActiveTab == "ForgotPassword";
        public bool IsVerifyEmailTab => ActiveTab == "VerifyEmail";

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string VerificationCode
        {
            get => _verificationCode;
            set => SetProperty(ref _verificationCode, value);
        }

        public bool RememberMe
        {
            get => _rememberMe;
            set => SetProperty(ref _rememberMe, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand RegisterCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand VerifyEmailCommand { get; }
        public ICommand SwitchTabCommand { get; }

        public AuthViewModel(
            IAuthenticationService authService,
            INotificationService notificationService,
            ILoggerService logger)
        {
            _authService = authService;
            _notificationService = notificationService;
            _logger = logger;

            LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsLoading);
            RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => !IsLoading);
            ForgotPasswordCommand = new AsyncRelayCommand(ForgotPasswordAsync, () => !IsLoading);
            VerifyEmailCommand = new AsyncRelayCommand(VerifyEmailAsync, () => !IsLoading);
            SwitchTabCommand = new RelayCommand(tab => ActiveTab = tab?.ToString() ?? "Login");
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Please enter your email and password.";
                return;
            }

            IsLoading = true;
            StatusMessage = "Authenticating with server...";

            var request = new LoginRequest
            {
                Email = Email.Trim(),
                Password = Password,
                RememberMe = RememberMe
            };

            var response = await _authService.LoginAsync(request);
            IsLoading = false;

            if (response.Success && response.Data != null)
            {
                StatusMessage = "Welcome back!";
                _notificationService.ShowSuccess("Login Success", $"Welcome back, {response.Data.FullName}!");
            }
            else
            {
                StatusMessage = response.Message;
                _notificationService.ShowError("Login Failed", response.Message);
            }
        }

        private async Task RegisterAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Please complete all registration fields.";
                return;
            }

            if (Password != ConfirmPassword)
            {
                StatusMessage = "Passwords do not match.";
                return;
            }

            IsLoading = true;
            StatusMessage = "Creating your Unlock Mate Pro account...";

            var request = new RegisterRequest
            {
                FullName = FullName.Trim(),
                Email = Email.Trim(),
                Password = Password,
                ConfirmPassword = ConfirmPassword
            };

            var response = await _authService.RegisterAsync(request);
            IsLoading = false;

            if (response.Success && response.Data != null)
            {
                StatusMessage = "Registration Complete!";
                _notificationService.ShowSuccess("Registration Complete", $"Welcome to Unlock Mate Pro, {response.Data.FullName}!");
            }
            else
            {
                StatusMessage = response.Message;
                _notificationService.ShowError("Registration Failed", response.Message);
            }
        }

        private async Task ForgotPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                StatusMessage = "Please enter your registered email address.";
                return;
            }

            IsLoading = true;
            var response = await _authService.ForgotPasswordAsync(Email);
            IsLoading = false;

            StatusMessage = response.Message;
            _notificationService.ShowSuccess("Password Reset", response.Message);
        }

        private async Task VerifyEmailAsync()
        {
            if (string.IsNullOrWhiteSpace(VerificationCode))
            {
                StatusMessage = "Please enter the verification code sent to your email.";
                return;
            }

            IsLoading = true;
            var response = await _authService.VerifyEmailAsync(Email, VerificationCode);
            IsLoading = false;

            StatusMessage = response.Message;
            _notificationService.ShowSuccess("Email Verified", response.Message);
        }
    }
}

