---
name: Custom Mod Integration
description: Create, validate, register, and install custom cosmetic mods with proper file structure, metadata, and extraction logs
---

# Custom Mod Integration

Step-by-step guide for creating a custom cosmetic mod that ArdysaModsTools recognizes and manages.

## Required File Structure

A custom mod must follow this directory structure inside the VPK:

```
your_mod/
├── models/                    ← 3D models (.vmdl_c)
├── materials/                 ← Textures (.vmat_c)
├── particles/                 ← Particle effects (.vpcf_c)
├── sounds/                    ← Audio files (.vsnd_c)  ← Music packs
├── panorama/                  ← HUD elements
├── resource/                  ← Localization/tooltips
└── scripts/npc/              ← KV overrides
```

## Step 1: Create a ModSource

Every mod needs a `ModSource` to track it in the system:

```csharp
var customMod = new ModSource
{
    ModId = "Weather_CustomRain",
    ModName = "Custom Rain",
    Category = "Weather",
    Priority = 50,
    AppliedAt = DateTime.UtcNow,
    AffectedFiles = new List<string>
    {
        "particles/weather/rain.vpcf_c",
        "materials/environment/rain_overlay.vmat_c"
    }
};

// Or use the factory:
var mod = ModSource.FromSelection("Weather", "Custom Rain");
```

## Step 2: Validate the VPK

Before installation, validate the VPK contains the required marker:

```csharp
var installer = serviceProvider.GetRequiredService<IModInstallerService>();

// Check VPK structure
bool isValid = await installer.ValidateVpkAsync(vpkPath, ct);
if (!isValid)
{
    Console.WriteLine("Invalid VPK: missing _ArdysaMods marker");
    return;
}
```

## Step 3: Install with Manual Install

For user-provided VPK files:

```csharp
var result = await installer.ManualInstallModsAsync(dotaPath, userVpkPath, ct);
if (result.Success)
    Console.WriteLine("Custom mod installed!");
else
    Console.WriteLine($"Failed: {result.Message}");
```

## Step 4: Register in Extraction Log

After installing custom files, record them in the extraction log for tracking and cleanup:

### Hero Cosmetic Mod

```csharp
var log = HeroExtractionLog.Load(dotaPath) ?? new HeroExtractionLog();

log.InstalledSets.Add(new HeroSetEntry
{
    HeroId = "npc_dota_hero_invoker",
    SetName = "Dark Artistry Custom",
    Files = new List<string>
    {
        "models/heroes/invoker/invoker_custom.vmdl_c",
        "materials/models/heroes/invoker/invoker_custom_color.vmat_c",
        "particles/heroes/invoker/custom_orbs.vpcf_c"
    }
});

log.Save(dotaPath);
// Saved to: game/_ArdysaMods/_temp/hero_extraction_log.json
```

### Misc Mod (HUD, Weather, Terrain, Music)

```csharp
var log = MiscExtractionLog.Load(dotaPath) ?? new MiscExtractionLog();

// Record the selection
log.Selections["Weather"] = "Custom Rain";
log.Mode = "AddToCurrent";
log.GeneratedAt = DateTime.UtcNow;

// Track installed files by category
log.AddFiles("Weather", new[]
{
    "particles/weather/rain.vpcf_c",
    "materials/environment/rain_overlay.vmat_c"
});

log.Save(dotaPath);
// Saved to: game/_ArdysaMods/_temp/misc_extraction_log.json
```

## Step 5: Set Priority (For Conflict Resolution)

```csharp
var priorityService = serviceProvider.GetRequiredService<IModPriorityService>();

await priorityService.SetModPriorityAsync(
    modId: "Weather_CustomRain",
    modName: "Custom Rain",
    category: "Weather",
    priority: 5,           // High priority (lower number = wins conflicts)
    dotaPath, ct);
```

## Step 6: Detect Conflicts Before Install

```csharp
var detector = serviceProvider.GetRequiredService<IConflictDetector>();

var existingMods = new[] { existingWeatherMod };
var allMods = existingMods.Append(customMod).ToArray();

var conflicts = await detector.DetectConflictsAsync(allMods, dotaPath, ct);

if (conflicts.Any())
{
    foreach (var c in conflicts)
        Console.WriteLine($"  Conflict: {c.Description} (Severity: {c.Severity})");

    // Resolve automatically with priority config
    var resolver = serviceProvider.GetRequiredService<IConflictResolver>();
    var config = await priorityService.LoadConfigAsync(dotaPath, ct);
    var results = await resolver.ResolveAllAsync(conflicts, config, ct);

    foreach (var r in results)
        Console.WriteLine($"  → Winner: {r.WinningSource?.ModName}");
}
```

## Step 7: Verify Installation

```csharp
// Check active mods
var activeMods = serviceProvider.GetRequiredService<IActiveModsService>();
var info = await activeMods.GetActiveModsAsync(dotaPath);

Console.WriteLine($"Total active mods: {info.TotalModCount}");
foreach (var hero in info.HeroMods)
    Console.WriteLine($"  Hero: {hero.HeroId} → {hero.SetName}");
foreach (var misc in info.MiscMods)
    Console.WriteLine($"  {misc.Category}: {misc.SelectedChoice}");

// Check status
var status = await statusService.GetDetailedStatusAsync(dotaPath, ct);
Console.WriteLine($"Status: {status.StatusText}");
```

## Complete Example: Install Custom Music Pack

End-to-end workflow for installing a custom music pack:

```csharp
public async Task InstallCustomMusicPack(string dotaPath, string musicPackPath, CancellationToken ct)
{
    var installer = serviceProvider.GetRequiredService<IModInstallerService>();
    var priorityService = serviceProvider.GetRequiredService<IModPriorityService>();
    var activeMods = serviceProvider.GetRequiredService<IActiveModsService>();

    // 1. Validate VPK
    if (!await installer.ValidateVpkAsync(musicPackPath, ct))
        throw new InvalidOperationException("Invalid VPK file");

    // 2. Install VPK
    var result = await installer.ManualInstallModsAsync(dotaPath, musicPackPath, ct);
    if (!result.Success)
        throw new Exception($"Install failed: {result.Message}");

    // 3. Register in extraction log
    var log = MiscExtractionLog.Load(dotaPath) ?? new MiscExtractionLog();
    log.Selections["Music"] = "Custom Music Pack";
    log.Mode = "AddToCurrent";
    log.GeneratedAt = DateTime.UtcNow;
    log.AddFiles("Music", new[]
    {
        "sounds/music/custom_battle.vsnd_c",
        "sounds/music/custom_menu.vsnd_c",
        "sounds/music/custom_victory.vsnd_c"
    });
    log.Save(dotaPath);

    // 4. Set priority
    await priorityService.SetModPriorityAsync(
        "Music_Custom", "Custom Music Pack", "Music", 10, dotaPath, ct);

    // 5. Verify
    var info = await activeMods.GetActiveModsAsync(dotaPath);
    var musicMod = info.MiscMods.FirstOrDefault(m => m.Category == "Music");
    Console.WriteLine(musicMod != null
        ? $"✅ Music pack active: {musicMod.SelectedChoice}"
        : "❌ Music pack not detected");
}
```

## Extraction Log Schemas

### hero_extraction_log.json

```json
{
   "installedSets": [
      {
         "heroId": "npc_dota_hero_invoker",
         "setName": "Dark Artistry Custom",
         "files": ["models/heroes/invoker/invoker_custom.vmdl_c"]
      }
   ]
}
```

### misc_extraction_log.json

```json
{
   "generatedAt": "2026-02-14T12:00:00Z",
   "mode": "AddToCurrent",
   "selections": { "Music": "Custom Music Pack", "Weather": "Rain" },
   "installedFiles": {
      "Music": ["sounds/music/custom_battle.vsnd_c"],
      "Weather": ["particles/weather/rain.vpcf_c"]
   },
   "conflictsDetected": [],
   "resolutionsApplied": {}
}
```

## Key Files

- **ModInstallerService:** `Core/Services/Mods/ModInstallerService.cs`
- **HeroExtractionLog:** `Core/Models/HeroExtractionLog.cs`
- **MiscExtractionLog:** `Core/Models/MiscExtractionLog.cs`
- **ModSource:** `Core/Models/ModConflict.cs`
- **ActiveModsService:** `Core/Services/Mods/ActiveModsService.cs`
