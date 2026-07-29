using System;

namespace UnlockMatePro.Models
{
    public class UserProfile
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = "Guest User";
        public string Email { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; } = false;
        public string PlanName { get; set; } = "Pro License"; // "Free", "Pro License", "Enterprise"
        public string AvatarUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime SubscriptionExpiresAt { get; set; } = DateTime.Now.AddYears(1);

        public bool IsActiveSubscription => SubscriptionExpiresAt > DateTime.Now;
        public string InitialLetter => string.IsNullOrWhiteSpace(FullName) ? "U" : FullName[0].ToString().ToUpper();
    }
}

