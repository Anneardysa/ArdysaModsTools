---
name: Detect and Resolve Mod Conflicts
description: Detect overlapping files between mods, classify conflict severity, set mod priorities, and resolve using 6 strategies
---

# Detect and Resolve Mod Conflicts

When multiple mods modify the same files, ArdysaModsTools detects, classifies, and resolves conflicts using priorities and configurable strategies.

## Setup

```csharp
var detector = serviceProvider.GetRequiredService<IConflictDetector>();
var resolver = serviceProvider.GetRequiredService<IConflictResolver>();
var priorityService = serviceProvider.GetRequiredService<IModPriorityService>();
```

## Step 1: Create Mod Sources

Each mod that could conflict is represented as a `ModSource`:

```csharp
var weatherRain = new ModSource
{
    ModId = "Weather_Rain",
    ModName = "Rain",
    Category = "Weather",
    Priority = 10,     // Lower = higher precedence
    AppliedAt = DateTime.UtcNow,
    AffectedFiles = new List<string>
    {
        "particles/weather/rain.vpcf_c",
        "materials/environment/weather_overlay.vmat_c"
    }
};

var weatherSnow = new ModSource
{
    ModId = "Weather_Snow",
    ModName = "Snow",
    Category = "Weather",
    Priority = 20,     // Lower priority than Rain
    AppliedAt = DateTime.UtcNow.AddMinutes(-5),
    AffectedFiles = new List<string>
    {
        "particles/weather/rain.vpcf_c",     // ← Overlaps with Rain!
        "particles/weather/snow_particles.vpcf_c"
    }
};

// Shorthand factory:
var hudMod = ModSource.FromSelection("HUD", "Immortal Gardens");
```

## Step 2: Detect Conflicts

```csharp
var mods = new[] { weatherRain, weatherSnow, hudMod };
IReadOnlyList<ModConflict> conflicts = await detector.DetectConflictsAsync(mods, dotaPath, ct);

Console.WriteLine($"Found {conflicts.Count} conflict(s)");

foreach (var conflict in conflicts)
{
    Console.WriteLine($"  [{conflict.Severity}] {conflict.Type}: {conflict.Description}");
    Console.WriteLine($"  Affected files: {string.Join(", ", conflict.AffectedFiles)}");
    Console.WriteLine($"  Between: {string.Join(" vs ", conflict.ConflictingSources.Select(s => s.ModName))}");
    Console.WriteLine($"  Needs user input: {conflict.RequiresUserIntervention}");
}
```

### Filter by Severity

```csharp
var critical = detector.GetConflictsBySeverity(conflicts, ConflictSeverity.Critical);
bool hasCritical = detector.HasCriticalConflicts(conflicts);
bool needsInput = detector.RequiresUserIntervention(conflicts);
```

### Check Single Pair

```csharp
ModConflict? conflict = await detector.CheckSingleConflictAsync(weatherRain, weatherSnow, ct);
if (conflict != null)
    Console.WriteLine($"Conflict: {conflict.Description}");
```

## Step 3: Set Priorities

Lower priority number = wins the conflict. Range: 1–999, default: 100.

```csharp
// Set individual mod priority
await priorityService.SetModPriorityAsync(
    modId: "Weather_Rain",
    modName: "Rain",
    category: "Weather",
    priority: 10,          // Wins over Snow (priority 20)
    dotaPath, ct);

await priorityService.SetModPriorityAsync("Weather_Snow", "Snow", "Weather", 20, dotaPath, ct);

// Read priority
int rainPriority = await priorityService.GetModPriorityAsync("Weather_Rain", dotaPath, ct);

// Get all priorities ordered (lowest first = highest precedence)
var ordered = await priorityService.GetOrderedPrioritiesAsync(dotaPath, category: "Weather", ct);
foreach (var p in ordered)
    Console.WriteLine($"  {p.ModName}: Priority {p.Priority}");
```

### Apply Priorities to Mod Sources

```csharp
// Sorts by priority and assigns Priority values from config
IReadOnlyList<ModSource> sorted = await priorityService.ApplyPrioritiesAsync(mods, dotaPath, ct);
// sorted[0] is the highest-priority mod
```

## Step 4: Resolve Conflicts

### Auto-Resolve All (Using Priority Config)

```csharp
var config = await priorityService.LoadConfigAsync(dotaPath, ct);

IReadOnlyList<ConflictResolutionResult> results = await resolver.ResolveAllAsync(conflicts, config, ct);

foreach (var result in results)
{
    if (result.Success)
        Console.WriteLine($"  ✅ {result.ConflictId}: {result.WinningSource?.ModName} wins ({result.UsedStrategy})");
    else
        Console.WriteLine($"  ❌ {result.ConflictId}: {result.ErrorMessage}");
}
```

### Resolve Single Conflict with Specific Strategy

```csharp
// Use higher priority mod
var result = await resolver.ResolveAsync(conflict, ResolutionStrategy.HigherPriority, ct);

// Use most recently applied mod
var result2 = await resolver.ResolveAsync(conflict, ResolutionStrategy.MostRecent, ct);

// Keep existing mod (discard new)
var result3 = await resolver.ResolveAsync(conflict, ResolutionStrategy.KeepExisting, ct);

// Use new mod (discard existing)
var result4 = await resolver.ResolveAsync(conflict, ResolutionStrategy.UseNew, ct);

// Attempt merge (Script/Config conflicts only, falls back to HigherPriority)
var result5 = await resolver.TryMergeAsync(conflict, ct);
```

### Apply User Choice (Interactive)

```csharp
// Present options to user
foreach (var option in conflict.AvailableResolutions)
    Console.WriteLine($"  [{option.Id}] {option.Strategy}: {option.Description}");

// User picks an option
var chosen = conflict.AvailableResolutions[0];  // e.g., "Use mod with higher priority"
var result = await resolver.ApplyUserChoiceAsync(conflict, chosen, ct);
```

### Check If Auto-Resolvable

```csharp
bool canAuto = resolver.CanAutoResolve(conflict, config);
// true for Low/Medium severity (if AutoResolveNonBreaking = true)
// false for Critical severity (always requires user)
```

## Step 5: Configure Default Strategies

```csharp
var config = await priorityService.LoadConfigAsync(dotaPath, ct);

// Set global default
config.DefaultStrategy = ResolutionStrategy.HigherPriority;

// Enable auto-resolve for Low+Medium severity
config.AutoResolveNonBreaking = true;

// Set per-category strategies
config.CategoryStrategies["Weather"] = ResolutionStrategy.MostRecent;
config.CategoryStrategies["HUD"] = ResolutionStrategy.HigherPriority;
config.CategoryStrategies["River"] = ResolutionStrategy.MostRecent;

// Save to disk
await priorityService.SaveConfigAsync(config, dotaPath, ct);
```

## Conflict Types

| Type            | Description                         | Default Resolution      |
| :-------------- | :---------------------------------- | :---------------------- |
| `File`          | Same file path from multiple mods   | Priority-based          |
| `Script`        | Overlapping KV script modifications | Merge or priority       |
| `Asset`         | Same VPK entry (models, textures)   | Priority-based          |
| `Configuration` | Conflicting game settings           | Priority or user choice |

## Severity Levels

| Severity   | Auto-Resolve? | Criteria                                                 |
| :--------- | :------------ | :------------------------------------------------------- |
| `Low`      | ✅ Always     | ≤2 overlapping files                                     |
| `Medium`   | ✅ If enabled | 3–5 files or script conflicts                            |
| `High`     | ❌            | 6–10 files or config conflicts                           |
| `Critical` | ❌ Never      | >10 files, core game files, or same-category >10 overlap |

## Resolution Strategies

| Strategy         | How Winner Is Determined                         |
| :--------------- | :----------------------------------------------- |
| `HigherPriority` | Lowest `ModSource.Priority` number wins          |
| `LowerPriority`  | Highest `ModSource.Priority` number wins         |
| `MostRecent`     | Latest `ModSource.AppliedAt` timestamp wins      |
| `Merge`          | Script/Config only; falls back to HigherPriority |
| `KeepExisting`   | First source wins (existing state preserved)     |
| `UseNew`         | Last source wins (new mod applied)               |
| `Interactive`    | User must explicitly choose                      |

## Priority Config File

Persisted at `game/_ArdysaMods/_temp/mod_priority.json`:

```json
{
   "lastModified": "2026-02-14T12:00:00Z",
   "priorities": [
      {
         "modId": "Weather_Rain",
         "modName": "Rain",
         "category": "Weather",
         "priority": 10,
         "isLocked": false
      },
      {
         "modId": "Weather_Snow",
         "modName": "Snow",
         "category": "Weather",
         "priority": 20,
         "isLocked": false
      }
   ],
   "defaultStrategy": 0,
   "autoResolveNonBreaking": true,
   "categoryStrategies": { "Weather": 2, "River": 2 }
}
```

## Key Files

- **ConflictDetector:** `Core/Services/Conflict/ConflictDetector.cs`
- **ConflictResolver:** `Core/Services/Conflict/ConflictResolver.cs`
- **ModPriorityService:** `Core/Services/Conflict/ModPriorityService.cs`
- **ModConflict model:** `Core/Models/ModConflict.cs`
- **ModPriority model:** `Core/Models/ModPriority.cs`
- **Enums:** `Core/Models/ConflictType.cs`
