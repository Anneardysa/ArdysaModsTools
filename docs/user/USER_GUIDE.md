# ArdysaModsTools User Guide

**The Ultimate Dota 2 Mod Manager**

![Banner](images/shell.png)

---

## Table of Contents

1. [What is ArdysaModsTools?](#what-is-ardysamodstools)
2. [System Requirements](#system-requirements)
3. [Installation](#installation)
4. [Getting Started](#getting-started)
5. [Features Overview](#features-overview)
6. [Play Dota 2](#play-dota-2)
7. [Main Features](#main-features)
   - [Mod Installation](#mod-installation)
   - [Hero Set Generation](#hero-set-generation)
   - [Miscellaneous Mods](#miscellaneous-mods)
8. [Status & Verification](#status--verification)
9. [Personalization](#personalization)
10. [Advanced Features](#advanced-features)
11. [Troubleshooting](#troubleshooting)
12. [FAQ](#faq)
13. [Support & Community](#support--community)

---

## What is ArdysaModsTools?

**ArdysaModsTools (AMT 2.0)** is a powerful Windows desktop application designed to help Dota 2 players easily install and manage custom cosmetic modifications. With AMT 2.0, you can:

- 🎮 **Install curated mod packs** with one click
- 🦸 **Create custom hero skins** from community sets
- 🌦️ **Customize weather, terrain, and HUD** elements
- 🔄 **Keep mods working** after game updates
- 🔧 **Auto-detect** your Dota 2 installation

> [!IMPORTANT]
> This tool **only modifies cosmetic elements** and does not affect gameplay. Use at your own risk. Always backup your game files before modding.

---

## System Requirements

| Component            | Requirement                                                                                                           |
| -------------------- | --------------------------------------------------------------------------------------------------------------------- |
| **Operating System** | Windows 10/11 (64-bit)                                                                                                |
| **Runtime**          | Bundled (self-contained — no separate .NET install needed)                                                            |
| **Browser Runtime**  | [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (usually pre-installed on Windows 10/11) |
| **Disk Space**       | ~2 GB free — VPK extraction during generation needs the headroom                                                      |
| **Dota 2**           | Installed via Steam                                                                                                   |

> [!NOTE]
> The app is self-contained — the .NET 8 runtime is bundled with the installer. No separate runtime installation is needed.

---

## Installation

### Download & Install

1. **Download the Installer**
   - Get `ArdysaModsTools_Setup_x64.exe` from the official source

2. **Run the Installer**
   - Right-click the installer and select **Run as Administrator**
   - Follow the installation wizard

3. **Dependency Check**
   - The .NET runtime is **bundled** — nothing to install separately
   - The installer auto-installs the WebView2 Runtime if it's missing
   - Any previous version of AMT is removed automatically first

4. **Complete Installation**
   - Choose installation location (default: `%LocalAppData%\ArdysaModsTools`)
   - Optionally create a desktop shortcut
   - Click **Install**

5. **Launch Application**
   - Check **Launch ArdysaModsTools** at the end of installation
   - Or launch from Start Menu or Desktop shortcut

> [!WARNING] > **Important**: Close Dota 2 completely before launching ArdysaModsTools. The application cannot run while Dota 2 is active.

### Updating

When a new version is available:

- The application will notify you automatically
- Download the new installer
- The installer will automatically remove the old version before installing the new one

---

## Getting Started

### First Launch & Onboarding Guide

When you first launch ArdysaModsTools, the **Newcomer Onboarding Guide** will automatically trigger. This interactive step-by-step visual spotlight highlights each critical control on the main dashboard to help you get situated.

The guide will walk you through:
1. **Auto Detect / Manual Detect**: Finding your Dota 2 installation.
2. **Skin Selector & Miscellaneous**: Navigating to mod categories.
3. **Install & Patch Update**: Applying and maintaining your mods.
4. **Play Dota 2**: Launching the game with your mods repaired and up to date.
5. **Console Logs & Settings**: Inspecting errors, changing theme/language, clearing caches.

*Note: You can re-run the Onboarding Guide anytime from the Settings page by clicking the "Show Guide" button.*

#### Step 1: Detect Dota 2 Installation

The application needs to know where Dota 2 is installed on your computer.

**Option A: Auto Detect (Recommended)**

1. Click the **Auto Detect** button
2. The app will search common locations and Steam libraries
3. If found, the path will appear in the target path field
4. Status will show "Detected successfully"

**Option B: Manual Selection**

1. Click the **Manual Select** button
2. Navigate to your Dota 2 installation folder
   - Default: `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`
3. Select the `dota 2 beta` folder
4. Click **OK**

> [!TIP]
> If auto-detection fails, your Dota 2 might be installed in a custom Steam library. Use manual selection to locate it.

#### Step 2: Install Mods

Once Dota 2 is detected:

1. Click the **Install** button
2. Choose installation method:
   - **Auto Install**: Download the latest mod pack from the server (recommended)
   - **Manual Install**: Use a VPK file you already have
3. Wait for the installation to complete
4. Status will show "Ready" in green when successful

#### Step 3: Press PLAY DOTA 2

Your mods are installed. Start the game with the **PLAY DOTA 2** button in AMT rather than from
Steam — see [Play Dota 2](#play-dota-2) below for why that matters.

---

## Features Overview

| Feature               | Description                                            | Access Point           |
| --------------------- | ------------------------------------------------------ | ---------------------- |
| **Play Dota 2**       | Repairs your mods if needed, then launches the game    | Sidebar (primary button) |
| **Install ModsPack**  | Download and install the main curated mod pack         | Sidebar                |
| **Skin Selector**     | Create custom hero skins from community sets           | Sidebar                |
| **Miscellaneous**     | Customize weather, terrain, HUD, and more              | Sidebar                |
| **Patch Update**      | Re-apply the game-config patch after a Dota 2 update   | Sidebar                |
| **Disable Mods**      | Restore vanilla Dota 2                                 | Sidebar                |
| **Performance Tweak** | Optimize Dota 2 cvar & `autoexec.cfg` settings         | Header icon (🔧)       |
| **Settings**          | Theme, language, cache control, re-run the guide       | Header icon            |
| **What's New**        | Release notes for the version you're running           | Header icon            |

---

## Play Dota 2

The **PLAY DOTA 2** button is the primary action in the sidebar. It is **not** just a shortcut
to Steam — it exists to stop the single most common way a modded Dota 2 breaks.

### Why not just launch from Steam?

Your mod package carries its own copy of Dota 2's item definitions, and those shadow the game's
own. When Valve ships a patch that changes those definitions, the game starts on data that no
longer matches its content — and **crashes on startup**.

Pressing Play fixes that automatically. Launching from Steam skips the fix entirely.

### What happens when you press it

1. **Checks Steam first.** If a Dota 2 update is pending or downloading, AMT waits — showing the
   download progress — rather than launching into it. This matters: if AMT handed the game to
   Steam with an update queued, Steam would install the update *after* the repair and undo it.
2. **Rebuilds your mod package** against whatever the game now ships. Nothing is redownloaded —
   the data needed is already on your disk. This step is skipped when nothing has changed
   (the check is a quick hash comparison, not a rebuild).
3. **Launches the game**, then watches until Dota 2 actually appears.

If an update *did* land, AMT stops and asks — *"Dota 2 has been updated — start it now?"* —
instead of launching by itself. Waiting out a patch can take many minutes and starting a freshly
patched game while you're away from the keyboard isn't AMT's call. Everything before that
question still happens automatically; your package is already rebuilt by the time you're asked.

### The launch panel

While the flow runs, a panel shows what it's doing. Every step except the final "Dota 2 is
running" can be cancelled.

| Panel says                      | Meaning                                                       |
| ------------------------------- | ------------------------------------------------------------- |
| **Checking Steam**              | Making sure a Dota 2 update isn't about to land               |
| **Steam is updating Dota 2**    | Waiting it out. Your mods will be rebuilt automatically — do nothing |
| **Rebuilding your mod package** | Repairing the package for the current game version            |
| **Dota 2 has been updated**     | Update done, package rebuilt — press **Start Dota 2** when ready |
| **Starting Dota 2**             | Handing the game over to Steam                                |
| **Waiting for Dota 2**          | Steam is starting the game; closes by itself                  |
| **Could not prepare your mods** | The rebuild failed. **Nothing was changed** — your existing package is untouched |
| **Dota 2 did not start**        | Steam accepted the launch but the game never appeared. Your mods are ready; just start it again |

### Things worth knowing

- **If Dota 2 is already running**, Play does nothing but tell you so. The rebuild swaps files
  the running game holds open, so it would spend minutes only to fail on the last step.
- **A failed rebuild is safe.** The repair is transactional — it either completes or leaves your
  existing package exactly as it was.
- **Steam must be running** for the launch handoff to work.

### Package Sync

The **Package Sync** chip in the status panel is the same check, shown before you press Play.
Red means your package is older than the game. Its **Fix** action runs the repair *without*
launching — useful if you want the rebuild out of the way now and will play later.

> [!IMPORTANT]
> **Patch Update and Play do different jobs.** Patch Update restores the *game config*
> (`gameinfo_branchspecific.gi`, signatures) so mods load at all. Play rebuilds the *mod package*
> so its item data matches the game. After a Dota 2 patch you generally want both — and the
> status panel tells you which one is outstanding.

---

## Main Features

### Mod Installation

![Main Window](images/shell.png)

The primary feature is installing curated mod packs that include multiple cosmetic enhancements.

#### Installing Mods

1. **Click Install** button
2. **Choose Auto Install** for the latest mods from the server
3. The application will:
   - Download the latest ModsPack
   - Validate the VPK file
   - Copy to your Dota 2 folder (`game/_ArdysaMods/`)
   - Patch game configuration files for mod compatibility
4. Monitor progress in the console at the bottom
5. When complete, status shows **Ready** (green)

#### Auto Install vs Manual Install

**Auto Install**:

- Always gets the latest version
- Automatic validation
- Recommended for most users

**Manual Install**:

- Use if you have a specific VPK file
- Must contain `_ArdysaMods` marker to be valid
- Useful for offline installation

#### Disabling Mods

To temporarily disable mods without uninstalling:

1. Click the **Disable** button
2. This will restore original game configuration
3. Mod files remain in place
4. Click **Install** again to re-enable

#### Updating Mods

When game updates or new mod versions are available:

1. Status indicator will show **Need Update** (orange)
2. Click the **Patch Update** button
3. The patch will update signatures and game configuration

> [!IMPORTANT]
> After each Dota 2 game update, run **Patch Update** to ensure mods continue working properly.

---

### Hero Set Generation

![Hero Selection](images/skin-selector.png)

Create custom hero skins by selecting from community-created cosmetic sets.

#### How It Works

The Hero Set Generator:

1. Downloads base game files
2. Merges custom set assets (models, textures, particles)
3. Patches item definitions
4. Recompiles into a VPK file
5. Replaces the original

#### Using Hero Set Generation

1. **Click Select Hero** button from the main window.
2. **Browse Heroes**:
   - Scroll through the grid of hero portraits.
   - Use the search bar to find specific heroes.
   - Click the star icon to favorite heroes (favorites stay pinned to the top).
3. **Configure Customization Layers**:
   When you click on a hero card, a modal will show the available options divided into categories:
   * **Legacy Sets**: Classical, curated full-body hero sets from the server.
   * **Custom Sets**: Mixed set variations compiled by the community.
   * **Items**: Individual cosmetic items (e.g., custom weapons, shoulders).
   * **Base Heroes**: Basic default model modifications.
   * **Persona** *(Magenta/Pink themed)*: Full model replacement sets (e.g., Baby Invoker, female Anti-Mage).

4. **Mutual Exclusion & Slot Verification**:
   * **Slot Tag Exclusion**: Selecting an individual item automatically deselects any currently active item that shares the same slot tag (e.g., you cannot equip two weapons). Equipped items display their slot tags as green badges (e.g., `[shoulder]`, `[weapon]`).
   * **Persona Exclusion**: Selecting a **Persona** acts as a full model override. It will automatically clear and disable all individual Items and Base Hero selections for that hero to prevent model clipping and crashes.
5. **Add Multiple Heroes**:
   - Select sets and customizations for as many heroes as you like.
6. **Generate**:
   - Click the **Generate** button.
   - The priorities in the merging pipeline are automatically handled: `Set / Custom Set / Persona → Selected Items → Base Hero` (so base hero overrides items, and items override sets/personas).
   - Monitor progress via the sidebar preview panel on the progress overlay.

#### Batch Generation

You can generate multiple heroes at once:

- Select sets for multiple heroes
- Click **Generate**
- The application processes them sequentially
- Progress bar shows overall completion

#### Favorites System

Mark frequently used heroes as favorites:

- Click the star icon on any hero card
- Favorites appear at the top of the list
- Easier access to your preferred heroes

> [!TIP]
> Hero set generation can take 2-5 minutes per hero depending on set size. Plan accordingly when generating multiple heroes.

---

### Miscellaneous Mods

![Misc Mods](images/miscellaneous.png)

Customize additional game elements beyond hero skins.

#### Available Categories

**🌦️ Weather Effects**

- Moonbeam
- Aurora
- Snow
- Ash
- Pestilence
- And more...

**🗺️ Terrain/Map**

- Custom map skins
- Different visual themes

**🎨 HUD Modifications**

- Interface customizations
- UI element replacements

**⚔️ Battle Effects**

- TI-themed kill/ability effects (Aghanim, Nemestice, TI 2015–2022)

**🐴 Courier**

- Custom courier skins with style support
- Up to 2 ethereal particle effects per courier

**🔮 Ward**

- Custom ward skins with model extraction
- Style variants and particle effects

**🔊 Audio Mods**

- Custom music packs

**🎯 Cursor**

- Custom cursor skins

**⭐ Special**

- Full ZIP-based mod packs (e.g., LowPoly Map)
- Mutual exclusion with Map option

#### Using Miscellaneous Mods

1. **Click Miscellaneous** button from main window
2. **Choose Generation Mode**:
   - **Clean Generate**: Start fresh, replaces all existing misc mods
   - **Add to Current**: Merge with existing modifications
3. **Select Options**:
   - Browse through categories
   - Check boxes or select from dropdowns
   - Multiple selections allowed
4. **Apply Changes**:
   - Click **Generate** or **Apply** button
   - Wait for compilation
   - Status will confirm completion

#### Generation Modes Explained

| Mode               | When to Use                        | Effect                                         |
| ------------------ | ---------------------------------- | ---------------------------------------------- |
| **Clean Generate** | First time, or reset all changes   | Extracts fresh VPK, applies only selected mods |
| **Add to Current** | Adding more mods to existing setup | Uses existing extraction, adds new mods        |

> [!NOTE]
> Clean Generate takes longer but ensures a clean slate. Use Add to Current for quick additions.

---

### 🔧 Performance Tweak (Autoexec Optimizer)

Optimize Dota 2 launch parameters and custom game settings to get the maximum frames per second (FPS) and minimum latency.

#### How to Use

1. Click the **Tweak** (🔧) button located in the top-right corner of the MainForm header.
2. This opens the **Performance Tweaker** dialog.
3. Configure your settings across these tabs:
   * **FPS & Display**: Cap gameplay and UI frame rates, modify viewport scaling, and set screen brightness.
   * **Visual Toggles**: Enable/disable expensive rendering options (e.g., portrait animations, normal maps, grass quality, wind effects on trees, and ambient creatures) to boost FPS.
   * **Quality**: Customize texture streaming mip bias and particle fallback modes.
   * **Engine Tweaks**: Adjust particle simulation limits and disable background sleeps.
   * **VSync & Latency**: Optimize latency sleeps and lag limiters.
   * **Network**: Adjust transmission rate, cl_updaterate, and cl_interp_ratio to improve connection stability.
4. Click **Apply Settings** to save these CVAR configurations directly to `autoexec.cfg` in your game files. AMT saves this file using atomic file transactions to prevent config corruption.
5. You can also export the settings to a custom `.cfg` file using the **Export Config** option.

---

## Status & Verification

AMT checks your install on every launch and shows the result as a status badge plus four
**verification chips**. Each chip is independent, and each has its own fix.

| Chip                  | Green means                                                     | If it's red                                |
| --------------------- | ---------------------------------------------------------------- | ------------------------------------------ |
| **Patch Integrity**   | The game config and signature files are still patched            | Click **Patch Update**                     |
| **Search Paths**      | Dota 2 is still told to load your mods folder                    | Click **Patch Update**                     |
| **Process Elevation** | AMT can actually write into your Dota 2 folder                   | Restart AMT **as Administrator**           |
| **Package Sync**      | Your mod package matches the game's current item data            | Press **PLAY DOTA 2**, or the chip's **Fix** |

> [!NOTE]
> **Package Sync going red after a Dota 2 patch is normal, not a bug.** It's the check that
> catches the crash-on-startup problem before you hit it. See [Play Dota 2](#play-dota-2).

### Verifying files

Right-click **Patch Update** → **Verify Mod Files** for a detailed report: required files
present, VPK integrity, and configuration patches, each listed individually.

---

## Personalization

### Theme

AMT ships light and dark themes, switchable in **Settings**. The interface is monochrome by
design, matching [ardysamods.my.id](https://ardysamods.my.id).

### Language

Change the interface language in **Settings** — it applies immediately, no restart. Available:
English, Spanish, German, French, Portuguese, Russian, Simplified Chinese, Traditional Chinese.

Hero names, item names, and Dota jargon deliberately stay in English so they match what you see
in-game and in the community. Spotted an awkward translation?
[Report it](https://github.com/Anneardysa/ArdysaModsTools/issues/new?template=4-translation.yml).

### Notifications

Long operations report back with a toast in the corner of the window; when AMT is minimised or
hidden in the tray, the same notification arrives as a tray balloon instead.

### Re-running the guide

**Settings → Show Guide** replays the newcomer onboarding walkthrough at any time.

---

## Advanced Features

### Patch Management

After Dota 2 game updates, some files need re-patching:

1. **Automatic detection** — AMT checks your mod status on launch, and **PatchWatcher** notices
   Dota 2 updates in the background while AMT is open.
2. **Manual patching** — click **Patch Update**. This rewrites the game configuration and
   signature files.

> [!IMPORTANT]
> Patch Update does **not** rebuild your mod package — that's what **PLAY DOTA 2** does. The two
> fix different things; the verification chips tell you which one you need.

### Console Logs

The console at the bottom shows real-time operation logs:

- Download progress
- File operations
- Errors and warnings
- Completion status

**Copy Logs**:

- Click the **Copy** button above console
- Paste logs when reporting issues

### Clear Temp Files

To free up disk space:

- The application stores temporary files during operations
- These are automatically cleaned on exit
- Manual cleanup happens during reinstall

---

## Troubleshooting

### Common Issues

#### Issue: Can't Launch Application

**Error**: "Cannot run while dota2.exe is active"

**Solution**:

- Close Dota 2 completely
- Check Task Manager for `dota2.exe` process
- End the process if found
- Launch AMT again

---

#### Issue: Auto Detect Fails

**Error**: "Could not detect Dota 2"

**Solutions**:

1. Use **Manual Select** instead
2. Check Dota 2 is installed via Steam
3. Verify installation by launching Dota 2 from Steam
4. Try detecting again after verifying

---

#### Issue: Installation Fails

**Error**: Various installation errors

**Solutions**:

1. **Run as Administrator**: Right-click app icon → Run as Administrator
2. **Check Disk Space**: Ensure at least 500MB free
3. **Disable Antivirus**: Temporarily disable if blocking VPK operations
4. **Verify Dota 2**: Right-click Dota 2 in Steam → Properties → Local Files → Verify Integrity
5. **Check Logs**: Copy console logs and see specific error

---

#### Issue: Mods Not Visible In-Game

**Problems**: Installed mods don't appear in Dota 2

**Solutions**:

1. **Check Status**: Status should show "Ready" (green)
2. **Run Patch**: Click Patch Update
3. **Restart Dota 2**: Completely exit and relaunch
4. **Verify Installation**:
   - Check the folder exists: `dota 2 beta/game/_ArdysaMods/`
   - Check `pak01_dir.vpk` is present
5. **Reinstall**: Click Disable, then Install again

---

#### Issue: Game Update Breaks Mods

**Problem**: After Dota 2 update, mods stop working

**Solution**:

1. Launch AMT
2. Status will show "Need Update" (orange)
3. Click **Patch Update**
4. Wait for completion
5. Relaunch Dota 2

---

#### Issue: AMT won't start at all

**Solutions**:

1. **Read `startup_log.txt`** — it sits next to `ArdysaModsTools.exe` and is overwritten on
   every launch, so grab it right after a failed start. It usually names the exact cause.
2. **Install the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)** —
   the entire interface needs it. It's normally pre-installed on Windows 10/11.
3. **Reinstall.** If your antivirus stripped a file from the install, AMT can fail to start.

> [!NOTE]
> You do **not** need to install .NET separately. The runtime ships inside the app. If an older
> guide told you to download the .NET 8 Desktop Runtime, that no longer applies.

---

### Performance Issues

#### Slow Generation

Hero set generation taking very long:

**Causes**:

- Large set files
- Slow internet connection (for downloads)
- Antivirus scanning VPK operations

**Solutions**:

- Be patient (2-5 minutes per hero is normal)
- Ensure stable internet connection
- Exclude AMT folder from antivirus scanning
- Close other applications

---

### Error Messages

AMT error codes look like `DL_006` or `VPK_003`. The prefix says which part failed:

| Prefix   | Subsystem                          | Usual fix                                     |
| -------- | ---------------------------------- | --------------------------------------------- |
| `DL_`    | Downloading assets from the CDN    | Check internet/firewall and retry. `DL_009` means your build is too old — update AMT |
| `VPK_`   | Packing / extracting game archives | Close Dota 2, run as Administrator, free disk space |
| `PATCH_` | Patching Dota 2 game files         | Verify the Dota 2 install in Steam            |
| `GEN_`   | Hero set generation                | Try the hero on its own to isolate it         |
| `CFG_`   | Dota 2 / Steam detection, settings | Use **Manual Select** to point at `dota 2 beta` |
| `MISC_`  | Miscellaneous mods                 | Try **Clean Generate** instead of Add to Current |
| `UPD_`   | Updating AMT itself                | Download the installer manually from Releases |

> [!TIP]
> Always copy console logs when reporting errors. Click the **Copy Console** button and paste when asking for help.

---

## FAQ

### 🛡️ Ban & Safety Questions

#### ❓ Will I get VAC banned for using AMT?

> [!IMPORTANT]
> **Short answer: No known bans from cosmetic mods, but use at your own risk.**

Here's what AMT does and does **NOT** do:

| AMT Does ✅                                | AMT Does NOT ❌                              |
| ------------------------------------------ | -------------------------------------------- |
| Modify local cosmetic VPK files            | Inject into game processes or memory         |
| Patch `gameinfo_branchspecific.gi` for mod loading | Interact with VAC or anti-cheat systems |
| Replace textures, models, particles        | Connect to Valve's online services           |
| Work only when Dota 2 is **closed**        | Modify game behavior or give advantages      |
| Store all changes in `_ArdysaMods/` folder | Touch any files outside the Dota 2 directory |

**Why it's considered safe:**

1. **Client-side only** — Other players cannot see your mods
2. **File-based modifications** — No process injection, no DLL hooking, no memory editing
3. **VAC targets cheats** — VAC detects runtime modifications (wallhacks, aimbots, memory hacks). File-based cosmetic mods do not trigger VAC
4. **Fully reversible** — Click "Disable Mods" to instantly restore vanilla Dota 2
5. **Historical precedent** — Cosmetic file mods have been used by the Dota 2 community for years without VAC bans

> [!CAUTION]
> **Valve's policies can change at any time.** While there have been no known bans for cosmetic-only file mods, Valve has the right to change their Terms of Service. AMT's authors are not responsible for any account actions.

---

#### ❓ Is this a cheat or a hack?

**No.** AMT is a cosmetic mod manager. It:

- Does **not** give any gameplay advantage
- Does **not** modify game logic, abilities, or mechanics
- Does **not** show hidden information (enemy positions, cooldowns, etc.)
- Only changes what **you** see on your screen (hero skins, weather, terrain, HUD)

---

#### ❓ Can other players see my mods?

**No.** All modifications are **client-side only**. Other players see the default game assets. Your mods only affect your local game rendering.

---

#### ❓ Can Valve detect that I'm using mods?

AMT modifies files in the game directory, which is something Dota 2 naturally reads on launch. However:

- AMT does **not** run while Dota 2 is active
- No hooks, injections, or runtime modifications occur
- The modifications are identical in nature to how custom games and workshop content work

---

#### ❓ What if Valve bans cosmetic mods in the future?

If Valve changes their policy:

1. Click **"Disable Mods"** in AMT to instantly restore vanilla Dota 2
2. Or verify game files via Steam: _Right-click Dota 2 → Properties → Local Files → Verify integrity of game files_
3. All changes are fully reversible with zero trace

---

#### ❓ Is AMT open-source? Can I trust it?

Yes, under the **GNU General Public License v3.0** — the source is published at
[GitHub](https://github.com/Anneardysa/ArdysaModsTools) and you can read and audit all of it.

Two honest caveats about that repository:

- It is a **published mirror** of a private development repo, updated automatically on every
  change. It is one-way, so pull requests aren't merged there — see
  [CONTRIBUTING.md](https://github.com/Anneardysa/ArdysaModsTools/blob/main/.github/CONTRIBUTING.md).
- **Source comments are stripped** from `.cs` files when publishing. The code is complete and
  compiles; it just isn't annotated.

Releases are also Authenticode-signed by the [SignPath Foundation](https://signpath.org), so you
can verify a download came from us and hasn't been tampered with.

---

### 🎮 Usage

#### ❓ How do I install AMT?

1. Download `ArdysaModsTools_Setup_x64.exe` from [GitHub Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
2. **Close Dota 2** completely
3. Run the installer as **Administrator**
4. Follow the setup wizard
5. Launch AMT from Desktop or Start Menu

---

#### ❓ How do I detect my Dota 2 installation?

- **Auto Detect** (recommended): Click the "Auto Detect" button — AMT searches common Steam library paths
- **Manual Select**: If auto-detection fails, click "Manual Select" and browse to your `dota 2 beta` folder
   - Default path: `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`

---

#### ❓ How do I install the mod pack?

1. Click **"Install"** button
2. Choose **"Auto Install"** (downloads latest mod pack from CDN)
3. Wait for download, validation, and installation
4. Status shows **"Ready" (green)** when complete
5. Launch Dota 2 and enjoy!

---

#### ❓ My mods stopped working after a Dota 2 update. What do I do?

This is **normal behavior**. Dota 2 updates overwrite modded files.

**Fix:**

1. Open AMT
2. Status will show **"Need Update" (orange)**
3. Click **"Patch Update"**
4. Wait for completion
5. Relaunch Dota 2

> [!TIP]
> Enable **PatchWatcher** to get automatic notifications when Dota 2 updates are detected.

---

#### ❓ How do I create custom hero skins?

1. Click **"Skin Selector"** from the main window
2. Browse or search for heroes
3. Click a hero card and choose a cosmetic set from the dropdown
4. Repeat for as many heroes as you want
5. Click **"Generate"**
6. Wait 2–5 minutes per hero

---

#### ❓ Can I select multiple heroes at once?

**Yes!** Select sets for as many heroes as you want before clicking "Generate". The app processes them sequentially.

---

#### ❓ How do I favorite heroes?

Click the **star icon** on any hero card. Favorites appear at the top of the list for quick access.

---

#### ❓ What misc mods are available?

| Category           | Examples                                |
| ------------------ | --------------------------------------- |
| **Weather**        | Moonbeam, Aurora, Snow, Ash, Pestilence |
| **Terrain**        | TI terrains, seasonal maps              |
| **HUD**            | Custom interface themes and overlays    |
| **Battle Effects** | TI-themed ability/kill effects          |
| **Music**          | Custom music packs                      |
| **Cursor**         | Custom cursor skins                     |

---

#### ❓ What's the difference between "Clean Generate" and "Add to Current"?

| Mode               | Use When                     | Effect                               |
| ------------------ | ---------------------------- | ------------------------------------ |
| **Clean Generate** | First time or resetting all  | Fresh extraction, only selected mods |
| **Add to Current** | Adding more mods to existing | Merges with current mods (faster)    |

---

#### ❓ How do I temporarily disable mods?

Click **"Disable Mods"** in AMT. This restores original game configuration. Mod files remain on disk — click "Install" to re-enable.

---

#### ❓ How do I completely remove all mods?

1. Click **"Disable"** in AMT
2. Optionally verify game files via Steam: _Steam → Right-click Dota 2 → Properties → Local Files → Verify integrity_
3. Optionally delete the `_ArdysaMods` folder in your Dota 2 installation

---

#### ❓ How do I uninstall AMT itself?

1. Click **"Disable"** in AMT to restore vanilla Dota 2
2. Uninstall via _Windows Settings → Apps → ArdysaModsTools → Uninstall_
3. Optionally delete residual folders in `%AppData%\ArdysaModsTools`

---

### 🔧 Troubleshooting

#### ❓ AMT won't start / shows a WebView2 error

**Cause:** Missing WebView2 Runtime

**Fix:** Install the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/). It's usually pre-installed on Windows 10/11 but may be missing on some systems.

---

#### ❓ Do I need to install .NET?

**No.** AMT is self-contained — the .NET runtime ships inside the app. The only external
requirement is the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/),
which the installer adds automatically if it's missing.

---

#### ❓ AMT says "Cannot run while dota2.exe is active"

**Fix:**

1. Close Dota 2 completely
2. Open Task Manager (`Ctrl+Shift+Esc`)
3. Look for `dota2.exe` and end the process if found
4. Relaunch AMT

---

#### ❓ AMT is flagged by my antivirus

**This is a false positive.** AMT manipulates VPK files which triggers some antivirus heuristics.

**Fix:**

1. Add an exception for your AMT installation folder
2. Add an exception for `cdn.ardysamods.my.id` in your firewall
3. Download only from the [official GitHub releases](https://github.com/Anneardysa/ArdysaModsTools/releases)

---

#### ❓ Dota 2 is not detected / "Could not detect Dota 2"

**Fixes (try in order):**

1. Run AMT **as Administrator**
2. Click **"Manual Select"** and browse to: `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`
3. If Dota 2 is in a custom Steam library, browse to that location instead
4. Verify Dota 2 is installed by launching it from Steam

---

#### ❓ Installation fails with "Access Denied"

**Causes & Fixes:**

| Cause               | Fix                                    |
| ------------------- | -------------------------------------- |
| Dota 2 is running   | Close Dota 2 completely                |
| No write permission | Run AMT as **Administrator**           |
| Antivirus blocking  | Temporarily disable real-time scanning |
| Disk space full     | Free up at least 500 MB–2 GB of space  |

---

#### ❓ Mods installed but not visible in-game

**Fixes (try in order):**

1. Check status shows **"Ready" (green)** in AMT
2. Click **"Patch Update"**
3. **Completely exit** Dota 2 and relaunch (don't just reconnect)
4. Verify the folder exists: `dota 2 beta/game/_ArdysaMods/`
5. Click **"Disable"** and then **"Install"** again for a fresh install

---

#### ❓ Download fails or stalls

**Fixes:**

1. Check your internet connection
2. AMT uses multi-CDN fallback (`cdn.ardysamods.my.id` → `cdn2.ardysamods.my.id`) — it retries automatically
3. If CDN is blocked in your region, try changing DNS to `8.8.8.8` or `1.1.1.1`
4. Whitelist `cdn.ardysamods.my.id` in your firewall

---

#### ❓ "Signature Mismatch" after Dota 2 update

**This is normal!** Dota 2 updates change file signatures.

**Fix:** Click **"Patch Update"** → Wait for completion → Launch Dota 2

---

#### ❓ VPK recompilation fails

**Fixes:**

1. Ensure the `tools/` folder has all required files (HLExtract.exe, vpk.exe)
2. Delete the `_ArdysaMods/_temp/` folder and retry
3. Free up disk space (~2 GB needed for extraction)
4. Verify game files via Steam

---

#### ❓ "Connection to server failed" in Skin Selector

**Causes & Fixes:**

| Cause             | Fix                                                   |
| ----------------- | ----------------------------------------------------- |
| CDN blocked       | Update to latest AMT version (uses R2 CDN + fallback) |
| Firewall blocking | Whitelist `cdn.ardysamods.my.id`                      |
| DNS issues        | Change DNS to `8.8.8.8` or `1.1.1.1`                  |
| Rate limiting     | Wait 1 hour, then retry                               |

**Check console logs for specific errors:**

- `[NET] Timeout` → Slow connection, retry
- `[NET] Server returned 403` → Rate limited, wait
- `[NET] Connection failed` → Network issue, check firewall

---

### 💡 Tips & Best Practices

### Do's ✅

1. **Always close Dota 2** before using AMT
2. **Run as Administrator** for best compatibility
3. **Start the game with PLAY DOTA 2**, not from Steam — it repairs your mod package after a Dota 2 patch, which is what stops the crash-on-startup
4. **Click "Patch Update"** after every Dota 2 game update
4. **Star favorite heroes** for quick access in Skin Selector
5. **Copy console logs** when reporting bugs (use the Copy button)
6. **Keep AMT updated** — newer versions have better CDN support and bug fixes
7. **Download only from official sources** — [GitHub Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)

### Don'ts ❌

1. **Don't** run AMT while Dota 2 is open
2. **Don't** manually edit files in `_ArdysaMods/` folder
3. **Don't** download AMT from unofficial sources
4. **Don't** share modified game files with others
5. **Don't** run multiple instances of AMT

### Pro Tips 💎

- **Batch generate** — Select sets for multiple heroes before clicking Generate to save time
- **Use "Add to Current"** when adding a single misc mod (faster than Clean Generate)
- **Enable PatchWatcher** — Automatically detects Dota 2 updates so you never play with broken mods
- **Backup favorites** — Your favorites and settings are stored in `%AppData%\ArdysaModsTools`

---

### 📊 Quick Reference

### Status Indicators

| Color     | Status        | Meaning                    | Action Needed        |
| --------- | ------------- | -------------------------- | -------------------- |
| 🟢 Green  | Ready         | Mods installed and working | None — enjoy!        |
| 🟠 Orange | Need Update   | Patch required             | Click "Patch Update" |
| ⚫ Gray   | Not Installed | No mods installed          | Click "Install"      |
| 🔴 Red    | Error         | Problem detected           | Check console logs   |

### Console Log Patterns

| Pattern   | Meaning                      |
| --------- | ---------------------------- |
| `[VPK]`   | VPK extraction/recompilation |
| `[NET]`   | Network operations           |
| `[PATCH]` | Signature patching           |
| `[GEN]`   | Hero/misc generation         |

---

### General

**Q: Does this work with Mac/Linux?**  
A: No, AMT 2.0 is Windows-only. It requires Windows 10/11 (64-bit) and .NET 8.

**Q: Can I use this on Dota 2 Reborn?**  
A: Yes, AMT 2.0 is designed for the current version of Dota 2 (Source 2/Reborn).

**Q: Is this free?**  
A: Yes, ArdysaModsTools is completely free. Donations are appreciated to support development.

---

### Technical

**Q: Where are mods installed?**  
A: Mods are installed in: `dota 2 beta/game/_ArdysaMods/pak01_dir.vpk`

**Q: Where are logs saved?**  
A: The main log is `ardysa_fallback.log` — in `%LocalAppData%\ArdysaModsTools\` for installer builds, or next to `ArdysaModsTools.exe` for portable builds. Generation reports are in `[Dota 2 Path]/game/_ArdysaMods/_temp/`.

**Q: Where are settings saved?**  
A: Settings are in: `%AppData%\ArdysaModsTools` (favorites, user preferences, and configuration files)

**Q: Can I backup my mods?**  
A: Yes, backup the `_ArdysaMods` folder and your AMT settings folder.

**Q: How do I completely uninstall?**  
A:

1. Click **Disable** in AMT to restore original files
2. Uninstall via Windows Settings → Apps
3. Optionally delete the `_ArdysaMods` folder in your Dota 2 installation

---

### Features

**Q: Can I use multiple hero sets at once?**  
A: Yes! That's the main benefit. Select different sets for different heroes and generate them all.

**Q: Can I mix hero sets with misc mods?**  
A: Yes, they work independently. Hero sets modify `pak01_dir.vpk`, misc mods also use the same VPK but different content.

**Q: How do I reset to vanilla Dota 2?**  
A: Click **Disable** button to restore original configuration. Or right-click Dota 2 in Steam → Properties → Verify Integrity of Game Files.

**Q: Can I use custom sets not in the list?**  
A: Not directly through AMT. The app uses curated sets from the CDN. Manual modding requires technical knowledge.

**Q: What happens if I select multiple sets for the same hero?**  
A: The last selected set for each hero is used. Each hero can only have one active set at a time.

---

## Support & Community

### Get Help

If you encounter issues or have questions:

1. **Check this guide** - Most questions are answered here
2. **Check console logs** - Often show what went wrong
3. **Join Discord** - Community support and discussions
4. **Watch tutorials** - YouTube channel has video guides

### Links

- 💬 **Discord Server**: [discord.gg/5xKg4fyumv](https://discord.gg/5xKg4fyumv)
- 📺 **YouTube Channel**: [youtube.com/@ardysa](https://youtube.com/@ardysa)
- ☕ **Support Development**: [ko-fi.com/ardysa](https://ko-fi.com/ardysa)

### Reporting Bugs

When reporting bugs, include:

1. **AMT version** - Shown in title bar
2. **Windows version** - Windows 10/11
3. **Dota 2 version** - From Dota 2 main menu
4. **Console logs** - Copy from console
5. **Steps to reproduce** - What you did before the error
6. **Screenshots** - If relevant

### Feature Requests

Have an idea? Open a
[feature request](https://github.com/Anneardysa/ArdysaModsTools/issues/new?template=3-feature.yml).
Want a specific hero set, courier, ward, HUD or terrain added? Use the
[mod / set request](https://github.com/Anneardysa/ArdysaModsTools/issues/new?template=2-mod-request.yml)
template instead. For "how do I…" questions, Discord is faster.

---

## Credits & License

### Development

**ArdysaModsTools** is developed and maintained by **Ardysa**.

### License

This software is licensed under the **GNU General Public License v3.0**.  
See LICENSE for full details.

### Third-Party Tools

AMT uses:

- **HLExtract** - VPK extraction (HLLib)
- **vpk.exe** - VPK compilation (Valve)
- Various .NET libraries (see LICENSE.txt)

---

## Disclaimer

> [!CAUTION] > **Important Disclaimer**
>
> This tool modifies Dota 2 game files. While it only changes cosmetic elements:
>
> - Use at your own risk
> - The developers are not responsible for any issues
> - This includes game bans, corrupted files, or data loss
> - Always backup your files before modding
> - Valve's policy on mods may change at any time

**By using ArdysaModsTools, you acknowledge and accept these risks.**

---

## Quick Reference Card

### Essential Shortcuts

| Action                | Steps                                              |
| --------------------- | -------------------------------------------------- |
| **First Time Setup**  | Auto Detect → Install ModsPack → **Play Dota 2**   |
| **Play**              | **PLAY DOTA 2** (repairs your package, then launches) |
| **Install Mods**      | Install → Auto Install → wait                      |
| **Disable Mods**      | Disable → Confirm                                  |
| **After Game Update** | Patch Update, then **Play Dota 2**                 |
| **Create Hero Skin**  | Skin Selector → pick hero → choose set → Generate  |
| **Add Misc Mods**     | Miscellaneous → select options → Generate          |
| **Verify Mods**       | Right-click Patch Update → Verify                  |
| **Repair package only** | Package Sync chip → Fix                          |
| **Get Logs**          | Copy Console button                                |

### Status Indicators

| Color     | Status        | Meaning                    |
| --------- | ------------- | -------------------------- |
| 🟢 Green  | Ready         | Mods installed and working |
| 🟠 Orange | Need Update   | Patch required             |
| ⚫ Gray   | Not Installed | No mods installed          |
| 🔴 Red    | Error         | Problem detected           |

---

**Thank you for using ArdysaModsTools! Enjoy your customized Dota 2 experience! 🎮**

_This guide tracks the latest release. Check **What's New** in the app for what changed in the build you're running._
