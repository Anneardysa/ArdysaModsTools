# AMT 2.0 Documentation

**ArdysaModsTools** — The Ultimate Dota 2 Mod Manager

[![Version](https://img.shields.io/badge/version-2.0-blue.svg)]()
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

---

## 🚀 Quick Start

### For Users

1. Download and install `ArdysaModsTools_Setup_x64.exe`
2. Launch the application (close Dota 2 first!)
3. Click **Auto Detect** to find Dota 2
4. Use **Skin Selector** or **Miscellaneous** to choose mods
5. Click **Patch Update** to apply
6. Launch Dota 2 and enjoy!

### For Developers

```bash
# Clone and build
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools

# Configure environment
cp .env.example .env
# Edit .env with your configuration

# Build and run
dotnet build -c Release
dotnet run
```

---

## 📁 Project Structure

```
ArdysaModsTools/
├── Core/                    # Business logic layer
│   ├── Controllers/         # MVC-style controllers
│   ├── Interfaces/          # Service contracts
│   ├── Models/              # Domain models & DTOs
│   └── Services/            # Service implementations
│       ├── Config/          # Environment configuration
│       ├── Hero/            # Hero set generation
│       ├── Misc/            # Miscellaneous mods
│       ├── Mods/            # Mod installation
│       ├── Security/        # Security utilities
│       ├── Update/          # Auto-updater
│       └── Vpk/             # VPK file handling
├── Helpers/                 # Global utility classes
├── UI/                      # Presentation layer
│   ├── Controls/            # Custom WinForms controls
│   ├── Forms/               # Application forms
│   └── Presenters/          # MVP presenters
├── Assets/                  # Static resources
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
| **Compression**  | SharpCompress              |

---

## ⚙️ Configuration

The application uses environment variables for sensitive configuration:

```env
# GitHub Configuration
GITHUB_OWNER=YourUsername
GITHUB_MODS_REPO=ModsPack
GITHUB_TOOLS_REPO=ArdysaModsTools
GITHUB_BRANCH=main
```

See [.env.example](../.env.example) for the full template.

---

## 🔗 Links

-  📦 [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
-  🐛 [Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
-  🔒 [Security Policy](../SECURITY.md)
-  🤝 [Contributing](../CONTRIBUTING.md)

---

## 📜 License

This project is open source under the MIT License.
