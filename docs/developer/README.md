# Developer Documentation

Technical documentation for AMT 2.0 contributors and developers.

---

## 📚 Contents

### Getting Started

- **[Development Setup](development.md)** - Environment setup, building, running
- **[Architecture Overview](architecture.md)** - System design and patterns

### API Reference

Detailed technical documentation:

- **[Services API](api/services.md)** - Core service implementations
- **[Data Models](api/models.md)** - Domain models and DTOs
- **[Active Mods](api/active-mods.md)** - Query installed/active mods
- **[Misc Mods](api/misc-mods.md)** - HUD, weather, terrain control
- **[Auto-Patching](api/auto-patching.md)** - Automatic re-patching after game updates
- **[Mod File Structure](api/mod-file-structure.md)** - File/folder specs and JSON schemas
- **[UI Components](api/ui-components.md)** - Forms and presenters
- **[Utilities](api/helpers.md)** - Helper classes and extensions

---

## 🚀 Quick Start

```bash
# Clone and setup
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools

# Build and run
dotnet restore
dotnet build -c Debug
dotnet run

# Run tests
dotnet test Tests/ArdysaModsTools.Tests.csproj
```

See [development.md](development.md) for detailed instructions.

---

## 🏗️ Project Structure

```
AMT2.0/
├── Core/              # Business logic layer
│   ├── Constants/     # CdnConfig, AppConstants
│   ├── DependencyInjection/  # DI setup
│   ├── Interfaces/    # Service contracts
│   ├── Models/        # Domain models
│   └── Services/      # Service implementations
├── UI/                # Presentation layer
│   ├── Factories/     # IMainFormFactory
│   ├── Forms/         # Windows Forms
│   ├── Interfaces/    # View contracts
│   └── Presenters/    # MVP presenters
├── Helpers/           # Utility classes
├── Assets/            # Static resources (HTML, fonts)
├── Tests/             # Unit tests (NUnit + Moq)
└── tools/             # External binaries (HLExtract, vpk.exe)
```

---

## 🔧 Technology Stack

| Component    | Technology                               |
| ------------ | ---------------------------------------- |
| Language     | C# 12                                    |
| Framework    | .NET 8.0 (Windows Forms)                 |
| UI Pattern   | MVP (Model-View-Presenter)               |
| DI Container | Microsoft.Extensions.DependencyInjection |
| Testing      | NUnit + Moq                              |
| CDN          | Cloudflare R2 + jsDelivr                 |
| VPK Tools    | HLExtract.exe, vpk.exe                   |

---

## 🔑 Key Patterns

### Dependency Injection

Services are registered in `ServiceCollectionExtensions.cs` and injected via constructors:

```csharp
// Program.cs uses IMainFormFactory for proper DI
var factory = serviceProvider.GetRequiredService<IMainFormFactory>();
Application.Run(factory.Create());
```

### Multi-CDN Fallback

Assets use R2 → jsDelivr → GitHub Raw fallback (see `CdnConfig.cs`).

---

## 🔗 Related Docs

- [Main README](../../README.md) - Project overview
- [Contributing Guidelines](../dev/CONTRIBUTING.md) - How to contribute
- [User Documentation](../user/) - End-user guides

---

<div align="center">

**[⬅ Back to Docs](../README.md)**

</div>
