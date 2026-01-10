# ArdysaModsTools (AMT 2.0) 🎮

<div align="center">

![AMT 2.0 Banner](https://img.shields.io/badge/AMT-2.0.15--beta-00d4ff?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjQiIGhlaWdodD0iMjQiIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cGF0aCBkPSJNMTIgMkw0IDdWMTJDNCAxNi40MTggNy41ODIgMjAgMTIgMjBDMTYuNDE4IDIwIDIwIDE2LjQxOCAyMCAxMlY3TDEyIDJaIiBmaWxsPSJ3aGl0ZSIvPjwvc3ZnPg==)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?style=for-the-badge&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-00C853?style=for-the-badge&logo=github)
![Build](https://img.shields.io/badge/Build-Passing-success?style=for-the-badge)

### **The Ultimate Dota 2 Mod Manager**

_Easily install, manage, and organize your Dota 2 cosmetic mods with a sleek, modern interface_

[📥 Download](#-installation) • [📖 Documentation](#-usage) • [🚀 Features](#-features) • [🤝 Contributing](#-contributing) • [💬 Support](#-support)

---

</div>

## 📋 Table of Contents

-  [Overview](#-overview)
-  [Features](#-features)
-  [Screenshots](#-screenshots)
-  [Installation](#-installation)
-  [Quick Start](#-quick-start)
-  [Usage Guide](#-usage-guide)
-  [Configuration](#%EF%B8%8F-configuration)
-  [Project Structure](#%EF%B8%8F-project-structure)
-  [Technology Stack](#%EF%B8%8F-technology-stack)
-  [Architecture](#-architecture)
-  [Building from Source](#-building-from-source)
-  [Contributing](#-contributing)
-  [Troubleshooting](#-troubleshooting)
-  [FAQ](#-faq)
-  [Security](#-security)
-  [Roadmap](#-roadmap)
-  [License](#-license)
-  [Acknowledgments](#-acknowledgments)
-  [Support](#-support)

---

## 🌟 Overview

**ArdysaModsTools (AMT 2.0)** is a powerful, user-friendly Windows application designed to simplify the process of installing and managing cosmetic modifications for Dota 2. Built with modern .NET technology and featuring an intuitive interface, AMT 2.0 makes customizing your Dota 2 experience effortless.

### Why AMT 2.0?

-  ✅ **User-Friendly**: Clean, modern UI that anyone can navigate
-  ✅ **Automated**: Auto-detection, auto-patching, and one-click installation
-  ✅ **Safe**: Non-intrusive modifications that don't affect gameplay
-  ✅ **Comprehensive**: Supports hero skins, HUD themes, terrains, weather effects, and more
-  ✅ **Maintained**: Regular updates to ensure compatibility with Dota 2 patches
-  ✅ **Open Source**: MIT licensed and community-driven

### ⚠️ Important Disclaimer

This software is a third-party tool and is **NOT** affiliated with, endorsed by, or sponsored by Valve Corporation. Use at your own risk. Modifying game files may violate Valve's Terms of Service. The developers are not responsible for any consequences arising from the use of this tool.

---

## 🎯 Features

### Core Functionality

<table>
<tr>
<td width="50%">

#### 🎨 **Skin Selector**

-  Browse curated library of hero cosmetic sets
-  Preview before installation
-  Batch installation support
-  Favorites system
-  Search and filter capabilities

</td>
<td width="50%">

#### 🌦️ **Miscellaneous Mods**

-  Custom weather effects
-  Terrain modifications
-  HUD themes
-  Music packs
-  Cursor modifications
-  UI enhancements

</td>
</tr>
<tr>
<td width="50%">

#### 🔄 **Auto-Patching System**

-  Automatically detects Dota 2 updates
-  Preserves mods after game patches
-  One-click re-patching
-  Patch history tracking
-  Rollback support

</td>
<td width="50%">

#### 🔍 **Smart Auto-Detection**

-  Automatically locates Dota 2 installation
-  Supports multiple Steam libraries
-  Validates game file integrity
-  Detects custom installation paths

</td>
</tr>
<tr>
<td width="50%">

#### ⚡ **One-Click Installation**

-  Simple, fast mod deployment
-  Progress tracking
-  Error handling and recovery
-  Backup creation
-  Uninstall support

</td>
<td width="50%">

#### 🛠️ **Advanced Features**

-  VPK file extraction and recompilation
-  Custom mod pack creation
-  Mod conflict detection
-  Performance optimization
-  Detailed logging

</td>
</tr>
</table>

### Additional Features

-  🔔 **Update Notifications** - Stay informed about new mods and app updates
-  📊 **Mod Statistics** - Track your installed mods and usage
-  🌐 **Multi-language Support** - (Coming soon)
-  🎨 **Customizable Interface** - Dark/Light themes
-  📁 **Mod Management** - Enable/disable mods without uninstalling
-  🔐 **Secure** - Code obfuscation and security measures

---

## 📸 Screenshots

<div align="center">

### Main Interface

_Clean, intuitive dashboard with all features at your fingertips_

### Skin Selector

_Browse and preview hero cosmetic sets before installing_

### Mod Manager

_Manage all your installed mods in one place_

### Settings Panel

_Comprehensive configuration options_

> 📷 **Note**: Screenshots will be added in the next release. See [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases) for the latest updates.

</div>

---

## 📥 Installation

### Method 1: Installer (Recommended)

1. **Download** the latest installer from [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
2. **Run** `ArdysaModsTools-Setup-v2.0.15.exe`
3. **Follow** the installation wizard
4. **Launch** AMT 2.0 from your Start Menu or Desktop

The installer includes:

-  ✅ .NET 8.0 Runtime (if not already installed)
-  ✅ All required dependencies
-  ✅ VPK tools (HLExtract, vpk.exe)
-  ✅ Automatic updates support

### Method 2: Portable Version

1. **Download** `ArdysaModsTools-Portable-v2.0.15.zip` from [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
2. **Extract** to a folder of your choice
3. **Ensure** you have [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed
4. **Run** `ArdysaModsTools.exe`

### System Requirements

| Component      | Minimum                    | Recommended         |
| -------------- | -------------------------- | ------------------- |
| **OS**         | Windows 10 (64-bit)        | Windows 11 (64-bit) |
| **RAM**        | 2 GB                       | 4 GB                |
| **Disk Space** | 500 MB                     | 1 GB                |
| **Runtime**    | .NET 8.0                   | .NET 8.0            |
| **Other**      | Dota 2 installed via Steam | -                   |

### First-Time Setup

On first launch, AMT will:

1. Request administrator privileges (for file system access)
2. Automatically detect your Dota 2 installation
3. Create necessary configuration files
4. Check for updates

---

## 🚀 Quick Start

### 5-Minute Setup Guide

1. **Close Dota 2** if it's running
2. **Launch AMT 2.0**
3. **Click "Auto Detect"** to locate your Dota 2 installation
4. **Select a mod** from the Skin Selector or Miscellaneous tabs
5. **Click "Install"** and wait for completion
6. **Click "Patch Update"** to apply changes
7. **Launch Dota 2** and enjoy your mods!

### Video Tutorial

> 📺 **Coming Soon**: A comprehensive video tutorial will be available on our YouTube channel.

---

## 📖 Usage Guide

### Detailed Workflow

#### 1. Detecting Dota 2 Installation

```text
Main Window → Auto Detect Button → Confirm Path
```

-  **Automatic**: AMT scans common Steam library locations
-  **Manual**: Use "Browse" if auto-detection fails
-  **Validation**: AMT verifies the path contains valid Dota 2 files

#### 2. Installing Hero Skins

```text
Skin Selector Tab → Select Hero → Choose Set → Install → Apply Patch
```

**Options:**

-  **Single Install**: Install one set at a time
-  **Batch Install**: Queue multiple sets
-  **Favorites**: Save frequently used sets
-  **Preview**: View before installing (if available)

#### 3. Applying Miscellaneous Mods

```text
Miscellaneous Tab → Category → Select Mod → Install → Apply Patch
```

**Categories:**

-  🌤️ Weather Effects
-  🗺️ Terrains
-  🖼️ HUD Themes
-  🎵 Music Packs
-  🖱️ Cursors
-  🎨 UI Modifications

#### 4. Managing Installed Mods

```text
Installed Mods Tab → Select Mod → Enable/Disable/Uninstall
```

**Actions:**

-  **Enable/Disable**: Toggle mods without uninstalling
-  **Uninstall**: Completely remove mods
-  **Refresh**: Update mod status
-  **Details**: View mod information

#### 5. Patching System

```text
Patch Update Button → Wait for Completion → Confirm Success
```

**What Patching Does:**

-  Modifies `gameinfo.gi` to load custom VPK files
-  Updates `dota.signatures` for compatibility
-  Creates backup of original files
-  Validates patch integrity

**When to Patch:**

-  ✅ After installing new mods
-  ✅ After Dota 2 updates
-  ✅ When switching mod configurations
-  ✅ If mods stop working

#### 6. Disabling All Mods

```text
Disable Mods Button → Confirm → Restart Dota 2
```

Temporarily disables all mods without uninstalling. Perfect for:

-  Testing game performance
-  Troubleshooting issues
-  Playing online (if preferred)
-  Quick vanilla experience

---

## ⚙️ Configuration

### Environment Variables

AMT uses environment variables for configuration. Create a `.env` file in the application directory:

```env
# GitHub Configuration (for mod downloads)
GITHUB_OWNER=YourGitHubUsername
GITHUB_MODS_REPO=YourModsRepository
GITHUB_TOOLS_REPO=YourToolsRepository
GITHUB_BRANCH=main

# Optional: Custom Paths
DOTA_PATH=C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta
```

### Configuration Files

AMT stores settings in:

-  **Windows**: `%AppData%\ArdysaModsTools\`
-  **Files**:
   -  `settings.json` - User preferences
   -  `installed_mods.json` - Installed mod tracking
   -  `patch_history.json` - Patch operation history

### Customization Options

Access via **Settings** menu:

| Setting                    | Description               | Default |
| -------------------------- | ------------------------- | ------- |
| **Theme**                  | Dark/Light mode           | Dark    |
| **Auto-detect on startup** | Scan for Dota 2 on launch | Enabled |
| **Check for updates**      | Automatic update checks   | Enabled |
| **Create backups**         | Backup before patching    | Enabled |
| **Enable logging**         | Detailed operation logs   | Enabled |
| **Mod download source**    | GitHub repository         | Default |

---

## 🏗️ Project Structure

```
ArdysaModsTools/
├── 📁 Core/                          # Business Logic Layer
│   ├── Controllers/                  # Application controllers
│   ├── Data/                         # Data access layer
│   ├── DependencyInjection/          # DI container configuration
│   ├── Exceptions/                   # Custom exception classes
│   ├── Helpers/                      # Utility helpers
│   ├── Interfaces/                   # Service contracts
│   │   ├── IConfigService.cs
│   │   ├── IModInstallerService.cs
│   │   └── IVpkRecompilerService.cs
│   ├── Models/                       # Data models
│   │   ├── HeroSetModel.cs
│   │   ├── MiscModModel.cs
│   │   └── PatchConfiguration.cs
│   └── Services/                     # Service implementations
│       ├── Config/                   # Configuration management
│       │   ├── EnvironmentConfig.cs
│       │   └── SecureConfig.cs
│       ├── Hero/                     # Hero set generation
│       │   └── HeroSetGeneratorService.cs
│       ├── Misc/                     # Miscellaneous mods
│       │   ├── MiscModService.cs
│       │   └── MiscUtilityService.cs
│       ├── Mods/                     # Mod installation
│       │   ├── ModInstallerService.cs
│       │   └── ModDownloaderService.cs
│       ├── Security/                 # Security utilities
│       │   └── SecureConfig.cs
│       ├── Update/                   # Auto-updater
│       │   └── UpdaterService.cs
│       └── Vpk/                      # VPK file handling
│           ├── VpkExtractorService.cs
│           └── VpkRecompilerService.cs
│
├── 📁 UI/                            # User Interface Layer
│   ├── Forms/                        # Windows Forms
│   │   ├── MainForm.cs               # Main application window
│   │   ├── SkinSelectorForm.cs       # Skin selector dialog
│   │   ├── SettingsForm.cs           # Settings dialog
│   │   └── AboutForm.cs              # About dialog
│   └── Presenters/                   # MVP Presenters
│       ├── MainFormPresenter.cs
│       └── SkinSelectorPresenter.cs
│
├── 📁 Helpers/                       # Cross-cutting Utilities
│   ├── FileHelper.cs                 # File operations
│   ├── PathHelper.cs                 # Path management
│   ├── LogHelper.cs                  # Logging utility
│   └── ValidationHelper.cs           # Input validation
│
├── 📁 Assets/                        # Embedded Resources
│   ├── Fonts/                        # Custom fonts
│   ├── Html/                         # HTML templates
│   ├── Icons/                        # Application icons
│   │   └── AppIcon.ico
│   └── Images/                       # UI images
│
├── 📁 tools/                         # External Tools
│   ├── hllib/                        # HLExtract (VPK extraction)
│   │   ├── HLExtract.exe
│   │   └── HLLib.dll
│   └── vpk/                          # Valve VPK tool
│       ├── vpk.exe
│       ├── tier0.dll
│       └── vstdlib.dll
│
├── 📁 docs/                          # Documentation
│   ├── dev/                          # Developer docs
│   │   ├── CONTRIBUTING.md
│   │   └── SECURITY.md
│   ├── user/                         # User docs
│   │   └── README.md
│   ├── GETTING_STARTED.txt
│   └── USER_GUIDE.md
│
├── 📁 scripts/                       # Build & Dev Scripts
│   ├── build/                        # Build scripts
│   │   ├── build_installer.py
│   │   └── installer.iss
│   ├── dev/                          # Development tools
│   │   ├── cleaning.py
│   │   └── run_tests.py
│   ├── tools/                        # Utility scripts
│   │   ├── localization.py
│   │   └── obfuscate_source.py
│   └── templates/                    # Dev tool templates
│       ├── commit-and-push.ps1.template
│       └── build.txt.template
│
├── 📁 tests/                         # Unit Tests
│   └── ArdysaModsTools.Tests/
│
├── 📄 Program.cs                     # Application entry point
├── 📄 ArdysaModsTools.csproj         # Project file
├── 📄 AMT 2.0.sln                    # Solution file
├── 📄 .env.example                   # Environment template
├── 📄 .gitignore                     # Git ignore rules
├── 📄 LICENSE                        # MIT License
└── 📄 README.md                      # This file
```

---

## 🛠️ Technology Stack

### Core Technologies

<table>
<tr>
<td width="33%">

#### Language

-  **C# 12**
-  Latest language features
-  Nullable reference types
-  Pattern matching

</td>
<td width="33%">

#### Framework

-  **.NET 8.0**
-  Cross-platform runtime
-  Modern APIs
-  High performance

</td>
<td width="33%">

#### UI Framework

-  **Windows Forms**
-  Native Windows controls
-  Fast rendering
-  Low resource usage

</td>
</tr>
</table>

### Dependencies & Libraries

| Package                                      | Version     | Purpose               |
| -------------------------------------------- | ----------- | --------------------- |
| **Microsoft.Web.WebView2**                   | 1.0.2903.40 | Web content rendering |
| **Microsoft.Toolkit.Uwp.Notifications**      | 7.1.3       | Toast notifications   |
| **SharpCompress**                            | 0.40.0      | Archive extraction    |
| **ValveKeyValue**                            | 0.13.1      | VDF file parsing      |
| **SixLabors.ImageSharp**                     | 3.1.12      | Image processing      |
| **QRCoder**                                  | 1.7.0       | QR code generation    |
| **Google.Apis.YouTube.v3**                   | 1.69.0      | YouTube integration   |
| **Microsoft.Extensions.DependencyInjection** | 9.0.9       | Dependency injection  |

### External Tools

-  **HLExtract** (HLLib) - VPK extraction (LGPL v2.1)
-  **vpk.exe** (Valve) - VPK creation (Source SDK License)
-  **ConfuserEx** - Code obfuscation (MIT License)

### Development Tools

-  **Visual Studio 2022** - Primary IDE
-  **Inno Setup 6** - Installer creation
-  **Python 3.11+** - Build automation
-  **Git** - Version control

---

## 🏛️ Architecture

### Design Patterns

#### Model-View-Presenter (MVP)

-  **Model**: Data models in `Core/Models/`
-  **View**: Windows Forms in `UI/Forms/`
-  **Presenter**: Business logic in `UI/Presenters/`

#### Dependency Injection

-  Services registered in `Core/DependencyInjection/`
-  Constructor injection throughout
-  Singleton and transient lifetimes

#### Service Layer

-  Clear separation of concerns
-  Interface-based design
-  Testable architecture

### Key Services

```csharp
IConfigService          → Configuration management
IModInstallerService    → Mod installation logic
IVpkRecompilerService   → VPK file operations
IUpdateService          → Auto-update functionality
ISecurityService        → Secure configuration handling
```

### Data Flow

```
User Input → UI Forms → Presenters → Services → External Tools → Game Files
                ↓                        ↓
            View Updates            Logging & Error Handling
```

---

## 🔨 Building from Source

### Prerequisites

-  [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-  [Git](https://git-scm.com/)
-  [Visual Studio 2022](https://visualstudio.microsoft.com/) (optional, recommended)

### Clone Repository

```bash
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools
```

### Configure Environment

```bash
# Copy environment template
cp .env.example .env

# Edit .env with your configuration
notepad .env
```

### Build

#### Using Command Line

```bash
# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build -c Release

# Run
dotnet run --project ArdysaModsTools.csproj
```

#### Using Visual Studio

1. Open `AMT 2.0.sln`
2. Select `Release` configuration
3. Build → Build Solution (`Ctrl+Shift+B`)
4. Debug → Start Without Debugging (`Ctrl+F5`)

### Create Installer

```bash
# Install Python dependencies
pip install -r requirements.txt

# Run build script
python scripts/build/build_installer.py
```

Installer will be created in `installer_output/`

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 🤝 Contributing

We welcome contributions from the community! Whether it's bug fixes, new features, documentation improvements, or translations, your help is appreciated.

### How to Contribute

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/AmazingFeature`)
3. **Commit** your changes (`git commit -m 'Add some AmazingFeature'`)
4. **Push** to the branch (`git push origin feature/AmazingFeature`)
5. **Open** a Pull Request

### Contribution Guidelines

Please read our [Contributing Guide](docs/dev/CONTRIBUTING.md) for detailed information on:

-  Code style guidelines
-  Commit message conventions
-  Pull request process
-  Development workflow
-  Testing requirements

### Areas for Contribution

-  🐛 **Bug Fixes** - Report and fix issues
-  ✨ **New Features** - Implement requested features
-  📖 **Documentation** - Improve guides and docs
-  🌍 **Translations** - Add language support
-  🎨 **UI/UX** - Enhance user interface
-  🧪 **Testing** - Increase test coverage
-  ⚡ **Performance** - Optimize code

### Development Setup

See [Building from Source](#-building-from-source) for detailed instructions.

---

## 🔧 Troubleshooting

### Common Issues

#### Auto-Detection Fails

**Problem**: AMT can't find Dota 2 installation

**Solutions**:

1. Ensure Dota 2 is installed via Steam
2. Use "Browse" to manually select:
   ```
   C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta
   ```
3. Check Steam library folders in Steam settings
4. Run AMT as Administrator

#### Mods Not Appearing In-Game

**Problem**: Installed mods don't show in Dota 2

**Solutions**:

1. Click "Patch Update" after installing mods
2. Restart Dota 2 completely
3. Verify game files in Steam
4. Check `gameinfo.gi` for correct modifications
5. Ensure Dota 2 is not running during patch

#### VPK Recompilation Failed

**Problem**: Error during mod installation

**Solutions**:

1. Ensure `vpk.exe` and DLLs are in `tools/vpk/`
2. Run AMT as Administrator
3. Check antivirus isn't blocking `vpk.exe`
4. Verify disk space (at least 500 MB free)
5. Check logs in `%AppData%\ArdysaModsTools\logs\`

#### WebView2 Runtime Error

**Problem**: "Couldn't find a compatible WebView2 Runtime"

**Solutions**:

1. Download [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
2. Install and restart AMT
3. Alternative: Update Microsoft Edge

#### Application Won't Start

**Problem**: AMT crashes on launch

**Solutions**:

1. Install [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Run as Administrator
3. Check Windows Event Viewer for errors
4. Reinstall AMT
5. Delete `%AppData%\ArdysaModsTools\` and restart

### Getting Help

If you encounter an issue not listed here:

1. **Check** the [Issues](https://github.com/Anneardysa/ArdysaModsTools/issues) page
2. **Search** for similar problems
3. **Create** a new issue with:
   -  Detailed description
   -  Steps to reproduce
   -  Error messages
   -  System information
   -  Log files (if applicable)

---

## ❓ FAQ

### General Questions

**Q: Is AMT 2.0 safe to use?**  
A: Yes, AMT only modifies cosmetic files locally and doesn't interact with Dota 2's online services. However, use at your own risk.

**Q: Will I get banned for using mods?**  
A: Cosmetic mods generally don't result in bans, but Valve's policy can change. The developers are not responsible for any consequences.

**Q: Does this give me an unfair advantage?**  
A: No, AMT only modifies cosmetic appearance. It doesn't change gameplay, hitboxes, or game mechanics.

**Q: Are the mods visible to other players?**  
A: No, mods are client-side only. Other players see the default game assets.

**Q: Is AMT compatible with Mac/Linux?**  
A: Currently, AMT is Windows-only. Cross-platform support may come in future versions.

### Technical Questions

**Q: What happens when Dota 2 updates?**  
A: Game updates may reset patched files. Simply click "Patch Update" in AMT to re-apply mods.

**Q: Can I use AMT with custom game modes?**  
A: AMT is designed for standard Dota 2. Compatibility with custom games is not guaranteed.

**Q: How do I uninstall all mods?**  
A: Use the "Disable Mods" button or verify game files through Steam.

**Q: Can I create my own mod packs?**  
A: Yes! Advanced users can create custom VPK files and use AMT to install them.

**Q: Where are mods downloaded from?**  
A: Mods are hosted on GitHub repositories. You can configure custom sources in `.env`.

---

## 🔒 Security

### Reporting Vulnerabilities

If you discover a security vulnerability, please:

1. **DO NOT** open a public issue
2. **Email** security details privately (see [SECURITY.md](docs/dev/SECURITY.md))
3. **Include**:
   -  Description of the vulnerability
   -  Steps to reproduce
   -  Potential impact
   -  Suggested fixes (if any)

We take security seriously and will respond promptly to valid reports.

### Security Measures

-  ✅ Code obfuscation (ConfuserEx)
-  ✅ Secure configuration storage
-  ✅ Environment variable protection
-  ✅ Input validation
-  ✅ Safe file operations
-  ✅ No credential storage
-  ✅ HTTPS for all downloads

### Best Practices for Users

-  🔒 Only download AMT from official sources
-  🔒 Keep Windows and .NET runtime updated
-  🔒 Use antivirus software
-  🔒 Don't share your `.env` file
-  🔒 Review permissions during installation

---

## 🗺️ Roadmap

### Version 2.1 (Q1 2026)

-  [ ] Multi-language support (Chinese, Russian, Spanish)
-  [ ] Cloud backup and sync
-  [ ] Mod categories and tagging
-  [ ] Advanced search and filtering
-  [ ] Mod rating and reviews
-  [ ] Automatic conflict resolution

### Version 2.2 (Q2 2026)

-  [ ] Custom mod creation wizard
-  [ ] Community mod sharing
-  [ ] Mod update notifications
-  [ ] Performance profiling
-  [ ] Plugin system
-  [ ] Themes and customization

### Version 3.0 (Q3 2026)

-  [ ] Cross-platform support (Mac, Linux)
-  [ ] Web-based management interface
-  [ ] Mobile companion app
-  [ ] AI-powered mod recommendations
-  [ ] Integration with Steam Workshop

See [GitHub Projects](https://github.com/Anneardysa/ArdysaModsTools/projects) for detailed progress tracking.

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

### Key Points

-  ✅ Free to use, modify, and distribute
-  ✅ Commercial use allowed
-  ✅ Must include copyright notice
-  ✅ No warranty provided
-  ✅ Authors not liable for damages

### Third-Party Licenses

This project uses several third-party components. See [LICENSE](LICENSE) for complete attribution and license information.

---

## 🙏 Acknowledgments

### Special Thanks

-  **Dota 2 Modding Community** - For inspiration and support
-  **Valve Corporation** - For creating Dota 2
-  **Open Source Contributors** - For excellent libraries and tools
-  **Beta Testers** - For feedback and bug reports
-  **You** - For using AMT and being part of the community!

### Built With

-  [.NET 8.0](https://dotnet.microsoft.com/) - Application framework
-  [HLLib](https://github.com/NeilJed/HLLib) - VPK extraction
-  [SharpCompress](https://github.com/adamhathcock/sharpcompress) - Archive handling
-  [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) - Web rendering
-  [Inno Setup](https://jrsoftware.org/isinfo.php) - Installer creation

### Inspired By

-  Dota 2 modding tools and utilities
-  Community mod managers
-  Modern desktop application design

---

## 💬 Support

### Get Help

-  📖 **Documentation**: [docs/](docs/)
-  🐛 **Issues**: [GitHub Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
-  💬 **Discussions**: [GitHub Discussions](https://github.com/Anneardysa/ArdysaModsTools/discussions)
-  📧 **Email**: Check [SECURITY.md](docs/dev/SECURITY.md) for contact

### Community

-  🌟 **Star** this repository if you find it useful
-  🍴 **Fork** and contribute
-  🐦 **Share** with other Dota 2 players
-  💡 **Suggest** features and improvements

### Stay Updated

-  Watch this repository for updates
-  Check [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases) for new versions
-  Follow development progress in [Projects](https://github.com/Anneardysa/ArdysaModsTools/projects)

---

<div align="center">

### Made with ❤️ for the Dota 2 Community

**ArdysaModsTools** © 2024-2026 Ardysa | Licensed under [MIT License](LICENSE)

[⬆ Back to Top](#ardysamodstools-amt-20-)

</div>
