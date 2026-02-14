---
name: Query Active Mods
description: Retrieve and inspect currently installed hero sets and misc mods using ActiveModsService
---

# Query Active Mods

Use `IActiveModsService` to get a unified snapshot of all currently installed mods.

## Setup

```csharp
var activeMods = serviceProvider.GetRequiredService<IActiveModsService>();
```

## Get All Active Mods

```csharp
ActiveModInfo info = await activeMods.GetActiveModsAsync(dotaPath);

Console.WriteLine($"Total mods: {info.TotalModCount}");
Console.WriteLine($"Status: {info.OverallStatus}");   // Ready, NotInstalled, Error
Console.WriteLine($"Has mods: {info.HasActiveMods}");
Console.WriteLine($"Generated: {info.LastGeneratedAt}");

// List hero cosmetic sets
foreach (var hero in info.HeroMods)
    Console.WriteLine($"  Hero: {hero.HeroId} -> {hero.SetName} ({hero.InstalledFiles.Count} files)");

// List misc mods (weather, HUD, terrain)
foreach (var misc in info.MiscMods)
    Console.WriteLine($"  {misc.Category}: {misc.SelectedChoice} ({misc.InstalledFiles.Count} files)");
```

## Get Active Categories

```csharp
IReadOnlyList<string> categories = info.GetActiveCategories();
// Returns: ["Hero", "Weather", "HUD", "Terrain"]
```

## Check Single Hero

```csharp
var antiMage = await activeMods.GetActiveHeroModAsync(dotaPath, "npc_dota_hero_antimage");
if (antiMage != null)
    Console.WriteLine($"Anti-Mage: {antiMage.SetName}");
```

## Check Single Misc Category

```csharp
var weather = await activeMods.GetActiveMiscModAsync(dotaPath, "Weather");
if (weather != null)
    Console.WriteLine($"Weather: {weather.SelectedChoice}");
```

## Get Only Hero Mods

```csharp
IReadOnlyList<ActiveHeroMod> heroes = await activeMods.GetActiveHeroModsAsync(dotaPath);
```

## Get Only Misc Mods

```csharp
IReadOnlyList<ActiveMiscMod> misc = await activeMods.GetActiveMiscModsAsync(dotaPath);
```

## Models

### ActiveModInfo

```csharp
public record ActiveModInfo
{
    public ModStatus OverallStatus { get; init; }          // Ready, NotInstalled, Error, NotChecked
    public IReadOnlyList<ActiveHeroMod> HeroMods { get; init; }
    public IReadOnlyList<ActiveMiscMod> MiscMods { get; init; }
    public int TotalModCount { get; }                      // HeroMods.Count + MiscMods.Count
    public bool HasActiveMods { get; }                     // TotalModCount > 0
    public DateTime? LastGeneratedAt { get; init; }
    public IReadOnlyList<string> GetActiveCategories();    // Distinct active categories
}
```

### ActiveHeroMod

```csharp
public record ActiveHeroMod
{
    public string HeroId { get; init; }           // "npc_dota_hero_antimage"
    public string SetName { get; init; }          // "Mage Slayer"
    public IReadOnlyList<string> InstalledFiles { get; init; }
}
```

### ActiveMiscMod

```csharp
public record ActiveMiscMod
{
    public string Category { get; init; }          // "Weather", "HUD", "Terrain"
    public string SelectedChoice { get; init; }    // "Rain", "Immortal Gardens"
    public IReadOnlyList<string> InstalledFiles { get; init; }
}
```

## Data Sources

The service reads these JSON files (no new state created):

| Source    | Path                                              | Content                          |
| :-------- | :------------------------------------------------ | :------------------------------- |
| Hero sets | `game/_ArdysaMods/_temp/hero_extraction_log.json` | Installed hero cosmetic sets     |
| Misc mods | `game/_ArdysaMods/_temp/misc_extraction_log.json` | Weather, HUD, terrain selections |

## Key Files

- **Interface:** `Core/Interfaces/IActiveModsService.cs`
- **Implementation:** `Core/Services/Mods/ActiveModsService.cs`
- **Model:** `Core/Models/ActiveModInfo.cs`
