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
using UnlockMatePro.Models;
using UnlockMatePro.Services;

namespace UnlockMatePro.ViewModels
{
    public class FileExplorerViewModel : ViewModelBase
    {
        private readonly IAdbService _adbService;
        private readonly ILoggerService _logger;
        private readonly INotificationService _notificationService;

        private string? _targetSerialNumber;
        private string _currentPath = "/sdcard";
        private string _searchText = string.Empty;
        private bool _showHiddenFiles = false;
        private FileItem? _selectedItem;
        private string _defaultDownloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Storage Metrics
        private StorageInfo _storageInfo = new StorageInfo();

        // Multi-Selection Tracking
        private List<FileItem> _selectedItems = new List<FileItem>();

        // Navigation History Stack
        private readonly Stack<string> _backStack = new Stack<string>();
        private readonly Stack<string> _forwardStack = new Stack<string>();

        // Clipboard for Copy / Cut / Paste
        private List<FileItem> _clipboardItems = new List<FileItem>();
        private bool _isCutOperation = false;

        // Cancellation Token for Transfers
        private CancellationTokenSource? _transferCts;

        private bool _isLoading = false;
        private bool _isTransferring = false;
        private double _transferProgress = 0;
        private string _transferSpeedText = string.Empty;
        private string _transferStatusText = string.Empty;
        private string _remainingTimeText = string.Empty;
        private string _currentItemName = string.Empty;

        // Sorting State
        private string _sortColumn = "Name";
        private bool _sortAscending = true;

        private List<FileItem> _rawItems = new List<FileItem>();

        public ObservableCollection<FileItem> DirectoryItems { get; } = new ObservableCollection<FileItem>();
        public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new ObservableCollection<BreadcrumbItem>();

        public string? TargetSerialNumber
        {
            get => _targetSerialNumber;
            set
            {
                if (SetProperty(ref _targetSerialNumber, value))
                {
                    _ = RefreshStorageAndDirectoryAsync();
                }
            }
        }

        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (SetProperty(ref _currentPath, value))
                {
                    UpdateBreadcrumbs(value);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilterAndSort();
                }
            }
        }

        public bool ShowHiddenFiles
        {
            get => _showHiddenFiles;
            set
            {
                if (SetProperty(ref _showHiddenFiles, value))
                {
                    ApplyFilterAndSort();
                }
            }
        }

        public FileItem? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public StorageInfo StorageInfo
        {
            get => _storageInfo;
            set => SetProperty(ref _storageInfo, value);
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

        public string CurrentItemName
        {
            get => _currentItemName;
            set => SetProperty(ref _currentItemName, value);
        }

        public bool CanGoBack => _backStack.Count > 0;
        public bool CanGoForward => _forwardStack.Count > 0;

        // Navigation Commands
        public ICommand RefreshCommand { get; }
        public ICommand NavigateBackCommand { get; }
        public ICommand NavigateUpCommand { get; }
        public ICommand OpenSelectedItemCommand { get; }
        public ICommand GoToPathCommand { get; }
        public ICommand NavigateBreadcrumbCommand { get; }

        // Quick Access Favorites Commands
        public ICommand JumpToDcimCommand { get; }
        public ICommand JumpToDownloadsCommand { get; }
        public ICommand JumpToPicturesCommand { get; }
        public ICommand JumpToMoviesCommand { get; }
        public ICommand JumpToMusicCommand { get; }
        public ICommand JumpToDocumentsCommand { get; }
        public ICommand JumpToWhatsAppCommand { get; }
        public ICommand JumpToTelegramCommand { get; }
        public ICommand JumpToAndroidCommand { get; }

        // Context & Toolbar Commands
        public ICommand CopyItemsCommand { get; }
        public ICommand CutItemsCommand { get; }
        public ICommand PasteItemsCommand { get; }
        public ICommand DownloadSelectedCommand { get; }
        public ICommand DownloadAsZipCommand { get; }
        public ICommand UploadToDeviceCommand { get; }
        public ICommand RenameSingleItemCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand NewFolderCommand { get; }
        public ICommand SelectDownloadFolderCommand { get; }
        public ICommand CancelTransferCommand { get; }
        public ICommand InstallApkCommand { get; }
        public ICommand SortCommand { get; }

        public FileExplorerViewModel(
            IAdbService adbService,
            ILoggerService logger,
            INotificationService notificationService)
        {
            _adbService = adbService;
            _logger = logger;
            _notificationService = notificationService;

            RefreshCommand = new AsyncRelayCommand(() => RefreshStorageAndDirectoryAsync(addToHistory: false));
            NavigateBackCommand = new AsyncRelayCommand(NavigateBackAsync, () => CanGoBack);
            NavigateUpCommand = new AsyncRelayCommand(NavigateUpAsync);
            OpenSelectedItemCommand = new AsyncRelayCommand(OpenSelectedItemAsync);
            GoToPathCommand = new AsyncRelayCommand(() => LoadDirectoryAsync(CurrentPath, addToHistory: true));
            NavigateBreadcrumbCommand = new AsyncRelayCommand<string>(path => LoadDirectoryAsync(path ?? "/sdcard", addToHistory: true));

            // Favorites
            JumpToDcimCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/DCIM"));
            JumpToDownloadsCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Download"));
            JumpToPicturesCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Pictures"));
            JumpToMoviesCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Movies"));
            JumpToMusicCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Music"));
            JumpToDocumentsCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Documents"));
            JumpToWhatsAppCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Android/media/com.whatsapp/WhatsApp"));
            JumpToTelegramCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Telegram"));
            JumpToAndroidCommand = new AsyncRelayCommand(() => LoadDirectoryAsync("/sdcard/Android"));

            // Context Actions
            CopyItemsCommand = new RelayCommand(CopySelectedItems);
            CutItemsCommand = new RelayCommand(CutSelectedItems);
            PasteItemsCommand = new AsyncRelayCommand(PasteClipboardItemsAsync, () => !IsTransferring);
            DownloadSelectedCommand = new AsyncRelayCommand(DownloadSelectedItemsAsync, () => !IsTransferring);
            DownloadAsZipCommand = new AsyncRelayCommand(DownloadSelectedAsZipAsync, () => !IsTransferring);
            UploadToDeviceCommand = new AsyncRelayCommand(UploadToDeviceAsync, () => !IsTransferring);
            RenameSingleItemCommand = new AsyncRelayCommand(RenameSingleItemAsync, () => SelectedItem != null || _selectedItems.Count == 1);
            DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedItemsAsync);
            NewFolderCommand = new AsyncRelayCommand(NewFolderAsync);
            SelectDownloadFolderCommand = new RelayCommand(SelectDownloadFolder);
            CancelTransferCommand = new RelayCommand(CancelTransfer);
            InstallApkCommand = new AsyncRelayCommand(InstallSelectedApkAsync);
            SortCommand = new RelayCommand<string>(SortByColumn);

            UpdateBreadcrumbs("/sdcard");
            _ = RefreshStorageAndDirectoryAsync(addToHistory: false);
        }

        public void SetSelectedItems(IEnumerable<FileItem> items)
        {
            _selectedItems = items.ToList();
            SelectedItem = _selectedItems.FirstOrDefault();
        }

        public async Task RefreshStorageAndDirectoryAsync(bool addToHistory = false)
        {
            _ = UpdateStorageInfoAsync();
            await LoadDirectoryAsync(CurrentPath, addToHistory);
        }

        public async Task UpdateStorageInfoAsync()
        {
            try
            {
                var stats = await _adbService.GetStorageInfoAsync(TargetSerialNumber);
                StorageInfo = stats;
            }
            catch { }
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
                _rawItems = files;
                ApplyFilterAndSort();
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

        private void UpdateBreadcrumbs(string path)
        {
            Breadcrumbs.Clear();
            if (string.IsNullOrWhiteSpace(path)) path = "/sdcard";

            string clean = path.Replace('\\', '/').Trim('/');
            string[] parts = clean.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            string accumulated = "";
            Breadcrumbs.Add(new BreadcrumbItem { Name = "Root", FullPath = "/", IsLast = parts.Length == 0 });

            for (int i = 0; i < parts.Length; i++)
            {
                accumulated += "/" + parts[i];
                Breadcrumbs.Add(new BreadcrumbItem
                {
                    Name = parts[i],
                    FullPath = accumulated,
                    IsLast = (i == parts.Length - 1)
                });
            }
        }

        private void ApplyFilterAndSort()
        {
            IEnumerable<FileItem> filtered = _rawItems;

            if (!ShowHiddenFiles)
            {
                filtered = filtered.Where(f => !f.IsHidden);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(f =>
                    f.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    f.Extension.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Apply Sorting
            filtered = _sortColumn switch
            {
                "Type" => _sortAscending
                    ? filtered.OrderBy(f => !f.IsDirectory).ThenBy(f => f.Type)
                    : filtered.OrderBy(f => !f.IsDirectory).ThenByDescending(f => f.Type),
                "Size" => _sortAscending
                    ? filtered.OrderBy(f => !f.IsDirectory).ThenBy(f => f.SizeBytes)
                    : filtered.OrderBy(f => !f.IsDirectory).ThenByDescending(f => f.SizeBytes),
                "Modified" or "FormattedDate" => _sortAscending
                    ? filtered.OrderBy(f => !f.IsDirectory).ThenBy(f => f.LastModified)
                    : filtered.OrderBy(f => !f.IsDirectory).ThenByDescending(f => f.LastModified),
                "Permissions" => _sortAscending
                    ? filtered.OrderBy(f => !f.IsDirectory).ThenBy(f => f.Permissions)
                    : filtered.OrderBy(f => !f.IsDirectory).ThenByDescending(f => f.Permissions),
                _ => _sortAscending
                    ? filtered.OrderBy(f => !f.IsDirectory).ThenBy(f => f.Name)
                    : filtered.OrderBy(f => !f.IsDirectory).ThenByDescending(f => f.Name)
            };

            DirectoryItems.Clear();
            foreach (var item in filtered)
            {
                DirectoryItems.Add(item);
            }
        }

        private void SortByColumn(string? columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return;

            if (_sortColumn.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = columnName;
                _sortAscending = true;
            }

            ApplyFilterAndSort();
        }

        private async Task OpenSelectedItemAsync()
        {
            if (SelectedItem == null) return;

            if (SelectedItem.IsDirectory)
            {
                await LoadDirectoryAsync(SelectedItem.FullPath, addToHistory: true);
            }
            else if (SelectedItem.Extension.Equals(".apk", StringComparison.OrdinalIgnoreCase))
            {
                await InstallSelectedApkAsync();
            }
            else
            {
                await DownloadSelectedItemsAsync();
            }
        }

        private async Task InstallSelectedApkAsync()
        {
            if (SelectedItem == null || !SelectedItem.Extension.Equals(".apk", StringComparison.OrdinalIgnoreCase)) return;

            // Temp download and install
            IsTransferring = true;
            TransferStatusText = $"Preparing APK installation: {SelectedItem.Name}...";
            string tempApk = Path.Combine(Path.GetTempPath(), SelectedItem.Name);

            var (pullOk, msg) = await _adbService.PullFileAsync(SelectedItem.FullPath, tempApk, TargetSerialNumber);
            if (pullOk && File.Exists(tempApk))
            {
                TransferStatusText = $"Installing {SelectedItem.Name} on device...";
                var (instSuccess, instMsg, _) = await _adbService.InstallApkAsync(tempApk, TargetSerialNumber);
                IsTransferring = false;

                if (instSuccess)
                    _notificationService.ShowSuccess("Installation Complete", $"{SelectedItem.Name} installed successfully!");
                else
                    _notificationService.ShowError("APK Installation Failed", instMsg);

                try { File.Delete(tempApk); } catch { }
            }
            else
            {
                IsTransferring = false;
                _notificationService.ShowError("Download Failed", msg);
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

        public void CopySelectedItems()
        {
            var targets = GetTargetItems();
            if (targets.Count == 0) return;

            _clipboardItems = new List<FileItem>(targets);
            _isCutOperation = false;
            _logger.LogInfo($"Copied {targets.Count} item(s) to internal remote clipboard.");
            _notificationService.ShowNotification("Clipboard", $"Copied {targets.Count} item(s) to clipboard.", NotificationType.Info);
        }

        public void CutSelectedItems()
        {
            var targets = GetTargetItems();
            if (targets.Count == 0) return;

            _clipboardItems = new List<FileItem>(targets);
            _isCutOperation = true;
            _logger.LogInfo($"Cut {targets.Count} item(s) to internal remote clipboard.");
            _notificationService.ShowNotification("Clipboard", $"Cut {targets.Count} item(s) to clipboard.", NotificationType.Info);
        }

        public async Task PasteClipboardItemsAsync()
        {
            _logger.LogInfo("[PasteClipboardItemsAsync] Paste operation initiated.");

            try
            {
                if (IsTransferring)
                {
                    _logger.LogWarning("[PasteClipboardItemsAsync] Operation ignored: A file transfer is already active.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(CurrentPath))
                {
                    CurrentPath = "/sdcard";
                }
                _logger.LogInfo($"[PasteClipboardItemsAsync] Destination directory: '{CurrentPath}'.");

                // 1. First check internal remote Android clipboard items
                if (_clipboardItems != null && _clipboardItems.Count > 0)
                {
                    _logger.LogInfo($"[PasteClipboardItemsAsync] Found {_clipboardItems.Count} item(s) in internal remote clipboard. IsCut = {_isCutOperation}.");
                }
                // 2. Otherwise check if PC Windows Clipboard contains local files copied from Windows Explorer
                else if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var dropList = System.Windows.Clipboard.GetFileDropList();
                    if (dropList != null && dropList.Count > 0)
                    {
                        string[] localFiles = dropList.Cast<string>().ToArray();
                        _logger.LogInfo($"[PasteClipboardItemsAsync] Found {localFiles.Length} local file(s) in PC Windows Clipboard. Initiating upload...");
                        await UploadFilesAsync(localFiles);
                        return;
                    }
                    else
                    {
                        _logger.LogWarning("[PasteClipboardItemsAsync] PC Windows Clipboard drop list was empty.");
                        _notificationService.ShowNotification("Clipboard Empty", "Nothing to paste.", NotificationType.Info);
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning("[PasteClipboardItemsAsync] Clipboard is empty (neither internal remote items nor PC file drop list present).");
                    _notificationService.ShowNotification("Clipboard Empty", "Nothing to paste.", NotificationType.Info);
                    return;
                }

                IsTransferring = true;
                _transferCts = new CancellationTokenSource();

                int totalItems = _clipboardItems.Count;
                int successfulCount = 0;
                bool overwriteAll = false;
                bool skipAll = false;

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                string baseDir = string.IsNullOrWhiteSpace(CurrentPath) ? "/sdcard" : CurrentPath.TrimEnd('/');

                for (int i = 0; i < totalItems; i++)
                {
                    if (_transferCts.Token.IsCancellationRequested) break;

                    var item = _clipboardItems[i];
                    string destPath = $"{baseDir}/{item.Name}";

                    // Check duplicate name on identical source and target directory
                    if (string.Equals(item.FullPath, destPath, StringComparison.Ordinal))
                    {
                        if (!_isCutOperation)
                        {
                            string ext = item.IsDirectory ? "" : System.IO.Path.GetExtension(item.Name);
                            string nameNoExt = item.IsDirectory ? item.Name : System.IO.Path.GetFileNameWithoutExtension(item.Name);
                            destPath = $"{baseDir}/{nameNoExt} - Copy{ext}";
                        }
                        else
                        {
                            _logger.LogInfo($"Skipping move of '{item.Name}' onto itself.");
                            continue;
                        }
                    }

                    // Check remote destination existence
                    bool destExists = await _adbService.CheckRemotePathExistsAsync(destPath, TargetSerialNumber);

                    if (destExists && !overwriteAll && !skipAll)
                    {
                        var promptResult = Microsoft.VisualBasic.Interaction.MsgBox(
                            $"'{item.Name}' already exists in destination folder.\n\nDo you want to overwrite it?",
                            Microsoft.VisualBasic.MsgBoxStyle.YesNoCancel | Microsoft.VisualBasic.MsgBoxStyle.Question,
                            "Confirm File Overwrite");

                        if (promptResult == Microsoft.VisualBasic.MsgBoxResult.Cancel)
                        {
                            _logger.LogInfo("User canceled paste operation.");
                            break;
                        }
                        else if (promptResult == Microsoft.VisualBasic.MsgBoxResult.No)
                        {
                            _logger.LogInfo($"User skipped overwriting '{item.Name}'.");
                            continue;
                        }
                    }

                    if (destExists && skipAll)
                    {
                        continue;
                    }

                    // Progress reporting
                    double percent = ((double)(i + 1) / totalItems) * 100.0;
                    TransferProgress = percent;
                    CurrentItemName = item.Name;
                    TransferStatusText = _isCutOperation
                        ? $"Moving [{i + 1}/{totalItems}]: {item.Name}"
                        : $"Copying [{i + 1}/{totalItems}]: {item.Name}";

                    double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                    if (elapsedSec > 0)
                    {
                        double rate = (i + 1) / elapsedSec;
                        double remainingSec = (totalItems - (i + 1)) / rate;
                        TransferSpeedText = $"{rate:0.0} items/sec";
                        RemainingTimeText = remainingSec > 0 ? $"ETA: {TimeSpan.FromSeconds(remainingSec):mm\\:ss}" : "Completing...";
                    }

                    var progress = new Progress<BackupProgressInfo>(p =>
                    {
                        if (!string.IsNullOrWhiteSpace(p.TransferSpeedText)) TransferSpeedText = p.TransferSpeedText;
                        if (!string.IsNullOrWhiteSpace(p.RemainingTimeText)) RemainingTimeText = p.RemainingTimeText;
                        if (p.OverallProgress > 0) TransferProgress = p.OverallProgress;
                    });

                    (bool success, string message) result;
                    if (_isCutOperation)
                    {
                        result = await _adbService.MoveRemoteItemAsync(item.FullPath, destPath, TargetSerialNumber, progress, _transferCts.Token);
                    }
                    else
                    {
                        result = await _adbService.CopyRemoteItemAsync(item.FullPath, destPath, TargetSerialNumber, progress, _transferCts.Token);
                    }

                    if (result.success)
                    {
                        successfulCount++;
                    }
                    else
                    {
                        _logger.LogError($"Paste error for '{item.Name}': {result.message}");
                        _notificationService.ShowError("Paste Failed", $"Error processing '{item.Name}': {result.message}");
                    }
                }

                if (_isCutOperation && successfulCount > 0)
                {
                    _clipboardItems.Clear();
                }

                IsTransferring = false;
                _logger.LogInfo($"Paste operation finished. Processed {successfulCount} of {totalItems} items.");
                _notificationService.ShowSuccess("Paste Complete", $"Pasted {successfulCount} item(s) successfully.");
                await RefreshStorageAndDirectoryAsync(addToHistory: false);
            }
            catch (Exception ex)
            {
                IsTransferring = false;
                _logger.LogError($"Unhandled exception in PasteClipboardItemsAsync: {ex.Message}");
                _notificationService.ShowError("Paste Error", $"An error occurred during paste: {ex.Message}");
            }
        }

        public async Task DownloadSelectedItemsAsync()
        {
            var targets = GetTargetItems();
            if (targets.Count == 0) return;

            string targetFolder = DefaultDownloadFolder;
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            // Check overwrite prompt if single file exists
            if (targets.Count == 1)
            {
                string checkPath = Path.Combine(targetFolder, targets[0].Name);
                if (File.Exists(checkPath) || Directory.Exists(checkPath))
                {
                    var dialogResult = Microsoft.VisualBasic.Interaction.MsgBox(
                        $"'{targets[0].Name}' already exists in download folder. Do you want to overwrite it?",
                        Microsoft.VisualBasic.MsgBoxStyle.YesNo | Microsoft.VisualBasic.MsgBoxStyle.Question,
                        "Confirm Overwrite");

                    if (dialogResult != Microsoft.VisualBasic.MsgBoxResult.Yes) return;
                }
            }

            IsTransferring = true;
            _transferCts = new CancellationTokenSource();

            var progress = new Progress<BackupProgressInfo>(p =>
            {
                TransferStatusText = p.StatusText;
                CurrentItemName = p.CurrentItemName;
                TransferSpeedText = p.TransferSpeedText;
                RemainingTimeText = p.RemainingTimeText;
                TransferProgress = p.OverallProgress;
            });

            var (success, message) = await _adbService.PullFilesAndFoldersAsync(targets, targetFolder, TargetSerialNumber, progress, _transferCts.Token);
            IsTransferring = false;

            if (success)
                _notificationService.ShowSuccess("Download Complete", $"Downloaded {targets.Count} item(s) to {targetFolder}");
            else
                _notificationService.ShowError("Download Failed", message);
        }

        public async Task DownloadSelectedAsZipAsync()
        {
            var targets = GetTargetItems();
            if (targets.Count == 0) return;

            var saveDialog = new SaveFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip",
                FileName = $"DeviceFiles_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                Title = "Save Download Archive as ZIP"
            };

            if (saveDialog.ShowDialog() != true) return;

            IsTransferring = true;
            _transferCts = new CancellationTokenSource();

            var progress = new Progress<BackupProgressInfo>(p =>
            {
                TransferStatusText = p.StatusText;
                CurrentItemName = p.CurrentItemName;
                TransferSpeedText = p.TransferSpeedText;
                RemainingTimeText = p.RemainingTimeText;
                TransferProgress = p.OverallProgress;
            });

            var (success, message) = await _adbService.DownloadAsZipAsync(targets, saveDialog.FileName, TargetSerialNumber, progress, _transferCts.Token);
            IsTransferring = false;

            if (success)
                _notificationService.ShowSuccess("ZIP Archive Created", $"Saved to {saveDialog.FileName}");
            else
                _notificationService.ShowError("ZIP Creation Failed", message);
        }

        public async Task UploadFilesAsync(string[] localPaths)
        {
            if (localPaths == null || localPaths.Length == 0) return;

            IsTransferring = true;
            _transferCts = new CancellationTokenSource();

            var progress = new Progress<BackupProgressInfo>(p =>
            {
                TransferStatusText = p.StatusText;
                CurrentItemName = p.CurrentItemName;
                TransferSpeedText = p.TransferSpeedText;
                RemainingTimeText = p.RemainingTimeText;
                TransferProgress = p.OverallProgress;
            });

            var (success, message) = await _adbService.PushFilesAndFoldersAsync(localPaths, CurrentPath, TargetSerialNumber, progress, _transferCts.Token);
            IsTransferring = false;

            if (success)
                _notificationService.ShowSuccess("Upload Complete", $"Uploaded files to {CurrentPath}");
            else
                _notificationService.ShowError("Upload Error", message);

            await RefreshStorageAndDirectoryAsync(addToHistory: false);
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

        private async Task RenameSingleItemAsync()
        {
            try
            {
                var targetItem = SelectedItem ?? _selectedItems.FirstOrDefault();
                if (targetItem == null || string.IsNullOrWhiteSpace(targetItem.Name) || string.IsNullOrWhiteSpace(targetItem.FullPath))
                {
                    _logger.LogWarning("Rename requested but no valid item is selected.");
                    _notificationService.ShowNotification("Rename Selection", "Please select a file or folder to rename.", NotificationType.Info);
                    return;
                }

                string oldName = targetItem.Name;
                string promptTitle = targetItem.IsDirectory ? "Rename Folder" : "Rename File";
                string promptMsg = $"Enter new name for '{oldName}':";

                string userInput = Microsoft.VisualBasic.Interaction.InputBox(promptMsg, promptTitle, oldName);

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    return; // User canceled or entered blank name
                }

                userInput = userInput.Trim();
                if (string.Equals(userInput, oldName, StringComparison.Ordinal))
                {
                    return; // Unchanged name
                }

                string finalNewName = userInput;

                // For files, preserve original extension if user did not specify one in the input
                if (!targetItem.IsDirectory)
                {
                    string origExt = System.IO.Path.GetExtension(oldName);
                    string userExt = System.IO.Path.GetExtension(userInput);

                    if (!string.IsNullOrEmpty(origExt) && string.IsNullOrEmpty(userExt))
                    {
                        finalNewName = userInput + origExt;
                    }
                }

                string baseDir = string.IsNullOrWhiteSpace(CurrentPath) ? "/sdcard" : CurrentPath.TrimEnd('/');
                string targetFullPath = $"{baseDir}/{finalNewName}";

                _logger.LogInfo($"Initiating rename: '{targetItem.FullPath}' -> '{targetFullPath}'");

                var (success, message) = await _adbService.RenameFileAsync(targetItem.FullPath, targetFullPath, TargetSerialNumber);

                if (success)
                {
                    _logger.LogInfo($"Rename succeeded: '{oldName}' -> '{finalNewName}'");
                    _notificationService.ShowSuccess("Renamed Successfully", $"'{oldName}' renamed to '{finalNewName}'");
                    await LoadDirectoryAsync(CurrentPath, addToHistory: false);
                }
                else
                {
                    _logger.LogError($"Rename failed for '{oldName}': {message}");
                    _notificationService.ShowError("Rename Failed", message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in RenameSingleItemAsync: {ex.Message}");
                _notificationService.ShowError("Rename Error", $"An error occurred during rename: {ex.Message}");
            }
        }

        public async Task DeleteSelectedItemsAsync()
        {
            var targets = GetTargetItems();
            if (targets.Count == 0) return;

            var dialogResult = Microsoft.VisualBasic.Interaction.MsgBox(
                $"Are you sure you want to permanently delete {targets.Count} selected item(s) from the device?",
                Microsoft.VisualBasic.MsgBoxStyle.YesNo | Microsoft.VisualBasic.MsgBoxStyle.Exclamation,
                "Confirm Delete");

            if (dialogResult != Microsoft.VisualBasic.MsgBoxResult.Yes) return;

            int deleted = 0;
            foreach (var item in targets)
            {
                var (success, _) = await _adbService.DeleteFileAsync(item.FullPath, TargetSerialNumber);
                if (success) deleted++;
            }

            _notificationService.ShowSuccess("Delete Complete", $"Deleted {deleted} item(s).");
            await RefreshStorageAndDirectoryAsync(addToHistory: false);
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

        private List<FileItem> GetTargetItems()
        {
            if (_selectedItems != null && _selectedItems.Count > 0) return _selectedItems;
            if (SelectedItem != null) return new List<FileItem> { SelectedItem };
            return new List<FileItem>();
        }
    }
}

