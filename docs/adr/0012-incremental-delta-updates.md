# ADR-0012: Incremental (Delta) App Updates

**Date:** 2026-07-14
**Status:** Accepted
**Deciders:** @Anneardysa
**Builds on:** [ADR-0003](0003-multi-cdn-strategy-r2-primary.md), [ADR-0009](0009-cdn-download-resilience-layer.md), [ADR-0010](0010-asset-hash-verification.md)

## Problem Statement

Every AMT update forced a **~70 MB installer re-download**, no matter how small the change — the
most common complaint from users on slow or metered connections. The app ships as a self-contained
.NET 8 publish tree (~171 MB, 529 files), and **167 MB of that is the .NET runtime, byte-identical
across releases**. Only the app assembly, its apphost and `Assets/` actually change: **3.9 MB across
47 files in the worst case** where every one of them changes at once.

The old auto-update pipeline was deleted in build 2231 (dead code, zero callers). Its warning stands:
it had an **opt-in** SHA-256 gate and a GitHub fallback that never populated a hash at all. Whatever
replaced it had to make that gate mandatory.

## Decision

Publish a **per-file SHA-256 manifest** per release and let an installed client download **only the
files whose content differs**, then apply them out-of-process.

### Manifest contract — `releases/<version>/files.json` (R2)

Deliberately the **same schema as `Assets/asset_hashes.json`** (ADR-0010), so the client reuses
`AssetHashManifestService.ParseManifest` and `AssetHashVerifier` unchanged:

```jsonc
{
  "version": 1,
  "algorithm": "SHA-256",
  "assets": {
    "ArdysaModsTools.dll":             { "sha256": "<UPPERCASE-HEX>", "size": 1324032 },
    "Assets/Locales/en.json":          { "sha256": "…", "size": 43859 },
    "tools/updater/AMT.Updater.exe":   { "sha256": "…", "size": 2965504 }
  }
}
```

Keys are install-root-relative, forward-slashed. Each file is served raw at
`releases/<version>/files/<key>`. `releases.json` gains one **additive** field per release,
`"files": "<manifest URL>"` — older clients ignore it, and its absence simply means "full download
only". Delta files exist on **R2 only**: the jsDelivr/GitHub mirrors carry ModsPack assets, not app
releases. A dead R2 means no delta, never a broken update.

### Client flow (`Core/Services/Update/DeltaUpdateService.cs`)

1. **Diff** — hash the install tree against the manifest. Missing or different ⇒ download.
   Deletions come from the *previous* version's manifest (`old keys − new keys`); when that manifest
   is gone (purged after 3 releases) the answer is "delete nothing", never a guess.
2. **Stage** — download each file into `%LocalAppData%\ArdysaModsTools\update\<version>\files\`,
   every one verified against the manifest hash by `ResumableDownloadService` (ADR-0009/0010).
   Only after **all** files verify are `apply.json` and then the `.staged-ok` marker written — a
   half-finished staging folder can never be applied.
3. **Apply** — launch the staged `AMT.Updater.exe` and exit. The app cannot replace its own running
   executable, so the swap must happen out-of-process.

### The applier — `Updater/AMT.Updater.csproj`

A standalone **Native AOT** exe (2.8 MB, no runtime dependency): it keeps running while the app's own
.NET runtime DLLs are being replaced, so it cannot depend on them. It re-verifies **every** staged
file's SHA-256 independently before touching anything — staging is user-writable, so "it verified at
download time" is not the same statement as "it is the file I am about to install". The app likewise
re-verifies the applier itself immediately before running it (it may run elevated).

Per file, the order is what makes it safe:

```
1. copy staged → target.amtnew    (a crash here leaves the original untouched)
2. rename target → target.amtbak  (metadata-only; allowed even for a loaded image)
3. rename target.amtnew → target  (metadata-only)
```

Any failure rolls **every** applied file back from its `.amtbak`, and the app is relaunched either
way — a failed update must never leave a user with no app. (The one exception: if the app never
exited, nothing was touched and it is still running, so the applier does *not* start a second copy.)

The only torn state is the sliver between (2) and (3), and the in-progress marker is what makes every
leftover unambiguous to the startup sweep (`RepairInterruptedUpdate`):

| | Target missing | Target present |
|---|---|---|
| **Marker present** (killed mid-swap) | restore from `.amtbak` | swap completed → drop the backup |
| **Marker absent** (apply finished) | the update deleted it on purpose → drop the backup | drop the backup |

So a backup the applier could not delete on the way out (an AV holding it open for a moment) is swept
on the next launch rather than living in the install folder forever, and a deliberately-deleted file
is never resurrected.

## Consequences

**Good**
- A typical update drops from ~70 MB to **1–4 MB**. The 167 MB runtime is never re-downloaded again.
- The integrity gate is **mandatory on both sides** — the failure mode the deleted pipeline warned about.
- Fully additive: no manifest, unreachable R2, no delta, or a failed apply all fall back to the
  full-installer links, which never leave the dialog.
- Portable installs get a working update path for the first time (same tree, no elevation needed).

**Bad / accepted**
- R2 stores the file tree per release (~171 MB × 3 kept releases ≈ 0.5 GB — pennies, and the existing
  3-release purge already covers it).
- Delta downloads have no CDN fallback (R2 only, by design — see above).
- The diff hashes ~171 MB of local files (≈1 s off the UI thread), only when an update exists.

## Alternatives rejected

- **Binary patching (bsdiff/courgette)**: would shrink a 1.3 MB DLL to ~200 KB, at the cost of a
  patch-generation pipeline and per-version-pair artifacts. Full-file replacement already captures
  ~95 % of the win.
- **Per-file gzip**: ~2.5× smaller downloads, but a typical delta is already 1–4 MB; not worth the
  compression step, the extra manifest fields, or the `Content-Encoding` interplay with Cloudflare.
  Revisit if a .NET runtime bump ever makes a delta ~167 MB.
- **Content-addressed store** (`releases/store/<sha256>`): dedupes the runtime across kept releases,
  but needs its own GC. The per-version tree is purged by the existing cleanup, unchanged.
- **Velopack / Squirrel**: would replace the whole Inno Setup + portable distribution model.
- **A batch/PowerShell applier**: no compile step, but retries, rollback and exit codes are miserable
  in cmd, and the rollback logic would be untestable. File safety outranks a smaller diff.
