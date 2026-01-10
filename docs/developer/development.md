# Development Guide

Guide for setting up, building, and contributing to AMT 2.0.

---

## Prerequisites

| Requirement       | Version | Notes                                             |
| ----------------- | ------- | ------------------------------------------------- |
| **.NET SDK**      | 8.0+    | [Download](https://dotnet.microsoft.com/download) |
| **Visual Studio** | 2022+   | With .NET desktop development workload            |
| **Python**        | 3.8+    | For build scripts                                 |
| **Inno Setup**    | 6.x     | Optional, for installer creation                  |

---

## Quick Setup

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

## Project Structure

```
AMT2.0/
├── Core/           # Business logic (services, models)
├── UI/             # WinForms (forms, controls, presenters)
├── Helpers/        # Global utilities
├── Assets/         # Fonts, icons, HTML templates
├── scripts/        # Build automation
├── tools/          # External binaries
├── Tests/          # Unit tests
└── docs/           # Documentation
```

---

## IDE Setup

### Visual Studio 2022

1. Open `AMT 2.0.sln`
2. Set `ArdysaModsTools` as startup project
3. Build solution (Ctrl+Shift+B)
4. Run (F5)

### JetBrains Rider

1. Open solution file
2. Wait for indexing
3. Select `ArdysaModsTools` configuration
4. Run/Debug

### VS Code

1. Open folder
2. Install C# extension
3. Run `dotnet build` in terminal
4. Use launch.json for debugging

---

## Build Configurations

### Debug Build

```bash
dotnet build -c Debug
```

Output: `bin/Debug/net8.0-windows/`

### Release Build (Unprotected)

```bash
dotnet publish -c Release -p:SkipInternalProtection=true
```

Output: `bin/Release/net8.0-windows/win-x64/publish/`

### Release Build (Protected)

```bash
dotnet publish -c Release
```

Output: `protected/` (after ConfuserEx)

### Full Distribution

```bash
python scripts/build_installer.py
```

Output: `installer_output/` (installer EXE)

---

## Running Tests

```bash
# Run all tests
cd Tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Common Tasks

### Adding a New Service

1. Create interface in `Core/Interfaces/`
2. Create implementation in `Core/Services/`
3. Register in `Core/DependencyInjection/ServiceCollectionExtensions.cs`
4. Inject via constructor

```csharp
// 1. Interface
public interface IMyService
{
    Task<OperationResult> DoWorkAsync(CancellationToken ct);
}

// 2. Implementation
public class MyService : IMyService
{
    private readonly ILogger _logger;

    public MyService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult> DoWorkAsync(CancellationToken ct)
    {
        // implementation
    }
}

// 3. Registration
services.AddTransient<IMyService, MyService>();
```

### Adding a New Form

1. Create form in `UI/Forms/`
2. Create presenter if needed in `UI/Presenters/`
3. Create view interface if needed in `UI/Interfaces/`
4. Follow MVP pattern

### Adding a Hero Set

1. Create set ZIP with assets
2. Create `index.txt` with item block overrides
3. Upload to CDN
4. Update `heroes.json` with set URL

---

## Code Style

### General

-  C# 12 features allowed
-  Nullable enabled
-  Async/await preferred
-  Use `OperationResult` for expected failures

### Naming

| Type            | Convention  | Example                |
| --------------- | ----------- | ---------------------- |
| Class           | PascalCase  | `ModInstallerService`  |
| Method          | PascalCase  | `InstallAsync`         |
| Variable        | camelCase   | `targetPath`           |
| Constant        | PascalCase  | `MaxRetries`           |
| Field (private) | \_camelCase | `_logger`              |
| Interface       | IPascalCase | `IModInstallerService` |

### File Organization

```csharp
// 1. Usings
using System;
using ArdysaModsTools.Core.Interfaces;

// 2. Namespace
namespace ArdysaModsTools.Core.Services;

// 3. Class
public class MyService
{
    // Fields
    private readonly ILogger _logger;

    // Constructor
    public MyService(ILogger logger) { }

    // Public methods
    public Task DoWorkAsync() { }

    // Private methods
    private void Helper() { }
}
```

---

## Debugging

### Common Breakpoints

| Location                   | Purpose                 |
| -------------------------- | ----------------------- |
| `Program.cs:17`            | Security initialization |
| `MainForm.cs:273`          | Form load               |
| `ModInstallerService.cs`   | Install operations      |
| `HeroGenerationService.cs` | Generation pipeline     |

### Debugging Tips

1. **Skip security checks** — Comment out `SecurityManager.Initialize()` in debug
2. **Use console** — Watch `mainConsoleBox` for logs
3. **Check logs** — `%APPDATA%/ArdysaModsTools/logs/`

---

## Troubleshooting Development

### Build Errors

| Error              | Solution                          |
| ------------------ | --------------------------------- |
| Missing .NET 8 SDK | Install from dotnet.microsoft.com |
| WebView2 not found | Install WebView2 runtime          |
| tools/ not copied  | Check .csproj Content items       |

### Runtime Errors

| Error                   | Solution                              |
| ----------------------- | ------------------------------------- |
| HLExtract.exe not found | Verify tools/hllib/ exists            |
| vpk.exe fails           | Check all DLLs present                |
| Security check fails    | Debugger detected (expected in debug) |

---

## Contributing

### Workflow

1. Fork repository
2. Create feature branch
3. Make changes
4. Run tests
5. Submit pull request

### Commit Messages

```
type(scope): description

feat(hero): add batch generation progress
fix(vpk): handle locked files with retry
docs(readme): update quick start
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`
