# ADR-0008: Hero Cosmetic Base-Priority & Layered Merge

**Date:** 2026-06-13
**Status:** Accepted
**Deciders:** @Anneardysa

## Problem Statement

The Skin Selector lets a user stack several cosmetic layers on one hero in a single generation pass — a **Base Hero**, a **Set / Custom Set / Persona**, and one or more individual **Items**. Each layer ships an `index.txt` whose KeyValues blocks (keyed by numeric item id) must be combined into the hero's single `items_game.txt` block per id.

Two earlier approaches both produced wrong results:

1. **Serializer deep-merge** combined every layer's sub-trees — crucially it accumulated multi-key `asset_modifier` entries from *both* a Base and a Set on the same item id, so their visuals stacked and clashed in-game.
2. **Exclusive override** (highest-priority layer claims an id and later layers are skipped) made a top-priority Base claim *every* slot it defined, so a specifically-selected Item (e.g. a custom arm/weapon) was dropped and never showed.

The base layer's priority is also not fixed: a true body replacement (an item that declares `"item_slot" "hero_base"`) belongs *under* the cosmetics, whereas a partial base (e.g. a weapon-only arcana) does not. This per-hero distinction was being inferred only by scanning the base's `index.txt`, which was fragile and not author-overridable.

## Decision Drivers

- **Correctness** — the most specific selection a user picks must show on its slot; non-overlapping slots from every layer must still apply; nothing silently dropped.
- **Per-hero base priority** — driven by whether the base owns the `hero_base` slot, and explicitly overridable per hero.
- **Structure preservation** — the authored `index.txt` block is applied verbatim (no lossy serializer round-trip).
- **Determinism** — the winning layer per id must be predictable and observable.

## Considered Alternatives

### Alternative 1: Layered last-writer-wins — Chosen

Apply selections as layers from foundation → top (`GetSortWeight` descending). Every layer's `index.txt` blocks are written; a later, lower layer overrides earlier ones for the **same** item id. Asset files overwrite in the same order.

- ✅ Good, because the most specific pick (Item) wins its slot while the Set fills the rest and the Base provides the body — matching user intent.
- ✅ Good, because every selected layer is parsed and applied; nothing is skipped.
- ✅ Good, because the winning block is the authored block applied verbatim (no `asset_modifier` stacking).
- ❌ Bad, because layers must be ordered correctly and the asset-file copy order must match the block order (they are coupled).

### Alternative 2: Serializer deep-merge

Deserialize both blocks and merge sub-trees / multi-keys.

- ✅ Good, because it can combine partial blocks.
- ❌ Bad, because it accumulates `asset_modifier` entries from multiple layers → visual clashes.
- ❌ Bad, because round-tripping through `KVSerializer` re-orders keys and leaks vanilla identity fields.

### Alternative 3: Exclusive first-writer-wins

Highest-priority layer claims an id; later layers for that id are skipped entirely.

- ✅ Good, because there is exactly one winner per id with no merge.
- ❌ Bad, because a top-priority Base claims every slot it defines, dropping specifically-selected Items/Sets.

## Decision

We will merge hero cosmetic layers with **layered last-writer-wins**, ordering layers by `GetSortWeight` (descending) where the base's rank is resolved per hero by `ResolveBaseWins(hero.Method, detectedHeroBase)` — the optional `heroes.json` `method` field overrides the VKV-aware `item_slot hero_base` auto-detection.

- `method = 1` (or `hero_base` detected) → `Base → Sets/Custom/Persona → Items`
- `method = 2` (or no `hero_base`) → `Sets/Custom/Persona → Items → Base`
- `Items` are always layered below `Sets/Custom/Persona`.

### Implementation

```csharp
// Resolve the base's rank: explicit heroes.json method wins; else item_slot hero_base detection.
internal static bool ResolveBaseWins(int? method, bool detectedHeroBase)
    => method == 1 ? true : method == 2 ? false : detectedHeroBase;

// Apply foundation -> top (highest weight first).
var orderedList = extractedList
    .OrderByDescending(x => GetSortWeight(x.category, hasHeroBaseSlot))
    .ToList();

foreach (var selection in orderedList)
{
    MergeSetAssetsAsync(...);                 // files overwrite (last layer wins on shared paths)
    var heroBlocks = _patcher.ParseIndexFile(contentRoot, hero.Id, hero.ItemIds, ...);
    foreach (var kvp in heroBlocks)
        mergedBlocks[kvp.Key] = kvp.Value;    // last-writer-wins: lower layer overrides for the same id
}
```

Each merged block is later applied **verbatim** onto vanilla `items_game.txt` via `KeyValuesBlockHelper.OverlayBlockPreservingStructure` (see `HeroSetPatcherService`).

## Consequences

### Positive

- ✅ A selected Item wins its slot; the Set fills remaining slots; the Base provides the body — nothing skipped.
- ✅ Base priority is author-controllable per hero (`heroes.json` `method`) with a robust VKV fallback.
- ✅ No `asset_modifier` stacking; authored blocks applied verbatim.
- ✅ Generation logs (`[DEBUG] Priority/Order/Override`) make the decision observable.

### Negative

- ❌ Block order and asset-file copy order are coupled and must stay consistent.
- ❌ `method` only takes effect once the `heroes.json` the app downloads (CDN/GitHub) actually contains it.

### Metrics

| Metric                                | Deep-merge | Exclusive override | Layered (chosen) |
| ------------------------------------- | ---------- | ------------------ | ---------------- |
| Same-id `asset_modifier` stacking     | Yes        | No                 | No               |
| Selected Item/Set dropped on conflict | No         | Yes                | No               |
| Author-overridable base priority      | No         | Partial            | Yes (`method`)   |

## Related

- `Core/Services/Hero/HeroGenerationService.cs` — `GetSortWeight`, `ResolveBaseWins`, layered merge
- `Core/Services/Hero/HeroSetPatcherService.cs` — verbatim overlay onto `items_game.txt`
- `Core/Helpers/KeyValuesBlockHelper.cs` — `AnyBlockHasItemSlot`, `TryGetTopLevelValue`, `OverlayBlockPreservingStructure`
- `Core/Models/HeroModel.cs` — per-hero `Method` field
- [ADR-0005: WebView2 Hybrid UI](./0005-webview2-hybrid-ui.md) — the Skin Selector gallery that produces the layered selections
