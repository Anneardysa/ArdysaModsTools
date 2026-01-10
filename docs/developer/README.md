# Developer Documentation

Technical documentation for ArdysaModsTools contributors and developers.

---

## 📚 Contents

### Core Documentation

| Document                            | Description                              |
| ----------------------------------- | ---------------------------------------- |
| [Architecture](architecture.md)     | System design, components, and data flow |
| [Development Guide](development.md) | Setup, building, and contributing        |

### API Reference

| Document                              | Description                         |
| ------------------------------------- | ----------------------------------- |
| [Services](api/services.md)           | Core service layer documentation    |
| [Models](api/models.md)               | Data models, DTOs, and enums        |
| [UI Components](api/ui-components.md) | Forms, controls, and presenters     |
| [Helpers](api/helpers.md)             | Utility classes and common patterns |
| [Exceptions](api/exceptions.md)       | Error codes and handling strategies |

---

## 🚀 Quick Start for Developers

```bash
# Clone repository
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools

# Configure environment
cp .env.example .env
# Edit .env with your GitHub details

# Restore dependencies
dotnet restore

# Build debug
dotnet build -c Debug

# Run
dotnet run
```

---

## 🏗️ Architecture Overview

```
ArdysaModsTools/
├── Core/                    # Business logic layer
│   ├── Controllers/         # MVC-style controllers
│   ├── Data/                # Configuration data
│   ├── Interfaces/          # Service contracts
│   ├── Models/              # Domain models & DTOs
│   └── Services/            # Service implementations
│       ├── Config/          # Environment & app config
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
├── installer/               # Inno Setup installer
├── tools/                   # External binaries (vpk.exe, HLExtract)
└── docs/                    # Documentation
```

---

## 🔧 Technology Stack

| Component    | Technology                 |
| ------------ | -------------------------- |
| Language     | C# 12 / .NET 8.0           |
| UI Framework | Windows Forms              |
| Architecture | MVP (Model-View-Presenter) |
| VPK Tools    | HLExtract.exe, vpk.exe     |
| Compression  | SharpCompress              |
| Installer    | Inno Setup                 |

---

## ⚙️ Configuration

The application uses environment variables for sensitive configuration:

```env
GITHUB_OWNER=YourUsername
GITHUB_MODS_REPO=ModsPack
GITHUB_TOOLS_REPO=ArdysaModsTools
GITHUB_BRANCH=main
```

See [.env.example](../../.env.example) for the full template.

---

## 🔗 Related Documentation

-  [User Documentation](../user/) - End-user guides
-  [Main README](../../README.md) - Project overview
-  [Contributing](../../CONTRIBUTING.md) - Contribution guidelines
