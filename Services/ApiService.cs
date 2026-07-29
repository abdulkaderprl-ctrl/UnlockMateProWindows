using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnlockMatePro.Models;

namespace UnlockMatePro.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly ILoggerService _logger;
        private string? _jwtToken;

        public ApiService(ISettingsService settingsService, ILoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private string BaseUrl => _settingsService.Settings.ApiBaseUrl.TrimEnd('/');

        public void SetAuthToken(string token)
        {
            _jwtToken = token;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public void ClearAuthToken()
        {
            _jwtToken = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                string url = $"{BaseUrl}/{endpoint.TrimStart('/')}";
                _logger.LogInfo($"API GET: {url}");

                var response = await _httpClient.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    return new ApiResponse<T> { Success = true, Data = data, Message = "Success" };
                }

                return new ApiResponse<T> { Success = false, Message = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" };
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"API GET Exception: {ex.Message}");
                return new ApiResponse<T> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object data)
        {
            try
            {
                string url = $"{BaseUrl}/{endpoint.TrimStart('/')}";
                _logger.LogInfo($"API POST: {url}");

                string reqJson = JsonSerializer.Serialize(data, JsonOptions);
                var content = new StringContent(reqJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var resData = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    return new ApiResponse<T> { Success = true, Data = resData, Message = "Success" };
                }

                return new ApiResponse<T> { Success = false, Message = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" };
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"API POST Exception: {ex.Message}");
                return new ApiResponse<T> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<T>> PutAsync<T>(string endpoint, object data)
        {
            try
            {
                string url = $"{BaseUrl}/{endpoint.TrimStart('/')}";
                string reqJson = JsonSerializer.Serialize(data, JsonOptions);
                var content = new StringContent(reqJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(url, content);
                string json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var resData = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    return new ApiResponse<T> { Success = true, Data = resData, Message = "Success" };
                }

                return new ApiResponse<T> { Success = false, Message = $"HTTP {(int)response.StatusCode}" };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<T>> DeleteAsync<T>(string endpoint)
        {
            try
            {
                string url = $"{BaseUrl}/{endpoint.TrimStart('/')}";
                var response = await _httpClient.DeleteAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var resData = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    return new ApiResponse<T> { Success = true, Data = resData, Message = "Success" };
                }

                return new ApiResponse<T> { Success = false, Message = $"HTTP {(int)response.StatusCode}" };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T> { Success = false, Message = ex.Message };
            }
        }

        private static JsonSerializerOptions JsonOptions => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }
}

