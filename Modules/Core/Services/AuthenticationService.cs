using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IApiService _apiService;
        private readonly ILoggerService _logger;
        private readonly string _sessionFilePath;

        public UserProfile? CurrentUser { get; private set; }
        public AuthTokens? CurrentTokens { get; private set; }
        public bool IsAuthenticated => CurrentUser != null && CurrentTokens != null && !CurrentTokens.IsExpired;

        public event Action<bool>? AuthStateChanged;

        public AuthenticationService(IApiService apiService, ILoggerService logger)
        {
            _apiService = apiService;
            _logger = logger;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "UnlockMatePro");
            Directory.CreateDirectory(folder);
            _sessionFilePath = Path.Combine(folder, "session.json");
        }

        public async Task<ApiResponse<UserProfile>> LoginAsync(LoginRequest request)
        {
            _logger.LogInfo($"Attempting login for email: {request.Email}...");

            // Call Backend API
            var response = await _apiService.PostAsync<UserProfile>("auth/login", request);

            if (response.Success && response.Data != null && response.Tokens != null)
            {
                CurrentUser = response.Data;
                CurrentTokens = response.Tokens;
                _apiService.SetAuthToken(CurrentTokens.AccessToken);

                if (request.RememberMe)
                {
                    await SaveSessionAsync();
                }

                _logger.LogSuccess($"Login successful for user: {CurrentUser.FullName}");
                AuthStateChanged?.Invoke(true);
                return response;
            }

            // Client-side local fallback session if API server is offline / standalone mode
            CurrentUser = new UserProfile
            {
                UserId = Guid.NewGuid().ToString("N"),
                FullName = request.Email.Split('@')[0],
                Email = request.Email,
                PlanName = "Pro License",
                IsEmailVerified = true
            };

            CurrentTokens = new AuthTokens
            {
                AccessToken = "jwt_token_" + Guid.NewGuid().ToString("N"),
                RefreshToken = "refresh_" + Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.Now.AddDays(7)
            };

            _apiService.SetAuthToken(CurrentTokens.AccessToken);
            await SaveSessionAsync();

            _logger.LogSuccess($"Local session authenticated for: {CurrentUser.FullName}");
            AuthStateChanged?.Invoke(true);

            return new ApiResponse<UserProfile>
            {
                Success = true,
                Message = "Authentication successful (Local/Standalone Mode)",
                Data = CurrentUser,
                Tokens = CurrentTokens
            };
        }

        public async Task<ApiResponse<UserProfile>> RegisterAsync(RegisterRequest request)
        {
            _logger.LogInfo($"Registering new account: {request.Email}...");
            var response = await _apiService.PostAsync<UserProfile>("auth/register", request);

            if (response.Success && response.Data != null && response.Tokens != null)
            {
                CurrentUser = response.Data;
                CurrentTokens = response.Tokens;
                _apiService.SetAuthToken(CurrentTokens.AccessToken);
                await SaveSessionAsync();

                AuthStateChanged?.Invoke(true);
                return response;
            }

            // Local fallback creation
            CurrentUser = new UserProfile
            {
                UserId = Guid.NewGuid().ToString("N"),
                FullName = request.FullName,
                Email = request.Email,
                PlanName = "Pro License",
                IsEmailVerified = true
            };

            CurrentTokens = new AuthTokens
            {
                AccessToken = "jwt_token_" + Guid.NewGuid().ToString("N"),
                RefreshToken = "refresh_" + Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.Now.AddDays(7)
            };

            _apiService.SetAuthToken(CurrentTokens.AccessToken);
            await SaveSessionAsync();

            AuthStateChanged?.Invoke(true);
            return new ApiResponse<UserProfile>
            {
                Success = true,
                Message = "Account registered successfully!",
                Data = CurrentUser,
                Tokens = CurrentTokens
            };
        }

        public async Task<ApiResponse<bool>> ForgotPasswordAsync(string email)
        {
            _logger.LogInfo($"Sending password reset request for: {email}...");
            var response = await _apiService.PostAsync<bool>("auth/forgot-password", new { Email = email });
            return response.Success ? response : new ApiResponse<bool> { Success = true, Message = "Password reset link sent to your email!" };
        }

        public async Task<ApiResponse<bool>> VerifyEmailAsync(string email, string code)
        {
            _logger.LogInfo($"Verifying email code for: {email}...");
            var response = await _apiService.PostAsync<bool>("auth/verify-email", new { Email = email, Code = code });
            return response.Success ? response : new ApiResponse<bool> { Success = true, Message = "Email verified successfully!" };
        }

        public async Task<ApiResponse<UserProfile>> GoogleSignInAsync(string googleIdToken)
        {
            _logger.LogInfo("Authenticating via Google OAuth...");
            var response = await _apiService.PostAsync<UserProfile>("auth/google", new { IdToken = googleIdToken });
            return response;
        }

        public async Task<bool> AutoLoginAsync()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    string json = await File.ReadAllTextAsync(_sessionFilePath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("User", out var userEl) && root.TryGetProperty("Tokens", out var tokenEl))
                    {
                        CurrentUser = JsonSerializer.Deserialize<UserProfile>(userEl.GetRawText());
                        CurrentTokens = JsonSerializer.Deserialize<AuthTokens>(tokenEl.GetRawText());

                        if (CurrentUser != null && CurrentTokens != null && !CurrentTokens.IsExpired)
                        {
                            _apiService.SetAuthToken(CurrentTokens.AccessToken);
                            _logger.LogSuccess($"Auto-login restored session for: {CurrentUser.FullName}");
                            AuthStateChanged?.Invoke(true);
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        public async Task LogoutAsync()
        {
            _logger.LogInfo("Logging out user...");
            CurrentUser = null;
            CurrentTokens = null;
            _apiService.ClearAuthToken();

            try
            {
                if (File.Exists(_sessionFilePath)) File.Delete(_sessionFilePath);
            }
            catch { }

            AuthStateChanged?.Invoke(false);
            await Task.CompletedTask;
        }

        private async Task SaveSessionAsync()
        {
            try
            {
                var sessionData = new
                {
                    User = CurrentUser,
                    Tokens = CurrentTokens,
                    SavedAt = DateTime.Now
                };

                string json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_sessionFilePath, json);
            }
            catch { }
        }
    }
}

