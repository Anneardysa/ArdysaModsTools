<div align="center">

<img src="Assets/Icons/AppIcon.ico" alt="ArdysaModsTools Logo" width="96" />

# ArdysaModsTools (AMT 2.0)

### *The Ultimate Dota 2 Mod Manager*

*Easily install, manage, and customize client-side cosmetic mods for Dota 2 — all with a single click.*

[![Version](https://img.shields.io/badge/Version-2.2.1--beta-00d4ff?style=flat-square&logo=github&logoColor=white)](https://github.com/Anneardysa/ArdysaModsTools/releases)
[![Build Status](https://img.shields.io/github/actions/workflow/status/Anneardysa/ArdysaModsTools/release.yml?style=flat-square&logo=github-actions&logoColor=white&label=Build)](https://github.com/Anneardysa/ArdysaModsTools/actions)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?style=flat-square&logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=flat-square)](LICENSE)

[![Downloads](https://img.shields.io/github/downloads/Anneardysa/ArdysaModsTools/total?style=flat-square&color=FF6B6B&logo=github&logoColor=white&label=Downloads)](https://github.com/Anneardysa/ArdysaModsTools/releases)
[![Stars](https://img.shields.io/github/stars/Anneardysa/ArdysaModsTools?style=flat-square&color=yellow&logo=github&logoColor=black)](https://github.com/Anneardysa/ArdysaModsTools/stargazers)
[![Last Commit](https://img.shields.io/github/last-commit/Anneardysa/ArdysaModsTools?style=flat-square&color=blue&logo=git&logoColor=white)](https://github.com/Anneardysa/ArdysaModsTools/commits/main)
[![Tests Passed](https://img.shields.io/badge/Tests-688%20Passed-brightgreen?style=flat-square&logo=github-actions&logoColor=white)](Tests/ArdysaModsTools.Tests.csproj)

[📥 Download](#-installation) • [🚀 Quick Start](#-quick-start) • [🎨 Features](#-key-features) • [🏗️ Architecture](#%EF%B8%8F-architecture--internals) • [❓ FAQ](#-faq) • [🔧 Troubleshooting](#-troubleshooting)

</div>

---

## ⚠️ Disclaimer

> [!CAUTION]
> This tool is **NOT** affiliated with, endorsed by, or connected to **Valve Corporation** or **Steam**.
> Modifying game files may violate Valve's Terms of Service. **Use at your own risk.**
> All mods are **client-side only** — other players cannot see your modifications.

---

## ✨ Introduction

**ArdysaModsTools (AMT)** is a powerful, desktop-based utility designed to elevate your Dota 2 experience. It provides a seamless interface to manage, customize, and apply client-side cosmetics, skins, terrains, weather effects, and performance optimizations. 

Built on .NET 8 with a high-performance **WinForms + WebView2 hybrid architecture**, AMT combines the raw speed of native Windows applications with the fluid, modern design capabilities of web technologies.

---

## 🎨 Key Features

### 🎮 Core Functionality

| Feature | Description |
| :--- | :--- |
| **One-Click ModsPack** | Download and install the complete, curated community mod pack with a single click. |
| **Skin & Persona Selector** | Browse and select hero sets, individual item pieces (with slot-based mutual exclusion), and full hero Personas (with model-wide exclusion) via an interactive gallery UI. |
| **Style Card Variants** | Select from multiple visual style variations for individual sets and items, rendered via a sleek in-app Style Preview Modal. |
| **Miscellaneous Mods** | Instantly toggle weather effects, custom terrains, HUD designs, cursor packs, custom music packs, kill/battle effects, and personalized couriers/wards. |
| **Performance Tweaker** | Tune Dota 2 cvars (written atomically to `autoexec.cfg`) and copy optimized launch options to maximize FPS and responsiveness. |
| **Auto-Patching** | Background update checker automatically detects Dota 2 updates and re-applies active mods. |
| **Manual VPK Import** | Import your own custom `.vpk` mod files directly into the manager's patch structure. |
| **Safe & Reversible** | Restore the vanilla game state instantly using the "Disable Mods" option without corrupting official assets. |

### 🛠️ Technical Highlights

* **Resilient Multi-CDN Fallback** — Centralized download chain prioritizing Cloudflare R2 (authoritative origin) with automatic failover to jsDelivr → GitHub Raw → Proxy mirrors.
* **Smart CDN Selector** — Automatically benchmarks latencies to determine the fastest fallback mirror per-session.
* **Resumable HTTP Range Downloads** — Supports partial content downloads with automatic failover mid-stream if a connection drops.
* **Atomic File Transactions** — Writes to critical files (`pak01_dir.vpk`, `autoexec.cfg`) are wrapped in transactions with verification and rollback logic to prevent data corruption.
* **SHA-256 Manifest Verification** — Pre-verifies files against a CDN-hosted manifest to ensure file integrity prior to installation.
* **Smart Resource Interception** — Intercepts and retrieves WebView2 assets locally from a persistent cache (`%LocalAppData%`), allowing offline operations and instant gallery loads.
* **Launch-State Preloading** — Background thread pre-warms the thumbnail cache on startup with throttled, cancellable requests.
* **Live `items_game.txt` Parsing** — Extracts and constructs the patch manifest directly from the game's actual VPK at run-time, maintaining compatibility with new Dota 2 patches without updates.
* **Clean Code Foundation** — Backed by 688 unit tests verifying all core business logic, API clients, and generation pipelines.

---

## 📥 Installation

### System Requirements

| Component | Minimum Requirement | Recommended |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (64-bit, Build 19041+) | Windows 11 (64-bit) |
| **Framework** | None (Self-Contained Runtime included) | None (Self-Contained Runtime included) |
| **Browser Engine** | [Microsoft WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) | WebView2 Runtime (Pre-installed on Win 10/11) |
| **Game** | Dota 2 installed via Steam | Dota 2 installed on an SSD |

### Download & Install Steps

1. Navigate to the [**Releases**](https://github.com/Anneardysa/ArdysaModsTools/releases) page.
2. Download the latest installer executable: `ArdysaModsTools_Setup_x64.exe`.
3. Launch the installer and complete the setup wizard.
4. Open **ArdysaModsTools** from the desktop shortcut or Start Menu.
5. *Note: Official releases are signed by the [SignPath Foundation](https://signpath.org) code signing service.*

> [!TIP]
> The application files install to `%LocalAppData%\ArdysaModsTools`.
> User configurations, favorites, and presets are safely stored in `%AppData%\ArdysaModsTools`.

---

## 🚀 Quick Start

### 1. Initial Setup
1. **Close Dota 2** if it is currently running.
2. Launch **ArdysaModsTools** (Administrator privileges recommended for path detection).
3. The app will auto-detect your Dota 2 installation. If it fails, click **Manual Detect** and select your `dota 2 beta` folder.
4. Click **Install ModsPack** to fetch, unpack, and hook the main mod assets.
5. Launch Dota 2 and enjoy!

### 2. Customizing Hero Skins
1. Open the **Skin Selector** in the sidebar.
2. Filter by hero, category (Legacy, Custom, Persona, Item, Base), or search by name.
3. Select your desired cosmetics (mutual exclusion is handled automatically).
4. For items with variants, click the **Style Card** to open the variant preview and select your style.
5. Click **Generate** to compile and apply the custom patch.

### 3. Handling Dota 2 Game Updates
Whenever Valve updates Dota 2, your custom cosmetic patch will be overwritten.
* **Automatic**: If **PatchWatcher** is enabled, AMT will detect the update and prompt you to re-apply.
* **Manual**: Simply open the app and click **Patch Update** to re-integrate your mod folder.

---

## 🎨 Supported Mod Types

| Category | Description | Examples / Subtypes |
| :--- | :--- | :--- |
| **Hero Skins** | Full hero cosmetics, item pieces, and personas | Arcanas, Personas, custom mixes, weapon items |
| **Weather** | Custom weather effects overrides | Rain, Snow, Aurora, Moonbeam, Spring |
| **Terrain** | Replaces the default map terrain | TI Terrains, Seasonal Maps, Desert, Immortal Gardens |
| **HUD Skins** | Re-skins the game's user interface overlay | Custom tournament and theme HUDs |
| **Music Packs** | Replaces default game soundtrack | Custom themes and classic music files |
| **Couriers & Wards** | Custom models with style selectors | Particle-heavy courier skins, custom ward models |
| **Battle Effects** | Overrides standard kills/streaks/actions | TI-themed streak banners, Nemestice/Aghanim effects |
| **Cursors** | Custom mouse pointer shapes and styles | Custom cursor packs |
| **Special Packs** | Archive-based external conversions | LowPoly Map, custom community UI theme adjustments |

---

## 🏗️ Architecture & Internals

### Tech Stack Details

* **Language**: C# (.NET 8.0 Windows Application)
* **Shell**: Windows Forms (WinForms) as the native container
* **Frontend**: WebView2 embedded browser rendering high-performance HTML5/CSS3/JS
* **Pattern**: Model-View-Presenter (MVP) with clean separation of concerns
* **Dependency Injection**: `Microsoft.Extensions.DependencyInjection`
* **Compression**: `SharpCompress` for zip/tar extraction
* **KeyValues Parser**: `ValveKeyValue` + custom block overlay helpers
* **Image Processing**: `SixLabors.ImageSharp` for local WebP decoding

### Project Directory Layout

```text
ArdysaModsTools/
├── Core/                      # Business logic, services & interfaces
│   ├── Constants/             # Absolute URLs, settings keys & constants
│   ├── Controllers/           # Application coordination workflows
│   ├── Data/                  # Static definitions & layout data
│   ├── DependencyInjection/   # Service container composition
│   ├── Exceptions/            # Domain-specific exceptions & error codes
│   ├── Helpers/               # Core utility libraries (KV parser, web helpers)
│   ├── Interfaces/            # Formal contracts for dependency decoupling
│   ├── Models/                # Data structures & serialization classes
│   └── Services/              # Core business services
│       ├── App/               # App lifecycle, initialization & update checks
│       ├── Cache/             # Local cache managers & preloading engines
│       ├── Cdn/               # SmartCdnSelector & download fallbacks
│       ├── Config/            # App preferences & feature gates
│       ├── Conflict/          # Mod conflict detection & resolution
│       ├── Detection/         # Steam & Dota 2 folder path locator
│       ├── FileTransaction/   # Atomic file I/O transactions with rollback
│       ├── Hero/              # Hero set mapping & VPK compiler
│       ├── Logging/           # ILogService interface implementations
│       ├── Meta/              # Donation and social channel endpoints
│       ├── Misc/              # Weather, terrain, courier, ward & cursor logic
│       ├── Mods/              # Main modspack installers & uninstallers
│       ├── Security/          # Integrity checkers & anti-tamper services
│       ├── Update/            # PatchWatcher, launcher updater & downloaders
│       └── Vpk/               # VPK compilers & items_game.txt extractor
├── UI/                        # UI Presentation Layer (View/Presenter)
│   ├── Controls/              # Custom native GDI+ WinForms controls
│   ├── Forms/                 # Form windows hosting WebView2 UI shells
│   ├── Helpers/               # Interceptors for local cache assets
│   ├── Presenters/            # MVP Presenters coordinating view events
│   └── Styles/                # Native system style colors
├── Helpers/                   # Shared system/OS helpers
├── Installer/                 # WPF-based setup application project
├── Assets/                    # App assets (icons, HTML templates, fonts)
├── Tests/                     # Comprehensive xUnit + Moq unit test project
├── docs/                      # Developer and user documentation
│   └── adr/                   # Architecture Decision Records (ADRs)
├── scripts/                   # Python build scripts and CDN sync utilities
└── tools/                     # Core external binaries (vpk.exe, HLExtract.exe)
```

### Architectural Decisions

| ADR | Decision Summary | Rationale |
| :--- | :--- | :--- |
| **[ADR-0002](docs/adr/0002-dependency-injection.md)** | Constructor-based DI | Decouples services, increases testability, and handles lifecycles cleanly. |
| **[ADR-0003](docs/adr/0003-multi-cdn-fallback-strategy.md)** | Multi-CDN with Fallback | Guarantees download stability for global and region-restricted users. |
| **[ADR-0004](docs/adr/0004-presenter-decomposition.md)** | MVP Presenter Decomposition | Prevents bloated forms/presenters by adhering to Single Responsibility. |
| **[ADR-0005](docs/adr/0005-webview2-hybrid-ui.md)** | WebView2 Hybrid Interface | Allows high-fidelity responsive styling while maintaining native Windows access. |
| **[ADR-0006](docs/adr/0006-patch-automation.md)** | FileSystemWatcher Patching | Automates patch reconciliation immediately following official updates. |
| **[ADR-0007](docs/adr/0007-security-integrity.md)** | Anti-Tamper Security Check | Guards internal assemblies and manifests to prevent mod injection exploits. |
| **[ADR-0008](docs/adr/0008-hero-cosmetic-priority-merge.md)** | Layered Last-Writer-Wins Merge | Enforces a strict merging hierarchy (Base > Set > Item) during VPK compilation. |
| **[ADR-0009](docs/adr/0009-cdn-download-resilience-layer.md)** | Exponential Backoff & Circuit Breaker | Keeps network connections stable and routes around unhealthy mirrors. |
| **[ADR-0010](docs/adr/0010-asset-hash-verification.md)** | Stream-based SHA-256 Validation | Ensures file integrity and drops corrupt packages before extraction. |

---

## 🔧 For Developers

### Prerequisites

* **.NET 8.0 SDK** (v8.0.x)
* **Visual Studio 2022** (with *.NET Desktop Development* workload)
* **Python 3.10+** (for utility packaging and asset scripts)
* **WebView2 Runtime** (Pre-installed on modern Windows versions)

### Local Environment Setup

```bash
# Clone the repository
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build

# Run unit tests
dotnet test Tests/ArdysaModsTools.Tests.csproj

# Run the application
dotnet run --project ArdysaModsTools.csproj
```

### Building the Installer

We compile the installer using a standalone WPF bootstrapper tool:
```bash
python scripts/build/build_installer.py
```

### Developer Links

* 📄 **[Contributing Guide](docs/dev/CONTRIBUTING.md)** — Core codebase code-styles and patterns.
* 📄 **[Security Model](docs/dev/SECURITY.md)** — Assembly integrity and security checks.
* 📄 **[Installer Design](docs/dev/INSTALLER.md)** — Details on bootstrapper setup configuration.
* 📄 **[Changelog](CHANGELOG.md)** — Chronological commit and change logs.

---

## ❓ FAQ

<details>
<summary><b>Is ArdysaModsTools safe to use?</b></summary>

Yes. AMT is designed to operate client-side only. It only modifies local configuration files and compiles custom VPK files inside your local game path. It does **not** hook into the game process, inject code, or alter server-side queries.
</details>

<details>
<summary><b>Can I get VAC banned for client-side modifications?</b></summary>

Client-side cosmetic and key-value modifications have historically been safe from Valve Anti-Cheat (VAC) bans because they do not modify game logic, memory addresses, or official servers. However, modifying game files always carries a non-zero risk under Valve's Terms of Service. **Use at your own risk.**
</details>

<details>
<summary><b>Can other players see my custom skins?</b></summary>

**No.** All modifications are local. Other players will see whatever standard cosmetics you have equipped on the Steam servers.
</details>

<details>
<summary><b>My mods disappeared after a game update. What should I do?</b></summary>

Dota 2 updates overwrite the main VPK index (`pak01_dir.vpk`). Simply launch ArdysaModsTools and click **Patch Update** to re-insert the mod hook. If PatchWatcher is running in the background, you will receive a notification automatically.
</details>

<details>
<summary><b>How do I completely remove all modifications?</b></summary>

Inside the app, click **Disable Mods**. This will safely restore the original game configuration. If you wish to be absolutely sure, you can verify game file integrity via the Steam library client (*Dota 2 → Properties → Installed Files → Verify integrity of game files*).
</details>

<details>
<summary><b>Why is the interface showing a blank white page or WebView2 error?</b></summary>

AMT relies on the Microsoft Edge WebView2 engine. If it is missing or corrupted, install/re-install the latest [WebView2 Evergreen Bootstrapper](https://developer.microsoft.com/microsoft-edge/webview2/) from Microsoft.
</details>

---

## 🔧 Troubleshooting

| Issue | Potential Cause | Troubleshooting Steps |
| :--- | :--- | :--- |
| **Application Crash at Launch** | Corrupted WebView2 Runtime | Download and repair the WebView2 Evergreen Runtime. |
| **Mods Fail to Appear in Game** | Game directory not detected correctly | Run AMT as Administrator and click **Auto Detect**, or manually select the folder path. |
| **Download Failed/Stalled** | CDN mirror block or ISP timeout | Check your internet connection. AMT will automatically cycle to alternative mirrors. |
| **"Operation Blocked by Lock"** | Dota 2 or Steam is running | Close the game/Steam fully and retry the installation. |
| **VPK Extraction/Recompile Fail** | Stale game installation files | Run *Verify Integrity* in Steam, then open AMT and re-apply mods. |

---

## 🏆 Credits & Acknowledgments

### Project Team

* **Author & Lead Dev**: **Ardysa** ([@Anneardysa](https://github.com/Anneardysa))
  *Copyright © 2025-2026 Ardysa. All rights reserved.*
* **Repository**: [ArdysaModsTools](https://github.com/Anneardysa/ArdysaModsTools)

### Open Source Libraries

Thank you to the authors of the packages enabling this project:

* **[Microsoft.Web.WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)** — Native WinForms Chromium frontend host.
* **[ValveKeyValue](https://github.com/ValveResourceFormat/ValveKeyValue)** — Valve KV file format reader/writer.
* **[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)** — High-performance image processing engine.
* **[SharpCompress](https://github.com/adamhathcock/sharpcompress)** — Multi-format compression library.
* **[HLExtract](https://github.com/Flaviusb/hl2parse)** — VPK file indexing and unpacking.

### Special Thanks

* **[SignPath Foundation](https://signpath.org)** for providing free code signing certificates to this open-source project.
* The **Dota 2 Modding & SkinChanger community** for sharing techniques, file indexes, and general reverse engineering knowledge.
* **Valve Corporation** for creating Dota 2 and keeping the VPK structures accessible.

---

## ⚖️ Licensing & Trademarks

### Code License
The source code of ArdysaModsTools is distributed under the terms of the **GNU General Public License v3.0**. See the [LICENSE](LICENSE) file for complete details. 

### Brand & Intellectual Property
The names **"ArdysaMods"**, **"ArdysaModsTools"**, **"AMT"**, the official logo, icons, visual identities, and domain names (**`ardysamods.my.id`**, **`cdn.ardysamods.my.id`**) are **strictly excluded** from the open-source license. 
* If you fork this project, you **must** rename the application, compile it with a distinct logo/identity, and host your own CDN update endpoints. For details, refer to the [`NOTICE`](NOTICE) file.

---

<div align="center">

**© 2025-2026 Ardysa. All rights reserved.**

*Made with ❤️ for the Dota 2 community.*

⭐ **Star this repository** if ArdysaModsTools helps you enjoy the game!

</div>
