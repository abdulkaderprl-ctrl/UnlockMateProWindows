[Setup]
AppId={{97081A2B-FCEB-4A69-83DB-4C72D60B972C}
AppName=Unlock Mate Pro
AppVersion=1.0.0
AppPublisher=Unlock Mate Pro Technologies
AppPublisherURL=https://unlockmatepro.com
AppSupportURL=https://unlockmatepro.com/support
AppUpdatesURL=https://unlockmatepro.com/updates
DefaultDirName={autopf}\UnlockMatePro
DefaultGroupName=Unlock Mate Pro
AllowNoIcons=yes
OutputDir=Release
OutputBaseFilename=UnlockMatePro-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Unlock Mate Pro"; Filename: "{app}\AdbEasyInstaller.exe"
Name: "{group}\{cm:UninstallProgram,Unlock Mate Pro}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Unlock Mate Pro"; Filename: "{app}\AdbEasyInstaller.exe"

[Run]
Filename: "{app}\AdbEasyInstaller.exe"; Description: "{cm:LaunchProgram,Unlock Mate Pro}"; Flags: nowait postinstall skipifsilent
