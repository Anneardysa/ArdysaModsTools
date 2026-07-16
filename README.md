<div align="center">

<img src="Assets/Images/banner.jpg" alt="ArdysaModsTools" width="100%" />

# ArdysaModsTools

**The one-click cosmetic mod manager for Dota 2.**
Install, customize, and safely revert client-side skins, terrains, HUDs, and more.

[![Version](https://img.shields.io/github/v/release/Anneardysa/ArdysaModsTools?include_prereleases&style=flat-square&logo=github&logoColor=white&label=Version&color=00d4ff)](https://github.com/Anneardysa/ArdysaModsTools/releases) ![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?style=flat-square&logo=windows&logoColor=white) ![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) [![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=flat-square)](LICENSE) [![Downloads](https://img.shields.io/github/downloads/Anneardysa/ArdysaModsTools/total?style=flat-square&color=FF6B6B&logo=github&logoColor=white&label=Downloads)](https://github.com/Anneardysa/ArdysaModsTools/releases) [![Stars](https://img.shields.io/github/stars/Anneardysa/ArdysaModsTools?style=flat-square&color=yellow&logo=github&logoColor=black)](https://github.com/Anneardysa/ArdysaModsTools/stargazers)

---

[Preview](#-preview) · [Features](#-features) · [Safety](#-safety--reliability) · [Install](#-installat
ion) · [Quick Start](#-quick-start) · [How It Works](#-how-it-works) · [Docs](#-documentation) · [FAQ](
docs/user/FAQ.md) · [Website](https://ardysamods.my.id)

</div>

<div align="center">

<img src="docs/user/images/shell.png" alt="ArdysaModsTools main window" width="900" />

</div>

> [!CAUTION]
> Not affiliated with Valve or Steam. All mods are **client-side only** no one else sees them. Modifying game files can violate Valve's Terms of Service. **Use at your own risk.**

## 📖 Overview

**ArdysaModsTools (AMT)** is a Windows desktop app for managing client-side cosmetic mods in Dota 2 hero skins, terrains, weather, HUDs, couriers, music, and performance tweaks. It writes only local files, verifies every download, and lets you return to the vanilla game at any time with a single click.

The UI is a clean monochrome shell rendered through an embedded WebView2 view, consistent with [ardysamods.my.id](https://ardysamods.my.id).

## 🎬 Preview

<div align="center">

<img src="https://cdn.ardysamods.my.id/image/preview-1.gif" alt="ArdysaModsTools in action — ModsPack i
nstall" width="820" />

<img src="https://cdn.ardysamods.my.id/image/preview-2.gif" alt="ArdysaModsTools in action — Skin Selec
tor" width="820" />

<img src="https://cdn.ardysamods.my.id/image/preview-3.gif" alt="ArdysaModsTools in action — Miscellane
ous & Performance" width="820" />

</div>


## ✨ Features

| Feature | What it does |
| :--- | :--- |
| **One-Click ModsPack** | Download and apply the full curated community mod pack in one click. |
| **Skin & Persona Selector** | Browse hero sets, individual items (with slot-based mutual exclusion), Personas, and Prismatic overlays in an interactive gallery. |
| **Miscellaneous Mods** | Toggle weather, terrains, HUDs, cursors, music packs, kill/battle effects, and custom couriers & wards. |
| **Performance Tweaker** | Tune Dota 2 cvars (`autoexec.cfg`) and copy optimized launch options for more FPS. |
| **Auto-Patching** | Detects Dota 2 updates in the background and re-applies your active mods. |
| **Safe & Reversible** | **Disable Mods** restores the vanilla game instantly official assets are never corrupted. |

<details>
<summary><b>Supported mod types</b></summary>

| Category | Examples |
| :--- | :--- |
| **Hero Skins** | Sets, item pieces, Arcanas, Personas, Prismatic overlays |
| **Terrain** | TI terrains, seasonal maps, LowPoly map |
| **Weather** | Rain, Snow, Aurora, Moonbeam, Spring |
| **HUD Skins** | Tournament and themed in-game UI |
| **Music Packs** | Custom and classic soundtracks |
| **Couriers & Wards** | Custom models with style selectors |
| **Battle Effects** | Kill streaks, event effects |
| **Cursors** | Custom mouse pointer packs |
| **Special Packs** | Archive-based conversions & community themes |

</details>

<div align="center">
<table>
<tr>
<td><img src="docs/user/images/skin-selector.png" alt="Skin Selector hero gallery" width="900" /></td>
</tr>
<tr>
<td align="center"><b>Skin Selector</b></td>
</table>

<table>
<tr>
<td><img src="docs/user/images/miscellaneous.png" alt="Miscellaneous mods" width="440" /></td>
<td><img src="docs/user/images/performance.png" alt="Performance tweaker" width="440" /></td>
</tr>
<tr>
<td align="center"><b>Miscellaneous Mods</b></td>
<td align="center"><b>Performance Tweaker</b></td>
</tr>
</table>

</div>

## 🛡️ Safety & Reliability

Because AMT rewrites real game files, correctness and rollback safety come first:

- **Integrity-checked** every downloaded file is verified against a SHA-256 manifest before it's applied.
- **Transactional writes** changes are staged, verified, then atomically swapped, with automatic rollback on any failure.
- **Resilient downloads** a multi-CDN chain (Cloudflare R2 → mirrors) with automatic failover and resumable transfers keeps installs working worldwide.
- **One-click revert** **Disable Mods** returns you to the original game; official VPKs are never overwritten in place.

## 📥 Installation

**Requirements:** Windows 10 (64-bit, Build 19041+) or Windows 11 · Dota 2 via Steam · [Microsoft WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (pre-installed on Win 10/11).

1. Open the [**Releases**](https://github.com/Anneardysa/ArdysaModsTools/releases) page (or [ardysamods.my.id](https://ardysamods.my.id)).
2. Download `ArdysaModsTools_Setup_<version>.exe` and run the installer.
3. Launch **ArdysaModsTools** from the desktop shortcut or Start Menu.

> [!TIP]
> Your configs and presets live in `%AppData%\ArdysaModsTools`, separate from the app, so they survive updates.

## 🚀 Quick Start

1. **Close Dota 2**, then launch AMT. It auto-detects your install or click **Manual Detect** and pick your `dota 2 beta` folder.
2. Click **Install ModsPack** for the full curated pack, or open the **Skin Selector** to pick specific cosmetics and hit **Generate**.
3. After a Dota 2 update overwrites your patch, click **Patch Update** (or let **PatchWatcher** re-apply it automatically).

Full walkthrough: [User Guide](docs/user/USER_GUIDE.md).

## 🔧 How It Works

AMT is built as a maintainable, file-safe desktop app rather than a game hook nothing is injected into the Dota 2 process.

- **Platform** .NET 8 (C#), Windows 10/11 x64.
- **UI** hybrid **WinForms + WebView2** shell; JS ↔ C# only over the WebView message bridge.
- **Architecture** strict **MVP** (View → Presenter → Service) with constructor-based dependency injection; every service sits behind an interface.
- **Delivery** resilient **multi-CDN** asset chain with **SHA-256** verification on every download.
- **File safety** all writes to the game folder go through a transactional pipeline: extract → verify → atomic swap → rollback.

**Languages**
---

<p align="center">
  <img src="docs/user/images/lang/csharp.svg" alt="C#" title="C#" height="44" />&nbsp;&nbsp;
  <img src="docs/user/images/lang/python.svg" alt="Python" title="Python" height="44" />&nbsp;&nbsp;
  <img src="docs/user/images/lang/html5.svg" alt="HTML5" title="HTML5" height="44" />&nbsp;&nbsp;
  <img src="docs/user/images/lang/javascript.svg" alt="JavaScript" title="JavaScript" height="44" />
</p>
  
<div align="center">
**Built with** [WebView2](https://developer.microsoft.com/microsoft-edge/webview2/), [ValveKeyValue](https://github.com/ValveResourceFormat/ValveKeyValue), [ImageSharp](https://github.com/SixLabors/ImageSharp), [SharpCompress](https://github.com/adamhathcock/sharpcompress), and HLLib / HLExtract.
</div>

## 📚 Documentation
<div align="center">

| Guide | |
| :--- | :--- |
| [User Guide](docs/user/USER_GUIDE.md) | Full walkthrough of every feature |
| [Quick Start](docs/user/QUICK_START.md) | Get running in minutes |
| [FAQ](docs/user/FAQ.md) | Common questions answered |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Fix common issues |
| [Contributing](docs/dev/CONTRIBUTING.md) | Branching, workflow, and code style |
| [Security Policy](docs/dev/SECURITY.md) | Reporting vulnerabilities |
| [Changelog](CHANGELOG.md) | What changed in each release |

</div>

## 📊 Project Activity

<div align="center">

![Repobeats analytics](https://repobeats.axiom.co/api/embed/37af48bc220aaa1f0c79d9f2f0860127b2e69329.svg "Repobeats analytics image")



## 🏆 Credits

**Author & Lead Developer** — **Ardysa** ([@Anneardysa](https://github.com/Anneardysa))
**Code signing** — [SignPath Foundation](https://signpath.org)
The **Dota 2 modding community**, and **Valve** for the game.
[Dota 2 Skinchanger](https://dota2changer.com)
[Darkness](https://t.me/s/Darkness_Logovo)
[Kisilev](https://vk.com/id363951132)
[Source 2 Viewer](https://github.com/ValveResourceFormat/ValveResourceFormat)


</div>

## ⚖️ License & Trademarks

Source code is licensed under the **GNU General Public License v3.0** — see [LICENSE](LICENSE).

The names **"ArdysaMods"**, **"ArdysaModsTools"**, **"AMT"**, the logo, visual identity, and domains (`ardysamods.my.id`, `cdn.ardysamods.my.id`) are **excluded** from the license. Derivative work must use a distinct name, logo, and its own update/CDN endpoints — see [NOTICE](NOTICE).

---

<div align="center">

**© 2025-2026 Ardysa.** Made for the Dota 2 community.

⭐ **Star this repo** if AMT helps you enjoy the game.

</div>
