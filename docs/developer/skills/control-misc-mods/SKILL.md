---
name: Control Misc Mods
description: Enable, disable, and query weather effects, HUD skins, terrain, and other misc mods programmatically
---

# Control Misc Mods (Weather, HUD, Terrain)

Misc mods are non-hero cosmetic modifications: weather effects, terrain skins, HUD themes, shaders, and more.

## Available Categories

| Category    | Examples                        | Description               |
| :---------- | :------------------------------ | :------------------------ |
| Weather     | Rain, Snow, Harvest, Pestilence | Weather particle effects  |
| HUD         | Immortal Gardens, Dire Shards   | HUD skin overlays         |
| Terrain     | Reef Edge, Immortal Gardens     | Map terrain replacements  |
| Shader      | Custom color grading            | Post-processing effects   |
| AtkModifier | Custom attack effects           | Hero attack particle mods |

## Enable Weather Effect

```csharp
var miscGen = new MiscGenerationService(logger: logger);

var selections = new Dictionary<string, string>
{
    ["Weather"] = "Rain"
};

var result = await miscGen.PerformGenerationAsync(
    dotaPath, selections,
    log: msg => Console.WriteLine(msg), ct);

if (result.Success)
    Console.WriteLine("Weather applied!");
```

## Enable HUD Skin

```csharp
var selections = new Dictionary<string, string>
{
    ["HUD"] = "Immortal Gardens"
};

await miscGen.PerformGenerationAsync(dotaPath, selections,
    msg => Console.WriteLine(msg), ct);
```

## Apply Multiple Misc Mods

```csharp
var selections = new Dictionary<string, string>
{
    ["Weather"] = "Snow",
    ["HUD"] = "Dire Shards",
    ["Terrain"] = "Reef Edge"
};

var result = await miscGen.PerformGenerationAsync(dotaPath, selections,
    msg => Console.WriteLine(msg), ct);
```

## Reset to Default

Set selection to `"Default"` to remove a mod:

```csharp
var selections = new Dictionary<string, string>
{
    ["Weather"] = "Default"
};

await miscGen.PerformGenerationAsync(dotaPath, selections,
    msg => Console.WriteLine(msg), ct);
```

## Check Active Misc Mods

```csharp
var activeMods = serviceProvider.GetRequiredService<IActiveModsService>();

// Check specific category
var weather = await activeMods.GetActiveMiscModAsync(dotaPath, "Weather");
if (weather != null)
    Console.WriteLine($"Weather: {weather.SelectedChoice}");

// List all active misc mods
var allMisc = await activeMods.GetActiveMiscModsAsync(dotaPath);
foreach (var mod in allMisc)
    Console.WriteLine($"  {mod.Category}: {mod.SelectedChoice}");
```

## Read Extraction Log Directly

```csharp
var log = MiscExtractionLog.Load(dotaPath);
if (log != null)
{
    Console.WriteLine($"Generated: {log.GeneratedAt}");
    Console.WriteLine($"Mode: {log.Mode}");

    foreach (var (category, choice) in log.Selections)
        Console.WriteLine($"  {category}: {choice}");

    // Get files for a category
    var weatherFiles = log.GetFiles("Weather");
    Console.WriteLine($"Weather files: {weatherFiles.Count}");

    // Check for conflicts
    foreach (var conflict in log.ConflictsDetected)
        Console.WriteLine($"  Conflict: {conflict}");
}
```

## Generation Pipeline

The `PerformGenerationAsync` method executes this pipeline:

1. **Extract** — VPK extraction via `IVpkExtractor`
2. **Modify** — Asset modification via `AssetModifierService`
3. **Recompile** — VPK recompilation via `IVpkRecompiler`
4. **Replace** — Swap original VPK via `IVpkReplacer`
5. **Cleanup** — Remove temp files

## Extraction Log Format

Stored at `game/_ArdysaMods/_temp/misc_extraction_log.json`:

```json
{
   "generatedAt": "2026-02-14T12:00:00Z",
   "mode": "CleanGenerate",
   "selections": { "Weather": "Rain", "HUD": "Immortal Gardens" },
   "installedFiles": { "Weather": ["particles/rain.vpcf"] },
   "conflictsDetected": [],
   "resolutionsApplied": {}
}
```

## Key Files

- **MiscGenerationService:** `Core/Services/Misc/MiscGenerationService.cs`
- **AssetModifierService:** `Core/Services/Misc/AssetModifierService.cs`
- **MiscExtractionLog:** `Core/Models/MiscExtractionLog.cs`
- **MiscOption:** `Core/Models/MiscOption.cs`
