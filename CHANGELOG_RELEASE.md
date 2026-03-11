# Release Notes — v2.1.22-beta

> Covers builds **2122 → 2125**

---

## ✨ Added

- Added **Newcomer Onboarding Guide** — an interactive step-by-step overlay that highlights each feature when the app opens for the first time, guiding users through Auto Detect, Manual Detect, Skin Selector, Miscellaneous, Install, Patch Update, Console, and Settings.
- Onboarding uses a native WinForms spotlight overlay with pulsing cyan glow animations and dark backdrop for pixel-perfect control highlighting.
   - Features smooth spotlight transition animations, connector lines, dynamic tooltip heights, and L-bracket corner decorations.
   - Automatically captures a screenshot of the parent form to render actual UI behind a dimming layer.
- Onboarding state persists via `IConfigService` — shows only on first launch, can be re-triggered from Settings → "Show Guide" button.
- Added WebView2-based **Support Dialog** displaying Ko-fi donation goal and YouTube subscriber goal with CSS animations, powered by a remote `support_goals.json` config on R2 CDN.
- Created `SupportGoalsConfig` model and `SupportGoalsService` for fetching and caching combined goal data.

## 🚀 Improved

- Removed TailwindCSS and Google Fonts CDN dependencies — all CSS and fonts now load locally via `@font-face` for offline and GFW compatibility.
- Switched WebView2 from `NavigateToString` to `Navigate(file://)` to enable proper local font resolution.
- Added real-time input validation on numeric cvar fields with red border feedback and range tooltips (e.g., `fps_max` 0–999, `rate` 1000–1000000).
- Form now sizes responsively to 90% of screen bounds (min 900×600), fixing overflow on 1366×768 displays.
- Fixed inconsistent JSON escaping in settings load/apply by using `EscapeJs()` uniformly.
- Added proper `Dispose` override for WebView2 resource cleanup.
- Added GitHub proxy mirror fallbacks (`ghfast.top`, `gh-proxy.com`) as CDN priority 4–5 for users in regions where GitHub, Cloudflare R2, and jsDelivr are blocked by ISP-level filtering (e.g., China Mobile/Telecom behind the Great Firewall).
- Added `IsProxyUrl()` helper to `CdnConfig` for proxy domain detection, used by `CdnFallbackService` statistics tracking.
- `SmartCdnSelector` now recognizes and labels proxy CDNs in latency benchmark results.
- `CdnFallbackService` tracks proxy download successes separately in statistics for better observability.

## 📖 Documentation

- Comprehensive documentation refresh across 7 files to match current project state (v2.1.22-beta, 480+ tests).
- Updated version badge, test count (285+ → 480+), added Performance Tweaker feature, Courier/Ward/Special/Cursor mod types, Smart CDN Selection, Resumable Downloads, GFW proxy mirrors, and ADR-0004 Presenter Decomposition to design decisions.
- Added FAQ, Installer Guide, Security links; expanded ADR listing from 3 → 7; added all 9 API docs and `samples/` directory to structure tree.
- Rewrote all Mermaid diagrams — system architecture (5 presenters, 10 WebView2 forms, CDN/Misc service subgraphs), entry point flow (IMainFormFactory + DI), MVP class diagram (decomposed presenters), service layer (Courier/Ward/CDN services), and CDN table (GFW proxy entries + SmartCdnSelector).
- Replaced stale `ServiceLocator` test pattern with constructor injection + Moq; added STA apartment tip.
- Version 2.1.2 → 2.1.21, self-contained build (no .NET install), corrected install path to `%LocalAppData%`, added 6 missing misc categories, fixed settings path to `%AppData%`.
- Fixed project root name, added full 15-directory service tree, expanded CDN entry, added `exceptions.md` link.
- Removed .NET 8 install step (self-contained), added WebView2 troubleshooting tip.

## 🧪 Testing

- Added 14 unit tests for `Dota2PerformanceForm.ParseAutoexec` and `GenerateAutoexecContent` covering empty input, comments, standard cvars, inline comments, tabs, duplicates, alias lines, category grouping, unknown cvars, timestamping, and round-trip preservation.
- Added `InternalsVisibleTo` attribute to expose internal static methods to the test project.

---
