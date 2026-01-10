# Architecture Overview

AMT 2.0 follows a **layered architecture** with the **MVP (Model-View-Presenter)** pattern for UI components.

---

## System Architecture

```mermaid
graph TB
    subgraph UI["UI Layer (WinForms)"]
        MF[MainForm]
        SH[SelectHero]
        MiscF[MiscForm]
        PO[ProgressOverlay]
    end

    subgraph Presenters["Presenter Layer"]
        MFP[MainFormPresenter]
        SHP[SelectHeroPresenter]
    end

    subgraph Core["Core Layer"]
        subgraph Controllers
            MC[MiscController]
        end
        subgraph Services
            MIS[ModInstallerService]
            HGS[HeroGenerationService]
            MGS[MiscGenerationService]
            SS[StatusService]
        end
        subgraph VPK["VPK Services"]
            VE[VpkExtractor]
            VR[VpkRecompiler]
            VRep[VpkReplacer]
        end
    end

    subgraph External["External Tools"]
        HLE[HLExtract.exe]
        VPKT[vpk.exe]
    end

    MF --> MFP
    SH --> SHP
    MFP --> MIS
    MFP --> SS
    SHP --> HGS
    MC --> MGS
    MIS --> VE
    HGS --> VE
    HGS --> VR
    MGS --> VE
    MGS --> VR
    VE --> HLE
    VR --> VPKT
```

---

## Layer Responsibilities

| Layer         | Purpose                      | Key Components                              |
| ------------- | ---------------------------- | ------------------------------------------- |
| **UI**        | User interaction, display    | Forms, Controls, Dialogs                    |
| **Presenter** | Business logic, coordination | MainFormPresenter, SelectHeroPresenter      |
| **Service**   | Domain operations            | ModInstaller, HeroGeneration, StatusService |
| **VPK**       | File operations              | Extractor, Recompiler, Replacer             |
| **External**  | Native tools                 | HLExtract.exe, vpk.exe                      |

---

## Component Relationships

### Entry Point Flow

```mermaid
sequenceDiagram
    participant User
    participant Program.cs
    participant SecurityManager
    participant MainForm

    User->>Program.cs: Launch App
    Program.cs->>SecurityManager: Initialize()
    alt Security OK
        SecurityManager-->>Program.cs: true
        Program.cs->>Program.cs: Check Dota2 not running
        Program.cs->>MainForm: new MainForm()
        MainForm-->>User: Display UI
    else Security Failed
        SecurityManager-->>Program.cs: false
        Program.cs->>User: Exit
    end
```

### MVP Pattern

```mermaid
classDiagram
    class IMainFormView {
        <<interface>>
        +TargetPath: string
        +SetStatus(text)
        +AppendLog(message)
        +UpdateModStatus(status)
    }

    class MainForm {
        -presenter: MainFormPresenter
        +TargetPath: string
        +SetStatus(text)
        +AppendLog(message)
    }

    class MainFormPresenter {
        -view: IMainFormView
        -modInstaller: ModInstallerService
        -statusService: StatusService
        +DetectDota2Async()
        +InstallModsAsync()
    }

    MainForm ..|> IMainFormView
    MainFormPresenter --> IMainFormView
    MainFormPresenter --> ModInstallerService
    MainFormPresenter --> StatusService
```

---

## Data Flow Diagrams

### Mod Installation Flow

```mermaid
flowchart LR
    A[User clicks Install] --> B{Dota 2 detected?}
    B -->|No| C[Show Error]
    B -->|Yes| D[Check for updates]
    D --> E{Update needed?}
    E -->|Yes| F[Download ModsPack]
    E -->|No| G[Use cached]
    F --> H[Validate VPK]
    G --> H
    H --> I[Copy to _ArdysaMods]
    I --> J[Patch game config]
    J --> K[Success]
```

### Hero Generation Flow

```mermaid
flowchart TB
    A[User selects heroes & sets] --> B[Click Generate]
    B --> C[Download Original.zip]
    C --> D[Extract pak01_dir.vpk]

    subgraph "For Each Hero"
        E[Download set ZIP]
        F[Merge assets into extract]
        G[Patch items_game.txt]
    end

    D --> E
    E --> F
    F --> G
    G --> H[Recompile VPK]
    H --> I[Replace original]
    I --> J[Patch signatures]
    J --> K[Done]
```

### Misc Generation Flow

```mermaid
flowchart LR
    A[User selects options] --> B[Choose mode]
    B --> C{Mode?}
    C -->|Clean| D[Extract fresh VPK]
    C -->|Add| E[Use existing extract]
    D --> F[Apply asset modifications]
    E --> F
    F --> G[Recompile VPK]
    G --> H[Replace in game]
    H --> I[Save extraction log]
```

---

## Service Layer Architecture

```mermaid
graph TB
    subgraph "Mod Services"
        MIS[ModInstallerService]
        SS[StatusService]
        DVS[DotaVersionService]
    end

    subgraph "Generation Services"
        HGS[HeroGenerationService]
        HSDS[HeroSetDownloaderService]
        HSPS[HeroSetPatcherService]
        MGS[MiscGenerationService]
        AMS[AssetModifierService]
    end

    subgraph "VPK Services"
        VES[VpkExtractorService]
        VRS[VpkRecompilerService]
        VRepS[VpkReplacerService]
        OVS[OriginalVpkService]
    end

    subgraph "Infrastructure"
        DS[DetectionService]
        US[UpdaterService]
        CS[ConfigService]
        LS[Logger]
    end

    MIS --> VES
    MIS --> SS
    HGS --> HSDS
    HGS --> HSPS
    HGS --> VRS
    HGS --> OVS
    MGS --> AMS
    MGS --> VES
    MGS --> VRS
```

---

## Dependency Injection

Services are wired via `ServiceCollectionExtensions.cs`:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArdysaServices(this IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<ILogger, Logger>();
        services.AddSingleton<IStatusService, StatusService>();
        services.AddSingleton<ConfigService>();

        // Core Services
        services.AddTransient<ModInstallerService>();
        services.AddTransient<HeroGenerationService>();
        services.AddTransient<MiscGenerationService>();

        // VPK Services
        services.AddTransient<IVpkExtractor, VpkExtractorService>();
        services.AddTransient<IVpkRecompiler, VpkRecompilerService>();
        services.AddTransient<IVpkReplacer, VpkReplacerService>();

        return services;
    }
}
```

---

## Security Architecture

```mermaid
graph TB
    subgraph "Security Layer"
        SM[SecurityManager]
        AD[AntiDebug]
        IC[IntegrityCheck]
        SC[SecureConfig]
        SP[StringProtection]
    end

    SM --> AD
    SM --> IC
    SM --> SC
    AD -->|Detects| DBG[Debuggers]
    AD -->|Detects| RE[RE Tools]
    IC -->|Validates| ASM[Assembly Hash]
```

| Component            | Purpose                                       |
| -------------------- | --------------------------------------------- |
| **SecurityManager**  | Orchestrates all security checks at startup   |
| **AntiDebug**        | Detects debuggers, timing anomalies, RE tools |
| **IntegrityCheck**   | Validates assembly checksums                  |
| **SecureConfig**     | Encrypted configuration storage               |
| **StringProtection** | String obfuscation helpers                    |

---

## Configuration Management

| Service               | Storage                                    | Purpose            |
| --------------------- | ------------------------------------------ | ------------------ |
| `ConfigService`       | `%APPDATA%/ArdysaModsTools/config.json`    | General app config |
| `UserSettingsService` | `%APPDATA%/ArdysaModsTools/settings.json`  | User preferences   |
| `FavoritesStore`      | `%APPDATA%/ArdysaModsTools/favorites.json` | Favorite heroes    |
| `MainConfigService`   | `%APPDATA%/ArdysaModsTools/main.json`      | Window state       |

---

## Error Handling Strategy

All service operations return `OperationResult` or `OperationResult<T>`:

```csharp
public class OperationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }
    public List<(string name, string reason)>? FailedItems { get; init; }
}
```

**Exception Hierarchy:**

```
Exception
└── ArdysaException (base)
    ├── VpkException
    ├── DownloadException
    ├── PatchException
    ├── ConfigurationException
    └── GenerationException
```

See [Exceptions Reference](./exceptions.md) for error codes and handling.

---

## Logging System

**Dual-logger approach:**

| Logger           | Purpose                                 | Usage                     |
| ---------------- | --------------------------------------- | ------------------------- |
| `Logger`         | Full logging with rotation, async write | DI-injected services      |
| `FallbackLogger` | Static fallback for critical paths      | Global exception handlers |

```csharp
// Normal logging
_logger.Log("Starting installation...");
_logger.Log($"[VPK_001] Extraction failed: {ex.Message}");

// Fallback for critical paths
FallbackLogger.Log($"UnhandledException: {ex}");
```

---

## Design Decisions

| Decision               | Rationale                                                     |
| ---------------------- | ------------------------------------------------------------- |
| **MVP over MVVM**      | WinForms lacks proper data binding; MVP is more natural       |
| **Service layer**      | Separates business logic from UI for testability              |
| **OperationResult**    | Avoids exceptions for expected failures (network, user input) |
| **External VPK tools** | Native Valve tools are battle-tested; no need to reimplement  |
| **ConfuserEx**         | Mature obfuscator with anti-tamper for release builds         |
