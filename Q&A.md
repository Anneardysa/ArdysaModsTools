# Ardysa Mods Tools — Q&A (Error-Indexed Troubleshooting)

Search this page for the **exact message you saw** (Ctrl+F). Each entry explains what it
means, why it happens, and how to fix it.

- Need a **how-to** ("how do I install a skin?", "is this VAC-safe?") → see [docs/user/FAQ.md](docs/user/FAQ.md).
- Building from source / dev environment issues → see [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

> **Before anything else:** close Dota 2 completely (check Task Manager for `dota2.exe`), and
> grab the log. The on-screen message is deliberately short; the log carries the real cause as a
> bracketed error code (e.g. `[VPK_003]`, `[DL_006]`) plus the raw tool output.
>
> **Log location:** `…/dota 2 beta/game/dota/_ArdysaMods/_temp/logs/` — or use the **Copy** button
> on the main window console.

---

## Table of Contents

1. [Installing base & mod pack](#1-installing-base--mod-pack)
2. [Generating skins & misc mods](#2-generating-skins--misc-mods)
3. [Applying mods to the game (patching)](#3-applying-mods-to-the-game-patching)
4. [Startup & detection](#4-startup--detection)
5. [Network & downloads](#5-network--downloads)
6. [Manual VPK install](#6-manual-vpk-install)
7. [Error-code reference](#7-error-code-reference)

---

## 1. Installing base & mod pack

### "Failed to get base files: Failed to extract pak01_dir.vpk using HLExtract"

**What it means:** Before building a mod, AMT unpacks a base game archive (`Original.zip` →
`pak01_dir.vpk`) using the bundled `HLExtract.exe`. This message means that extraction didn't
finish — so generation can't continue. Log code: **`VPK_001` (VPK_EXTRACT_FAILED)**.

**Causes & fixes, most common first:**

1. **Corrupted / half-downloaded base file** — the usual cause.
   AMT auto-clears and re-downloads only when the file is *missing*; a **corrupt-but-present**
   file won't self-heal. → Delete the cached `Original.zip` and its extracted base folder, then
   retry so it re-downloads clean.
2. **Missing Visual C++ 2010 runtime** (builds before 2232) — `HLExtract.exe` and `HLLib.dll`
   depend on `msvcr100.dll` / `msvcp100.dll` from the VC++ 2010 Redistributable. On fresh Windows
   10/11 installs these aren't present, so HLExtract crashes immediately with a missing-DLL error.
   → **Fixed in build 2232**: the CRT DLLs are now bundled with the app. On older builds, install
   the [VC++ 2010 x64 Redistributable](https://www.microsoft.com/en-us/download/details.aspx?id=26999).
3. **Antivirus killed `HLExtract.exe` mid-run** — it writes thousands of files and trips
   heuristics. → Add the AMT install folder to your AV exclusions, then retry.
4. **Extraction timed out** — hard limit is **10 minutes**. A slow/USB/network drive or one that's
   nearly full can exceed it. → Free space, extract on a local SSD/HDD, close other heavy disk
   activity.
5. **Out of disk space / no write permission** — extraction writes the full unpacked tree (several
   GB). → Free space; if AMT is under `Program Files`, run once as Administrator or reinstall to a
   user-writable path.
6. **`HLExtract.exe` missing** (often AV quarantine) → reinstall AMT to restore it.

---

### "Base file appears corrupted, clearing cache…" then it fails again

The self-heal ran (deleted `Original.zip` + extract dir) but the re-download is also failing.
This is almost always **network** — see [§5](#5-network--downloads). If the re-download succeeds
but extraction still fails, it's one of the extraction causes above (disk/AV).

---

### "The VPK file format is invalid." / `VPK_005`

The archive was downloaded but isn't a readable VPK (truncated, or the wrong file). Same fix as a
corrupt base: clear the cache and re-download. If it recurs on a good connection, the CDN copy may
be stale — report it (see [Getting help](#getting-help)).

---

## 2. Generating skins & misc mods

### "Required asset files are missing." / `GEN_INDEX_NOT_FOUND`

The hero/set's asset index couldn't be resolved. The Skin Selector reads each set's index from the
cloud (R2), falling back to the index bundled in the set zip. This means neither was available.

- **Fix:** check your connection (§5), then retry generation. If it only happens for one specific
  set, that set's cloud index may not be synced yet — try a different set to confirm, and report
  the set name.

### "Hero data not found." / `GEN_HERO_NOT_FOUND`

The hero catalog didn't load. Usually a transient network/cache issue — retry. If it persists,
your `heroes.json` cache may be stale; reopening the Skin Selector re-fetches it.

### "Failed to merge hero assets." / `GEN_MERGE_FAILED`

Generation downloaded the set but couldn't overlay it onto the base. Retry once (transient file
locks resolve). If it repeats, do a **Clean Generate** rather than "Add to Current" — a stale
per-run base copy is the common cause.

### Skin Selector: "integrity check failed" / `DL_006` during generation

A downloaded asset didn't match its expected hash. This is a **safety gate**, not a bug on your
end — it usually means the server's asset manifest drifted behind rebuilt assets.

- **Fix:** retry (AMT re-downloads from the next CDN automatically). If every hero fails with this
  right after a release, it's a server-side manifest sync issue — please report it; a local retry
  won't fix a drifted manifest.

---

## 3. Applying mods to the game (patching)

### "Signature Mismatch" / mods stopped working after a Dota 2 update

**This is normal.** Dota 2 updates overwrite the modded files and change signatures. Status turns
🟠 **Need Update**.

- **Fix:** click **Patch Update** → wait → relaunch Dota 2. Enable **PatchWatcher** to get notified
  automatically next time.

### "Required game files not found." / `PATCH_BLOCK_NOT_FOUND`

The patcher couldn't find the block it needs in the game files — typically because Dota 2 files are
in an unexpected state (mid-update, or partially verified).

- **Fix:** let Dota 2 finish updating, launch it once to settle files, close it, then Patch Update.
  If it persists, Verify integrity of game files in Steam, then reinstall the mod pack.

### "Failed to write patch files. Check file permissions." / `PATCH_WRITE_FAILED`

AMT can't write into the game folder.

- **Fix:** close Dota 2 (it locks VPKs), run AMT as Administrator, and pause real-time AV scanning
  during the patch.

### Mods installed, status green, but nothing shows in-game

1. Fully **exit** Dota 2 and relaunch (don't just reconnect to a match).
2. Confirm `…/dota 2 beta/game/dota/_ArdysaMods/` exists.
3. Click **Disable** then **Install** for a clean re-apply.

---

## 4. Startup & detection

### AMT won't start / "WebView2 runtime not found"

The app shell is WebView2. Windows 10/11 usually ships it, but it can be missing.

- **Fix:** install the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/).
  The AMT installer also offers to install it. If AMT opens but dialogs render blank, the WebView2
  profile may be corrupted — recent builds self-heal it on next launch; if not, close AMT and
  delete its WebView2 user-data folder under `%LocalAppData%`.

### AMT says ".NET 8 not found"

- **Fix:** install the **.NET 8 Desktop Runtime (x64)** from
  [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0), then rerun the installer.

### "Cannot run while dota2.exe is active"

- **Fix:** close Dota 2, end any lingering `dota2.exe` in Task Manager, relaunch AMT.

### "Dota 2 Not Detected" / "Could not detect Dota 2"

1. Run AMT **as Administrator**.
2. **Manual Select** → browse to `…\steamapps\common\dota 2 beta` (in the correct Steam library if
   you use a custom one).
3. Launch Dota 2 once from Steam to confirm it's actually installed.

### AMT is flagged by antivirus

False positive — editing VPKs trips heuristics. Add an exclusion for the AMT folder and only
download from the [official releases](https://github.com/Anneardysa/ArdysaModsTools/releases).

---

## 5. Network & downloads

AMT downloads through a multi-CDN fallback chain: **Cloudflare R2 → jsDelivr → GitHub Raw**, with
automatic retries and resume.

| Message / log code | Meaning | Fix |
| --- | --- | --- |
| "Network error. Please check your internet connection." / `DL_001` | No/failed connection | Check internet; retry |
| "Download timed out. Please try again." / `DL_002` | Slow or stalled | Retry; switch DNS to `1.1.1.1` / `8.8.8.8` |
| "Server error. Please try again later." / `DL_SERVER_ERROR` | CDN returned 5xx / 403 | Wait (possible rate-limit) and retry |
| "Downloaded file verification failed." / `DL_006` | Hash mismatch | Retry (next CDN); if persistent → server manifest issue |
| `[NET] Server returned 403` | Rate limited | Wait ~1 hour |

**If a CDN is blocked in your region:** change DNS to `1.1.1.1` or `8.8.8.8`, and whitelist
`cdn.ardysamods.my.id` in your firewall. Always update to the latest AMT — CDN routing improves
over versions.

---

## 6. Manual VPK install

When you install a `pak01_dir.vpk` by hand (drag-drop), AMT classifies its origin:

- **Official** — recognized Ardysa pack (identified by a hidden marker path inside the VPK). Installs
  normally, with a full rebuild.
- **Unofficial** — no marker. AMT shows a yellow disclaimer and, on your consent, installs the pack
  **as-is** (no official index overlay). It does not reject it.

Copying an old `version/_ArdysaMods` marker into a foreign pack does **not** make it Official — that
path is a decoy and grants nothing. If a genuinely-official pack is misclassified, it's likely an
older pack from before the marker; rebuild/re-download it from the current release.

---

## 7. Error-code reference

Codes appear in the log as `[CODE]`. Group prefix = subsystem.

| Code | Name | Subsystem |
| --- | --- | --- |
| `VPK_001` | Extract failed (HLExtract error) | VPK |
| `VPK_002` | Recompile failed (vpk.exe error) | VPK |
| `VPK_003` | Replace failed (copy to game dir) | VPK |
| `VPK_004` | File not found | VPK |
| `VPK_005` | Invalid/corrupt format | VPK |
| `VPK_006` | Tool missing (HLExtract/vpk.exe) | VPK |
| `DL_001` | Network error | Download |
| `DL_002` | Timeout | Download |
| `DL_005` | Server error (5xx/403) | Download |
| `DL_006` | Hash mismatch (integrity) | Download |
| `DL_007` | Downloaded archive unpack failed | Download |
| `PATCH_002` | Signature patch failed | Patch |
| `PATCH_003` | Block not found (game files) | Patch |
| `PATCH_007` | Write/permission failure | Patch |
| `GEN_002` | Hero data missing | Generation |
| `GEN_005` | Asset merge failed | Generation |
| `GEN_007` | Asset index missing | Generation |
| `CFG_001` | Dota 2 not detected | Config |
| `CFG_004` / `CFG_005` | Config read / write failed | Config |

Log prefixes: `[VPK]` extraction/recompile · `[NET]` network · `[PATCH]` signatures · `[GEN]`
generation.

---

## Getting help

Include the **log** (Copy button) and the **exact error code** when reporting — it's the difference
between a one-line answer and a guessing game.

- 🐛 [GitHub Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
- 💬 [Discord](https://discord.gg/ffXw265Z7e)

> Not affiliated with Valve. Modifying game files may violate Valve's ToS — use at your own risk.
