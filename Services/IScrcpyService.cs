using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
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

