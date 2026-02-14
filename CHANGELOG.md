# Changelog

All notable changes to ArdysaModsTools will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.1.12-beta] (Build 2089)

### 📖 Documentation

- **Context7 Skills**: Added 7 `SKILL.md` files for Context7 indexing:
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
- **Enhanced** `mod-file-structure` SKILL.md with VPK validation API, gameinfo status checking, all 3 JSON schemas with CRUD examples, and error handling patterns.
- **context7.json**: Enhanced with `$schema`, folder config, exclusions, and coding rules.

---

## [2.1.12-beta] (Build 2088)

### 🚀 Features

- **ActiveModsService**: Added unified API to query currently installed mods (Heroes + Misc).
- **API Documentation**: Added comprehensive developer docs for:
   - Mod File Structure (`mod-file-structure.md`)
   - Auto-Patching Configuration (`auto-patching.md`)
   - Active Mods Querying (`active-mods.md`)
   - Misc Mods Control (`misc-mods.md`)

---

## [2.1.12-beta] (Build 2087)

### 🚀 UI/UX

- **Progress**: Improved "Preparing" phase feedback in Skin Selector.
   - Status now shows real-time download percentage (e.g., "Downloading base files (45%)") instead of static "Preparing".
   - Re-enabled substatus display in `ProgressOverlay` to show detailed log messages (download size, extraction steps).

### 📡 Network

- **Resilience**: Added stall detection to `OriginalVpkService` for base file downloads.
   - Warns user after 30s of no data ("Download appears stalled").
   - Suggests troubleshooting steps after 90s.
   - Auto-resets warning label when download resumes.

---

## [2.1.12-beta] (Build 2085) - 2026-02-12

### 🚀 Added

- **CI/CD**: Improved release workflow with **NuGet caching**, **SHA256 checksums** for artifacts, and **automatic runtime installer downloading** (only if missing).
- **CI/CD**: Added build summary to GitHub Actions and upgraded to `action-gh-release@v2`.

### 🐛 Fixed

- **Installer**: Fixed critical regression in .NET 8 detection where running in 64-bit mode caused the installer to miss 32-bit registry keys (`WOW6432Node`). Now explicitly checks `HKLM32`.

---

## [2.1.12-beta] (Build 2084) - 2026-02-12

### 🐛 Fixed

- **Installer**: Removed unnecessary .NET 8 Desktop Runtime check for self-contained builds, fixing a blocking prompt during updates.
- **Installer**: Bundled .NET 8 Desktop Runtime installer for seamless auto-installation if needed (fallback).

---

## [2.1.12-beta] (Build 2083) - 2026-02-12

### 🚀 Added

- **Feature Access Control**: Added remote feature gating system via Cloudflare R2 (`feature_access.json`).
   - New `FeatureAccessConfig` model with fail-open defaults.
   - New `FeatureAccessService` with 5-minute cache and graceful error handling.
   - Skin Selector and Miscellaneous can now be remotely enabled/disabled with custom messages.
- **UI**: Added `FeatureUnavailableDialog` — WebView2-based dialog matching `progress.html` aesthetic (animated wave, corner decorations, monospace font) with native MessageBox fallback.

### ♻️ Refactoring

- **KV Parsing**: Consolidated three duplicate KeyValues parsing implementations into single source of truth in `KeyValuesBlockHelper`.
   - Added `heroId` filtering to `ExtractBlockById` and `ReplaceIdBlock` to prevent false matches on short numeric IDs (e.g., ID `"99"` matching in `kill_eater_score_types` instead of `items`).
   - Enhanced `NormalizeKvText` with smart-quote (`""`→`""`), smart-apostrophe (`''`→`''`), non-breaking space, and zero-width character handling.
- **HeroSetPatcherService**: Refactored to delegate all KV parsing to `KeyValuesBlockHelper`, removing ~200 lines of duplicate code. Domain logic (validation, indentation normalization, file discovery) preserved.

### 🗑️ Removed

- **Dead Code**: Deleted `GeneratorService.cs` (zero references). Its superior text normalization logic was merged into `KeyValuesBlockHelper.NormalizeKvText`.

### 🧪 Testing

- Added 14 new unit tests for `KeyValuesBlockHelper`: heroId filtering (4), smart-quote normalization (2), zero-width char stripping, non-breaking space handling, BOM removal, `ParseKvBlocks` (3), `ReplaceIdBlock` with heroId (2).
- Added 16 new unit tests for `FeatureAccessConfig` model and `FeatureAccessService` (total: 285 tests).

---

## [2.1.11-beta] (Build 2082) - 2026-02-10

### ♻️ Refactoring

- **Architecture**: Decomposed `MainFormPresenter` into 3 specialized presenters for SRP (`ADR-0004`):
   - `ModOperationsPresenter` - install, reinstall, disable operations
   - `PatchPresenter` - patch updates, verification, watcher
   - `NavigationPresenter` - hero selection, miscellaneous forms
- **DI**: Created specialized registration methods (`AddCoreServices()`, `AddConflictServices()`, `AddHeroServices()`, `AddLoggingServices()`, `AddPresenters()`, `AddUIFactories()`).
- **Tests**: Added `TestServiceFactory` helper for cleaner test setup without ServiceLocator.

### 🐛 Fixed

- **Installer**: Switched `.NET 8` Desktop Runtime detection from CLI-based (`dotnet --list-runtimes`) to registry-based check for improved reliability across system configurations.

### 📝 Documentation

- **ADR**: Rewrote all existing ADRs (0001–0004) to full MADR format with Problem Statement, Decision Drivers, and Alternatives Considered.
- **ADR**: Added 3 new Architecture Decision Records:
   - `ADR-0005` - WebView2 Hybrid UI strategy
   - `ADR-0006` - Automated Patch Watcher system
   - `ADR-0007` - Security & Anti-Tamper architecture
- **ADR**: Created ADR index (`README.md`) and standardized `TEMPLATE.md` based on MADR format.

### 🗑️ Removed

- **Legacy**: Completely removed `ServiceLocator.cs` from production and test code.
- **MainForm**: Replaced obsolete default constructor with `NotSupportedException`.

### 🧪 Testing

- Added 26 new unit tests for specialized presenters (total: 269 tests).

---

## [2.1.11-beta] (Build 2080)

### 🚀 Added

- **Misc**: Added "Battle Effect" asset category with 10 TI-themed effects (Aghanim, Nemestice, TI 2015-2022).
- **Helpers**: Added async versions of file wait/copy methods (`WaitForFileReadyAsync`, `SafeCopyFileWithRetriesAsync`) to prevent UI thread blocking.

### 🛠️ Changed

- **CDN**: Switched to Cloudflare R2 CDN (`cdn.ardysamods.my.id`) as primary content source for faster updates.
- **Settings**: Removed Clear Cache button and Cache Size display for cleaner UI.
- **Logging**: Added diagnostic logging to empty catch blocks in `MiscFormWebView` for better debugging visibility.

### 🐛 Fixed

- **Settings**: Fixed X button not closing the settings form properly.
- **Assets**: Fixed caching overlay display and asset loading flow for miscellaneous options.
- **UI**: Fixed thumbnail URL generation for "Battle Effect" and other misc categories.
- **Scripts**: Updated `patch_models.py` to handle double URLs and CDN fallback for `heroes.json`.
- **Tests**: Fixed `MainFormPresenterTests` missing `configService` constructor parameter.

## [2.1.10-beta] (Build 2078) - 2026-02-04

### 🚀 Features & Architecture

- **Architecture**: Implemented `IMainFormFactory` to enable constructor injection in WinForms (`ADR-0002`).
- **CDN**: Added multi-CDN strategy with **Cloudflare R2** as primary, falling back to jsDelivr and GitHub Raw (`ADR-0003`).
- **Documentation**: Added comprehensive [Troubleshooting Guide](docs/TROUBLESHOOTING.md) and new Architecture Decision Records.

### 🐛 Bug Fixes

- **Network**: Fixed "CONNECTION TO SERVER FAILED" in Skin Selector by implementing R2 support and increasing timeout to 15s.
- **Safety**: Fixed critical bug in `ClearTempFolder` that could recursively delete files; now targets only application-specific temp data.
- **UI**: Removed duplicate method in `MainFormPresenter` and fixed logging for connection errors.

### ♻️ Refactoring

- **DI Migration**: Replaced `ServiceLocator` anti-pattern with proper **Constructor Injection** across `MainForm`, `SelectHero`, and `HeroGalleryForm`.
- **Config**: Moved `FavoritesStore` persistence to `%AppData%\ArdysaModsTools` to prevent data loss.
- **Cleanup**: Suppressed obsolete warnings for legacy test compatibility helpers.

### 🗑️ Removed

- **Legacy**: Removed `ServiceLocator` usage from all production code paths (kept only for unit tests).

## [2.1.9] - 2026-02-04

### Changed

- **Performance**: Optimized application startup time by lazy-loading non-critical services.
- **Dependencies**: Updated internal libraries to improve compatibility with latest Windows updates.
- **Network**: Refined error messages for network timeouts to be more user-friendly.

### Fixed

- **UI**: Fixed minor flickering issues in the Hero Gallery grid when resizing the window.
- **Cleanup**: Resolved edge case where temporary files weren't fully cleared on application exit.

## [2.1.8] - 2026-02-04

### Added

- **Logging**: Added detailed logging for VPK extraction steps to aid in debugging.
- **UI**: Introduced new "Troubleshooting" section in the main documentation.

### Changed

- **Performance**: Optimized `sync-to-r2.ps1` script for faster asset uploads.
- **Rendering**: Enhanced `MiscForm` rendering performance for smoother scrolling.

### Fixed

- **UI**: Fixed layout alignment issues in the Settings form for high-DPI displays.
- **Memory**: Resolved potential memory leak in image processing when loading large hero sets.

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

[Unreleased]: https://github.com/Anneardysa/ArdysaModsTools/compare/v2.1.7-beta...HEAD
[2.1.7-beta]: https://github.com/Anneardysa/ArdysaModsTools/compare/v2.1.6...v2.1.7-beta
[2.1.6]: https://github.com/Anneardysa/ArdysaModsTools/compare/v2.1.0...v2.1.6
[2.1.0]: https://github.com/Anneardysa/ArdysaModsTools/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/Anneardysa/ArdysaModsTools/releases/tag/v2.0.0
