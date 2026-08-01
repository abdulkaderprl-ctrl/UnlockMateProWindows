using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class UserProfileViewModel : ViewModelBase
    {
        private readonly IAuthenticationService _authService;
        private readonly INotificationService _notificationService;

        public UserProfile? CurrentUser => _authService.CurrentUser;

        public ICommand LogoutCommand { get; }

        public UserProfileViewModel(
            IAuthenticationService authService,
            INotificationService notificationService)
        {
            _authService = authService;
            _notificationService = notificationService;

            LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        }

        private async Task LogoutAsync()
        {
            await _authService.LogoutAsync();
            _notificationService.ShowSuccess("Logged Out", "You have been safely logged out.");
        }

        public void RefreshProfile()
        {
            OnPropertyChanged(nameof(CurrentUser));
        }
    }
}

