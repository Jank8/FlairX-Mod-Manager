# FlairX Mod Manager

<img align="right" width="256" height="256" alt="appicon" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

A comprehensive mod management application for multiple miHoYo games, built with **WinUI 3** and **.NET 8**. This application provides an intuitive interface for managing, organizing, and activating game modifications across Zenless Zone Zero, Genshin Impact, Honkai Impact 3rd, Honkai: Star Rail, and Wuthering Waves.

**Built with modern Windows 11 Fluent Design** - Features native acrylic backgrounds, smooth animations, and responsive UI that scales efficiently to handle **900+ mods** using only **~900MB RAM**.




## Supported Games

- **Zenless Zone Zero (ZZZ)** - ZZMI
- **Genshin Impact (GI)** - GIMI  
- **Honkai Impact 3rd (HI)** - HIMI
- **Honkai: Star Rail (SR)** - SRMI
- **Wuthering Waves (WW)** - WWMI

## Key Features

### 🎮 Mod Management
- **Visual Mod Library**: Browse mods with thumbnails in a responsive, virtualized grid layout
- **Multiple Preview Images**: Support for up to 100 preview images per mod with navigation controls
- **Category Organization**: Automatic detection of categories (Characters, Weapons, UI, Effects) from mod library structure
- **Dynamic Menu**: Categories automatically appear in the sidebar menu for easy navigation
- **Mod Activation**: Toggle mods on/off using symbolic links with simple heart icons
- **Real-time Search**: Dynamic search functionality across all categories simultaneously
- **Multi-Game Support**: Switch between games with isolated mod libraries and configurations
- **Automatic Detection**: Smart detection of mod metadata and hotkeys from configuration files

### 🔧 Advanced Features
- **Preset System**: Save and load different mod configurations for each game
- **XXMI Integration**: Built-in launcher for the XXMI mod injection framework
- **Hotkey Detection**: Automatic detection of mod hotkeys from INI configuration files
- **StatusKeeper Integration**: Backup and restore system with dynamic synchronization
- **Namespace Support**: Modern namespace-based mod synchronization alongside classic methods
- **Image Optimization**: Automatic optimization and conversion of preview images to standardized format
- **Multi-language Support**: Automatic language detection with specialized fonts for international scripts
- **Administrator Privileges**: Automatic elevation for symbolic link creation and file operations

### 🚀 Performance Features
- **Optimized Memory Usage**: Efficiently handles **900+ mods** using only **~900MB RAM** (~1MB per mod)
- **Smart Image Caching**: Dual-layer caching system (disk + RAM) with automatic cleanup
- **Background Processing**: Non-blocking thumbnail generation and image optimization
- **Instant Startup**: Fast application launch with progressive mod loading
- **Responsive Interface**: Smooth scrolling and efficient search across large mod collections
- **Performance Monitoring**: Built-in performance measurement and logging system

### 🎨 User Interface
- **Modern Design**: WinUI 3 with Fluent Design System and smooth animations
- **Theme Support**: Light, Dark, and Auto themes with system integration
- **Backdrop Effects**: Mica and MicaAlt (Windows 11 only), Acrylic and AcrylicThin or None (solid background)
- **Responsive Layout**: Adaptive interface with zoom support and window state persistence
- **Accessibility**: Multi-language font support for Chinese, Japanese, Korean, Arabic, Hebrew, Hindi, Thai, and Silesian
- **Loading Experience**: Professional loading screen with progress indication during startup

### 🛠️ Technical Features
- **Security Validation**: Path traversal protection and safe file operations
- **NTFS Validation**: Automatic file system checking for symbolic link support
- **Directory Management**: Automatic creation of required directories with proper structure
- **Error Handling**: Comprehensive error handling with detailed logging
- **Performance Optimization**: Efficient image caching and background processing
- **Global Hotkeys**: Configurable keyboard shortcuts for common operations
- **Smart Window Management**: Real-time resolution display with automatic updates and intelligent validation

## System Requirements

### Operating System
- **Windows 10** version 1809 (build 17763) or later
- **Windows 11** (recommended for best backdrop effects)

### Required Software
- **.NET 8 Desktop Runtime** (automatically prompted if missing)
- **Windows App SDK 1.8** (automatically handled by Windows)
- **XXMI Framework** (Portable version recommended)

### File System & Permissions
- **NTFS partition** required for mod activation (symbolic link support)
- **Administrator privileges** automatically requested for symbolic link creation
- Application validates NTFS file system and shows warnings for incompatible drives

### Hardware
- **RAM**: 2 GB minimum, 4 GB recommended (efficiently handles **900+ mods in ~900MB**)
- **Storage**: 500 MB for application, additional space for mod libraries
- **Display**: 1300x720 minimum resolution (supports up to 20000x15000)
- **Default Window Size**: 1300x720 pixels
- **Performance**: Excellent memory efficiency - approximately **1MB RAM per mod**

## Installation

### Prerequisites
1. Ensure your system meets the requirements above
2. Install .NET 8 Desktop Runtime from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Install latest stable Windows App SDK from [Microsoft's official site](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)

### Installation Steps
1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive to your desired location (preferably on an NTFS drive)
3. Run `FlairX Mod Manager Launcher.exe`
4. The application will automatically request administrator privileges and install missing dependencies

### First Run Setup
1. **Game Selection**: Choose your game from the dropdown menu
2. **XXMI Integration**: Ensure XXMI portable version is extracted to the manager folder
3. **Mod Library**: Place your mods in category folders within the game's ModLibrary directory

## Usage

### Getting Started
1. **Select Game**: Choose your target game from the dropdown menu
2. **Add Mods**: Place mod folders in category directories: `ModLibrary/[Game]/[Category]/[ModName]/`
3. **Browse & Activate**: Use category menu items to browse, then click heart icons to activate mods
4. **Launch**: Use the floating action button to launch XXMI with active mods

### Key Features
- **Category Navigation**: Browse mods by auto-detected categories (Characters, Weapons, UI, etc.)
- **Cross-Category Search**: Search across all categories simultaneously
- **Presets**: Save and load different mod configurations per game
- **StatusKeeper**: Backup/restore mod states with automatic game-specific path detection

## Quick Start Guide

### Setup
1. **Extract and run** `FlairX Mod Manager Launcher.exe`
2. **Download XXMI** portable version when prompted, extract to `app/XXMI/`
3. **Select your game** from the dropdown menu

### Installing Mods
1. **Download** mods from GameBanana/NexusMods
2. **Extract** to: `app/ModLibrary/[Game]/[Category]/[ModName]/`
   - Example: `app/ModLibrary/GI/Characters/Ayaka_Outfit/`
3. **Add preview images** (optional): Include `preview*.png` or `preview*.jpg` files for thumbnails
4. **Click reload** (↻) to detect new mods
5. **Activate** by clicking heart icons

### Directory Structure Example
```
📁 app/ModLibrary/GI/
├── 📁 Characters/
│   ├── 📄 catprev.jpg (auto-generated category preview - 600x600)
│   └── 📁 Ayaka_Outfit/
│       ├── 📄 mod.json (auto-created)
│       ├── 📄 preview.jpg (main preview)
│       ├── 📄 preview-01.jpg (additional preview)
│       ├── 📄 preview-02.jpg (additional preview)
│       ├── 📄 minitile.jpg (auto-generated thumbnail)
│       └── 📁 mod files...
├── 📁 Weapons/
│   ├── 📄 catprev.jpg (auto-generated category preview)
│   └── 📁 weapon mods...
└── 📁 UI/
    ├── 📄 catprev.jpg (auto-generated category preview)
    └── 📁 ui mods...
```

## Preview Image System

### Multiple Preview Support
FlairX Mod Manager supports up to **100 preview images** per mod **and automatic category preview optimization** with navigation:

- **Main Preview**: `preview.jpg` - Primary image shown in grid view
- **Additional Previews**: `preview-01.jpg` through `preview-99.jpg` - Extra images for detailed viewing
- **Navigation**: Left/right arrow buttons appear when multiple images are available
- **Image Counter**: Shows current position (e.g., "3 / 7") in the detail view

### Image Optimization
The built-in optimizer automatically processes both **mod previews** and **category previews**:

#### **Mod Preview Processing:**
1. **Input Formats**: Accepts any `preview*.png`, `preview*.jpg`, or `preview*.jpeg` files
2. **Automatic Processing**: 
   - Crops images to square (1:1 ratio) from center
   - Scales down to maximum 1000x1000 pixels (no upscaling)
   - Converts to optimized JPEG format
   - Renames to proper sequence (`preview.jpg`, `preview-01.jpg`, `preview-02.jpg`, etc.)
3. **Thumbnail Generation**: Creates 600x600 `minitile.jpg` from the **first preview image** (main `preview.jpg`) for grid display
4. **Gap Handling**: Automatically fills gaps in numbering sequence
5. **Safe Cleanup**: Moves original files to Recycle Bin after optimization (recoverable)

#### **Category Preview Processing:**
1. **Input Formats**: Accepts `catprev*.png/jpg`, `catpreview*.png/jpg`, or `preview*.png/jpg` files in category directories
2. **Automatic Processing**:
   - Crops images to square (1:1 ratio) from center
   - Scales to exactly 600x600 pixels for category tiles
   - Converts to optimized JPEG format as `catprev.jpg`
   - Prioritizes `catprev*` files over generic `preview*` files
3. **Category Display**: Creates hover previews and category tile thumbnails
4. **Safe Cleanup**: Moves original files to Recycle Bin after optimization (recoverable)

### Before and After Optimization

**Before Optimization:**
```
📁 Ayaka_Outfit/
├── 📄 preview242.png          # Random named preview files
├── 📄 preview_screenshot.jpg  # Will be processed in alphabetical order
├── 📄 preview999.png          # 
├── 📄 previewA.jpg            # First alphabetically becomes main preview
└── 📁 mod files...
```

**After Optimization:**
```
📁 Ayaka_Outfit/
├── 📄 preview.jpg      # Main preview (from previewA.jpg) - minitile source
├── 📄 preview-01.jpg   # From preview242.png
├── 📄 preview-02.jpg   # From preview_screenshot.jpg  
├── 📄 preview-03.jpg   # From preview999.png
├── 📄 minitile.jpg     # 600x600 thumbnail generated from preview.jpg
├── 📄 mod.json         # Auto-created metadata
└── 📁 mod files...
```

**Key Points:**
- **First alphabetically** becomes `preview.jpg` (the main preview)
- **Only `preview.jpg`** is used to generate the `minitile.jpg` for grid thumbnails
- **Additional images** become `preview-01.jpg`, `preview-02.jpg`, etc. (no minitiles)
- **Maximum 100 images**: Only the first 100 images (alphabetically) are processed
- **Excess images**: If you have 150+ preview files, only the first 100 are kept, the rest are moved to Recycle Bin
- **Original files** are moved to Recycle Bin after successful optimization (recoverable)

### Example with Many Images

**Before Optimization (150 files):**
```
📁 Mod_With_Many_Previews/
├── 📄 previewA.png     # Will become preview.jpg
├── 📄 previewB.jpg     # Will become preview-01.jpg
├── 📄 previewC.png     # Will become preview-02.jpg
├── 📄 previewD.jpg     # Will become preview-03.jpg
├── ...                 # (96 more files processed)
├── 📄 preview099.png   # Will become preview-99.jpg (last kept)
├── 📄 preview100.jpg   # ❌ MOVED TO RECYCLE BIN (exceeds 100 limit)
├── 📄 preview101.png   # ❌ MOVED TO RECYCLE BIN (exceeds 100 limit)
├── ...                 # (49 more files moved to recycle bin)
├── 📄 preview150.jpg   # ❌ MOVED TO RECYCLE BIN (exceeds 100 limit)
└── 📁 mod files...
```

**After Optimization (100 files kept):**
```
📁 Mod_With_Many_Previews/
├── 📄 preview.jpg      # From previewA.png
├── 📄 preview-01.jpg   # From previewB.jpg
├── 📄 preview-02.jpg   # From previewC.png
├── 📄 preview-03.jpg   # From previewD.jpg
├── ...                 # (95 more optimized files)
├── 📄 preview-99.jpg   # From preview099.png
├── 📄 minitile.jpg     # Generated from preview.jpg
└── 📁 mod files...
```

### Adding Preview Images

#### **For Mods:**
1. **Place any preview files** in your mod folder with names like:
   - `preview.png`, `preview.jpg`
   - `preview242.png`, `preview_screenshot.jpg`
   - Any file starting with "preview"
2. **Run image optimization** from Settings → Optimize Preview Images
3. **Images are automatically**:
   - Optimized and renamed to proper sequence
   - Made available for navigation in mod details

#### **For Categories:**
1. **Place category preview files** in category folders with names like:
   - `catprev.png`, `catprev.jpg` (preferred)
   - `catpreview.png`, `catpreview.jpg`
   - `preview.png`, `preview.jpg` (fallback)
2. **Run image optimization** from Settings → Optimize Preview Images
3. **Category images are automatically**:
   - Optimized to 600x600 pixels as `catprev.jpg`
   - Used for category tile thumbnails and hover previews
   - Displayed when hovering over category menu items

## Configuration

### Directory Structure
```
📁 FlairX Mod Manager/
├── 📁 ModLibrary/
│   ├── 📁 ZZ/              # Zenless Zone Zero mods (ZZMI)
│   │   ├── 📁 Characters/  # Character mods
│   │   ├── 📁 Weapons/     # Weapon mods
│   │   ├── 📁 UI/          # UI mods
│   │   └── 📁 Other/       # Other mods
│   ├── 📁 GI/              # Genshin Impact mods (GIMI)
│   ├── 📁 HI/              # Honkai Impact 3rd mods (HIMI)
│   ├── 📁 SR/              # Honkai: Star Rail mods (SRMI)
│   └── 📁 WW/              # Wuthering Waves mods (WWMI)
├── 📁 XXMI/
│   ├── 📁 ZZ/              # Game-specific XXMI directories
│   │   ├── 📁 Mods/        # Active mod symlinks
│   │   └── 📄 d3dx_user.ini # Game configuration
│   └── 📁 GI/, HI/, SR/, WW/ # Other game directories
├── 📁 Settings/
│   ├── 📄 Settings.json    # Main application settings
│   ├── 📁 Presets/         # Game-specific preset configurations
│   ├── 📄 Application.log  # Application log file
│   └── 📄 StatusKeeper.log # StatusKeeper log file
├── 📁 Language/            # Multi-language support files
└── 📁 Assets/              # Application resources and icons
```

### Settings & Configuration
- **Per-Game Paths**: Custom XXMI and ModLibrary paths for each game
- **Themes**: Light, Dark, Auto with backdrop effects (Mica/MicaAlt require Windows 11, Acrylic/AcrylicThin/None work on Windows 10+)
- **Global Hotkeys**: Configurable shortcuts for optimize previews, reload manager, shuffle active mods, and deactivate all mods
- **Window State**: Automatic saving of size, position, and maximized state
- **Resolution Management**: 
  - **Real-time Display**: Shows current window size with automatic updates during resize
  - **Smart Validation**: Prevents invalid sizes (minimum 1300×720, maximum monitor resolution)
  - **Default Resolution Toggle**: Option to use fixed startup size instead of remembering last size
  - **Responsive Updates**: Window size fields update automatically with 200ms throttling for smooth performance

## Development

### Building from Source
1. **Prerequisites**:
   - Visual Studio 2022 with .NET 8 SDK
   - Windows App SDK 1.8
   - WinUI 3 project templates

2. **Clone and Build**:
   ```bash
   git clone [repository-url]
   cd "FlairX Mod Manager"
   dotnet restore
   dotnet build --configuration Release
   ```

3. **Dependencies**:
   - Microsoft.WindowsAppSDK (1.8.250907003)
   - CommunityToolkit.WinUI.Behaviors (8.2.250402)
   - CommunityToolkit.WinUI.Controls.Segmented (8.2.250402)
   - Microsoft.Graphics.Win2D (1.3.2)
   - NLua (1.7.5) - Lua scripting support
   - System.Drawing.Common (8.0.10)

### Architecture
- **Component-Based Design**: Modular architecture with specialized components
- **Core Components**:
  - **App**: Main application with startup logic, language detection, and mod library initialization
  - **MainWindow**: Split into partial classes (Navigation, EventHandlers, UIManagement, WindowManagement, Hotkeys)
  - **LoadingWindow**: Professional loading screen with progress indication
  - **PathManager**: Centralized path management with relative/absolute path handling
  - **SettingsManager**: Per-game configuration with JSON persistence
  - **SecurityValidator**: Path traversal protection and input sanitization
  - **SharedUtilities**: Common utilities including Win32 folder picker and UI helpers
  - **Logger**: Enhanced logging with line numbers and performance measurement
  - **ImageCacheManager**: Dual-layer caching system with automatic memory management
- **Multi-Game Support**: Game-agnostic architecture with isolated configurations per game
- **Dynamic Menu System**: Automatic category detection and menu generation from mod library structure
- **Security-First**: Comprehensive validation, NTFS checking, and safe file operations
- **Performance Optimized**: Smart memory management handling 900+ mods efficiently
- **Internationalization**: 15 languages with automatic font switching for different scripts

## Troubleshooting

### Common Issues
1. **Mods not activating**: Run as Administrator on NTFS drive
2. **XXMI not launching**: Verify XXMI portable version in correct directory
3. **Categories not appearing**: Place mods in category subdirectories (`ModLibrary/[Game]/[Category]/[ModName]/`)
4. **Missing thumbnails**: Ensure mod folders have files with "preview" in filename, run image optimization
5. **Preview images not showing**: Run "Optimize Preview Images" from Settings to process and rename images
6. **Navigation buttons missing**: Only appears when multiple optimized preview images exist
7. **Category thumbnails missing**: Place `catprev.*` or `preview.*` files in category directories, run image optimization
8. **Backdrop effects not working**: Mica/MicaAlt require Windows 11 (automatically disabled on Windows 10), Windows 10 1809+ supports Acrylic/AcrylicThin/None only
9. **Resolution fields showing red border**: Values below 1300×720 or above monitor resolution are invalid
10. **Window size not updating in settings**: Resolution fields auto-update when "Default resolution on start" is disabled

### Log Files
Check `Settings/` directory for:
- `Application.log` - Main application events and errors
- `StatusKeeper.log` - StatusKeeper synchronization events  
- `GridLog.log` - Mod grid operations (when grid logging enabled)

All logs include line numbers and performance measurements for debugging.

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Credits

### Author
- **[Jank8](https://github.com/Jank8)** - Main developer

### AI Assistance
- **[Kiro](https://kiro.dev/)** with **[GitHub Copilot](https://github.com/features/copilot)** - Development assistance

### Fonts
- **[Noto Fonts](https://notofonts.github.io/)** - Multi-language font support

### Special Thanks
- **[XLXZ](https://github.com/XiaoLinXiaoZhu)** for [source code](https://github.com/XiaoLinXiaoZhu/XX-Mod-Manager/blob/main/plugins/recognizeModInfoPlugin.js) contributions

### Additional Acknowledgments
- **Microsoft WinUI 3 and Windows App SDK** - UI framework
- **Game modding communities** - Feedback and testing support
- **XXMI Framework developers** - Mod injection system

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

---

*This application is not affiliated with miHoYo/HoYoverse or any of the supported games. It is a community-developed tool for managing game modifications.*

---

**Built entirely through AI-assisted development without prior C# knowledge! 🤖**

**Development Stack:**
- **[Kiro IDE](https://kiro.dev/)** - Primary AI coding assistant
- **[GitHub Copilot](https://github.com/features/copilot)** - Code completion and suggestions  
- **[Qoder](https://qoder.org/)** - Code fixes and optimizations

*Proof that modern AI tools can help anyone build complex, performant applications! 🚀*







