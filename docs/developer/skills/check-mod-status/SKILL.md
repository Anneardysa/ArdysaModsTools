---
name: Check Mod Status
description: Check mod installation status, detect issues, and get recommended actions using StatusService
---

# Check Mod Status

Use `IStatusService` to check mod installation status with step-based validation.

## Setup

```csharp
var statusService = serviceProvider.GetRequiredService<IStatusService>();
```

## Get Detailed Status

Performs comprehensive validation: Dota path → mods installed → gameinfo patched → signatures patched → version check.

```csharp
var status = await statusService.GetDetailedStatusAsync(dotaPath, ct);

Console.WriteLine($"Status: {status.Status}");         // Ready, NeedUpdate, NotInstalled, etc.
Console.WriteLine($"Text: {status.StatusText}");        // Human-readable status
Console.WriteLine($"Description: {status.Description}");
Console.WriteLine($"Action: {status.Action}");          // None, Install, Update, Enable, Fix
Console.WriteLine($"Button: {status.ActionButtonText}");// "Patch Update", "Install ModsPack"
Console.WriteLine($"Version: {status.Version}");
Console.WriteLine($"Color: {status.UIColor}");          // For UI display
```

## Handle Status States

```csharp
switch (status.Status)
{
    case ModStatus.Ready:
        // Mods active and up-to-date — no action needed
        break;

    case ModStatus.NeedUpdate:
        // Dota updated — run UpdatePatcherAsync
        await installer.UpdatePatcherAsync(dotaPath, msg => { }, ct);
        break;

    case ModStatus.NotInstalled:
        // No mods — run InstallModsAsync
        await installer.InstallModsAsync(dotaPath, ct: ct);
        break;

    case ModStatus.Disabled:
        // Mods present but gameinfo not patched
        break;

    case ModStatus.Error:
        Console.WriteLine($"Error: {status.ErrorMessage}");
        break;
}
```

## Force Refresh

Clear cache and re-check:

```csharp
var freshStatus = await statusService.ForceRefreshAsync(dotaPath, ct);
```

## Get Cached Status

Fast read without I/O:

```csharp
var cached = statusService.GetCachedStatus();
if (cached != null)
    Console.WriteLine($"Last known: {cached.Status}");
```

## Auto-Refresh (Periodic Monitoring)

```csharp
// Subscribe to status changes
statusService.OnStatusChanged += newStatus =>
{
    Console.WriteLine($"Status changed: {newStatus.StatusText}");
};

statusService.OnCheckingStarted += () =>
{
    Console.WriteLine("Checking...");
};

// Start monitoring
statusService.StartAutoRefresh(dotaPath);

// Stop when done
statusService.StopAutoRefresh();
```

## ModStatus Enum

```csharp
public enum ModStatus
{
    NotChecked,    // Never checked / path not set
    Checking,      // Currently checking (UI loading state)
    Ready,         // Mods active and up-to-date
    NeedUpdate,    // Dota updated, re-patch required
    Disabled,      // Mods present but not patched
    NotInstalled,  // No mods found
    Error          // Error during check
}
```

## RecommendedAction Enum

```csharp
public enum RecommendedAction
{
    None,     // No action needed (Ready)
    Install,  // Install mods (NotInstalled)
    Update,   // Re-patch after Dota update (NeedUpdate)
    Enable,   // Re-enable disabled mods (Disabled)
    Fix       // Fix error condition (Error)
}
```

## Key Files

- **IStatusService:** `Core/Interfaces/IStatusService.cs`
- **StatusService:** `Core/Services/Mods/StatusService.cs`
- **ModStatus:** `Core/Models/ModStatus.cs`
