using System.Collections.Generic;
using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
{
    public interface IFastbootService
    {
        string FastbootExecutablePath { get; }
        bool IsFastbootAvailable { get; }

        Task<bool> DetectAndSetFastbootPathAsync(string customAdbPath = "");
        Task<List<FastbootDevice>> GetConnectedFastbootDevicesAsync();
        Task<(bool Success, string Output)> ExecuteFastbootCommandAsync(string arguments, string? serialNumber = null, System.Threading.CancellationToken cancellationToken = default);
        Task<(bool Success, string Message)> FlashImageAsync(string partition, string imagePath, string? serialNumber);
        Task<(bool Success, string Message)> FlashAllPartitionsAsync(string? serialNumber);
        Task<(bool Success, string Message)> BootImageAsync(string imagePath, string? serialNumber);
        Task<(bool Success, string Message)> ErasePartitionAsync(string partition, string? serialNumber);
        Task<(bool Success, string Message)> OemUnlockAsync(string? serialNumber);
        Task<(bool Success, string Message)> OemLockAsync(string? serialNumber);
        Task<(bool Success, string Message)> RebootFastbootAsync(string? serialNumber, string mode = "");
        Task<(bool Success, string Output)> GetVarAllAsync(string? serialNumber);
        Task<(bool Success, string Status)> GetFrpStatusAsync(string? serialNumber);
    }
}

