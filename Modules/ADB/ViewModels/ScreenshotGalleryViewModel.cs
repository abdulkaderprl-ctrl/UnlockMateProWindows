using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class ScreenshotGalleryViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;
        private string? _selectedImagePath;

        public ObservableCollection<string> GalleryImages { get; } = new ObservableCollection<string>();

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set => SetProperty(ref _targetSerialNumber, value);
        }

        public string? SelectedImagePath
        {
            get => _selectedImagePath;
            set => SetProperty(ref _selectedImagePath, value);
        }

        public ICommand CaptureScreenshotCommand { get; }
        public ICommand RefreshGalleryCommand { get; }
        public ICommand OpenGalleryFolderCommand { get; }

        public ScreenshotGalleryViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            CaptureScreenshotCommand = new AsyncRelayCommand(CaptureScreenshotAsync);
            RefreshGalleryCommand = new RelayCommand(LoadGallery);
            OpenGalleryFolderCommand = new RelayCommand(OpenGalleryFolder);

            LoadGallery();
        }

        private void LoadGallery()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ADB Screenshots");
            Directory.CreateDirectory(folder);

            GalleryImages.Clear();
            var files = Directory.GetFiles(folder, "*.png").OrderByDescending(File.GetCreationTime);
            foreach (var file in files)
            {
                GalleryImages.Add(file);
            }

            if (GalleryImages.Count > 0) SelectedImagePath = GalleryImages[0];
        }

        private async Task CaptureScreenshotAsync()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ADB Screenshots");
            Directory.CreateDirectory(folder);

            var (success, filePath) = await _adbService.TakeScreenshotAsync(TargetSerialNumber, folder);
            if (success)
            {
                _notificationService.ShowSuccess("Screenshot Captured", $"Saved: {Path.GetFileName(filePath)}");
                LoadGallery();
                SelectedImagePath = filePath;
            }
            else
            {
                _notificationService.ShowError("Screenshot Error", filePath);
            }
        }

        private void OpenGalleryFolder()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ADB Screenshots");
            Directory.CreateDirectory(folder);
            try { Process.Start("explorer.exe", folder); } catch { }
        }
    }
}

