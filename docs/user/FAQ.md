# ArdysaModsTools — Frequently Asked Questions (FAQ)

Complete guide covering usage, troubleshooting, and ban safety

---

## Table of Contents

- [🛡️ Ban & Safety Questions](#️-ban--safety-questions)
- [🎮 What To Do — Usage Guide](#-what-to-do--usage-guide)
- [🔧 How To Fix — Troubleshooting](#-how-to-fix--troubleshooting)
- [💡 Tips & Best Practices](#-tips--best-practices)

---

## 🛡️ Ban & Safety Questions

### ❓ Will I get VAC banned for using AMT?

> [!IMPORTANT]
> **Short answer: No known bans from cosmetic mods, but use at your own risk.**

Here's what AMT does and does **NOT** do:

| AMT Does ✅                                | AMT Does NOT ❌                              |
| ------------------------------------------ | -------------------------------------------- |
| Modify local cosmetic VPK files            | Inject into game processes or memory         |
| Patch `gameinfo.gi` for mod loading        | Interact with VAC or anti-cheat systems      |
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

### ❓ Is this a cheat or a hack?

**No.** AMT is a cosmetic mod manager. It:

- Does **not** give any gameplay advantage
- Does **not** modify game logic, abilities, or mechanics
- Does **not** show hidden information (enemy positions, cooldowns, etc.)
- Only changes what **you** see on your screen (hero skins, weather, terrain, HUD)

---

### ❓ Can other players see my mods?

**No.** All modifications are **client-side only**. Other players see the default game assets. Your mods only affect your local game rendering.

---

### ❓ Can Valve detect that I'm using mods?

AMT modifies files in the game directory, which is something Dota 2 naturally reads on launch. However:

- AMT does **not** run while Dota 2 is active
- No hooks, injections, or runtime modifications occur
- The modifications are identical in nature to how custom games and workshop content work

---

### ❓ What if Valve bans cosmetic mods in the future?

If Valve changes their policy:

1. Click **"Disable Mods"** in AMT to instantly restore vanilla Dota 2
2. Or verify game files via Steam: _Right-click Dota 2 → Properties → Local Files → Verify integrity of game files_
3. All changes are fully reversible with zero trace

---

### ❓ Is AMT open-source? Can I trust it?

Yes. AMT is fully open-source under the **GNU General Public License v3.0**. The complete source code is available on [GitHub](https://github.com/Anneardysa/ArdysaModsTools). You can audit every line of code.

---

## 🎮 What To Do — Usage Guide

### First-Time Setup

#### ❓ How do I install AMT?

1. Download `ArdysaModsTools_Setup_x64.exe` from [GitHub Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
2. **Close Dota 2** completely
3. Run the installer as **Administrator**
4. Follow the setup wizard
5. Launch AMT from Desktop or Start Menu

#### ❓ How do I detect my Dota 2 installation?

- **Auto Detect** (recommended): Click the "Auto Detect" button — AMT searches common Steam library paths
- **Manual Select**: If auto-detection fails, click "Manual Select" and browse to your `dota 2 beta` folder
   - Default path: `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`

#### ❓ How do I install the mod pack?

1. Click **"Install"** button
2. Choose **"Auto Install"** (downloads latest mod pack from CDN)
3. Wait for download, validation, and installation
4. Status shows **"Ready" (green)** when complete
5. Launch Dota 2 and enjoy!

---

### After Dota 2 Updates

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

### Hero Skin Selection

#### ❓ How do I create custom hero skins?

1. Click **"Skin Selector"** from the main window
2. Browse or search for heroes
3. Click a hero card and choose a cosmetic set from the dropdown
4. Repeat for as many heroes as you want
5. Click **"Generate"**
6. Wait 2–5 minutes per hero

#### ❓ Can I select multiple heroes at once?

**Yes!** Select sets for as many heroes as you want before clicking "Generate". The app processes them sequentially.

#### ❓ How do I favorite heroes?

Click the **star icon** on any hero card. Favorites appear at the top of the list for quick access.

---

### Miscellaneous Mods

#### ❓ What misc mods are available?

| Category           | Examples                                |
| ------------------ | --------------------------------------- |
| **Weather**        | Moonbeam, Aurora, Snow, Ash, Pestilence |
| **Terrain**        | TI terrains, seasonal maps              |
| **HUD**            | Custom interface themes and overlays    |
| **Battle Effects** | TI-themed ability/kill effects          |
| **Music**          | Custom music packs                      |
| **Cursor**         | Custom cursor skins                     |

#### ❓ What's the difference between "Clean Generate" and "Add to Current"?

| Mode               | Use When                     | Effect                               |
| ------------------ | ---------------------------- | ------------------------------------ |
| **Clean Generate** | First time or resetting all  | Fresh extraction, only selected mods |
| **Add to Current** | Adding more mods to existing | Merges with current mods (faster)    |

---

### Disabling & Uninstalling

#### ❓ How do I temporarily disable mods?

Click **"Disable Mods"** in AMT. This restores original game configuration. Mod files remain on disk — click "Install" to re-enable.

#### ❓ How do I completely remove all mods?

1. Click **"Disable"** in AMT
2. Optionally verify game files: _Steam → Right-click Dota 2 → Properties → Local Files → Verify integrity_
3. Optionally delete the `_ArdysaMods` folder in your Dota 2 installation

#### ❓ How do I uninstall AMT itself?

1. Click **"Disable"** in AMT to restore vanilla Dota 2
2. Uninstall via _Windows Settings → Apps → ArdysaModsTools → Uninstall_
3. Optionally delete residual folders in `%AppData%\ArdysaModsTools`

---

## 🔧 How To Fix — Troubleshooting

### Application Issues

#### ❓ AMT won't start / shows a WebView2 error

**Cause:** Missing WebView2 Runtime

**Fix:** Install the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/). It's usually pre-installed on Windows 10/11 but may be missing on some systems.

---

#### ❓ AMT says ".NET 8 not found"

**Fix:**

1. Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Install the **.NET 8 Desktop Runtime (x64)**
3. Run the AMT installer again

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

### Detection Issues

#### ❓ Dota 2 is not detected / "Could not detect Dota 2"

**Fixes (try in order):**

1. Run AMT **as Administrator**
2. Click **"Manual Select"** and browse to: `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`
3. If Dota 2 is in a custom Steam library, browse to that location instead
4. Verify Dota 2 is installed by launching it from Steam

---

### Installation & Mod Issues

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
4. Verify the folder exists: `dota 2 beta/game/dota/_ArdysaMods/`
5. Click **"Disable"** and then **"Install"** again for a fresh install

---

#### ❓ Download fails or stalls

**Fixes:**

1. Check your internet connection
2. AMT uses multi-CDN fallback (Cloudflare R2 → jsDelivr → GitHub Raw) — it will retry automatically
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

### Network Issues

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

## 💡 Tips & Best Practices

### Do's ✅

1. **Always close Dota 2** before using AMT
2. **Run as Administrator** for best compatibility
3. **Click "Patch Update"** after every Dota 2 game update
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

## 📊 Quick Reference

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

## 🆘 Still Need Help?

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
- 💬 **Discord**: [discord.gg/ffXw265Z7e](https://discord.gg/ffXw265Z7e)
- 📺 **Tutorials**: [youtube.com/@ardysa](https://youtube.com/@ardysa)
- ☕ **Support Development**: [ko-fi.com/ardysa](https://ko-fi.com/ardysa)
- 📖 **Full User Guide**: [USER_GUIDE.md](USER_GUIDE.md)

---

> [!CAUTION]
> **Disclaimer**: ArdysaModsTools is **NOT** affiliated with Valve Corporation or Steam. Modifying game files may violate Valve's Terms of Service. **Use at your own risk.** The developers are not responsible for any consequences including account restrictions or bans.

---

<div align="center">

**© 2025-2026 Ardysa. All rights reserved.**

Made with ❤️ for the Dota 2 community

</div>
