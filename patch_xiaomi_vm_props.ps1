
$path = "Modules\Xiaomi\ViewModels\XiaomiViewModel.cs"
$content = Get-Content $path -Raw

$content = $content -replace "private string _romPath = `"`";", "private string _romPath = `"`";`n        private string _persistPath = `"`";"

$content = $content -replace "public string RomPath \{ get => _romPath; set => SetProperty\(ref _romPath, value\); \}", "public string RomPath { get => _romPath; set => SetProperty(ref _romPath, value); }`n        public string PersistPath { get => _persistPath; set => SetProperty(ref _persistPath, value); }"

$content = $content -replace "public ICommand RebootFastbootCommand \{ get; \}", "public ICommand RebootFastbootCommand { get; }`n        public ICommand RebootEdlCommand { get; }"

$content = $content -replace "public ICommand WipeDataCommand \{ get; \}", "public ICommand WipeDataCommand { get; }`n        public ICommand UnlockBootloaderCommand { get; }`n        public ICommand RelockBootloaderCommand { get; }"

$content = $content -replace "public ICommand FlashRomCommand \{ get; \}", "public ICommand FlashRomCommand { get; }`n        public ICommand FlashPersistCommand { get; }"

$content = $content -replace "public ICommand BrowseRomCommand \{ get; \}", "public ICommand BrowseRomCommand { get; }`n        public ICommand BrowsePersistCommand { get; }"

$content = $content -replace "RebootFastbootCommand = new AsyncRelayCommand\(RebootFastbootAsync, \(\) => !IsBusy\);", "RebootFastbootCommand = new AsyncRelayCommand(RebootFastbootAsync, () => !IsBusy);`n            RebootEdlCommand = new AsyncRelayCommand(RebootEdlAsync, () => !IsBusy);"

$content = $content -replace "WipeDataCommand = new AsyncRelayCommand\(WipeDataAsync, \(\) => !IsBusy\);", "WipeDataCommand = new AsyncRelayCommand(WipeDataAsync, () => !IsBusy);`n            UnlockBootloaderCommand = new AsyncRelayCommand(UnlockBootloaderAsync, () => !IsBusy);`n            RelockBootloaderCommand = new AsyncRelayCommand(RelockBootloaderAsync, () => !IsBusy);"

$content = $content -replace "FlashRomCommand = new AsyncRelayCommand\(FlashRomAsync, \(\) => !IsBusy\);", "FlashRomCommand = new AsyncRelayCommand(FlashRomAsync, () => !IsBusy);`n            FlashPersistCommand = new AsyncRelayCommand(FlashPersistAsync, () => !IsBusy);"

$content = $content -replace "BrowseRomCommand = new RelayCommand\(\(\) => RomPath = OpenFileDialog\(`"Fastboot ROM \(\*\.zip;\*\.tgz;\*\.sh;\*\.bat\)\|\*\.zip;\*\.tgz;\*\.sh;\*\.bat\|All Files \(\*\.\*\)\|\*\.\*`"\)\);", "BrowseRomCommand = new RelayCommand(() => RomPath = OpenFileDialog(`"Fastboot ROM (*.zip;*.tgz;*.sh;*.bat)|*.zip;*.tgz;*.sh;*.bat|All Files (*.*)|*.*`"));`n            BrowsePersistCommand = new RelayCommand(() => PersistPath = OpenFileDialog(`"Persist Image (*.img)|*.img`"));"

Set-Content $path -Value $content

