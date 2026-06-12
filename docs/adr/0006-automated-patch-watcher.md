# ADR-0006: Automated Patch Watcher System

**Date:** 2026-02-10
**Status:** Accepted
**Deciders:** @Anneardysa

## Problem Statement

When Dota 2 receives a game update, Valve's patching process overwrites the `gameinfo.gi` configuration file and VPK archives, effectively removing all installed mods. Users had to manually re-run ArdysaModsTools after every Dota 2 update to restore their mods. This was frustrating for users who update Dota 2 frequently, and many didn't realize their mods were removed until they launched the game.

## Decision Drivers

- **User experience** — Mod restoration should be automatic and require zero user intervention
- **Reliability** — The watcher must detect real Dota 2 updates, not false triggers (e.g., saving a config)
- **Resource efficiency** — The watcher runs continuously in the background and must not consume noticeable CPU/memory
- **Safety** — Auto-patching must not corrupt game files or interfere with ongoing Dota 2 processes
- **User control** — Users should be able to enable/disable auto-patching

## Considered Alternatives

### Alternative 1: FileSystemWatcher on `gameinfo.gi` — Chosen

Use .NET's `FileSystemWatcher` to monitor changes to Dota 2's `gameinfo.gi` file. When Valve's patcher modifies this file, trigger an automatic mod re-application after a debounce delay.

- ✅ Good, because `FileSystemWatcher` is built into .NET — zero external dependencies
- ✅ Good, because `gameinfo.gi` is the definitive file that Dota 2 patches modify
- ✅ Good, because detection is instant (OS-level file notification) with negligible CPU usage
- ✅ Good, because debounce delay avoids triggering during mid-patch writes
- ❌ Bad, because `FileSystemWatcher` can miss events under heavy I/O (rare but documented)
- ❌ Bad, because false positives possible if user manually edits `gameinfo.gi`

### Alternative 2: Polling-Based Detection

Periodically check file hashes or timestamps of game files at a fixed interval (e.g., every 30 seconds).

- ✅ Good, because it cannot miss changes (checks explicitly)
- ❌ Bad, because polling wastes CPU cycles when no update has occurred (99.9% of the time)
- ❌ Bad, because 30-second poll interval means up to 30 seconds of latency before detection
- ❌ Bad, because hashing large VPK files is expensive on each poll

### Alternative 3: Steam API Integration

Use Steam's web API or local library to detect when Dota 2 finishes updating.

- ✅ Good, because it detects the actual update event, not just file changes
- ❌ Bad, because Steam's local API is undocumented and unstable across updates
- ❌ Bad, because web API requires authentication and network connectivity
- ❌ Bad, because the update event fires before files are fully written, requiring additional delay logic

### Alternative 4: Manual Re-Application Only

Require users to manually click "Install" after every Dota 2 update.

- ✅ Good, because it is the simplest implementation (no background process)
- ❌ Bad, because it is the core problem we're trying to solve — poor user experience
- ❌ Bad, because users forget or don't notice their mods are gone

## Decision

We will use **`FileSystemWatcher`** on Dota 2's `steam.inf` and `dota.signatures` files with a debounce mechanism and verification checks, delegated to a specialized `DotaPatchWatcherService`.

### Architecture

```
PatchPresenter (owns watcher lifecycle)
    └── DotaPatchWatcherService
            ├── steam.inf FileSystemWatcher (monitors game/dota/steam.inf)
            ├── dota.signatures FileSystemWatcher (monitors game/bin/win64/dota.signatures)
            ├── debounce: 3-second delay
            └── action: verify version/digest changes → notify via events
```

### Implementation

```csharp
// DotaPatchWatcherService.cs
public sealed class DotaPatchWatcherService : IDisposable
{
    private FileSystemWatcher? _steamInfWatcher;
    private FileSystemWatcher? _signaturesWatcher;
    private CancellationTokenSource? _debounceCts;
    private string? _dotaPath;
    private DotaVersionInfo? _lastKnownVersion;
    
    private const int DebounceDelayMs = 3000; // 3 seconds to let patches settle
    
    public event Action<PatchDetectedEventArgs>? OnPatchDetected;

    public async Task StartWatchingAsync(string dotaPath)
    {
        _dotaPath = dotaPath;
        _lastKnownVersion = await _versionService.GetVersionInfoAsync(dotaPath);
        
        // Setup steam.inf watcher
        string steamInfPath = Path.Combine(dotaPath, DotaPaths.SteamInfWindows);
        if (File.Exists(steamInfPath))
        {
            _steamInfWatcher = CreateWatcher(
                Path.GetDirectoryName(steamInfPath)!,
                Path.GetFileName(steamInfPath),
                "steam.inf");
        }
        
        // Setup dota.signatures watcher
        string signaturesPath = Path.Combine(dotaPath, DotaPaths.SignaturesWindows);
        if (File.Exists(signaturesPath))
        {
            _signaturesWatcher = CreateWatcher(
                Path.GetDirectoryName(signaturesPath)!,
                Path.GetFileName(signaturesPath),
                "dota.signatures");
        }
    }
    
    private void OnFileChanged(string filePath, string displayName)
    {
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _debounceCts, newCts);
        try { oldCts?.Cancel(); } catch { }
        oldCts?.Dispose();
        
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelayMs, newCts.Token);
                await CheckForPatchAsync();
            }
            catch (TaskCanceledException) { }
        });
    }

    private async Task CheckForPatchAsync()
    {
        var currentVersion = await _versionService.GetVersionInfoAsync(_dotaPath);
        bool versionChanged = !_lastKnownVersion.DotaVersion.Equals(currentVersion.DotaVersion, StringComparison.OrdinalIgnoreCase);
        bool digestChanged = !_lastKnownVersion.CurrentDigest.Equals(currentVersion.CurrentDigest, StringComparison.OrdinalIgnoreCase);
        
        if (versionChanged || digestChanged)
        {
            _lastKnownVersion = currentVersion;
            OnPatchDetected?.Invoke(new PatchDetectedEventArgs { ... });
        }
    }
}
```

### Safeguards

| Check                | Purpose                                                  |
| -------------------- | -------------------------------------------------------- |
| 3-second debounce    | Avoids triggering during mid-write sequences             |
| Version/Digest Check  | Verifies game version info changed (not our own write)   |
| Dota 2 running check | Won't patch while the game is actively running           |
| User toggle          | Auto-patch can be disabled in settings                   |

## Consequences

### Positive

- ✅ Mods are automatically restored within seconds of a Dota 2 update
- ✅ Zero CPU cost when no update is occurring (OS event-driven, not polling)
- ✅ Debounce prevents rapid-fire trigger during Dota 2's multi-file patch process
- ✅ `IDisposable` pattern ensures clean resource cleanup

### Negative

- ❌ `FileSystemWatcher` can theoretically miss events under extreme I/O load (very rare)
- ❌ False positives possible if user manually edits monitored files
- ❌ Background process must be properly disposed on application exit

### Metrics

| Metric                            | Manual Only                      | With Watcher            |
| --------------------------------- | -------------------------------- | ----------------------- |
| Mod restoration time after update | Minutes (user must notice + act) | ~10 seconds (automatic) |
| CPU usage (idle)                  | 0                                | ~0 (event-driven)       |
| User intervention required        | Every update                     | None                    |

## Related

- [ADR-0004: Presenter Decomposition for SRP](./0004-presenter-decomposition-srp.md) — `PatchPresenter` is one of the decomposed presenters
- `UI/Presenters/PatchPresenter.cs` — presenter using the watcher service
- `Core/Services/Update/DotaPatchWatcherService.cs` — watcher service implementation
- `Core/Services/Mods/StatusService.cs` — version and mod status tracking
