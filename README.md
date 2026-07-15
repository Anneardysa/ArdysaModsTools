<div align="center">

# ArdysaModsTools (AMT 2.0)

### _The Ultimate Dota 2 Mod Manager_

_Install, manage, and customize client-side cosmetic mods for Dota 2 — all with a single click._

[![Version](https://img.shields.io/github/v/release/Anneardysa/ArdysaModsTools?include_prereleases&style=flat-square&logo=github&logoColor=white&label=Version&color=00d4ff)](https://github.com/Anneardysa/ArdysaModsTools/releases)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?style=flat-square&logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=flat-square)](LICENSE)

[![Downloads](https://img.shields.io/github/downloads/Anneardysa/ArdysaModsTools/total?style=flat-square&color=FF6B6B&logo=github&logoColor=white&label=Downloads)](https://github.com/Anneardysa/ArdysaModsTools/releases)
[![Stars](https://img.shields.io/github/stars/Anneardysa/ArdysaModsTools?style=flat-square&color=yellow&logo=github&logoColor=black)](https://github.com/Anneardysa/ArdysaModsTools/stargazers)

[📥 Download](#-installation) • [🚀 Quick Start](#-quick-start) • [🎨 Features](#-key-features) • [📚 Documentation](#-documentation) • [❓ FAQ](#-faq)

🌐 **Website:** [ardysamods.my.id](https://ardysamods.my.id)

</div>

---

## ⚠️ Disclaimer

> [!CAUTION]
> This tool is **NOT** affiliated with, endorsed by, or connected to **Valve Corporation** or **Steam**.
> Modifying game files may violate Valve's Terms of Service. **Use at your own risk.**
> All mods are **client-side only** — other players cannot see your modifications.

---

## ✨ Introduction

**ArdysaModsTools (AMT)** is a desktop utility that elevates your Dota 2 experience with a seamless interface to manage, customize, and apply client-side cosmetics — hero skins, terrains, weather effects, HUDs, couriers, and performance optimizations.

It runs entirely on your machine, modifies only local files, and lets you return to the vanilla game at any time with a single click.

The interface follows a clean, monochrome terminal-style design — consistent across the app and with [ardysamods.my.id](https://ardysamods.my.id) — rendered through an embedded WebView2 shell.

---

## 🎨 Key Features

| Feature | Description |
| :--- | :--- |
| **One-Click ModsPack** | Download and install the complete, curated community mod pack in a single click. |
| **Skin & Persona Selector** | Browse hero sets, individual item pieces (with slot-based mutual exclusion), full Personas, and Prismatic overlays via an interactive gallery. |
| **Style Card Variants** | Pick from multiple visual style variations for sets and items, previewed in an in-app modal. |
| **Miscellaneous Mods** | Toggle weather, custom terrains, HUD designs, cursor packs, music packs, kill/battle effects, and custom couriers/wards. |
| **Performance Tweaker** | Tune Dota 2 cvars (`autoexec.cfg`) and copy optimized launch options to maximize FPS. |
| **Auto-Patching** | Detects Dota 2 updates in the background and re-applies your active mods. |
| **Manual VPK Import** | Import your own custom `.vpk` mod files into the manager. |
| **Safe & Reversible** | Restore the vanilla game instantly via **Disable Mods** — official assets are never corrupted. |

### Why it's reliable

- **Resilient downloads** — a multi-CDN chain (Cloudflare R2 → mirrors) with automatic failover keeps installs working worldwide, even on slow or restricted networks.
- **Resumable transfers** — large downloads survive dropped connections and switch mirrors mid-stream.
- **Integrity-checked** — files are verified against a SHA-256 manifest before they're applied.
- **Safe writes** — changes to critical game files are transactional, with verification and rollback to prevent corruption.
- **Stays current** — reads the game's live data at runtime, so it keeps working across most Dota 2 patches.

---

## 🎨 Supported Mod Types

| Category | Description | Examples |
| :--- | :--- | :--- |
| **Hero Skins** | Full hero cosmetics, item pieces, and personas | Arcanas, Personas, custom mixes, weapon items |
| **Weather** | Custom weather effect overrides | Rain, Snow, Aurora, Moonbeam, Spring |
| **Terrain** | Replaces the default map terrain | TI Terrains, Seasonal Maps, Immortal Gardens |
| **HUD Skins** | Re-skins the in-game UI overlay | Tournament and themed HUDs |
| **Music Packs** | Replaces the default soundtrack | Custom and classic music |
| **Couriers & Wards** | Custom models with style selectors | Particle couriers, custom ward models |
| **Battle Effects** | Overrides kills/streaks/actions | TI streak banners, event effects |
| **Cursors** | Custom mouse pointer styles | Custom cursor packs |
| **Special Packs** | Archive-based external conversions | LowPoly Map, community UI themes |

---

## 📥 Installation

### System Requirements

| Component | Minimum | Recommended |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (64-bit, Build 19041+) | Windows 11 (64-bit) |
| **Framework** | None — self-contained runtime included | None |
| **Browser Engine** | [Microsoft WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) | WebView2 (pre-installed on Win 10/11) |
| **Game** | Dota 2 installed via Steam | Dota 2 on an SSD |

### Download & Install

1. Open the [**Releases**](https://github.com/Anneardysa/ArdysaModsTools/releases) page (or [ardysamods.my.id](https://ardysamods.my.id)).
2. Download the latest installer: `ArdysaModsTools_Setup_<version>.exe`.
3. Run the installer and complete the setup wizard.
4. Launch **ArdysaModsTools** from the desktop shortcut or Start Menu.

> [!TIP]
> The app installs to `%LocalAppData%\ArdysaModsTools`. Your configurations, favorites, and presets are stored separately in `%AppData%\ArdysaModsTools`, so they survive updates.

---

## 🚀 Quick Start

### 1. Initial setup
1. **Close Dota 2** if it is running.
2. Launch **ArdysaModsTools** (running as Administrator helps with path detection).
3. AMT auto-detects your Dota 2 install. If it can't, click **Manual Detect** and select your `dota 2 beta` folder.
4. Click **Install ModsPack** to fetch and apply the main assets.
5. Launch Dota 2 and enjoy.

### 2. Customizing hero skins
1. Open the **Skin Selector** in the sidebar.
2. Filter by hero or category (Legacy, Custom, Persona, Item, Base, Prismatic), or search by name.
3. Select your cosmetics — mutual exclusion is handled automatically.
4. For items with variants, click the **Style Card** to preview and pick a style.
5. Prismatic overlays are a Base add-on — select a **Base Hero** first to unlock the **Prismatic** category.
6. Click **Generate** to compile and apply your custom patch.

### 3. After a Dota 2 update
Game updates overwrite your cosmetic patch. To restore it:
- **Automatic** — with **PatchWatcher** enabled, AMT detects the update and prompts you to re-apply.
- **Manual** — open AMT and click **Patch Update**.

---

## 📚 Documentation

| Guide | |
| :--- | :--- |
| [User Guide](docs/user/USER_GUIDE.md) | Full walkthrough of every feature |
| [Quick Start](docs/user/QUICK_START.md) | Get running in minutes |
| [FAQ](docs/user/FAQ.md) | Common questions answered |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Fix common issues |
| [Security Policy](docs/dev/SECURITY.md) | Reporting vulnerabilities |
| [Changelog](CHANGELOG.md) | What changed in each release |

---

## ❓ FAQ

<details>
<summary><b>Is ArdysaModsTools safe to use?</b></summary>

Yes. AMT operates client-side only. It modifies local configuration files and compiles custom VPK files inside your local game path. It does **not** hook into the game process, inject code, or alter server-side data.

</details>

<details>
<summary><b>Can I get VAC banned for client-side modifications?</b></summary>

Client-side cosmetic modifications have historically been safe from VAC because they don't change game logic, memory, or official servers. However, modifying game files always carries a non-zero risk under Valve's Terms of Service. **Use at your own risk.**

</details>

<details>
<summary><b>Can other players see my custom skins?</b></summary>

**No.** All modifications are local. Other players see whatever standard cosmetics you have equipped on the Steam servers.

</details>

<details>
<summary><b>My mods disappeared after a game update. What do I do?</b></summary>

Dota 2 updates overwrite the main VPK index (`pak01_dir.vpk`). Launch AMT and click **Patch Update** to re-insert the mod hook. With PatchWatcher running, you'll be notified automatically.

</details>

<details>
<summary><b>How do I completely remove all modifications?</b></summary>

Click **Disable Mods** in the app to restore the original configuration. To be thorough, verify game files in Steam (_Dota 2 → Properties → Installed Files → Verify integrity of game files_).

</details>

<details>
<summary><b>The interface shows a blank white page or a WebView2 error.</b></summary>

AMT uses the Microsoft Edge WebView2 engine. If it's missing or corrupted, install the latest [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/).

</details>

---

## 🔧 Troubleshooting

| Issue | Likely Cause | Fix |
| :--- | :--- | :--- |
| **Crash at launch** | Corrupted WebView2 Runtime | Reinstall the WebView2 Evergreen Runtime |
| **Mods don't appear in game** | Game folder not detected | Run AMT as Administrator, **Auto Detect**, or select the folder manually |
| **Download failed / stalled** | CDN/ISP timeout | Check your connection; AMT auto-cycles to alternative mirrors |
| **"Operation blocked by lock"** | Dota 2 or Steam is running | Fully close the game/Steam and retry |
| **VPK extraction/recompile fail** | Stale game files | Run **Verify Integrity** in Steam, then re-apply mods |

For more, see the [Troubleshooting guide](docs/TROUBLESHOOTING.md).

---

## 🏆 Credits & Acknowledgments

- **Author & Lead Developer:** **Ardysa** ([@Anneardysa](https://github.com/Anneardysa))
- **Code signing:** [SignPath Foundation](https://signpath.org)
- The **Dota 2 modding community** for shared techniques and knowledge.
- **Valve Corporation** for creating Dota 2.

Built with open-source libraries including [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/), [ValveKeyValue](https://github.com/ValveResourceFormat/ValveKeyValue), [ImageSharp](https://github.com/SixLabors/ImageSharp), and [SharpCompress](https://github.com/adamhathcock/sharpcompress).

---

## ⚖️ Licensing & Trademarks

The released source code of ArdysaModsTools is licensed under the **GNU General Public License v3.0** — see [LICENSE](LICENSE).

The names **"ArdysaMods"**, **"ArdysaModsTools"**, **"AMT"**, the logo, visual identity, and domains (**`ardysamods.my.id`**, **`cdn.ardysamods.my.id`**) are **excluded** from the open-source license. Any derivative work must use a distinct name, logo, and its own update/CDN endpoints — see the [NOTICE](NOTICE) file.

---

<div align="center">

**© 2025-2026 Ardysa. All rights reserved.**

_Made with ❤️ for the Dota 2 community._

⭐ **Star this repository** if ArdysaModsTools helps you enjoy the game!

</div>
