using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public class FastbootService : IFastbootService
    {
        private readonly ILoggerService _logger;
        private string _fastbootPath = "fastboot";
        private bool _isAvailable = false;

        public string FastbootExecutablePath => _fastbootPath;
        public bool IsFastbootAvailable => _isAvailable;

        public FastbootService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<bool> DetectAndSetFastbootPathAsync(string customAdbPath = "")
        {
            if (!string.IsNullOrWhiteSpace(customAdbPath) && File.Exists(customAdbPath))
            {
                string dir = Path.GetDirectoryName(customAdbPath) ?? "";
                string fbPath = Path.Combine(dir, "fastboot.exe");
                if (File.Exists(fbPath) && await TestFastbootExecutableAsync(fbPath))
                {
                    _fastbootPath = fbPath;
                    _isAvailable = true;
                    _logger.LogSuccess($"Fastboot initialized from ADB folder: {fbPath}");
                    return true;
                }
            }

            string localFb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "platform-tools", "fastboot.exe");
            if (File.Exists(localFb) && await TestFastbootExecutableAsync(localFb))
            {
                _fastbootPath = localFb;
                _isAvailable = true;
                _logger.LogSuccess($"Fastboot initialized from local Tools directory: {localFb}");
                return true;
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidatePaths = new[]
            {
                Path.Combine(localAppData, @"Android\Sdk\platform-tools\fastboot.exe"),
                @"C:\platform-tools\fastboot.exe",
                @"C:\adb\fastboot.exe"
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path) && await TestFastbootExecutableAsync(path))
                {
                    _fastbootPath = path;
                    _isAvailable = true;
                    _logger.LogSuccess($"Fastboot auto-detected at: {path}");
                    return true;
                }
            }

            if (await TestFastbootExecutableAsync("fastboot"))
            {
                _fastbootPath = "fastboot";
                _isAvailable = true;
                _logger.LogSuccess("Fastboot auto-detected in System PATH.");
                return true;
            }

            _isAvailable = false;
            _logger.LogWarning("fastboot.exe executable not found.");
            return false;
        }

        private async Task<bool> TestFastbootExecutableAsync(string path)
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

                return process.ExitCode == 0 && output.Contains("fastboot version");
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool Success, string Output)> ExecuteFastbootCommandAsync(string arguments, string? serialNumber = null)
        {
            if (!_isAvailable)
            {
                return (false, "Fastboot executable is not available.");
            }

            string fullArgs = string.IsNullOrWhiteSpace(serialNumber)
                ? arguments
                : $"-s \"{serialNumber}\" {arguments}";

            _logger.LogCommand($"{_fastbootPath} {fullArgs}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _fastbootPath,
                    Arguments = fullArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null) return (false, "Failed to launch fastboot process.");

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                string combined = (!string.IsNullOrWhiteSpace(output) ? output : error).Trim();
                return (process.ExitCode == 0, combined);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Fastboot exception: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<List<FastbootDevice>> GetConnectedFastbootDevicesAsync()
        {
            var list = new List<FastbootDevice>();
            var (success, output) = await ExecuteFastbootCommandAsync("devices");
            if (!success || string.IsNullOrWhiteSpace(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^(\S+)\s+(\S+)");
                if (match.Success)
                {
                    list.Add(new FastbootDevice
                    {
                        SerialNumber = match.Groups[1].Value,
                        DeviceState = match.Groups[2].Value
                    });
                }
            }

            return list;
        }

        public async Task<(bool Success, string Message)> FlashImageAsync(string partition, string imagePath, string? serialNumber)
        {
            if (!File.Exists(imagePath)) return (false, $"Image file not found: {imagePath}");

            _logger.LogInfo($"Flashing {partition} partition with {Path.GetFileName(imagePath)}...");
            var (success, output) = await ExecuteFastbootCommandAsync($"flash {partition} \"{imagePath}\"", serialNumber);

            if (success)
            {
                _logger.LogSuccess($"Successfully flashed {partition} image.");
                return (true, $"Flashed {partition} successfully!");
            }

            return (false, output);
        }

        public async Task<(bool Success, string Message)> BootImageAsync(string imagePath, string? serialNumber)
        {
            if (!File.Exists(imagePath)) return (false, $"Image file not found: {imagePath}");

            _logger.LogInfo($"Booting temporary image {Path.GetFileName(imagePath)}...");
            var (success, output) = await ExecuteFastbootCommandAsync($"boot \"{imagePath}\"", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> ErasePartitionAsync(string partition, string? serialNumber)
        {
            _logger.LogWarning($"Erasing partition {partition}...");
            var (success, output) = await ExecuteFastbootCommandAsync($"erase {partition}", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> OemUnlockAsync(string? serialNumber)
        {
            _logger.LogWarning("Executing OEM Unlock command...");
            var (success, output) = await ExecuteFastbootCommandAsync("flashing unlock", serialNumber);
            if (!success)
            {
                (success, output) = await ExecuteFastbootCommandAsync("oem unlock", serialNumber);
            }

            return (success, output);
        }

        public async Task<(bool Success, string Message)> OemLockAsync(string? serialNumber)
        {
            _logger.LogInfo("Executing OEM Lock command...");
            var (success, output) = await ExecuteFastbootCommandAsync("flashing lock", serialNumber);
            if (!success)
            {
                (success, output) = await ExecuteFastbootCommandAsync("oem lock", serialNumber);
            }

            return (success, output);
        }

        public async Task<(bool Success, string Message)> RebootFastbootAsync(string? serialNumber, string mode = "")
        {
            string cmd = string.IsNullOrWhiteSpace(mode) ? "reboot" : $"reboot {mode}";
            var (success, output) = await ExecuteFastbootCommandAsync(cmd, serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Output)> GetVarAllAsync(string? serialNumber)
        {
            return await ExecuteFastbootCommandAsync("getvar all", serialNumber);
        }

        public async Task<(bool Success, string Status)> GetFrpStatusAsync(string? serialNumber)
        {
            var (success, output) = await ExecuteFastbootCommandAsync("getvar frp-state", serialNumber);
            if (!success || string.IsNullOrWhiteSpace(output))
            {
                (success, output) = await ExecuteFastbootCommandAsync("getvar secure", serialNumber);
            }
            return (success, output);
        }
    }
}
