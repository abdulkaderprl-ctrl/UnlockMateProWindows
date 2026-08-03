using System.Threading.Tasks;

namespace UnlockMatePro.Services
{
    public interface IAppleService
    {
        bool IsAppleToolAvailable(string toolName);
        Task<string> RunAppleToolAsync(string toolName, string arguments);
        Task<ViewModels.DeviceInfo?> DetectDeviceAsync();
        Task<string> ReadInfoAsync();
        Task<string> EnterRecoveryModeAsync();
        Task<string> ExitRecoveryModeAsync();
        Task<string> RebootDeviceAsync();
        Task<string> FlashIpswAsync(string ipswPath);
        Task<string> RestoreFirmwareAsync(string ipswPath);
        Task<string> CheckActivationStatusAsync();
        Task<string> CheckFindMyIphoneAsync();
    }
}
