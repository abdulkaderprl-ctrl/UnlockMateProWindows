using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnlockMatePro.ViewModels;

namespace UnlockMatePro.Services
{
    public class AppleService : IAppleService
    {
        private readonly ILoggerService _logger;
        private readonly string _toolsDir;

        public AppleService(ILoggerService logger)
        {
            _logger = logger;
            _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "Apple");
            if (!Directory.Exists(_toolsDir))
            {
                Directory.CreateDirectory(_toolsDir);
            }
        }

        public bool IsAppleToolAvailable(string toolName)
        {
            string path = Path.Combine(_toolsDir, toolName + ".exe");
            if (File.Exists(path)) return true;
            
            // Check in system PATH if not found in Tools\Apple
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = toolName,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var process = Process.Start(psi);
                return process != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> RunAppleToolAsync(string toolName, string arguments)
        {
            try
            {
                string executable = Path.Combine(_toolsDir, toolName + ".exe");
                if (!File.Exists(executable))
                {
                    executable = toolName; // Fallback to PATH
                }

                var psi = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return "Failed to start tool.";

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(error) && string.IsNullOrEmpty(output))
                {
                    return error;
                }
                return output + "\n" + error;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error running {toolName}: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public async Task<DeviceInfo?> DetectDeviceAsync()
        {
            // Check Normal Mode
            string info = await RunAppleToolAsync("ideviceinfo", "");
            if (info.Contains("ProductType"))
            {
                var matchModel = Regex.Match(info, @"ProductType:\s*([^\r\n]+)");
                var matchUdid = Regex.Match(info, @"UniqueDeviceID:\s*([^\r\n]+)");
                return new DeviceInfo
                {
                    Model = matchModel.Success ? matchModel.Groups[1].Value.Trim() : "Apple Device",
                    Mode = "Normal Mode",
                    Serial = matchUdid.Success ? matchUdid.Groups[1].Value.Trim() : "Unknown UDID"
                };
            }

            // Check Recovery/DFU Mode
            string irecv = await RunAppleToolAsync("irecovery", "-q");
            if (irecv.Contains("CPID:") || irecv.Contains("MODE:"))
            {
                string mode = irecv.Contains("MODE: DFU") ? "DFU Mode" : "Recovery Mode";
                var matchModel = Regex.Match(irecv, @"PRODUCT:\s*([^\r\n]+)");
                var matchEcid = Regex.Match(irecv, @"ECID:\s*([^\r\n]+)");
                return new DeviceInfo
                {
                    Model = matchModel.Success ? matchModel.Groups[1].Value.Trim() : "Apple Device",
                    Mode = mode,
                    Serial = matchEcid.Success ? matchEcid.Groups[1].Value.Trim() : "Unknown ECID"
                };
            }

            return null;
        }

        public async Task<string> ReadInfoAsync()
        {
            string normalInfo = await RunAppleToolAsync("ideviceinfo", "");
            if (normalInfo.Contains("ProductType"))
            {
                return normalInfo;
            }
            
            string irecvInfo = await RunAppleToolAsync("irecovery", "-q");
            if (irecvInfo.Contains("MODE:"))
            {
                return irecvInfo;
            }

            return "No Apple device found in Normal, Recovery, or DFU mode.";
        }

        public async Task<string> EnterRecoveryModeAsync()
        {
            return await RunAppleToolAsync("ideviceenterrecovery", "");
        }

        public async Task<string> ExitRecoveryModeAsync()
        {
            return await RunAppleToolAsync("irecovery", "-n");
        }

        public async Task<string> RebootDeviceAsync()
        {
            string output = await RunAppleToolAsync("idevicediagnostics", "restart");
            if (output.Contains("Error") || output.Contains("Could not"))
            {
                // Fallback to irecovery reboot if in recovery
                return await RunAppleToolAsync("irecovery", "-c reboot");
            }
            return output;
        }

        public async Task<string> FlashIpswAsync(string ipswPath)
        {
            if (string.IsNullOrEmpty(ipswPath) || !File.Exists(ipswPath))
                return "Invalid IPSW path.";

            return await RunAppleToolAsync("idevicerestore", $"\"{ipswPath}\"");
        }

        public async Task<string> RestoreFirmwareAsync(string ipswPath)
        {
            if (string.IsNullOrEmpty(ipswPath) || !File.Exists(ipswPath))
                return "Invalid IPSW path.";

            // Using erase (restore) mode
            return await RunAppleToolAsync("idevicerestore", $"-e \"{ipswPath}\"");
        }

        public async Task<string> CheckActivationStatusAsync()
        {
            string info = await RunAppleToolAsync("ideviceinfo", "-q com.apple.mobile.lockdown_cache");
            if (info.Contains("ActivationState: Activated"))
                return "Activation Status: Activated";
            if (info.Contains("ActivationState: Unactivated"))
                return "Activation Status: Unactivated";
            
            return "Activation Status: Unknown / Not in Normal Mode";
        }

        public async Task<string> CheckFindMyIphoneAsync()
        {
            // Note: True FMI check requires a server-side API using SN/IMEI.
            // Locally we can check if it's activated, or run a surrogate command.
            // Placeholder for FMI check (supported methods).
            string info = await RunAppleToolAsync("ideviceinfo", "-k EncryptedDonotbackup");
            // Usually we'd check an external API. We'll return a simulated or offline check result.
            return "Find My iPhone (FMI): Check requires server-side SN/IMEI verification. Ensure device is unlinked before restore.";
        }
    }
}
