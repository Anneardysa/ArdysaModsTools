# ADR-0001: Refactor MainForm to MVP Pattern

**Date:** 2026-01-28
**Status:** Accepted
**Deciders:** @Anneardysa

## Problem Statement

`MainForm.cs` had grown to over 2,000 lines with business logic tightly coupled to UI event handlers. This made unit testing impossible without running the UI, caused frequent regressions when changing logic or layout, and made code navigation extremely difficult for contributors.

## Decision Drivers

- **Testability** — Business logic must be unit-testable without instantiating WinForms controls
- **Separation of concerns** — UI rendering and business logic should change independently
- **Maintainability** — Contributors need to navigate code by responsibility, not by searching a monolithic file
- **Incremental migration** — The refactoring must be achievable incrementally without rewriting the entire form
- **WinForms compatibility** — The pattern must work naturally with WinForms (no data-binding hacks)

## Considered Alternatives

### Alternative 1: MVP (Model-View-Presenter) — Chosen

The Presenter holds all business logic and communicates with the View through an explicit interface contract (`IMainFormView`). The View is a thin wrapper that delegates events to the Presenter.

- ✅ Good, because WinForms is event-driven which maps directly to Presenter method calls
- ✅ Good, because the View interface makes mocking trivial for unit tests
- ✅ Good, because the migration can be done method-by-method (move one event handler at a time)
- ✅ Good, because the learning curve is lower than MVVM — no binding infrastructure required
- ❌ Bad, because the View interface must be kept in sync with the Form's actual capabilities
- ❌ Bad, because it introduces more files (View, Presenter, Interface) per form

### Alternative 2: MVVM (Model-View-ViewModel)

ViewModel exposes properties and commands that the View binds to. Changes propagate automatically through data binding.

- ✅ Good, because two-way data binding reduces boilerplate for simple property updates
- ❌ Bad, because WinForms lacks native `INotifyPropertyChanged` binding — requires third-party libraries or manual binding code
- ❌ Bad, because debugging invisible binding failures is significantly harder than explicit method calls
- ❌ Bad, because the infrastructure overhead (binding engine, command pattern) is disproportionate for this project's scale

### Alternative 3: MVC (Model-View-Controller)

Controller handles input, updates Model, and View observes Model changes directly.

- ✅ Good, because it is a well-known pattern with extensive documentation
- ❌ Bad, because the View-Model observation pattern encourages direct coupling in WinForms
- ❌ Bad, because controller routing logic adds complexity without clear benefit in a desktop app (no HTTP requests to route)

### Alternative 4: Do Nothing (Keep Monolithic MainForm)

Leave all logic in `MainForm.cs` and manage complexity through comments and regions.

- ✅ Good, because zero effort required
- ❌ Bad, because the file will continue growing beyond 2,000+ lines
- ❌ Bad, because all future features compound the maintenance burden
- ❌ Bad, because automated testing remains impossible

## Decision

We will refactor `MainForm` to follow **MVP (Model-View-Presenter)** because it is the natural fit for WinForms' event-driven architecture, requires no binding infrastructure, and enables incremental migration.

### Structure

```
UI/
├── Forms/
│   ├── MainForm.cs           # View (event wiring + delegation)
│   └── MainForm.View.cs      # IMainFormView implementation (partial class)
├── Interfaces/
│   └── IMainFormView.cs      # View contract (testable surface)
└── Presenters/
    └── MainFormPresenter.cs  # All business logic lives here
```

### Interface Design

```csharp
public interface IMainFormView
{
    // State
    string? TargetPath { get; set; }
    bool IsVisible { get; }

    // UI Updates / Status Updates
    void SetModsStatus(bool isActive, string statusText);
    void SetModsStatusDetailed(ModStatusInfo statusInfo);
    void SetVersion(string version);
    void ShowCheckingState();

    // UI State Management
    void EnableAllButtons();
    void EnableDetectionButtonsOnly();
    void DisableAllButtons();
    void SetButtonEnabled(string buttonName, bool enabled);

    // Dialogs (abstracted for testability)
    DialogResult ShowMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon);
    DialogResult ShowStyledMessage(string title, string message, Forms.StyledMessageType type);
    void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000);
}
```

### Presenter Pattern

```csharp
public class MainFormPresenter
{
    private readonly IMainFormView _view;
    private readonly Logger _logger;
    private readonly IConfigService _config;
    
    // Composed sub-presenters for Single Responsibility Principle (ADR-0004)
    private readonly IModOperationsPresenter _modOperations;
    private readonly IPatchPresenter _patchPresenter;
    private readonly INavigationPresenter _navigationPresenter;

    public MainFormPresenter(IMainFormView view, Logger logger, IConfigService configService)
    {
        _view = view;
        _logger = logger;
        _config = configService;

        // Composed presenters instantiated to coordinate operations
        _modOperations = new ModOperationsPresenter(_view, _logger);
        _patchPresenter = new PatchPresenter(_view, _logger);
        _navigationPresenter = new NavigationPresenter(_view, _logger);
        
        WireUpPresenterEvents();
    }

    public async Task<bool> InstallAsync()
    {
        // ... delegates installation process and coordinates with IMainFormView
    }
}
```

### Migration Strategy

1. Create `IMainFormView` with the minimal set of methods needed
2. Create `MainFormPresenter` taking `IMainFormView` as a dependency
3. Move event handler logic from `MainForm` → `Presenter` one method at a time
4. Split `MainForm` into partial classes (`.cs` for events, `.View.cs` for interface impl)
5. Write unit tests for each migrated presenter method
6. Iterate — expand the interface only as needed

## Consequences

### Positive

- ✅ 243 unit tests now pass covering presenter logic
- ✅ Business logic is fully testable without instantiating any WinForms control
- ✅ Clear separation: UI changes don't break logic, logic changes don't break UI
- ✅ `MainForm.cs` reduced from 2,000+ to ~500 lines

### Negative

- ❌ More files per form (3: View, Presenter, Interface)
- ❌ View interface must be manually kept in sync with the form
- ❌ Slight learning curve for new contributors unfamiliar with MVP

### Metrics

| Metric                   | Before | After |
| ------------------------ | ------ | ----- |
| `MainForm.cs` lines      | 2,000+ | ~500  |
| Unit tests on UI logic   | 0      | 243   |
| Files per form component | 1      | 3     |

## Related

- [ADR-0002: Complete DI Migration with Factory Pattern](./0002-complete-di-migration-factory-pattern.md) — enables constructor injection for Presenter
- [ADR-0004: Presenter Decomposition for SRP](./0004-presenter-decomposition-srp.md) — further splits the Presenter
- `UI/Interfaces/IMainFormView.cs`
- `UI/Presenters/MainFormPresenter.cs`
