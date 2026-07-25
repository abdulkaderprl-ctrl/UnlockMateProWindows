using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public interface IAdbService
    {
        string AdbExecutablePath { get; }
        bool IsAdbAvailable { get; }

        Task<bool> DetectAndSetAdbPathAsync(string customPath = "");
        Task<List<AdbDevice>> GetConnectedDevicesAsync();
        Task<AdbDevice?> GetDeviceDetailsAsync(string serialNumber);
        Task<(bool Success, string Output)> ExecuteCommandAsync(string arguments, string? serialNumber = null);

        // Universal Professional APK Operations
        Task<(bool PreCheckPassed, string Message)> PreInstallCheckAsync(ApkInfo apkInfo, string? serialNumber);
        Task<(bool Success, string Message, string DetailedLog)> InstallApkAsync(
            string apkPath,
            string? serialNumber,
            bool reinstall = true,
            bool grantPermissions = true,
            bool allowDowngrade = false,
            bool autoUninstallOnConflict = false,
            IProgress<double>? progress = null,
            IProgress<string>? logProgress = null);

        Task<bool> VerifyPackageInstalledAsync(string packageName, string? serialNumber);
        Task<(bool Success, string Message)> UninstallApkAsync(string packageName, string? serialNumber);
        Task<ApkInfo> GetApkInfoAsync(string apkPath);
        Task<List<AppInfo>> GetInstalledAppsAsync(string? serialNumber, bool includeSystemApps = false);
        Task<(bool Success, string Message)> BackupApkAsync(string packageName, string destinationPath, string? serialNumber);

        // App Management Controls
        Task<(bool Success, string Message)> EnableAppAsync(string packageName, string? serialNumber);
        Task<(bool Success, string Message)> DisableAppAsync(string packageName, string? serialNumber);
        Task<(bool Success, string Message)> ForceStopAppAsync(string packageName, string? serialNumber);
        Task<(bool Success, string Message)> ClearAppDataAsync(string packageName, string? serialNumber);
        Task<(bool Success, string Message)> GrantPermissionAsync(string packageName, string permission, string? serialNumber);
        Task<(bool Success, string Message)> RevokePermissionAsync(string packageName, string permission, string? serialNumber);

        // Device File Explorer
        Task<List<FileItem>> GetDirectoryFilesAsync(string remotePath, string? serialNumber);
        Task<(bool Success, string Message)> PushFileAsync(string localPath, string remotePath, string? serialNumber);
        Task<(bool Success, string Message)> PullFileAsync(string remotePath, string localPath, string? serialNumber);
        Task<(bool Success, string Message)> DeleteFileAsync(string remotePath, string? serialNumber);
        Task<(bool Success, string Message)> RenameFileAsync(string oldPath, string newPath, string? serialNumber);
        Task<(bool Success, string Message)> CreateDirectoryAsync(string remotePath, string? serialNumber);

        // Backup & Restore Data
        Task<List<ContactItem>> ExportContactsAsync(string? serialNumber);
        Task<List<SmsItem>> ExportSmsAsync(string? serialNumber);
        Task<List<CallLogItem>> ExportCallLogsAsync(string? serialNumber);
        Task<(bool Success, string Message)> RestoreContactsAsync(List<ContactItem> contacts, string? serialNumber);
        Task<(bool Success, string Message)> RestoreSmsAsync(List<SmsItem> smsList, string? serialNumber);
        Task<(bool Success, string Message)> RestoreCallLogsAsync(List<CallLogItem> callLogs, string? serialNumber);

        // System Metrics & Diagnostics
        Task<SystemStats> GetSystemStatsAsync(string? serialNumber);
        Task<bool> CheckRootAsync(string? serialNumber);
        Task<(bool Success, string OutputPath)> GenerateBugReportAsync(string? serialNumber, string destinationFolder);

        // Advanced ADB Tools
        Task<(bool Success, string Message)> EnableWirelessAdbAsync(string? serialNumber, int port = 5555);
        Task<(bool Success, string Message)> ConnectWirelessDeviceAsync(string ipAddress, int port = 5555);
        Task<(bool Success, string Message)> RebootDeviceAsync(string? serialNumber, string mode = "");
        Task<(bool Success, string Message)> RebootEdlAsync(string? serialNumber);
        Task<(bool Success, string Message)> SideloadZipAsync(string zipPath, string? serialNumber);
        Task<(bool Success, string FilePath)> TakeScreenshotAsync(string? serialNumber, string destinationFolder);
        Task<(bool Success, string Message)> OpenDeviceStorageAsync(string? serialNumber);
    }
}
