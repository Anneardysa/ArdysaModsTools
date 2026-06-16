# Changelog

All notable changes to ArdysaModsTools will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.2.1-beta] (Build 2156)

### 🗑️ Removed

- Dropped the now-unused `item_namer` import, the `vpk_path` config, the `sets_renamed` result tracking, and the menu/CLI/dispatch wiring for the mode. The tool now offers modes 1–6.

---

## [2.2.1-beta] (Build 2155)

### 🐛 Fixed

- **Skin Selector → Latest Updates**: Styled sets (Style Cards) no longer go missing from the "Latest Updates" bar. The patch tool recorded a newly-added styled set under its bare group key (`Set N`), but the app flattens each style into a `Set N (Label)` entry — so the bar could never resolve a thumbnail for the bare key and silently hid the update. Patch Models now records the flattened representative key (`Set N (first style label)`) for styled sets, and a one-time idempotent migration repoints any pre-existing bare-group entries in `set_update.json` so currently-hidden styled updates reappear.

---

## [2.2.1-beta] (Build 2154)

### 🛠️ Changed

- **UI/UX**: In the Skin Selector hero modal, Legacy / Custom / Persona / Base cards no longer show the generic "Set N" name label. Each now displays a color-coded category tag badge instead — **Set** (Legacy), **Mix** (Custom), **Persona**, and **Arcana** (Base) — mirroring the slot tag badges already used on item cards. Item cards keep their name plus slot tag unchanged.

---

## [2.2.1-beta] (Build 2153)

### ✨ Added

- **UI/UX**: Added a Style Preview Modal in the Skin Selector. Clicking on a collapsed Style Card representative tile now opens a modal where users can preview, select, and deselect specific style variants (showing corresponding custom labels and thumbnails) before applying them.
- **UI/UX**: Added click-outside and `Escape` key handlers to close the Style Preview Modal.

### 🛠️ Changed

- **Mock Data**: Updated Crystal Maiden's test sets/items in `hero_gallery.html` to include styleGroup and styleLabel metadata to support layout and behavior testing.

---

## [2.2.0-beta] (Build 2152)

### ✨ Added

- **Skin Selector → Style Cards** — a set or item can now ship multiple **styles** (alternate visual variants, each with its own archive + thumbnail). Authored in `heroes.json` as a `{ "styles": { "<label>": [urls...] } }` object on a set (fully backward compatible with the existing array form), they collapse into a single **Style Card** with a horizontal style-chip row in the hero modal — picking a chip applies that style. Eligible across all categories (Legacy / Custom / Persona / Item / Base), with exactly one active style enforced per group. Each style is flattened into a normal set entry keyed `"{Group} ({Label})"`, so the download / patch / VPK pipeline is completely unchanged.

### 🛠️ Changed

- **`scripts/tools/2-patch_models.py`** now authors and maintains styled sets. A style group is a subfolder under the hero (`models/<hero>/<Group>/<Style>.zip`, images mirrored under `image/`); the scanner emits it as a `{ "styles": { ... } }` object keyed `Set N`. Every set-iterating operation — CDN URL migration, duplicate detection, image back-fill, deleted-file sync (per-style prune + empty-group drop), base-priority auto-detect, category overview, and the delete tool (with empty style-folder cleanup) — is now style-aware instead of silently skipping the object form.

---

## [2.2.0-beta] (Build 2151)

### 🐛 Fixed

- **CDN fallback 404 errors** — removed the `?v=YYYYMMDDHH` cache-busting query string from `BuildFreshUrl`. The parameter was structurally ineffective (R2 uses response headers for caching, jsDelivr caches by branch→commit resolution, GitHub Raw ignores query strings) and `ExtractAssetPath` propagated it into fallback URLs, causing `HTTP 404` on GitHub Raw and silently serving stale data from jsDelivr mirrors instead of the authoritative R2 origin.
- **R2 CDN not used as primary** — `SmartCdnSelector` now pins Cloudflare R2 at position 0 regardless of benchmark latency, since R2 is the authoritative origin (content is uploaded there first, always freshest). The latency benchmark previously ranked GitHub Raw first (66ms vs R2 86ms), so the app bypassed R2 entirely and fetched potentially-stale data from mirrors. Only fallback CDNs (jsDelivr, GitHub Raw, GFW proxies) are now reordered by speed; R2 can still be circuit-breaker-tripped if genuinely down.

### 🛠️ Changed

- **CDN path extraction** — `CdnConfig.ExtractAssetPath` now defensively strips query strings from the returned path, preventing any future `?param=` from polluting the fallback chain through `ConvertToCdn`.

---

## [2.2.0-beta] (Build 2150)

### ✨ Added

- **Settings → Hero Database** — a **Check Database** / **Update Database** pair that verifies the local hero data (`heroes.json`) against the live copy by **SHA-256** and force-updates it on demand, with a status line (set count • last updated • source: live/manual/bundled). Lets users on impaired or region-blocked connections self-fix missing/duplicated Skin Selector thumbnails without reinstalling.
- **Persistent hero-database cache** — a successfully-downloaded `heroes.json` is now saved to `%LocalAppData%\ArdysaModsTools\data\` with a SHA-256/ETag meta sidecar (`ManifestCache`), and preferred over the stale bundled snapshot on later launches.

### 🐛 Fixed

- **Skin Selector "Latest Updates" showed the same image on every card** for some users. Root cause: their `heroes.json` was stale relative to the live `set_update.json` feed (a slow/blocked CDN made the large file time out and fall back to the snapshot bundled in their installed version), so the carousel couldn't resolve the newly-added sets and the per-card `onerror` collapsed every card onto the same hero portrait. The carousel now renders **only** updates whose set resolves to a real, distinct thumbnail in the loaded data; the persistent cache above keeps the two manifests in sync; and the card `onerror` shows a neutral placeholder instead of the hero portrait.
- **Misleading "asset cache ready"** — the launch console now reports a partial result (e.g. `412/435 (23 unavailable)`) when thumbnails failed to download, instead of an unconditional "ready".

### 🛠️ Changed

- **`heroes.json` loading** now falls back CDN → persisted last-known-good → bundled (previously CDN → bundled), capturing ETag/Last-Modified for freshness checks.
- **Intercepted thumbnail fetches** are bounded (45 s) so a single slow/blocked CDN can no longer hold a WebView2 request open and make the gallery appear to hang loading thumbnails.

---

## [2.2.0-beta] (Build 2149)

### ✨ Added

- **About dialog** — a new title-bar button (beside Settings) opens a minimalist WebView2 About page with the app identity, a short description, and Credits & Acknowledgments (author, community, third-party libraries, license).
- **What's New chooser** — the card opens a modal to pick **Changelog** (in-app GitHub releases) or **ModsPack** (searchable, attribute-filterable hero-skin updates grid from the site).
- **ModsPack preview lightbox** — clicking any hero card in the ModsPack updates grid opens a full-screen preview modal showing the image full-colour and larger, with left/right carousel navigation (and arrow-key support) through the currently filtered set, a hero/attribute caption, a position counter, and click-backdrop/Esc to close.
- **Version badges** on the What's New card — live app version + ModsPack version (served from R2 `config/banner.json`).
- **Banner carousel** on the main shell, sourced from the R2 manifest.
- **Install Method** and **Disable Options** rebuilt as native WebView2 dialogs.

### 🐛 Fixed

- **Patch Update button** no longer spams `[STATUS] Ready: ...` to the console on every click — status logging is now driven by an actual status change, decoupled from the forced (cache-bypassing) refresh.
- **Apply settings** no longer fails with "Dota 2 cfg folder not found" — resolves `game\dota\cfg` correctly and creates it on a fresh install.
- **Onboarding guide** now works on the WebView2 shell (DOM-native overlay).
- **Card fonts** — promo cards and a few stray controls now render in JetBrains Mono.

### 🛠️ Changed

- **Monochrome redesign** of the main shell and Performance Tweaks page to match [ardysamods.my.id](https://ardysamods.my.id/) (black/white, JetBrains Mono, sharp corners).
- **Layout** — Disable Mods and Performance Tweak moved into the sidebar; Install ModsPack is now an image card.
- **Performance Tweak** is now gated behind path detection (Auto/Manual Detect), matching the other mod tools — the button stays disabled until a Dota 2 install is known, since `autoexec.cfg` cannot be resolved without it.
- **Notifications** — the "no autoexec.cfg" notice is a persistent banner; toasts are high-contrast status cards.

> **Deploy:** add `"modspackVersion": "2.6"` to R2 `config/banner.json`.

---

## [2.1.27-beta] (Build 2148)

### ✨ Added

- **Main Window**: The main window is now a WebView2 hybrid shell (`MainFormWebView` + `Assets/Html/main_shell.html`), matching the rest of the app (Hero Gallery, Miscellaneous, Settings). The interface was redesigned for a more compact, breathable layout at a slightly larger fixed size (920×640): a clean title bar (Tweak / Settings / Minimize / Close), a left sidebar grouped into **Detect Path**, **Mods**, and **Tools** with a dedicated status row, social links, and version footer, and a content column with the banner, Install / Disable actions, a collapsible "close Dota 2" warning, and a modernized console. Improved hierarchy, padding, and color grading (cyan accent on near-black) with no overlapping controls. The Patch Update button keeps its status-driven accent (ready/green, update/orange, error/red) and opens its menu (Patch / Verify / View Status) as an in-page dropdown. The console drops the scanline/glow treatment for a cleaner, category-colored log (success/error/warning/progress/default), kept in sync with the existing terminal classifier.

- The shell visual language follows `DESIGN.md` (near-black `#101010` canvas, single electric-green `#00d992` accent, Inter for UI text with a monospace console, 1px hairline borders, 6px/8px radii).

### 🐛 Fixed

- **Settings / Tweaks / Support crashed (or rendered blank) from the main shell**: Two issues. (1) The main shell keeps a WebView2 alive for the whole session, so a dialog opened on top created a **second** `CoreWebView2Environment` rooted at the same persistent user-data folder — unsupported by WebView2, leaving the child blank. `WebView2EnvironmentHelper` now creates the environment **once** and shares that single instance across every WebView2 control in the process (the supported pattern), with retry if the first creation faults. (2) The sidebar/title-bar buttons opened those modal dialogs **synchronously inside the `WebMessageReceived` callback**, spinning a nested message loop (and a child WebView2) while still inside the WebView2 event handler — a reentrancy violation that hard-crashed the process (`STATUS_BREAKPOINT 0x80000003`). `MainFormWebView` now defers message handling onto a fresh message-loop turn via `BeginInvoke`, so dialogs open on a clean stack. Verified (Settings / Support / Performance Tweaks) via UI Automation.

### 🛠️ Changed

- **Startup / Resilience**: The shell is chosen at launch by `MainFormFactory` — the WebView2 shell when the Edge WebView2 runtime is available, otherwise the classic WinForms `MainForm` (kept intact as a fallback). The MVP boundary is unchanged: `MainFormWebView` implements the same `IMainFormView` contract and owns the same `MainFormPresenter`, so detection, install/disable, patching, status monitoring, and all dialogs behave identically. `Logger` gained a WebView log sink so console output streams into the page (buffered until the page is ready), and `IMainFormFactory.Create` now returns `Form` to allow the runtime shell choice.

---

## [2.1.27-beta] (Build 2147)

### 🐛 Fixed

- **Dota 2 Performance / autoexec.cfg**: `ApplySettingsAsync` no longer rolls the file transaction back twice on failure. `IFileTransaction.ExecuteAsync` already rolls back internally, so the extra manual `RollbackAsync` ran the rollback a second time; the failure is now just logged and surfaced. Additionally, when an explicit game path is supplied but contains no `cfg` folder, the resolver no longer silently falls back to the default `Program Files (x86)\Steam\…\dota 2 beta\…\cfg` install — which could write `autoexec.cfg` into an unrelated Dota 2 installation. An explicit-but-invalid path now reports not-found (Apply throws) instead.
- **Dota 2 Performance**: Toast notifications are now awaited (`ShowToastAsync`) rather than fire-and-forget `async void`, so load/apply/export messages can't be lost or race the next navigation. WebView2 bridge payloads (settings JSON, toast text) are passed as `JsonSerializer`-encoded literals so quotes, newlines, and U+2028/U+2029 line separators are escaped safely instead of breaking the injected JS string, and bridge-handler failures are now logged via `IAppLogger` instead of `Debug.WriteLine`. The "no autoexec.cfg" message now states that a recommended preset is shown (matching the grid) rather than claiming raw defaults.

### 🛠️ Changed

- **Dota 2 Performance / Launch Options**: Your launch-options selection (enabled flags + custom entries) is now remembered across sessions via `localStorage` in the persistent WebView2 user-data folder. The panel also makes explicit that launch options are copy-only — they are **not** written by [ APPLY ], since the app cannot edit Steam's launch options.

---

## [2.1.27-beta] (Build 2146)

### 🐛 Fixed

- **Miscellaneous / Skin Selector**: Fixed a request storm when opening a panel that contains thumbnails which don't exist on the CDN (e.g. Crownfall, Woodland Warbands, a few Battlepass screens). The WebView interceptor was calling the cache for every `<img>` request — including known-missing ones and the browser's `.jpg`/`.jpeg` alt-format probes — and each ran the full 5-CDN fallback chain, producing a flood of `HTTP 404` logs on every open. Three fixes: (1) the interceptor now answers a known-missing asset with an instant `404` (no network, no CDN chain) so the browser just shows the placeholder; (2) `AssetCacheService.GetAssetBytesAsync` short-circuits known-missing URLs before any download; (3) most importantly, a definitive **404/403 no longer trips the CDN circuit breaker** — a missing file isn't an unhealthy CDN, so real downloads are no longer degraded for 120s — and the fallback chain now stops after one pass when every CDN reports not-found.

---

## [2.1.27-beta] (Build 2145)

### ✨ Added

- **Launching State**: a new background asset preloader that downloads all gallery thumbnails (misc choices + hero set thumbnails) into the persistent local cache (`%LocalAppData%\ArdysaModsTools\AssetCache`) right after the app window opens — so Miscellaneous and Skin Selector open instantly and work offline. It runs non-blocking and throttled (4 concurrent), skips assets already cached or known-missing (so repeat launches are effectively instant), reports concise progress in the console ("Launching State: caching assets… 240/1919"), and cancels cleanly on app close. New `IAssetPreloadService`/`AssetPreloadService` (DI singleton) reuses the existing `AssetCacheService` download pipeline and enumerates URLs from the misc config + `heroes.json`.
- **Settings**: new "Preload assets on launch" toggle (default ON). Turning it on mid-session warms the cache immediately; turning it off skips the preload on future launches.

### 🛠️ Changed

- **Settings / Clear Cache**: after a successful Clear Cache, the Launching State preload re-arms automatically so the cache refills without needing a restart (when the toggle is on). `AssetCacheService.ClearCache()` now also resets the batch-refresh cooldown (deletes `.last_refresh`) so freshness checks aren't blocked after a manual clear. Confirmed Clear Cache fully removes preloaded assets.
- **Tests**: added `AssetPreloadServiceTests` (URL enumeration: skips Default/Disable choices, picks the first image per hero set, de-dupes, merges sources).

---

## [2.1.27-beta] (Build 2144)

### 🐛 Fixed

- **Miscellaneous**: Fixed the thumbnail overlay ("Downloading 0/70…") re-appearing on every open. Two causes: (1) **Sanitization mismatch** — `MiscOption.GetThumbnailUrl` kept apostrophes/commas/`&` and converted hyphens to underscores, but the CDN filenames (and the browser's `getThumbUrl` JS) strip those and keep hyphens. So special-character Courier/Ward thumbnails resolved to 404 URLs the C# preload could never cache — they always looked "missing" even when the browser had already cached them. The C# sanitization (new `MiscOption.SanitizeChoice`) now matches the CDN/JS convention exactly (verified against the live CDN). (2) **No memory of absent assets** — the ~30 choices that legitimately have no CDN thumbnail (e.g. "Default …"/"Disable …" plus a few not-yet-uploaded cosmetics) were re-attempted on every open, re-showing the overlay and triggering a slow CDN retry-storm.
- **Skin Selector**: Same not-found handling applied to Hero Gallery set thumbnails.

### ⚡ Performance

- **Miscellaneous**: The "Default …"/"Disable …" no-op choice of every option (the unmodded/disabled state) no longer requests a CDN thumbnail at all — it renders the built-in placeholder instantly. This removes ~17 needless image requests/404s per open for a faster, quieter load. Implemented as a synced contract: `MiscOption.IsDefaultChoice` (C#) and `isDefaultChoice()` (`misc_form.html`).

### 🛠️ Changed

- **Core**: `AssetCacheService` now persists a known-missing set (`.missing_assets`, keyed by URL with a 7-day TTL). Definitive not-found responses (HTTP 404/403) are recorded — transient/offline failures are not — and a successful download, `Invalidate`, or `Clear Cache` clears the marker. The misc/hero preload skips known-missing URLs, so the overlay only appears for genuinely new thumbnails and absent ones are silently re-checked at most once a week.
- **Tests**: Added `MiscOptionTests` (sanitization parity with the CDN, incl. hyphen/apostrophe/comma/`&`/curly-quote cases) and `AssetCacheServiceTests` (known-missing lookups).

---

## [2.1.27-beta] (Build 2143)

### 🐛 Fixed

- **Miscellaneous / Skin Selector**: Fixed thumbnails re-downloading from the CDN on every open after the app was closed. The gallery `<img>` tags pointed straight at CDN URLs, so the embedded browser fetched each thumbnail from the network and cached it only in WebView2's user-data folder — which lived in `%TEMP%` and was wiped by Windows cleanup (Storage Sense / Disk Cleanup) and on every app update. Meanwhile the persistent `AssetCacheService` (in `%LocalAppData%`) downloaded the same images but its bytes only gated the "Downloading thumbnails…" overlay and were never shown. The two caches are now unified: a new `WebViewAssetInterceptor` (via `CoreWebView2.WebResourceRequested`) serves CDN image requests from the persistent asset cache, so thumbnails download once and survive restarts, temp cleanup, and updates (with graceful network fallback when an asset isn't cached). Applied to both `MiscFormWebView` and `HeroGalleryForm`.

### 🛠️ Changed

- **Core / Stability**: The WebView2 user-data folder moved from `%TEMP%\ArdysaModsTools.WebView2` to a persistent `%LocalAppData%\ArdysaModsTools\WebView2`, centralized behind a new `WebView2EnvironmentHelper.CreateEnvironmentAsync()` and adopted by all 12 WebView2 forms (the old per-form temp-path duplication is gone). The legacy temp folder is cleaned up once on first run. `UpdaterService` and `CacheCleaningService` were updated to target the new location (Settings → Clear Cache still frees it; updates still refresh the browser cache without nuking thumbnails, which now live in the separate asset cache).
- **Tests**: Added `WebViewAssetInterceptorTests` (13 tests) covering content-type resolution (extensions, query strings, case-insensitivity, unknown → octet-stream) and the request-filter builder.

---

## [2.1.27-beta] (Build 2142)

### ✨ Added

- **Skin Selector / Miscellaneous**: `items_game.txt` is now sourced live from your detected Dota 2 install on every generation instead of from the bundled `Original.zip`. A new `GameItemsGameExtractorService` extracts `scripts/items/items_game.txt` from `…/game/dota/pak01_dir.vpk` (via HLExtract, the same proven path the courier/ward extraction uses) and injects it into the extracted base before patching. This keeps mods aligned with the current game patch automatically — no more manual `Original.zip` rebuilds — and guarantees a clean (never stale/pre-patched) base each run. Wired into both Original.zip-based flows: `HeroGenerationService` and `MiscCleanGenerationService` (_Add to Current_ is intentionally excluded, as it reuses the already-patched mods VPK).

### 🛠️ Changed

- **Core**: `Original.zip` no longer needs to carry `items_game.txt` (shrinks the CDN download). `VpkExtractorService.ExtractAsync` gained an optional `requireItemsGame` flag (default `true`; _Add to Current_ unchanged), and `OriginalVpkService` switched its cache-validity sentinel from `items_game.txt` to a `.ready` marker kept beside the extracted folder so it is never packed into the mod VPK. If the game `items_game.txt` cannot be read (game files missing or path not detected), generation now aborts with a clear _"Re-run Detect"_ message rather than silently using a stale copy.
- **Tests**: Added `GameItemsGameExtractorServiceTests` covering the guard paths (empty target/extract path, missing game VPK).

---

## [2.1.27-beta] (Build 2141)

### 🐛 Fixed

- **Miscellaneous / Skin Selector**: Fixed user-cancelled generation being reported as a failure. Services returned the message `"Canceled by user."` while the forms compared against `"Operation cancelled by user."` (different spelling/wording), so cancelling popped a _"Generation Failed"_ dialog. Replaced the magic-string compare with an explicit `OperationResult.WasCanceled` flag, set by all generation services and `ProgressOperationRunner` and honoured by `MiscForm`, `MiscFormWebView`, `SelectHero`, and `HeroGalleryForm`.
- **Miscellaneous**: Fixed the WebView UI (`MiscFormWebView`) silently failing on critical mod conflicts. It never inspected `RequiresConflictResolution`, so a critical conflict fell through to a generic error with no way to resolve it. The WebView host now shows the `ConflictResolutionDialog` + retry loop, matching the classic `MiscForm`.
- **Miscellaneous**: Fixed conflict resolution being a no-op on the generated VPK. `MiscController` detected and "resolved" conflicts but always generated from the original selection set, so the losing mod was still written (and a critical conflict could retry indefinitely). Resolution outcomes now feed back into the selection set via `ApplyResolutionsToSelections` — the losing selection is dropped (unless it won another conflict) for both auto-resolve and user-resolve paths.
- **Miscellaneous**: Fixed _Add to Current_ mode fully extracting `pak01_dir.vpk` (potentially several GB) just to test for the `version/_ArdysaMods` signature file. `VerifyExistingVpkAsync` now reuses `IModInstallerService.ValidateVpkAsync`, which lists the VPK index (`HLExtract -l`) without extracting — removing the redundant extraction and its temp directory.
- **Miscellaneous**: Fixed `CancellationTokenSource` leaks — `MiscFormWebView._generationCts` is now disposed after each run and on form dispose, and `MiscForm`'s per-generation source is scoped with `using`.

### 🛠️ Changed

- **Core**: `MiscController.ApplyConflictResolutionsAsync` now takes the current selections and returns the adjusted set alongside the result, so callers retry generation with the losing mods removed.
- **Core**: `MiscController` now depends on `IModInstallerService` for VPK signature verification (injectable; defaults preserved for the parameterless constructor).
- **Tests**: Added `MiscControllerResolutionTests` covering the resolution→selection mapping (loser dropped, winner/unrelated retained; failed resolution drops nothing).

---

## [2.1.27-beta] (Build 2140)

### 🐛 Fixed

- **Skin Selector**: Fixed generation reading a stale side-channel selection field. `HeroGalleryForm` ignored the `selections` payload on the `generate` message and instead trusted whatever the last `selectionChanged` message had cached — any missed update could generate (and save as a preset) the wrong skins. The generate handler now parses the payload as the authoritative snapshot.
- **Skin Selector**: Fixed the "Base Hero without a set" confirmation being able to hang generation forever. The `await` on the JS callback had no timeout (unlike the success alert), so a missing/failed `baseNoSetConfirmed` callback left the operation stuck and the Generate button permanently disabled. Bridge confirmation/alert waits now share a bounded 60s timeout.
- **Skin Selector**: Hardened selection-index handling — a negative ("deselect") or out-of-range index from the web UI is now ignored instead of risking an `IndexOutOfRangeException`, and a set assigned to more than one slot is de-duplicated so it is no longer downloaded and merged twice for an identical result.

### 🛠️ Changed

- **Skin Selector / MVP**: Extracted the generation flow out of `HeroGalleryForm` into a new `HeroGalleryPresenter` (`IHeroGalleryView`). The form is now a thin WebView2 host; generation orchestration, validation, and result mapping are testable in isolation. Removed the misleading "priority-ordered" selection list from the form — layer priority is resolved downstream in `HeroGenerationService` (category + heroes.json `method`), not by the form's list order.
- **Tests**: Added `HeroGalleryPresenterTests` (19 tests) covering plan building (bounds, de-dup, base-without-set) and the generate flow (no-selection, decline, no-path, default-only, preview-decline, success, failure, cancel, re-entrancy).

---

## [2.1.27-beta] (Build 2139)

### 🐛 Fixed

- **MainForm**: Fixed graceful shutdown never triggering on close. `MainFormPresenter.IsOperationRunning` was backed by `_ongoingOperationTask`, a field that was never assigned, so it was always `false` — the window could close mid-install/patch without cancelling or waiting for the in-flight file operation. Operation state is now tracked via `_operationCts` + an `_operationGate` (`TaskCompletionSource`), so `ShutdownAsync` actually awaits the running operation (bounded by the existing 5s timeout) before closing.
- **MainForm**: Fixed a resource leak — the presenter (and therefore the Dota 2 patch watcher's `FileSystemWatcher`, the process monitor, and the operation `CancellationTokenSource`) plus `TrayService` were never disposed on a normal close, because disposal was gated behind the always-false `IsOperationRunning`. Added `MainForm_FormClosed` to dispose them unconditionally (idempotent).
- **MainForm**: Fixed `Form1_Load` reporting presenter/CDN initialization failures as _"Error loading social media icons"_ and swallowing them — presenter init now has isolated error handling and icon loading moved to a best-effort `LoadSocialMediaIcons()`.
- **MainForm**: Fixed `HandlePatcherClickAsync` acting on a cached status despite its "force refresh" comment — it now refreshes with `force: true` before deciding to prompt or show the menu.
- **MainForm**: Fixed the `--update` self-replace cleanup surfacing its error dialog and exiting from a background thread — the error path now marshals to the UI thread.

### 🛠️ Changed

- **MainForm / MVP**: Consolidated Dota 2 process monitoring into `MainFormPresenter` as the single owner. The form previously started a _second_ `Dota2Monitor` with its own handler and duplicate status checks; it now only reflects state through the new `IMainFormView.SetDotaRunningState` callback. Removed the form's duplicate `Dota2Monitor`, `StatusService`, `DotaStateChanged`, and `CheckModsStatus`.
- **MainForm**: Removed dead code (`MainFormPresenter.HandlePatchButtonClickAsync`, no-op `OnPaint`/`WndProc` overrides) and extracted the `--update` handshake into `RunPendingUpdateCleanup()`.
- **Tests**: Added `MainFormPresenter` shutdown regression tests (idle `ShutdownAsync` completes promptly and disposes; safe without a prior operation). Full suite: 582 passing.

---

## [2.1.27-beta] (Build 2138)

### ✨ Added

- **Security**: Added end-to-end **SHA-256 content verification** for downloaded assets (ADR-0010). A server-published manifest (`Assets/asset_hashes.json`, `assetPath → { sha256, size }`) is fetched via the resilient CDN pipeline and cached; hero set zips (single + merged split), `Original.zip`, and the app installer/portable are verified before extraction, install, or launch.
- **Core**: Added `AssetHashEntry`, `AssetHashVerifier` (streamed SHA-256, uppercase hex), and `AssetHashManifestService` (singleton fetch + 10-min cache, returns null when the manifest/asset is absent) under `Core/Services/Cdn/`.
- **Update**: Added optional `installerSha256` / `portableSha256` fields to `releases.json` (`UpdateInfo`); the installer/portable download is verified before applying, and discarded on mismatch.
- **Tooling**: Added `scripts/generate-asset-hashes.ps1` to generate the manifest from the ModsPack asset tree (concatenating split parts to hash the merged archive); run before `sync-to-r2.ps1`.
- **Documentation**: Added [ADR-0010](docs/adr/0010-asset-hash-verification.md) and updated ADR-0009's future-work note to reference it.

### 🛠️ Changed

- **Download**: `ResumableDownloadService.DownloadAsync` accepts an optional expected hash and verifies the completed `.partial` before promotion — a mismatch is rejected and the next CDN is tried; if all CDNs fail verification it throws `DownloadException` with `DL_HASH_MISMATCH`.
- **Download**: `HeroSetDownloaderService` resolves the expected hash by asset path and verifies merged split archives and cached files before extraction; a manifest hash change now also invalidates a stale cached zip. Assets absent from the manifest fall back to the existing size-only check (backward compatible).

---

## [2.1.27-beta] (Build 2137)

### ✨ Added

- **Core**: Added `DownloadRetryPolicy` (`Core/Services/Cdn/DownloadRetryPolicy.cs`) — a shared, status-aware retry policy for the CDN pipeline with exponential backoff + jitter, a capped `Retry-After` honour for 429/503, and transient-vs-permanent classification (a 404 falls through to the next CDN; a 503/429/socket error is retried). Reuses `RetryHelper.IsTransientStatusCode`.
- **Core**: Added a session **circuit breaker** to `SmartCdnSelector` (`ReportFailure`/`ReportSuccess`): after `CdnFailureThreshold` consecutive failures a CDN is deprioritized to the end of the order for `CdnCooldownSeconds`, then auto-restored — never dropped, preserving the ADR-0003 fallback completeness.
- **Core**: Added resilience tunables to `CdnConfig` (`RetryBaseDelayMs`, `RetryMaxDelayMs`, `MaxRetryAfterSeconds`, `ChainRetryPasses`, `CdnFailureThreshold`, `CdnCooldownSeconds`) and an `ExtractBaseUrl` helper for keying CDN health by base URL.
- **Documentation**: Added [ADR-0009](docs/adr/0009-cdn-download-resilience-layer.md) (CDN Download Resilience Layer) amending ADR-0003.

### 🛠️ Changed

- **Download**: `CdnFallbackService` and `ResumableDownloadService` now retry each CDN for transient failures (`MaxRetryPerCdn`, previously unused) before falling through, and sweep the whole chain up to `ChainRetryPasses` times with backoff before giving up. Both report success/failure to the circuit breaker (keyed by base URL).
- **Download**: Added size-only integrity validation across all paths — empty bodies and payloads whose length ≠ declared `Content-Length` are rejected (per file, and per split-archive part in `HeroSetDownloaderService`).
- **CDN selection**: `SmartCdnSelector` now confirms reachability via the GET speed test instead of trusting the HEAD probe, so it honours benchmark/circuit-breaker order without burying CDNs.

### 🐛 Fixed

- **CDN selection**: Fixed CDNs (notably the ghfast.top / gh-proxy.com GFW mirrors and occasionally jsDelivr) being wrongly marked UNREACHABLE and buried for 6 hours when they returned a non-2xx response to a `HEAD` request while serving `GET` fine — penalising exactly the region-blocked users the proxies exist for.
- **Download**: Fixed transient network blips (timeouts, 5xx, 429, connection/TLS resets) immediately failing a download instead of being retried, and a dead/blocked CDN being retried first on every asset.
- **GitHub**: Fixed an expired/invalid `GITHUB_TOKEN` killing the entire GitHub CDN tier — `GitHubTokenHandler` now replays the request once anonymously on a 401/403 (the ModsPack repo is public), while still never leaking the token to third-party CDNs.
- **Split archives**: Fixed a latent corruption risk where a part that failed mid-stream could be re-appended to the merge on retry/fallback; each attempt now truncates the merge stream back to the part boundary.

---

## [2.1.27-beta] (Build 2136)

### ✨ Added

- **Core**: Added an optional per-hero `method` field to `heroes.json` (`1` = Base first, `2` = Base last) that explicitly drives base-priority during generation. It is read through the hero load chain (`HeroService` → `HeroSummary` → `HeroModelMapper` → `HeroModel`) and **overrides** the `item_slot hero_base` auto-detection when present (`ResolveBaseWins`); absent → auto-detection as before.
- **Core**: Added VKV-aware, top-level `item_slot` detection (`KeyValuesBlockHelper.TryGetTopLevelValue` / `AnyBlockHasItemSlot`) so `hero_base` is only matched as a real top-level key on an item block, never inside nested `visuals`/`asset_modifier` sub-blocks.
- **Core**: Added `Persona` to `SkinCategory` enum for handling full hero model replacement mods (`persona_` prefix).
- **Core**: Added `ExtractItemTag()` to parse slot tags from item archives (e.g., extracting "shoulder" from `item_shoulder_pauldrons_1.zip`).
- **Model**: Added `PersonaIndex` to `HeroSelectionState` to support persisting and generating persona selections.
- **UI**: Added a 5th "Persona" section to the Hero Skin Selection modal with a distinct magenta/pink theme and glowing selection state.
- **UI**: Added a professional "Cosmetic Compatibility Alert" confirmation prompt when a Base Hero modification is selected without a corresponding Set to warn about potential visual conflicts.
- **UI**: Item tiles now display their slot tags as small green badges (`[shoulder]`, `[weapon]`, etc.).
- **Tooling**: Added Mode 6 to `2-patch_models.py` to set a hero's base-priority `method` — manually (`--set-method`) or auto-detected from each base set's `index.txt` (`--auto-detect`, single hero via `--hero` or all heroes).
- **Tooling**: Updated `2-patch_models.py` to classify and summarize `persona_` archives and display extracted item tags in Mode 4 overview.
- **Generator**: Added generation diagnostics (priority source/method, layer order, per-id override, and a "0 patchable blocks" warning) to trace merge behavior in the Visual Studio Output window.

### 🛠️ Changed

- **Generator**: Reworked merging into a layered **last-writer-wins** pipeline. Selections apply foundation→top (Base → Sets/Custom/Persona → Items, by descending sort weight); **every** selection's `index.txt` blocks are written into `items_game.txt`, and a later, lower layer overrides earlier ones for the same item id — so the most specific pick (e.g. a selected Item) wins its slot while non-overlapping slots from every layer still apply (nothing skipped). Asset files overwrite in the same order. `Items` are always layered below `Sets/Custom/Persona`. This supersedes the earlier per-id deep-merge/replace approach.
- **Generator**: Reordered the merging priority pipeline to apply selections in sequence: Sets/Custom Sets/Personas (lowest) → Selected Items (middle) → Base Hero (highest), ensuring custom base models correctly override set and item assets.
- **Patcher**: Replaced the items_game.txt block patcher with a structure-preserving overlay (`KeyValuesBlockHelper.OverlayBlockPreservingStructure`) that applies the custom `index.txt` block verbatim and carries over only essential base keys the index omits, instead of round-tripping both blocks through the KeyValues serializer.
- **Architecture**: Refactored `Dota2PerformanceForm` to adhere to the MVP pattern. Decoupled business logic into `AutoexecService` and UI state orchestration into `Dota2PerformancePresenter`.
- **Security**: Upgraded `autoexec.cfg` saving to use `FileTransactionService` ensuring atomic writes and automatic rollback on failure.
- **UI**: Resized the Support Dialog panel to a more compact, reliable size (760x460) with no scrollbars, optimized CSS spacing/typography tokens for maximum legibility, and added unique branding hover glow effects to each donation card.
- **UI**: Changed the font style of the main and miscellaneous consoles from JetBrains Mono Bold to Regular for cleaner terminal styling.
- **UI**: Replaced the text-based Tweak and unicode gear (⚙) buttons in the MainForm header with custom, high-visibility minimalist vector icons drawn natively via GDI+ paths, aligning them geometrically with minimize/close buttons.
- **UI**: Resized and compacted the Settings page layout to fit perfectly inside the 420x540 container, completely eliminating the need for vertical scrollbars, and added a 1px border frame around the page.
- **UI**: Implemented tag-based mutual exclusion for Items — selecting an item automatically deselects any currently active item sharing the same slot tag.
- **UI**: Implemented Persona mutual exclusion — selecting a Persona automatically clears and disables all Items and Base Hero selections, as Personas act as full model replacements.
- **Documentation**: Re-aligned all 7 Architecture Decision Records (ADRs) to perfectly reflect the actual .NET 8 codebase patterns, file paths, interop method names, multi-CDN fallback proxies, and WebView2 templates.
- **Documentation**: Added [ADR-0008](docs/adr/0008-hero-cosmetic-priority-merge.md) (Hero Cosmetic Base-Priority & Layered Merge) and updated the helpers/models API references and the "Generate Hero Cosmetics" skill to document the per-hero `method` field, `KeyValuesBlockHelper.TryGetTopLevelValue`/`AnyBlockHasItemSlot`, and the layered last-writer-wins merge.

### 🐛 Fixed

- **Generator**: Fixed selected Sets/Items being dropped when a higher-priority Base claimed the same item ids (and a related regression that copied 0 asset files). Every selected layer is now parsed and applied into `items_game.txt`.
- **Generator**: Fixed inverted layer sort weights in `HeroGenerationService` causing incorrect merge priority. Priority now correctly enforces Base > Sets > Items (when `hero_base` is present) and Sets > Items > Base (otherwise), resolving visual bugs.
- **Generator**: Fixed an issue where merging multiple mods with the same item ID (e.g. Base Hero replacements and Sets) would simply overwrite the ID block, causing important fields like 'used_by_heroes' or multiple 'asset_modifier' dummy objects to be stripped. Implemented deep merging logic for KeyValues blocks (e.g., `visuals` fields, `portraits` hierarchies, and `asset_modifier` tracking) to preserve nested data instead of replacing sub-trees.
- **Patcher**: Fixed `items_game.txt` generation so the authored `index.txt` block is applied **verbatim** — preserving its exact key order, values, nested structure, and numbered modifiers (`asset_modifier25/27/28`). Only structurally essential vanilla keys the index omits (`used_by_heroes`, `hero_presets`, `item_slot`, `prefab`) are now carried over; cosmetic vanilla keys are dropped.
- **UI**: Fixed Settings form top-right close ("✕") button not working due to mouse capture during drag initialization (`onmousedown`) swallowing the click event.
- **UI**: Increased WebView2 navigation timeouts in `ProgressOverlay` and `HeroGalleryForm` to 15 seconds to prevent installation and loading timeouts on slower client machines.

---

## [2.1.25-beta] (Build 2131)

### ✨ Added

- **UI**: Added a 4-section layout in Hero Skin Selection modal (Legacy Sets, Custom Sets, Items, Base Heroes) with custom category styling.
- **Model**: Added `HeroSelectionState` model to represent structured selections (Sets, Items, and Base Heroes) with JSON serialization.
- **Core**: Added `SkinCategory` enum and classification utilities to categorize sets based on filename prefixes (`mix_`, `item_`, `base_`).
- **Core**: Added support for mapping and tracking Base Hero assets under the `hero_base` item slot.
- **Tooling**: Added Mode 4 (Category Overview) to the Python model patching script (`2-patch_models.py`) to summarize hero assets by category in terminal output.

### 🛠️ Changed

- **UI**: Implemented selection exclusivity logic: Set (Legacy/Custom) mutual exclusion, single-select Base Heroes, and multi-select Items.
- **Generator**: Re-engineered the mod generation pipeline into a 3-tier priority merging pipeline applying selections in order: Set → Items → Base Hero.
- **Persistence**: Simplified selection saving to persist only highlighted hero IDs, while providing backward compatibility parsing for older selection schemas.
- **Refactor**: Standardized category classification logic across C# (`HeroModelMapper.cs`) and Python (`2-patch_models.py`) using filename prefixes.

---

## [2.1.24-beta] (Build 2130)

### 🚀 Improved & Refactored

- **Hero Mapping Consolidation**: Extracted a unified `HeroModelMapper.MapFromSummaries(List<HeroSummary>)` service to map raw hero configurations to full `HeroModel` objects, replacing the legacy 270-line reflection-based mapping monster in `SelectHero.cs` and duplicate implementations in `HeroGalleryForm.cs` and `SelectHeroPresenter.cs`.
- **Shared Utilities Extracted**:
   - Extracted `IsCustomSet()` (detecting custom/mixed skin sets from file patterns) and `FormatHeroIdAsName()` to the shared `HeroModelMapper` utility.
   - Consolidated all hero cache cleanup logic into `HeroCacheHelper.Cleanup()`, removing duplicate cleanup procedures in `SelectHero.cs` and `SelectHeroPresenter.cs`.
- **Dead Code Removal**: Cleaned up orphaned event handlers and dead design bindings in `SelectHero.Designer.cs`.

### 🐛 Fixed

- **Preset Loading Fix**: Corrected a layout bug in `SelectHero.LoadPresetFromFile` where the selector queried active rows from `ScrollContainer` instead of `RowsFlow` controls, fixing preset activation.
- **Dependency Conflicts Resolution**: Reverted `SixLabors.ImageSharp` back to stable `3.1.12` and test frameworks back to original versions to resolve build-blocking licensing restrictions and `NUnit` compile ambiguity errors.

### 🧪 Testing

- Added a comprehensive unit test suite in `HeroModelMapperTests.cs` validating `MapFromSummaries`, `IsCustomSet`, and `FormatHeroIdAsName` helper functions across 17 distinct scenarios.

## [2.1.24-beta] (Build 2129)

### ♻️ Refactoring

- **Feature Gating Consolidation**: Replaced duplicate inline feature access checks in `MainFormPresenter` (2× ~18-line try/catch blocks) and `NavigationPresenter` (1× ~40-line method) with a single shared `CheckFeatureAsync()` helper.
- All feature gating now flows through `FeatureAccessService.CheckFeatureAsync()` → `FeatureCheckResult` → presenter decision, eliminating code duplication and ensuring consistent behavior.

### 🧪 Testing

- Added 8 new unit tests for `CheckFeatureAsync`, `FeatureCheckResult` model, and `IsDevMode` accessor.

## [2.1.24-beta] (Build 2128)

### ✨ Added

- Added **ModsPack Update Trigger Dialog** — a modern WebView2 dialog that prompts users to update when a newer ModsPack is available after Dota 2 path detection.
- Created `ModsPackUpdateService` to wrap hash comparison with a clean API that only triggers for existing installs with a newer remote hash.
- Created `ModsPackUpdateDialog` (WebView2 form) with dark-themed `modspack_update.html` template featuring JetBrains Mono font, cyan accent borders, pulsing download icon, and staggered fadeInUp animations.
- Dialog includes "Update Now" (triggers install flow) and "Not Now" (dismiss) buttons, with fallback to native MessageBox if WebView2 is unavailable.
- Update check runs after Auto-Detect/Manual-Detect, following the flow: detect → check status → install dialog (if needed) → ModsPack update check.

## [2.1.22-beta] (Build 2126)

### 🐛 Fixed

- Fixed Support Dialog X button (titlebar close) not working by adding `WindowCloseRequested` handler and `window.close()` JS fallback for when WebView2 interop isn't available.
- Fixed Support Dialog UI being cropped on small resolution monitors (e.g., 1366×768) — form now sizes responsively to 85% of screen bounds (capped at 820×620), CSS container scrolls on overflow, and a compact media query fires below 580px viewport height.
- Added proper `Dispose` override for WebView2 resource cleanup in `SupportDialog`.

## [2.1.21-beta] (Build 2125)

### 🚀 Improved

- Removed TailwindCSS and Google Fonts CDN dependencies — all CSS and fonts now load locally via `@font-face` for offline and GFW compatibility.
- Switched WebView2 from `NavigateToString` to `Navigate(file://)` to enable proper local font resolution.
- Added real-time input validation on numeric cvar fields with red border feedback and range tooltips (e.g., `fps_max` 0–999, `rate` 1000–1000000).
- Form now sizes responsively to 90% of screen bounds (min 900×600), fixing overflow on 1366×768 displays.
- Fixed inconsistent JSON escaping in settings load/apply by using `EscapeJs()` uniformly.
- Added proper `Dispose` override for WebView2 resource cleanup.

### 🧪 Testing

- Added 14 unit tests for `Dota2PerformanceForm.ParseAutoexec` and `GenerateAutoexecContent` covering empty input, comments, standard cvars, inline comments, tabs, duplicates, alias lines, category grouping, unknown cvars, timestamping, and round-trip preservation.
- Added `InternalsVisibleTo` attribute to expose internal static methods to the test project.

## [2.1.21-beta] (Build 2124)

### ✨ Added

- Added **Newcomer Onboarding Guide** — an interactive step-by-step overlay that highlights each feature when the app opens for the first time, guiding users through Auto Detect, Manual Detect, Skin Selector, Miscellaneous, Install, Patch Update, Console, and Settings.
- Onboarding uses a native WinForms spotlight overlay with pulsing cyan glow animations and dark backdrop for pixel-perfect control highlighting.
   - Features smooth spotlight transition animations, connector lines, dynamic tooltip heights, and L-bracket corner decorations.
   - Automatically captures a screenshot of the parent form to render actual UI behind a dimming layer.
- Onboarding state persists via `IConfigService` — shows only on first launch, can be re-triggered from Settings → "Show Guide" button.

## [2.1.21-beta] (Build 2123)

### ✨ Added

- Added WebView2-based **Support Dialog** displaying Ko-fi donation goal and YouTube subscriber goal with CSS animations, powered by a remote `support_goals.json` config on R2 CDN.
- Created `SupportGoalsConfig` model and `SupportGoalsService` for fetching and caching combined goal data.

### 📖 Documentation

- Comprehensive documentation refresh across 7 files to match current project state (v2.1.22-beta, 480+ tests).
- Updated version badge, test count (285+ → 480+), added Performance Tweaker feature, Courier/Ward/Special/Cursor mod types, Smart CDN Selection, Resumable Downloads, GFW proxy mirrors, and ADR-0004 Presenter Decomposition to design decisions.
- Added FAQ, Installer Guide, Security links; expanded ADR listing from 3 → 7; added all 9 API docs and `samples/` directory to structure tree.
- Rewrote all Mermaid diagrams — system architecture (5 presenters, 10 WebView2 forms, CDN/Misc service subgraphs), entry point flow (IMainFormFactory + DI), MVP class diagram (decomposed presenters), service layer (Courier/Ward/CDN services), and CDN table (GFW proxy entries + SmartCdnSelector).
- Replaced stale `ServiceLocator` test pattern with constructor injection + Moq; added STA apartment tip.
- Version 2.1.2 → 2.1.21, self-contained build (no .NET install), corrected install path to `%LocalAppData%`, added 6 missing misc categories, fixed settings path to `%AppData%`.
- Fixed project root name, added full 15-directory service tree, expanded CDN entry, added `exceptions.md` link.
- Removed .NET 8 install step (self-contained), added WebView2 troubleshooting tip.

## [2.1.20-beta] (Build 2122)

### 🚀 Improved

- Added GitHub proxy mirror fallbacks (`ghfast.top`, `gh-proxy.com`) as CDN priority 4–5 for users in regions where GitHub, Cloudflare R2, and jsDelivr are blocked by ISP-level filtering (e.g., China Mobile/Telecom behind the Great Firewall).
- Added `IsProxyUrl()` helper to `CdnConfig` for proxy domain detection, used by `CdnFallbackService` statistics tracking.
- `SmartCdnSelector` now recognizes and labels proxy CDNs in latency benchmark results.
- `CdnFallbackService` tracks proxy download successes separately in statistics for better observability.

## [2.1.20-beta] (Build 2121)

### 🐛 Fixed

- Fixed Courier and Ward parsing structure breaking deeply nested properties like `styles` and `alternate_icons`. Re-implemented `ParseTopLevelKeyValues` and `ExtractVisualsKeyValues` with block depth-tracking algorithms to ensure nested properties no longer leak into the top-level merged output.
- Fixed Courier and Ward `particle_create` entries extracting all style particles regardless of the selected style. Now strictly filters and strips the `style` field, ensuring only the target style's ambient particles are injected.
- Fixed Couriers with `alternate_icons` falling back to the default unstyled `/onibi_lvl_00` thumbnail and item name. `BuildMergedCourierBlock` now explicitly overrides `item_name` and `image_inventory` properties by parsing the selected style's metadata before applying the merge.
- Fixed Ethereal effects failing to apply on couriers that already have native particle effects (e.g., Aghanim's Interdimensional Baby Roshan). Ethereal effects now properly replace native particles instead of being blocked by slot limits.

## [2.1.19-beta] (Build 2120)

### 🐛 Fixed

- Fixed critical game crash occurring when players installed Misc mods (via "Add to Current Mods") after installing a Hero ModsPack.
- Rewrote `KeyValuesBlockHelper.PrettifyKvText` state machine to correctly format one-liner `items_game.txt` files extracted from hero VPKs, ensuring proper Valve KeyValues block structure.
- Fixed `PrettifyKvText` and `ReplaceIdBlock` incorrectly inserting Windows-style CRLF (`\r\n`) line endings which broke the Source 2 engine parser; now strictly enforces LF (`\n`).
- Added comprehensive regression tests for KeyValues double-tab spacing, nesting indentation, round-trip extraction, and LF-only line endings.

## [2.1.18-beta] (Build 2119)

### ✨ Added

- Added a new "Tweak" button to the main form header.
- Implemented a completely new `Dota2PerformanceForm` using WebView2 to manage game performance options.
- Created `dota2_performance.html` featuring a cyber-themed industrial UI (JetBrains Mono, `#000` background, bracket-style headers) to easily apply FPS, Visual, Quality, Engine, VSync, and Network cvar presets to `autoexec.cfg`.
- Added custom Launch Options generator within the new Performance Tweak UI.

## [2.1.17-beta] (Build 2118)

### 🐛 Fixed

- Fixed "Generation Failed: 404 (Not Found)" crash caused by unhandled `HttpRequestException` propagating on the final retry attempt, completely bypassing the CDN fallback system.
- `GetStringWithRetryAsync` and `GetByteArrayWithProgressAsync` now return `null` immediately on 404 instead of retrying 3 times and then crashing.
- `TryWithFallbackAsync` now catches `HttpRequestException` at each CDN level, ensuring R2 → jsDelivr → GitHub fallback actually works when a CDN returns an error.
- Misc generation now reports partial-success warnings (e.g., "Weather: Download failed — asset not available from any CDN") instead of silently skipping mods and showing "All mods successfully applied."
- Both WebView2 and classic Misc forms now display warning details in the console and adjust the success dialog when mods were skipped.

### 🚀 Improved

- Misc mod downloads (`AssetModifierService`) and Hero skin downloads (`HeroSetDownloaderService`) now use `SmartCdnSelector` benchmark results to auto-select the fastest CDN for each user, instead of hardcoded R2 → jsDelivr → GitHub order.
- If GitHub is fastest for the user's connection, it becomes the primary CDN automatically. Slower CDNs serve as fallbacks.

## [2.1.17-beta] (Build 2117)

### 🛠️ Changed

- Updated `sync-to-r2.ps1` to set `Cache-Control` headers on upload — `max-age=86400` (24h) for Assets, `max-age=300` (5min) for config/remote — ensuring Cloudflare edge caching and reducing download stalls.
- Replaced stale `pub-*.r2.dev` URL with custom domain `cdn.ardysamods.my.id` in sync script and workflow docs to avoid ISP blocking.
- Added post-sync cache verification step showing `cf-cache-status` (HIT/MISS) for key files.
- Added cache pre-warming — automatically fetches popular files (heroes.json, Original.zip, config) after sync to prime Cloudflare edge cache so first real user gets a HIT.
- Added `--metadata` flag to rclone sync for proper content-type detection on uploaded files.
- Added Brotli decompression to `HttpClientProvider` — Cloudflare serves Brotli by default, 20-30% more efficient than gzip for JSON/text transfers.
- Replaced standalone `HttpClient` in `SubsGoalService` with shared `HttpClientProvider.Client` for consistent decompression, proxy, and TLS settings.

## [2.1.17-beta] (Build 2116)

### ✨ Added

- **Misc**: Added Special mod option — supports direct ZIP-based mod downloads (e.g., LowPoly Map) extracted into `pak01_dir.vpk` like other mods.
- Added `IsSpecialVpk` flag and `type` field to `RemoteMiscOption` for config-driven special mod identification.
- Added data-driven **mutual exclusion** system (`excludesWith`) — options like Map and Special auto-reset each other when selected, with red flash animation in the UI.
- Added `ExcludesWith` property to `RemoteMiscOption` and `MiscOption` models, wired through `MiscCategoryService`.
- Added JS `resetOption()` and `enforceExclusion()` functions for instant client-side exclusion feedback.
- Added C# exclusion enforcement in `HandleSelectionChanged` as backend guard.
- Added `"Special"` category mapping in `ModConfigurationData`.
- Added 16 unit tests for Special mod option and exclusion logic.

## [2.1.16-beta] (Build 2115)

### ✨ Added

- **Misc**: Added Ward option — select cosmetic ward skins with style support and model extraction from game VPK.
- Added `WardPatcherService` for ward block parsing, merging, and model mapping (`entity_model` type, single target `default_ward.vmdl_c`).
- Added 31 unit tests for ward generation (parsing, mapping, merging, styles, skin injection, particles).

## [2.1.16-beta] (Build 2114)

### ✨ Added

- Added FAQ page into user docs

## [2.1.16-beta] (Build 2113)

### 🚀 UI/UX

- Replaced the simple MessageBox update prompt with a modern, dark-themed WebView2 `UpdateAvailableDialogWebView`.
- Included clear version comparison (Current vs Latest) and app type (Portable/Installer) in the update dialog.
- Added manual download cards for the CDN Server and official website (`ardysamods.my.id`).

## [2.1.15-beta] (Build 2111)

### 🚀 Improved

- Included `AssetCacheService` directory (`%LOCALAPPDATA%\ArdysaModsTools\AssetCache`) in cache clearing and size calculation.
- Clears `AssetCacheService` in-memory cache after disk cleanup to prevent serving stale entries.
- Added `--minimized` flag to Windows startup registry entry, enabling the app to start silently in the system tray.

### 🐛 Fixed

- Re-calculate actual cache size after clearing instead of hardcoded 0 B.
- Fixed `resetButton()` in HTML to use dynamic icon parameter (now properly shows Emoji instead of static SVG).
- Validates `IsRunOnStartupEnabled` to ensure the stored registry path matches the current exe path.
- `EnsureStartupPathCurrent()` now auto-fixes stale registry paths on launch.

## [2.1.15-beta] (Build 2110)

### 🚀 Improved

- Fixed slow "Checking for updates" overlay in Miscellaneous form by parallelizing freshness checks (`SemaphoreSlim(8)`) and downloading missing thumbnails first.

## [2.1.15-beta] (Build 2109)

### ✨ Added

- Added multi-CDN fallback strategy (R2 → jsDelivr → GitHub Raw) for all miscellaneous mod downloads (Battle Effect, River Vial, Emblems, etc.) to prevent failures caused by CDN file-type restrictions.

### 🐛 Fixed

- Fixed indentation loss during nested key-value block merging in courier mod generation, ensuring correct `items_game.txt` formatting.
- Fixed `ConvertToFastUrl` logic to correctly prioritize R2 CDN for project repository assets, resolving "asset not found" errors for `.zip` files blocked by jsDelivr.

## [2.1.15-beta] (Build 2108)

### 🐛 Fixed

- Testing NuNit particle effects

## [2.1.15-beta] (Build 2107)

### ✨ Added

- **Ethereal Courier Particle Effects**
   - Added "✦ Ethereal" badge and multi-select overlay to all Courier choice cards (including Default Courier).
   - Select up to 2 unusual particle effects from 40 available options (Trails, Eye Glows, Ambient effects).
   - Intelligent slot logic limits selections based on existing carrier particle configurations.

### 🐛 Fixed

- Model not showing up in game

## [2.1.14-beta] (Build 2105)

### ✨ Added

- Add Courier option in Miscellaneous

### 🚀 Improved

- Replaced `HttpClient` manual retry loops in `HeroSetDownloaderService` and `ModInstallerService` with a centralized `ResumableDownloadService`.
- Replaced 200+ lines of custom stall detection logic in `OriginalVpkService` with `ResumableDownloadService` integration.
- Implemented HTTP `Range`-based chunk streaming to support resuming downloads across CDN failovers (R2 → jsDelivr → GitHub) without losing progress.
- Increased `CdnConfig.TimeoutSeconds` from 15 to 30 and `CdnConfig.MaxRetryPerCdn` from 1 to 2 for better reliability on unstable connections.

### 🐛 Fixed

- Suppressed verbose developer-facing `[VPK-Search]` diagnostics from the UI progress overlay; these are now routed exclusively to the background `FallbackLogger`.
- Coalesced redundant log entries during Courier and River modification for a cleaner user experience.

---

## [2.1.14-beta] (Build 2104)

### 🐛 Fixed

- Fixed hardcoded SHA1 hash literal to use `ModConstants.ModPatchLine` preventing breakage when hash changes.
- Fixed silent exception swallowing during manual installation; exceptions are now properly logged via `FallbackLogger`.
- Fixed inaccurate diagnostic path error pointing to `bin/win64` instead of the Dota game root.
- Fixed race condition in `OnFileChanged` that triggered `ObjectDisposedException` when rapidly cancelling debounced events.
- Resolved cache stampede vulnerability in `GetOrCreateAsync` by implementing a per-key `SemaphoreSlim` double-check locking pattern.
- Added initialization guard (`_initialized`) to prevent duplicate subscription bindings if called alongside `Program.cs`.
- Added missing `CancellationToken` support to `DownloadFileAsync` for resumable/cancellable streaming downloads.

---

## [2.1.14-beta] (Build 2103)

### 🐛 Fixed

- Fixed download speed display showing "-- MB/S" during app updates by properly triggering UI update events and using a decoupled timer-based refresh.

---

## [2.1.14-beta] (Build 2102)

### 🐛 Fixed

- Removed `requireAdministrator` requirement from core application to prevent files written by the tool from inheriting admin ownership, which was causing Dota 2 to run in administrator mode and block matchmaking.
- Added `AdminHelper` utility to handle on-demand elevation only for legacy Program Files installations, maintaining write access backward compatibility.

---

## [2.1.14-beta] (Build 2101)

- Added multi-CDN fallback (R2 → jsDelivr → GitHub Raw) with 30s stall-based failover to `OriginalVpkService` to reliably handle CDN unreachable/blocking states without endless stalls during base file downloads.

---

## [2.1.13-beta] (Build 2100)

### 🚀 Added

- Implemented build-aware update system that detects updates even when the version string remains the same (e.g., hotfixes re-uploaded to CDN).
- Added multi-pattern build extraction from GitHub release notes and titles, supporting specific formats and ranges
- Added robust version parsing logic via new `AppVersion` model to handle comparing semantic versions, pre-release suffixes, and build numbers.

### 🐛 Fixed

- Added notes-based fallback to R2 manifest parsing to gracefully extract build numbers from the `"notes"` field when an explicit `"build"` field is missing.

---

## [2.1.13-beta] (Build 2099)

### 🐛 Fixed

- Fixed VPK item patching issue where rich `index.txt` data (e.g., Shadow Fiend arcana) wasn't being applied to `items_game.txt` due to unhandled one-liner formats.
- Improved `index.txt` discovery to fallback to the zip root when assets are stored in subfolders.
- Added comprehensive diagnostic logging for block parsing, validation, and replacement steps to aid debugging.
- Added complete test coverage for deeply nested block parsing, small-to-large block replacements, and double-tab formatting scenarios.

---

## [2.1.13-beta] (Build 2098)

### 🐛 Fixed

- Fixed hash comparison in `ModInstallerService` using case-sensitive `==` — now uses `StringComparison.OrdinalIgnoreCase` for consistent SHA256 hex comparison.
- Eliminated duplicate network call in `InstallAsync` — redundant `CheckForNewerModsPackAsync` pre-check removed; version check is handled internally by `InstallModsAsync`.
- Fixed reinstall triggering a double progress overlay when "Reinstall anyway?" was accepted from the up-to-date prompt.

### 🚀 Improved

- Added automatic retry (1 attempt, 2s delay) for HTTP ModsPack downloads on transient `HttpRequestException` / `IOException` failures. Progress resets to 0% on retry with "Retrying download..." status feedback.
- Wired `statusCallback` through `RunInstallCoreAsync` — status messages ("Downloading...", "Verifying download...", "Retrying download...") now display live in the progress overlay.
- Unified `RunAutoInstallAsync` and `ReinstallAsync` into a single `RunInstallCoreAsync(bool force)` method, eliminating ~80 lines of duplicate logic and context-aware install/reinstall result messaging.

---

## [2.1.12-beta] (Build 2097)

### 🚀 UI/UX

- Added **ModsPack Preview Panel** to the progress overlay during auto-install and reinstall operations.
   - Overlay resizes to 1280×720 with a side-by-side layout: progress ring (left) + hero skin grid (right).
   - Hero skin images are fetched live from GitHub (`assets/updates`) and displayed in a 3-column scrollable grid with search and lightbox.
   - Preview loads asynchronously in the background — download progress is never blocked.
- Moved **cancel button** into the HTML overlay (was hidden behind the Dock.Fill WebView panel in WinForms).
   - Uses WebView2 `postMessage("cancel")` bridge to fire the C# `RequestCancel()` method.
   - Shows "CANCELLING..." feedback directly on the HTML button when clicked.
- Fixed **corner "L" decorations** overlapping the preview panel — changed from `position: fixed` (viewport-wide) to `position: absolute` scoped inside `.progress-side`, so they frame only the progress container.

---

## [2.1.12-beta] (Build 2096)

### 🚀 Improved

- Redesigned with WebView2. Now features status-aware diagnostics (no false positive fails when cache is empty), dedicated "Patch Update" action, and "Up to date" labelling.
- Redesigned as a WebView2 dialog with real-time animated checks, progress bar, and comprehensive 4-step verification logic (VPK, Version, Signature, Integration).
- Enhanced Dota 2 version detection to read `steam.inf` directly, preventing false "Never patched" errors when `version.json` is missing but mods are active.

### 🐛 Fixed

- Fixed issue where "Game Patch" and "Mod Integration" diagnostics showed "FAIL" even when overall status was "Ready", by using status-aware logic instead of raw cache values.

---

## [2.1.12-beta] (Build 2095)

### 🚀 Improved

- Added "Legacy Set" & "Custom Set" categorization

### 🐛 Fixed

- Fixed stuck "Loading Assets 0/0" overlay by moving silent cache validation to background task.

## [2.1.12-beta] (Build 2094)

### 🚀 Improved

- Significantly reduced uninstaller size (~140MB -> ~70MB) by separating the payload from the uninstaller executable.
- Implemented smart mode detection — slim uninstaller now correctly auto-detects "Uninstall" mode when run directly.
- Enhanced self-deletion reliability with a PID-based wait loop to ensure the process exits fully before file removal.
- Added terminal-retro style `[ OK ]` completion symbols and removed the "Launch" button from the uninstall success screen.

### 🐛 Fixed

- Updated Help, Update, and About URLs to point to the official website `https://ardysamods.my.id`.
- Fixed "Reinstall" showing instead of "Uninstall" when running the uninstaller directly.
- Fixed version number overflow in Update mode by stacking old/new versions vertically.
- Fixed race condition where self-deletion failed if the window wasn't closed immediately.

---

## [2.1.12-beta] (Build 2093)

### 🐛 Fixed

- Fixed progress overlay appearing on every open by implementing a smart cooldown mechanism. Now skips the overlay entirely if thumbnails are cached and recently checked (within 10 mins).

---

## [2.1.12-beta] (Build 2092)

### 🚀 Added

- Added Clear Cache button with trash icon and live cache size display to the WebView2 settings dialog.

### 🐛 Fixed

- Fixed Close/✕ buttons hanging — `SafeClose()` now always defers via `BeginInvoke` to avoid disposing WebView2 mid-event.
- Fixed Run on Startup toggle silently failing — now checks `SetRunOnStartup` return value and reverts the toggle with error toast on failure.

### 🗑️ Removed

- Deleted orphaned `SettingsForm.cs` (WinForms) and `SettingsPresenter.cs` (unused MVP presenter wired to dead form).

---

## [2.1.12-beta] (Build 2091)

### 📖 Documentation

- Comprehensive rewrite with detailed features table, system requirements, quick start guide, full architecture overview (tech stack, project structure tree, ADR links), developer build instructions, collapsible FAQ, troubleshooting table, and credits section acknowledging Dota 2 SkinChanger community, modders, Valve, and open-source libraries.

---

## [2.1.12-beta] (Build 2090)

### 🐛 Fixed

- Fixed critical logic bug where legacy Inno Setup installations were ignored if the new registry key existed but pointed to a different path (e.g., portable move). Now checks both keys independently.
- Implemented atomic extract-then-swap pattern. Installation now extracts to a temporary directory first and only swaps files upon success, preventing broken installs if the process is interrupted.
- Added PE header validation (MZ bytes) and minimum size check (50KB) for downloaded installers to prevent launching corrupted or truncated files.
- Added `Mutex`-based single-instance enforcement to prevent concurrent installations and file locking issues.
- Improved global error handler to show more informative error messages with inner exception details.
- Mapped all 10 JetBrains Mono font variants (Thin, Light, ExtraLight, etc.) to their correct Windows font names.
- Fixed animation holding issue where `FillBehavior.HoldEnd` prevented subsequent property changes.

### ♻️ Refactoring

- Made `RegistryHelper` and `ShortcutHelper` context-aware.
- `%LocalAppData%` uses `HKCU` registry hive and user-specific Desktop/StartMenu shortcuts.
- `Program Files` uses `HKLM` registry hive and All Users Desktop/StartMenu shortcuts.
- `InstallationDetector` and `RegistryHelper` now scan both `HKCU` and `HKLM` hives to correctly detect and clean up any installation type.
- Rewrote update strategy for WPF installer — removed legacy Inno Setup batch script (`/VERYSILENT`), now directly launches installer with `--update` flag. Added UAC cancellation handling.
- Replaced `unins000.exe` detection with registry-based detection (`HKLM` uninstall key) and `%LocalAppData%` path check. Supports both new WPF key and legacy Inno Setup `_is1` key suffix for backward compatibility.
- Added legacy `_is1` registry key fallback for detecting old Inno Setup installations in `Program Files`. Automatically cleans up legacy key during migration. Strips `+commitHash` from `DisplayVersion` before writing to registry.

---

## [2.1.12-beta] (Build 2089)

### 📖 Documentation

- Added 7 `SKILL.md` files for Context7 indexing:
   - `install-mods` — Install, update, disable mods
   - `query-active-mods` — Query active hero/misc mods
   - `auto-patching` — Detect updates and re-apply patches
   - `control-misc-mods` — Weather, HUD, terrain control
   - `mod-file-structure` — File layout and JSON schemas
   - `generate-hero-cosmetics` — Hero set generation
   - `check-mod-status` — Status validation and monitoring
   - `dependency-injection` — DI setup and service registration
   - `conflict-resolution` — Conflict detection, 6 resolution strategies, priority management with executable code
   - `custom-mod-integration` — End-to-end custom mod lifecycle including complete music pack install example
- Enhanced `mod-file-structure` SKILL.md with VPK validation API, gameinfo status checking, all 3 JSON schemas with CRUD examples, and error handling patterns.
- Enhanced `context7.json` with `$schema`, folder config, exclusions, and coding rules.

---

## [2.1.12-beta] (Build 2088)

### 🚀 Features

- Added unified API to query currently installed mods (Heroes + Misc).
- Added comprehensive developer docs for:
   - Mod File Structure (`mod-file-structure.md`)
   - Auto-Patching Configuration (`auto-patching.md`)
   - Active Mods Querying (`active-mods.md`)
   - Misc Mods Control (`misc-mods.md`)

---

## [2.1.12-beta] (Build 2087)

### 🚀 UI/UX

- Improved "Preparing" phase feedback in Skin Selector.
   - Status now shows real-time download percentage (e.g., "Downloading base files (45%)") instead of static "Preparing".
   - Re-enabled substatus display in `ProgressOverlay` to show detailed log messages (download size, extraction steps).

### 📡 Network

- Added stall detection to `OriginalVpkService` for base file downloads.
   - Warns user after 30s of no data ("Download appears stalled").
   - Suggests troubleshooting steps after 90s.
   - Auto-resets warning label when download resumes.

---

## [2.1.12-beta] (Build 2085) - 2026-02-12

### 🚀 Added

- Improved release workflow with NuGet caching, SHA256 checksums for artifacts, and automatic runtime installer downloading (only if missing).
- Added build summary to GitHub Actions and upgraded to `action-gh-release@v2`.

### 🐛 Fixed

- Fixed critical regression in .NET 8 detection where running in 64-bit mode caused the installer to miss 32-bit registry keys (`WOW6432Node`). Now explicitly checks `HKLM32`.

---

## [2.1.12-beta] (Build 2084) - 2026-02-12

### 🐛 Fixed

- Removed unnecessary .NET 8 Desktop Runtime check for self-contained builds, fixing a blocking prompt during updates.
- Bundled .NET 8 Desktop Runtime installer for seamless auto-installation if needed (fallback).

---

## [2.1.12-beta] (Build 2083) - 2026-02-12

### 🚀 Added

- Added remote feature gating system via Cloudflare R2 (`feature_access.json`).
   - New `FeatureAccessConfig` model with fail-open defaults.
   - New `FeatureAccessService` with 5-minute cache and graceful error handling.
   - Skin Selector and Miscellaneous can now be remotely enabled/disabled with custom messages.
- Added `FeatureUnavailableDialog` — WebView2-based dialog matching `progress.html` aesthetic (animated wave, corner decorations, monospace font) with native MessageBox fallback.

### ♻️ Refactoring

- Consolidated three duplicate KeyValues parsing implementations into single source of truth in `KeyValuesBlockHelper`.
- Added `heroId` filtering to `ExtractBlockById` and `ReplaceIdBlock` to prevent false matches on short numeric IDs (e.g., ID `"99"` matching in `kill_eater_score_types` instead of `items`).
- Enhanced `NormalizeKvText` with smart-quote (`""`→`""`), smart-apostrophe (`''`→`''`), non-breaking space, and zero-width character handling.
- Refactored to delegate all KV parsing to `KeyValuesBlockHelper`, removing ~200 lines of duplicate code. Domain logic (validation, indentation normalization, file discovery) preserved.

### 🗑️ Removed

- Deleted `GeneratorService.cs` (zero references). Its superior text normalization logic was merged into `KeyValuesBlockHelper.NormalizeKvText`.

### 🧪 Testing

- Added 14 new unit tests for `KeyValuesBlockHelper`: heroId filtering (4), smart-quote normalization (2), zero-width char stripping, non-breaking space handling, BOM removal, `ParseKvBlocks` (3), `ReplaceIdBlock` with heroId (2).
- Added 16 new unit tests for `FeatureAccessConfig` model and `FeatureAccessService` (total: 285 tests).

---

## [2.1.11-beta] (Build 2082) - 2026-02-10

### ♻️ Refactoring

- Decomposed `MainFormPresenter` into 3 specialized presenters for SRP (`ADR-0004`):
   - `ModOperationsPresenter` - install, reinstall, disable operations
   - `PatchPresenter` - patch updates, verification, watcher
   - `NavigationPresenter` - hero selection, miscellaneous forms
- Created specialized registration methods (`AddCoreServices()`, `AddConflictServices()`, `AddHeroServices()`, `AddLoggingServices()`, `AddPresenters()`, `AddUIFactories()`).
- Added `TestServiceFactory` helper for cleaner test setup without ServiceLocator.

### 🐛 Fixed

- Switched `.NET 8` Desktop Runtime detection from CLI-based (`dotnet --list-runtimes`) to registry-based check for improved reliability across system configurations.

### 📝 Documentation

- Rewrote all existing ADRs (0001–0004) to full MADR format with Problem Statement, Decision Drivers, and Alternatives Considered.
- Added 3 new Architecture Decision Records:
   - `ADR-0005` - WebView2 Hybrid UI strategy
   - `ADR-0006` - Automated Patch Watcher system
   - `ADR-0007` - Security & Anti-Tamper architecture
- Created ADR index (`README.md`) and standardized `TEMPLATE.md` based on MADR format.

### 🗑️ Removed

- Completely removed `ServiceLocator.cs` from production and test code.
- Replaced obsolete default constructor with `NotSupportedException`.

### 🧪 Testing

- Added 26 new unit tests for specialized presenters (total: 269 tests).

---

## [2.1.11-beta] (Build 2080)

### 🚀 Added

- Added "Battle Effect" asset category with 10 TI-themed effects (Aghanim, Nemestice, TI 2015-2022).
- Added async versions of file wait/copy methods (`WaitForFileReadyAsync`, `SafeCopyFileWithRetriesAsync`) to prevent UI thread blocking.

### 🛠️ Changed

- Switched to Cloudflare R2 CDN (`cdn.ardysamods.my.id`) as primary content source for faster updates.
- Removed Clear Cache button and Cache Size display for cleaner UI.
- Added diagnostic logging to empty catch blocks in `MiscFormWebView` for better debugging visibility.

### 🐛 Fixed

- Fixed X button not closing the settings form properly.
- Fixed caching overlay display and asset loading flow for miscellaneous options.
- Fixed thumbnail URL generation for "Battle Effect" and other misc categories.
- Updated `patch_models.py` to handle double URLs and CDN fallback for `heroes.json`.
- Fixed `MainFormPresenterTests` missing `configService` constructor parameter.

## [2.1.10-beta] (Build 2078) - 2026-02-04

### 🚀 Features & Architecture

- Implemented `IMainFormFactory` to enable constructor injection in WinForms (`ADR-0002`).
- Added multi-CDN strategy with **Cloudflare R2** as primary, falling back to jsDelivr and GitHub Raw (`ADR-0003`).
- Added comprehensive [Troubleshooting Guide](docs/TROUBLESHOOTING.md) and new Architecture Decision Records.

### 🐛 Bug Fixes

- Fixed "CONNECTION TO SERVER FAILED" in Skin Selector by implementing R2 support and increasing timeout to 15s.
- Fixed critical bug in `ClearTempFolder` that could recursively delete files; now targets only application-specific temp data.
- Removed duplicate method in `MainFormPresenter` and fixed logging for connection errors.

### ♻️ Refactoring

- Replaced `ServiceLocator` anti-pattern with proper **Constructor Injection** across `MainForm`, `SelectHero`, and `HeroGalleryForm`.
- Moved `FavoritesStore` persistence to `%AppData%\ArdysaModsTools` to prevent data loss.
- Suppressed obsolete warnings for legacy test compatibility helpers.

### 🗑️ Removed

- Removed `ServiceLocator` usage from all production code paths (kept only for unit tests).

## [2.1.9] - 2026-02-04

### Changed

- Optimized application startup time by lazy-loading non-critical services.
- Updated internal libraries to improve compatibility with latest Windows updates.
- Refined error messages for network timeouts to be more user-friendly.

### Fixed

- Fixed minor flickering issues in the Hero Gallery grid when resizing the window.
- Resolved edge case where temporary files weren't fully cleared on application exit.

## [2.1.8] - 2026-02-04

### Added

- Added detailed logging for VPK extraction steps to aid in debugging.
- Introduced new "Troubleshooting" section in the main documentation.

### Changed

- Optimized `sync-to-r2.ps1` script for faster asset uploads.
- Enhanced `MiscForm` rendering performance for smoother scrolling.

### Fixed

- Fixed layout alignment issues in the Settings form for high-DPI displays.
- Resolved potential memory leak in image processing when loading large hero sets.

## [2.1.7-beta] - 2026-02-03

### Added

- Hero Gallery WebView2 UI with Tailwind CSS
- Favorites system for hero selection
- Cache cleaning service with size calculation

### Changed

- Migrated to MVP pattern for MainForm
- Improved status detection accuracy

### Fixed

- WebView2 crash on startup for some systems
- VPK extraction timeout issues

---

## [2.1.6] - 2026-01-28

### Added

- Auto-patching after Dota 2 updates
- Subscriber goal display widget

### Changed

- Improved mod installation speed
- Better error messages for common issues

### Fixed

- Path detection for non-standard Steam installations
- Signature patching reliability

---

## [2.1.0] - 2026-01-15

### Added

- Miscellaneous mods support (weather, terrain, HUD)
- Preset save/load for hero selections
- Progress overlay with WebView2

### Changed

- Complete UI redesign with dark theme
- Refactored service layer architecture

---

## [2.0.0] - 2025-12-01

### Added

- Initial release of AMT 2.0
- Hero skin selector
- ModsPack auto-installer
- One-click patching system

---
