# Changelog

All notable changes to ArdysaModsTools will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

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
