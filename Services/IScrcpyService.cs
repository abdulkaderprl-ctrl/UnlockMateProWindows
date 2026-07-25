using System.Threading.Tasks;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public interface IScrcpyService
    {
        string ScrcpyExecutablePath { get; }
        bool IsScrcpyAvailable { get; }

        Task<bool> DetectAndSetScrcpyPathAsync(string customPath = "");
        Task<(bool Success, string Message)> LaunchMirroringAsync(string? serialNumber, AppSettings settings, string? recordFilePath = null);
        void StopMirroring();
    }
}
