<div align="center">

# ⚡ Unlock Mate Pro

### Professional Universal Android Utility, Flashing & Management Suite

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![GitHub Stars](https://img.shields.io/github/stars/unlockmatepro/app?style=social)](https://github.com/unlockmatepro/app)

---

**Unlock Mate Pro** is a modern, production-ready Windows application built with **.NET 8 WPF** and supported by an **ASP.NET Core 8 Web API** backend platform. It provides a complete suite of Android management, universal package installation (`.apk`, `.apks`, `.xapk`, `.apkm`), real-time file exploration with symlink resolution, Smart Switch backup & restore, fastboot flashing, and interactive ADB console tools.

[Explore Features](#-key-features) • [Installation](#-installation) • [Build from Source](#-build-from-source) • [Author](#-author)

</div>

---

## 🚀 Key Features

### 📦 Universal APK Package Installer
- Supports **`.apk`**, **`.apks`**, **`.xapk`**, **`.apkm`**, and split APK package bundles.
- Automatic package type detection, manifest parsing, and split bundle extraction.
- Pre-flight diagnostics: device connection, ABI compatibility (`arm64-v8a`, `x86_64`), minimum SDK version, and storage space.
- Automatic error handling for `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, `INSTALL_FAILED_VERSION_DOWNGRADE`, `INSTALL_FAILED_ALREADY_EXISTS`, etc.

### 📂 Real-Time Android File Explorer
- Real-time navigation starting at `/sdcard/` with automatic symlink resolution (`/sdcard/` → `/storage/emulated/0/`).
- Double-click folder navigation & double-click file download prompt.
- Right-click context menu: Copy, Cut, Paste (`cp -r` / `mv`), Download to PC, Upload to Device, Rename, Delete, New Folder, and Refresh.
- Drag & Drop support (PC → Phone Upload & Phone → PC Download) with live transfer speed (MB/s) and remaining time indicators.

### 💾 Smart Switch Backup & Restore
- Creates structured backup bundles named `DeviceName_YYYY-MM-DD_HH-MM-SS`.
- Multi-format exports:
  - **Contacts**: `.vcf` (vCard 3.0), `.csv`, and `.json`.
  - **SMS Messages**: `.xml` and `.json`.
  - **Call Logs**: `.csv` and `.json`.
  - **Installed Apps**: `.apk` packages + `packages.json` manifest.
  - **Files**: `/sdcard` internal storage items.
- One-Click Full Backup & Full Restore with optional **ZIP compression** and direct ZIP archive restoration.
- Fault-tolerant execution with per-module error isolation and automatic `BackupReport.txt` generation.

### ⚡ Fastboot & Bootloader Flashing Suite
- Fastboot device detection (`fastboot devices`) and `getvar all` inspection.
- Partition image flashing: `boot`, `recovery`, `vbmeta`, `vendor_boot`, `init_boot`, `super`, `system`, `vendor`, and `userdata`.
- Boot temporary image (`fastboot boot <image>`) without flashing.
- Erase partition support with safety confirmation dialogues.
- FRP status check (`getvar frp-state` / `getvar secure`) and OEM Lock / Unlock (`flashing unlock`).

### 💻 Interactive ADB Shell Terminal
- Interactive console with command execution history (`Up`/`Down` arrow navigation).
- Pre-populated auto-complete dictionary for common ADB shell commands.
- Clear console, copy output to clipboard, export log to `.txt`, and target device selection.

### 🛡️ Advanced Device & Root Diagnostics
- Hardware summary: Brand, Model, Android Version, SDK Level, Build Number, CPU ABI, Serial Number, Battery %, Storage, and RAM stats.
- Root & Security diagnostics: Root access check (`su`), Magisk detection (`com.topjohnwu.magisk`), SuperSU binary, BusyBox binary, SELinux enforcement state (`getenforce`), and dm-verity state.
- Advanced reboot modes: Reboot System, Reboot Recovery, Reboot Bootloader, Reboot FastbootD, Reboot Safe Mode, and Reboot Sideload.

### 🎨 Theme & UI Customization
- Global **Dark & Light** theme engine with instant live switching.
- High-contrast `{DynamicResource}` color palettes ensuring high readability in all display modes.
- Fully responsive WPF layout supporting 100%, 125%, 150%, and 200% Windows display scaling.

---

## 🖼️ Screenshots

<div align="center">

| Dashboard & Device Info | File Explorer & Storage |
|:---:|:---:|
| ![Dashboard Screenshot](https://via.placeholder.com/600x350/1e1e2e/ffffff?text=Unlock+Mate+Pro+-+Dashboard) | ![File Explorer Screenshot](https://via.placeholder.com/600x350/1e1e2e/ffffff?text=Unlock+Mate+Pro+-+File+Explorer) |

| Backup & Restore Suite | Fastboot Flashing Suite |
|:---:|:---:|
| ![Backup & Restore Screenshot](https://via.placeholder.com/600x350/1e1e2e/ffffff?text=Unlock+Mate+Pro+-+Backup+%26+Restore) | ![Fastboot Suite Screenshot](https://via.placeholder.com/600x350/1e1e2e/ffffff?text=Unlock+Mate+Pro+-+Fastboot+Suite) |

</div>

---

## 📋 System Requirements

| Component | Minimum Requirement | Recommended |
|---|---|---|
| **OS** | Windows 10 (64-bit) | Windows 11 (64-bit) |
| **Runtime** | [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) | Included in Installer |
| **Processor** | Intel Core i3 / AMD Ryzen 3 | Intel Core i5 / AMD Ryzen 5 or higher |
| **Memory** | 4 GB RAM | 8 GB RAM |
| **Storage** | 200 MB free space | 1 GB free space |
| **Android Device** | Android 5.0 (Lollipop) | Android 10 - 15 with USB Debugging enabled |

---

## 📥 Installation

### Option 1: Setup Installer (Recommended)
1. Download `UnlockMatePro-Setup.exe` from the latest [GitHub Release](https://github.com/unlockmatepro/app/releases).
2. Run the installer and follow the setup wizard.
3. Launch **Unlock Mate Pro** from your Desktop or Start Menu.

### Option 2: Portable Package
1. Download `UnlockMatePro-v1.0.0-Portable.zip`.
2. Extract the archive to any directory on your system.
3. Launch `AdbEasyInstaller.exe`.

---

## 🛠️ Build from Source

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (with .NET Desktop Development workload) or [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
- Git.

### Build Steps

```bash
# 1. Clone the repository
git clone https://github.com/unlockmatepro/app.git
cd app

# 2. Restore dependencies
dotnet restore

# 3. Build Desktop Client in Release mode
dotnet build AdbEasyInstaller.csproj -c Release

# 4. Build Backend API in Release mode
dotnet build UnlockMatePro.Server.csproj -c Release

# 5. Publish Release Artifacts
dotnet publish AdbEasyInstaller.csproj -c Release -o ./publish
```

---

## 📂 Project Structure

```text
UnlockMatePro/
├── AdbEasyInstaller.csproj          # Main WPF Desktop Client Project
├── App.xaml / App.xaml.cs           # Global Application Entry & Crash Handler
├── MainWindow.xaml                  # Fluent Shell Window Layout & Navigation
├── Models/                          # Data Models (Device, FileItem, BackupManifest, User)
├── Services/                        # Core Logic (AdbService, FastbootService, ApiService)
├── ViewModels/                      # MVVM ViewModels (FileExplorer, BackupRestore, Terminal)
├── Views/                           # Modern WPF Views & Controls
├── Styles/                          # Design System Tokens & Dynamic Theme Resources
├── installer.iss                    # Inno Setup Script for Building Setup.exe
└── UnlockMatePro.Server/            # ASP.NET Core 8 Web API Backend Platform
```

---

## 💻 Technologies Used

- **Desktop Frontend**: C#, .NET 8.0, WPF, XAML, MVVM.
- **Backend API**: ASP.NET Core 8 Web API, Entity Framework Core, MySQL, JWT Authentication.
- **Android Platform Tools**: ADB (Android Debug Bridge), Fastboot, Scrcpy.
- **Packaging & Deployment**: Inno Setup, PowerShell Automation.

---

## 👤 Author

**Abdul Kader**

- **Website**: [https://abdulkader.online](https://abdulkader.online)
- **GitHub**: [@abdulkaderprl-ctrl](https://github.com/abdulkaderprl-ctrl)

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
