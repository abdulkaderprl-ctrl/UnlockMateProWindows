using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdbEasyInstaller.Services
{
    public class UpdateService : IUpdateService
    {
        private const string GITHUB_RELEASES_URL = "https://api.github.com/repos/adbeasyinstaller/adb-easy-installer/releases/latest";
        private readonly string _currentVersion = "1.0.0";
        private readonly ILoggerService _logger;

        public UpdateService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            _logger.LogInfo("Checking for software updates from GitHub...");

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AdbEasyInstallerApp/1.0");

                var response = await client.GetAsync(GITHUB_RELEASES_URL);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string tag = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "1.0.0";
                    string notes = root.GetProperty("body").GetString() ?? "No release notes provided.";
                    string htmlUrl = root.GetProperty("html_url").GetString() ?? "";

                    bool isNewer = IsVersionNewer(tag, _currentVersion);

                    if (isNewer)
                    {
                        _logger.LogSuccess($"New update found: v{tag}");
                    }
                    else
                    {
                        _logger.LogInfo("Application is up to date.");
                    }

                    return new UpdateInfo
                    {
                        IsUpdateAvailable = isNewer,
                        LatestVersion = tag,
                        ReleaseNotes = notes,
                        DownloadUrl = htmlUrl
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Update check failed: {ex.Message}");
            }

            return new UpdateInfo
            {
                IsUpdateAvailable = false,
                LatestVersion = _currentVersion,
                ReleaseNotes = "Could not connect to update server."
            };
        }

        private bool IsVersionNewer(string latest, string current)
        {
            if (Version.TryParse(latest, out var latestVer) && Version.TryParse(current, out var currentVer))
            {
                return latestVer > currentVer;
            }
            return false;
        }
    }
}
