# ADR-0004: Presenter Decomposition for Single Responsibility

**Date:** 2026-02-09
**Status:** Accepted — **partially superseded (build 2227): `PatchPresenter` removed**
**Deciders:** @Anneardysa

> **Amendment (build 2227).** The `PatchPresenter` half of this decomposition was never completed.
> `MainFormPresenter` constructed a `PatchPresenter` and subscribed to its events, but never routed any
> patch action to it and never started its watcher — so its events could not fire and every live patch
> action (`patchClick` / `patchApply` / `patchVerify` / `patchViewStatus`) ran through
> `MainFormPresenter`'s own duplicate methods. Two divergent implementations of the same feature had
> already drifted apart (verification logic existed only in the unreachable copy). The dead
> `PatchPresenter` / `IPatchPresenter` and their DI registration were deleted rather than wired up:
> **`MainFormPresenter` is the sole owner of the patch actions and the `DotaPatchWatcherService`
> lifecycle.** `NavigationPresenter` is unaffected and remains live. Sections below that describe
> `PatchPresenter` are historical.

## Problem Statement

After the MVP refactoring (ADR-0001), `MainFormPresenter` absorbed all business logic from `MainForm`. While this achieved testability, the Presenter itself grew to 1,650+ lines handling four distinct concerns: mod operations, patch management, navigation, and status coordination. This violated the Single Responsibility Principle and made it difficult to test individual features in isolation.

## Decision Drivers

- **Single responsibility** — Each class should have exactly one reason to change
- **Test isolation** — Testing mod installation shouldn't require setting up patch watcher infrastructure
- **Feature boundaries** — Different team members should be able to work on patches vs navigation without merge conflicts
- **Composition over inheritance** — Sub-presenters should be composable, not a rigid class hierarchy
- **Backward compatibility** — `MainForm` should still interact with a single coordinator (no view changes)

## Considered Alternatives

### Alternative 1: Composition-Based Presenter Decomposition — Chosen

Split `MainFormPresenter` into three specialized presenters coordinated by the original Presenter acting as a thin coordinator.

- ✅ Good, because each presenter has a single clear responsibility (~300-500 lines each)
- ✅ Good, because features can be tested in complete isolation
- ✅ Good, because `MainForm` doesn't change — it still talks to one coordinator
- ✅ Good, because DI registration is clean with `AddPresenters()` extension method
- ❌ Bad, because cross-presenter communication requires event wiring through the coordinator
- ❌ Bad, because 6 new files (3 interfaces + 3 implementations) must be maintained

### Alternative 2: Keep Monolithic Presenter with Regions

Keep `MainFormPresenter` as one file but organize it with `#region` blocks.

- ✅ Good, because zero new files — simplest change
- ❌ Bad, because regions don't enforce boundaries — one concern can still bleed into another
- ❌ Bad, because test files remain large and unfocused
- ❌ Bad, because the file will continue growing with every new feature

### Alternative 3: Mediator Pattern (MediatR-style)

Use a mediator to decouple commands/queries from handlers.

- ✅ Good, because it provides maximum decoupling between features
- ✅ Good, because adding new features is just adding a new handler
- ❌ Bad, because it adds a mediator library dependency (or requires building one)
- ❌ Bad, because the indirection makes debugging harder (commands go through a pipeline)
- ❌ Bad, because overkill for 4 distinct concerns — MediatR shines with dozens of handlers

### Alternative 4: Partial Classes (Same Class, Multiple Files)

Split `MainFormPresenter` into partial class files by feature area.

- ✅ Good, because it improves navigation without changing the API
- ❌ Bad, because it is syntactic sugar — all methods still share the same state, no real isolation
- ❌ Bad, because partial classes cannot have independent unit tests
- ❌ Bad, because it doesn't enforce boundaries (methods can freely access unrelated state)

## Decision

We will decompose `MainFormPresenter` into three specialized presenters using **composition**:

| Presenter                 | Responsibility                            | Why Separate                       |
| ------------------------- | ----------------------------------------- | ---------------------------------- |
| `IModOperationsPresenter` | Install, reinstall, disable operations    | Changes when mod workflow changes  |
| `IPatchPresenter`         | Patch updates, verification, file watcher | Changes when patch system changes  |
| `INavigationPresenter`    | Hero selection, miscellaneous forms       | Changes when UI navigation changes |

### Architecture

```
MainFormPresenter (Coordinator)
    ├── ModOperationsPresenter   →  Install, Reinstall, Disable
    ├── PatchPresenter           →  Updates, Verification, Watcher (IDisposable)
    └── NavigationPresenter      →  Hero Selection, Misc Forms
```

`MainFormPresenter` delegates to the specialized presenters while coordinating cross-cutting concerns like button enable/disable and status bar updates.

### DI Registration

```csharp
public static IServiceCollection AddPresenters(this IServiceCollection services)
{
    services.AddTransient<IModOperationsPresenter, ModOperationsPresenter>();
    services.AddTransient<IPatchPresenter, PatchPresenter>();
    services.AddTransient<INavigationPresenter, NavigationPresenter>();
    services.AddTransient<MainFormPresenter>();
    return services;
}
```

> [!NOTE]
> Although sub-presenters are registered in the DI container to enable constructor mocking in unit tests, the coordinator `MainFormPresenter` instantiates them manually inside its constructor. This allows passing the shared `IMainFormView` and `Logger` references directly without complex factory wiring.

## Consequences

### Positive

- ✅ Each presenter has a single responsibility (~300-500 lines each vs 1,650+ monolith)
- ✅ 26 new unit tests added with full feature isolation (total: 269)
- ✅ Clearer code organization by feature — find patch logic in `PatchPresenter`, not searching 1,650 lines
- ✅ `ServiceLocator` fully removed from production code as part of this refactoring

### Negative

- ❌ 6 new files to maintain (3 interfaces + 3 implementations)
- ❌ Cross-presenter coordination requires event wiring through the coordinator
- ❌ Slight increase in DI container complexity
- ❌ **Trade-off:** Coordination requires manual instantiation in the coordinator constructor rather than full container auto-resolution.

### Metrics

| Metric                      | Before | After              |
| --------------------------- | ------ | ------------------ |
| `MainFormPresenter` lines   | 1,650+ | ~200 (coordinator) |
| Specialized presenter lines | 0      | ~300-500 each      |
| Unit tests (total)          | 243    | 269 (+26)          |
| Files for presenter layer   | 2      | 8                  |

## Related

- [ADR-0001: Refactor MainForm to MVP](./0001-refactor-mainform-mvp.md) — created the original monolithic Presenter
- [ADR-0002: DI Migration with Factory Pattern](./0002-complete-di-migration-factory-pattern.md) — provides the DI infrastructure for new presenters
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- `UI/Presenters/ModOperationsPresenter.cs`
- `UI/Presenters/PatchPresenter.cs`
- `UI/Presenters/NavigationPresenter.cs`
