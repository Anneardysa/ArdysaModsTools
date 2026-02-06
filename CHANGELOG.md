# Changelog

All notable changes to ArdysaModsTools will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased] (Build 2080)

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
