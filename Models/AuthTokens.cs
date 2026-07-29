using System;

namespace UnlockMatePro.Models
{
    public class AuthTokens
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(24);

        public bool IsExpired => DateTime.Now >= ExpiresAt;
    }
}

