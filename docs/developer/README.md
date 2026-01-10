# Developer Documentation

Technical documentation for ArdysaModsTools contributors and developers.

---

## 📚 Contents

### Core Documentation

| Document                            | Description                                |
| ----------------------------------- | ------------------------------------------ |
| [Architecture](architecture.md)     | System design, components, and data flow   |
| [Development Guide](development.md) | Setup, building, and contributing          |
| [Tools & Scripts](tools.md)         | Build tools, VPK utilities, and automation |

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
git clone https://github.com/ardysa/AMT2.0.git
cd AMT2.0

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
└── docs/                    # Documentation
```

---

## 📖 Documentation Guide

1. **New to the project?** Start with [Architecture](architecture.md)
2. **Want to contribute?** Read [Development Guide](development.md)
3. **Need API details?** Check the [api/](api/) folder
4. **Build/deploy questions?** See [Tools & Scripts](tools.md)

---

## 🔧 Technology Stack

| Component    | Technology                 |
| ------------ | -------------------------- |
| Language     | C# 12 / .NET 8.0           |
| UI Framework | Windows Forms              |
| Architecture | MVP (Model-View-Presenter) |
| VPK Tools    | HLExtract.exe, vpk.exe     |
| Build        | MSBuild + ConfuserEx       |
| Installer    | Inno Setup                 |

---

## 🔗 Related Documentation

-  [User Documentation](../user/) - End-user guides
-  [Main README](../README.md) - Project overview
