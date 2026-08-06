using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
{
    public class AdbService : IAdbService
    {
        private readonly ILoggerService _logger;
        private string _adbPath = "adb";
        private bool _isAvailable = false;

        public string AdbExecutablePath => _adbPath;
        public bool IsAdbAvailable => _isAvailable;

        public AdbService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<bool> DetectAndSetAdbPathAsync(string customPath = "")
        {
            _logger.LogInfo("Searching for adb.exe...");

            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            {
                if (await TestAdbExecutableAsync(customPath))
                {
                    _adbPath = customPath;
                    _isAvailable = true;
                    _logger.LogSuccess($"ADB initialized successfully from custom path: {customPath}");
                    return true;
                }
            }

            string localToolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "platform-tools", "adb.exe");
            if (File.Exists(localToolPath) && await TestAdbExecutableAsync(localToolPath))
            {
                _adbPath = localToolPath;
                _isAvailable = true;
                _logger.LogSuccess($"ADB initialized from local directory: {localToolPath}");
                return true;
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] candidatePaths = new[]
            {
                Path.Combine(localAppData, @"Android\Sdk\platform-tools\adb.exe"),
                Path.Combine(programFiles, @"Android\android-sdk\platform-tools\adb.exe"),
                Path.Combine(programFilesX86, @"Android\android-sdk\platform-tools\adb.exe"),
                Path.Combine(localAppData, @"Programs\platform-tools\adb.exe"),
                @"C:\platform-tools\adb.exe",
                @"C:\adb\adb.exe"
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path) && await TestAdbExecutableAsync(path))
                {
                    _adbPath = path;
                    _isAvailable = true;
                    _logger.LogSuccess($"ADB auto-detected at: {path}");
                    return true;
                }
            }

            if (await TestAdbExecutableAsync("adb"))
            {
                _adbPath = "adb";
                _isAvailable = true;
                _logger.LogSuccess("ADB auto-detected in System PATH.");
                return true;
            }

            _isAvailable = false;
            _logger.LogWarning("adb.exe was not found. Please install Android Platform Tools or click Download ADB in Settings.");
            return false;
        }

        private async Task<bool> TestAdbExecutableAsync(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && output.Contains("Android Debug Bridge");
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool Success, string Output)> ExecuteCommandAsync(string arguments, string? serialNumber = null, System.Threading.CancellationToken cancellationToken = default)
        {
            if (!_isAvailable)
            {
                return (false, "ADB executable is not available.");
            }

            string fullArgs = string.IsNullOrWhiteSpace(serialNumber)
                ? arguments
                : $"-s \"{serialNumber}\" {arguments}";

            _logger.LogCommand($"{_adbPath} {fullArgs}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = fullArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null) return (false, "Failed to start ADB process.");

                string outputTask = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                string errorTask = await process.StandardError.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken);

                string resultText = !string.IsNullOrWhiteSpace(outputTask) ? outputTask : errorTask;
                bool isSuccess = process.ExitCode == 0;

                if (!isSuccess)
                {
                    _logger.LogError($"ADB command failed with exit code {process.ExitCode}: {resultText.Trim()}");
                }

                return (isSuccess, resultText.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception executing ADB command: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<List<AdbDevice>> GetConnectedDevicesAsync()
        {
            var devices = new List<AdbDevice>();
            var (success, output) = await ExecuteCommandAsync("devices -l");
            if (!success || string.IsNullOrWhiteSpace(output)) return devices;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("List of devices attached") || string.IsNullOrWhiteSpace(line))
                    continue;

                var match = Regex.Match(line, @"^(\S+)\s+(\S+)(.*)$");
                if (match.Success)
                {
                    string serial = match.Groups[1].Value;
                    string state = match.Groups[2].Value;
                    string extraInfo = match.Groups[3].Value;

                    var device = new AdbDevice
                    {
                        SerialNumber = serial,
                        DeviceState = state
                    };

                    var modelMatch = Regex.Match(extraInfo, @"model:(\S+)");
                    if (modelMatch.Success)
                    {
                        device.Model = modelMatch.Groups[1].Value.Replace('_', ' ');
                    }

                    if (device.IsConnected)
                    {
                        var details = await GetDeviceDetailsAsync(serial);
                        if (details != null)
                        {
                            device.AndroidVersion = details.AndroidVersion;
                            device.ApiLevel = details.ApiLevel;
                            device.BatteryLevel = details.BatteryLevel;
                            device.IsCharging = details.IsCharging;
                            device.IsRooted = details.IsRooted;
                        }
                    }

                    devices.Add(device);
                }
            }

            return devices;
        }

        public async Task<AdbDevice?> GetDeviceDetailsAsync(string serialNumber)
        {
            var device = new AdbDevice { SerialNumber = serialNumber };

            var (_, modelOut) = await ExecuteCommandAsync("shell getprop ro.product.model", serialNumber);
            if (!string.IsNullOrWhiteSpace(modelOut)) device.Model = modelOut;

            var (_, verOut) = await ExecuteCommandAsync("shell getprop ro.build.version.release", serialNumber);
            if (!string.IsNullOrWhiteSpace(verOut)) device.AndroidVersion = verOut;

            var (_, apiOut) = await ExecuteCommandAsync("shell getprop ro.build.version.sdk", serialNumber);
            if (!string.IsNullOrWhiteSpace(apiOut)) device.ApiLevel = apiOut;

            var (_, batteryOut) = await ExecuteCommandAsync("shell dumpsys battery", serialNumber);
            if (!string.IsNullOrWhiteSpace(batteryOut))
            {
                var levelMatch = Regex.Match(batteryOut, @"level:\s*(\d+)");
                if (levelMatch.Success && int.TryParse(levelMatch.Groups[1].Value, out int level))
                {
                    device.BatteryLevel = level;
                }

                var statusMatch = Regex.Match(batteryOut, @"status:\s*(\d+)");
                if (statusMatch.Success)
                {
                    device.IsCharging = statusMatch.Groups[1].Value == "2" || statusMatch.Groups[1].Value == "5";
                }
            }

            device.IsRooted = await CheckRootAsync(serialNumber);
            device.Stats = await GetSystemStatsAsync(serialNumber);

            return device;
        }

        // UNIVERSAL PROFESSIONAL APK INSTALLATION ENGINE
        public async Task<(bool PreCheckPassed, string Message)> PreInstallCheckAsync(ApkInfo apkInfo, string? serialNumber)
        {
            _logger.LogInfo($"Performing pre-install diagnostics for {apkInfo.FileName}...");

            if (!File.Exists(apkInfo.FilePath))
            {
                return (false, $"File not found: {apkInfo.FilePath}");
            }

            var (stateSuccess, stateOut) = await ExecuteCommandAsync("get-state", serialNumber);
            if (!stateSuccess || !stateOut.Contains("device"))
            {
                return (false, "No active Android device connected or device unauthorized.");
            }

            var (_, apiOut) = await ExecuteCommandAsync("shell getprop ro.build.version.sdk", serialNumber);
            if (int.TryParse(apiOut.Trim(), out int deviceSdk))
            {
                if (deviceSdk < apkInfo.MinSdkVersion)
                {
                    return (false, $"Device Android SDK ({deviceSdk}) is below app minimum required SDK ({apkInfo.MinSdkVersion}).");
                }
            }

            var (_, abiOut) = await ExecuteCommandAsync("shell getprop ro.product.cpu.abi", serialNumber);
            string deviceAbi = abiOut.Trim();

            var (_, dfOut) = await ExecuteCommandAsync("shell df -k /data", serialNumber);
            if (!string.IsNullOrWhiteSpace(dfOut))
            {
                var lines = dfOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1)
                {
                    var parts = Regex.Split(lines[1].Trim(), @"\s+");
                    if (parts.Length >= 4 && long.TryParse(parts[3], out long freeKb))
                    {
                        long freeBytes = freeKb * 1024;
                        if (freeBytes < apkInfo.FileSizeBytes)
                        {
                            return (false, $"Insufficient device storage: Free {freeBytes / (1024 * 1024)} MB < Package size {apkInfo.FileSizeBytes / (1024 * 1024)} MB.");
                        }
                    }
                }
            }

            _logger.LogSuccess($"Pre-install checks passed: Device SDK={apiOut.Trim()}, ABI={deviceAbi}");
            return (true, $"Diagnostics Passed (SDK: {apiOut.Trim()}, ABI: {deviceAbi})");
        }

        public async Task<(bool Success, string Message, string DetailedLog)> InstallApkAsync(
            string apkPath,
            string? serialNumber,
            bool reinstall = true,
            bool grantPermissions = true,
            bool allowDowngrade = false,
            bool autoUninstallOnConflict = false,
            IProgress<double>? progress = null,
            IProgress<string>? logProgress = null)
        {
            var logBuilder = new StringBuilder();
            void LogMsg(string msg)
            {
                logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
                logProgress?.Report(msg);
                _logger.LogInfo(msg);
            }

            LogMsg($"Initiating Universal Installation for: {Path.GetFileName(apkPath)}");
            progress?.Report(10);

            var apkInfo = await GetApkInfoAsync(apkPath);
            var (preCheckOk, preCheckMsg) = await PreInstallCheckAsync(apkInfo, serialNumber);
            LogMsg(preCheckMsg);

            if (!preCheckOk)
            {
                return (false, preCheckMsg, logBuilder.ToString());
            }

            progress?.Report(30);

            string ext = Path.GetExtension(apkPath).ToLowerInvariant();
            (bool success, string output) installResult;

            if (ext == ".xapk" || ext == ".apks" || ext == ".apkm")
            {
                LogMsg("Detected Split APK Archive Bundle. Extracting split APKs...");
                installResult = await InstallSplitApkBundleInternalAsync(apkPath, serialNumber, reinstall, grantPermissions, allowDowngrade, progress, LogMsg);
            }
            else
            {
                LogMsg("Standard Single APK detected. Executing adb install...");
                var argsBuilder = new StringBuilder("install ");
                if (reinstall) argsBuilder.Append("-r ");
                if (grantPermissions) argsBuilder.Append("-g ");
                if (allowDowngrade) argsBuilder.Append("-d ");
                argsBuilder.Append("-t ");
                argsBuilder.Append($"\"{apkPath}\"");

                progress?.Report(60);
                installResult = await ExecuteCommandAsync(argsBuilder.ToString(), serialNumber);
            }

            LogMsg($"ADB Raw Output: {installResult.output}");
            progress?.Report(85);

            if (installResult.success && installResult.output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                LogMsg("Verifying package presence via pm list packages...");
                bool verified = await VerifyPackageInstalledAsync(apkInfo.PackageName, serialNumber);

                progress?.Report(100);
                if (verified)
                {
                    LogMsg($"Installation Verified! Package {apkInfo.PackageName} is installed.");
                    return (true, "Installation & Verification Successful!", logBuilder.ToString());
                }

                return (true, "Installation Succeeded!", logBuilder.ToString());
            }

            string rawOut = installResult.output;
            string handledError = MapAdbErrorCode(rawOut);
            LogMsg($"Error Detected: {handledError}");

            if ((rawOut.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE") || rawOut.Contains("INSTALL_FAILED_VERSION_DOWNGRADE")) && autoUninstallOnConflict)
            {
                LogMsg($"Attempting Auto-Uninstall of conflicting package {apkInfo.PackageName}...");
                var (unOk, unMsg) = await UninstallApkAsync(apkInfo.PackageName, serialNumber);
                if (unOk)
                {
                    LogMsg("Auto-Uninstall successful. Retrying installation...");
                    return await InstallApkAsync(apkPath, serialNumber, reinstall, grantPermissions, allowDowngrade, false, progress, logProgress);
                }
            }
            else if (rawOut.Contains("INSTALL_FAILED_ALREADY_EXISTS") && !reinstall)
            {
                LogMsg("Retrying with reinstall (-r) flag enabled...");
                return await InstallApkAsync(apkPath, serialNumber, true, grantPermissions, allowDowngrade, autoUninstallOnConflict, progress, logProgress);
            }

            progress?.Report(100);
            return (false, handledError, logBuilder.ToString());
        }

        private string MapAdbErrorCode(string output)
        {
            if (output.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE"))
                return "INSTALL_FAILED_UPDATE_INCOMPATIBLE: Signature mismatch with existing package on device. Uninstall the old version first.";
            if (output.Contains("INSTALL_FAILED_VERSION_DOWNGRADE"))
                return "INSTALL_FAILED_VERSION_DOWNGRADE: Target version is lower than installed version. Enable 'Allow Downgrade' option.";
            if (output.Contains("INSTALL_FAILED_ALREADY_EXISTS"))
                return "INSTALL_FAILED_ALREADY_EXISTS: Package already exists. Enable 'Reinstall' option.";
            if (output.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE"))
                return "INSTALL_FAILED_INSUFFICIENT_STORAGE: Device memory is full. Please free up space on internal storage.";
            if (output.Contains("INSTALL_FAILED_NO_MATCHING_ABIS"))
                return "INSTALL_FAILED_NO_MATCHING_ABIS: CPU Architecture incompatible (e.g., ARM64 app on x86 device/emulator).";
            if (output.Contains("INSTALL_PARSE_FAILED"))
                return "INSTALL_PARSE_FAILED: Invalid APK structure or corrupted AndroidManifest.xml syntax.";
            if (output.Contains("INSTALL_FAILED_USER_RESTRICTED"))
                return "INSTALL_FAILED_USER_RESTRICTED: Installation blocked by device security settings (e.g. enable 'Install via USB' in Developer Options).";
            if (output.Contains("INSTALL_FAILED_TEST_ONLY"))
                return "INSTALL_FAILED_TEST_ONLY: Test-only APK. Re-building without android:testOnly FLAG.";
            if (output.Contains("INSTALL_FAILED_INVALID_APK"))
                return "INSTALL_FAILED_INVALID_APK: APK file is invalid or corrupted zip archive.";
            if (output.Contains("INSTALL_FAILED_INTERNAL_ERROR"))
                return "INSTALL_FAILED_INTERNAL_ERROR: Android system package manager service crashed or encountered internal error.";

            return string.IsNullOrWhiteSpace(output) ? "Unknown Installation Error" : output;
        }

        public async Task<bool> VerifyPackageInstalledAsync(string packageName, string? serialNumber)
        {
            if (string.IsNullOrWhiteSpace(packageName) || packageName == "Unknown Package") return true;

            var (success, output) = await ExecuteCommandAsync($"shell pm list packages {packageName}", serialNumber);
            return success && output.Contains($"package:{packageName}");
        }

        private async Task<(bool Success, string Output)> InstallSplitApkBundleInternalAsync(
            string bundlePath,
            string? serialNumber,
            bool reinstall,
            bool grantPermissions,
            bool allowDowngrade,
            IProgress<double>? progress,
            Action<string> log)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "UnlockMatePro_Splits", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                ZipFile.ExtractToDirectory(bundlePath, tempFolder);
                progress?.Report(45);

                var apkFiles = Directory.GetFiles(tempFolder, "*.apk", SearchOption.AllDirectories);
                if (apkFiles.Length == 0)
                {
                    return (false, "No .apk files found inside split bundle archive.");
                }

                log($"Found {apkFiles.Length} split APKs. Executing adb install-multiple...");
                var argsBuilder = new StringBuilder("install-multiple ");
                if (reinstall) argsBuilder.Append("-r ");
                if (grantPermissions) argsBuilder.Append("-g ");
                if (allowDowngrade) argsBuilder.Append("-d ");
                argsBuilder.Append("-t ");

                foreach (var apk in apkFiles)
                {
                    argsBuilder.Append($"\"{apk}\" ");
                }

                progress?.Report(70);
                return await ExecuteCommandAsync(argsBuilder.ToString().Trim(), serialNumber);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
            }
        }

        public async Task<(bool Success, string Message)> UninstallApkAsync(string packageName, string? serialNumber)
        {
            _logger.LogInfo($"Uninstalling package {packageName}...");
            var (success, output) = await ExecuteCommandAsync($"uninstall \"{packageName}\"", serialNumber);

            if (success && output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogSuccess($"Successfully uninstalled {packageName}");
                return (true, "Package uninstalled successfully.");
            }

            return (false, output);
        }

        public Task<ApkInfo> GetApkInfoAsync(string apkPath)
        {
            var apkInfo = new ApkInfo
            {
                FilePath = apkPath,
                FileName = Path.GetFileName(apkPath),
                AppName = Path.GetFileNameWithoutExtension(apkPath),
                Status = "Ready"
            };

            try
            {
                var fileInfo = new FileInfo(apkPath);
                apkInfo.FileSizeBytes = fileInfo.Length;

                using var zip = ZipFile.OpenRead(apkPath);
                var manifestEntry = zip.GetEntry("AndroidManifest.xml") ?? zip.GetEntry("manifest.json");

                if (manifestEntry != null)
                {
                    using var stream = manifestEntry.Open();
                    using var reader = new StreamReader(stream);
                    string text = reader.ReadToEnd();

                    var pkgMatch = Regex.Match(text, @"package[\s=:]+""([^""]+)""", RegexOptions.IgnoreCase);
                    if (pkgMatch.Success) apkInfo.PackageName = pkgMatch.Groups[1].Value;
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(apkInfo.PackageName) || apkInfo.PackageName == "Unknown Package")
            {
                string cleanName = Regex.Replace(Path.GetFileNameWithoutExtension(apkPath), @"[^a-zA-Z0-9\._]", "").ToLower();
                apkInfo.PackageName = string.IsNullOrWhiteSpace(cleanName) ? "com.example.app" : $"com.app.{cleanName}";
            }

            return Task.FromResult(apkInfo);
        }

        public async Task<List<AppInfo>> GetInstalledAppsAsync(string? serialNumber, bool includeSystemApps = false)
        {
            var list = new List<AppInfo>();
            string arg = includeSystemApps ? "shell pm list packages -f" : "shell pm list packages -f -3";

            var (success, output) = await ExecuteCommandAsync(arg, serialNumber);
            if (!success || string.IsNullOrWhiteSpace(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"package:(.*)=(.*)");
                if (match.Success)
                {
                    list.Add(new AppInfo
                    {
                        ApkPath = match.Groups[1].Value,
                        PackageName = match.Groups[2].Value.Trim(),
                        IsSystemApp = !line.Contains("/data/app/")
                    });
                }
            }

            return list;
        }

        public async Task<(bool Success, string Message)> BackupApkAsync(string packageName, string destinationPath, string? serialNumber)
        {
            _logger.LogInfo($"Querying path for package {packageName}...");
            var (pathSuccess, pathOutput) = await ExecuteCommandAsync($"shell pm path {packageName}", serialNumber);

            if (!pathSuccess || string.IsNullOrWhiteSpace(pathOutput))
            {
                return (false, "Could not locate package on device.");
            }

            string remotePath = pathOutput.Replace("package:", "").Trim();
            string destFile = Path.Combine(destinationPath, $"{packageName}.apk");

            _logger.LogInfo($"Pulling APK from {remotePath} to {destFile}...");
            var (pullSuccess, pullOutput) = await ExecuteCommandAsync($"pull \"{remotePath}\" \"{destFile}\"", serialNumber);

            if (pullSuccess && File.Exists(destFile))
            {
                _logger.LogSuccess($"Backup complete: {destFile}");
                return (true, $"Saved to: {destFile}");
            }

            return (false, pullOutput);
        }

        // App Management Controls
        public async Task<(bool Success, string Message)> EnableAppAsync(string packageName, string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync($"shell pm enable {packageName}", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> DisableAppAsync(string packageName, string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync($"shell pm disable-user --user 0 {packageName}", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> ForceStopAppAsync(string packageName, string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync($"shell am force-stop {packageName}", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> ClearAppDataAsync(string packageName, string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync($"shell pm clear {packageName}", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> GrantPermissionAsync(string packageName, string permission, string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync($"shell pm grant {packageName} {permission}", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> RevokePermissionAsync(string packageName, string permission, string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync($"shell pm revoke {packageName} {permission}", serialNumber);
            return (success, output);
        }

        // Professional Android Device File Explorer (Robust Symlink & /sdcard Parser)
        public async Task<List<FileItem>> GetDirectoryFilesAsync(string remotePath, string? serialNumber)
        {
            var list = new List<FileItem>();
            string targetPath = string.IsNullOrWhiteSpace(remotePath) ? "/sdcard" : remotePath.Trim();
            string queryPath = targetPath.EndsWith("/") ? targetPath : $"{targetPath}/";

            var (success, output) = await ExecuteCommandAsync($"shell ls -la \"{queryPath}\"", serialNumber);
            if (!success || string.IsNullOrWhiteSpace(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("total") || line.Trim().EndsWith(" .") || line.Trim().EndsWith(" ..")) continue;

                string cleanLine = line.Trim();
                string perms = cleanLine.Split(' ')[0];

                string name = string.Empty;
                if (cleanLine.Contains(" -> "))
                {
                    var parts = cleanLine.Split(new[] { " -> " }, StringSplitOptions.None);
                    var leftTokens = Regex.Split(parts[0].Trim(), @"\s+");
                    name = leftTokens[leftTokens.Length - 1];
                }
                else
                {
                    var tokens = Regex.Split(cleanLine, @"\s+");
                    if (tokens.Length >= 7)
                    {
                        name = tokens[tokens.Length - 1];
                    }
                }

                if (string.IsNullOrWhiteSpace(name) || name == "." || name == "..") continue;

                bool isDir = perms.StartsWith("d") || perms.StartsWith("l") ||
                             name.Equals("sdcard", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("primary", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("emulated", StringComparison.OrdinalIgnoreCase);

                long size = 0;
                var tokensAll = Regex.Split(cleanLine, @"\s+");
                if (!isDir && tokensAll.Length >= 5)
                {
                    long.TryParse(tokensAll[4], out size);
                }

                list.Add(new FileItem
                {
                    Name = name,
                    FullPath = queryPath + name,
                    IsDirectory = isDir,
                    Permissions = perms,
                    SizeBytes = size,
                    LastModified = DateTime.Now
                });
            }

            return list;
        }

        public async Task<(bool Success, string Message)> PushFileAsync(string localPath, string remotePath, string? serialNumber)
        {
            _logger.LogInfo($"Pushing {localPath} to {remotePath}...");
            var (success, output) = await ExecuteCommandAsync($"push \"{localPath}\" \"{remotePath}\"", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> PullFileAsync(string remotePath, string localPath, string? serialNumber)
        {
            _logger.LogInfo($"Pulling {remotePath} to {localPath}...");
            var (success, output) = await ExecuteCommandAsync($"pull \"{remotePath}\" \"{localPath}\"", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> DeleteFileAsync(string remotePath, string? serialNumber)
        {
            _logger.LogInfo($"Deleting remote file/dir: {remotePath}...");
            var (success, output) = await ExecuteCommandAsync($"shell rm -rf \"{remotePath}\"", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> RenameFileAsync(string oldPath, string newPath, string? serialNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
                {
                    _logger.LogError("Rename failed: Source or destination path is empty.");
                    return (false, "Invalid file or folder path.");
                }

                _logger.LogInfo($"Renaming remote path from '{oldPath}' to '{newPath}'...");

                // Check if destination already exists on remote device
                var (checkSuccess, checkOutput) = await ExecuteCommandAsync($"shell test -e \"{newPath}\" && echo EXISTS", serialNumber);
                if (checkSuccess && checkOutput.Contains("EXISTS"))
                {
                    _logger.LogWarning($"Rename failed: Destination path '{newPath}' already exists.");
                    return (false, "A file or folder with that name already exists in this location.");
                }

                var (success, output) = await ExecuteCommandAsync($"shell mv \"{oldPath}\" \"{newPath}\"", serialNumber);
                if (!success || (!string.IsNullOrWhiteSpace(output) && (output.Contains("failed") || output.Contains("Error") || output.Contains("Permission denied"))))
                {
                    string errReason = string.IsNullOrWhiteSpace(output) ? "Failed to rename remote item." : output.Trim();
                    _logger.LogError($"Rename failed for '{oldPath}' -> '{newPath}': {errReason}");
                    return (false, errReason);
                }

                _logger.LogInfo($"Successfully renamed '{oldPath}' to '{newPath}'.");
                return (true, "Success");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during RenameFileAsync: {ex.Message}");
                return (false, $"Error during rename operation: {ex.Message}");
            }
        }

        public async Task<bool> CheckRemotePathExistsAsync(string remotePath, string? serialNumber)
        {
            if (string.IsNullOrWhiteSpace(remotePath)) return false;
            _logger.LogInfo($"[CheckRemotePathExistsAsync] Checking remote existence of '{remotePath}'...");

            var (lsSuccess, lsOutput) = await ExecuteCommandAsync($"shell ls -d \"{remotePath}\"", serialNumber);
            bool exists = lsSuccess && !string.IsNullOrWhiteSpace(lsOutput) && !lsOutput.Contains("No such file") && !lsOutput.Contains("not found");
            _logger.LogInfo($"[CheckRemotePathExistsAsync] Path '{remotePath}' exists: {exists}");
            return exists;
        }

        public async Task<(bool Success, string Message)> CopyRemoteItemAsync(
            string sourcePath,
            string destPath,
            string? serialNumber,
            IProgress<BackupProgressInfo>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destPath))
                {
                    _logger.LogError("[CopyRemoteItemAsync] Failed: Invalid source or destination path.");
                    return (false, "Source or destination path is invalid.");
                }

                _logger.LogInfo($"[CopyRemoteItemAsync] Copying remote path: '{sourcePath}' -> '{destPath}'...");

                // Attempt adb shell cp -r "sourcePath" "destPath"
                var (success, output) = await ExecuteCommandAsync($"shell cp -r \"{sourcePath}\" \"{destPath}\"", serialNumber);
                _logger.LogInfo($"[CopyRemoteItemAsync] adb shell cp command output: '{output}' (Success={success})");

                if (success && (string.IsNullOrWhiteSpace(output) || (!output.Contains("cp:") && !output.Contains("not found") && !output.Contains("Error") && !output.Contains("Permission denied"))))
                {
                    _logger.LogInfo($"[CopyRemoteItemAsync] adb shell cp succeeded for '{sourcePath}' -> '{destPath}'.");
                    return (true, "Copy successful.");
                }

                _logger.LogWarning($"[CopyRemoteItemAsync] adb shell cp failed ({output}). Falling back to adb pull -> adb push...");

                // Fallback: Pull to temp local dir, then Push to destination
                string tempLocalDir = Path.Combine(Path.GetTempPath(), "UnlockMateCopy_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempLocalDir);

                try
                {
                    _logger.LogInfo($"[CopyRemoteItemAsync] Pulling '{sourcePath}' to temporary directory '{tempLocalDir}'...");
                    var pullRes = await PullFileAsync(sourcePath, tempLocalDir, serialNumber);
                    if (!pullRes.Success)
                    {
                        _logger.LogError($"[CopyRemoteItemAsync] Fallback copy failed on adb pull: {pullRes.Message}");
                        return (false, $"Copy failed during pull: {pullRes.Message}");
                    }

                    string downloadedName = Path.GetFileName(sourcePath.TrimEnd('/'));
                    string localItemPath = Path.Combine(tempLocalDir, downloadedName);

                    if (!File.Exists(localItemPath) && !Directory.Exists(localItemPath))
                    {
                        var entries = Directory.GetFileSystemEntries(tempLocalDir);
                        if (entries.Length > 0) localItemPath = entries[0];
                    }

                    string destParentDir = Path.GetDirectoryName(destPath.TrimEnd('/'))?.Replace('\\', '/') ?? "/sdcard";
                    _logger.LogInfo($"[CopyRemoteItemAsync] Pushing local item '{localItemPath}' to destination parent '{destParentDir}'...");

                    var pushRes = await PushFilesAndFoldersAsync(new[] { localItemPath }, destParentDir, serialNumber, progress, cancellationToken);
                    if (!pushRes.Success)
                    {
                        _logger.LogError($"[CopyRemoteItemAsync] Fallback copy failed on adb push: {pushRes.Message}");
                        return (false, $"Copy failed during push: {pushRes.Message}");
                    }

                    _logger.LogInfo($"[CopyRemoteItemAsync] Fallback adb pull/push copy completed successfully for '{sourcePath}'.");
                    return (true, "Copy successful via fallback.");
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(tempLocalDir))
                            Directory.Delete(tempLocalDir, recursive: true);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[CopyRemoteItemAsync] Exception: {ex.Message}");
                return (false, $"Copy error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> MoveRemoteItemAsync(
            string sourcePath,
            string destPath,
            string? serialNumber,
            IProgress<BackupProgressInfo>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destPath))
                {
                    _logger.LogError("[MoveRemoteItemAsync] Failed: Invalid source or destination path.");
                    return (false, "Source or destination path is invalid.");
                }

                _logger.LogInfo($"[MoveRemoteItemAsync] Moving remote item: '{sourcePath}' -> '{destPath}'...");

                var (success, output) = await ExecuteCommandAsync($"shell mv \"{sourcePath}\" \"{destPath}\"", serialNumber);
                _logger.LogInfo($"[MoveRemoteItemAsync] adb shell mv command output: '{output}' (Success={success})");

                if (success && (string.IsNullOrWhiteSpace(output) || (!output.Contains("failed") && !output.Contains("Error") && !output.Contains("Permission denied"))))
                {
                    _logger.LogInfo($"[MoveRemoteItemAsync] adb shell mv succeeded for '{sourcePath}' -> '{destPath}'.");
                    return (true, "Move successful.");
                }

                _logger.LogWarning($"[MoveRemoteItemAsync] adb shell mv failed ({output}). Falling back to Copy + Delete...");
                var copyRes = await CopyRemoteItemAsync(sourcePath, destPath, serialNumber, progress, cancellationToken);
                if (copyRes.Success)
                {
                    _logger.LogInfo($"[MoveRemoteItemAsync] Fallback copy succeeded. Deleting original source '{sourcePath}'...");
                    await DeleteFileAsync(sourcePath, serialNumber);
                    return (true, "Move successful via fallback copy/delete.");
                }

                return copyRes;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MoveRemoteItemAsync] Exception: {ex.Message}");
                return (false, $"Move error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> CreateDirectoryAsync(string remotePath, string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync($"shell mkdir -p \"{remotePath}\"", serialNumber);
            return (success, output);
        }

        public async Task<List<string>> EnumerateRemoteStoragePathsAsync(string remoteBasePath, string? serialNumber)
        {
            var paths = new List<string>();
            string targetPath = string.IsNullOrWhiteSpace(remoteBasePath) ? "/sdcard" : remoteBasePath.TrimEnd('/');
            _logger.LogInfo($"Enumerating device internal storage at: {targetPath}...");

            var (success, output) = await ExecuteCommandAsync($"shell find \"{targetPath}\"", serialNumber);
            if (success && !string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    if (trimmed.StartsWith("find:", StringComparison.OrdinalIgnoreCase)) continue;
                    paths.Add(trimmed);
                }
            }

            return paths;
        }

        public async Task<long> GetRemoteStorageSizeBytesAsync(string remoteBasePath, string? serialNumber)
        {
            string targetPath = string.IsNullOrWhiteSpace(remoteBasePath) ? "/sdcard" : remoteBasePath.TrimEnd('/');

            // Try du -sb first
            var (duSuccess, duOutput) = await ExecuteCommandAsync($"shell du -sb \"{targetPath}\"", serialNumber);
            if (duSuccess && !string.IsNullOrWhiteSpace(duOutput))
            {
                var match = Regex.Match(duOutput, @"^(\d+)\s+");
                if (match.Success && long.TryParse(match.Groups[1].Value, out long bytes) && bytes > 0)
                {
                    return bytes;
                }
            }

            // Fallback: df -k /sdcard
            var (dfSuccess, dfOutput) = await ExecuteCommandAsync($"shell df -k \"{targetPath}\"", serialNumber);
            if (dfSuccess && !string.IsNullOrWhiteSpace(dfOutput))
            {
                var lines = dfOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1)
                {
                    var tokens = Regex.Split(lines[1].Trim(), @"\s+");
                    if (tokens.Length >= 3 && long.TryParse(tokens[2], out long usedKb) && usedKb > 0)
                    {
                        return usedKb * 1024;
                    }
                }
            }

            return 0;
        }

        public async Task<StorageInfo> GetStorageInfoAsync(string? serialNumber)
        {
            var info = new StorageInfo();
            string[] cmdCandidates = new[] { "shell df -k /sdcard", "shell df /sdcard", "shell df -k /storage/emulated/0" };

            foreach (var cmd in cmdCandidates)
            {
                var (success, output) = await ExecuteCommandAsync(cmd, serialNumber);
                if (success && !string.IsNullOrWhiteSpace(output))
                {
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("1K-blocks", StringComparison.OrdinalIgnoreCase)) continue;

                        var tokens = Regex.Split(line.Trim(), @"\s+");
                        if (tokens.Length >= 4)
                        {
                            for (int i = 1; i < tokens.Length - 2; i++)
                            {
                                if (long.TryParse(tokens[i], out long totalKb) &&
                                    long.TryParse(tokens[i + 1], out long usedKb) &&
                                    long.TryParse(tokens[i + 2], out long freeKb) &&
                                    totalKb > 1024)
                                {
                                    info.TotalBytes = totalKb * 1024;
                                    info.UsedBytes = usedKb * 1024;
                                    info.FreeBytes = freeKb * 1024;
                                    return info;
                                }
                            }
                        }
                    }
                }
            }

            return info;
        }

        public async Task<(bool Success, string Message)> PushFilesAndFoldersAsync(
            string[] localPaths,
            string remoteDestinationDir,
            string? serialNumber,
            IProgress<BackupProgressInfo>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (localPaths == null || localPaths.Length == 0) return (true, "No files specified.");

            var progressInfo = new BackupProgressInfo
            {
                StatusText = "Preparing file upload..."
            };

            var allFiles = new List<string>();
            long totalBytes = 0;

            foreach (var path in localPaths)
            {
                if (File.Exists(path))
                {
                    allFiles.Add(path);
                    totalBytes += new FileInfo(path).Length;
                }
                else if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    allFiles.AddRange(files);
                    totalBytes += files.Sum(f => new FileInfo(f).Length);
                }
            }

            progressInfo.TotalFiles = Math.Max(1, allFiles.Count);
            progressInfo.TotalBytes = totalBytes;
            progress?.Report(progressInfo);

            int processedFiles = 0;
            long transferredBytes = 0;
            var sw = Stopwatch.StartNew();

            string remoteDirClean = remoteDestinationDir.TrimEnd('/');

            foreach (var localPath in localPaths)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string name = Path.GetFileName(localPath);
                string remoteTarget = $"{remoteDirClean}/{name}";

                progressInfo.CurrentItemName = $"Uploading: {name}";
                progressInfo.StatusText = $"Uploading [{processedFiles + 1}/{allFiles.Count}]: {name}...";
                progress?.Report(progressInfo);

                var (success, msg) = await PushFileAsync(localPath, remoteTarget, serialNumber);
                if (!success)
                {
                    _logger.LogWarning($"Push warning for {name}: {msg}");
                }

                if (File.Exists(localPath))
                {
                    processedFiles++;
                    transferredBytes += new FileInfo(localPath).Length;
                }
                else if (Directory.Exists(localPath))
                {
                    var subFiles = Directory.GetFiles(localPath, "*", SearchOption.AllDirectories);
                    processedFiles += subFiles.Length;
                    transferredBytes += subFiles.Sum(f => new FileInfo(f).Length);
                }

                double elapsed = Math.Max(0.1, sw.Elapsed.TotalSeconds);
                double speed = transferredBytes / elapsed;
                progressInfo.TransferredFiles = processedFiles;
                progressInfo.TransferredBytes = transferredBytes;
                progressInfo.BytesPerSecond = speed;

                if (speed > 1024 * 1024)
                    progressInfo.TransferSpeedText = $"{speed / (1024.0 * 1024.0):F1} MB/s";
                else
                    progressInfo.TransferSpeedText = $"{speed / 1024.0:F1} KB/s";

                double remainingBytes = Math.Max(0, totalBytes - transferredBytes);
                double etaSec = speed > 0 ? remainingBytes / speed : 0;
                var eta = TimeSpan.FromSeconds(etaSec);
                progressInfo.RemainingTimeText = $"ETA: {eta:mm\\:ss}";
                progressInfo.OverallProgress = ((double)processedFiles / Math.Max(1, allFiles.Count)) * 100.0;

                progress?.Report(progressInfo);
            }

            return (true, $"Uploaded {processedFiles} file(s) successfully.");
        }

        public async Task<(bool Success, string Message)> PullFilesAndFoldersAsync(
            List<FileItem> remoteItems,
            string localDestinationDir,
            string? serialNumber,
            IProgress<BackupProgressInfo>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (remoteItems == null || remoteItems.Count == 0) return (true, "No items selected.");

            if (!Directory.Exists(localDestinationDir))
            {
                Directory.CreateDirectory(localDestinationDir);
            }

            var progressInfo = new BackupProgressInfo
            {
                StatusText = "Preparing file download..."
            };

            long estimatedTotalBytes = remoteItems.Sum(i => i.SizeBytes);
            progressInfo.TotalFiles = remoteItems.Count;
            progressInfo.TotalBytes = estimatedTotalBytes;
            progress?.Report(progressInfo);

            int processedFiles = 0;
            long transferredBytes = 0;
            var sw = Stopwatch.StartNew();

            foreach (var item in remoteItems)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string localTargetPath = Path.Combine(localDestinationDir, item.Name);
                progressInfo.CurrentItemName = $"Downloading: {item.Name}";
                progressInfo.StatusText = $"Downloading [{processedFiles + 1}/{remoteItems.Count}]: {item.Name}...";
                progress?.Report(progressInfo);

                var (success, msg) = await PullFileAsync(item.FullPath, localTargetPath, serialNumber);
                if (!success)
                {
                    _logger.LogWarning($"Pull warning for {item.Name}: {msg}");
                }

                processedFiles++;
                if (File.Exists(localTargetPath))
                {
                    transferredBytes += new FileInfo(localTargetPath).Length;
                    // Preserve timestamp
                    try { File.SetLastWriteTime(localTargetPath, item.LastModified); } catch { }
                }
                else if (Directory.Exists(localTargetPath))
                {
                    var files = Directory.GetFiles(localTargetPath, "*", SearchOption.AllDirectories);
                    transferredBytes += files.Sum(f => new FileInfo(f).Length);
                }

                double elapsed = Math.Max(0.1, sw.Elapsed.TotalSeconds);
                double speed = transferredBytes / elapsed;
                progressInfo.TransferredFiles = processedFiles;
                progressInfo.TransferredBytes = transferredBytes;
                progressInfo.BytesPerSecond = speed;

                if (speed > 1024 * 1024)
                    progressInfo.TransferSpeedText = $"{speed / (1024.0 * 1024.0):F1} MB/s";
                else
                    progressInfo.TransferSpeedText = $"{speed / 1024.0:F1} KB/s";

                double remainingBytes = Math.Max(0, estimatedTotalBytes - transferredBytes);
                double etaSec = speed > 0 ? remainingBytes / speed : 0;
                var eta = TimeSpan.FromSeconds(etaSec);
                progressInfo.RemainingTimeText = $"ETA: {eta:mm\\:ss}";
                progressInfo.OverallProgress = ((double)processedFiles / Math.Max(1, remoteItems.Count)) * 100.0;

                progress?.Report(progressInfo);
            }

            return (true, $"Downloaded {processedFiles} item(s) to {localDestinationDir}.");
        }

        public async Task<(bool Success, string Message)> DownloadAsZipAsync(
            List<FileItem> remoteItems,
            string localZipFilePath,
            string? serialNumber,
            IProgress<BackupProgressInfo>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "UnlockMatePro_ZipDownload", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                var (pullSuccess, pullMsg) = await PullFilesAndFoldersAsync(remoteItems, tempFolder, serialNumber, progress, cancellationToken);
                if (!pullSuccess) return (false, pullMsg);

                if (File.Exists(localZipFilePath))
                {
                    File.Delete(localZipFilePath);
                }

                ZipFile.CreateFromDirectory(tempFolder, localZipFilePath);
                return (true, $"Archive created successfully at {localZipFilePath}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
            }
        }

        // Backup & Restore Content Data
        public async Task<List<ContactItem>> ExportContactsAsync(string? serialNumber)
        {
            var list = new List<ContactItem>();
            var (success, output) = await ExecuteCommandAsync("shell content query --uri content://com.android.contacts/data/phones --projection display_name:data1:contact_id", serialNumber);
            if (!success || string.IsNullOrWhiteSpace(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var nameMatch = Regex.Match(line, @"display_name=([^,]+)");
                var phoneMatch = Regex.Match(line, @"data1=([^,]+)");
                var idMatch = Regex.Match(line, @"contact_id=(\d+)");

                if (nameMatch.Success || phoneMatch.Success)
                {
                    list.Add(new ContactItem
                    {
                        Id = idMatch.Success ? idMatch.Groups[1].Value : "0",
                        DisplayName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Unknown",
                        PhoneNumber = phoneMatch.Success ? phoneMatch.Groups[1].Value.Trim() : ""
                    });
                }
            }

            return list;
        }

        public async Task<List<SmsItem>> ExportSmsAsync(string? serialNumber)
        {
            var list = new List<SmsItem>();
            var (success, output) = await ExecuteCommandAsync("shell content query --uri content://sms/ --projection _id:address:body:date:type", serialNumber);
            if (!success || string.IsNullOrWhiteSpace(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var idMatch = Regex.Match(line, @"_id=(\d+)");
                var addrMatch = Regex.Match(line, @"address=([^,]+)");
                var bodyMatch = Regex.Match(line, @"body=(.*?)(?:, date=|, type=|$)", RegexOptions.Singleline);
                var dateMatch = Regex.Match(line, @"date=(\d+)");
                var typeMatch = Regex.Match(line, @"type=(\d+)");

                if (bodyMatch.Success)
                {
                    list.Add(new SmsItem
                    {
                        Id = idMatch.Success ? idMatch.Groups[1].Value : "0",
                        Address = addrMatch.Success ? addrMatch.Groups[1].Value : "Unknown",
                        Body = bodyMatch.Groups[1].Value,
                        Date = dateMatch.Success ? dateMatch.Groups[1].Value : DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(),
                        Type = typeMatch.Success ? typeMatch.Groups[1].Value : "1"
                    });
                }
            }

            return list;
        }

        public async Task<List<CallLogItem>> ExportCallLogsAsync(string? serialNumber)
        {
            var list = new List<CallLogItem>();
            var (success, output) = await ExecuteCommandAsync("shell content query --uri content://call_log/calls --projection _id:number:name:date:duration:type", serialNumber);
            if (!success || string.IsNullOrWhiteSpace(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var idMatch = Regex.Match(line, @"_id=(\d+)");
                var numMatch = Regex.Match(line, @"number=([^,]+)");
                var nameMatch = Regex.Match(line, @"name=([^,]+)");
                var dateMatch = Regex.Match(line, @"date=(\d+)");
                var durMatch = Regex.Match(line, @"duration=(\d+)");
                var typeMatch = Regex.Match(line, @"type=(\d+)");

                if (numMatch.Success)
                {
                    list.Add(new CallLogItem
                    {
                        Id = idMatch.Success ? idMatch.Groups[1].Value : "0",
                        Number = numMatch.Groups[1].Value,
                        CachedName = nameMatch.Success && nameMatch.Groups[1].Value != "NULL" ? nameMatch.Groups[1].Value : "",
                        Date = dateMatch.Success ? dateMatch.Groups[1].Value : DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(),
                        DurationSeconds = durMatch.Success ? durMatch.Groups[1].Value : "0",
                        Type = typeMatch.Success ? typeMatch.Groups[1].Value : "1"
                    });
                }
            }
            return list;
        }

        public async Task<(bool Success, string Message)> RestoreContactsAsync(List<ContactItem> contacts, string? serialNumber)
        {
            int restored = 0;
            foreach (var c in contacts)
            {
                if (string.IsNullOrWhiteSpace(c.PhoneNumber) && string.IsNullOrWhiteSpace(c.DisplayName))
                    continue;

                // 1. Insert into raw_contacts
                var (rawSuccess, rawOutput) = await ExecuteCommandAsync("shell content insert --uri content://com.android.contacts/raw_contacts --bind account_name:s:\"\" --bind account_type:s:\"\"", serialNumber);
                
                var rawIdMatch = Regex.Match(rawOutput, @"row.*?(\d+)");
                if (rawSuccess && rawIdMatch.Success)
                {
                    string rawId = rawIdMatch.Groups[1].Value;

                    // 2. Insert Name
                    if (!string.IsNullOrWhiteSpace(c.DisplayName))
                    {
                        await ExecuteCommandAsync($"shell content insert --uri content://com.android.contacts/data --bind raw_contact_id:i:{rawId} --bind mimetype:s:\"vnd.android.cursor.item/name\" --bind data1:s:\"{c.DisplayName.Replace("\"", "\\\"")}\"", serialNumber);
                    }

                    // 3. Insert Phone
                    if (!string.IsNullOrWhiteSpace(c.PhoneNumber))
                    {
                        await ExecuteCommandAsync($"shell content insert --uri content://com.android.contacts/data --bind raw_contact_id:i:{rawId} --bind mimetype:s:\"vnd.android.cursor.item/phone_v2\" --bind data1:s:\"{c.PhoneNumber}\" --bind data2:i:2", serialNumber);
                    }

                    restored++;
                }
            }
            return (restored > 0, $"Restored {restored} contact(s) to device.");
        }

        public async Task<(bool Success, string Message)> RestoreSmsAsync(List<SmsItem> smsList, string? serialNumber)
        {
            int restored = 0;
            foreach (var s in smsList)
            {
                if (string.IsNullOrWhiteSpace(s.Address)) continue;
                
                string safeBody = s.Body.Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`");
                long.TryParse(s.Date, out long dateVal);
                if (dateVal == 0) dateVal = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                int.TryParse(s.Type, out int typeVal);
                if (typeVal == 0) typeVal = 1;

                var (success, _) = await ExecuteCommandAsync($"shell content insert --uri content://sms --bind address:s:\"{s.Address}\" --bind body:s:\"{safeBody}\" --bind date:l:{dateVal} --bind type:i:{typeVal}", serialNumber);
                if (success) restored++;
            }
            return (restored > 0, $"Restored {restored} SMS message(s) to device.");
        }

        public async Task<(bool Success, string Message)> RestoreCallLogsAsync(List<CallLogItem> callLogs, string? serialNumber)
        {
            int restored = 0;
            foreach (var cl in callLogs)
            {
                if (string.IsNullOrWhiteSpace(cl.Number)) continue;
                
                long.TryParse(cl.Date, out long dateVal);
                if (dateVal == 0) dateVal = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                int.TryParse(cl.Type, out int typeVal);
                if (typeVal == 0) typeVal = 1;

                long.TryParse(cl.DurationSeconds, out long durVal);

                var (success, _) = await ExecuteCommandAsync($"shell content insert --uri content://call_log/calls --bind number:s:\"{cl.Number}\" --bind type:i:{typeVal} --bind date:l:{dateVal} --bind duration:l:{durVal}", serialNumber);
                if (success) restored++;
            }
            return (restored > 0, $"Restored {restored} call log(s) to device.");
        }

        // System Metrics & Diagnostics
        public async Task<SystemStats> GetSystemStatsAsync(string? serialNumber)
        {
            var stats = new SystemStats();

            var (_, memOut) = await ExecuteCommandAsync("shell cat /proc/meminfo", serialNumber);
            if (!string.IsNullOrWhiteSpace(memOut))
            {
                var totalMatch = Regex.Match(memOut, @"MemTotal:\s*(\d+)");
                var availableMatch = Regex.Match(memOut, @"MemAvailable:\s*(\d+)");
                var freeMatch = Regex.Match(memOut, @"MemFree:\s*(\d+)");
                var buffersMatch = Regex.Match(memOut, @"Buffers:\s*(\d+)");
                var cachedMatch = Regex.Match(memOut, @"Cached:\s*(\d+)");

                if (totalMatch.Success)
                {
                    double totalKb = double.Parse(totalMatch.Groups[1].Value);
                    double availableKb = 0;

                    if (availableMatch.Success)
                    {
                        availableKb = double.Parse(availableMatch.Groups[1].Value);
                    }
                    else if (freeMatch.Success)
                    {
                        availableKb = double.Parse(freeMatch.Groups[1].Value);
                        if (buffersMatch.Success) availableKb += double.Parse(buffersMatch.Groups[1].Value);
                        if (cachedMatch.Success) availableKb += double.Parse(cachedMatch.Groups[1].Value);
                    }

                    stats.RamTotalMb = totalKb / 1024.0;
                    stats.RamUsedMb = (totalKb - availableKb) / 1024.0;
                }
            }

            var storage = await GetStorageInfoAsync(serialNumber);
            if (storage.TotalBytes > 0)
            {
                stats.StorageTotalGb = storage.TotalBytes / (1024.0 * 1024.0 * 1024.0);
                stats.StorageUsedGb = storage.UsedBytes / (1024.0 * 1024.0 * 1024.0);
            }

            var (_, secOut) = await ExecuteCommandAsync("shell getprop ro.build.version.security_patch", serialNumber);
            if (!string.IsNullOrWhiteSpace(secOut)) stats.SecurityPatch = secOut;

            var (_, fpOut) = await ExecuteCommandAsync("shell getprop ro.build.fingerprint", serialNumber);
            if (!string.IsNullOrWhiteSpace(fpOut)) stats.BuildFingerprint = fpOut;

            return stats;
        }

        public async Task<bool> CheckRootAsync(string? serialNumber)
        {
            var (success, output) = await ExecuteCommandAsync("shell su -c id", serialNumber);
            return success && output.Contains("uid=0(root)");
        }

        public async Task<(bool Success, string OutputPath)> GenerateBugReportAsync(string? serialNumber, string destinationFolder)
        {
            string file = Path.Combine(destinationFolder, $"bugreport_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            _logger.LogInfo("Generating ADB Bug Report...");
            var (success, output) = await ExecuteCommandAsync($"bugreport \"{file}\"", serialNumber);
            return (success, file);
        }

        // Advanced ADB Actions
        public async Task<(bool Success, string Message)> EnableWirelessAdbAsync(string? serialNumber, int port = 5555)
        {
            var (success, output) = await ExecuteCommandAsync($"tcpip {port}", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> ConnectWirelessDeviceAsync(string ipAddress, int port = 5555)
        {
            string target = ipAddress.Contains(":") ? ipAddress : $"{ipAddress}:{port}";
            var (success, output) = await ExecuteCommandAsync($"connect {target}");
            return (success, output);
        }

        public async Task<(bool Success, string Message)> RebootDeviceAsync(string? serialNumber, string mode = "")
        {
            string cmd = string.IsNullOrWhiteSpace(mode) ? "reboot" : $"reboot {mode}";
            var (success, output) = await ExecuteCommandAsync(cmd, serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> RebootEdlAsync(string? serialNumber)
        {
            _logger.LogWarning("Rebooting device into EDL emergency mode...");
            var (success, output) = await ExecuteCommandAsync("reboot edl", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string Message)> SideloadZipAsync(string zipPath, string? serialNumber)
        {
            _logger.LogInfo($"Sideloading OTA/Update package: {zipPath}...");
            var (success, output) = await ExecuteCommandAsync($"sideload \"{zipPath}\"", serialNumber);
            return (success, output);
        }

        public async Task<(bool Success, string FilePath)> TakeScreenshotAsync(string? serialNumber, string destinationFolder)
        {
            string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string localPath = Path.Combine(destinationFolder, fileName);
            string remotePath = $"/sdcard/{fileName}";

            var (capSuccess, _) = await ExecuteCommandAsync($"shell screencap -p {remotePath}", serialNumber);
            if (!capSuccess) return (false, "Failed screencap.");

            var (pullSuccess, _) = await ExecuteCommandAsync($"pull {remotePath} \"{localPath}\"", serialNumber);
            await ExecuteCommandAsync($"shell rm {remotePath}", serialNumber);

            return (pullSuccess && File.Exists(localPath), localPath);
        }

        public async Task<(bool Success, string Message)> OpenDeviceStorageAsync(string? serialNumber)
        {
            string tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Unlock Mate Pro");
            Directory.CreateDirectory(tempFolder);
            Process.Start("explorer.exe", tempFolder);
            return await Task.FromResult((true, "Opened workspace folder."));
        }
    }
}

