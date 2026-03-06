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
- **[Misc Mods](api/misc-mods.md)** - HUD, weather, terrain, courier, ward control
- **[Auto-Patching](api/auto-patching.md)** - Automatic re-patching after game updates
- **[Mod File Structure](api/mod-file-structure.md)** - File/folder specs and JSON schemas
- **[Exceptions](api/exceptions.md)** - Error codes and handling
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
ArdysaModsTools/
├── Core/              # Business logic layer
│   ├── Constants/     # CdnConfig, AppConstants
│   ├── DependencyInjection/  # DI setup
│   ├── Interfaces/    # Service contracts (16 interfaces)
│   ├── Models/        # Domain models & DTOs
│   └── Services/      # Service implementations
│       ├── App/       # App lifecycle
│       ├── Cache/     # Cache cleaning
│       ├── Cdn/       # SmartCdnSelector, CdnFallback
│       ├── Config/    # Settings, favorites, feature access
│       ├── Conflict/  # Conflict detection & resolution
│       ├── Detection/ # Dota 2 folder detection
│       ├── FileTransaction/ # Atomic file operations
│       ├── Hero/      # Hero set generation & patching
│       ├── Logging/   # App & fallback logging
│       ├── Meta/      # Support goals (Ko-fi, YouTube)
│       ├── Misc/      # Weather, HUD, courier, ward, etc.
│       ├── Mods/      # ModsPack install, disable, patch
│       ├── Security/  # Anti-tamper & integrity checks
│       ├── Update/    # Auto-update, PatchWatcher, resumable DL
│       └── Vpk/       # VPK extraction & recompilation
├── UI/                # Presentation layer
│   ├── Factories/     # IMainFormFactory
│   ├── Forms/         # WinForms + WebView2 hybrid forms
│   ├── Interfaces/    # View contracts
│   └── Presenters/    # MVP presenters (5 specialized)
├── Helpers/           # Utility classes
├── Assets/            # Static resources (HTML, fonts)
├── Tests/             # Unit tests (480+, NUnit + Moq)
└── tools/             # External binaries (HLExtract, vpk.exe)
```

---

## 🔧 Technology Stack

| Component    | Technology                                                |
| ------------ | --------------------------------------------------------- |
| Language     | C# 12                                                     |
| Framework    | .NET 8.0 (Windows Forms)                                  |
| UI Pattern   | MVP (Model-View-Presenter)                                |
| DI Container | Microsoft.Extensions.DependencyInjection                  |
| Testing      | NUnit + Moq                                               |
| CDN          | Cloudflare R2 + jsDelivr + GitHub Raw + GFW proxy mirrors |
| VPK Tools    | HLExtract.exe, vpk.exe                                    |

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

Assets use R2 → jsDelivr → GitHub Raw → GFW proxy mirrors fallback with `SmartCdnSelector` auto-selecting the fastest CDN per user (see `CdnConfig.cs`).

---

## 🔗 Related Docs

- [Main README](../../README.md) - Project overview
- [Contributing Guidelines](../dev/CONTRIBUTING.md) - How to contribute
- [User Documentation](../user/) - End-user guides

---

<div align="center">

**[⬅ Back to Docs](../README.md)**

</div>
