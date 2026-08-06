using System.Threading.Tasks;

namespace UnlockMatePro.Services
{
    public interface INothingService
    {
        Task<bool> DetectDeviceAsync();
        
        string CurrentMode { get; }
        string Model { get; }
        string Product { get; }
        string Codename { get; }
        string AndroidVersion { get; }
        string BuildNumber { get; }
        string SerialNumber { get; }
        string BootloaderState { get; }
        string Slot { get; }
        string BatteryLevel { get; }

        Task RebootSystemAsync();
        Task RebootRecoveryAsync();
        Task RebootBootloaderAsync();
        Task RebootFastbootDAsync();
        Task RebootEdlAsync();

        Task UnlockBootloaderAsync();
        Task RelockBootloaderAsync();
        Task CheckOemUnlockStatusAsync();
        Task CheckBootloaderStateAsync();

        Task FlashPartitionAsync(string partition, string filePath);
        Task FlashFirmwareAsync(string folderPath, System.Action<string> logCallback, System.Action<int> progressCallback);
    }
}
