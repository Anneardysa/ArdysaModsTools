# Mod File Structure

Complete specification of files, folders, and metadata that ArdysaModsTools uses to recognize and manage mods.

---

## Directory Layout

```
dota 2 beta/                              ← Dota 2 installation root (dotaPath)
└── game/
    ├── dota/
    │   └── gameinfo.gi                   ← Patched with _ArdysaMods mount point
    └── _ArdysaMods/
        ├── pak01_dir.vpk                 ← Main mod VPK (from ModsPack or generated)
        ├── gameinfo_branchspecific.gi     ← Patched gameinfo for mod loading
        └── _temp/                        ← Hidden metadata directory
            ├── config.json               ← App configuration
            ├── settings.json             ← User preferences
            ├── hero_extraction_log.json  ← Tracks installed hero sets
            ├── misc_extraction_log.json  ← Tracks installed misc mods
            └── version.json             ← Dota version at last patch
```

---

## Key Files

### pak01_dir.vpk

The main VPK archive containing all mod assets. This file is what Dota 2 loads at runtime.

**Created by:**

- `ModInstallerService.InstallModsAsync()` — downloads pre-built ModsPack
- `HeroGenerationService.GenerateBatchAsync()` — generates custom hero VPK
- `MiscGenerationService.PerformGenerationAsync()` — generates misc mod VPK
- `ModInstallerService.ManualInstallModsAsync()` — copies user-provided VPK

**Validation:**

```csharp
// Check if mod VPK contains the _ArdysaMods marker
bool isValid = await installer.ValidateVpkAsync(vpkPath, ct);

// Check if mods are installed at a path
bool installed = installer.IsRequiredModFilePresent(dotaPath);
```

---

### hero_extraction_log.json

Tracks which hero cosmetic sets are installed and what files they contain.

**Location:** `game/_ArdysaMods/_temp/hero_extraction_log.json`

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

**Code access:**

```csharp
// Read hero extraction log
var log = HeroExtractionLog.Load(dotaPath);
if (log != null)
{
    foreach (var entry in log.InstalledSets)
        Console.WriteLine($"{entry.HeroId}: {entry.SetName} ({entry.Files.Count} files)");
}

// Save hero extraction log
var newLog = new HeroExtractionLog();
newLog.InstalledSets.Add(new HeroSetEntry
{
    HeroId = "npc_dota_hero_axe",
    SetName = "Red Mist",
    Files = new List<string> { "models/heroes/axe/axe.vmdl" }
});
newLog.Save(dotaPath);
```

---

### misc_extraction_log.json

Tracks installed misc mods (weather, terrain, HUD) and their files.

**Location:** `game/_ArdysaMods/_temp/misc_extraction_log.json`

```json
{
   "generatedAt": "2026-02-14T12:00:00Z",
   "mode": "CleanGenerate",
   "selections": {
      "Weather": "Rain",
      "HUD": "Immortal Gardens",
      "Terrain": "Reef Edge"
   },
   "installedFiles": {
      "Weather": ["particles/rain/rain_001.vpcf"],
      "HUD": ["panorama/images/hud_skin/custom.png"]
   },
   "conflictsDetected": [],
   "resolutionsApplied": {}
}
```

**Code access:**

```csharp
// Read misc extraction log
var log = MiscExtractionLog.Load(dotaPath);
if (log != null)
{
    foreach (var (category, choice) in log.Selections)
        Console.WriteLine($"{category}: {choice}");

    var weatherFiles = log.GetFiles("Weather");
    Console.WriteLine($"Weather files: {weatherFiles.Count}");
}

// Save misc extraction log
var newLog = new MiscExtractionLog
{
    Mode = "CleanGenerate",
    Selections = new Dictionary<string, string>
    {
        ["Weather"] = "Snow"
    }
};
newLog.AddFiles("Weather", new[] { "particles/snow.vpcf" });
newLog.Save(dotaPath);
```

---

### version.json

Records the Dota 2 version at the time of last patch. Used to detect game updates.

**Location:** `game/_ArdysaMods/_temp/version.json`

**Used by:** `DotaVersionService.ComparePatchedVersionAsync()` — compares current `steam.inf` against saved state to detect updates.

---

### gameinfo_branchspecific.gi

Modified gameinfo that adds the `_ArdysaMods` mount point so Dota 2 loads mod assets.

**Detection:**

```csharp
// Check if gameinfo is patched (contains _ArdysaMods marker)
var status = await statusService.GetDetailedStatusAsync(dotaPath);
bool isPatched = status.Status == ModStatus.Ready || status.Status == ModStatus.NeedUpdate;
```

---

## Hero Set Structure (Remote CDN)

Hero sets are downloaded as ZIP archives from CDN. Each ZIP contains:

```
hero_set.zip/
├── index.txt            ← KeyValues block overrides for items_game.txt
├── models/              ← 3D model files (.vmdl_c)
├── materials/           ← Texture files (.vmat_c)
├── particles/           ← Particle effects (.vpcf_c)
└── localization/        ← Tooltip/description overrides (optional)
```

The `index.txt` file contains KeyValues blocks that replace entries in `items_game.txt` to register the custom cosmetic items.

---

## Misc Mod Configuration (Remote)

Available misc mods are fetched from CDN as `RemoteMiscConfig`:

```csharp
// RemoteMiscConfig schema
{
    "options": [
        {
            "id": "Weather",
            "displayName": "Weather Effects",
            "category": "Environment",
            "choices": ["Default", "Rain", "Snow", "Harvest", "Pestilence"],
            "thumbnailUrlPattern": "https://cdn.example.com/misc/weather/{choice}.png"
        },
        {
            "id": "HUD",
            "displayName": "HUD Skin",
            "category": "Interface",
            "choices": ["Default", "Immortal Gardens", "Dire Shards"]
        }
    ]
}
```

---

## Constants

Key markers used for mod detection:

```csharp
public static class ModConstants
{
    // SHA1 hash of the modded gameinfo file
    public const string ModPatchSHA1 = "1A9B91FB43FE89AD104B8001282D292EED94584D";

    // Marker text in modded gameinfo
    public const string GameInfoMarker = "_Ardysa";
}
```
