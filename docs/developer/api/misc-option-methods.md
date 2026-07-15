# Misc Option Methods — Reference

Which application method each misc category uses. The dispatcher is
`AssetModifierService.ApplyModificationsAsync` (`Core/Services/Misc/AssetModifierService.cs`);
each row's option `id` is the key in the `selections` dictionary and the option `id` in
`misc_config.json`.

Three things a method can do:
- **Block merge** — patch a `"<id>" { … }` block into `scripts/items/items_game.txt` (the "package").
- **Copy to root** — extract archive entries into the package tree (rebuilt into `pak01_dir.vpk`).
- **Game-VPK pull** — extract referenced models from the live `game/dota/pak01_dir.vpk`.

---

## Block mods — fixed item ID

`ApplyBlockModAsync`. Downloads one `.txt` block and replaces the block at a **hardcoded** item ID
via `KeyValuesBlockHelper.ReplaceIdBlock`. IDs live in `CategoryItemIds`.

| Option id | Item ID |
|---|---|
| `Weather` | 555 |
| `Map` | 590 |
| `Music` | 588 |
| `HUD` | 587 |
| `Versus` | 12970 |
| `RadiantCreep` | 660 |
| `DireCreep` | 661 |
| `RadiantSiege` | 34462 |
| `DireSiege` | 34463 |
| `RadiantTower` | 677 |
| `DireTower` | 678 |

## Block merge + game-VPK pull

| Option id | Method | Notes |
|---|---|---|
| `Courier` | `ApplyCourierModAsync` | Merges selected courier block into Default Courier (595), pulls courier models from game VPK → `models/props_gameplay`. ID may carry a style (`10746:1`). |
| `CourierEthereal` | (within `ApplyCourierModAsync`) | Sub-option of Courier — appends ethereal particle effects; comma-separated, applies to Default Courier too. |
| `Ward` | `ApplyWardModAsync` | Merges selected ward block into Default Ward (596), pulls ward models from game VPK. |

## Single-file mods

| Option id | Method | Destination | Disable choice |
|---|---|---|---|
| `Emblems` | `ApplyEmblemModAsync` | `particles/ui_mouseactions/selected_ring.vpcf_c` | `Disable Emblem` deletes the dir |
| `Shader` | `ApplyShaderModAsync` | `materials/dev/deferred_post_process.vmat_c` | `Disable Shader` deletes the dir |

## Archive → copy to root

Extracts every archive entry into the package tree — no items_game.txt change.

| Option id | Method |
|---|---|
| `River` | `ApplyRiverModAsync` → `DownloadAndExtractRarAsync` |
| `atkModifier` | `ApplyAtkModifierAsync` → `DownloadAndExtractRarAsync` |
| `Effect` | `ApplyEffectModAsync` → `DownloadAndExtractRarAsync` |
| `Special` | `ApplySpecialModAsync` → `DownloadAndExtractRarAsync` |
| `ancient` | `ApplyZipModAsync` (copyToRoot) |

## Block merge (ID from patch file)

`ApplyZipModAsync` with `mergeTxt`. The asset's standalone `.txt` is a `"<id>" { … }` block; the ID is
read from the block itself (`KeyValuesBlockHelper.ParseKvBlocks`) and the matching block in
items_game.txt is replaced (`ReplaceIdBlock`) — same outcome as the fixed-ID block mods, but no
hardcoded ID table. Mega-Kills and Announcer ship asset files in the same zip that are extracted to
the package root (`copyToRoot`).

| Option id | Merges block | Extracts files to root |
|---|---|---|
| `cursor` | ✓ | – |
| `kill_streak` | ✓ | – |
| `mega_kills` | ✓ | ✓ |
| `announcer` | ✓ | ✓ |
| `roshan` | ✓ | ✓ |

## Special VPK

Options with `"type": "vpk"` in `misc_config.json` (`MiscOption.IsSpecialVpk`) are **not** recompiled
into `pak01_dir.vpk` — the complete VPK is downloaded and installed as a separate pak file.

---

**Notes**
- New categories need no code in `ApplyModificationsAsync` beyond their `ApplyZipModAsync` wiring;
  unknown ids resolve their URL through `ModConfigurationData.GetUrl`'s `_ => category` fallthrough.
- "Default …"/"Disable …" choices are handled by the absence of a URL (skip) or a vanilla-block
  asset (restore), not by special-casing the name — except the single-file mods, which match the
  literal `Disable …` string to delete their folder.
