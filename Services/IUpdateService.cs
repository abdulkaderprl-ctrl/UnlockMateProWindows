using System.Threading.Tasks;

namespace AdbEasyInstaller.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; } = false;
        public string LatestVersion { get; set; } = "1.0.0";
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public interface IUpdateService
    {
        Task<UpdateInfo> CheckForUpdatesAsync();
    }
}
