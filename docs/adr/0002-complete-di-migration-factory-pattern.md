# ADR-0002: Complete DI Migration with Factory Pattern

**Date:** 2026-02-04
**Status:** Accepted
**Deciders:** @Anneardysa

## Problem Statement

The application relied on `ServiceLocator` — a well-known anti-pattern that made dependencies implicit, complicated unit testing (every test needed `ServiceLocator.Initialize()`), and generated `CS0618` obsolete warnings. The core challenge was that WinForms' `Application.Run()` doesn't support constructor injection, so simply removing ServiceLocator required a bridging strategy.

## Decision Drivers

- **Explicit dependencies** — Every class should declare what it needs through its constructor, not resolve internally
- **Testability** — Unit tests must be able to supply mock dependencies without global state setup
- **Zero build warnings** — Eliminate all `CS0618` obsolete warnings from production code
- **WinForms constraint** — `Application.Run(new MainForm())` doesn't accept constructor parameters natively
- **Incremental adoption** — Legacy test code must keep working during the transition

## Considered Alternatives

### Alternative 1: Factory Pattern with MS DI Container — Chosen

Create `IMainFormFactory` that resolves all dependencies from the DI container and constructs `MainForm` with full constructor injection.

- ✅ Good, because `MainForm` receives all dependencies explicitly via constructor
- ✅ Good, because the factory is itself registered in the container (fully composable)
- ✅ Good, because all existing DI registrations continue to work unchanged
- ✅ Good, because child forms can receive `IConfigService` from their parent naturally
- ❌ Bad, because the factory must be updated when `MainForm` constructor changes

### Alternative 2: Keep ServiceLocator (Suppress Warnings)

Continue using ServiceLocator but suppress `CS0618` with `#pragma warning disable`.

- ✅ Good, because zero refactoring effort required
- ❌ Bad, because dependencies remain hidden — no way to tell what a class needs without reading its implementation
- ❌ Bad, because test isolation is impossible (global shared state)
- ❌ Bad, because suppressing warnings masks a genuine design problem

### Alternative 3: Pure Manual DI (No Container)

Wire all dependencies manually in `Program.cs` without any DI container.

- ✅ Good, because no third-party dependency needed
- ✅ Good, because the full dependency graph is visible in one place
- ❌ Bad, because `Program.cs` would become a massive composition root (30+ services to wire)
- ❌ Bad, because adding a new dependency requires touching `Program.cs` every time
- ❌ Bad, because lifetime management (singleton vs transient) must be handled manually

### Alternative 4: Third-Party DI with Auto-Resolution (Autofac, etc.)

Use a container like Autofac that can auto-resolve WinForms forms via assembly scanning.

- ✅ Good, because it auto-discovers and registers services
- ❌ Bad, because it adds a heavy third-party dependency for a relatively simple application
- ❌ Bad, because auto-registration can mask missing registrations until runtime
- ❌ Bad, because the team is already using `Microsoft.Extensions.DependencyInjection`

## Decision

We will complete the DI migration using the **Factory Pattern** with `Microsoft.Extensions.DependencyInjection`:

1. Keep the existing DI container (`Microsoft.Extensions.DependencyInjection`)
2. Create `IMainFormFactory` / `MainFormFactory` to bridge DI → WinForms
3. Use constructor injection everywhere in production code
4. Remove all `ServiceLocator` calls from production code
5. Keep `ServiceLocator.Initialize()` only for test compatibility with `#pragma` suppression

### Factory Implementation

```csharp
// IMainFormFactory.cs — the bridge between DI and WinForms
public interface IMainFormFactory
{
    MainForm Create(bool startMinimized = false);
}

// MainFormFactory.cs
public class MainFormFactory : IMainFormFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MainFormFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public MainForm Create(bool startMinimized = false)
    {
        var configService = _serviceProvider.GetRequiredService<IConfigService>();
        var detectionService = _serviceProvider.GetRequiredService<IDetectionService>();
        var modInstallerService = _serviceProvider.GetRequiredService<IModInstallerService>();
        var statusService = _serviceProvider.GetRequiredService<IStatusService>();
        
        return new MainForm(
            configService,
            detectionService,
            modInstallerService,
            statusService,
            _serviceProvider,
            startMinimized);
    }
}

// Program.cs — entry point resolving MainForm from factory
var factory = serviceProvider.GetRequiredService<IMainFormFactory>();
bool startMinimized = Environment.GetCommandLineArgs().Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
Application.Run(factory.Create(startMinimized));
```

### DI Registration

```csharp
public static IServiceCollection AddArdysaServices(this IServiceCollection services)
{
    // Infrastructure (singletons — shared state)
    services.AddSingleton<ILogger, ProxyLogger>();
    services.AddSingleton<IConfigService, ConfigService>();

    // Core Services (transient — no shared state)
    services.AddTransient<IModInstallerService, ModInstallerService>();
    services.AddTransient<IDetectionService, DetectionService>();

    // Factory (singleton — one factory, many forms)
    services.AddSingleton<IMainFormFactory, MainFormFactory>();

    return services;
}
```

## Consequences

### Positive

- ✅ All dependencies are explicit in constructors — you can read a class's needs from its signature
- ✅ Unit tests create mocks directly without global state setup
- ✅ 0 build warnings (all `CS0618` eliminated from production code)
- ✅ All 243 tests pass without ServiceLocator in the test path
- ✅ Loose coupling enables swapping implementations freely

### Negative

- ❌ Factory must be updated when `MainForm` constructor signature changes
- ❌ `ServiceLocator` class file still exists for legacy test compatibility (with `#pragma` suppress)

### Neutral

- Forms receive dependencies via constructor instead of resolving internally
- Child forms (`SelectHero`, `HeroGalleryForm`) receive `IConfigService` from their parent form
- **Trade-off:** While sub-presenters (e.g. `ModOperationsPresenter`) are registered in DI to enable constructor testability, they are manually instantiated in the coordinator constructor (`MainFormPresenter`) to maintain tight UI event-wiring and reference bounds.

### Metrics

| Metric                             | Before                | After              |
| ---------------------------------- | --------------------- | ------------------ |
| ServiceLocator calls in production | 12+                   | 0                  |
| Build warnings (CS0618)            | 6                     | 0                  |
| Test setup complexity              | Global `Initialize()` | Direct constructor |

## Related

- [ADR-0001: Refactor MainForm to MVP](./0001-refactor-mainform-mvp.md) — MVP drives the need for injectable Presenters
- [ADR-0004: Presenter Decomposition for SRP](./0004-presenter-decomposition-srp.md) — adds more services to DI
- `Core/DependencyInjection/ServiceCollectionExtensions.cs`
- `UI/Factories/MainFormFactory.cs`
