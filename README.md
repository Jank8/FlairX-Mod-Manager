# FlairX Mod Manager

<img align="right" width="256" height="256" alt="appicon" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

Modern mod manager for miHoYo games built with **WinUI 3** and **.NET 10**. Handles **1100+ mods** using less than **600MB RAM** with smooth performance and intuitive interface.

## Supported Games

- **Zenless Zone Zero** (ZZMI)
- **Genshin Impact** (GIMI)
- **Honkai Impact 3rd** (HIMI)
- **Honkai: Star Rail** (SRMI)
- **Wuthering Waves** (WWMI)

## Features

### Mod Management
- **One-Click Activation** - Click tile footer to toggle mods on/off
- **Drag & Drop Images** - Drop up to 100 preview images directly on mod tiles
- **Visual Grid & Table Views** - Multiple ways to browse your collection
- **Category Organization** - Automatic sorting by Characters, Weapons, UI, etc.
- **Multi-Game Support** - Separate libraries per game with automatic switching

### GameBanana Integration
- **Browse & Search** - Find mods directly in the app
- **One-Click Install** - Download and extract automatically
- **Update Checking** - See which mods have updates available
- **NSFW Filter** - Optional content filtering

### Image Management
- **Auto-Optimization** - Resizes to 1000×1000, crops to square, saves as JPG
- **100 Images Per Mod** - Support for extensive mod galleries
- **Automatic Processing** - Optimizes on download and drag & drop
- **Mini-Tiles** - Generates small thumbnails for fast loading

### System Features
- **No Admin Required** - Works on any Windows file system (NTFS, FAT32, exFAT)
- **Preset System** - Save and load mod configurations
- **StatusKeeper Integration** - Backup and restore mod states
- **Global Hotkeys** - System-wide shortcuts (Ctrl+O, Ctrl+R, Ctrl+S, Ctrl+D)
- **17 Languages** - Automatic detection with specialized fonts
- **Zoom Support** - Ctrl + Mouse Wheel (100%-250%)
- **Modern UI** - Fluent Design with Mica/Acrylic effects

## System Requirements

- **Windows 10 22H2** or **Windows 11**
- **[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)**
- **[Windows App SDK 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)**
- **XXMI Framework** (Portable version)
- **2GB RAM** minimum, 4GB recommended
- **1280×720** minimum display resolution
- **No administrator rights required** - works on any file system

## Installation

1. Download latest release
2. Extract to desired location
3. Run `FlairX Mod Manager Launcher.exe`
4. Select your game and start managing mods

## Quick Start

### Adding Mods

**Option 1: GameBanana Browser**
1. Click "Browse GameBanana" button
2. Search for mods
3. Click download - mod installs automatically
4. Click tile footer to activate

**Option 2: Manual Installation**
1. Download mods from GameBanana or other sources
2. Extract to: `XXMI/[GameTag]/Mods/[Category]/[ModName]/`
   - Example: `XXMI/GIMI/Mods/Characters/Ayaka/`
3. Click reload button (↻)
4. Click tile footer to activate

### Adding Preview Images

**Drag & Drop Method:**
1. Select up to 100 images (JPG/PNG)
2. Drag them onto any mod tile
3. Images are automatically optimized and numbered
4. First image becomes the mod thumbnail

**Manual Method:**
1. Place images in mod folder as `preview.jpg`, `preview-01.jpg`, etc.
2. Click "Optimize Previews" in Settings
3. Images are resized to 1000×1000 and cropped to square

### Directory Structure

```
📁 FlairX Mod Manager/
├── 📁 XXMI/
│   ├── 📁 ZZMI/Mods/          # Zenless Zone Zero
│   ├── 📁 GIMI/Mods/          # Genshin Impact
│   ├── 📁 HIMI/Mods/          # Honkai Impact 3rd
│   ├── 📁 SRMI/Mods/          # Honkai: Star Rail
│   └── 📁 WWMI/Mods/          # Wuthering Waves
├── 📁 Settings/
│   ├── 📄 Settings.json
│   ├── 📁 Presets/
│   │   ├── 📁 ZZ/
│   │   ├── 📁 GI/
│   │   ├── 📁 HI/
│   │   ├── 📁 SR/
│   │   └── 📁 WW/
│   └── 📄 *.log
└── 📁 Language/
```

## Building from Source

### Prerequisites
- Visual Studio 2026 with **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**
- **[Windows App SDK 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)**

### Build Main Application
```bash
git clone [repository-url]
cd "FlairX Mod Manager"
dotnet restore
dotnet build --configuration Release
```

### Build Launcher
```bash
dotnet publish "FlairX-Mod-Manager Launcher/FlairX Mod Manager Launcher.csproj" -c Release -p:PublishSingleFile=true -r win-x64 --self-contained true
```

Output: `FlairX-Mod-Manager Launcher\bin\Release\net10.0\win-x64\publish\FlairX Mod Manager Launcher.exe`

### Key Dependencies
- Microsoft.WindowsAppSDK (1.8.251106002)
- CommunityToolkit.WinUI (8.2.x)
- Microsoft.Graphics.Win2D (1.3.2)
- SharpCompress (0.41.0)
- ReverseMarkdown (4.7.1)
- NLua (1.7.5)

## Troubleshooting

**Mods not activating?**
- Verify game is selected
- Check mod folder names are valid
- Reload manager (Ctrl+R)

**Missing thumbnails?**
- Ensure mod folders contain preview images
- Run image optimization from Settings

**XXMI not launching?**
- Verify XXMI portable version in XXMI/ directory
- Check launcher path in settings

**Logs:** Check `Settings/` directory for `Application.log`, `StatusKeeper.log`, `GridLog.log`

## Credits

**Author:** [Jank8](https://github.com/Jank8)

**AI Assistance:** [Kiro](https://kiro.dev/), [GitHub Copilot](https://github.com/features/copilot), [Qoder](https://qoder.org/)

**Special Thanks:** [XLXZ](https://github.com/XiaoLinXiaoZhu), [Noto Fonts](https://notofonts.github.io/), Microsoft WinUI 3, XXMI Framework

## License

GNU General Public License v3.0

---

*Not affiliated with miHoYo/HoYoverse. Community tool for managing game modifications.*

**Built entirely through AI-assisted development! 🤖**
