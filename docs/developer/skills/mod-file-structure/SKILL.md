---
name: Mod File Structure
description: Directory layout, JSON schemas, VPK structure, and extraction log formats for Dota 2 mod files
---

# Mod File Structure

Complete specification of how ArdysaModsTools organizes mod files on disk.

## Directory Layout

```
dota 2 beta/                              ← Dota 2 root (dotaPath)
└── game/
    ├── dota/
    │   ├── gameinfo.gi                   ← Patched with _ArdysaMods mount
    │   └── steam.inf                     ← Dota version (monitored for updates)
    └── _ArdysaMods/
        ├── pak01_dir.vpk                 ← Main mod VPK
        └── _temp/                        ← Metadata (hidden)
            ├── config.json               ← App config
            ├── settings.json             ← User preferences
            ├── hero_extraction_log.json  ← Installed hero sets
            ├── misc_extraction_log.json  ← Installed misc mods
            └── version.json             ← Dota version at last patch
```

## hero_extraction_log.json

Tracks installed hero cosmetic sets:

```json
{
   "installedSets": [
      {
         "heroId": "npc_dota_hero_antimage",
         "setName": "Mage Slayer",
         "files": [
            "models/heroes/antimage/antimage.vmdl",
            "materials/models/heroes/antimage/antimage_color.vmat"
         ]
      }
   ]
}
```

### Code access:

```csharp
// Read
var log = HeroExtractionLog.Load(dotaPath);
foreach (var entry in log.InstalledSets)
    Console.WriteLine($"{entry.HeroId}: {entry.SetName}");

// Write
var newLog = new HeroExtractionLog();
newLog.InstalledSets.Add(new HeroSetEntry
{
    HeroId = "npc_dota_hero_axe",
    SetName = "Red Mist",
    Files = new List<string> { "models/heroes/axe/axe.vmdl" }
});
newLog.Save(dotaPath);

// Delete
HeroExtractionLog.Delete(dotaPath);
```

## misc_extraction_log.json

Tracks miscellaneous mods (weather, HUD, terrain):

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

### Code access:

```csharp
// Read
var log = MiscExtractionLog.Load(dotaPath);
foreach (var (cat, choice) in log.Selections)
    Console.WriteLine($"{cat}: {choice}");

// File operations
log.AddFiles("Weather", new[] { "particles/rain.vpcf" });
var files = log.GetFiles("Weather");
log.ClearFiles("Weather");
log.Save(dotaPath);
```

## Hero Set ZIP Structure (CDN)

Hero sets downloaded from CDN contain:

```
hero_set.zip/
├── index.txt            ← KeyValues overrides for items_game.txt
├── models/              ← 3D models (.vmdl_c)
├── materials/           ← Textures (.vmat_c)
├── particles/           ← Particle effects (.vpcf_c)
└── localization/        ← Tooltip overrides (optional)
```

## VPK Detection

```csharp
// Check if mod VPK exists
bool hasVpk = installer.IsRequiredModFilePresent(dotaPath);

// Validate VPK contains _ArdysaMods marker
bool valid = await installer.ValidateVpkAsync(vpkPath, ct);
```

## Gameinfo Patching

The `gameinfo.gi` file is patched to add the `_ArdysaMods` mount point. Detection:

```csharp
var status = await statusService.GetDetailedStatusAsync(dotaPath);
// ModStatus.Ready = patched, ModStatus.Disabled = not patched
```

## Key Constants

```csharp
// SHA1 hash of correctly patched gameinfo
public const string ModPatchSHA1 = "1A9B91FB43FE89AD104B8001282D292EED94584D";

// Marker text searched in gameinfo
public const string GameInfoMarker = "_Ardysa";
```

## Key Files

- **HeroExtractionLog:** `Core/Models/HeroExtractionLog.cs`
- **MiscExtractionLog:** `Core/Models/MiscExtractionLog.cs`
- **ModInstallerService:** `Core/Services/Mods/ModInstallerService.cs`
