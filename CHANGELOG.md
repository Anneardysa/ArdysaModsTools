# Changelog

All notable changes to ArdysaModsTools will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.2.16-beta] (Builds 2236–2242)

### 🎨 UI/UX (builds 2240–2241)

- **Update dialog redesigned, installer updates now delta-first** (2241): `update_available.html` reworked; on installer installs with a delta available the full-download links stay hidden behind a "Calculating update size…" state until the diff answers (`_deltaPending` in `UpdateAvailableDialogWebView`, mirroring `PrepareDeltaAsync`'s gate so the links are never hidden for a diff that never runs). Navigation wait bounded to 10s + `RunContinuationsAsynchronously` (blank-window hang / STATUS_BREAKPOINT guards); `UpdaterService` HTTP responses now disposed (per-retry connection leak fixed). New locale key `updateAvail.checking` in all 8 catalogs.
- **Generation output stays in each feature's own console** (2240): log routing in `MainFormPresenter`/`NavigationPresenter` (+ `FallbackLogger`/`FileAppLogger`) fixed so a feature's generation output no longer bleeds into another feature's console.

### 🔧 Build (builds 2237–2239, 2242)

- **GitLab CI auto-mirrors `main` to the public GitHub repo** (2239): comment-stripped, secret-redacted, gated via `scripts/publish-github.ps1`; `.mirrorignore` expanded to drop dev/internal paths. `EmbeddedAssetKey` fragments redacted in the mirror via `[AMT:MIRROR-*]` markers (2238). `publish/` mirror-clone excluded from the main build globs (fixes MSB3577 duplicate `ProgressOverlay.resources`) (2237).
- **`sync-release-to-r2.yml` restored on the GitHub mirror** (2242): the earlier blanket `.github/workflows` exclusion had removed it, so publishing a GitHub release no longer updated `releases.json`. `.mirrorignore` now drops only `ci.yml`/`release.yml`, keeping the release-sync workflow public so `release: published` fires again.

### ✨ Added (build 2236)

- **Latest Updates strip on the main shell** (2236): the Skin Selector's "Latest Updates" (recent hero-set additions from `set_update.json`) now also appears on the main window, directly under the banner carousel — a horizontal band of set-thumbnail cards (hero name, `Today/Yesterday/Nd ago`, NEW badge for ≤2 days, capped at 12) that opens the Skin Selector on click. Content-discovery tier between the promotional banner and the action cards, mirroring the gallery's own top-of-page placement.
  - **Data path** (`MainFormWebView.LoadLatestUpdatesAsync`): mirrors the banner loader — fired non-blocking from `OnFormLoad`, reuses `HeroService.LoadSetUpdatesAsync()` (CDN fallback chain, 10s timeout, bundled fallback), skips the heroes.json fetch entirely when nothing is recent (≤7 days), and pushes through the buffered `Js()` channel so a fetch finishing before page-ready isn't dropped. Any failure = the strip stays hidden, one console line, no error UI.
  - **Shared resolver** (`Core/Services/Hero/SetUpdateResolver.cs`, new): the ~70-line entry→card resolution block (3-format hero-id match, set-name → index + first `.jpg/.png/.webp` thumbnail, unresolved-entry filtering) extracted out of `HeroGalleryForm.LoadSetUpdatesAsync` into a pure static helper with an injectable clock — both surfaces now serialize the same `SetUpdateCard` payload, byte-compatible with the existing `loadLatestUpdates()` JS contract in both pages.
  - **UI**: Skin-Selector-style navigation — the scrollbar is hidden; hover reveals dark blurred side arrows (mirroring `hero_gallery.html`'s `.carousel-arrow`) that page by one visible width with smooth scrolling, hide at their end of the row, plus wheel→horizontal scroll. Arrow ink hardcoded white and card captions on a literal dark scrim (the "artwork overlays stay dark" light-theme rule). Cards are built via `createElement`/`textContent` — hero names are remote data, no innerHTML interpolation at the shell trust boundary. Heading reuses the existing `heroGallery.latestUpdates` key (all 8 locales).

### 🎨 UI/UX (build 2236)

- **Main window grown to 1280×1000** (was 1280×780) to fit the strip without squeezing the console (the `flex: 1` element that absorbed it); banner carousel 200px → 240px, update cards 180×120. `DpiLayout.ClampToWorkingArea` shrinks the window back on monitors that can't fit it; note the UI Size fit-cap consequence: on ~1080p working areas Large/Extra Large now cap at ×1.0 for the main window (designed behaviour — zoom and window grow together or not at all).

### 🐛 Fixed (build 2236)

- **A banner manifest without slides was silently discarded** (2236): `BannerService.Parse` returned `null` unless at least one usable slide survived, but `LoadBannerCarouselAsync` also reads `modspackVersion` and `installCard` from the same config — so a manifest carrying only those fields (e.g. a ModsPack version bump with no carousel change) never updated the What's New badge or the install-card art. Non-slide fields no longer gate on slide count.
- **`--wash` was a self-referential CSS variable** (2236): `main_shell.html` declared `--wash: var(--wash)` (the guaranteed-invalid value), working only because `theme.css` splices a real definition after it — a theme.css load failure would have blanked every `var(--wash)` surface (patch-menu inset, confirm icon/note, failure-log body). Now a real dark-theme fallback (`rgba(255,255,255,0.06)`).
- **Localized console lines could render blank before the i18n bootstrap** (2236): `appendLogI18n` fell back to an empty string when a keyed log line arrived before `WebViewLocalizer` injected `window.renderLogSegments`. It now shows the literal segments/raw keys immediately; the line still re-translates via its `data-i18n-log` attribute once the catalog loads.
- **Dead code swept** (2236): the `variant == "info" ? Info : Info` ternary in `ShowShellToast`'s tray fallback (both branches identical — now `error ? Error : Info`), and the `.win-btn.tweak:hover` selector for a title-bar button that doesn't exist.

### 🧪 Tests (build 2236)

- `SetUpdateResolverTests` (new): hero-id matching across all 3 formats, first-image-file selection skipping non-images, unresolved hero/set/thumbnail entries dropped, `DaysAgo` from the injected clock, input order preserved, null-input guards. Plus a `BannerServiceTests` regression pin: a version-only manifest (no slides) parses non-null. **755 tests green.**

## [2.2.15-beta] (Builds 2234–2235)

### ✨ Added (build 2235)

- **Light theme — pure black-on-white, selectable in Settings** (2235): the app has shipped a single monochrome dark UI; there is now a **Settings → Appearance → Theme** dropdown (Dark/Light, default Dark — absent config key keeps today's exact behaviour). The light theme is the faithful inversion of the design language: **every surface solid white, every ink solid black**, separation carried by strengthened hairline borders instead of grey fills, and the single white accent becomes a single black accent (primary buttons like **Generate ModsPack** flip to black-on-white automatically).
  - **Delivery** (`UI/Helpers/WebViewTheming.cs` + `Assets/Html/theme.css`): one shared stylesheet holds a `:root[data-theme="light"]` override for the union of all four token vocabularies the 18 HTML pages grew. It is spliced into each page's HTML **string before `NavigateToString`** (and stamped `data-theme="light"` on `<html>`), so the CSS is present at parse time — **a page never paints dark and then flips**. `dota2_performance.html`, the one page navigated from disk, gets the identical payload via `AddScriptToExecuteOnDocumentCreatedAsync`; it was also the only untokenized page (89 hex literals, plus a Tailwind palette) and is now fully token-driven — including the four opacity-modifier call sites Tailwind would have silently rendered at full alpha over a `var()` color.
  - **Wiring**: persisted as the `"theme"` key via the existing `IConfigService` bag (same ad-hoc pattern as `uiScale`), read in `Program.cs` before any form exists, broadcast through the previously dead `UI.Theme.SetTheme`/`ThemeChanged` (revived — it had zero callers). The main shell subscribes and flips live behind the Settings dialog; every WebView host's `BackColor`/`DefaultBackgroundColor` follows `Theme.Canvas` so there is no black flash behind white content. All windows open via `ShowDialog` in a modal chain, so no other window can exist mid-flip.
  - **Deliberate exceptions**, each scoped in `theme.css`: content overlaid on artwork (Install ModsPack card, banner carousel controls, hero-card name/favourite overlays, ModsPack-preview hero names) keeps the dark-theme ink — the surface under it is an image behind a dark scrim, never white; hover/selection feedback keeps a faint tint because a white-on-white hover state gives no feedback; native WinForms dialogs (ConflictResolution, ErrorLog, …) stay dark in v1 — rare, transient surfaces.
  - **Readability pass over every window in light mode**: solid-white title bars (including the Skin Selector/Miscellaneous `#titleBar`, a *different* id from the shell dialogs' `#titlebar`), modals, headers, footers, search/filter containers, console head; console log stream re-inked from pale on-dark literals to dark equivalents (deep red / dark amber / dark blue); attribute glyphs (Str/Agi/Int/Universal) and the About page's Built-With icons flip their white-silhouette filters to solid black; About's version chip is a white bar with black text.
  - **Locales**: `settings.theme.*` + `toast.theme.changed` added to all 8 catalogs (parity test enforced). **Tests**: `Tests/Helpers/WebViewThemingTests.cs` pins the string-splice contract (light adds attribute + stylesheet, dark stays byte-identical apart from an inert style block, anchor-less input returned untouched) plus an anchors guard over every `Assets/Html/*.html`; **748 tests green.**

### ✨ Added (build 2234)

- **Incremental ("delta") app updates — the ~200 MB installer re-download is gone** (2234): Every update forced a full installer download regardless of how little changed. The app ships as a self-contained .NET 8 publish tree (**174 MB, 528 files**) of which **~167 MB is the .NET runtime, byte-identical across releases** — only `ArdysaModsTools.dll`, its apphost and `Assets/` actually change: **3.9 MB across 47 files** in the worst case where all of them change at once. Each release now publishes a per-file SHA-256 manifest (`releases/<version>/files.json` on R2) alongside the file tree it describes; the client hashes its own install folder against it and downloads **only the entries that differ**, then applies them out-of-process. See [ADR-0012](docs/adr/0012-incremental-delta-updates.md).
  - **Manifest contract**: deliberately the *same schema* as `Assets/asset_hashes.json` (ADR-0010), so `AssetHashManifestService.ParseManifest` and `AssetHashVerifier` are reused unchanged. `releases.json` gains one **additive** field per release — `"files": "<manifest URL>"` — which older clients ignore; its absence simply means "full download only". The manifest URL is the **single source of truth** for where a release lives: the file base (`…/files/`) and the previous version's manifest are *derived* from it, so the manifest and the files it describes can never point at different places.
  - **Client** (`Core/Services/Update/DeltaUpdateService.cs`): `PrepareAsync` fetches the manifest, hashes the install tree, and diffs (deletions come from the *previous* version's manifest, `old keys − new keys`; when that manifest is purged the answer is "delete nothing", never a guess). `StageAsync` downloads each file into `%LocalAppData%\ArdysaModsTools\update\<version>\`, **every one verified against the manifest hash** by `ResumableDownloadService` (ADR-0009/0010); only after *all* files verify are `apply.json` and then the `.staged-ok` marker written, so a half-finished staging folder can never be applied.
  - **Applier** (`Updater/AMT.Updater.csproj` → `tools/updater/AMT.Updater.exe`): a standalone **Native AOT** exe (2.8 MB, no runtime dependency — it keeps running while the app's own runtime DLLs are replaced). It **re-verifies every staged file's SHA-256 independently** before touching anything (staging is user-writable, so "it verified at download time" is not the same statement as "it is the file I am about to install"), and the app re-verifies the applier itself immediately before running it (it may run elevated). Per file the swap is `copy → target.amtnew` → `rename target → .amtbak` → `rename .amtnew → target`, so a crash before the renames leaves the original untouched; any failure rolls **every** applied file back and the app is relaunched either way. The one exception: if the app never exited, nothing was touched and it is still running, so no second instance is spawned.
  - **Recovery**: an in-progress marker makes every leftover unambiguous to the startup sweep (`RepairInterruptedUpdate`). Marker present (killed mid-swap): a target that is *missing* while its backup exists is restored. Marker absent (apply finished): every surviving backup is one the applier could not delete, so it is swept — and a deliberately-deleted file is never resurrected.
  - **UI**: `UpdateAvailableDialogWebView` gains an **Update Now** card (revealed only once the diff succeeds, with the real download size) plus a progress bar; the applier is launched **while the dialog is still open** so a refused launch (UAC declined, integrity check failed) is shown rather than silently closing the window. The CDN/website links stay exactly as they were and are the fallback for every failure path. Installer-type installs only — the portable build is a **single-file** exe (`FolderProfile.pubxml`), a different layout entirely, so it keeps the zip link.
  - **Release pipeline**: `scripts/generate-files-manifest.ps1` emits `files.json` **and** `files.list`; the upload is driven by that list (`rclone --files-from`), so the manifest and the uploaded tree come from one enumeration and cannot drift — a manifest entry with no file behind it would be a 404 that breaks the delta for everyone. Excluded from both: `*.pdb`, `createdump.exe`, `*.dll.config`, `unins000.exe` (files the installer does not place, which would otherwise read as permanently "missing" on every diff). Wired into **both** paths: `.github/workflows/release.yml` (tag-triggered) and `scripts/build/build_installer.py` (the local WPF-installer build, which now also AOT-publishes the applier), with `scripts/publish-delta-to-r2.ps1` for the manual upload. `sync-release-to-r2.yml` adds the `"files"` field **only if** `files.json` actually exists on R2.
  - **Not built** (recorded in the ADR): bsdiff-style binary patching, per-file gzip, a content-addressed store, Velopack. A typical delta is already 1–4 MB; full-file replacement keyed by hash captures ~95 % of the win.
  - **Tests** (`Tests/Services/`): `DeltaUpdateEndToEndTests` stands up a real `HttpListener`, publishes two releases onto it, and drives the whole chain — fetch manifest → diff → download over the wire → verify → apply — asserting the install folder *becomes* the new release, that the unchanged runtime DLL is **not** re-downloaded, that a 404 manifest degrades to full-download-only, and that **tampered bytes on the server are rejected** with no `.staged-ok` and an untouched install. Plus `ApplyEngineTests` (happy path, hash-mismatch gate, missing `.staged-ok`, wrong target dir, path-escape rejection, rollback on a locked file, dry run, still-running abort) and `DeltaUpdateServiceTests` (diff branches, deletion rules, URL derivation, torn-state repair). **742 tests green.**
- **`reportCdnHealth` on `ResumableDownloadService.DownloadAsync`** (2234): optional, defaults to today's behaviour. The delta path passes `false` — a 404 on an app-release file says nothing about R2's health for ModsPack assets, and must not trip the `SmartCdnSelector` circuit breaker for the rest of the session.

### 🛠️ Changed (build 2234)

- **`UpdaterService.CheckForUpdatesAsync()` now returns `Task<bool>`** (2234): true means the user took the incremental route and `Application.Exit()` has been called. `MainFormPresenter.InitializeAsync` returns early on it rather than driving `CheckModsStatusAsync`/`EnableAllButtons` against a UI that is already tearing down. The startup repair sweep also moved off the UI thread (it walks the install folder).

## [2.2.14-beta] (Builds 2220–2232)

### 🐛 Fixed (build 2232)

- **HLExtract failed on fresh Windows installs with a missing-DLL error** (2232): The bundled `HLExtract.exe` and `HLLib.dll` are compiled against the **Visual C++ 2010 runtime** (`msvcr100.dll`, `msvcp100.dll`), which is not present on clean Windows 10/11 installs. When the DLLs were missing, the process crashed immediately and the app could only surface the generic *"Failed to extract pak01_dir.vpk using HLExtract"* — the actual cause (missing CRT) was invisible. Both x64 CRT DLLs are now **bundled directly** alongside `HLExtract.exe` in the portable build (csproj `Content` items) and the installer (ISS), so HLExtract works out of the box with no prerequisites.

### 🐛 Fixed (build 2231)

- **The ModsPack update check froze the whole UI for up to 3 minutes on a bad network** (2231): `CheckModsPackUpdateOnStartupAsync()` was **awaited before `EnableAllButtons()`** in both `AutoDetectAsync` and `ManualDetectAsync`, with **no `CancellationToken`** (default `ct`) and a **60-second timeout per URL** across the three-CDN hash chain (`DownloadRemoteHashAsync`) — a worst case of ~180 seconds with every button disabled, no spinner, and no way to abort. The check now runs **after** the buttons enable, fire-and-forget (started *from* the UI thread and never `ConfigureAwait(false)`, so its continuation and the update dialog still marshal correctly; the method already swallows everything, so there is no unobserved exception), carries a presenter-lifetime `CancellationToken` (cancelled in `Dispose`, and `OperationCanceledException` is no longer logged as a failure), and the per-URL budget is **10 seconds** — it is fetching a 64-byte hash file. Because the check is now concurrent with the user, it skips the prompt when `_commandInFlight` is set, so it can never stack a second modal on top of an install the user already started.
- **A hard kill mid-install could leave a "this pack is installed" marker next to a VPK that isn't** (2231): `InstallModsAsync` wrote the new `ModsPack.hash` **before** `RebuildVpkAsync` — the step that actually produces `pak01_dir.vpk`. `InstallSnapshot` rolls that back on a *handled* failure, but a process kill or crash between the write and a rebuild failure left the new hash on disk beside an old (or absent) VPK. The next check compares hashes, sees a match, and reports **up to date** on a broken install. The marker is now written **last**, only once the VPK it describes exists.
- **The update dialog's download link could fail silently** (2231): `UpdateAvailableDialogWebView`'s `openLink` handler wrapped `Process.Start` in a `catch` that only wrote to `Debug.WriteLine` — invisible in a Release build. Since those links are the *only* way to update from that dialog, a failure left the user clicking a dead button. It now routes through the existing `UIHelpers.OpenUrlWithErrorDialog` (reused, not reinvented), which reports the failure with the existing `error.openLink` key — no new locale strings. The WebView2 init `catch (Exception)` that silently collapsed *every* failure into "fall back to MessageBox" now logs first, so a real bug is distinguishable from a missing WebView2 runtime.
- **Returning users never got the ModsPack update prompt** (2231): the saved-path branch of `InitializeAsync` (the path taken by everyone who has already detected Dota 2 — i.e. precisely the users who *have* an install to update) never called the check at all; only Auto/Manual Detect did. Now that the check is non-blocking, that branch runs it too.

### 🧹 Cleanup (build 2231)

- **The app's entire auto-update pipeline was dead code and has been removed** (2231): `UpdaterService.DownloadAndApplyUpdateAsync` — with its multi-CDN download engine (`DownloadWithMultiCdnAsync`, `DownloadFromMultipleCdnsAsync`, `DownloadFromSingleCdnAsync`, `BuildOrderedDownloadUrls`, `DownloadFileAsync`), stall detection, SHA-256 gate, `ProgressOverlay` server-log wiring and `CleanupAfterUpdateAsync` — had **zero callers**, and with it both `IUpdateStrategy` implementations (`InstallerUpdateStrategy` launching the setup exe elevated, `PortableUpdateStrategy` generating a copy-and-relaunch `.bat`) were unreachable: their `ApplyUpdateAsync` was only ever invoked from the orphaned method. The shipped flow has always been **check-and-notify**: `CheckForUpdatesAsync` shows `UpdateAvailableDialogWebView` with manual download links the user opens in a browser. Deleted `IUpdateStrategy.cs`, `InstallerUpdateStrategy.cs`, `PortableUpdateStrategy.cs`, `Models/UpdateResult.cs`, `Models/ServerLogEntry.cs`, the dead half of `UpdaterService` (which drops from 1,155 to 438 lines, along with its unsubscribed `OnProgressChanged`/`OnStatusChanged` events and its dedicated download `HttpClient`), `ProgressOverlay.UpdateServerLogAsync` (last `ServerLogEntry` reference) and the now-caller-less `InstallationDetector.GetInstallPath`/`IsRunningAsAdmin` (`Program.cs` uses the unrelated `AdminHelper.IsRunningAsAdmin`). **1,374 net lines removed, no shipped behaviour change** — the same class of never-reachable duplicate as `PatchPresenter` (2228), the `StatusService` auto-refresh layer (2229) and `MainFormPresenter.DisableAsync` (2230). Note for anyone reviving it: the SHA-256 gate it contained was **opt-in** (skipped when the manifest omitted the hash) and the GitHub-API fallback never populated those fields at all, so it must not be revived without a fail-closed integrity check.
- **`CheckForNewerModsPackAsync` returned a tuple every caller had to `&&` away** (2231): it returned `(hasNewer, hasLocalInstall)` and answered `(true, false)` for a machine with **no install** — "there's something newer" about a pack that isn't installed. Both consumers (`ModsPackUpdateService`, `MainFormPresenter`) required `hasNewer && hasLocalInstall`, so the `true` was never once used. It now returns `bool` (as `docs/developer/api/services.md` already claimed), returns early — before any network call — when there is no local marker, and re-throws `OperationCanceledException` instead of swallowing it as "no update".

### 🔒 Hardened (build 2231)

- **The update dialog no longer hardcodes CDN and website URLs** (2231): `https://cdn.ardysamods.my.id/releases/` and `https://ardysamods.my.id/#download` were string literals in `UpdateAvailableDialogWebView`, bypassing the `CdnConfig`/`EnvironmentConfig` overrides every other call site honours. They now come from `CdnConfig.R2BaseUrl` and `EnvironmentConfig.WebsiteBase`. `UpdaterService`'s silent `catch` around `FileVersionInfo` (which left `CurrentBuildNumber = 0`, i.e. "never update") now logs why.

### 🧪 Tests (build 2231)

- **The install rollback and the update check had zero coverage** (2231): `InstallSnapshot` — the thing standing between a failed ModsPack install and a user left with *no* mods — was untested. Three new NUnit cases pin it: dispose-without-commit restores the previous `pak01_dir.vpk` + `ModsPack.hash` byte-for-byte and leaves no `.bak`; `Commit` keeps the new pair and drops the backups; and on a **fresh** install (no prior files) a rollback clears the slot rather than leaving a half-written VPK with a hash marker vouching for it. Four more pin `ModsPackUpdateService.CheckForUpdateAsync`: the null-installer guard, empty/whitespace paths, the **no-local-install → false** semantics established above (which returns before any network call), and that a cancelled token surfaces as `OperationCanceledException` rather than a quiet "no update". 715 tests green.

### 🐛 Fixed (build 2230)

- **Disable Mods wrote to the game folder before the download that could fail** (2230): `DisableModsAsync` trimmed the pinned `ModPatchLine` out of `dota.signatures` **first**, and only then downloaded the clean `gameinfo_branchspecific.gi` it needs to restore. When every CDN was unreachable — the *common* failure — the user was left with a stripped signature file next to a still-modded game config: the mismatched pair `StatusService.CheckSignaturesPatched` flags as an invalid (VAC-risk) patch. The signatures read and the download now both happen **before the first write**, the same ordering rule build 2228 established on the patch path, which is also why the two writes below it don't need a `FileTransaction` to be safe.
- **A failed restore reported "Mods disabled successfully"** (2230): if writing the downloaded game config threw (file locked by Dota 2 or antivirus, disk full), the `catch` only logged, execution fell through to the success log and the method returned `true` — with the **modded config still installed and mods still active**. Worse, on the *Delete Permanently* route `MainFormPresenter` took that `true` at face value and deleted `game/_ArdysaMods`, leaving a game config mounting a VPK that no longer existed. A failed restore now falls through to the delete-the-config fallback (Steam then restores a clean one, mods are off either way), and if *that* delete also fails the method returns `false` — the one state where mods genuinely are not disabled.
- **Disable could orphan a `.tmp` in the Dota 2 folder** (2230): `dota.signatures.tmp` and `gameinfo_branchspecific.gi.tmp` were written and then swapped in with `File.Replace`; a throw or a cancellation between the two left the temp file behind in the game folder — the same leak class build 2228 fixed for the patch path, on the path it didn't cover. Both writes now go through one `ReplaceAtomicAsync` helper whose `finally` guarantees the temp file never survives the call.
- **Disable failures were completely silent** (2230): `RunDisableWithOptionsAsync` ignored `DisableModsAsync`'s `false` return entirely — no toast, no dialog, only a console line the user had no reason to open. The status pill simply stayed on **Ready**, so a failed disable was indistinguishable from a no-op. Failure now raises an error toast, as does the case where mods disable correctly but `_ArdysaMods` cannot be deleted (Dota 2 still running) — previously log-only, despite being precisely the thing the user asked for.

### 🎨 UI/UX (build 2230)

- **The restart prompt after "Delete Permanently" is now an in-shell modal** (2230): it was `RestartAppDialog`, a native WinForms window with hard-coded English chrome (`⚠ RESTART REQUIRED ⚠`, `[ OK ]`, title "Restart Required") that ignored the locale catalog entirely — the last native surface left on the disable path, visibly out of place next to every other in-app dialog. It now reuses the shell's `showShellConfirm` modal (`IMainFormView.ShowShellConfirmAsync`), the same one the delete confirmation already uses two steps earlier: fully localized in all 8 languages, and offering **Restart Now / Later** instead of an OK-only box that couldn't be declined. `RestartAppDialog.cs` and `IMainFormView.ShowRestartAppDialog` are deleted with it (single caller).
- **The Disable Options dialog wears the real brand mark** (2230): its title bar drew a generic lightning-bolt glyph instead of the canonical Ardysa `A`. It now uses the `#ardysaGlyph` sprite (mirroring `Assets/logo/ardysa.svg`) like every other WebView2 surface in the app.

### 🧹 Cleanup (build 2230)

- **`MainFormPresenter.DisableAsync()` was dead code and has been removed** (2230): a second, entirely separate disable implementation — it deleted the VPK outright rather than reverting the game config — with **zero callers**. Only `DisableWithOptionsAsync` is wired to the shell's disable button. Deleting it orphaned two locale keys (`log.disable.vpkRemoved`, `mods.disable.failed`), removed from all 8 catalogs. Same class of never-reachable duplicate as the `PatchPresenter` (2228) and `StatusService` auto-refresh (2229) removals. A stale comment in `DisableOptionsDialogWebView` claiming its `Abort` result "falls back to the classic WinForms dialog" was corrected — the caller has always treated `Abort` as a cancel.

### 🧪 Tests (build 2230)

- **The disable write path had zero coverage** (2230): `ModInstallerServiceTests` exercised only the empty-path and invalid-path guards — nothing that actually touches a file. Five new NUnit cases pin the new logic: `TrimAfterDigest` drops `ModConstants.ModPatchLine` while keeping everything through `DIGEST:` (keeping one line too many is exactly the mismatched-pair state 2228's red patch button exists to surface), and returns null when there is no digest line; and `ReplaceAtomicAsync` overwrites an existing file, creates a missing one, and — the invariant that matters — **leaves no `.tmp` behind and does not touch the target** when the write throws.

### 🧹 Cleanup (build 2229)

- **StatusService's dead auto-refresh layer removed** (2229): `StatusService` carried a 30-second refresh timer, an internal `FileWatcherService` (watching the VPK/gameinfo/signatures), `OnStatusChanged`/`OnCheckingStarted` events, a status cache and `ForceRefresh`/`Refresh`/`GetCachedStatus` — **none of which ever ran in the shipped app**: the only entry point, `MainFormPresenter.StartAutoRefresh()`, had zero callers. Live update detection has always been `DotaPatchWatcherService`'s job (ADR-0006), so this was a second, never-started watcher waiting to double-fire if anyone ever wired it. Deleted the whole layer: `StatusService` is now a stateless checker whose interface is exactly one method, `GetDetailedStatusAsync` (which also fixes the `string?` nullability drift between interface and implementation). Also deleted the now-orphaned `Core/Services/Mods/FileWatcherService.cs`, the caller-less `UI/Helpers/StatusUIHelper.cs` (WinForms `Label`-based updater from the pre-WebView shell), the legacy `record StatusResult`, and `StatusColors`' unused button-color members. The per-check `new DotaVersionService(...)` inside `CheckBuildVersionAsync` was hoisted to a readonly field. No shipped behaviour changes — every deleted path was unreachable.

### 🔧 Refactor (build 2229)

- **One StatusService instance, DI-sourced** (2229): three instances existed — `new StatusService(...)` in `MainFormPresenter`, another in `NavigationPresenter` (each constructing its own never-started internal file watcher), and the DI-registered one that `MainFormFactory` resolved and passed into `MainFormWebView`'s constructor, where it was **silently ignored**. The DI instance now flows `MainFormWebView → MainFormPresenter → NavigationPresenter` (constructor injection, per the architecture rules), which also makes the "Build changed" log-dedupe state actually shared instead of per-copy. The presenter's `CheckModsStatusAsync(force)` parameter fell out too: `ForceRefreshAsync` vs `GetDetailedStatusAsync` only differed by the now-deleted cache/events, so both branches were identical.

### 🎨 UI/UX (build 2229)

- **"Update Detected" status is now localized** (2229): `SetPatchDetectedStatus` pushed a hard-coded English literal into the status pill when the patch watcher fired — the one non-i18n string in the status pipeline. Now `Loc.T("status.updateDetected.text")`, with the key added to all 8 locale catalogs.

### 🧪 Tests (build 2229)

- **The status-determination logic is finally pinned** (2229): `StatusServiceTests` previously covered only guards and the (now-deleted) lifecycle API — zero coverage of the branches that matter for VAC safety. The rewritten fixture fabricates a minimal Dota tree in a temp directory and pins every `GetDetailedStatusAsync` branch: signatures missing → `Error` (coreMissing); VPK missing → `NotInstalled` + `Install`; gameinfo missing or unmarked → `Disabled` + `Enable`; no `DIGEST:` → `Error` (invalidCore); exact `ModConstants.ModPatchLine` after the digest → `Ready` (with version metadata); digest without the patch line → `NeedUpdate` + `Update`; and the pinned SHA-1 present **in the wrong path form** → `Error` + `Fix` — the mismatched-pair VAC-risk state that build 2228's red patch-button fix depends on surfacing.

### 📚 Documentation (build 2229)

- Developer docs described the deleted APIs (and taught the never-live `FileWatcherService` flow as if it were the watcher): `docs/developer/api/auto-patching.md`, `api/services.md`, and the `auto-patching`/`check-mod-status` skills now document `DotaPatchWatcherService` as the live watcher and the slim single-method `StatusService` surface (2229).

### 🔒 Security (build 2228)

- **Patch Update installed a game config it never verified** (2228): `UpdatePatcherAsync` writes `ModConstants.ModPatchLine` into `dota.signatures` — a line that **pins the SHA-1 of the modded `gameinfo_branchspecific.gi`** (`1A9B91FB…`). It then downloaded that gameinfo from the CDN and installed **whatever came back**, with no integrity check: any non-empty response was accepted. A stale CDN edge, a truncated transfer or a tampered response therefore produced an installed gameinfo whose real hash no longer matched the signature line the app had just written — which is precisely the mismatched-pair state `StatusService.CheckSignaturesPatched` already flags as an invalid patch with a VAC-risk comment. The two artefacts are a matched set by design, so the check is not optional hardening. Verification now lives **inside `DownloadGameInfoAsync`**, the single method all three callers route through, and is applied **per URL inside the CDN fallback loop**: a source whose payload fails verification is treated as a failed source and the next CDN is tried (a stale jsDelivr cache is exactly the drift this must survive — same class as DL_006), so integrity never costs availability. Both callers that fetch the **modded** gameinfo — Patch Update and the manual-install path, which writes the same signature line — now pass `ModConstants.ModPatchSHA1`; the disable path fetches the *clean* gameinfo, for which no canonical hash constant exists, and passes `null` (unchanged). Gated before shipping by fetching the live R2 file and confirming it hashes to the pinned constant, so the check could not brick the feature. Tagged `[AMT:OPUS]`.

### 🐛 Fixed (build 2228)

- **A failed Patch Update left an orphaned `.tmp` in the Dota 2 folder** (2228): `UpdatePatcherAsync` wrote `dota.signatures.tmp` and queued its `MoveOperation` **before** downloading the game config. When the download failed — the *common* failure, i.e. no connectivity or a dead CDN — it called `transaction.RollbackAsync()`, but nothing had executed yet (`_attemptedCount == 0`), so rollback was a documented no-op; and `Dispose` then skipped its implicit cleanup because the transaction was already marked rolled-back. The temp file was simply left behind in `game/bin/win64/`. Fixed at the root, matching the sibling manual-install path that already carried a comment explaining exactly this ordering: the signatures file is read + validated and the game config downloaded (and now verified) **before anything is written to disk**, so a failure at either step leaves the disk untouched. The `[AMT:OPUS]` atomic two-`MoveOperation` pair is unchanged — both game files still land together or not at all. A redundant second read of `dota.signatures` (once as text to locate `DIGEST:`, then again as lines) fell out of the reorder and was collapsed into one read.
- **The Patch Update button could never show its error state** (2228): `setPatchButton(status, isError)` and the `.nav-btn.patch.error` red-accent style both existed, but `UpdateButtonsForStatus` called `UpdatePatchButtonStatus(statusInfo.Status)` and left `isError` at its `false` default — so `ModStatus.Error`, the invalid-patch state above, rendered as an ordinary unremarkable button. It now surfaces.
- **The patch watcher touched the view off the UI thread** (2228): `MainFormPresenter.OnPatchDetected` is raised from `DotaPatchWatcherService`'s thread-pool debounce task and called `_view.SetPatchDetectedStatus()` (plus the notification and a status refresh) directly, while its sibling `OnStatusChanged` correctly marshals through `_view.InvokeOnUIThread`. It survived only because the WebView2 view happens to marshal internally — a latent trap for any other `IMainFormView`. Now wrapped like its sibling.

### 🧹 Cleanup (build 2228)

- **`PatchPresenter` was dead code and has been removed** (2228): `MainFormPresenter` constructed a `PatchPresenter`, subscribed to its `StatusRefreshRequested`/`PatchDetected` events and set its `TargetPath` — but **never routed a single patch action to it and never started its watcher**, so those events could not fire and the wiring was inert. Every live patch action (`patchClick` / `patchApply` / `patchVerify` / `patchViewStatus`) ran through `MainFormPresenter`'s own duplicate methods against its own `DotaPatchWatcherService`. The two copies had already drifted: the real byte-level file-verification logic (VPK size, gameinfo marker, signatures checks) existed **only in the unreachable one**, and the `Disabled`-status re-patch prompt likewise. Deleted `UI/Presenters/PatchPresenter.cs`, `Core/Interfaces/IPatchPresenter.cs`, the `AddPresenters()` DI registration, the remnant field/construction/subscriptions in `MainFormPresenter`, and `Tests/Presenters/PatchPresenterTests.cs` (which exercised only the dead path). No behavioural change — the removed code was unreachable. `_patchDialogDismissedByUser` is kept: it is genuinely used elsewhere; only its reset via the dead event is gone. **ADR-0004 amended** (the `PatchPresenter` half of that decomposition was never completed and is now withdrawn; `NavigationPresenter` is unaffected) and **ADR-0006 corrected** — its architecture diagram named `PatchPresenter` as the owner of the watcher lifecycle, which was never true in shipped code; `MainFormPresenter` owns it.

### 🧪 Tests (build 2228)

- Four new NUnit cases in `ModInstallerServiceTests`: a malformed `dota.signatures` (no `DIGEST:` line) fails the patch **and leaves no `.tmp` behind** and does not modify the original file (pins the leak fix); a known-answer test on the SHA-1 helper (uppercase hex, no separators) plus a changed-payload mismatch; and a guard that `ModConstants.ModPatchSHA1` stays a well-formed 40-char hash **and is actually embedded in `ModPatchLine`** — a typo there would silently reject every CDN source and block all patching.

### 🐛 Fixed (build 2227)

- **Main shell — the onboarding guide's active step dot was invisible** (2227): `main_shell.html` referenced `var(--accent)` in six places (the spotlight border and its corner brackets, the guide card's top rule, the active progress dot's background and glow, and the ghost button's hover border) but **never defined it** — `:root` only declared `--accent-dim` and `--warn-accent`. An undefined custom property makes the whole declaration invalid, so the active dot rendered transparent and the accented borders fell back to `currentColor`. Defined `--accent: #ffffff`, the same value `progress.html` and `settings_form.html` already use.
- **Main shell — switching language wiped the version label and mod status text** (2227): `#version` and `#status-text` carry a `data-i18n` key only for their *placeholder* text ("Checking…", "Not Checked"), but `applyI18n` re-runs on every `CultureChanged` and rewrites `textContent` on **every** `[data-i18n]` element. Changing the language from Settings therefore reset both back to their placeholders while the status dot kept its live colour — a status pill that said "Not Checked" in green. The host now takes ownership of an element on its first dynamic write (`claimFromI18n` drops the attribute), the same "snapshot, don't re-translate" rule the failure-modal log clone already followed.
- **Main shell — Enter could turn a deliberate Cancel into a Continue** (2227): the global `keydown` handler mapped Enter → `resolveConfirm(true)` unconditionally while the confirm modal was open, so a keyboard user who tabbed to **Cancel** and pressed Enter got **Continue** — on the modal that gates destructive actions (the Skin Selector beta gate and friends). Enter now defers to the focused button when focus is inside `#confirm-modal`, and only falls back to the default confirm action otherwise. The countdown lock is unchanged.
- **Main shell — Escape didn't close the install failure/complete card** (2227): every other surface (confirm modal, What's New, onboarding, patch menu) handled Escape; the failure/complete modal only offered backdrop-click and the ✕. It now closes on Escape, and its Close/Done button takes focus when the card opens so Enter dismisses it too.
- **Main shell — a stale tooltip lingered over "Checking…"** (2227): `showChecking()` updated the status dot and text but left the previous status' `title` on both, so hovering the checking state showed the *old* status' description. Both titles are now cleared.

### 🎨 UI/UX (build 2227)

- **Main shell — toasts pause while you're reading them** (2227): a toast auto-dismissed on a fixed timer regardless of interaction, so a longer message could vanish mid-sentence. Hovering a toast now holds it open and leaving re-arms the full timeout.
- **Main shell — the banner carousel no longer advances while the window is hidden** (2227): the auto-advance interval ran unconditionally, so returning to a minimised or backgrounded window landed you mid-way through a slide you never saw start. It now skips the flip while `document.hidden` (the same pause the bilingual nav fade already does). A carousel slide whose CDN image fails also hides the broken-image glyph instead of showing it, matching the install card's existing `onerror` guard.

### 🎨 UI/UX (build 2225)

- **Skin Selector's "Show Log" now matches Miscellaneous's — an in-shell console, not a native popup** (2225): The completion alert's Show Log button (added 2223) opened a native WinForms dialog (`ErrorLogDialog`), a visibly different style from every other in-app surface. It's now an in-shell HTML modal — same `.log-dialog`/`.log-modal-console` markup, monospace retro-terminal scanline texture, and Copy/Close actions as the Miscellaneous log modal — rendered directly in the WebView2 shell instead of a separate OS window. The sanitized run log is pushed into the page via `setGenerationLog(...)` before the completion alert shows, rendered as per-line text nodes (never `innerHTML` — the log is plain text, so this stays XSS-safe even though the content already passed through the service-side sanitizer). The alert-dismissal timeout re-arm (added 2223, so a user reading the log doesn't get the gallery closed out from under them) now keys off a `generationLogOpened` bridge message instead of a native-dialog round trip — same guarantee, one less WebView2↔native hop. `ErrorLogDialog` drops the neutral `[ LOG ]` variant added in 2223 (dead code now that the Skin Selector log lives in HTML) and reverts to error-only, matching its one remaining caller (the failure dialog).

### 🐛 Fixed (build 2224)

- **Misc generation could destroy your installed mod package instead of replacing it** (2224): `VpkReplacerService.ReplaceAsync` — shared by Misc (both modes) and the Skin Selector — overwrote the deployed `pak01_dir.vpk` with a raw `File.Copy(overwrite:true)` and no backup. If Dota 2 or antivirus held the file open, the copy threw mid-write and the previous package was already gone, with nothing to roll back to. It now renames the current package aside to `.bak` **before** copying in the new one: a lock makes that rename fail cleanly (nothing destroyed yet) and the run reports "the current mod package is locked — close Dota 2 and try again"; a failed copy restores the `.bak`; a successful run deletes it. Superhidden (Hidden+System) state still rides through correctly. The Skin Selector path also silently swallowed this exact failure message via an empty log sink — it now reaches the console there too. Three new `VpkReplacerServiceTests` pin the locked-destination path, that no `.bak` survives a successful run, and that a stray `.bak` from an interrupted prior run is cleaned up rather than accumulating.
- **Misc "Generation Failed" hid the one place you could see what failed** (2224): the failure alert calls the same `hideProgress()` as success, which also hid the **Show Log** button — it was gated to success/warning outcomes only, so on an actual failure there was no way to reopen the run's console. The gate now includes error outcomes (the log content survives being hidden; it's only cleared by the next run), and the failure message points at "Show Log" instead of "the console below," which had already been hidden by the time the user read it.
- **Misc generation mode modal could dead-end for 60 seconds — or forever** (2224): the wait handle for the mode picker was created *after* `showModeModal()` ran, and on timeout the C# side gave up silently while leaving the JS modal open — a user who clicked a mode after the timeout posted into an abandoned wait and nothing happened. Dismissing via the modal's scrim or Cancel button had the same problem: they closed the modal without telling C#, so the wait sat out the full 60 seconds. The wait handle is now created before the modal opens, a timeout now closes the modal from the C# side too, and scrim/Cancel now report a null mode so the app unblocks immediately instead of hanging.
- **Misc "Generate Only" silently swallowed its replace-failure cause and its download warnings** (2224): the clean-generation path passed an empty log sink to the VPK replacer (the exact bug already fixed on the "Add to Current" path) and to the base-file extractor, so failures there produced no diagnostic detail and a cold-cache first run looked frozen for minutes with the progress bar stuck at "Preparing base files." It also discarded per-mod download warnings that "Add to Current" already surfaces, and treated a failed signatures/gameinfo patch as a silent debug-only log line while still reporting plain success — the game could end up half-patched with no indication anything was wrong. All three now surface: the replacer's real failure message reaches the console, a "downloading base files — first run can take a few minutes" line explains the initial stall, and both the patch failure and dropped download warnings are now returned as user-visible `Warnings` the UI already knows how to render.
- **A redundant 2-second sleep on every "Add to Current" generation** (2224): a fixed `Task.Delay(2000)` before installing was meant to let the VPK recompiler release its file handle, but the recompiler already awaits process exit and does its own exclusive-open readiness check, and the replacer polls again before copying — the sleep was pure dead time on every run. Removed.

### ✨ Added (build 2223)

- **Skin Selector — "Show Log" on the completion card + a sanitized, shareable run log** (2223): Generation now keeps a per-run trace (`GenerationReport` buffers every step and returns it on the new `OperationResult.LogLines`), and the in-shell completion alert gains a ghost **Show Log** button next to OK — shown only when a log exists, and **OK never opens it**. The button opens a copyable log dialog (the existing `ErrorLogDialog` gained a neutral monochrome `[ LOG ]` variant — no new dialog class); on a *failed* generation the same trace is appended to the error dialog under `--- Generation log ---` automatically, since there you want it without an extra click. **Privacy boundary**: every line entering the report passes a sanitizer — absolute Windows paths and full URLs are masked down to their trailing file name (`C:\Users\Bob\...\set.zip` → `…\set.zip`, CDN URLs → `…/set.zip`), so the shown log, the copied log, and the saved `generation_report_*.txt` never leak the Windows username, machine layout, or CDN host/bucket structure; the downloader also stopped printing the local cache path and rewords a decrypt failure generically ("Set file invalid — it will be re-downloaded on retry") with the real cause going to the diagnostic logger only. **Minimalist by design** (iterated twice): intro lines whose outcome line already proves the step ran are gone ("Preparing a clean base copy", "Downloading set…", "Extracting…", "Loading latest package…"), the three duplicate per-hero priority lines collapsed and then — with layer order, merge counts, and block overrides — moved to `Debug.WriteLine` (VS Output only), and timestamps/durations were dropped from the report entirely; what remains is one line per fact that localizes a problem (`Using cached set`, `Download complete (…)`, `Integrity verified`, `Patching complete: 5/5 applied`). One real edge handled: the alert's 60-second dead-callback timeout would have fired while a user read the log and closed the gallery underneath the open dialog — each log view now re-arms the timeout (a button click proves the JS bridge is alive), while a genuinely dead callback still times out as before. `hero.log.*` keys added in all 8 languages; NUnit pins the sanitizer (paths/URLs/warnings), the debug channel's not-forwarded behavior, and the presenter's log-passing paths.

### 🐛 Fixed (build 2222)

- **Misc "Add to Current" silently unhid a protected mod package** (2222): Build 2221 marks the deployed `pak01_dir.vpk` Hidden+System when a Skin Selector generation bundles encrypted sets. But the Misc **Add to Current** mode rebuilds the *existing* package — the encrypted-derived hero content stays inside — and called `ReplaceAsync` with the default `hideOutput:false`, stripping the attributes on every Misc run. `MiscGenerationService` now reads the current VPK's attributes right before replacing and preserves the superhidden state. Clean-mode generation is untouched **on purpose**: it builds from a pristine base with no hero content, so becoming visible again is correct.
- **Extracted plaintext set models were left in plain sight** (2222): 2221 superhid the *decrypted zip's* temp dir (`ArdysaSelectHero/dec`) but not `ArdysaSelectHero/HeroSets`, where that zip's **extracted models** actually land — the more valuable target. The parent work folder is now superhidden too (one `HideDirectory` covers every set extracted under it).
- **`FileTransaction` overwrite operations failed over Hidden/System destinations** (2222): Win32 `CopyFile` fails with `ACCESS_DENIED` when the destination is Hidden (or System/ReadOnly), and `FileMode.Create` over a hidden file throws `UnauthorizedAccessException` — and with a superhidden `pak01_dir.vpk` now a *legitimate* on-disk state, any `FileTransaction` write targeting it would die mid-transaction. `CopyOperation`, `WriteContentOperation`, `WriteTextOperation` and `ReplaceFileOperation` now clear destination attributes **after** taking their pre-overwrite backup; because the backup copy carries the original attributes, **rollback restores the superhidden state as well as the bytes** (pinned by test, not assumed). `MoveOperation` needs nothing — it renames the destination aside first, and rename is attribute-agnostic. Today's installer paths were already safe by design (`InstallSnapshot` renames the live VPK aside before anything writes), so this closes the boundary for every *future* caller rather than a live crash. Three new `FileTransactionTests` pin copy-over-hidden, write-over-hidden, and rollback-restores-attributes.

### 🔒 Security (build 2221)

- **Encrypted-asset generations now superhide their output and work folders (Hidden+System)** (2221): A folder marked `Hidden` alone reappears the moment "Show hidden files" is ticked; `Hidden|System` ("superhidden") keeps it invisible even then — Windows treats it as a protected OS file — while staying fully readable by the app, by Dota, by the Explorer address bar and by Search. New `SafeTempPathHelper.HideDirectory` applies exactly that (best-effort, never throws), and the three model classes that hand-rolled a `Hidden`-only hide (`HeroExtractionLog`/`MiscExtractionLog`/`ModPriority`) now call it instead — upgraded for free. The folders that hold **decrypted plaintext** during a run are superhidden (`AssetCipher`'s decrypt dir, `HeroGenerationService`'s per-run temp root), and when any downloaded set is an AES-GCM container (`HeroSetDownloaderService` surfaces this via a new synchronous `onEncryptedDetected` callback), the deployed `game/_ArdysaMods/pak01_dir.vpk` itself is superhidden via `VpkReplacerService.ReplaceAsync(hideOutput:true)` — which **always clears attributes before its `File.Copy` overwrite**, because copying over a Hidden+System file throws `UnauthorizedAccessException` and would have broken every second generation. Generations without encrypted sets leave the VPK visible. **This is a casual-copy deterrent, not copy protection** — anyone with the path can copy the file, and a mounted VPK is extractable with the very tools AMT ships (the DRM analog-hole); real anti-clone remains the ADR-0011 track. NUnit `SafeTempPathHelperTests` + `VpkReplacerServiceTests` pin the attribute flags, idempotency, and the un-hide-before-copy regression.

### 🐛 Fixed (build 2220)

- **Install ModsPack — a failed install could DELETE the user's working mod pack when Dota was running** (2220): `InstallSnapshot.Capture` renames the live `pak01_dir.vpk` + `ModsPack.hash` aside to `*.bak` so the incoming install writes fresh files, and restores them on any failure/cancel. But the `File.Move` in `Capture` was wrapped in a `try/catch` that only logged — if the move **failed** (the classic case: Dota 2 is open and holds a lock on the VPK), capture silently continued as if it had backed the file up. The install then proceeded, and on the *next* failure the rollback path ran `TryDelete(_vpk)` with **no `.bak` to restore from** — destroying the only copy of a working install, while the UI still claimed "your previous install was restored." Fixed by tracking per-file whether the backup move actually succeeded (`_vpkCaptured`/`_hashCaptured`, set only *after* `File.Move` returns): rollback now deletes/restores **only** files it genuinely captured, and leaves an un-captured pre-existing file untouched rather than deleting the user's last good copy. **Not covered by a direct regression test** — `InstallSnapshot` is a private nested type only reachable through a full networked install, and simulating a locked-VPK capture failure needs `InternalsVisibleTo`; the fix is four guarded lines.
- **Install ModsPack — a stalled GitHub releases lookup hung the install at "Checking version…" for up to 5 minutes** (2220): `TryGetModsPackAssetUrlAsync` (the release-asset resolver) issued its GitHub API `GetAsync` on the bare shared `HttpClient`, whose only ceiling is the 5-minute default `Timeout` — every sibling network call in this service already caps itself (60s hash, 2-min index). It now runs under the same 60-second linked-token budget, and the budget covers the **response-body read** (`ReadAsStreamAsync`/`ParseAsync`) too, not just the headers — the exact hole fixed for `DownloadRemoteHashAsync` in 2217. A user cancel during the lookup now propagates as `OperationCanceledException` (surfacing as a neutral "canceled" instead of the generic "Could not reach the download server").

### ⚡ Performance (build 2220)

- **Install ModsPack — the multi-hundred-MB package was SHA-256 hashed twice** (2220): For the HTTP download path, `ResumableDownloadService` already verifies the completed file against the published `ModsPack.hash` **before** promoting it into place (added 2217), then `InstallModsAsync` re-opened and re-hashed the very same file a second time as a "verify" step — several seconds of pure disk read on every install for a check that just passed. The redundant re-hash now runs **only** on the local-copy path (`file://`/rooted URL), which never went through the downloader's verify. Both gates still exist; neither is weakened.
- **Skin Selector / Install — ~15 ms of dead sleep per patched item block** (2220): The `PatchWithMergedBlocksAsync` loop (shared by ModsPack rebuild *and* Skin Selector generation) ended each iteration with `await Task.Delay(1, ct)` purely to yield. On Windows the default timer resolution rounds a 1 ms delay up to ~15 ms, so a hero pack with hundreds of blocks spent **seconds** asleep in the timer queue. Replaced with `await Task.Yield()` — same cooperative yield, cancellation is already checked at the top of the loop, and the per-block cost drops to microseconds.

### 🧹 Cleanup (build 2220)

- **Dead code in `UpdatePatcherAsync`** (2220): the `alreadyPatched` flag was computed (allocating a substring of the entire `dota.signatures` file via `Substring(digestIndex)` + `Contains`) and then **never read** — the method always performs a full patch. Removed.

## [2.2.13-beta] (Builds 2214–2219)

### 🔒 Security (build 2219)

- **Hero set assets are now encrypted at rest on the CDN (AES-256-GCM)** (2219): Every `Assets/models/**` set archive on R2 shipped as a **plain zip** — anyone who knew (or guessed) a URL could pull the raw mod assets straight out of the CDN, no app required, which is exactly the clone/leech vector ADR-0011 is about. Set archives are now published as authenticated containers and decrypted only inside the app. New `Core/Services/Security/AssetCipher.cs`: whole-file AES-256-GCM with the layout `magic "AME1"(4) | version(1) | nonce(12) | tag(16) | ciphertext`, plus `Core/Services/Security/EmbeddedAssetKey.cs`, which reconstructs the 32-byte master secret at runtime by XOR-ing two fragments (never a contiguous literal in the binary) and derives the actual AES key **per file** as `HMAC-SHA256(masterSecret, cdnAssetPath)` — so a recovered file key reveals nothing about the master secret or any other file's key, and every zip is encrypted under a different key. GCM's auth tag means a wrong key or a tampered container **throws** instead of yielding garbage that would later fail as a confusing "extraction failed". `HeroSetDownloaderService` decrypts to a temp zip, extracts from it, and **deletes the plaintext in a `finally`** (never cached to disk). The decrypt step sits **after** the existing SHA-256 download gate — the R2 manifest hashes the *encrypted* bytes, so the download/verify/CDN-fallback pipeline (ADR-0003/0010) is untouched. **Rollover-safe:** files without the `AME1` magic pass straight through as plaintext, so a client can read encrypted and not-yet-encrypted assets simultaneously while R2 is being re-uploaded — no flag day, and old clients keep working until the plaintext copies are replaced. Key material and the packaging side live outside this repo (`ModsPack/scripts/gen-asset-secret.ps1` emits the fragments + the gitignored `.asset-secret`; `encrypt-assets.ps1` re-encrypts the dataset and re-keys the hash manifest on the encrypted bytes) — **the fragments checked in here are non-functional placeholders**, and `AssetCipher.Encrypt` exists as the reference implementation the script must stay byte-identical to. New `Tests/Services/AssetCipherTests.cs` (NUnit) pins the round-trip, the container header, wrong-key and tampered-ciphertext rejection, plaintext pass-through detection, and that a decrypted temp file actually opens as a zip. **Note:** this is obfuscation-grade confidentiality, not DRM — the key ships in the client and a determined attacker with the binary can recover it. It raises the cost of *scraping the CDN* from "curl a URL" to "reverse the app", which is the threat that was actually being exploited; the ADR-0011 endgame remains server-issued tokens (swap only `EmbeddedAssetKey.GetMasterSecret()`).

### 🐛 Fixed (build 2218)

- **Miscellaneous generation — every failure was undiagnosable ("Generation Failed / Extraction failed.")** (2218): Two independent layers of silence made the Misc pipeline unable to report *why* anything broke. **(1) The whole pipeline ran with a null logger.** `MiscController` built its services with `new MiscGenerationService()` / `new MiscCleanGenerationService()` and passed **no `IAppLogger`**, so the null propagated into `VpkExtractorService`, `AssetModifierService`, `VpkRecompilerService` and `VpkReplacerService` — making **every `_logger?.Log(...)` in the entire Misc chain a no-op**, including all the `VpkException` error codes (`VPK_001`, `VPK_005`, `VPK_008`) those services already took the trouble to raise. The diagnostics were being written; they went nowhere. **(2) The generation service swallowed its sub-services' console sink.** `MiscGenerationService.PerformGenerationAsync` passed `_ => { }` as the `log` callback to the extractor, recompiler and replacer, so their *user-facing* reasons ("Package missing after extraction.", "Extraction tool not found.", "Extraction process failed: …") were discarded too. The only survivor of both layers was the generic `Fail("Extraction failed.")`, which is what every user and support thread saw. Fixed at the root: new `Core/Services/Logging/FileAppLogger.cs` (an `IAppLogger` over the existing `FallbackLogger` → `ardysa_fallback.log`) is now the **DI default** for both Misc generation services instead of `null`, and `MiscController` threads one logger through the whole chain — which switches on every error code already present in those services without adding a single new log statement. The extractor and replacer now receive the real `log` sink (VPK-tool chatter still goes to the log file only, matching what `MiscCleanGenerationService` already did), and every `Fail(...)` carries a machine-readable code.
- **`VpkExtractorService` — HLExtract's exit code and stderr were captured and then thrown away** (2218): `RunProcessAsync` resolved `proc.ExitCode` into a `TaskCompletionSource<int>` whose value was **never read**, and drained stderr in a fire-and-forget `Task.Run` that reported to the (null) logger — so a failing HLExtract "succeeded", and the run only died later at the `items_game.txt` post-check with a message that named the wrong thing. It now returns `(ExitCode, StdErr)`; both feed the diagnostic log and are folded into the failure message. The exit code deliberately does **not** fail the run on its own (HLExtract has been observed returning 0 while writing nothing, so it is not trusted as a success signal) — it is evidence, and the content checks remain the gate. Also closes a real hole: an extraction that produced **no files at all** is now a failure regardless of exit code, which previously slipped through for callers passing `requireItemsGame: false` (an empty directory was accepted as a successful extraction). Both output pipes are now drained concurrently, and the TCS uses `RunContinuationsAsynchronously`.

### ✨ Added (build 2218)

- **Miscellaneous "Add to Current" — self-heals a damaged mod package instead of dead-ending** (2218): When the existing `pak01_dir.vpk` can no longer be extracted, `MiscController` now **automatically rebuilds it from a clean base** via the existing `MiscCleanGenerationService` (same selections, pristine `Original.zip` + the live `items_game.txt`) and returns a warning telling the user their hero sets need re-applying — instead of failing and instructing them to manually re-run in "Generate Only" mode. This is the exact gap the old error fell into: `VerifyExistingVpkAsync` gates on an **index listing** (`HLExtract -p … -l`, header + directory tree only), so a package whose tree is intact but whose **file-data section** is truncated/corrupt passes verification and then fails the full extract. Its contents are unrecoverable at that point, which makes a rebuild strictly better than a dead end. New `OperationResult.ErrorCode` (`Core/Exceptions/ErrorCodes`) lets callers branch on **why** something failed rather than string-matching user-facing prose, and the recovery is deliberately narrow — `MiscController.ShouldRebuildClean` fires **only** on `VPK_001` from a package that already passed the origin-marker check (i.e. one we own and may replace), and **never** on cancel, on conflict resolution, or on a modify/recompile/replace failure (a rebuild would just hit those again after burning a second multi-GB extraction cycle). The "VPK wasn't created by ArdysaModsTools" path is untouched on purpose: silently overwriting a third-party tool's VPK stays a user decision. New `Tests/Services/MiscSelfHealTests.cs` (NUnit) pins the full truth table. The Misc failure dialog now also shows the error code and points at the console + `ardysa_fallback.log`. **Note:** the recovery *decision* is unit-tested; the rebuild *execution* is an unmodified call into the already-shipping clean-generation path and has not been driven end-to-end against a genuinely corrupt package.

### 🐛 Fixed (build 2217)

- **`FileTransaction` — a failed or cancelled operation could DESTROY the file it was supposed to protect** (2217): The most serious bug in this batch, found by a new regression test rather than a bug report. `MoveOperation.ExecuteAsync` moves the destination aside to `*.transaction_bak` **first**, then moves the replacement in — so a failure *between* those two steps leaves the destination **gone**. But `FileTransaction.ExecuteAsync` only incremented `_executedCount` **after** an operation completed successfully, so the operation that threw was **excluded from the rollback range** and its half-applied state was never undone: the live `dota.signatures` / `gameinfo_branchspecific.gi` was left as an orphaned `.transaction_bak` and the real file simply vanished. Fixed at the root — the counter is now `_attemptedCount`, incremented **before** the operation runs, so a partially-applied operation is always rolled back (every `IFileOperation.RollbackAsync` is state-driven via its own `_existedBefore`/`_created` flags and is safe to call on a partial or never-started op). Two further holes closed in the same engine: `RollbackAsync` is now **idempotent** (`_rolledBack` guard) — `ExecuteAsync` already rolls back before rethrowing *and* every caller rolls back again in its `catch`, and that second pass was moving the just-restored backup back out to the temp path, deleting the destination a second way; and `ct.ThrowIfCancellationRequested()` moved **inside** the try, so cancelling *between* operations also rolls back instead of leaving the earlier ones applied. This engine backs `UpdatePatcherAsync` too, so the fix is not limited to the install path. New `Transaction_SecondPatchWriteFails_RestoresBothGameFiles_EvenWhenRolledBackTwice` in `FileTransactionTests` models the real gameinfo+signatures pair and pins all three invariants.
- **Install ModsPack — manual install could leave the game half-patched** (2217): `ManualInstallModsAsync` wrote `gameinfo_branchspecific.gi` and `dota.signatures` with two **independent** `File.Replace`/`File.Move` calls. If the signatures write failed, gameinfo was already swapped with no rollback, while `InstallSnapshot` (which guards only the VPK + hash) rolled the VPK back — leaving a new gameinfo, stale signatures and a reverted VPK, i.e. a Dota that can't load mods. Both writes now go through a **single `FileTransaction`** (`MoveOperation` × 2) exactly like `UpdatePatcherAsync`: either both land or neither does, with rollback on any failure or cancel. The signatures file is also validated (`DIGEST:` present) **before** anything is written, so a malformed core file fails fast without leaving stray `.tmp` files.
- **Install ModsPack — cancelling an install reported "Installation Failed"** (2217): `InstallModsAsync`/`ManualInstallModsAsync` swallow `OperationCanceledException` and just return `false`, so a user-cancelled install was indistinguishable from a real crash and opened the red **failure card** — while the report line underneath said "Installation canceled.", contradicting the title. The presenter now checks the cancellation token and returns `OperationResult.Canceled()` (the `WasCanceled` flag already existed), and a cancel surfaces as a neutral info **toast** with no failure card, no patch prompt. New `mods.toast.canceled.*` keys in all 8 locales.
- **Install ModsPack — double-clicking Install could start two concurrent installs** (2217): The re-entrancy guard was dead code — `_ongoingOperationTask` was declared but **never assigned**, so `IsOperationRunning` was permanently `false` and nothing ever checked it. Worse, the buttons are only disabled by `StartOperation()`, which runs **after** the feature-access gate and the install-method dialog; those modal dialogs pump the message loop, so a second queued `install` message from the shell re-entered and ran a concurrent install over the same game files. Replaced with a real `_commandInFlight` guard held for the **whole** command (dialogs included) on `InstallAsync` and `DisableWithOptionsAsync`.
- **Install ModsPack — a hung HLExtract froze the install forever** (2217): `ModInstallerService.RunHlExtractAsync` awaited process exit with **no timeout** (unlike `ModsPackDataService`, which has a 20-minute guard), so a wedged HLExtract hung the install with no way out — cancellation never reaches it. Now bounded by the same 20-minute cap (kill + `FallbackLogger` on timeout), with stdout/stderr drained concurrently to avoid a full-pipe deadlock.
- **Install ModsPack — a corrupt CDN copy wasn't rejected at the download boundary** (2217): `ResumableDownloadService.DownloadAsync` supports an `expected` SHA-256 that verifies the file **before** promoting it into place (and falls through to the next CDN on mismatch), but the installer never passed it — even though the published `ModsPack.hash` was already fetched. It's now passed; the existing post-download hash check stays as belt-and-braces.
- **Smaller hardening** (2217): the 60-second per-URL budget in `DownloadRemoteHashAsync` now covers the **response-body read** as well (it used the outer token, so a stalled body could hang past the timeout); the three `Progress<T>` callbacks in `ProgressOperationRunner` (`status`/`substatus`/`percent`) are `async void` under the hood and now `try/catch` like `speedProgress` already did — an exception from an overlay closing mid-report would otherwise crash the process; removed the dead `signaturesBackup` variable in `UpdatePatcherAsync` (declared and deleted-on-cancel, but never actually written).

### ♻️ Refactor (build 2217)

- **Install/disable logic single-sourced; dead duplicate presenter deleted** (2217): `ModOperationsPresenter` held a **complete but never-invoked** copy of the install/reinstall/manual-install/disable flow — the WebView shell only ever calls `MainFormPresenter` (`_presenter.InstallAsync()` / `DisableWithOptionsAsync()`), and `MainFormPresenter` used its composed `_modOperations` only to sync `TargetPath`. The two copies had already drifted (the live one carries the `FeatureAccessService` gate, the ModsPack update check and the post-install patch prompt; the dead one didn't), and the 2215–2216 card work had to be applied to **both**, which is exactly the double-maintenance trap. Deleted `UI/Presenters/ModOperationsPresenter.cs`, `Core/Interfaces/IModOperationsPresenter.cs`, its DI registration and its test fixture; `MainFormPresenter` is now the single source of truth for install/disable. Relocating the *live* path onto the decomposed presenter (the ADR-0004 direction) was considered and rejected: it would have meant re-wiring the 5-second shutdown gate (`_operationGate`/`ShutdownAsync`), the feature-access gate and the patch prompt through events on the most safety-critical path in the app, for no runtime gain. ADR-0004's `ModOperations` split is therefore **not** completed — it was never wired up in the first place.

### ✨ Added (builds 2215–2216)

- **Install ModsPack — install report shown in result cards** (2216): New `InstallReport` (`Core/Services/Logging/InstallReport.cs`, static session-scoped collector like `FallbackLogger`, thread-safe, `[AMT:PRO]`) records a **curated, user-simple log** of every install run — `ModInstallerService` and `ModsPackDataService` write plain milestone/outcome lines at each stage: "ModsPack downloaded (245.3 MB).", "Download verified.", "Hero database loaded (126 heroes).", "Hero: Abaddon (#3)" (friendly name + block count per customized hero), "Package data applied.", "Installation completed successfully.". **Every failure branch now writes an actionable plain-language reason** — connection ("Download failed — check your internet connection and try again"), corrupted download ("The downloaded package was corrupted — please try again"), disk/AV ("Could not unpack the download — check free disk space and antivirus"), Steam core files ("verify game files in Steam"), rollback reassurance ("your previous install was restored"), cancel (amber "Installation canceled."). Lines never contain URLs, file paths, or internal identifiers — raw diagnostics (`[CdnFallback] Success: https://…`, `[DEBUG] Processing ID 454 for npc_dota_hero_abaddon`) stay in `FallbackLogger`/Debug output. The complete/failed cards render the report (`lines:[{t,c}]` payload, color-coded via the console's log-line classes) instead of the shell-console excerpt; the console excerpt remains only as a fallback when no report exists. New `Tests/Services/InstallReportTests.cs` (NUnit — note: the Tests project is NUnit 4, not xUnit as CLAUDE.md claims) covers ordering/categories, blank-line filtering + snapshot isolation, and concurrent writes.
- **Install ModsPack — completion card with Done + Show Log** (2216): Auto/Manual/Reinstall success now opens an in-shell **completion card** (green accent, check icon, "Complete" eyebrow) instead of a transient toast: the completion message with **Done** (primary) and **Show Log** actions — Show Log expands the install report in place (with Copy Log), Done closes. New `IMainFormView.ShowInstallCompleteCard` → JS `showInstallComplete`; both complete and failed cards share one modal + opener (`openInstallLog(title, body, variant, withLog)`) in `main_shell.html`. Falls back to the styled success message when the shell isn't on-screen. New `shell.failure.eyebrowOk` / `shell.toast.showLog` keys in all 8 locales.
- **Install ModsPack — failure card with the actual reasons** (2215): Install/reinstall/manual-install failures now open an in-shell **failure card** (red accent, error/warning log excerpt visible immediately, Copy Log + Close) via the new `IMainFormView.ShowInstallFailureCard` instead of a 5-second error toast that just said "check the console". Falls back to the styled error dialog when the shell isn't available. New `shell.failure.*` keys in all 8 locales. (Build 2216 upgraded its log source from the console excerpt to the curated install report above.)

### 🐛 Fixed (build 2214)

- **Performance Tweak — Medium preset blacking out the map on unit hover** (2214): Applying the **Medium** preset on the Performance tab wrote `cl_globallight_shadow_mode 2` (global shadows on) together with `lb_shadow_texture_width_override 128` / `lb_shadow_texture_height_override 128` (a fixed, tiny shadow atlas) — the only preset combining the two. Hovering any unit/NPC spawns a highlight light whose shadow render overflows that undersized atlas, so the deferred lighting pass reads as fully shadowed and the whole map goes black (matches [ValveSoftware/Dota-2#1723](https://github.com/ValveSoftware/Dota-2/issues/1723) and [Dota-2-Vulkan#328](https://github.com/ValveSoftware/Dota-2-Vulkan/issues/328)). Every other preset avoided this: Low/Potato/Competitive keep `cl_globallight_shadow_mode` at 0–1, High/Ultra pair mode 2–3 with auto shadow textures (`-1`). Medium's `cl_globallight_shadow_mode` (`Assets/Html/dota2_performance.html`, `PRESETS.medium`) is now `1` — the same shadow block Competitive already ships — keeping Medium's FPS/quality character with no field reports against that combo. New `Tests/Services/PerformancePresetInvariantTests.cs` parses the shipped preset data and asserts, across every preset, that `cl_globallight_shadow_mode >= 2` always pairs with auto (`-1`) shadow textures — pins the exact bug class against regression. Users who already applied the old Medium preset must re-apply it (or press **DELETE**) to clear the bad line from their `autoexec.cfg` — the fix only affects new applies.

## [2.2.12-beta] (Builds 2212–2213)

### 📝 Documentation (build 2213)

- **Added `CLAUDE.md` contributor/agent guide** (2213): Repo-root guide capturing the AMT ground rules — MVP layering, the new-service sequence, DI-only, mandatory tests, the file-safety subsystem rules (`FileTransactionService`, `ILogService`, CDN fallback order, Security), `[AMT:TIER]` annotations, grounding/anti-hallucination, and the commit format. No code change.

### ✨ Added (build 2212)

- **Settings — Dota 2 install-path card + auto-detect control** (2212): Settings gains a top **Dota 2** section showing the current install path (left-truncated so the meaningful `…\dota 2 beta` tail stays visible, full path on hover) with a **Change** button and an **Auto-detect Dota 2 Path** toggle. *Change* opens the folder picker **at the current path** (`IDetectionService.ManualDetect(initialPath)`) for quick re-pointing and runs a full re-sync — config, `PatchWatcher`, mod status, button states — via the new `MainFormPresenter.ChangeTargetPathAsync`, which deliberately **skips** the post-detect install / ModsPack-update prompts (those would stack a main-form dialog behind the Settings modal and read as a hang). It returns `(null,false)` on cancel/invalid, `(path,false)` when the same folder is re-picked (still re-synced, "Path unchanged" toast), `(path,true)` on a real change ("Dota 2 path updated" toast). The toggle persists to the new `IConfigService.AutoDetectOnStartup` (default true). Settings window widened 420→540px. New `settings.dota.*` / `settings.autoDetect.*` / `toast.autoDetect.*` / `toast.dotaPath.*` keys in all 8 locales.
- **Auto-detect Dota 2 on startup when no path is saved** (2212): When there's no saved path and `AutoDetectOnStartup` is on, `MainFormPresenter.InitializeAsync` now runs the same flow as the **Auto** button instead of dropping straight to detection-only buttons — the app finds and attaches the install on first launch with no manual step. An already-saved/manually-set path is never overridden.
- **Transient "Dota 2 path found" confirmation banner** (2212): A green banner (`showPathFound`, auto-hides after 4s) confirms the attached path on a fresh auto-detect, manual detect, or path change — but not on a silent saved-path reload. Buffered via `Js()` so a banner requested during early startup replays once the shell is ready.

### 🛠️ Changed (build 2212)

- **Auto Detect grays out once a path is attached; Manual Detect stays live** (2212): `EnableAllButtons` now sets `Auto = !hasPath` — once a Dota 2 folder is attached, Auto Detect is done and goes non-clickable (re-enables when no path is attached). Manual Detect stays enabled on purpose so the user can always re-point the app (multiple installs, moved folder, wrong auto-pick). The stray unconditional `EnableDetectionButtonsOnly()` in `MainFormWebView` startup was removed — button state is owned by the presenter's terminal call, and re-setting it here defeated the gray-out after a successful auto-detect. A cancelled/failed Manual Detect now restores the state matching the current path (`RestoreButtonsForCurrentPath`) instead of un-graying Auto.
- **Failed auto-detect explains the next step** (2212): When startup auto-detect finds nothing (or throws), a localized `log.autoDetect.notFound` warning now points the user at the highlighted detect buttons instead of silently falling back.

## [2.2.11-beta] (Builds 2204–2211)

### ♻️ Refactor (build 2211)

- **WebView2 shell-load readiness extracted + hardened** (2211): The inline readiness block in `InitializeWebViewAsync` (build 2210) is now a dedicated `LoadShellAndWaitReadyAsync` with a single documented contract. Same DOMContentLoaded-not-onload behaviour, plus three hardening points: (1) **fail-fast on a genuine navigation error** — it also subscribes `NavigationCompleted` and, on `IsSuccess == false`, throws with the `CoreWebView2WebErrorStatus` immediately instead of waiting out the full timeout (near-impossible for a local `NavigateToString`, but now correct if it ever happens, restoring the error detection 2210 dropped); (2) **both event handlers are always detached** via `finally`, on every success/error/timeout path — no dangling subscriptions; (3) the `TaskCompletionSource` uses `RunContinuationsAsynchronously`, so the continuation (DOM pushes, possibly a nested WebView2 dialog) never runs inside the event callback stack — the same reentrancy guard the app's other WebView2 TCS round-trips use. The 30s cap is now a named `ShellLoadTimeout` documented as a hang guard, not an image-load budget. No behaviour change for the healthy path.

### 🐛 Fixed (build 2210)

- **WebView2 — false "navigation timed out / please reinstall the runtime" on slow-CDN regions** (2210): The shell-load gate waited on `CoreWebView2.NavigationCompleted`, which fires on `body.onload` — *after every subresource loads*, including the CDN images the shell pulls through `WebViewAssetInterceptor`. Where the CDN is slow or blocked, those images pushed `onload` past the 15s cap, so `InitializeWebViewAsync` threw `TimeoutException("WebView2 navigation timeout")` and told the user to reinstall the runtime — even though WebView2 was perfectly healthy and had already rendered. The gate now waits on **`DOMContentLoaded`** (HTML parsed + the shell's inline scripts ready — the actual JS-bridge contract), which does **not** block on images, so late-loading images no longer fail launch. The timeout is now only a genuine-hang guard, raised to 30s to cover slow first-run render-process startup on low-end/HDD machines with aggressive AV. Root cause is the readiness signal, not the runtime — this replaces build 2209's runtime-detection work for the users who were still hitting a timeout.

### 🐛 Fixed (build 2209)

- **Portable — "WebView2 runtime required" (or WebView2 errors) when the runtime is already installed** (2209): The portable build declared the runtime missing on machines where it was present. Two root causes, both fixed. (1) The launch gate used a bare `CoreWebView2Environment.GetAvailableBrowserVersionString()` probe, which *throws* when the native `WebView2Loader.dll` can't load (AV-stripped, Mark-of-the-Web block on an extracted zip, or RID-less resolution) — a throw is not "not installed". A new `Core.Helpers.WebView2Runtime.Detect()` runs in `Program.Main`, falls back to the EdgeUpdate registry (per-machine WOW6432Node/native + per-user hives, rejecting empty/`0.0.0.0` `pv`) when the probe throws, and logs `source`/`version`/`diag` to `startup_log.txt` so the real cause is captured; only a genuine absence now blocks launch. The duplicate exit-on-missing check in `MainFormFactory` is removed. (2) `FolderProfile.pubxml` now publishes a **self-contained single-file win-x64** portable build (was framework-dependent Any-CPU): the framework-dependent build forced users to have the .NET Desktop Runtime and resolved `WebView2Loader.dll` via RID-less probing — the portable-only failure. `IncludeNativeLibrariesForSelfExtract=false` keeps `WebView2Loader.dll` loose next to the exe. Output moved to its own wiped-clean `portable\` dir (gitignored), satellite resource folders dropped. A post-startup WebView2 init failure now also persists the real exception to `startup_log.txt` via `StartupLog.Append` (was a release-invisible `Debug.WriteLine`), and the init-failed dialog text points users at Unblock → antivirus → runtime reinstall.

### 🐛 Fixed (build 2208)

- **Install ModsPack — corrupt cached `Original.zip` no longer wedges every install** (2208): The extract fast-path only self-healed on a *missing* `pak01_dir.vpk`; a `pak01_dir.vpk` that was present but corrupt (truncated/partial download) failed HLExtract on every run and left the bad zip in place, so the install could never recover without a manual cache wipe. On extract failure `OriginalVpkService` now purges all three cached inputs — the extract dir, the unzipped VPK dir, and `Original.zip` itself — so the next run re-downloads a clean zip (Q&A VPK_001). Also gitignores the QA bot's local `scripts/qa_bot/qa.db*` question database.

### 🔒 Security (build 2207)

- **Manual install — origin verification moved to a hidden marker; content fingerprint reverted** (2207): Build 2206's content fingerprint was reverted in favour of a **hidden origin marker**. The verdict is now decided by the presence of a single, deliberately obscure path — `materials/dev/deferred_light_cache.vtex_c` — buried among a real pack's thousands of `materials` entries so it can't be spotted and copy-pasted to forge Official. It lives outside `scripts/items` and `resource/localization`, so it survives the client rebuild (`StripBundledData`) and one marker covers the slim release and every rebuilt install. The old `version/_ArdysaMods` path is now a **decoy**: it never grants Official in `ClassifyVpkAsync`, though `ValidateVpkAsync` (Misc "Add to Current") still honours it (and the new path) so packs installed before the marker moved keep working without a reinstall. The `Modified` verdict is gone — a pack is **Official** (marker present) or **Unofficial** (absent); `ClassifyVpkAsync` now returns `(VpkOrigin, bool NeedsRebuild)`, with `NeedsRebuild` true only for a **slim** official pack (no `scripts/items/items_game.txt` inside — the release form needing the `modspack_index` + localization rebuild), so self-contained official/generated packs install as-is. Whole classification is now one HLExtract listing — no extraction, no network: `VpkFingerprint`, `pack_fingerprints.txt` fetch, `BuiltInPackFingerprint`, and the generalized `DownloadRemoteTextAsync` are all removed. This is obscurity, not crypto (the marker string ships in the binary); the strong fix was deliberately traded away for zero network dependency and no false-Modified verdicts on shared packs. `ClassifyVpkTests` rewritten for the two-verdict + rebuild model.

### 🔒 Security (build 2206)

- **Manual install — copied-marker forgery closed by content fingerprinting** (2206): Build 2205's origin check hashed the `version/_ArdysaMods` marker's content — but the marker is public content, so copying the genuine file into any third-party `pak01_dir.vpk` classified it **Official**. The verdict is now anchored to a **content fingerprint** (`VpkFingerprint`): the VPK's own directory tree (lowercase path + CRC32 per file, VPK v1 and v2) is read directly from the tree section — no extraction, sub-second even on multi-GB packs (~380k-entry vanilla pack parses in <1s) — filtered of the rebuild-replaced entries (`scripts/items/items_game.txt`, `resource/localization/*`, mirroring `StripBundledData`), sorted, and SHA-256'd. Only byte-identical official content matches, so the one fingerprint covers the release's slim VPK and every user's rebuilt install alike. Known-good fingerprints come from `remote/pack_fingerprints.txt` (R2 → jsDelivr → GitHub, append-only, 8s per-URL cap) plus a built-in constant for offline. Marker presence now only separates Ardysa-derived (Official|Modified) from third-party (Unofficial) and can never grant Official by itself; if the tree is unparseable, the HLExtract listing fallback caps a marker-bearing pack at **Modified**. Consequence (by design): shared Skin Selector-generated packs and misc-modified installs classify Modified — they aren't the official release. The marker-content-hash machinery (`TryGetMarkerHashAsync`, `remote/marker.hash`) is removed; release ops append one fingerprint line per ModsPack release (printed by the `[Explicit]` test `ClassifyVpkTests.PrintOfficialPackFingerprint`). New regression test forges a pack embedding the byte-exact genuine marker → must classify Modified.

### ✨ Added (build 2205)

- **Manual install — VPK origin verification (Official / Modified / Unofficial)** (2205): A user-provided `pak01_dir.vpk` is now classified before install by `ModInstallerService.ClassifyVpkAsync` → `VpkOrigin`. The identity anchor is the SHA-256 of the `version/_ArdysaMods` marker's **content** — not the whole VPK, whose hash changes on every client-side rebuild; the rebuild pipeline never touches the marker, so its hash is stable across auto-install rebuilds and Skin Selector repacks. Known-good hashes come from `remote/marker.hash` (R2 → jsDelivr → GitHub, one hex hash per line so the marker can rotate without stranding old releases) with a built-in constant as offline fallback; the fetch is capped at 8s per URL because it runs before the confirm modal with no progress UI, and the result is cached for the session. Verdicts: **Official** (marker matches) → normal confirm + full rebuild pipeline; **Modified** (marker present but content differs — tampered or cloned) → yellow-accent shell modal with a "not our responsibility" disclaimer, rebuild on consent; **Unofficial** (no marker — previously rejected outright, now accepted) → same disclaimer modal, then installed **as-is** (`ManualInstallModsAsync rebuild:false`), because the official `modspack_index` overlay must not be applied to foreign packs; **Unreadable** → existing "Invalid VPK" error. New locale keys `mods.unofficialVpk.*` / `mods.modifiedVpk.*` in all 8 locales; wired in both `MainFormPresenter` and `ModOperationsPresenter`. Forged-pack tests (`Tests/Services/ClassifyVpkTests.cs`) cover all four verdicts using the bundled `vpk.exe`.

### 🛠️ Changed (build 2205)

- **Marker presence check — exact path match** (2205): `ValidateVpkAsync` and `ClassifyVpkAsync` now share one `ListingContainsMarker` helper that matches the HLExtract listing line **exactly** against `root\version\_ardysamods` (handles the trailing-dot formatting of extensionless entries, separators, case), replacing the old two-substring check (`"version"` + `"_ArdysaMods"` anywhere in the tree) that any pack with lookalike filenames could spoof. If the marker is listed but its bytes can't be extracted and hashed, the verdict is **Modified**, never Official — nothing is trusted without a verified hash. A non-zero HLExtract exit now counts as unreadable instead of its error text being parsed as a listing.

### 🐛 Fixed (build 2204)

- **WebView2 — corrupted profile no longer blocks the app** (2204): `CoreWebView2Environment.CreateAsync()` can throw when the EBWebView profile folder is corrupted or stale-locked — typically after a force-close or crash interrupted a write. The bad state sits on disk, so a reboot or runtime reinstall doesn't help. `CreateEnvironmentInternalAsync` now catches the failure, renames the profile folder to `<folder>.corrupt-<timestamp>` (atomic move clears the path even when a leftover file can't be deleted), then retries. If the reset can't clear it (a live `msedgewebview2.exe` still holds the lock), the error is rethrown and surfaced to the user.

## [2.2.10-beta] (Builds 2200–2203)

### 🐛 Fixed (build 2203)

- **Installer — "install WebView2" prompt when the runtime is already present** (2203): Setup decided whether WebView2 was installed from an EdgeUpdate registry key only, while the app decides from `CoreWebView2Environment.GetAvailableBrowserVersionString()`. The two could disagree (runtime provided by a non-standalone channel/path, or a per-user install the key check missed), so setup would offer to install a runtime the app could already see. `PrerequisiteChecker.IsWebView2Installed` now uses that same authoritative API as the source of truth and falls back to the registry only if the probe can't run, so setup and app never disagree. The registry fallback also now checks each hive independently and rejects a `pv` of `""`/`0.0.0.0` (the value a broken/uninstalled runtime leaves behind). The legacy Inno `IsWebView2Installed` (`ArdysaModsTools.iss`) got the matching registry hardening — its `else if` chain previously skipped later hives when an earlier key existed empty. `EnsureWebView2Async` also no-ops cleanly when no bootstrapper is embedded; the Evergreen bootstrapper it runs is idempotent, so a stray run after a false negative is harmless. Distribution is unchanged (still the embedded Evergreen bootstrapper).

### ✨ Added

- **Install ModsPack — remote feature gate** (2201): The **Install ModsPack** action (auto, manual, and reinstall) is now gated by `config/feature_access.json` alongside Skin Selector and Miscellaneous, so it can be paused from R2 during maintenance. New `installModsPack` config key + `FeatureAccessService.InstallModsPackFeature`; single gate at `MainFormPresenter.InstallAsync`. Fail-open (enabled if the config can't be fetched).
- **Feature-unavailable notice — in-shell, with Join Discord** (2201): The "disabled by remote config" notice is now the shell modal (`UIHelpers.ShowFeatureUnavailableAsync`) instead of a separate WebView dialog, with a yellow accent variant and a **Join Discord** button linking the community server. Retired `FeatureUnavailableDialog` + `feature_unavailable.html`; new locale key `feature.unavailable.joinDiscord`.

### 🐛 Fixed

- **High-DPI / UI Size — cropped pages, modals, and cards** (2201): At Large/Extra Large UI size a window could grow past the monitor and get clamped smaller while its WebView zoom stayed full, cropping content. `DpiLayout` now caps the effective scale so `baseSize × scale` fits the monitor and applies that one scale to both zoom and window, keeping the CSS viewport invariant (fits at Normal → fits at every size). `main_shell` modals/content scroll (`safe center` + `overflow-y`) as a fallback.
- **Manual-install card cropped + drag-drop zone** (2201): The install-method dialog sized its manual card by a hardcoded value that ignored UI Size, cropping it when scaled — now scales with `CurrentUiScale`. Drag-drop is fixed for real files: the invisible drop overlay uses alpha 1 (alpha 0 was skipped by drag hit-testing → "blocked" cursor) and its bounds account for UI Size; drag messages are allowed through UIPI process-wide (`Program.cs`) so drops work when the app runs elevated. Positioning is guarded against dialog close mid-await.
- **Install — cancel/failure restores the previous working install** (2202): Replaced the delete-based `RemoveBrokenInstall` with a rename-based `InstallSnapshot` that backs up the existing `pak01_dir.vpk` + `ModsPack.hash` before the rebuild and restores them on failure/cancel (or clears the slot for a first-time install). Users no longer lose their working mod pack when an install attempt fails. Applies to both the auto-install and manual-VPK paths.
- **ModsPack rebuild always starts vanilla** (2202): `ModsPackDataService.StripBundledData` now deletes any bundled `scripts/items/items_game.txt` and `resource/localization/` from the extracted pack before the rebuild, so a legacy fat pack or a stale slim pack is always repacked with the live game items_game + modspack_index overlay + R2 localization. Previously a fat pack's stale items_game survived and skipped the rebuild entirely.

### 🛠️ Changed

- **Feature gate — quieter + clearer copy** (2201): Removed the `[ACCESS] … disabled by remote config` console lines (the modal already says it). The close-Dota banner now reads "Dota 2 is still running — tools are unavailable until you close the game." across all 8 locales.
- **Install — always rebuild VPK, remove fat-vs-slim branch** (2202): The auto-install and manual-install paths no longer call `VpkBundlesItemsGameAsync` to skip the rebuild for "fat" (self-contained) packs — every install now goes through the full strip → rebuild pipeline. The fat-pack branch (`VpkBundlesItemsGameAsync` + its `TryListVpkContentsAsync` caller doc comment) is removed. This guarantees a deterministic output regardless of what the download bundled.

### ✨ Added (build 2200)

- **ModsPack build — live progress percentage** (2200): Rebuilding the self-contained VPK (the "Building game data…" step) now drives a real progress bar instead of a static line that looked frozen. `HeroSetPatcherService.PatchWithMergedBlocksAsync` reports an `onBlockDone(done, total)` callback as each package block is applied, `LocalizationPatcherService.PatchLocalizationAsync` reports `onFileDone(done, total)` per localization file, and `ModsPackDataService.RebuildVpkAsync` maps the phases onto the overlay bar (extract → items_game refresh → index download → **block patching 35–75%** → localization 75–90% → VPK build → install) with the status text showing `Applying package data… N%` (throttled to 1% steps).
- **Shell — Utilities nav entry (placeholder)** (2200): A new **Utilities** item appears under a new **Extra** sidebar group, shipped visibly disabled with a `SOON` tag until the page is built. Localized (`shell.group.extra`, `shell.nav.utilities`, `shell.tag.soon`).

### 🐛 Fixed (build 2200)

- **Install — cancel/failure no longer leaves a half-built pack** (2200): Canceling an install (or a rebuild/data failure) now removes the freshly copied `_ArdysaMods/pak01_dir.vpk` and its `ModsPack.hash` marker, so Dota 2 can't load an incomplete package and a retry runs a full reinstall instead of reporting "up to date". Applies to the auto, reinstall, and manual-VPK paths (`ModInstallerService.RemoveBrokenInstall`). The auto path only arms this after the copy transaction commits, so a mid-copy cancel still rolls back to the previous working VPK. Generated-VPK replacement also drops the stale hash (`VpkReplacerService`).
- **Installer — a custom install path could delete user folders** (2200): The install path is now normalized to always end in an `ArdysaModsTools` subfolder (`InstallerService.NormalizeInstallPath`), and every recursive delete (install cleanup, uninstall, self-deletion) refuses drive roots and well-known user/system folders (`UninstallService.IsDangerousDeleteTarget`). Previously, browsing to or typing `Desktop`/`Documents`/a drive root would scatter files there and a later uninstall would `rmdir /s` that folder as administrator.
- **Installer — corrupt payload no longer destroys a working install** (2200): The previous version is now removed only **after** the new payload is extracted and verified, inside `ExtractPayloadAsync` (`previousInstallPath`), instead of before extraction. A failed/corrupt download leaves the existing install intact.
- **Installer — silent uninstall no longer flashes the window** (2200): Removed `StartupUri` from `App.xaml`; `OnStartup` now shows `MainWindow` explicitly only on the interactive path, so Windows Add/Remove Programs' quiet uninstall (`--uninstall --silent`) runs with no UI as intended.
- **Installer — second instance crash on exit** (2200): The single-instance guard now tracks mutex ownership and only calls `ReleaseMutex` when owned, fixing an `ApplicationException` thrown at shutdown when a second installer was launched.
- **Installer — self-deletion after uninstall now waits reliably** (2200): The post-uninstall cleanup script is written as a real temp `.bat` (batch `GOTO` labels don't work in an inline `cmd /c "…"` string), so it actually polls until the process exits before deleting the uninstaller and install folder instead of degrading to a single ~1s wait.
- **Installer — Update vs Reinstall detection** (2200): The new-version read now uses the installer exe's `FileVersionInfo` — the same source as the installed-app read — instead of mixing `AssemblyVersion` and `FileVersion`, so mode detection no longer depends on both being stamped identically. Installer version stamp aligned to `2.2.10.2200`.

### 🛠️ Changed

- **Install/generate status routing** (2200): `ManualInstallModsAsync` gained a `statusCallback` (interface + all callers), so manual, auto, and reinstall paths mirror their live status to the progress overlay (`MainFormPresenter`, `ModOperationsPresenter`).
- **Logging — quieter ModsPack build** (2200): Dropped the "rebuilt `<path>` with N blocks + localization" log line and the block count from the overlay; the patcher reports just "Patching complete." on a clean run, keeping the `X/Y applied, Z skipped` detail only when something was skipped (full counts still go to the debug logger).
- **Shell — clearer "Dota 2 running" warning** (2200): The close-Dota banner now reads "dota2.exe is still running — close Dota 2, or end it in Task Manager → Background processes" and is mirrored to the console (`log.dota2.running`) so the disabled-buttons state is explained in the session log.
- **Installer — faster app shutdown & safer extraction** (2200): `ProcessHelper` now force-closes immediately when the app has no main window (tray/hidden) instead of stalling the full 5-second timeout; payload extraction gained a zip-slip guard; and the installer confirms before closing while an install/uninstall is in progress.

## [2.2.9-beta] (Builds 2195–2199)

### ✨ Added

- **Startup — region/CDN connectivity notice** (2197): The shell console now shows a highlighted heads-up on every launch: if Downloads or Generate get stuck it's usually a CDN connection issue for that region, with a suggestion to retry, switch networks, or use a VPN (free Cloudflare WARP recommended). Emitted via the localized log pipeline (`log.notice.regionCdn`, new `notice` console category) and translated across all 8 locales, so region-affected users can self-diagnose instead of assuming the app is broken.

### 🐛 Fixed

- **Skin Selector & Miscellaneous — generation no longer freezes/crashes on dialog round-trips** (2199): Fixed a hard crash (freeze + force-close, exit code `0x80000003` / `STATUS_BREAKPOINT`) that hit generation whenever a WebView2 dialog confirmed through a JS round-trip — most reliably a Skin Selector preset with a **Base override and no set** selected on any hero (which shows the base-no-set confirm dialog). The dialog `TaskCompletionSource` ran its continuation **inline on the native WebView2 message-callback stack**, so the next WebView2 (preview form / progress overlay) was spun up inside that native callback and tripped WebView2's fail-fast. The affected sources (`_confirmBaseNoSet`, `_alertDismissed`, `_modeSelected`) now use `TaskCreationOptions.RunContinuationsAsynchronously` so continuations post to the message loop instead. The crash was independent of which set was chosen.
- **Skin Selector — new sets generate immediately** (2196): After uploading a new set, generation no longer hard-fails while its cloud index is still being synced to R2. `HeroGenerationService` now falls back to the `index.txt` bundled inside the downloaded set zip (`ResolveIndexTextAsync` / `FindBundledIndex`) when the cloud index isn't available yet, so freshly uploaded sets work for users without waiting for the separate index-sync step.
- **Skin Selector & Miscellaneous — Generate no longer blocked by thumbnail caching** (2196): Capped the full-screen thumbnail caching overlay at 30 seconds. On slow or blocked CDN paths the overlay previously waited on the entire download batch with no time limit, leaving users with a cold cache unable to click **Generate ModsPack** / **Generate** (it presented as "only some people" because it depended on each user's CDN reachability). The overlay now releases after the cap while downloads finish in the background and thumbnails load on demand (`RunDownloadWithOverlayAsync` / `PreloadMiscThumbnailsAsync`).

### 🗑️ Removed

- **Classic WinForms fallback shell** (2195): Retired the legacy `MainForm` shell and its classic-only controls (`RetroTerminal`, `RoundedPanel`) plus the GDI onboarding overlay. WebView2 is now **required** — the modern shell is the only interface.
- **Dead code sweep** (2195): Removed ~10,000 lines of unreferenced code — orphaned dialogs/controls (`MiscModeDialog`, `ModernSearchBox`, `MiscGenerationPreviewForm`, `PrioritySettingsDialog`, `PriorityManagerPanel`), dead services (`HeroCacheHelper`, `UserSettingsService`, `SecureConfig`), the old panel-based MVP cluster (Action/Detection/Status panels + presenters) and their unit tests, and dead members (`ToNonGeneric`, `UpdatePatcherAsyncLegacy`, `ArchivePassword`).
- **Unused dependency** (2195): Dropped the `SixLabors.ImageSharp` package — `.webp` is decoded natively by WebView2.
- **Dead code sweep, pass 2** (2198): Removed ~2,770 more lines of unreferenced code with zero behavior change. Deleted never-called classes (`MemoryCache`, `FileHelper`, `NetworkHelper`, `StringProtection`), a built-but-unwired dialog (`PatchRequiredDialogWebView`), a production `DeleteOperation` whose only caller was its own test, and the empty `Core/Services/Meta` folder.
- **Unreachable classic dialog fallbacks** (2198): Now that WebView2 is a hard startup requirement, the legacy WinForms twins that could only run on a WebView2 init failure were retired — `StatusDetailsForm`, `DisableOptionsDialog`, and `InstallMethodDialog` (+ their Designer/resx). The `InstallMethod` enum moved onto the WebView2 dialog and `DisableOptionsDialog`'s two-value option enum collapsed into a `DeletePermanently` bool; a mid-session dialog init failure now cancels cleanly instead of opening a legacy dialog.

### 🛠️ Changed

- **Startup** (2195): `MainFormFactory` now always builds the WebView2 shell; if the Edge WebView2 Runtime is missing it shows the localized `program.webview2Required.*` message (added across all 8 locales) and exits cleanly instead of degrading to the classic UI.
- **Logging** (2195): Simplified `Logger` — removed the `RetroTerminal` constructor and its unused typewriter output path (`RichTextBox` and WebView console sinks unchanged).
- **File transactions** (2198): Inlined the single-product `FileTransactionFactory` (+ `IFileTransactionFactory`, `IFileTransaction`) — `AutoexecService` constructs `FileTransaction` directly, matching `ModInstallerService`. Replaced the hand-rolled `Guard` helper with .NET 8 `ArgumentNullException.ThrowIfNull` / `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual`.

## [2.2.8-beta] (Builds 2188–2194)

### ✨ Added

- **Skin Selector — cloud set index** (2189): Read the items_game set index from the cloud by zip hash instead of bundling it.
- **Skin Selector — generation tracking** (2191): Surface skipped/failed sets in the completion dialog and live log, and save a per-run generation report (last 10 kept).
- **Miscellaneous — more categories** (2192): Enable the cursor, ancient, roshan, announcer, and mega_kills zip-merge categories; added a Show Log modal to the completion dialog.
- **Support — daily snooze** (2193): "Don't show this today" checkbox snoozes the on-launch support prompt for the day (the manual Support button is unaffected).
- **Miscellaneous — Clear button** (2193): Reset all options to default, synced across the JS/C# state and the saved selections file.

### 🛠️ Changed

- **Mods — Manual VPK Install** (2188): Reverted Manual Install to VPK-only, removing `.zip` extraction support.
- **UI/UX — Install Method Card** (2188): Restricted the file dialog filter and drag-and-drop handler to `.vpk` files only.
- **UI/UX — Confirm Generation modal** (2190): Rebuilt the Skin Selector confirmation as a shell-style WebView modal.
- **UI/UX — Confirm Install modal** (2194): Replaced the Confirm Install prompt with an in-shell WebView modal (falls back to a native box on the classic form).
- **Logging** (2191): Say "package" instead of `items_game.txt` in user-facing logs.

### 🐛 Fixed

- **UI/UX — Double File Dialog** (2188): Fixed importing a VPK via the WebView manual install card double-prompting with a second file dialog.
- **Skin Selector — thumbnails & crash** (2190): Fixed set/item/persona/base thumbnails and a generate `STATUS_BREAKPOINT` crash.
- **Miscellaneous — block ID collisions** (2192): Anchor short block IDs by prefab so 202/586/801 resolve the real item instead of a collision.
- **UI/UX — Manual VPK drag-drop** (2194): Fixed drag-drop via a transparent layered overlay drop target over the dropzone, accepting a single `.vpk` only.

## [2.2.7] (Builds 2184–2188)

### ✨ Added

- **Dota 2 Performance — full multi-language support**: Fully localized the Performance Tweak page (categories, CVars, dropdown options, and launch option descriptions) across all 8 supported languages.
- **Dota 2 Performance — live culture updates**: Bound tweak page WebView forms to the active culture change event to allow dynamic in-place translation updates without app restart.
- **Dota 2 Performance — localized host alerts**: Migrated C# toast notifications and status banners to localized string keys.
- **Settings — database badge localization**: Added missing database status translations to non-English locale files.
- **Localization — parity unit tests**: Added `LocalizationServiceTests` to enforce catalog validation and translation parity.
- **Mods — manual VPK zip import**: Added extraction and installation support for `pak01_dir.vpk` nested inside `.zip` archives.
- **Mods — drag & drop WebView integration**: Enabled dragging and dropping `.vpk` or `.zip` files directly onto the Install Method WebView dialog.
- **UI/UX — dynamic UI zoom**: Implemented user-configurable global UI scaling (Normal/Large/Extra Large) applied on form load.
- **DPI — PerMonitorV2 alignment**: Synchronized WinForms host DPI scaling with WebView2 layout rendering to resolve display clipping at high DPI settings.

### 🛠️ Changed

- **UI/UX — Main Shell Layout**: Increased host window size from `1040x780` to `1280x780` and widened the sidebar panel from `216px` to `280px` to give navigation buttons more breathing room without changing font sizes.
- **UI/UX — Performance Tweak Icon**: Replaced the generic lightning bolt icon in the title bar of the Dota 2 Performance form with the official Ardysa brand logo (`ardysa.svg`).

## [2.2.6-beta] (Builds 2181–2183)

### ✨ Added

- **What's New — config-driven install-card image** (build 2183): The "Install ModsPack" promo-card background can now be set from the R2 `config/banner.json` manifest via a new optional `installCard` field (a CDN-relative path like `Assets/image/banner/install_card.png`, or a full URL), so the card art can be swapped without an app release. The value is cache-busted on load; when absent the shell keeps its bundled image. This complements the existing manifest-driven `modspackVersion` badge — both now live in one file (`BannerConfig.InstallCard`, surfaced via `setInstallCard()` in `main_shell.html`).
- **Dota 2 Performance — remove autoexec.cfg** (build 2183): Added a control to delete the installed `autoexec.cfg` from the resolved Dota 2 cfg folder (`AutoexecService.DeleteCfgAsync` / `IDota2PerformanceView.OnDeleteCfgRequested`). It reports success / "nothing to remove" via a toast and restores the "recommended values — nothing written yet" banner afterward.

### 🗑️ Removed

- **Miscellaneous — dead code**: Removed code with no remaining callers — the legacy `ComboBoxDataService` (hard-coded option lists long superseded by the remote `misc_config.json`), the unused `MiscModel` / `MiscRequest`, and four unused accessors on `RemoteMiscConfigService` / `MiscCategoryService` (`GetUrls`, `GetOptions`, `GetOptionsByCategory`, `GetThumbnailUrl`). The deleted `RemoteMiscConfigService.GetThumbnailUrl` also encoded a stale filename convention (`.png` + hyphen→underscore) that disagreed with the live `MiscOption.SanitizeChoice` (`.webp`, hyphens preserved), so removing it also drops a latent trap.
- **Miscellaneous — classic WinForms UI**: Removed the classic `MiscForm` and the three widgets only it consumed (`MiscRow`, `MiscTile`, `MiscSectionHeader`). `MiscFormWebView` has been the primary UI for some time; the classic form was only reachable as a WebView2-initialization fallback. If WebView2 cannot initialize, the Miscellaneous page now shows a "requires the WebView2 runtime" notice instead of maintaining a second, parallel UI.
- **Skin Selector — orphaned presenter**: Removed `SelectHeroPresenter` and `ISelectHeroView`, an unused presenter/view pair left from an abandoned refactor — the classic `SelectHero` form never implemented the interface and nothing constructed the presenter (they survived only through their own unit test). Also dropped three unused `HeroModelMapper` set-classification wrappers (`IsPersonaSet` / `IsItemSet` / `IsBaseHeroSet`).
- **Skin Selector — classic WinForms UI**: Removed the classic `SelectHero` form and the two widgets only it consumed (`HeroRow`, `TileCard`), mirroring the Miscellaneous change. `HeroGalleryForm` (WebView2) is the primary UI; the classic form was only a WebView2-initialization fallback. If WebView2 cannot initialize, the Skin Selector now shows a "requires the WebView2 runtime" notice (the `ShowClassicHeroSelector` member was removed from `IMainFormView` and both hosts).

### 🛠️ Changed

- **Miscellaneous — faster generation**: Removed ~1.5 s of artificial `Task.Delay` pacing from the generation path — seven sequential 200 ms waits between the file-based mod steps in `AssetModifierService`, plus a couple of cosmetic delays in `MiscController`. Each step already awaits its real work, so progress reporting is unchanged; the operation just no longer idles.
- **What's New — cache-busted card art** (build 2183): The bundled install-card image now loads via `data-src` with a per-launch `?t=` cache-buster, so overwriting `install_card.png` on the CDN reliably shows the new art on next launch instead of a stale WebView2-cached copy.

### 📝 Documentation

- Updated `docs/developer/architecture.md` and `docs/developer/api/ui-components.md` to reflect the WebView2-only Miscellaneous UI — replaced the stale classic `MiscForm` API section with `MiscFormWebView` and corrected the referenced HTML filename to `misc_form.html`.
- Updated the same developer docs for the Skin Selector — dropped the removed `SelectHero` / `SelectHeroPresenter` / `ISelectHeroView` / `HeroRow` / `TileCard` references and re-pointed the hero generation diagram at `HeroGalleryForm` → `HeroGalleryPresenter` → `HeroGenerationService`.

Net: ~−6,100 lines across the Miscellaneous (build 2181) and Skin Selector (build 2182) features, no dependency changes, full test suite green (688 tests).

---

## [2.2.5-beta] (Builds 2177–2180)

### ✨ Added

- **Skin Selector — style-group cover thumbnails**: A style group authored in `heroes.json` may now carry an optional `"thumbnail"` cover (a sibling of `"styles"`). When present it is shown on the collapsed **Style Card** and in the **Style Preview** header in place of an individual style's thumbnail, giving a multi-style set one representative cover. The cover is UI-only metadata (`SetStyleInfo.GroupThumbnail`) shared by every flattened style entry in the group, is never part of a set's download URLs, and is de-duplicated when preloading so an N-style group fetches its cover once. When no cover is authored the UI falls back to the first style's thumbnail (or the hero portrait) as before.
- **Skin Selector — heroes without sets are locked**: Heroes that have no sets are now greyed-out and non-selectable in the gallery instead of appearing pickable, and the **Has Sets** filter was repositioned for clearer access.
- **Multi-language support (8 languages)**: The app is now fully localizable through a single JSON catalog under `Assets/Locales/`, shared verbatim by the C# side (`ILocalizationService` / `LocalizationService` / the `Loc` helper) and the WebView side (`i18n.js` `data-i18n` bindings, injected via `WebViewLocalizer`). English (`en`) is the default and the fallback for any missing key, with **Spanish, German, French, Portuguese, Russian, Simplified Chinese, and Traditional Chinese** shipping alongside it. Date/number/currency formatting follows the active culture, and `{token}` interpolation plus `zero`/`one`/`other` pluralization are supported.
- **Language selector in Settings**: A new **Language** control under *Appearance* lets you switch language live — the shell, Settings, and the localized dialogs re-translate in place via the `CultureChanged` event, with no restart.
- **Shared `AppIconHelper`**: A single best-effort loader for `AppIcon.ico` (deployment path → dev-assets fallback) used by every window so the brand icon is applied consistently.

### 🛠️ Changed

- **Unified app icon across all windows and the tray**: The **Skin Selector** and **Miscellaneous** windows now show the Ardysa app icon in the taskbar / Alt-Tab (they previously fell back to the default form icon because they are borderless and never set one). The classic and WebView main windows now load the icon **before** creating the tray `NotifyIcon`, so the system-tray icon also matches the app icon instead of the default Windows icon. The duplicated icon-loading code paths were consolidated into `AppIconHelper`.
- **In-shell "Update Required" prompt**: The native Yes/No *Update Available* `MessageBox` shown by the Patcher when Dota 2 has been updated is replaced with the in-shell monochrome confirmation modal (`ShowShellConfirmAsync`), matching the rest of the shell. The classic form still falls back to a native box. New `patch.statusPrompt.*` locale keys (eyebrow / heading / body / confirm) were added across all 8 languages.

### 🐛 Fixed

- **Skin Selector — stale / ghost assets between runs**: Generation previously merged each run's custom set assets directly into the cached *pristine* base extraction, leaving those files behind so they bled into ("ghosted" onto) later, unrelated generations. Each run now copies the cached extraction into its own temp folder and patches that disposable copy, keeping the cache clean. The base-ready cache marker was bumped (`.ready` → `.ready2`) to force a one-time clean re-extract on existing installs.
- **Skin Selector — STR / Universal attribute icons**: Fixed the Strength and Universal attribute icons not rendering by serving them as local data URIs.
- **Tests**: Fixed unit tests failing due to missing localization assertions after migrating strings to the i18n system.

---

## [2.2.4-beta] (Builds 2171–2176)

### 🛠️ Changed

- **Skin Selector — Prismatic layer sorting**: Changed Prismatic layer sorting weight from highest to lowest. It is now applied last (on top of other cosmetic layers, last-writer-wins) so Prismatic files override other selections (Set, Items, and Base) where they overlap.
- **UI**: Disabled default right-click context menus and browser accelerator keys (such as F5 / Ctrl+R refresh, F12 developer tools) across all 17 WebView2-based panels.
- **Skin Selector — monochrome redesign**: The hero gallery and all of its modals (set picker, Style Preview, alert, confirm, caching overlay) are rebuilt on the shared monochrome terminal design system used by the main shell and [ardysamods.my.id](https://ardysamods.my.id) — black/white only, JetBrains Mono, sharp 0px corners, signature cut-corner brackets, an animated grid/wave backdrop, and a golden-ratio type hierarchy. The six set categories that previously relied on accent colors (Legacy/Custom/Persona/Items/Base/Prismatic) are now distinguished by labels, bracketed `[TAG]` badges, and weight; the active pick is shown with a white ring + corner brackets. No selection logic, element IDs, or the C#↔JS bridge changed.
- **Skin Selector — title-bar notice**: Added a scrolling announcement marquee in the title bar ("Not all heroes have a set — sets will be updated gradually"), the one deliberate accent in the otherwise-monochrome chrome.
- **Miscellaneous — monochrome redesign**: The Miscellaneous page and every overlay (option carousel, style overlay, ethereal effects, progress console, mode/alert/tribute modals, caching overlay) are rebuilt on the same design system. The window is wider, the option cards are larger and more square (the redundant change-indicator square was removed — selection now reads from the white ring), and the option preview modal is enlarged for easier browsing.

### 🐛 Fixed

- **Borderless-window drag/close crash (`0xc000041d`)**: Dragging or closing the Skin Selector / Miscellaneous windows could terminate the app with `STATUS_FATAL_USER_CALLBACK_EXCEPTION`. Both windows started the native `WM_NCLBUTTONDOWN` modal move-loop (and `Form.Close()`) directly inside the WebView2 `WebMessageReceived` callback, re-entering the native callback. They are now deferred onto a fresh message-loop turn via `BeginInvoke`, matching the main shell's working drag path.

---

## [2.2.4-beta] (Build 2170)

### ✨ Added

- **Skin Selector — Prismatic layer**: A new **Prismatic** category (sets whose archive filename starts with `prismatic_`) is now available per hero. Prismatic is a pure asset overlay — it ships no `index.txt` and contributes no `items_game.txt` blocks; it only merges its texture/model files. It is applied **first as the foundation** (strictly highest layer weight), so every other selection — Set, Items, and Base — overrides its files wherever they overlap. Prismatic coexists with your Set/Items/Base picks and only one can be active at a time. The selection is persisted with your last configuration (`HeroSelectionState.PrismaticIndex`).
- **Prismatic requires a Base Hero**: Prismatic is a Base add-on. It can only be enabled once a Base Hero is selected — the category is dimmed/locked with a "Select a Base Hero first" notice until then, clearing the Base automatically drops the Prismatic, and a stale selection carrying a Prismatic without a Base is dropped during plan building so the rule holds end-to-end (UI → presenter → generation).

### 🐛 Fixed

- **Misc generation progress bar stuck at 60%**: The Misc generation progress bar previously stalled at 60% because the build/install/finalize phase messages matched none of the progress keywords. The phase-to-percentage mapping now covers the full pipeline (Validating → conflict check → Preparing/Extracting → Applying/Fetching → Building/Recompiling → Installing → Finalizing → Complete) and uses `Math.Max`, so the bar advances monotonically through every phase and never rewinds on out-of-order or repeated messages.

---

## [2.2.3-beta] (Builds 2161–2169)

### ✨ Added

- **Skin Selector — last selection is remembered**: Your most recent configuration (each hero's set, items, and base override) is now saved when you generate and automatically restored the next time you open the Skin Selector, so your previous picks are highlighted and visible again. Previously only which heroes were highlighted was remembered, not their actual selections.
- **About → Built With**: Added a tech-stack row (C#, Python, HTML5, JavaScript) above the existing library credits.
- **Startup countdown**: The Support dialog shown on launch — and the **Continue** button on the Skin Selector beta notice — now have a short countdown before they can be dismissed/confirmed, so the notice isn't skipped instantly.

### 🛠️ Changed

- **In-app notifications**: Native Windows message boxes and tray balloons are replaced with notifications styled to match the app shell — an in-window confirmation modal (Skin Selector beta notice and the permanent "delete mod files" confirmation) and toast notifications for patch results (**Patch Complete** / **Already Up To Date** / **Patch Failed**), shown inside the main window instead of as separate dialogs.
- **Branding**: The main window title bar and the About page (header and identity mark) now use the Ardysa logo in place of the previous glyph.
- **What's New card**: Now features the Ardysa logo, with the logo, title, and subtitle centered within the card.
- **Install ModsPack card**: The download icon is now a solid black tile with a white glyph for stronger contrast against the banner.

---

## [2.2.2-beta] (Build 2160)

### ✨ Added

- **`scripts/check_cdn_fallback.py`**: A CDN-fallback asset auditor that reproduces `CdnFallbackService` offline — it fetches `misc_config.json` + `heroes.json` from the CDN chain, expands every referenced asset (misc download URLs **and** thumbnails honoring the `thumbnailId` override, plus hero skin zips/images from `sets` and styled sets), and probes each across the full chain (R2 → jsDelivr → GitHub Raw → ghfast → gh-proxy). Reports an asset as DEAD only when it fails on **all** CDNs, printing the exact `[CdnFallback] Failed: <url> -> HTTP <code>` lines and exiting non-zero for CI. Complements `scripts/check_r2_assets.py`, which covers neither `heroes.json` nor thumbnails. Stdlib-only.

### 🐛 Fixed

- **Misc thumbnails**: The picker now honors the per-choice `thumbnailId` override in `misc_config.json`, so choices (and style sub-choices) that reuse another's art (e.g. "Crownfall" → `cavernite`) resolve to the existing CDN image instead of 404-ing on a non-existent file named after the choice. The override was previously defined in the config but read nowhere — `RemoteMiscChoice.ThumbnailId` is now bound and flows through `MiscOption.ChoiceThumbnailIds` (covering both top-level choices and nested styles) to both `MiscOption.GetThumbnailUrl` (C#) and `getThumbUrl()` (JS), which substitute the authored stem verbatim before falling back to the sanitized choice name. (Genuinely missing thumbnails with no `thumbnailId` still need uploading to R2.)

### 🛠️ Changed

- **Clear Cache**: The cache-cleaning sweep now also removes the remote misc-config cache (`%LocalAppData%\ArdysaModsTools\misc_config_cache.json`) via the new `RemoteMiscConfigService.DeleteCache()`, and `CacheCleaningService.GetCacheSizeBytes` counts it so the displayed size matches what is freed. Previously neither Clear Cache nor Disable Mods → Delete Permanently touched this file, leaving no in-app way to drop a stale/broken config that could pin bad map/misc asset URLs. The file still self-heals from the remote feed on the next successful launch.

---

## [2.2.1-beta] (Build 2158)

### ✨ Added

- **Documentation**: Added [PRIVACY.md](PRIVACY.md) to clarify that ArdysaModsTools does not collect any user data or telemetry.

### 🛠️ Changed

- **Documentation**: Added references and acknowledgments to SignPath Foundation in [README.md](README.md) to comply with code signing application requirements.
- **What's New**: The in-app changelog now reads a public, auth-free feed (`EnvironmentConfig.WhatsNewFeedUrl`, default `cdn.ardysamods.my.id/config/whatsnew.json`, override `AMT_WHATSNEW_FEED`) first and only falls back to the GitHub Releases API. This removes What's New's hard dependency on the source repo being publicly readable, so it keeps working if the repo goes private. The feed reuses the existing GitHub releases JSON shape, so `WhatsNewService.Parse` is unchanged; the feed fetch has its own short timeout so a slow/missing feed cleanly degrades to the GitHub fallback.

---

## [2.2.1-beta] (Build 2156)

### 🗑️ Removed

- **`scripts/tools/2-patch_models.py`**: Removed Mode 7 ("Name Item/Base sets from items_game.txt") and its `--vpk` option. Dropped the now-unused `item_namer` import, the `vpk_path` config, the `sets_renamed` result tracking, and the menu/CLI/dispatch wiring for the mode. The tool now offers modes 1–6.

---

## [2.2.1-beta] (Build 2155)

### 🐛 Fixed

- **Skin Selector → Latest Updates**: Styled sets (Style Cards) no longer go missing from the "Latest Updates" bar. The patch tool recorded a newly-added styled set under its bare group key (`Set N`), but the app flattens each style into a `Set N (Label)` entry — so the bar could never resolve a thumbnail for the bare key and silently hid the update. Patch Models now records the flattened representative key (`Set N (first style label)`) for styled sets, and a one-time idempotent migration repoints any pre-existing bare-group entries in `set_update.json` so currently-hidden styled updates reappear.

---

## [2.2.1-beta] (Build 2154)

### 🛠️ Changed

- **UI/UX**: In the Skin Selector hero modal, set cards no longer show a name label — each shows just a color-coded category tag badge: **Set** (Legacy), **Mix** (Custom), **Persona**, **Arcana** (Base), and the existing slot tag for Items (head/weapon/…). Style Cards follow the same rule (tag only, no name on the tile); the group title and per-variant names still appear inside the Style Preview Modal.

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
