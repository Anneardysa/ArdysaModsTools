# 💿 AMT (ArdysaModsTools)

![ArdysaModsTools Logo](https://yt3.googleusercontent.com/OQcC2Dt77FQaH0ORtSsNpOR-mv36MtE7T0vL1wTTNvEKKwO1MZSoq83Pzk-XMWyqjFDa2quQlg=w1707-fcrop64=1,00005a57ffffa5a8-k-c0xffffffff-no-nd-rj) <!-- Replace with actual logo URL if available -->

**ArdysaModsTools** is a user-friendly Windows application designed to simplify the process of installing, disabling, and updating mods for 
Dota 2. Whether you're a casual player or a mod enthusiast, this tool helps you manage your Dota 2 mods effortlessly.

## 📌 Overview

ArdysaModsTools allows you to:
- **Detect Dota 2 Folder:** Automatically or manually locate your Dota 2 installation folder.
- **Install Mods:** Apply mods to your Dota 2 game with a single click.
- **Disable Mods:** Revert to the original game files easily.
- **Update Patcher:** Keep your mods up-to-date with the latest versions.
- **Check Mod Status:** See if mods are installed and working with a clear status indicator.

## 🚀 Features

- **Auto Detect Dota 2 Folder:** Automatically finds your Dota 2 installation by checking Steam registry keys and library folders.
- **Manual Detect Option:** Manually select your Dota 2 folder if auto-detection doesn’t work.
- **One-Click Mod Installation:** Install mods by copying local files and downloading necessary files from online sources.
- **Disable Mods Easily:** Restore original game files with a single click.
- **Update Patcher:** Refresh your mods to the latest version.
- **Status Indicator:** Displays a colored dot and text to show mod status:
  - 🟢 **Green Dot + "Working"**: Mods are installed and match the latest online versions.
  - 🟡 **Yellow Dot + "Not Installed"**: Mods are not installed or do not match the latest versions.
- **Progress Bar:** Visual feedback during operations like detection, installation, and disabling.
- **Log Console:** Detailed logs of operations for troubleshooting.

## 📥 Download and Installation

1. **Download the Latest Release:**
   - Visit the [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases) page and download the latest version (`ArdysaModsTools.exe` and the `game` folder).
   - Ensure the `game` folder is in the same directory as `ArdysaModsTools.exe`.

2. **Extract Files:**
   - Extract the downloaded files to a folder on your computer.

3. **Run the Application:**
   - Double-click `ArdysaModsTools.exe` to launch the tool.

## 🖥️ How to Use

### 1. Detect Dota 2 Folder
- **Auto Detect:** Click the "Auto Detect Dota 2" button to let the tool find your Dota 2 folder automatically. It checks Steam registry keys and library folders.
- **Manual Detect:** If auto-detection fails, click "Manual Detect Dota 2" and browse to your Dota 2 folder (e.g., `C:\Program Files (x86)\Steam\steamapps\common\dota 2 beta`).
- Once detected, the tool will check the mod status and enable additional buttons.

### 2. Check Mod Status
- After detecting the Dota 2 folder, the tool checks if mods are installed:
  - A 🟢**Green Dot** with "Working" means mods are installed and up-to-date.
  - A 🟡**Yellow Dot** with "Not Installed" means mods need to be installed or updated.

### 3. Install Mods
- Click the "Install Mods" button to apply mods to your Dota 2 game.
- The tool copies local mod files from the `game` folder and downloads additional files from online sources.
- Progress is shown on the progress bar, and logs are displayed in the console.
- After installation, the mod status is updated automatically.

### 4. Disable Mods
- Click the "Disable Mods" button to revert to the original Dota 2 game files.
- The tool downloads the original files and replaces the modded ones.
- Progress and logs are shown, and the mod status is updated afterward.

### 5. Update Patcher
- Click the "Update Patcher" button to bypassing your mods to the latest version.
- The tool downloads the latest mod files and applies them.
- Progress, logs, and status updates occur as usual.

## ⚙️ Requirements

- **Operating System:** Windows (tested on Windows 10).
- **Dota 2 Installed:** Ensure Dota 2 is installed via Steam.
- **Internet Connection:** Required for downloading mod files during Install, Disable, and Update Patcher operations.
- **Dependencies:**
  - The `game` folder must be in the same directory as `ArdysaModsTools.exe` (contains local mod files).
  - .NET Framework (included with most Windows installations).

## 🛠️ Troubleshooting

- **Auto Detect Fails:**
  - Ensure Steam is installed and Dota 2 is in your Steam library.
  - Try Manual Detect and select the `dota 2 beta` folder manually.
- **Installation Fails with "game folder not found":**
  - Make sure the `game` folder is in the same directory as `ArdysaModsTools.exe`.
  - The `game` folder should contain the `_ArdysaMods` folder with `pak01_dir.vpk`.
- **Status Shows "Not Installed":**
  - Install or update the mods using the "Install Mods" or "Update Patcher" buttons (if you already install ArdysaMods before).

## 📝 Notes

- The `System.Net` namespace is included but currently unused. Future updates may remove it to clean up the code.
- The tool is designed for Dota 2 mods and may not work with other games.
- Always ensure you have a backup of your Dota 2 folder before installing mods.

## 📜 License

This project is licensed under the [MIT License](LICENSE). See the LICENSE file for details.

## 📬 Contact

For support or suggestions:
- **GitHub Issues:** [Open an issue](https://github.com/Anneardysa/ArdysaModsTools/issues)
- **YouTube:** [Ardysa YouTube Channel](https://www.youtube.com/@Ardysa?sub_confirmation=1)

Happy modding! 🎮
