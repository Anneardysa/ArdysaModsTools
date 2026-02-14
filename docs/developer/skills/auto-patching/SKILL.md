---
name: Auto-Patching After Game Updates
description: Detect Dota 2 updates and automatically re-apply mod patches using DotaVersionService and StatusService
---

# Auto-Patching After Game Updates

When Dota 2 updates, file signatures change and the modded gameinfo may be overwritten. ArdysaModsTools detects this and re-applies patches.

## Check If Re-Patch Is Needed

### Quick check (minimal I/O):

```csharp
var versionService = new DotaVersionService(logger);
bool needsPatch = await versionService.QuickNeedsPatchCheckAsync(dotaPath);
```

### Detailed version info:

```csharp
var versionInfo = await versionService.GetVersionInfoAsync(dotaPath);
Console.WriteLine($"Current build: {versionInfo.PatchVersion}");
Console.WriteLine($"Needs patch: {versionInfo.NeedsPatch}");
```

### Compare versions:

```csharp
var (matches, current, patched) = await versionService.ComparePatchedVersionAsync(dotaPath);
if (!matches)
    Console.WriteLine($"Version mismatch: current={current}, patched={patched}");
```

## Re-Apply Patches

```csharp
var installer = serviceProvider.GetRequiredService<IModInstallerService>();

var result = await installer.UpdatePatcherAsync(
    dotaPath,
    statusCallback: msg => Console.WriteLine($"[Patch] {msg}"),
    ct);

if (result.Success)
{
    // Save version after successful patch
    await versionService.SavePatchedVersionJsonAsync(dotaPath);
    await versionService.SaveVersionCacheAsync(dotaPath);
    Console.WriteLine("Patches re-applied successfully");
}
```

## Monitor For Updates (Auto-Refresh)

Use `StatusService` to automatically detect Dota updates via timer + file watcher:

```csharp
var statusService = serviceProvider.GetRequiredService<IStatusService>();

statusService.OnStatusChanged += newStatus =>
{
    if (newStatus.Status == ModStatus.NeedUpdate)
        Console.WriteLine("Dota updated — re-patch needed!");
};

// Start monitoring (checks periodically + watches steam.inf)
statusService.StartAutoRefresh(dotaPath);

// Stop when done
statusService.StopAutoRefresh();
```

## Full Auto-Patch Implementation

```csharp
var statusService = serviceProvider.GetRequiredService<IStatusService>();
var installer = serviceProvider.GetRequiredService<IModInstallerService>();

statusService.OnStatusChanged += async newStatus =>
{
    if (newStatus.Status == ModStatus.NeedUpdate)
    {
        var result = await installer.UpdatePatcherAsync(dotaPath,
            msg => Console.WriteLine($"  {msg}"), CancellationToken.None);

        if (result.Success)
            await statusService.ForceRefreshAsync(dotaPath);
    }
};

statusService.StartAutoRefresh(dotaPath);
```

## Check Status Programmatically

```csharp
var status = await statusService.GetDetailedStatusAsync(dotaPath);

switch (status.Status)
{
    case ModStatus.Ready:       // Mods active and up-to-date
    case ModStatus.NeedUpdate:  // Dota updated, re-patch needed → call UpdatePatcherAsync
    case ModStatus.NotInstalled:// No mods installed → call InstallModsAsync
    case ModStatus.Disabled:    // Mods present but gameinfo not patched
    case ModStatus.Error:       // Error checking status
        break;
}

// Recommended action
Console.WriteLine($"Action: {status.Action}");           // None, Install, Update, Enable, Fix
Console.WriteLine($"Button: {status.ActionButtonText}"); // "Patch Update", "Install ModsPack"
```

## Version Data

| Source      | File                                  | Purpose                |
| :---------- | :------------------------------------ | :--------------------- |
| Dota build  | `game/dota/steam.inf`                 | Current Dota 2 version |
| Saved state | `game/_ArdysaMods/_temp/version.json` | Version at last patch  |

## Key Files

- **DotaVersionService:** `Core/Services/Mods/DotaVersionService.cs`
- **StatusService:** `Core/Services/Mods/StatusService.cs`
- **ModStatus enum:** `Core/Models/ModStatus.cs`
