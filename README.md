# ArdysaModsTools (AMT 2.0)

<div align="center">

![AMT 2.0](https://img.shields.io/badge/AMT-2.0-cyan?style=for-the-badge)
![Platform](https://img.shields.io/badge/platform-Windows-blue?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge)
![License](https://img.shields.io/badge/license-MIT-green?style=for-the-badge)

**The Ultimate Dota 2 Mod Manager**

[Features](#features) • [Installation](#installation) • [Usage](#usage) • [Configuration](#configuration) • [Contributing](#contributing)

</div>

---

## 🎮 Features

| Feature                  | Description                                         |
| ------------------------ | --------------------------------------------------- |
| 🎨 **Skin Selector**     | Choose custom hero sets from a curated library      |
| 🌦️ **Misc Mods**         | Weather, terrain, HUD, music, and more              |
| 🔄 **Auto-Patching**     | Automatically keeps mods working after Dota updates |
| 🔍 **Auto-Detection**    | Finds your Dota 2 installation automatically        |
| ⚡ **One-Click Install** | Simple, fast mod installation                       |

## 📸 Screenshots

<div align="center">
<i>Screenshots coming soon</i>
</div>

---

## 📥 Installation

### Prerequisites

-  **Windows 10/11** (64-bit)
-  **Dota 2** installed via Steam
-  **.NET 8.0 Runtime** (included in installer)

### Download

Download the latest release from [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases).

### Build from Source

```bash
# Clone the repository
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools

# Copy environment template
cp .env.example .env
# Edit .env with your configuration

# Build
dotnet build -c Release

# Run
dotnet run
```

---

## ⚙️ Configuration

This application uses environment variables for configuration. For development:

1. Copy `.env.example` to `.env`
2. Fill in your values:

```env
# GitHub Configuration
GITHUB_OWNER=YourGitHubUsername
GITHUB_MODS_REPO=YourModsRepository
GITHUB_TOOLS_REPO=YourToolsRepository
GITHUB_BRANCH=main
```

---

## 🚀 Usage

1. **Close Dota 2** before running AMT
2. **Launch AMT** and click **Auto Detect**
3. **Install Mods** using Skin Selector or Miscellaneous
4. **Apply Patch** to enable mods
5. **Launch Dota 2** and enjoy!

### Quick Reference

| Button        | Action                           |
| ------------- | -------------------------------- |
| Auto Detect   | Find Dota 2 installation         |
| Skin Selector | Choose hero cosmetics            |
| Miscellaneous | Apply HUD, weather, terrain mods |
| Patch Update  | Apply/update game patches        |
| Disable       | Temporarily disable all mods     |

---

## 🏗️ Project Structure

```
ArdysaModsTools/
├── Core/                     # Business logic
│   ├── Services/             # Service implementations
│   │   ├── Config/           # Configuration management
│   │   ├── Hero/             # Hero set generation
│   │   ├── Misc/             # Miscellaneous mods
│   │   ├── Mods/             # Mod installation
│   │   ├── Security/         # Security utilities
│   │   ├── Update/           # Auto-updater
│   │   └── Vpk/              # VPK file handling
│   ├── Models/               # Data models
│   └── Interfaces/           # Service contracts
├── UI/                       # Windows Forms UI
│   ├── Forms/                # Application forms
│   └── Presenters/           # MVP presenters
├── Helpers/                  # Utility classes
├── Assets/                   # Embedded resources
├── tools/                    # External tools (vpk.exe, HLExtract)
├── docs/                     # Documentation
└── Tests/                    # Unit tests
```

---

## 🛠️ Technology Stack

| Component    | Technology                  |
| ------------ | --------------------------- |
| Language     | C# 12                       |
| Framework    | .NET 8.0                    |
| UI           | Windows Forms               |
| Architecture | MVP Pattern                 |
| VPK Tools    | HLExtract, Valve vpk.exe    |
| HTTP         | HttpClient with retry logic |
| Compression  | SharpCompress               |

---

## 🤝 Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Clone the repository
3. Configure `.env` file
4. Open `AMT 2.0.sln` in Visual Studio 2022 or VS Code

---

## 🔒 Security

-  Report vulnerabilities via [SECURITY.md](SECURITY.md)
-  Never commit secrets - use environment variables
-  See `.env.example` for configuration template

---

## 📄 License

This project is licensed under the MIT License - see [LICENSE](LICENSE) for details.

---

## 🔗 Links

-  📦 [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
-  🐛 [Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
-  📖 [Documentation](docs/README.md)

---

<div align="center">

**Made with ❤️ for the Dota 2 Community**

</div>
