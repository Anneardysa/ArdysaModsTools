---
name: Generate Hero Cosmetics
description: Install custom hero cosmetic sets by generating patched VPKs with HeroGenerationService
---

# Generate Hero Cosmetics

Use `HeroGenerationService` to install custom hero cosmetic sets into Dota 2.

## Setup

```csharp
var heroGen = serviceProvider.GetRequiredService<IHeroGenerationService>();
```

## Generate Hero Sets

Generate a batch of hero cosmetic sets:

```csharp
var heroSets = new Dictionary<string, string>
{
    ["npc_dota_hero_antimage"] = "https://cdn.example.com/sets/antimage_mage_slayer.zip",
    ["npc_dota_hero_invoker"] = "https://cdn.example.com/sets/invoker_dark_artistry.zip"
};

var result = await heroGen.GenerateBatchAsync(
    dotaPath,
    heroSets,
    progress: (percent, status) =>
    {
        Console.WriteLine($"[{percent}%] {status}");
    },
    ct);

if (result.Success)
    Console.WriteLine($"Generated {heroSets.Count} hero set(s)");
```

## Check Active Hero Sets

```csharp
var activeMods = serviceProvider.GetRequiredService<IActiveModsService>();

// Check specific hero
var am = await activeMods.GetActiveHeroModAsync(dotaPath, "npc_dota_hero_antimage");
if (am != null)
    Console.WriteLine($"Anti-Mage: {am.SetName} ({am.InstalledFiles.Count} files)");

// List all active hero sets
var heroes = await activeMods.GetActiveHeroModsAsync(dotaPath);
foreach (var hero in heroes)
    Console.WriteLine($"  {hero.HeroId}: {hero.SetName}");
```

## Read Hero Extraction Log

```csharp
var log = HeroExtractionLog.Load(dotaPath);
if (log != null)
{
    foreach (var entry in log.InstalledSets)
    {
        Console.WriteLine($"{entry.HeroId}: {entry.SetName}");
        Console.WriteLine($"  Files: {entry.Files.Count}");
        foreach (var file in entry.Files)
            Console.WriteLine($"    {file}");
    }
}
```

## Hero Set ZIP Contents

Each hero set is a ZIP archive containing:

| File/Folder     | Purpose                                        |
| :-------------- | :--------------------------------------------- |
| `index.txt`     | KeyValues block overrides for `items_game.txt` |
| `models/`       | 3D model files (`.vmdl_c`)                     |
| `materials/`    | Texture files (`.vmat_c`)                      |
| `particles/`    | Particle effects (`.vpcf_c`)                   |
| `localization/` | Tooltip overrides (optional)                   |

## HeroModel

Heroes are loaded from `heroes.json`:

```csharp
public class HeroModel
{
    public string Id { get; set; }              // "npc_dota_hero_antimage"
    public string Name { get; set; }            // "Anti-Mage"
    public List<HeroSetInfo> Sets { get; set; } // Available cosmetic sets
}
```

## Key Files

- **HeroGenerationService:** `Core/Services/Hero/HeroGenerationService.cs`
- **HeroSetPatcherService:** `Core/Services/Hero/HeroSetPatcherService.cs`
- **HeroExtractionLog:** `Core/Models/HeroExtractionLog.cs`
- **HeroModel:** `Core/Models/HeroModel.cs`
