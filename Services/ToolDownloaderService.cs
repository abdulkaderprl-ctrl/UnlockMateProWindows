using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace UnlockMatePro.Services
{
    public class ToolDownloaderService : IToolDownloaderService
    {
        private const string PLATFORM_TOOLS_URL = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";
        private const string SCRCPY_URL = "https://github.com/Genymobile/scrcpy/releases/download/v2.4/scrcpy-win64-v2.4.zip";

        private readonly ILoggerService _logger;

        public ToolDownloaderService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<bool> DownloadPlatformToolsAsync(IProgress<double>? progress = null)
        {
            string toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            Directory.CreateDirectory(toolsDir);

            string zipPath = Path.Combine(toolsDir, "platform-tools.zip");
            string targetExtractDir = Path.Combine(toolsDir, "platform-tools");

            _logger.LogInfo("Downloading Android Platform Tools from Google servers...");
            bool downloaded = await DownloadFileAsync(PLATFORM_TOOLS_URL, zipPath, progress);

            if (!downloaded) return false;

            try
            {
                _logger.LogInfo("Extracting platform-tools.zip archive...");
                if (Directory.Exists(targetExtractDir))
                {
                    Directory.Delete(targetExtractDir, true);
                }

                ZipFile.ExtractToDirectory(zipPath, toolsDir);
                File.Delete(zipPath);

                _logger.LogSuccess("Android Platform Tools extracted successfully!");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Extraction failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DownloadScrcpyAsync(IProgress<double>? progress = null)
        {
            string toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            Directory.CreateDirectory(toolsDir);

            string zipPath = Path.Combine(toolsDir, "scrcpy.zip");
            string targetFolder = Path.Combine(toolsDir, "scrcpy");

            _logger.LogInfo("Downloading Scrcpy release from GitHub...");
            bool downloaded = await DownloadFileAsync(SCRCPY_URL, zipPath, progress);

            if (!downloaded) return false;

            try
            {
                _logger.LogInfo("Extracting scrcpy.zip archive...");
                string tempUnzipFolder = Path.Combine(toolsDir, "scrcpy_temp");
                if (Directory.Exists(tempUnzipFolder)) Directory.Delete(tempUnzipFolder, true);

                ZipFile.ExtractToDirectory(zipPath, tempUnzipFolder);
                File.Delete(zipPath);

                // Find extracted inner folder (e.g. scrcpy-win64-v2.4)
                string[] subDirs = Directory.GetDirectories(tempUnzipFolder);
                string sourceFolder = subDirs.Length > 0 ? subDirs[0] : tempUnzipFolder;

                if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true);
                Directory.Move(sourceFolder, targetFolder);
                if (Directory.Exists(tempUnzipFolder)) Directory.Delete(tempUnzipFolder, true);

                _logger.LogSuccess("Scrcpy tools extracted successfully!");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Scrcpy extraction failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DownloadFileAsync(string url, string destinationFilePath, IProgress<double>? progress)
        {
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode) return false;

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        double percentage = (double)totalRead / totalBytes * 100.0;
                        progress?.Report(percentage);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Download exception for {url}: {ex.Message}");
                return false;
            }
        }
    }
}

