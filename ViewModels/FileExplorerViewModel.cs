using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using AdbEasyInstaller.Models;
using AdbEasyInstaller.Services;

namespace AdbEasyInstaller.ViewModels
{
    public class FileExplorerViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;
        private string _currentPath = "/sdcard";
        private string _searchText = string.Empty;
        private FileItem? _selectedItem;
        private string _defaultDownloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Navigation History Stack
        private readonly Stack<string> _backStack = new Stack<string>();
        private readonly Stack<string> _forwardStack = new Stack<string>();

        // Clipboard for Copy / Cut / Paste
        private FileItem? _clipboardItem;
        private bool _isCutOperation = false;

        // Cancellation Token for Transfers
        private CancellationTokenSource? _transferCts;

        private bool _isLoading = false;
        private bool _isTransferring = false;
        private double _transferProgress = 0;
        private string _transferSpeedText = string.Empty;
        private string _transferStatusText = string.Empty;
        private string _remainingTimeText = string.Empty;

        private List<FileItem> _rawItems = new List<FileItem>();

        public ObservableCollection<FileItem> DirectoryItems { get; } = new ObservableCollection<FileItem>();

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set
            {
                if (SetProperty(ref _targetSerialNumber, value))
                {
                    _ = LoadDirectoryAsync(_currentPath, addToHistory: false);
                }
            }
        }

        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplySearchFilter();
                }
            }
        }

        public FileItem? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public string DefaultDownloadFolder
        {
            get => _defaultDownloadFolder;
            set => SetProperty(ref _defaultDownloadFolder, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsTransferring
        {
            get => _isTransferring;
            set => SetProperty(ref _isTransferring, value);
        }

        public double TransferProgress
        {
            get => _transferProgress;
            set => SetProperty(ref _transferProgress, value);
        }

        public string TransferSpeedText
        {
            get => _transferSpeedText;
            set => SetProperty(ref _transferSpeedText, value);
        }

        public string TransferStatusText
        {
            get => _transferStatusText;
            set => SetProperty(ref _transferStatusText, value);
        }

        public string RemainingTimeText
        {
            get => _remainingTimeText;
            set => SetProperty(ref _remainingTimeText, value);
        }

        public bool CanGoBack => _backStack.Count > 0;
        public bool CanGoForward => _forwardStack.Count > 0;

        // Navigation Commands
        public ICommand RefreshCommand { get; }
        public ICommand NavigateBackCommand { get; }
        public ICommand NavigateUpCommand { get; }
        public ICommand OpenSelectedItemCommand { get; }
        public ICommand GoToPathCommand { get; }

        // Context Menu Actions
        public ICommand CopyItemCommand { get; }
        public ICommand CutItemCommand { get; }
        public ICommand PasteItemCommand { get; }
        public ICommand CopyToPcCommand { get; }
        public ICommand UploadToDeviceCommand { get; }
        public ICommand RenameItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand NewFolderCommand { get; }
        public ICommand SelectDownloadFolderCommand { get; }
        public ICommand CancelTransferCommand { get; }

        public FileExplorerViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            RefreshCommand = new AsyncRelayCommand(() => LoadDirectoryAsync(CurrentPath, addToHistory: false));
            NavigateBackCommand = new AsyncRelayCommand(NavigateBackAsync, () => CanGoBack);
            NavigateUpCommand = new AsyncRelayCommand(NavigateUpAsync);
            OpenSelectedItemCommand = new AsyncRelayCommand(OpenSelectedItemAsync);
            GoToPathCommand = new AsyncRelayCommand(() => LoadDirectoryAsync(CurrentPath, addToHistory: true));

            CopyItemCommand = new RelayCommand(CopySelectedItem, () => SelectedItem != null);
            CutItemCommand = new RelayCommand(CutSelectedItem, () => SelectedItem != null);
            PasteItemCommand = new AsyncRelayCommand(PasteClipboardItemAsync, () => _clipboardItem != null && !IsTransferring);
            CopyToPcCommand = new AsyncRelayCommand(CopyToPcAsync, () => SelectedItem != null && !IsTransferring);
            UploadToDeviceCommand = new AsyncRelayCommand(UploadToDeviceAsync, () => !IsTransferring);
            RenameItemCommand = new AsyncRelayCommand(RenameItemAsync, () => SelectedItem != null);
            DeleteItemCommand = new AsyncRelayCommand(DeleteItemAsync, () => SelectedItem != null);
            NewFolderCommand = new AsyncRelayCommand(NewFolderAsync);
            SelectDownloadFolderCommand = new RelayCommand(SelectDownloadFolder);
            CancelTransferCommand = new RelayCommand(CancelTransfer);

            _ = LoadDirectoryAsync("/sdcard", addToHistory: false);
        }

        public async Task LoadDirectoryAsync(string path, bool addToHistory = true)
        {
            if (string.IsNullOrWhiteSpace(path)) path = "/sdcard";

            if (addToHistory && !string.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase))
            {
                _backStack.Push(CurrentPath);
                _forwardStack.Clear();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
            }

            IsLoading = true;
            CurrentPath = path;

            try
            {
                var files = await _adbService.GetDirectoryFilesAsync(path, TargetSerialNumber);
                _rawItems = files.OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name).ToList();
                ApplySearchFilter();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Directory Error", $"Failed to load {path}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplySearchFilter()
        {
            DirectoryItems.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _rawItems
                : _rawItems.Where(f => f.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var item in filtered)
            {
                DirectoryItems.Add(item);
            }
        }

        private async Task OpenSelectedItemAsync()
        {
            if (SelectedItem == null) return;

            if (SelectedItem.IsDirectory)
            {
                await LoadDirectoryAsync(SelectedItem.FullPath, addToHistory: true);
            }
            else
            {
                await CopyToPcAsync();
            }
        }

        private async Task NavigateBackAsync()
        {
            if (_backStack.Count > 0)
            {
                string prevPath = _backStack.Pop();
                _forwardStack.Push(CurrentPath);
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
                await LoadDirectoryAsync(prevPath, addToHistory: false);
            }
        }

        private async Task NavigateUpAsync()
        {
            if (CurrentPath == "/" || string.IsNullOrWhiteSpace(CurrentPath)) return;

            string parent = Path.GetDirectoryName(CurrentPath.TrimEnd('/'))?.Replace('\\', '/') ?? "/";
            if (string.IsNullOrWhiteSpace(parent)) parent = "/";

            await LoadDirectoryAsync(parent, addToHistory: true);
        }

        private void CopySelectedItem()
        {
            if (SelectedItem == null) return;
            _clipboardItem = SelectedItem;
            _isCutOperation = false;
            _notificationService.ShowNotification("Clipboard", $"Copied '{SelectedItem.Name}' to clipboard.", NotificationType.Info);
        }

        private void CutSelectedItem()
        {
            if (SelectedItem == null) return;
            _clipboardItem = SelectedItem;
            _isCutOperation = true;
            _notificationService.ShowNotification("Clipboard", $"Cut '{SelectedItem.Name}' to clipboard.", NotificationType.Info);
        }

        private async Task PasteClipboardItemAsync()
        {
            if (_clipboardItem == null) return;

            string destPath = CurrentPath.EndsWith("/") ? $"{CurrentPath}{_clipboardItem.Name}" : $"{CurrentPath}/{_clipboardItem.Name}";
            TransferStatusText = _isCutOperation ? $"Moving {_clipboardItem.Name}..." : $"Copying {_clipboardItem.Name}...";

            (bool success, string message) result;
            if (_isCutOperation)
            {
                result = await _adbService.RenameFileAsync(_clipboardItem.FullPath, destPath, TargetSerialNumber);
                _clipboardItem = null;
            }
            else
            {
                result = await _adbService.ExecuteCommandAsync($"shell cp -r \"{_clipboardItem.FullPath}\" \"{destPath}\"", TargetSerialNumber);
            }

            if (result.success)
            {
                _notificationService.ShowSuccess("Paste Successful", $"Pasted to {destPath}");
                await LoadDirectoryAsync(CurrentPath, addToHistory: false);
            }
            else
            {
                _notificationService.ShowError("Paste Failed", result.message);
            }
        }

        public async Task CopyToPcAsync()
        {
            if (SelectedItem == null) return;

            string targetFolder = DefaultDownloadFolder;
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            string destFile = Path.Combine(targetFolder, SelectedItem.Name);
            IsTransferring = true;
            TransferProgress = 15;
            TransferStatusText = $"Downloading {SelectedItem.Name} to PC...";
            TransferSpeedText = "Calculating transfer metrics...";
            RemainingTimeText = "Estimating time...";

            _transferCts = new CancellationTokenSource();
            var sw = Stopwatch.StartNew();

            var (success, message) = await _adbService.PullFileAsync(SelectedItem.FullPath, destFile, TargetSerialNumber);
            sw.Stop();

            IsTransferring = false;
            if (success)
            {
                double seconds = Math.Max(0.2, sw.Elapsed.TotalSeconds);
                double sizeMb = SelectedItem.SizeBytes / (1024.0 * 1024.0);
                double speedMb = sizeMb / seconds;

                TransferSpeedText = $"Average Speed: {speedMb:F2} MB/s";
                RemainingTimeText = "Transfer Complete";
                _notificationService.ShowSuccess("Download Complete", $"Saved {SelectedItem.Name} to {destFile}");
            }
            else
            {
                _notificationService.ShowError("Download Failed", message);
            }
        }

        public async Task UploadFilesAsync(string[] localFilePaths)
        {
            if (localFilePaths == null || localFilePaths.Length == 0) return;

            IsTransferring = true;
            _transferCts = new CancellationTokenSource();
            int total = localFilePaths.Length;
            int current = 0;

            var sw = Stopwatch.StartNew();
            foreach (var file in localFilePaths)
            {
                if (_transferCts.IsCancellationRequested) break;

                current++;
                string fileName = Path.GetFileName(file);
                TransferStatusText = $"Uploading [{current}/{total}]: {fileName}...";
                TransferProgress = (double)current / total * 100;

                long fileSize = new FileInfo(file).Length;
                double sizeMb = fileSize / (1024.0 * 1024.0);

                var (success, message) = await _adbService.PushFileAsync(file, CurrentPath, TargetSerialNumber);
                if (!success)
                {
                    _notificationService.ShowError("Upload Error", $"Failed pushing {fileName}: {message}");
                }

                double elapsed = sw.Elapsed.TotalSeconds;
                double speedMb = sizeMb / Math.Max(0.5, elapsed);
                TransferSpeedText = $"Speed: {speedMb:F1} MB/s";
                RemainingTimeText = $"{total - current} item(s) remaining";
            }

            IsTransferring = false;
            _notificationService.ShowSuccess("Upload Complete", $"Uploaded {total} file(s) to device.");
            await LoadDirectoryAsync(CurrentPath, addToHistory: false);
        }

        private async Task UploadToDeviceAsync()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Files to Upload to Android Device"
            };

            if (dialog.ShowDialog() == true)
            {
                await UploadFilesAsync(dialog.FileNames);
            }
        }

        private async Task RenameItemAsync()
        {
            if (SelectedItem == null) return;

            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter new name for '{SelectedItem.Name}':",
                "Rename File / Folder",
                SelectedItem.Name);

            if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedItem.Name)
            {
                string targetPath = CurrentPath.EndsWith("/") ? $"{CurrentPath}{newName}" : $"{CurrentPath}/{newName}";
                var (success, message) = await _adbService.RenameFileAsync(SelectedItem.FullPath, targetPath, TargetSerialNumber);

                if (success)
                {
                    _notificationService.ShowSuccess("Renamed", $"Item renamed to {newName}");
                    await LoadDirectoryAsync(CurrentPath, addToHistory: false);
                }
                else
                {
                    _notificationService.ShowError("Rename Failed", message);
                }
            }
        }

        private async Task DeleteItemAsync()
        {
            if (SelectedItem == null) return;

            var (success, message) = await _adbService.DeleteFileAsync(SelectedItem.FullPath, TargetSerialNumber);
            if (success)
            {
                _notificationService.ShowSuccess("Deleted", $"Deleted {SelectedItem.Name}");
                await LoadDirectoryAsync(CurrentPath, addToHistory: false);
            }
            else
            {
                _notificationService.ShowError("Delete Failed", message);
            }
        }

        private async Task NewFolderAsync()
        {
            string folderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new directory name:",
                "Create New Folder",
                "NewFolder");

            if (!string.IsNullOrWhiteSpace(folderName))
            {
                string targetPath = CurrentPath.EndsWith("/") ? $"{CurrentPath}{folderName}" : $"{CurrentPath}/{folderName}";
                var (success, message) = await _adbService.CreateDirectoryAsync(targetPath, TargetSerialNumber);

                if (success)
                {
                    _notificationService.ShowSuccess("Folder Created", $"Created folder {folderName}");
                    await LoadDirectoryAsync(CurrentPath, addToHistory: false);
                }
                else
                {
                    _notificationService.ShowError("Create Folder Failed", message);
                }
            }
        }

        private void SelectDownloadFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Default PC Download Directory"
            };

            if (dialog.ShowDialog() == true)
            {
                DefaultDownloadFolder = dialog.FolderName;
            }
        }

        private void CancelTransfer()
        {
            _transferCts?.Cancel();
            IsTransferring = false;
            TransferStatusText = "Transfer cancelled by user.";
            _notificationService.ShowNotification("Transfer Cancelled", "User cancelled current file operation.", NotificationType.Warning);
        }
    }
}
