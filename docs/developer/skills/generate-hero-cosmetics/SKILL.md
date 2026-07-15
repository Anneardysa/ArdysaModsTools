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

Heroes are loaded from `heroes.json` (CDN/GitHub first, local fallback) via
`HeroService.LoadHeroesAsync` → `HeroModelMapper.MapFromSummaries`:

```csharp
public sealed class HeroModel
{
    public List<int> ItemIds { get; set; }                   // "id" — items_game.txt block ids
    public string Name { get; set; }                         // "name"
    public string HeroId { get; set; }                       // "used_by_heroes", e.g. npc_dota_hero_antimage
    public string LocalizedName { get; set; }                // "localized_name"
    public string PrimaryAttribute { get; set; }             // "primary_attr"
    public int? Method { get; set; }                         // optional base-priority: 1 = Base first, 2 = Base last
    public Dictionary<string, List<string>> Sets { get; set; } // "sets": setName -> asset URLs
    public string Id => HeroId;                               // convenience
}
```

## Base-Priority & Merge Order

When several layers are selected for one hero (a Base + a Set/Custom/Persona + Items), each
layer's `index.txt` blocks are merged into `items_game.txt` as **layers, last-writer-wins**:

- Layers apply foundation → top by `GetSortWeight` (descending). **Every** layer is written;
  a later, lower layer overrides earlier ones for the **same** item id, so a specifically
  selected Item wins its slot while non-overlapping slots from every layer still apply.
- The base's rank is resolved per hero by `ResolveBaseWins(hero.Method, detectedHeroBase)`:
  the optional `heroes.json` `"method"` (1 = Base first, 2 = Base last) **overrides** the
  VKV-aware `item_slot hero_base` auto-detection; absent → detection.
  - `method 1` / `hero_base` present → `Base → Sets/Custom/Persona → Items`
  - `method 2` / no `hero_base` → `Sets/Custom/Persona → Items → Base`
  - `Items` are always layered below `Sets/Custom/Persona`.
- Each winning block is applied **verbatim** onto vanilla `items_game.txt` via
  `KeyValuesBlockHelper.OverlayBlockPreservingStructure`.

Generation emits `[DEBUG] Priority/Order/Override` lines (Visual Studio Output window) showing
the resolved method, the layer order, and which layer won each id. See
[ADR-0008](../../../adr/0008-hero-cosmetic-priority-merge.md) for the rationale.

Set the per-hero `method` with the tooling script: `python 2-patch_models.py --auto-detect`
(reads each base set's `index.txt`) or `--hero <name> --set-method <1|2>`.

## Key Files

- **HeroGenerationService:** `Core/Services/Hero/HeroGenerationService.cs`
- **HeroSetPatcherService:** `Core/Services/Hero/HeroSetPatcherService.cs`
- **HeroExtractionLog:** `Core/Models/HeroExtractionLog.cs`
- **HeroModel:** `Core/Models/HeroModel.cs`
