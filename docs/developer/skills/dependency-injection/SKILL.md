---
name: Dependency Injection Setup
description: Configure and register all ArdysaModsTools services using Microsoft.Extensions.DependencyInjection
---

# Dependency Injection Setup

ArdysaModsTools uses `Microsoft.Extensions.DependencyInjection` for all service resolution.

## Register All Services

```csharp
using ArdysaModsTools.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddArdysaServices();

var provider = services.BuildServiceProvider();
```

## Service Categories

`AddArdysaServices()` registers everything. Individual categories can also be registered separately:

```csharp
services
    .AddCoreServices()       // ModInstaller, Status, ActiveMods, Detection, Config, FileTransaction
    .AddConflictServices()   // ConflictDetector, ConflictResolver, ModPriority
    .AddHeroServices()       // HeroGeneration
    .AddLoggingServices()    // Logger (NullLogger default)
    .AddPresenters()         // ModOperations, Patch, Navigation presenters
    .AddUIFactories();       // MainForm factory
```

## Resolve Services

```csharp
// Core services
var installer = provider.GetRequiredService<IModInstallerService>();
var status = provider.GetRequiredService<IStatusService>();
var activeMods = provider.GetRequiredService<IActiveModsService>();
var detection = provider.GetRequiredService<IDetectionService>();
var config = provider.GetRequiredService<IConfigService>();

// Hero generation
var heroGen = provider.GetRequiredService<IHeroGenerationService>();

// Conflict handling
var detector = provider.GetRequiredService<IConflictDetector>();
var resolver = provider.GetRequiredService<IConflictResolver>();
```

## Service Lifetimes

| Service                | Lifetime  | Reason                            |
| :--------------------- | :-------- | :-------------------------------- |
| `IModInstallerService` | Transient | Each operation gets fresh state   |
| `IStatusService`       | Transient | Disposable (timer + file watcher) |
| `IActiveModsService`   | Transient | Stateless query service           |
| `IDetectionService`    | Transient | Fresh detection each call         |
| `IConfigService`       | Singleton | Shared config state               |
| `IConflictDetector`    | Singleton | Stateless, reusable               |
| `IAppLogger`           | Singleton | Single logger instance            |

## Replace Logger

The default is `NullLogger`. Replace with a real logger:

```csharp
var services = new ServiceCollection();
services.AddArdysaServices();

// Override with real logger
services.AddSingleton<IAppLogger>(new Logger(richTextBoxConsole));

var provider = services.BuildServiceProvider();
```

## Key File

- **ServiceCollectionExtensions:** `Core/DependencyInjection/ServiceCollectionExtensions.cs`
