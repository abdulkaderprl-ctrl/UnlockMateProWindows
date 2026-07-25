# Unlock Mate Pro 🔓⚡

A modern, production-ready Windows 11 desktop platform built with **C# (.NET 8)**, **WPF**, and **MVVM architecture** combining professional **Android Device Management**, **Authentication System**, **ADB/Fastboot Tools**, and **Scrcpy Screen Control**.

---

## ✨ Validated Feature Matrix

### 🔐 Authentication & User Account Architecture
- **Auth System**: `IAuthenticationService` and `IApiService` with full JWT Bearer Authorization header support.
- **Backend API Ready**: Configurable `ApiBaseUrl` (defaults to `https://api.unlockmatepro.com/api` or `http://localhost:5000/api`).
- **Features**:
  - **Login Page**: Email & Password login with JWT token persistence.
  - **Register Page**: User registration with instant account activation.
  - **Forgot Password**: Password reset request workflow.
  - **Email Verification**: Verification code validation step.
  - **Google Sign-In**: Prepared OAuth token handler.
  - **Session Persistence**: Session stored securely in `%APPDATA%\UnlockMatePro\session.json`.
  - **Auto Login**: Automatically validates and restores sessions on app startup.
  - **User Profile Page**: User Account details, License Plan badge (Pro License / Enterprise), and Secure Logout.

### 📁 Device Tools & File Explorer
- **PC ↔ Android File Browser**: Browse internal storage `/sdcard/`, upload (push), download (pull), delete files, and create remote directories.
- **App Management Controls**: Enable/Disable packages (`pm disable-user`), Force Stop (`am force-stop`), Clear Data (`pm clear`), and Manage Permissions (`pm grant`/`pm revoke`).
- **Smart Switch Data Backup**: One-click export of Contacts, SMS Messages, and Call Logs to JSON.

### ⚡ ADB & Fastboot Suite
- **Interactive ADB Terminal**: Embedded command prompt console with live stdout stream.
- **Fastboot Flashing & Partitioning**: Flash `.img` files into recovery, boot, vbmeta, or system partitions.
- **ZIP Sideload**: Execute `adb sideload` for OTA updates.
- **EDL Reboot**: One-click reboot to Qualcomm Emergency Download (EDL) mode.
- **Safe OEM Unlock Workflow**: Automated bootloader unlock commands (`flashing unlock`).

### 🖥️ Screen Mirroring & Remote Input Control (scrcpy)
- **Ultra Low-Latency Mirroring**: Full-screen (`--fullscreen`) and windowed screen mirroring via `scrcpy`.
- **Mouse & Keyboard Remote Control**: Full remote PC input control over Android UI.
- **Display Adjustments**: Stay awake (`--stay-awake`), Screen off (`--turn-screen-off`), Show touches (`--show-touches`), custom Resolution (720p/1080p), FPS (30/60/90/120), and Bitrate (4M to 32M).
- **Video Recording & Gallery**: Record screen to MP4 video and manage screenshots in a built-in gallery.

### 📦 APK Management & Inspection
- **Split APK Bundle Engine**: Automated ZIP extraction and `adb install-multiple` for `.apks`, `.xapk`, and `.apkm` packages.
- **APK Signature Inspector**: Inspect package name, certificates, permissions, and version codes.
- **Side-by-Side APK Compare**: Compare package metadata and file sizes between two APKs.
- **Auto Rename & Organize**: Auto-rename APK files to `<package_name>_v<version>.apk`.

### 📊 System Diagnostics & Advanced Tools
- **Hardware Metrics**: Real-time RAM & Storage usage progress gauges, battery health & temperature, network info, security patch level, and build fingerprint.
- **Root Status Checker**: Instant verification of `su` superuser binary access.
- **Bug Report & Crash Log Viewer**: Generate full zip bugreports and filter logcat for fatal exceptions.
- **Usability**: Global Keyboard Shortcuts (`Ctrl+R`, `Ctrl+I`, `Ctrl+M`, `Ctrl+F`, `Ctrl+T`, `Ctrl+L`) and Multi-Language Support (**English** & **Bangla - বাংলা**).

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + R` | Refresh Connected Devices |
| `Ctrl + I` | Open APK & Split Bundle Installer |
| `Ctrl + M` | Launch Screen Mirroring (Scrcpy) |
| `Ctrl + F` | Open Android File Explorer |
| `Ctrl + T` | Open ADB Shell Command Terminal |
| `Ctrl + L` | Open ADB Execution Logs |

---

## 🔨 Build Instructions

### Option 1: Double-click `build.bat`
```cmd
build.bat
```

### Option 2: Using the .NET CLI
```bash
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --no-build -o bin\Release\net8.0-windows\publish
```

Executable output location:
`bin\Release\net8.0-windows\publish\AdbEasyInstaller.exe`

---

## 📄 License

Licensed under the **MIT License**. Copyright © 2026 Unlock Mate Pro Team.
