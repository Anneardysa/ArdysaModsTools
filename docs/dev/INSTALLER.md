# Installer Build Guide

Professional Inno Setup installer for ArdysaModsTools.

## Quick Start

```powershell
python scripts\build\build_installer.py
```

Output: `installer_output\ArdysaModsTools-Setup-v2.0.exe`

---

## Prerequisites

| Requirement      | Download                                                                 |
| ---------------- | ------------------------------------------------------------------------ |
| **Inno Setup 6** | [jrsoftware.org/isinfo.php](https://jrsoftware.org/isinfo.php)           |
| **.NET 8 SDK**   | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Python 3.8+**  | [python.org](https://www.python.org/)                                    |

---

## Installer Features

### Automatic Prerequisite Detection

| Component                  | Action                                  |
| -------------------------- | --------------------------------------- |
| **.NET 8 Desktop Runtime** | Prompts download if missing             |
| **WebView2 Runtime**       | Auto-installs from bundled bootstrapper |

### Smart Installation

-  **Auto-uninstall** previous versions before installing
-  **Font installation** (JetBrains Mono)
-  **Clean upgrade** path with folder cleanup
-  **Admin privileges** for Dota 2 file modifications

### Modern UI/UX

-  Modern wizard style
-  Custom welcome page with feature list
-  Desktop shortcut (optional)
-  Post-install launch option

---

## File Structure

```
scripts/build/
├── installer.iss        # Inno Setup script
└── build_installer.py   # Automated build script

tools/
├── vpk/                 # VPK tools (bundled)
├── hllib/               # HLLib tools (bundled)
└── webview2/            # WebView2 bootstrapper

Assets/Fonts/            # JetBrains Mono (auto-installed)
```

---

## Build Process

1. **Clean** - Removes previous publish folder
2. **Publish** - `dotnet publish` in Release mode
3. **Verify** - Checks for main executable
4. **Compile** - Runs Inno Setup compiler

---

## Customization

### Version Update

Edit `installer.iss` line 9:

```iss
#define MyAppVersion "2.0"
```

---

## Troubleshooting

| Error            | Solution             |
| ---------------- | -------------------- |
| `File not found` | Check source paths   |
| `ISCC not found` | Install Inno Setup 6 |
| `Publish failed` | Install .NET 8 SDK   |
