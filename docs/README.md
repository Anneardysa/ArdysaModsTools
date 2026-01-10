# AMT 2.0 Documentation

**ArdysaModsTools** — The Ultimate Dota 2 Mod Manager

[![Version](https://img.shields.io/badge/version-2.0.10-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)]()
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

---

## What is AMT 2.0?

AMT 2.0 is a Windows desktop application for installing and managing custom cosmetic mods for Dota 2. It provides a sleek, modern interface for:

| Feature                    | Description                                             |
| -------------------------- | ------------------------------------------------------- |
| 🎮 **Mod Installation**    | One-click download and install of curated mod packs     |
| 🦸 **Hero Set Generation** | Create custom hero skins from community sets            |
| 🌦️ **Misc Mods**           | Weather, terrain, HUD, and other cosmetic modifications |
| 🔧 **Auto-Detection**      | Automatically finds your Dota 2 installation            |
| 🔄 **Patch Management**    | Keeps mods working after game updates                   |

---

## 📚 Documentation

### 👥 For Users

Complete guides for installing and using ArdysaModsTools.

| Document                                  | Description                 |
| ----------------------------------------- | --------------------------- |
| [Quick Start](user/QUICK_START.md)        | Get started in 5 minutes    |
| [User Guide](user/USER_GUIDE.md)          | Comprehensive documentation |
| [Overview](user/README.md)                | Features and FAQ            |
| [Offline Guide](user/GETTING_STARTED.txt) | Plain text reference        |

**New users**: Start with [Quick Start](user/QUICK_START.md)

---

### 🔧 For Developers

Technical documentation for contributors and developers.

| Document                                  | Description                 |
| ----------------------------------------- | --------------------------- |
| [Developer Guide](developer/README.md)    | Development overview        |
| [Architecture](developer/architecture.md) | System design and data flow |
| [Development](developer/development.md)   | Setup and contributing      |
| [Tools & Scripts](developer/tools.md)     | Build automation            |

#### API Reference

| Document                                        | Description          |
| ----------------------------------------------- | -------------------- |
| [Services](developer/api/services.md)           | Core service layer   |
| [Models](developer/api/models.md)               | Data models and DTOs |
| [UI Components](developer/api/ui-components.md) | Forms and controls   |
| [Helpers](developer/api/helpers.md)             | Utility classes      |
| [Exceptions](developer/api/exceptions.md)       | Error handling       |

---

## 🚀 Quick Start

### For Users

1. Download and install `ArdysaModsTools_Setup_x64.exe`
2. Launch the application (close Dota 2 first!)
3. Click **Auto Detect** to find Dota 2
4. Click **Install** to download and install mods
5. Launch Dota 2 and enjoy!

### For Developers

```bash
# Clone and build
git clone https://github.com/ardysa/AMT2.0.git
cd AMT2.0
dotnet build -c Release

# Run
./bin/Release/net8.0-windows/win-x64/ArdysaModsTools.exe
```

---

## 📁 Project Structure

```
AMT2.0/
├── Core/                    # Business logic layer
│   ├── Controllers/         # MVC-style controllers
│   ├── Interfaces/          # Service contracts
│   ├── Models/              # Domain models & DTOs
│   └── Services/            # Service implementations
├── Helpers/                 # Global utility classes
├── UI/                      # Presentation layer
│   ├── Controls/            # Custom WinForms controls
│   ├── Forms/               # Application forms
│   └── Presenters/          # MVP presenters
├── Assets/                  # Static resources
├── scripts/                 # Build & automation
├── tools/                   # External binaries
├── Tests/                   # Unit tests
└── docs/                    # Documentation (you are here)
    ├── user/                # End-user guides
    └── developer/           # Technical docs
```

---

## 🔧 Technology Stack

| Component        | Technology                 |
| ---------------- | -------------------------- |
| **Language**     | C# 12 / .NET 8.0           |
| **UI Framework** | Windows Forms              |
| **Architecture** | MVP (Model-View-Presenter) |
| **VPK Tools**    | HLExtract.exe, vpk.exe     |
| **Build**        | MSBuild + ConfuserEx       |
| **Installer**    | Inno Setup                 |

---

## 🔗 Links

-  📺 [YouTube Channel](https://youtube.com/@ardysa)
-  💬 [Discord Server](https://discord.gg/ardysa)
-  ☕ [Support on Ko-fi](https://ko-fi.com/ardysa)

---

## 📜 License

See [LICENSE.txt](../LICENSE.txt) for licensing information.
