using System;
using System.Threading.Tasks;

namespace UnlockMatePro.Services
{
    public interface IToolDownloaderService
    {
        Task<bool> DownloadPlatformToolsAsync(IProgress<double>? progress = null);
        Task<bool> DownloadScrcpyAsync(IProgress<double>? progress = null);
    }
}

