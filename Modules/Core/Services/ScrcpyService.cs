using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
{
    public class ScrcpyService : IScrcpyService
    {
        private readonly ILoggerService _logger;
        private string _scrcpyPath = "scrcpy";
        private bool _isAvailable = false;
        private Process? _activeScrcpyProcess;

        public string ScrcpyExecutablePath => _scrcpyPath;
        public bool IsScrcpyAvailable => _isAvailable;

        public ScrcpyService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<bool> DetectAndSetScrcpyPathAsync(string customPath = "")
        {
            _logger.LogInfo("Searching for scrcpy.exe executable...");

            // 1. Check custom path
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            {
                if (await TestScrcpyExecutableAsync(customPath))
                {
                    _scrcpyPath = customPath;
                    _isAvailable = true;
                    _logger.LogSuccess($"Scrcpy initialized from custom path: {customPath}");
                    return true;
                }
            }

            // 2. Check local application directory Tools/scrcpy/scrcpy.exe
            string localTool = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "scrcpy", "scrcpy.exe");
            if (File.Exists(localTool) && await TestScrcpyExecutableAsync(localTool))
            {
                _scrcpyPath = localTool;
                _isAvailable = true;
                _logger.LogSuccess($"Scrcpy initialized from local directory: {localTool}");
                return true;
            }

            // 3. System PATH check
            if (await TestScrcpyExecutableAsync("scrcpy"))
            {
                _scrcpyPath = "scrcpy";
                _isAvailable = true;
                _logger.LogSuccess("Scrcpy auto-detected in System PATH.");
                return true;
            }

            _isAvailable = false;
            _logger.LogWarning("scrcpy.exe not found. Screen mirroring requires scrcpy platform tools.");
            return false;
        }

        private async Task<bool> TestScrcpyExecutableAsync(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && output.Contains("scrcpy");
            }
            catch
            {
                return false;
            }
        }

        public Task<(bool Success, string Message)> LaunchMirroringAsync(string? serialNumber, AppSettings settings, string? recordFilePath = null)
        {
            if (!_isAvailable)
            {
                return Task.FromResult((false, "scrcpy.exe executable is missing. Please download scrcpy in Settings."));
            }

            var argsBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(serialNumber))
            {
                argsBuilder.Append($"-s \"{serialNumber}\" ");
            }

            // Remote Mouse & Keyboard control flag
            if (!settings.ScrcpyControlEnabled)
            {
                argsBuilder.Append("--no-control ");
            }

            if (settings.ScrcpyStayAwake)
            {
                argsBuilder.Append("--stay-awake ");
            }

            if (settings.ScrcpyTurnScreenOff)
            {
                argsBuilder.Append("--turn-screen-off ");
            }

            if (settings.ScrcpyShowTouches)
            {
                argsBuilder.Append("--show-touches ");
            }

            if (settings.ScrcpyMaxFps > 0)
            {
                argsBuilder.Append($"--max-fps {settings.ScrcpyMaxFps} ");
            }

            if (!string.IsNullOrWhiteSpace(settings.ScrcpyBitrateMbps))
            {
                argsBuilder.Append($"--video-bit-rate {settings.ScrcpyBitrateMbps} ");
            }

            // Screen Recording path
            if (!string.IsNullOrWhiteSpace(recordFilePath))
            {
                argsBuilder.Append($"--record \"{recordFilePath}\" ");
            }

            string fullArgs = argsBuilder.ToString().Trim();
            _logger.LogCommand($"{_scrcpyPath} {fullArgs}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _scrcpyPath,
                    Arguments = fullArgs,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                _activeScrcpyProcess = Process.Start(psi);
                if (_activeScrcpyProcess == null)
                {
                    return Task.FromResult((false, "Failed to start scrcpy process."));
                }

                _logger.LogSuccess("Scrcpy Screen Mirroring session launched.");
                return Task.FromResult((true, "Screen Mirroring active!"));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Scrcpy launch exception: {ex.Message}");
                return Task.FromResult((false, ex.Message));
            }
        }

        public void StopMirroring()
        {
            try
            {
                if (_activeScrcpyProcess != null && !_activeScrcpyProcess.HasExited)
                {
                    _activeScrcpyProcess.Kill();
                    _logger.LogInfo("Scrcpy process terminated.");
                }
            }
            catch { }
        }
    }
}

