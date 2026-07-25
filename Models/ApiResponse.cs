namespace AdbEasyInstaller.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public AuthTokens? Tokens { get; set; }
        public UserProfile? User { get; set; }
    }
}
