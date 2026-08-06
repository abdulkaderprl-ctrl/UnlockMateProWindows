using System;
using System.Linq;
using System.Windows;
using UnlockMatePro.Services;
using UnlockMatePro.ViewModels;

namespace UnlockMatePro.Core.Services
{
    public static class ThemeManager
    {
        public static void SetTheme(string themeName)
        {
            var application = Application.Current;
            if (application == null) return;

            var dictionaries = application.Resources.MergedDictionaries;
            var themeDict = dictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

            if (themeDict != null)
            {
                dictionaries.Remove(themeDict);
            }

            var newThemeDict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Styles/Themes/{themeName}Theme.xaml", UriKind.Absolute)
            };

            dictionaries.Add(newThemeDict);
            
            // Persist setting
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                // We should theoretically save it in SettingsService
            }
        }
        
        public static void InitializeTheme(ISettingsService settingsService)
        {
            string theme = settingsService.Settings.Theme;
            if (string.IsNullOrEmpty(theme)) theme = "Dark";
            SetTheme(theme);
        }
        
        public static void ToggleTheme(ISettingsService settingsService)
        {
            string currentTheme = settingsService.Settings.Theme;
            if (string.IsNullOrEmpty(currentTheme)) currentTheme = "Dark";
            
            string newTheme = currentTheme == "Dark" ? "Light" : "Dark";
            settingsService.Settings.Theme = newTheme;
            _ = settingsService.SaveSettingsAsync();
            SetTheme(newTheme);
        }
    }
}
