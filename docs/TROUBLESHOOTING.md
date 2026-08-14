# Troubleshooting Guide

Common issues and solutions for developers and users.

---

## 🔌 Connection Issues

### "CONNECTION TO SERVER FAILED" in Skin Selector

**Symptoms:**

- Cannot open Skin Selector
- Console shows `[NET] Timeout connecting to...`

**Causes:**

1. **CDN blocked or unreachable** from your network
2. **Firewall/antivirus blocking** the application
3. **DNS issues** with your ISP
4. **Rate limiting** from too many requests

**Solutions:**

1. Update to the latest version.
2. Check the console log for the specific error:
   - `[NET] Timeout` → slow connection, try again
   - `[NET] Server returned 403` → rate limited, wait an hour
   - `[NET] Connection failed` → network issue, check the firewall
3. Change DNS to `8.8.8.8` or `1.1.1.1`.
4. Whitelist **both** CDN hosts in your firewall:
   - `cdn.ardysamods.my.id` (primary)
   - `cdn2.ardysamods.my.id` (fallback)

> [!NOTE]
> AMT contacts **only** those two hosts for assets. It does not fall back to jsDelivr, GitHub
> Raw, or any GFW proxy — those were removed from the chain. Whitelisting them does nothing.

---

## 🏗️ Build Issues

### Missing .NET 8 SDK

**Error:** `The SDK 'Microsoft.NET.Sdk' specified could not be found`

**Solution:**

```bash
# Download and install from
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### WebView2 Runtime Not Found

**Error:** `WebView2 runtime not found`

**Solution:**

```bash
# Install from Microsoft
# https://developer.microsoft.com/microsoft-edge/webview2/
```

### tools/ Directory Missing

**Error:** `HLExtract.exe not found`

**Solution:**
Ensure `.csproj` copies tools:

```xml
<Content Include="tools\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

---

## 🧪 Test Issues

### Tests Fail with DI Errors

**Error:** `Unable to resolve service for type 'IConfigService'`

**Cause:** Missing mock dependency in test constructor injection

**Solution:**

All services use **constructor injection** — create mocks for each dependency:

```csharp
[SetUp]
public void Setup()
{
    _mockConfigService = new Mock<IConfigService>();
    _mockLogger = new Mock<IAppLogger>();
    _mockDetectionService = new Mock<IDetectionService>();

    _sut = new MyService(
        _mockConfigService.Object,
        _mockLogger.Object,
        _mockDetectionService.Object
    );
}
```

> [!NOTE]
> `ServiceLocator` was completely removed in Build 2082. All tests now use direct constructor injection with Moq.

### Tests Pass Locally but Fail in CI

**Common causes:**

1. **Path differences** - Use `Path.Combine()` not hardcoded paths
2. **Missing test data** - Ensure embedded resources are included
3. **Timing issues** - Add proper async waits
4. **STA apartment** - WinForms presenters need `[Apartment(ApartmentState.STA)]`

---

## 🎮 Runtime Issues

### "Dota 2 Not Detected"

**Causes:**

1. Steam not installed to default path
2. Dota 2 installed in non-standard location
3. Registry entries missing

**Solutions:**

1. Click "Manual Detect" and browse to `dota 2 beta` folder
2. Run AMT as Administrator
3. Reinstall Steam (recreates registry entries)

### Mods Not Showing in Game

**Causes:**

1. Dota 2 was updated (signatures changed)
2. `gameinfo_branchspecific.gi` not patched
3. The mod package is out of sync with the game's item data
4. VPK file corrupted

**Solutions:**

1. Click **Patch Update** in AMT — this rewrites `gameinfo_branchspecific.gi` and
   `dota.signatures`.
2. Press **PLAY DOTA 2**. Patch Update does *not* rebuild the package; if the **Package Sync**
   chip is red, only Play (or its **Fix** action) repairs it. See
   [Dota 2 crashes on launch](#dota-2-crashes-on-startup-after-a-game-update) below.
3. Check the console for errors.
4. Reinstall with **Install ModsPack**.

### Dota 2 crashes on startup after a game update

**Cause:** the installed mod package carries its own copy of Dota 2's item definitions and
shadows the game's. When a patch changes those definitions, the game starts on data that no
longer matches its own content and dies.

**Solution:** press **PLAY DOTA 2** in AMT. It waits for any pending Steam update to finish,
rebuilds the package against whatever the update actually delivered, and then launches. You do
**not** need to redownload the ModsPack — the data needed to repair it is already on disk.

> [!TIP]
> Launching Dota 2 directly from Steam skips this repair entirely. After a Dota 2 patch, start
> the game from AMT at least once.

### "Signature Mismatch" After Dota Update

**Normal behavior!** Dota 2 updates change file signatures.

**Solution:**

```
Click "Patch Update" → Wait for completion → Launch Dota 2
```

---

## 🔒 Antivirus & Integrity

### Antivirus flags AMT or removes part of the install

**This is a false positive.** AMT bundles Valve's `vpk.exe` and HLLib's `HLExtract.exe` to repack
game archives, and some scanners flag any tool that writes into game files.

**Solutions:**

1. Add an exception for your AMT installation folder.
2. Download only from [official releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
   or [ardysamods.my.id](https://ardysamods.my.id). Releases are Authenticode-signed by the
   SignPath Foundation — check the publisher on the UAC prompt.
3. If your AV already stripped a file, reinstall rather than patching around it.

> [!NOTE]
> AMT has **no anti-debug or runtime anti-tamper layer** — it was removed in 2026-07 precisely
> because it caused hacktool false positives and stopped nobody. If AMT refuses to start, it is
> not a "security check": read `ardysa_fallback.log` or `startup_log.txt` for the real reason.

### `DL_009` — this build is too old

The asset format has moved on and your version can't read it. **Update AMT.** This is deliberate,
not a bug: an old client reading new assets is how installs get corrupted.

### `DL_006` — integrity check failed on a download

A downloaded file's SHA-256 didn't match the manifest, so it was rejected before it could touch
your game folder. Usually transient — retry. If it persists, it's a server-side manifest issue;
report it with the log.

---

## 🔄 Update & Auto-Update Issues

### "Incremental update failed"

**Symptoms:**

- An error banner or notification appears in the update dialog or main window:
  - `"Update failed. Use a download link below. (<specific error reason>)"`
  - In-shell toast notification: `Update Available - Update to vX.Y.Z failed: <reason>`
- AMT restarts on the previous version without applying the new update.
- Log contains: `Incremental update failed: <reason>` or `The last update could not be applied and the app was restarted on the previous version — <reason>`.

**How Incremental (Delta) Update Works:**

1. AMT compares local files against the release's `files.json` manifest published on the CDN.
2. Downloads changed/missing files into staging (`%LocalAppData%\ArdysaModsTools\update\<version>\`).
3. Verifies SHA-256 integrity of all staged files and writes `.staged-ok` marker.
4. Launches `AMT.Updater.exe` (`tools/updater/AMT.Updater.exe`) and shuts down AMT.
5. `AMT.Updater.exe` re-verifies staged files, performs atomic file swap (`.amtbak` / `.amtnew`), and restarts AMT.

**Causes:**

1. **File Locks / Process not exiting:** Background processes (e.g., WebView2/Chromium renderer instances, search indexer, or antivirus scanner) holding locks on application binaries when `AMT.Updater.exe` attempts file swaps.
2. **Permission / UAC Issues:** AMT is installed in a protected location (e.g., `C:\Program Files\`) and administrator permissions/UAC elevation prompt was declined or timed out.
3. **Antivirus Interference:** Antivirus or Windows Defender blocked `AMT.Updater.exe` or flagged the staging directory file swap as suspicious.
4. **Staging / Hash Mismatch (`DL_006`):** Network interruption, timeout, or CDN cache inconsistency causing file download or SHA-256 verification failure.
5. **Missing `.staged-ok` marker:** Download/staging process was interrupted before all files could be verified.
6. **Post-Apply Version Stagnation:** The updater swapped files but the running binary version remained unchanged (e.g. locked `.exe` reverted).

> [!NOTE]
> **Automatic Loop Protection:**
> AMT employs multi-layered loop guards across memory and persistent state (`%LocalAppData%\ArdysaModsTools\update\update_state.json`):
> 1. **Staging & Launch Protection:** If file download, hash verification, or updater execution fails/is declined, the version is immediately recorded as failed and auto-update is suppressed on subsequent startups.
> 2. **Applier Rollback & Outcome Tracking:** If `AMT.Updater.exe` fails or rolls back, it logs `FAILED: <reason>` into `%LocalAppData%\ArdysaModsTools\update\<version>\update.log`. AMT imports this on startup and suppresses delta auto-update for that version.
> 3. **Stale Post-Apply Version Guard:** If `AMT.Updater.exe` runs but the application version does not advance after restart, AMT detects this mismatch and halts further automatic re-apply attempts.
> When suppressed, the update dialog falls back to showing direct manual download links (Installer and Portable) with a red error notice showing the exact failure reason.

---

### Step-by-Step Fixes:

#### Fix 1: Manual Direct Download / Installer (Recommended & Fastest)
If an automatic incremental update fails, you can update manually without losing your mods, presets, or settings:
1. Open the Update Dialog or visit [ardysamods.my.id](https://ardysamods.my.id) or [GitHub Releases](https://github.com/Anneardysa/ArdysaModsTools/releases).
2. Click **CDN Server** or **Download from Website** to download the latest installer (`.exe`) or portable package (`.zip`).
3. Run the installer or extract the `.zip` directly over your existing ArdysaModsTools folder.
4. Launch AMT. Your configuration and active mods are preserved.

#### Fix 2: Reset Update State & Clear Staging Cache (To Retry Auto-Update)
If you want to re-trigger the incremental auto-update clean:
1. Close ArdysaModsTools completely.
2. Press <kbd>Win</kbd> + <kbd>R</kbd>, paste the following path, and press <kbd>Enter</kbd>:
   ```text
   %LocalAppData%\ArdysaModsTools\update
   ```
3. Delete the folder contents (or delete `update_state.json` to clear failure suppression).
4. Launch ArdysaModsTools.
5. Go to **Settings** ➔ click **Check for Updates** to retry the update with fresh files.

#### Fix 3: Resolve File Locks & Background Processes
If the log mentions `Could not install ... (locked by another process)`:
1. Press <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Esc</kbd> to open **Task Manager**.
2. Under the **Processes** and **Details** tabs, look for any leftover instances of:
   - `ArdysaModsTools.exe`
   - `AMT.Updater.exe`
   - `msedgewebview2.exe`
3. Right-click and select **End Task** on any matching processes.
4. Right-click `ArdysaModsTools.exe` and select **Run as Administrator**.
5. Retry the update.

#### Fix 4: Accept UAC Elevation Prompt & Configure Antivirus Exclusions
If the update fails with `UAC prompt declined or updater launch failed`:
1. When prompted by Windows User Account Control (*"Do you want to allow this app to make changes to your device?"* for `AMT.Updater.exe`), click **Yes**.
2. If Windows Defender or your antivirus blocks the updater:
   - Open **Windows Security** ➔ **Virus & threat protection**.
   - Under **Virus & threat protection settings**, click **Manage settings**.
   - Under **Exclusions**, click **Add or remove exclusions**.
   - Add the following exclusions:
     - **Folder:** `%LocalAppData%\ArdysaModsTools`
     - **Folder:** Your AMT installation directory (e.g. `C:\Program Files\ArdysaModsTools` or custom path).
3. Retry the update.

#### Fix 5: Inspect Update Diagnostic Logs
To see the exact technical failure reason:
- **App-level log:** Open `%LocalAppData%\ArdysaModsTools\ardysa_fallback.log`.
- **Updater engine log:** Open `%LocalAppData%\ArdysaModsTools\update\<version>\update.log` (or `update.log.previous`).
- **Update state tracker:** Open `%LocalAppData%\ArdysaModsTools\update\update_state.json` to inspect `FailedVersions` and `FailureReasons`.

---

## 📁 File Issues

### "Access Denied" Errors

**Causes:**

1. Dota 2 is running (locks VPK files)
2. Antivirus scanning files
3. No write permission to game folder

**Solutions:**

1. Close Dota 2 completely
2. Temporarily disable real-time antivirus scan
3. Run AMT as Administrator

### VPK Recompilation Fails

**Error:** `vpk.exe returned non-zero exit code`

**Causes:**

1. Missing vpk.exe dependencies
2. Corrupted extraction directory
3. Disk space full

**Solutions:**

1. Check `tools/vpk/` and `tools/hllib/` have all their DLLs
2. Delete `<Dota 2>/game/_ArdysaMods/_temp/` and retry
3. Free up disk space (need ~2 GB for extraction)

---

## 💡 Debugging Tips

### Enable Verbose Logging

Check console in main window for detailed logs. Copy with the "Copy" button.

### Log File Locations

| File                      | Where                                                                       |
| ------------------------- | --------------------------------------------------------------------------- |
| `ardysa_fallback.log`     | Installer builds: `%LocalAppData%\ArdysaModsTools\` · portable: next to the exe. **This is the one to attach to a bug report.** |
| `update.log`              | `%LocalAppData%\ArdysaModsTools\update\<version>\update.log` (or `.previous`). Written by `AMT.Updater.exe` when an update fails or rolls back. |
| `startup_log.txt`         | Next to `ArdysaModsTools.exe`. For when AMT won't start at all — overwritten every launch, so grab it right after a failed start. |
| `generation_report_*.txt` | `<Dota 2 folder>\game\_ArdysaMods\_temp\`. Skin Selector / Miscellaneous bugs. Already sanitized — safe to post as-is. |

> A screenshot of the red failure card is not enough — that card is deliberately stripped of
> file paths and internal identifiers before it's shown. The log can say what actually broke.

### Common Log Patterns

| Pattern   | Meaning                      |
| --------- | ---------------------------- |
| `[VPK]`   | VPK extraction/recompilation |
| `[NET]`   | Network operations           |
| `[PATCH]` | Signature patching           |
| `[GEN]`   | Hero/misc generation         |

### Debug Build

```bash
dotnet build -c Debug
# Then run from bin/Debug/net8.0-windows/
```

---

## 🔗 Getting Help

1. **Check console logs** - Copy and share error messages
2. **GitHub Issues** - [Open an issue](https://github.com/Anneardysa/ArdysaModsTools/issues)
3. **Discord** - [Join community](https://discord.gg/5xKg4fyumv)
