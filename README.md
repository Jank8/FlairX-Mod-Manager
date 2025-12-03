# FlairX Mod Manager

<img align="right" width="256" height="256" alt="appicon" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

Modern mod manager for miHoYo games built with **WinUI 3** and **.NET 10**. Handles **1100+ mods** using less than **600MB RAM** with smooth performance and intuitive interface.

## What's New in v3.1.0

### 🎨 Advanced Image Optimization System
- **4 Optimization Modes:**
  - **Full** - Resize to 1000×1000, smart crop, JPEG conversion, generate all thumbnails
  - **Lite** - JPEG compression only, preserve original dimensions, generate thumbnails
  - **Rename + Thumbnails** - Standardize filenames and create thumbnails
  - **Rename Only** - Just rename files, no processing
- **Smart Cropping Algorithms:**
  - **Center** - Traditional center crop
  - **Smart** - Edge detection for optimal framing
  - **Entropy** - Focus on high-detail areas
  - **Attention** - Detect faces and important regions
  - **Manual** - Interactive crop adjustment with live preview
- **Original File Preservation** - Keep source files with `_original` suffix for unlimited reoptimization attempts
- **Parallel Processing** - Configure thread count (auto-detect or manual) for faster batch optimization
- **Crop Inspection Panel** - Preview and adjust crops before applying with drag-and-resize interface

### 🌐 GameBanana Enhancements
- **Cloudflare Bypass** - Automatic handling of Cloudflare protection with browser verification dialog
- **Improved Download Reliability** - Better error handling and retry logic
- **Auto-Optimization** - Downloaded mods automatically optimized with configurable settings
- **Smart Extraction** - Handles nested folders and complex archive structures

### 🖼️ Category Image System
- **Dual Image Types:**
  - `catprev.jpg` (600×600) - Square format for navigation icons and hover previews
  - `catmini.jpg` (600×722) - Portrait format for category grid tiles
- **Auto-Refresh** - Navigation menu updates when switching view modes
- **Optimized Loading** - Separate images for different UI contexts improve performance

### ⚙️ Settings & Configuration
- **Dedicated Image Optimizer Page** - Comprehensive settings for all optimization features
- **Per-Context Settings** - Different modes for manual, drag-drop, and auto-download optimization
- **JPEG Quality Control** - Adjustable compression (1-100)
- **Backup Options** - Optional ZIP backup before optimization
- **Reoptimization Toggle** - Choose whether to reprocess already optimized files

### 🔧 Technical Improvements
- **Thread-Safe Operations** - Proper file locking prevention during reoptimization
- **Memory Efficient** - Optimized image processing pipeline
- **Better Error Handling** - Detailed logging and graceful failure recovery
- **Archive Helper** - Unified archive extraction (ZIP, 7z, RAR)

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
- **Cloudflare Protection Bypass** - Automatic handling with browser verification
- **Update Checking** - See which mods have updates available
- **NSFW Filter** - Optional content filtering
- **Smart Extraction** - Handles complex archive structures automatically

### Image Management
- **Advanced Optimization System** - Multiple modes: Full, Lite, Rename, RenameOnly
- **Smart Reoptimization** - Preserves originals with `_original` suffix for multiple optimization passes
- **Flexible Image Sizes** - Preview: 1000×1000, Minitile: 600×722, Category Preview: 600×600, Category Mini: 600×722
- **100 Images Per Mod** - Support for extensive mod galleries
- **Automatic Processing** - Optimizes on download and drag & drop
- **Parallel Processing** - Configurable thread count for faster batch optimization
- **Crop Inspection** - Optional manual crop adjustment before optimization

### System Features
- **No Admin Required** - Works on any Windows file system (NTFS, FAT32, exFAT)
- **Preset System** - Save and load mod configurations
- **StatusKeeper Integration** - Backup and restore mod states
- **Global Hotkeys** - System-wide shortcuts (Ctrl+O, Ctrl+R, Ctrl+S, Ctrl+D, Ctrl+F)
- **16 Languages** - Automatic detection with specialized fonts
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

**Adding Images:**
- **GameBanana Browser:** Mods installed via internal browser have previews automatically downloaded and optimized, you can add more inside and optimize manually to combine with more images.
- **Drag & Drop:** Select up to 100 images (JPG/PNG) and drag onto any mod tile - automatically optimized
- **Manual:** Place `preview.jpg`, `preview001.jpg`, `preview002.jpg`, etc. in mod folder, then run "Optimize Previews" from Settings

**Image Optimization Modes:**
- **Full Mode:** 
  - Resize to 1000×1000 (mods) or 600×600/600×722 (categories)
  - Smart crop with 4 algorithms (Center, Smart, Entropy, Attention) or manual adjustment
  - Convert to JPEG with configurable quality
  - Generate all thumbnails (minitile, catprev, catmini)
  - Optional backup and original preservation
- **Lite Mode:** 
  - Convert to JPEG with compression
  - Preserve original dimensions (no resizing or cropping)
  - Generate thumbnails
  - Faster processing for large batches
- **Rename + Thumbnails:** 
  - Standardize filenames (preview.jpg, preview-01.jpg, etc.)
  - Generate thumbnails from existing images
  - No quality changes to source files
- **Rename Only:** 
  - Only rename files to standard naming convention
  - No processing or thumbnail generation
  - Fastest option for organization

**Optimization Results:**
- **Mod Images:** `preview.jpg`, `preview-01.jpg`, `preview-02.jpg`, etc. (1000×1000px in Full mode)
- **Mod Thumbnail:** `minitile.jpg` (600×722px for fast grid loading)
- **Category Preview:** `catprev.jpg` (600×600px for hover popup and navigation icons)
- **Category Tile:** `catmini.jpg` (600×722px for category grid tiles)
- **Limit:** Up to 100 images per mod

**Smart Cropping Algorithms:**
- **Center Crop** - Traditional center-based cropping (fastest)
- **Smart Crop** - Edge detection to find optimal framing
- **Entropy Crop** - Focuses on areas with highest detail/complexity
- **Attention Crop** - Detects faces and important visual elements
- **Manual Crop** - Interactive adjustment with live preview and drag-resize

**Original File Preservation:**
- Enable "Keep Originals" to preserve source files with `_original` suffix
- Allows unlimited reoptimization attempts with different settings
- Automatically uses `_original` files when reoptimizing
- Example: `preview000_original.png` preserved while `preview.jpg` is optimized multiple times

**Category Thumbnails:**
- Place `preview.jpg` in category folder (e.g., `XXMI/GIMI/Mods/Category/`)
- Run "Optimize Previews" from Settings to generate `catprev.jpg` (600×600) and `catmini.jpg` (600×722)
- Categories use Full or RenameOnly modes only (Lite/Rename modes skipped)

**Example Mod Structure:**
```
📁 XXMI/GIMI/Mods/
├── 📁 Category/
│   ├── 📄 preview.jpg          # Source image for category
│   ├── 📄 catprev.jpg          # Category preview 600x600 (hover/navigation, auto-generated)
│   ├── 📄 catmini.jpg          # Category tile 600x722 (grid display, auto-generated)
│   └── 📁 ModName/
│       ├── 📄 mod.json
│       ├── 📄 preview.jpg      # Optimized image 1 (1000x1000 in Full mode)
│       ├── 📄 preview000_original.png  # Original preserved (if Keep Originals enabled)
│       ├── 📄 minitile.jpg     # Mini thumbnail for grid (600x722, auto-generated)
│       ├── 📄 preview-01.jpg   # Optimized image 2 (1000x1000 in Full mode)
│       ├── 📄 preview-02.jpg   # Optimized image 3 (1000x1000 in Full mode)
│       └── 📁 [mod files...]
```

### Directory Structure

```
📁 app/
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
- SharpSevenZip (2.0.33) + 7z.dll
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
