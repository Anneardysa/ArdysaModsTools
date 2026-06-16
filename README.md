<div align="center">

<img src="Assets/Icons/AppIcon.ico" alt="ArdysaModsTools Logo" width="80" />

# ArdysaModsTools (AMT 2.0)

### The Ultimate Dota 2 Mod Manager

_Easily install, manage, and customize cosmetic mods for Dota 2 — all in one click._

![Version](https://img.shields.io/badge/Version-2.2.0--beta-00d4ff?style=for-the-badge&logo=v)
![Build](https://img.shields.io/github/actions/workflow/status/Anneardysa/ArdysaModsTools/release.yml?style=for-the-badge&logo=github&label=Build)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?style=for-the-badge&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)

![Downloads](https://img.shields.io/github/downloads/Anneardysa/ArdysaModsTools/total?style=flat-square&color=FF6B6B&logo=github&label=Downloads)
![Stars](https://img.shields.io/github/stars/Anneardysa/ArdysaModsTools?style=flat-square&color=yellow)
![Last Commit](https://img.shields.io/github/last-commit/Anneardysa/ArdysaModsTools?style=flat-square&color=blue)
![Tests](https://img.shields.io/badge/Tests-660+-brightgreen?style=flat-square)

[📥 Download](#-installation) · [🚀 Quick Start](#-quick-start) · [🎨 Features](#-features) · [🏗️ Architecture](#%EF%B8%8F-architecture) · [❓ FAQ](#-faq)

</div>

---

## ⚠️ Disclaimer

> [!CAUTION]
> This tool is **NOT** affiliated with, endorsed by, or connected to **Valve Corporation** or **Steam**.
> Modifying game files may violate Valve's Terms of Service. **Use at your own risk.**
> All mods are **client-side only** — other players cannot see your modifications.

---

## ✨ Features

### 🎮 Core Functionality

| Feature                        | Description                                                                                               |
| ------------------------------ | --------------------------------------------------------------------------------------------------------- |
| **One-Click ModsPack Install** | Download and install the complete mod pack from CDN with a single click                                   |
| **Skin & Persona Selector**    | Browse and select hero sets, individual item pieces (with slot-based mutual exclusion), and full hero Personas (with model-wide exclusion) via a gallery UI |
| **Miscellaneous Mods**         | Toggle weather, terrain, HUD, cursors, music, battle effects, couriers, wards, and special mods           |
| **Performance Tweaker**        | Tune Dota 2 FPS and cvars (saved to `autoexec.cfg` atomically via transactions) plus copy-ready launch options (remembered across sessions) via WebView2 |
| **Auto-Patching**              | Automatically detects Dota 2 updates and re-applies your mods — no manual work needed                     |
| **Manual VPK Install**         | Import your own custom `.vpk` mod files directly                                                          |
| **Safe & Reversible**          | Click "Disable Mods" to instantly restore vanilla Dota 2 — no files are permanently altered               |

### 🛠️ Technical Highlights

- **Multi-CDN Download Strategy** — Primary: Cloudflare R2, fallback: jsDelivr → GitHub Raw → GFW proxy mirrors
- **Smart CDN Selection** — Automatic latency benchmarking picks the fastest CDN per user
- **Resumable Downloads** — HTTP Range-based chunk streaming with cross-CDN failover without losing progress
- **Model Exclusivity Engine** — Handles tag-based mutual exclusion for items and model-wide exclusions for Personas
- **Atomic File Operations** — Extract-then-swap pattern (and transaction wrapper for `autoexec.cfg` saving) prevents corruption
- **SHA-256 Hash Verification** — Downloads are verified against remote hashes for integrity
- **Persistent Thumbnail Cache** — Gallery thumbnails are served from a local `%LocalAppData%` asset cache via a WebView2 resource interceptor, so they download once and survive restarts, temp cleanup, and app updates
- **Background Asset Preloader** — A "Launching State" preloader warms the thumbnail cache on startup (throttled, cancellable) so the Skin Selector and Miscellaneous panels open instantly and work offline
- **Known-Missing Asset Tracking** — Definitive `404`/`403` responses are remembered (7-day TTL) and no longer trip the CDN circuit breaker, eliminating request storms for absent thumbnails
- **Live `items_game.txt` Extraction** — Skin generation sources `items_game.txt` directly from your installed game's `pak01_dir.vpk` each run, keeping mods aligned with the current patch
- **PatchWatcher** — Background file watcher detects Dota 2 updates in real-time by monitoring key system manifests
- **Remote Feature Control** — Features can be remotely enabled/disabled via Cloudflare R2 config
- **Self-Contained Build** — .NET 8 runtime is bundled; no external runtime installation needed
- **660+ Unit Tests** — Comprehensive test coverage for core services

---

## 📥 Installation

### System Requirements

| Requirement         | Details                                                                                                               |
| ------------------- | --------------------------------------------------------------------------------------------------------------------- |
| **OS**              | Windows 10/11 (64-bit)                                                                                                |
| **Runtime**         | Bundled (self-contained — no separate .NET install needed)                                                            |
| **Browser Runtime** | [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (usually pre-installed on Windows 10/11) |
| **Game**            | Dota 2 installed via Steam                                                                                            |

### Download & Install

1. Go to the [**Releases**](https://github.com/Anneardysa/ArdysaModsTools/releases) page
2. Download the latest `ArdysaModsTools_Setup_x64.exe`
3. Run the installer and follow the setup wizard
4. Launch **ArdysaModsTools** from your Desktop or Start Menu

> [!TIP]
> The app installs to `%LocalAppData%\ArdysaModsTools` by default.
> User settings and favorites are stored in `%AppData%\ArdysaModsTools`.

---

## 🚀 Quick Start

### First-Time Setup

```
1. Close Dota 2 (if running)
2. Launch ArdysaModsTools (run as Administrator recommended)
3. The app will auto-detect your Dota 2 installation folder
4. Click "Install ModsPack" to download and apply all mods
5. Start Dota 2 and enjoy your new cosmetics!
```

### After a Dota 2 Update

When Dota 2 receives an update, your mods will be overwritten by Valve. AMT handles this automatically:

- **If PatchWatcher is active**: AMT detects the update and prompts you to re-patch
- **Manual**: Click **"Patch Update"** in AMT to re-apply your mods

### Customizing Individual Heroes

1. Click **"Skin Selector"** to open the Hero Gallery
2. Browse heroes and pick cosmetic sets you like
3. Click **"Generate"** to build your personalized VPK
4. The mod is applied to your Dota 2 installation

### Disabling Mods

Click **"Disable Mods"** to restore vanilla Dota 2. This safely removes all modifications and restores original game files.

---

## 🎨 Mod Types

| Type               | Description                           | Examples                                             |
| ------------------ | ------------------------------------- | ---------------------------------------------------- |
| **Hero Sets & Personas** | Custom cosmetic sets and full hero replacement models | Arcanas, Personas, cache sets, immortals |
| **Weather**        | Weather visual effects                | Rain, snow, moonbeam, aurora                         |
| **Terrain**        | Custom map skins                      | TI terrains, seasonal maps                           |
| **HUD**            | Interface themes                      | Custom HUD skins and overlays                        |
| **Battle Effects** | Kill/ability effects                  | TI-themed effects (Aghanim, Nemestice, TI 2015–2022) |
| **Music**          | Custom music packs                    | —                                                    |
| **Courier**        | Cosmetic courier skins with particles | Styled couriers + up to 2 ethereal particle effects  |
| **Ward**           | Cosmetic ward skins with styles       | Custom ward models and particle effects              |
| **Special**        | Full ZIP-based mod packs              | LowPoly Map, community-made total conversions        |
| **Cursor**         | Custom cursor skins                   | —                                                    |

---

## 🏗️ Architecture

### Tech Stack

| Layer                    | Technology                                    |
| ------------------------ | --------------------------------------------- |
| **Runtime**              | .NET 8.0 (Windows, self-contained)            |
| **UI Framework**         | WinForms + WebView2 (hybrid)                  |
| **Architecture Pattern** | MVP (Model-View-Presenter)                    |
| **DI**                   | `Microsoft.Extensions.DependencyInjection`    |
| **VPK Tools**            | HLExtract, Valve vpk.exe                      |
| **Image Processing**     | SixLabors.ImageSharp (WebP support)           |
| **KV Parsing**           | ValveKeyValue + custom `KeyValuesBlockHelper` |
| **CDN**                  | Cloudflare R2 (primary), jsDelivr, GitHub Raw |

### Project Structure

```
ArdysaModsTools/
├── Core/                      # Business logic & services
│   ├── Constants/             # Shared constants (paths, URLs, CDN config)
│   ├── DependencyInjection/   # DI container setup
│   ├── Interfaces/            # Service contracts (18 interfaces)
│   ├── Models/                # Data models & DTOs
│   ├── Services/
│   │   ├── App/               # App lifecycle, update service
│   │   ├── Cache/             # Cache cleaning, persistent asset cache & background preloader
│   │   ├── Cdn/               # CDN config, SmartCdnSelector, fallback
│   │   ├── Config/            # Settings, favorites, feature access
│   │   ├── Conflict/          # Mod conflict detection & resolution
│   │   ├── Detection/         # Dota 2 folder auto/manual detection
│   │   ├── FileTransaction/   # Atomic file operations with rollback
│   │   ├── Hero/              # Hero set generation & patching
│   │   ├── Logging/           # App & fallback logging
│   │   ├── Meta/              # Support goals (Ko-fi, YouTube)
│   │   ├── Misc/              # Weather, terrain, HUD, courier, ward, etc.
│   │   ├── Mods/              # ModsPack install, disable, patch
│   │   ├── Security/          # Anti-tamper & integrity checks
│   │   ├── Update/            # Auto-update, PatchWatcher, resumable DL
│   │   └── Vpk/               # VPK extraction, recompilation & live items_game.txt extraction
│   └── Helpers/               # Utility classes (incl. WebView2 environment helper)
├── UI/                        # Presentation layer
│   ├── Forms/                 # WinForms + WebView2 hybrid forms (33 files)
│   ├── Presenters/            # MVP presenters (7 presenters)
│   ├── Helpers/               # WebView2 asset interceptor
│   ├── Controls/              # Custom UI controls
│   └── Styles/                # Theme & styling
├── Installer/                 # WPF-based installer project
├── Assets/                    # Icons, HTML templates, fonts, images
├── Tests/                     # Unit tests (660+)
├── scripts/                   # Build & automation scripts
├── tools/                     # VPK tools, .NET runtime, WebView2
└── docs/                      # Developer & user documentation
```

### Key Design Decisions

| Decision                    | Approach                | Details                                                                   |
| --------------------------- | ----------------------- | ------------------------------------------------------------------------- |
| **UI Strategy**             | WebView2 Hybrid         | Rich HTML/CSS UI inside WinForms shell ([ADR-0005](docs/adr/))            |
| **Dependency Injection**    | Constructor Injection   | Full DI via `Microsoft.Extensions.DI` ([ADR-0002](docs/adr/))             |
| **CDN Strategy**            | Multi-CDN with Fallback | R2 → jsDelivr → GitHub Raw → Proxy mirrors ([ADR-0003](docs/adr/))        |
| **Presenter Decomposition** | SRP Specialization      | 3 focused presenters split from MainFormPresenter ([ADR-0004](docs/adr/)) |
| **Patch Automation**        | FileSystemWatcher       | Detects Dota 2 updates automatically ([ADR-0006](docs/adr/))              |
| **Security**                | Anti-Tamper             | Integrity checks on critical files ([ADR-0007](docs/adr/))                |

> See [Architecture Decision Records](docs/adr/) for full details on all design decisions.

---

## 🔧 For Developers

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (with .NET Desktop workload)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)

### Build & Run

```bash
# Clone the repo
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run

# Run tests
dotnet test Tests/ArdysaModsTools.Tests.csproj
```

### Build Installer

```bash
python scripts/build/build_installer.py
```

### Documentation

| Document                                       | Description                          |
| ---------------------------------------------- | ------------------------------------ |
| [Contributing Guide](docs/dev/CONTRIBUTING.md) | How to contribute to the project     |
| [Security Guide](docs/dev/SECURITY.md)         | Security model & anti-tamper details |
| [Installer Guide](docs/dev/INSTALLER.md)       | Installer build process              |
| [Troubleshooting](docs/TROUBLESHOOTING.md)     | Common issues & fixes                |
| [Architecture Decisions](docs/adr/)            | ADRs in MADR format                  |
| [Changelog](CHANGELOG.md)                      | Full version history                 |

---

## ❓ FAQ

<details>
<summary><b>Is this safe to use?</b></summary>

Yes. AMT only modifies local cosmetic files inside your Dota 2 installation directory. It does **not** interact with Valve's online services, inject into game processes, or modify game memory. All changes are file-based and fully reversible.

</details>

<details>
<summary><b>Will I get VAC banned?</b></summary>

Cosmetic file mods have historically not resulted in VAC bans, as they don't modify game behavior or interact with anti-cheat systems. However, Valve's policies can change at any time. **Use at your own risk.**

</details>

<details>
<summary><b>Can other players see my mods?</b></summary>

**No.** All mods are client-side only. Other players see the default game assets. Your modifications only affect what **you** see on your screen.

</details>

<details>
<summary><b>Mods stopped working after a Dota 2 update?</b></summary>

This is expected — Dota 2 updates overwrite modded files. Simply open AMT and click **"Patch Update"** or **"Install ModsPack"** to re-apply your mods. If PatchWatcher is active, AMT will notify you automatically.

</details>

<details>
<summary><b>How do I completely remove mods?</b></summary>

Click **"Disable Mods"** in AMT. This restores the original game files. You can also verify game integrity via Steam: _Right-click Dota 2 → Properties → Local Files → Verify integrity of game files_.

</details>

<details>
<summary><b>The app won't start or shows a WebView2 error</b></summary>

Install the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/). This is usually pre-installed on Windows 10/11 but may be missing on some systems.

</details>

<details>
<summary><b>Dota 2 folder is not detected</b></summary>

Try running AMT **as Administrator**. If auto-detection still fails, use the **"Manual Detect"** button to browse to your `dota 2 beta` folder (typically at `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`).

</details>

---

## 🔧 Troubleshooting

| Problem               | Solution                                                                             |
| --------------------- | ------------------------------------------------------------------------------------ |
| App won't start       | Install [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) |
| Mods not working      | Run AMT as Administrator, click "Install ModsPack"                                   |
| Dota 2 not detected   | Run as Admin → "Auto Detect" or "Manual Detect"                                      |
| Download fails/stalls | Check internet connection; AMT will try fallback CDN servers automatically           |
| "Installation failed" | Close Dota 2 first, then retry. Check console log for specific errors                |
| VPK error             | Verify Dota 2 game files via Steam, then reinstall mods                              |

> For more detailed troubleshooting, see [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

---

## 🏆 Credits & Acknowledgments

### Project

|             |                                                                  |
| ----------- | ---------------------------------------------------------------- |
| **Author**  | **Ardysa** ([@Anneardysa](https://github.com/Anneardysa))        |
| **Project** | [ArdysaModsTools](https://github.com/Anneardysa/ArdysaModsTools) |
| **License** | [GPL-3.0](LICENSE)                                               |

### Dota 2 Modding Community

This project exists thanks to the incredible Dota 2 modding community. Special thanks to:

- **Dota 2 SkinChanger Community** — Pioneers who first explored client-side cosmetic modding for Dota 2, laying the groundwork for community-driven tools like AMT.
- **Dota 2 Modders & Content Creators** — The dedicated modders who create and share custom hero sets, terrain skins, weather effects, emblems, and more, making the modding scene thrive.
- **Valve Corporation** — For creating Dota 2 and the Source 2 engine, and for the VPK/KeyValues ecosystem that makes modding possible.

### Open-Source Libraries

| Library                                                                        | Purpose                 | License    |
| ------------------------------------------------------------------------------ | ----------------------- | ---------- |
| [Microsoft WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) | Hybrid UI rendering     | BSD-style  |
| [ValveKeyValue](https://github.com/ValveResourceFormat/ValveKeyValue)          | Parsing Valve KV files  | MIT        |
| [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)                | Image processing (WebP) | Apache 2.0 |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress)                 | Archive extraction      | MIT        |
| [HLLib/HLExtract](https://github.com/Flaviusb/hl2parse)                        | VPK extraction          | LGPL       |

### Community & Inspiration

- The **Dota 2 modding community** for sharing knowledge and techniques
- All the **beta testers** and **users** who report bugs and provide feedback
- Everyone who has contributed to making Dota 2 modding more accessible

---

## ⚖️ Licensing & Trademarks

> [!IMPORTANT]
> The code license and the **brand** are separate. Read both before forking.

- **Code (released GPL-3.0 versions):** You may copy, modify, and redistribute under
  [GPL-3.0](LICENSE) — which requires preserving attribution and publishing your modified
  source under the same license. **Future versions may be released under different terms**
  (the sole copyright holder may relicense new versions; already-published GPL-3.0 versions
  stay GPL-3.0).
- **Brand (NOT licensed):** The names **"ArdysaMods" / "ArdysaModsTools" / "AMT"**, the logo and
  app icon, the visual identity, and the domains **`ardysamods.my.id`** / **`cdn.ardysamods.my.id`**
  are **not** covered by the code license and may not be used without written permission.
- **If you fork:** use a **different name and logo**, do **not** imply official status or
  affiliation, and do **not** use the official domains, CDN, or update endpoints.

Full terms are in [`NOTICE`](NOTICE). Misuse of the brand or attribution-stripping
redistribution is actionable under trademark/copyright law independently of the code license.

---

## 💬 Support & Community

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
- 💡 **Feature Requests**: [GitHub Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
- 📖 **Documentation**: [docs/](docs/)
- 🤝 **Contributing**: [CONTRIBUTING.md](docs/dev/CONTRIBUTING.md)

---

## 📝 License

This project is licensed under the **GNU General Public License v3.0** — see the [LICENSE](LICENSE) file for details.

You are free to use, modify, and distribute this software under the terms of GPLv3.

---

<div align="center">

**© 2025-2026 Ardysa. All rights reserved.**

Made with ❤️ for the Dota 2 community

⭐ **Star this repo** if AMT helps you enjoy Dota 2 with style!

</div>
