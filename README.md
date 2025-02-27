## Step 1: Detect Your Dota 2 Folder

Before using any mod features, you need to detect your Dota 2 folder. Use either method below:

### Auto Detect Dota 2

1. Click **"Auto Detect Dota 2"**.
2. The app searches your Steam installation for the "dota 2 beta" folder.
3. Wait a moment (progress bar fills)—all buttons will be disabled during this process.
4. If successful, you’ll see a log message like:
`[17:03] Dota 2 folder detected: C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta, Method: SteamExe default path`
6. If it fails (e.g., `[17:03] No Dota 2 folder found across all methods.`), try Manual Detect.

### Manual Detect Dota 2

1. Click **"Manual Detect Dota 2"**.
2. A folder browser opens—all buttons disable while you choose.
3. Navigate to your Dota 2 folder (e.g., `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`) and click **OK**.
- The folder name must be exactly "dota 2 beta" (case doesn’t matter).
4. If correct, you’ll see:`[17:03] Dota 2 folder manually selected: C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`
5. If wrong (e.g., not "dota 2 beta"), a message pops up: "Please select 'dota 2 beta' Folder."
- Try again until you pick the right folder or click **Cancel** (`[17:03] Manual detection canceled.`).

## Install Mods

1. **Detect Folder:** Use "Auto Detect" or "Manual Detect" first to enable the "Install Mods" button.
2. Click **"Install Mods"**.
3. Watch the console log:
- `[HH:mm] Applying mods...`
- `[HH:mm] Starting mod installation...`
- `[HH:mm] Copying local mod files...`
- `[HH:mm] Downloading mod files from online sources...`
- `[HH:mm] Mod installation completed.`
4. Wait for the progress bar to finish
5. **If It Fails:**
- Check logs like `[HH:mm] Failed to copy local mod files: [error]` or `[HH:mm] Download failed: [error]`.
- Ensure internet is active and your Dota 2 folder is writable (run as Administrator if needed).

## Disable Mods

Reverts mods by downloading original Dota 2 files.

1. **Detect Folder:** Ensure a folder is detected to enable "Disable Mods."
2. Click **"Disable Mods"**.
- All buttons disable during processing.
3. Watch the console log:
- `[HH:mm] Disabling mods...`
- `[HH:mm] Starting mod disable process...`
- `[HH:mm] Downloading original files to disable mods...`
- `[HH:mm] Mod disabling completed.`
4. Wait for the progress bar
5. **If It Fails:**
- See logs like `[HH:mm] Download failed: [error]`.
- Check internet connection or folder permissions.

## Update Mods (Update Patcher)

Updates your Dota 2 patcher files to keep mods compatible—use this if you see "Patch is Outdated."

1. **Detect Folder:** Ensure a folder is detected to enable "Update Patcher."
2. Click **"Update Patcher"**.
3. Watch the console log:
- `[HH:mm] Updating patcher...`
- `[HH:mm] Downloading patcher files...`
- `[HH:mm] Mods Updated.`
4. Wait for the progress bar
5. **If You See "Patch is Outdated" Issue:**
- This means your Dota 2 patch doesn’t match the mod files.
- Run "Update Patcher" to download the latest compatible files from Google Drive.
- If it still fails (e.g., `[HH:mm] Download failed: [error]`), check your internet or contact support—the patch files online might need updating by the developer.
6. **If It Fails:**
- Logs like `[HH:mm] Patcher update failed: [error]` indicate network or permission issues—retry with Administrator rights or stable internet.

## Troubleshooting Tips

- **No Folder Detected:** Ensure Dota 2 is installed and use "Manual Detect" if "Auto Detect" fails.
- **Download Errors:** Check your internet connection or antivirus settings (allow `ArdysaModsTools.exe`).
- **Need Help?** Share the console log (e.g., `[17:03] Download failed: [error]`) with the developer.

---

**Notes:**
- **Internet Required:** For downloading mod/patcher files during "Install Mods," "Disable Mods," and "Update Mods."

Enjoy modding Dota 2! If you encounter issues, especially "Patch is Outdated," use "Update Patcher" or reach out for support.
