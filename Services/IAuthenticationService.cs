using System;
using System.Threading.Tasks;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public interface IAuthenticationService
    {
        UserProfile? CurrentUser { get; }
        AuthTokens? CurrentTokens { get; }
        bool IsAuthenticated { get; }
        event Action<bool>? AuthStateChanged;

        Task<ApiResponse<UserProfile>> LoginAsync(LoginRequest request);
        Task<ApiResponse<UserProfile>> RegisterAsync(RegisterRequest request);
        Task<ApiResponse<bool>> ForgotPasswordAsync(string email);
        Task<ApiResponse<bool>> VerifyEmailAsync(string email, string code);
        Task<ApiResponse<UserProfile>> GoogleSignInAsync(string googleIdToken);
        Task<bool> AutoLoginAsync();
        Task LogoutAsync();
    }
}
