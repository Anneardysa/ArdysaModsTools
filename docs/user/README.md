# ArdysaModsTools 2.0

**The Ultimate Dota 2 Mod Manager**

![ArdysaModsTools](images/main_window_interface.png)

---

## 🎮 What is ArdysaModsTools?

ArdysaModsTools (AMT) is a powerful, easy-to-use Windows application that lets you customize your Dota 2 experience with cosmetic modifications. Install custom hero sets, weather effects, terrain skins, and more—all with just a few clicks!

### ✨ Key Features

-  **🚀 One-Click Mod Installation** - Download and install curated mod packs automatically
-  **🦸 Custom Hero Sets** - Choose from hundreds of community-created hero skins
-  **🌦️ Misc Customization** - Weather effects, terrain, HUD, and audio mods
-  **🔄 Auto-Updates** - Keep mods working after Dota 2 game updates
-  **🔍 Smart Detection** - Automatically finds your Dota 2 installation
-  **⚡ Fast & Efficient** - Optimized VPK compilation and patching
-  **🎨 Modern Interface** - Sleek, intuitive dark-themed UI

---

## 💻 System Requirements

| Component   | Requirement            |
| ----------- | ---------------------- |
| OS          | Windows 10/11 (64-bit) |
| .NET        | Desktop Runtime 8.0+   |
| Dota 2      | Installed via Steam    |
| Storage     | 500 MB free space      |
| Permissions | Administrator rights   |

---

## 📥 Installation

1. **Download** `ArdysaModsTools_Setup_x64.exe`
2. **Run as Administrator**
3. **Install .NET 8** if prompted
4. **Complete installation**
5. **Launch the app**

> **⚠️ Must close Dota 2 before launching AMT!**

---

## 🚀 Quick Start

### First Time Setup

1. **Launch AMT** (Dota 2 must be closed)
2. **Click "Auto Detect"** to find Dota 2
3. **Click "Install"** and choose "Auto Install"
4. **Wait for completion** (status shows "Ready")
5. **Launch Dota 2** and enjoy!

### Daily Usage

**Install/Update Mods**:

```
Install → Auto Install
```

**Create Hero Skins**:

```
Select Hero → Choose hero & set → Generate
```

**Add Weather/Terrain**:

```
Miscellaneous → Select options → Generate
```

**After Dota 2 Updates**:

```
Patch Update → Quick Patch
```

---

## 📖 Documentation

### For Users

-  **[Quick Start Guide](QUICK_START.md)** - Get started in 5 minutes
-  **[User Guide](USER_GUIDE.md)** - Comprehensive documentation with screenshots

### For Developers

See the `/docs` folder in the source repository for:

-  Architecture documentation
-  Development guide
-  API reference
-  Contributing guidelines

---

## 🎯 Main Features Explained

### 1. Mod Installation

Install curated mod packs that include multiple cosmetic enhancements:

-  One-click download from CDN
-  Automatic validation and installation
-  Patches game configuration files
-  Updates signatures for compatibility

**Status Indicators**:

-  🟢 **Ready** - Mods working perfectly
-  🟠 **Need Update** - Patch required after game update
-  🔴 **Error** - Issue detected
-  ⚫ **Not Installed** - No mods installed

### 2. Hero Set Generation

![Hero Selection](images/hero_selection_screen.jpg)

Create custom hero skins:

-  Browse 100+ heroes with searchable grid
-  Choose from multiple sets per hero
-  Generate multiple heroes at once
-  Favorite system for quick access
-  Takes 2-5 minutes per hero

**How it works**:

1. Downloads base game files
2. Merges custom set assets
3. Patches item definitions
4. Recompiles VPK
5. Replaces original files

### 3. Miscellaneous Mods

![Misc Mods](images/misc_mods_screen.jpg)

Customize additional elements:

-  **Weather**: Moonbeam, Aurora, Snow, Ash, etc.
-  **Terrain**: Custom map skins
-  **HUD**: Interface modifications
-  **Audio**: Sound and music replacements

**Generation Modes**:

-  **Clean Generate**: Fresh start
-  **Add to Current**: Merge with existing

---

## ⚙️ Advanced Features

### Patch Management

After Dota 2 updates, mods may need re-patching:

-  **Quick Patch**: Fast, updates signatures only (recommended after updates)
-  **Full Patch**: Complete re-patch of all files
-  **Auto-detection**: Status indicator shows when patching is needed

### Verification

Verify mod installation integrity:

-  Right-click "Patch Update" button
-  Select "Verify Mod Files"
-  View detailed status report

### Console Logs

Real-time operation logs visible at bottom of main window:

-  Download progress
-  File operations
-  Errors and warnings
-  Click "Copy" to copy logs for debugging

---

## ❗ Important Warnings

> **⚠️ USE AT YOUR OWN RISK**
>
> This tool modifies Dota 2 game files. While it only changes cosmetic elements:
>
> -  Valve's policy on mods may change
> -  Potential for game bans (unlikely but possible)
> -  Risk of file corruption
> -  No warranty provided
>
> **Always backup your files before modding!**

> **🚫 Cannot run while Dota 2 is active**
>
> AMT must be closed before launching Dota 2, and vice versa.

> **🔄 After every Dota 2 update**
>
> Run "Quick Patch" to maintain mod compatibility.

---

## 🔧 Troubleshooting

### Common Issues

| Problem                  | Solution                       |
| ------------------------ | ------------------------------ |
| Can't launch AMT         | Close Dota 2 completely        |
| Auto detect failed       | Use Manual Select button       |
| Mods not visible in-game | Run Patch Update → Full Patch  |
| After game update        | Run Patch Update → Quick Patch |
| Permission errors        | Run AMT as Administrator       |
| .NET 8 not found         | Install from installer prompt  |

### Getting Help

1. Check console logs (click Copy button)
2. Read full User Guide
3. Join Discord for community support
4. Watch YouTube tutorials

---

## 🌐 Community & Support

### Links

-  💬 **Discord**: [discord.gg/ardysa](https://discord.gg/ardysa) - Get help, share sets, discuss
-  📺 **YouTube**: [youtube.com/@ardysa](https://youtube.com/@ardysa) - Tutorials and updates
-  ☕ **Support**: [ko-fi.com/ardysa](https://ko-fi.com/ardysa) - Buy me a coffee!

### Support the Project

AMT is **free** and open-source. If you find it useful:

-  ⭐ Star the repository
-  ☕ Donate on Ko-fi
-  📢 Share with friends
-  🐛 Report bugs
-  💡 Suggest features

---

## 📋 FAQ

**Q: Is this safe?**  
A: Cosmetic mods only, but use at your own risk. No guarantees against bans.

**Q: Can I get VAC banned?**  
A: Unlikely for cosmetic mods, but we cannot guarantee immunity.

**Q: Does this work on Mac/Linux?**  
A: No, Windows-only (10/11 64-bit).

**Q: Where are mods installed?**  
A: `dota 2 beta/game/dota/_ArdysaMods/pak01_dir.vpk`

**Q: How do I uninstall?**  
A: Click "Disable" in AMT, then uninstall via Windows Settings.

**Q: Can I use multiple hero sets?**  
A: Yes! Select different sets for different heroes.

**Q: What if I want vanilla Dota 2 back?**  
A: Click "Disable" or verify game files in Steam.

---

## 📜 License

```
MIT License

Copyright (c) 2024-2025 Ardysa

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

See [LICENSE.txt](LICENSE.txt) for third-party notices and full disclaimer.

---

## 🔨 Technical Details

### Architecture

-  **Language**: C# 12 / .NET 8.0
-  **UI Framework**: Windows Forms
-  **Pattern**: MVP (Model-View-Presenter)
-  **VPK Tools**: HLExtract.exe, vpk.exe
-  **Obfuscation**: ConfuserEx (release builds)
-  **Installer**: Inno Setup

### Project Structure

```
AMT2.0/
├── Core/          # Business logic (services, models)
├── UI/            # WinForms (forms, controls)
├── Helpers/       # Utilities
├── Assets/        # Fonts, icons, images
├── scripts/       # Build automation
├── tools/         # External binaries
└── docs/          # Documentation
```

### Build From Source

```bash
# Clone repository
git clone https://github.com/ardysa/AMT2.0.git
cd AMT2.0

# Restore and build
dotnet restore
dotnet build -c Release

# Run
dotnet run
```

See [docs/development.md](docs/development.md) for full developer guide.

---

## 🙏 Acknowledgments

### Third-Party Tools & Libraries

-  **HLLib** - VPK extraction
-  **Valve** - VPK tools
-  **ValveKeyValue** - Configuration parsing
-  **SharpCompress** - Archive handling
-  **QRCoder** - QR code generation
-  **Microsoft WebView2** - Modern web content
-  **ConfuserEx** - Code protection

### Community

Thanks to all contributors, testers, and the Dota 2 modding community!

---

## 📞 Contact

**Developer**: Ardysa

-  Discord: [discord.gg/ardysa](https://discord.gg/ardysa)
-  YouTube: [youtube.com/@ardysa](https://youtube.com/@ardysa)
-  Ko-fi: [ko-fi.com/ardysa](https://ko-fi.com/ardysa)

---

**Enjoy your customized Dota 2 experience with ArdysaModsTools! 🎮✨**

_Version 2.0 | Last Updated: January 2026_
