using System.Threading.Tasks;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public interface IApiService
    {
        void SetAuthToken(string token);
        void ClearAuthToken();
        Task<ApiResponse<T>> GetAsync<T>(string endpoint);
        Task<ApiResponse<T>> PostAsync<T>(string endpoint, object data);
        Task<ApiResponse<T>> PutAsync<T>(string endpoint, object data);
        Task<ApiResponse<T>> DeleteAsync<T>(string endpoint);
    }
}
