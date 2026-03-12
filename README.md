# FlairX Mod Manager

<img align="right" width="256" height="256" alt="FlairX Mod Manager Logo" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

**Modern mod manager for XXMI-supported games**

FlairX Mod Manager is a powerful application for managing game modifications across XXMI-supported games. Built with .NET 10 and WinUI 3, it offers intuitive mod management with advanced features.

✨ **Key Features:**
- Supports 1100+ mods with optimized performance
- GameBanana integration with auto-update, author search, and Cloudflare support
- Screenshot capture with AI-powered cropping and preview management
- Game overlay with full controller support
- 29 languages with specialized font support
- One-click updates and automatic XXMI framework installation

## 🎮 Supported Games

| Game | Framework | Status |
|------|-----------|--------|
| **Arknights: Endfield** | EFMI | ✅ Fully Supported |
| **Genshin Impact** | GIMI | ✅ Fully Supported |
| **Honkai Impact 3rd** | HIMI | ✅ Fully Supported |
| **Honkai: Star Rail** | SRMI | ✅ Fully Supported |
| **Wuthering Waves** | WWMI | ✅ Fully Supported |
| **Zenless Zone Zero** | ZZMI | ✅ Fully Supported |

## 💻 System Requirements

- **Windows 11** (recommended) or **Windows 10 22H2**
- **.NET 10 Runtime** - automatically installed if missing
- 4GB RAM (8GB recommended)
- 1280×720 display resolution
- 500MB free disk space

## 📥 Installation

1. Download the latest release (v3.9.4)
2. Extract files to your desired location
3. Run `FlairX Mod Manager Launcher.exe`
4. If .NET 10 is missing, FlairX will prompt you to install it
5. Select your game and start managing mods

## 🚀 Quick Start

### Adding Mods

**GameBanana Browser (Recommended):**
1. Click "Browse GameBanana" button
2. Search mods (use `user:exampleuser` to find by author)
3. Click "Download and Install" on mod card
4. Configure options and click "Start"
5. Activate mod by clicking its tile footer

**Manual Installation:**
1. Download mods from any source
2. Extract to: `XXMI/[GameTag]/Mods/[Category]/[ModName]/`
3. Click reload button (↻) in FlairX
4. Activate mod by clicking its tile footer

### Game Overlay

**Access:**
- Press **Alt+W** (keyboard) or **Back+Start** (controller)
- Navigate with D-Pad/Left Stick
- **A** to toggle mods, **B** to close
- **LB/RB** to switch categories
- **Alt+A** (keyboard) or **Back+A** (controller) to filter active mods

### Preview Images

**Add Images:**
- Screenshot Capture: Plus (+) button on mod details
- Drag & Drop: Drop images onto mod tiles
- GameBanana: Automatic with downloads or use "Download Previews" button

**Optimize:**
- AI-powered cropping (Center, Smart, Entropy, Attention)
- Automatic thumbnail generation
- Multi-threaded batch processing

## 🚀 Core Features

### 🎯 **Mod Management**
- **Visual Mod Tiles**: Large preview images with mod names in footer bars
- **One-Click Activation**: Toggle mods by clicking the footer area
- **Smart Activation**: Mods activate by removing `DISABLED_` prefix from folder names
- **Category Navigation**: Left sidebar with expandable categories
- **Game Selection**: Top dropdown for switching between games
- **Advanced Search**: Real-time filtering across all mods
- **Multiple View Modes**: Mods grid, Categories grid, and Table view
- **Duplicate Detection**: Identify and manage duplicate mods
- **Persistent Views**: Remembers Active/Broken/Outdated filter states

**Context Menus:**
- **Mod Actions**: Activate/deactivate, open folder, view details, open URL, copy name, rename, delete
- **Category Actions**: Open folder, copy name, rename
- **Sorting Options**: By name, category, date
- **Filtering**: Active mods, outdated mods, broken mods, hide broken mods

**Mod Details:**
- **Preview Gallery**: Navigate through multiple images
- **Download Previews**: Fetch preview images from GameBanana for installed mods
- **Update Monitoring**: Track dates and author changes
- **Author Information**: Direct links to creators and GameBanana
- **NSFW Control**: Per-mod adult content marking
- **Individual Hotkeys**: Assign keys to mod components

### 🌐 **GameBanana Integration**
- **Built-in Browser**: Browse and search GameBanana directly
- **Author Search**: Find mods by creator using `user:exampleuser` syntax
- **Cloudflare Support**: Automatic handling of protection with cookie management
- **Smart Mod Cards**: Preview images, author info, dates, and statistics
- **Installation Status**: Visual indicators for downloaded mods
- **Download Previews**: Fetch preview images for mods in browser or library
- **NSFW Control**: Filtering with blur options
- **Auto-Update**: Automatically fetch versions, dates, and authors on startup (configurable interval: 1-365 days)

**Download System:**
- **Flexible Options**: Full install or previews-only
- **Preview Management**: Keep, combine, or replace images
- **Clean Install**: Remove existing files before updates
- **Automatic Backups**: Backup before updates
- **Progress Tracking**: Real-time download and extraction progress

**Batch Operations:**
- **Fetch All Data**: Update authors, versions, and dates in one operation
- **Fetch Previews**: Download all or missing preview images
- **Smart Update**: Skip mods with valid data
- **Skip Invalid URLs**: Ignore mods without GameBanana links

### 🎨 **Image System**
- **Screenshot Capture**: Built-in capture with cropping
- **AI-Powered Cropping**: Center, Smart, Entropy, Attention, and Manual modes
- **Multi-threaded Processing**: Configurable threads for batch optimization
- **Automatic Thumbnails**: Generate minitile, catprev, catmini
- **Drag & Drop**: Add images with automatic optimization
- **Quality Control**: Adjustable JPEG quality (1-100%)
- **Backup Safety**: ZIP archives before optimization

### 🎮 **Game Overlay**
- **Always-On-Top**: Quick mod toggle during gameplay
- **Category Navigation**: Browse mods by category
- **Active-Only Filter**: Show only active mods
- **Virtualized Loading**: Optimal performance with large collections
- **Customizable Theme**: Auto/Light/Dark with backdrop effects

**Controller Support (SDL3):**
- **Supported**: Xbox, PlayStation, Nintendo Switch, Steam Controller, generic gamepads
- **Navigation**: D-Pad or Left Stick with 2D grid navigation
- **Haptic Feedback**: Configurable vibration on actions
- **Button Mapping**: A (select), B (back), LB/RB (categories), Back+Start (toggle), Back+A (filter)

### ⌨️ **Keyboard Shortcuts**
- **Ctrl+R**: Reload mod library
- **Ctrl+S**: Shuffle active mods
- **Ctrl+D**: Deactivate all mods
- **Alt+W**: Toggle game overlay
- **Alt+A**: Filter active mods
- **Ctrl + Mouse Wheel**: Zoom (100%-250%)
- **Escape**: Close all sliding panels

*All hotkeys customizable in Settings*

### 💾 **Additional Features**
- **Preset System**: Save and load mod configurations per game
- **StatusKeeper Integration**: Sync with XXMI launcher via d3dx_user.ini
- **ModInfo Backup**: Up to 3 backup versions with one-click restore
- **Sliding Panels**: Non-blocking UI panels that don't cover the menu

## 🌍 Multi-Language Support

FlairX supports **29 languages** with automatic detection:
- English, Spanish, French, German, Italian, Dutch, Swedish, Danish, Norwegian, Finnish
- Japanese, Korean, Chinese (Simplified/Traditional)
- Russian, Polish, Portuguese (BR/PT), Czech, Romanian, Ukrainian
- Hindi, Thai, Filipino, Turkish, Vietnamese, Indonesian, Hungarian, Greek

**Features:**
- Automatic language detection based on system locale
- Specialized fonts for Asian and RTL languages
- Complete UI localization including runtime check dialogs

## 🛠️ Settings

### **Appearance**
- Theme: Auto, Light, Dark
- Backdrop Effect: Mica, Mica Alt, Acrylic, Acrylic Thin, None
- Preview Effect: None, Frame, Accent, Parallax, Glass
- Language: 29 languages with automatic detection

### **Behavior**
- Skip XXMI Launcher Startup
- Move Active Mods to Top
- Auto-deactivate Conflicting Mods (can be disabled; re-enable to reset "don't ask again" preference)
- Dynamic Filtering
- Grid Zoom (100%-250%)
- Minimize to Tray
- Hide NSFW Content

### **GameBanana Auto-Update**
- Enable/disable automatic updates
- Configurable interval (1-365 days)
- Fetches versions, dates, and authors on startup
- Skip mods with invalid URLs

### **Hotkeys**
- Enable/disable global hotkeys
- Customize all shortcuts with edit and reset options

### **Updates**
- Automatic update check on startup

## 📁 Folder Structure

```
📁 FlairX Mod Manager/
└── 📁 XXMI/
    ├── 📁 EFMI/
    │   └── 📁 Mods/
    │       ├── 📁 Ayaka/                # Category folder
    │       │   ├── 📁 Outfit_Red/       # Mod folder
    │       │   ├── 📁 Dress_Blue/       # Mod folder
    │       │   └── 📁 Recolor_Gold/     # Mod folder
    │       ├── 📁 Ganyu/                # Category folder
    │       │   ├── 📁 Outfit_Winter/    # Mod folder
    │       │   └── 📁 Dress_Summer/     # Mod folder
    │       └── 📁 UI/                   # Category folder
    │           └── 📁 Menu_Redesign/    # Mod folder
    ├── 📁 GIMI/
    │   └── 📁 Mods/
    │       ├── 📁 Raiden/
    │       │   ├── 📁 Outfit_Casual/
    │       │   └── 📁 Recolor_Purple/
    │       └── 📁 Nahida/
    ├── 📁 HIMI/
    │   └── 📁 Mods/
    ├── 📁 SRMI/
    │   └── 📁 Mods/
    ├── 📁 WWMI/
    │   └── 📁 Mods/
    └── 📁 ZZMI/
        └── 📁 Mods/
```

**Structure:**
- Path format: `XXMI/[GameTag]/Mods/[Category]/[ModName]/`
- **Two-level organization**: Category → Mod
- Categories created manually or during GameBanana install
- Mods activate by removing `DISABLED_` prefix

**Example:**
- ✅ Correct: `XXMI/GIMI/Mods/Raiden/Outfit_Casual/`
- ❌ Wrong: `XXMI/GIMI/Mods/Characters/Raiden/Outfit_Casual/`

**Game Tags:**
- EFMI = Arknights: Endfield
- GIMI = Genshin Impact
- HIMI = Honkai Impact 3rd
- SRMI = Honkai: Star Rail
- WWMI = Wuthering Waves
- ZZMI = Zenless Zone Zero

## 🏗️ Building from Source

For developers who want to build FlairX themselves:

### Prerequisites
- **Visual Studio 2026** (required for .NET 10 support) or **Visual Studio 2022** with preview .NET 10 SDK
- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**
- **[Windows App SDK 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)**

⚠️ **Important**: .NET 10 is officially supported only in Visual Studio 2026. Visual Studio 2022 requires enabling preview .NET SDK support and may have compatibility issues.

### Build Steps
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

### Key Technologies Used
- Microsoft.WindowsAppSDK (1.8.260209005) - Modern Windows UI
- CommunityToolkit.WinUI (7.1.2) - UI components
- ppy.SDL3-CS (2025.1205.0) - Controller support
- Microsoft.Web.WebView2 (1.0.3800.47) - GameBanana browser
- SharpSevenZip (2.0.36) - Archive extraction

## 📄 License

GNU General Public License v3.0

## 🙏 Credits

**Author:** [Jank8](https://github.com/Jank8)

**AI Assistance:** [Kiro](https://kiro.dev/), [GitHub Copilot](https://github.com/features/copilot), [Qoder](https://qoder.org/)

**Special Thanks:** [XLXZ](https://github.com/XiaoLinXiaoZhu), [Noto Fonts](https://notofonts.github.io/), [Kenney Input Prompts](https://kenney.nl/assets/input-prompts), Microsoft WinUI 3, XXMI Framework

---

*Not affiliated with HoYoverse. Community tool for managing game modifications.*

**Built entirely through AI-assisted development! 🤖**