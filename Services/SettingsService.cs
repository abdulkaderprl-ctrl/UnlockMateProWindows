using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AdbEasyInstaller.Models;

namespace AdbEasyInstaller.Services
{
    public class SettingsService : ISettingsService
    {
        private string _settingsFilePath;
        public AppSettings Settings { get; private set; } = new AppSettings();

        public SettingsService()
        {
            // Portable Mode Check: if settings.json exists in executable base directory, use portable location
            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(portablePath))
            {
                _settingsFilePath = portablePath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "AdbEasyInstaller");
                Directory.CreateDirectory(folder);
                _settingsFilePath = Path.Combine(folder, "settings.json");
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = await File.ReadAllTextAsync(_settingsFilePath);
                    var deserialized = JsonSerializer.Deserialize<AppSettings>(json);
                    if (deserialized != null)
                    {
                        Settings = deserialized;
                    }
                }
            }
            catch
            {
                Settings = new AppSettings();
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                // Re-evaluate path if user toggled Portable Mode
                if (Settings.IsPortableMode)
                {
                    _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                }
                else
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string folder = Path.Combine(appData, "AdbEasyInstaller");
                    Directory.CreateDirectory(folder);
                    _settingsFilePath = Path.Combine(folder, "settings.json");
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Settings, options);
                await File.WriteAllTextAsync(_settingsFilePath, json);
            }
            catch { }
        }

        public void UpdateSettings(AppSettings settings)
        {
            Settings = settings;
            _ = SaveSettingsAsync();
        }
    }
}
