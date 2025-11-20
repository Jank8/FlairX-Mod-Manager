# FlairX Mod Manager

<img align="right" width="256" height="256" alt="appicon" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

A comprehensive mod management application for multiple miHoYo games, built with **WinUI 3** and **.NET 10**. This application provides an intuitive interface for managing, organizing, and activating game modifications across Zenless Zone Zero, Genshin Impact, Honkai Impact 3rd, Honkai: Star Rail, and Wuthering Waves.

**Built with modern Windows 11 Fluent Design** - Features native acrylic backgrounds, smooth animations, and responsive UI that scales efficiently to handle **900+ mods** using only **~900MB RAM**.




## Supported Games

- **Zenless Zone Zero (ZZZ)** - ZZMI
- **Genshin Impact (GI)** - GIMI  
- **Honkai Impact 3rd (HI)** - HIMI
- **Honkai: Star Rail (SR)** - SRMI
- **Wuthering Waves (WW)** - WWMI

## Key Features

### 🌐 GameBanana Integration
- **Browse & Search**: Explore thousands of mods directly from GameBanana without leaving the app
- **Infinite Scroll**: Automatic loading of more mods as you scroll down
- **One-Click Install**: Download and install mods with a single click
- **Smart Installation Dialog**:
  - Custom mod name and category selection
  - Download preview images option
  - Clean install (delete existing mod) option
  - Create backup before installation
  - Keep existing previews or combine with new ones
  - Automatic mod.json creation with author info and URL
- **Automatic Updates**: Check for and install mod updates seamlessly
- **Mod Details**: 
  - Full mod information with markdown rendering (ReverseMarkdown library)
  - Image carousel with multiple preview images
  - Author information with avatar and profile links
  - Statistics (downloads, likes, views)
  - Category information with icons
  - Date information (added, modified, updated)
- **Category Filtering**: Filter mods by game-specific categories
- **Direct Links**: Quick access to mod pages on GameBanana and author profiles
- **Archive Support**: Automatic extraction of compressed mod files (ZIP, RAR, 7Z) using SharpCompress
- **NSFW Content**: Optional blurring of NSFW thumbnails
- **Tilt Animations**: Smooth 3D tilt effects on mod tiles when hovering

### 🎮 Mod Management
- **Visual Mod Library**: Browse mods with thumbnails in a responsive, virtualized grid layout
- **Multiple View Modes**: 
  - **Mods View**: Grid layout with mod tiles and thumbnails
  - **Categories View**: Browse by category with category preview images
  - **Table View**: Detailed list view with sortable columns
- **Multiple Preview Images**: Support for up to 100 preview images per mod with navigation controls
- **Category Organization**: Automatic detection of categories (Characters, Weapons, UI, Effects) from mod library structure
- **Dynamic Menu**: Categories automatically appear in the sidebar menu for easy navigation
- **Mod Activation**: 
  - Toggle mods on/off with simple one-click activation
  - Simple heart icons (filled/empty) with system accent color when active
  - Security validation for directory names (path traversal protection)
  - Mod state persistence across sessions
- **Real-time Search**: Dynamic search functionality across all categories simultaneously
- **Sorting Options**: Sort by name (A-Z/Z-A), category, active status, last checked, last updated
- **Zoom Support**: 
  - Adjustable zoom levels for mod grid (100% to 250%)
  - Ctrl + Mouse Wheel for smooth zooming
  - ScaleTransform-based rendering for performance
  - 60 FPS throttling (16ms) for smooth animations
  - Persistent zoom level saved in settings
- **Context Menu**: 
  - Right-click menu with quick actions
  - Dynamic sorting (automatically switches to Table View):
    - Sort by name (A-Z / Z-A)
    - Sort by category (A-Z / Z-A)
    - Sort by last checked (newest / oldest)
    - Sort by last updated (newest / oldest)
  - Filter options:
    - Show outdated mods only
    - Show active mods only
  - Adaptive menu visibility based on current view mode
- **Multi-Game Support**: Switch between games with isolated mod libraries and configurations
- **Automatic Detection**: Smart detection of mod metadata and hotkeys from configuration files
- **Mod Details Panel**: Sliding panel with detailed mod information, multiple preview images, and metadata

### 🔧 Advanced Features
- **Preset System**: Save and load different mod configurations for each game with game-specific preset directories
- **XXMI Integration**: Built-in launcher for the XXMI mod injection framework with per-game configuration
- **Background Hotkey Detection**: Automatic detection and updating of mod hotkeys from INI files on startup and refresh
- **Hotkey Finder**: Automatic scanning of mod INI files to detect and display keyboard shortcuts
- **StatusKeeper Integration**: 
  - **Dynamic Synchronization**: 
    - Real-time mod state backup and restore
    - FileSystemWatcher for automatic d3dx_user.ini monitoring
    - Periodic timer (10-second intervals) for continuous sync
    - Background validation to avoid UI blocking
  - **Namespace Support**: Modern namespace-based mod synchronization alongside classic methods
  - **Backup System**: 
    - Create and restore .msk (ModStatusKeeper) backup files
    - Automatic backup before restore operations
    - Skip disabled INI files (files starting with "disabled")
    - Recursive directory scanning
  - **Logging**: 
    - Detailed logging of all synchronization operations
    - Real-time log viewer with auto-refresh (3-second intervals)
    - Newest entries displayed first
    - Toggle logging on/off from settings
- **GameBanana Author Updates**: 
  - **Smart Update**: Fetch mod author information from GameBanana URLs in mod.json files
  - **Date Fetching**: Automatically retrieve last checked and updated dates for mods
  - **Version Tracking**: Track mod versions from GameBanana
- **Mod Info Backup**: Backup and restore mod.json files and category preview images (up to 3 backups)
- **Functions System**: Enable/disable custom Lua script functions for extended functionality
- **Image Optimization**: Automatic optimization and conversion of preview images to standardized format
- **Multi-language Support**: 17 languages with automatic detection and specialized fonts for international scripts
- **Administrator Privileges**: Automatic elevation for file operations when needed

### 🚀 Performance Features
- **Optimized Memory Usage**: Efficiently handles **900+ mods** using only **~900MB RAM** (~1MB per mod)
- **Smart Image Caching**: 
  - Dual-layer caching system (disk + RAM)
  - ConcurrentDictionary for thread-safe access
  - LRU cleanup strategy
  - Automatic size estimation and tracking
  - Cache hit/miss logging
- **Background Processing**: 
  - Non-blocking thumbnail generation
  - Background mod data loading with volatile flags
  - Thread-safe JSON cache with timestamp validation
  - Task.Run for CPU-intensive operations
  - DispatcherQueue.TryEnqueue for UI updates
- **Instant Startup**: 
  - Fast application launch with LoadingWindow
  - Progressive mod loading (load visible items first)
  - Deferred hotkey detection (background Task.Run)
  - Lazy image loading (load on scroll)
- **Responsive Interface**: 
  - Smooth scrolling with virtualized GridView
  - Efficient search with cached menu items
  - 60 FPS zoom throttling (16ms)
  - Debounced window size updates (200ms)
- **Performance Monitoring**: 
  - Built-in performance measurement (Logger.MeasurePerformance)
  - Stopwatch-based timing
  - IDisposable pattern for automatic logging
  - Grid-specific logging toggle

### 🎨 User Interface
- **Modern Design**: WinUI 3 with Fluent Design System and smooth animations
- **Global Style System**:
  - Consistent 4px corner radius for buttons, comboboxes, navigation items
  - 8px corner radius for dialogs
  - Custom styles:
    - **CategoryAvatarNavigationViewItem**: Custom template with 32×32 avatar support
    - **TransparentButtonStyle**: Fully transparent buttons for overlays
    - **NoHoverTileButtonStyle**: Buttons without hover effects for tiles
    - **ActivateButtonStyle**: Animated activation button with scale effects:
      - Normal: 1.0 scale
      - Hover: 1.1 scale (0.1s duration)
      - Pressed: 0.95 scale (0.05s duration)
      - RenderTransformOrigin: center (0.5, 0.5)
- **Redesigned Settings**: Cleaner, more intuitive settings page with improved organization
- **Enhanced Mod Tiles**: 
  - Refined visual appearance with better spacing and readability
  - Heart icon converters:
    - BoolToHeartGlyphConverter: \uEB52 (filled) / \uEB51 (empty)
    - BoolToHeartColorConverter: SystemAccentColor when active / Gray when inactive
- **Theme Support**: 
  - Light, Dark, and Auto themes with system integration
  - Theme-specific AcrylicBrush definitions
  - Automatic font color adjustments
- **Backdrop Effects**: 
  - Mica and MicaAlt (Windows 11 only)
  - Acrylic and AcrylicThin (Windows 10 1809+)
  - None (solid background)
  - Automatic compatibility detection
- **Responsive Layout**: Adaptive interface with zoom support and window state persistence
- **Accessibility**: Multi-language font support with 8 font families:
  - Noto Sans (default)
  - Noto Sans SC (Chinese Simplified)
  - Noto Sans JP (Japanese)
  - Noto Sans KR (Korean)
  - Noto Sans Arabic
  - Noto Sans Hebrew
  - Noto Sans Devanagari (Hindi)
  - Noto Sans Thai
- **Loading Experience**: 
  - Professional loading screen (500×250px)
  - Minimalist design: Title + ProgressBar + Status
  - Grid layout with 24px spacing
  - Centered ProgressBar (60% width)
  - Backdrop effects matching main window
- **Loading Experience**: Professional loading screen with progress indication during startup

### 🛠️ Technical Features
- **Security Validation**: 
  - Path traversal protection and input sanitization
  - Reserved filename checking (CON, PRN, AUX, etc.)
  - URL validation for safe operations
  - Safe file operations with comprehensive error handling

- **Directory Management**: Automatic creation of required directories with proper structure (XXMI/ZZMI, GIMI, WWMI, SRMI, HIMI + ModLibrary subdirectories)
- **Dual-Layer Image Caching**: 
  - **Disk cache** for persistent storage (minitile.jpg files)
  - **RAM cache** for fast access during session
  - Automatic memory management with configurable size limits
  - LRU (Least Recently Used) cleanup strategy
  - Thread-safe ConcurrentDictionary implementation
  - Automatic size estimation (4 bytes per pixel RGBA)
  - Cache hit/miss logging for debugging
  - Unlimited cache size by default (configurable)
- **Advanced Logging System**:
  - Caller information tracking (file, method, line number)
  - Performance measurement with stopwatch
  - Grid-specific logging toggle
  - Thread-safe file operations
- **Global Hotkeys**: 
  - System-wide keyboard shortcuts using Win32 RegisterHotKey API
  - Work even when app is not focused or minimized to tray
  - Configurable hotkeys:
    - Optimize preview images (default: Ctrl+O)
    - Reload manager (default: Ctrl+R)
    - Shuffle active mods (default: Ctrl+S)
    - Deactivate all mods (default: Ctrl+D)
  - Toggle hotkeys on/off from settings
  - Automatic unregistration on app close
- **Smart Window Management**: 
  - Real-time resolution display with automatic updates (200ms throttling)
  - Intelligent validation (min 1300×720, max monitor resolution)
  - Window state persistence (size, position, maximized state)
  - Automatic centering on first launch or invalid position
  - On-screen position validation (prevents off-screen windows)
  - Default resolution toggle (use fixed startup size or remember last size)
  - Minimize to system tray support with Win32 tray icon
  - Double-click tray icon to restore window
  - Right-click tray menu (Show/Exit)
- **Post-Build Automation**: Automatic cleanup of unnecessary files (.xbf, createdump.exe) and directory structure creation
- **Backdrop System**: 
  - Dynamic backdrop effects with automatic fallback:
    - **Mica** (Windows 11 only) - Base material
    - **MicaAlt** (Windows 11 only) - Alternative base material
    - **Acrylic** (Windows 10 1809+) - Translucent blur effect
    - **AcrylicThin** (Windows 10 1809+) - Lighter blur effect
    - **None** - Solid background
  - Automatic compatibility detection (MicaController.IsSupported, DesktopAcrylicController.IsSupported)
  - SystemBackdropConfiguration for proper theme integration
  - Event-based updates when theme changes
  - Sliding panels inherit backdrop settings
- **Administrator Elevation**: Automatic privilege escalation for file operations when needed

## System Requirements

### Operating System
- **Windows 10** version 1809 (build 17763) or later
- **Windows 11** (recommended for best backdrop effects)
- **DPI Awareness**: PerMonitorV2 (automatic scaling on multi-monitor setups)
- **Compatibility**: Declared in app.manifest for unpackaged application support

### Required Software
- **.NET 10 Desktop Runtime** (automatically prompted if missing)
- **Windows App SDK 1.8** (automatically handled by Windows)
- **XXMI Framework** (Portable version recommended)

### File System & Permissions
- **Administrator privileges** may be requested for certain file operations
- Application works on any file system (NTFS, FAT32, exFAT)

### Hardware
- **RAM**: 2 GB minimum, 4 GB recommended (efficiently handles **900+ mods in ~900MB**)
- **Storage**: 500 MB for application, additional space for mod libraries
- **Display**: 1280x720 minimum resolution (supports up to 20000x15000)
- **Default Window Size**: 1650x820 pixels
- **Performance**: Excellent memory efficiency - approximately **1MB RAM per mod**

## Installation

### Prerequisites
1. Ensure your system meets the requirements above
2. Install .NET 10 Desktop Runtime from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/10.0)
3. Install latest stable Windows App SDK from [Microsoft's official site](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)

### Installation Steps
1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive to your desired location (preferably on an NTFS drive)
3. Run `FlairX Mod Manager Launcher.exe`
   - **Launcher Features**:
     - Console window hiding (GetConsoleWindow + ShowWindow Win32 API)
     - Single-file executable (self-contained, no runtime required)
     - AppContext.BaseDirectory for single-file compatibility
     - Silent fail (no error dialogs)
     - Automatic working directory setup
     - Launches `app\FlairX Mod Manager.exe`
4. The application will automatically request administrator privileges and install missing dependencies

### First Run Setup
1. **Game Selection**: Choose your game from the dropdown menu
2. **XXMI Integration**: Ensure XXMI portable version is extracted to the manager folder
3. **Mod Library**: Place your mods in category folders within the game's ModLibrary directory

## Usage

### Getting Started
1. **Select Game**: Choose your target game from the dropdown menu
2. **Add Mods**: Place mod folders in category directories: `XXMI/[GameTag]/Mods/[Category]/[ModName]/`
3. **Browse & Activate**: Use category menu items to browse, then click heart icons to activate mods
4. **Launch**: Use the floating action button to launch XXMI with active mods

### Key Features
- **Category Navigation**: Browse mods by auto-detected categories (Characters, Weapons, UI, etc.)
- **Cross-Category Search**: Search across all categories simultaneously
- **View Modes**: Switch between Mods, Categories, and Table views
- **Sorting & Filtering**: Multiple sort options and real-time search
- **Presets**: Save and load different mod configurations per game
- **StatusKeeper**: 
  - Backup/restore mod states with automatic game-specific path detection
  - Dynamic synchronization with namespace support
  - Up to 3 backup snapshots
- **GameBanana Browser**: Browse, search, and install mods directly from GameBanana
- **Mod Info Backup**: Backup and restore mod.json files and category previews
- **Functions**: Enable/disable custom Lua script functions
- **Global Hotkeys**: System-wide keyboard shortcuts for common operations

## Quick Start Guide

### Setup
1. **Extract and run** `FlairX Mod Manager Launcher.exe`
2. **Download XXMI** portable version when prompted, extract to `app/XXMI/`
3. **Select your game** from the dropdown menu

### Installing Mods
1. **Download** mods from GameBanana/NexusMods
2. **Extract** to: `app/XXMI/[GameTag]/Mods/[Category]/[ModName]/`
   - Example: `app/XXMI/GIMI/Mods/Characters/Ayaka_Outfit/`
3. **Add preview images** (optional): Include `preview*.png` or `preview*.jpg` files for thumbnails
4. **Click reload** (↻) to detect new mods
5. **Activate** by clicking heart icons

### Directory Structure Example
```
📁 app/XXMI/GIMI/
├── 📁 Mods/
│   ├── 📁 Characters/
│   │   ├── 📄 catprev.jpg (auto-generated category preview - 600x600)
│   │   └── 📁 Ayaka_Outfit/
│   │       ├── 📄 mod.json (auto-created)
│   │       ├── 📄 preview.jpg (main preview)
│   │       ├── 📄 preview-01.jpg (additional preview)
│   │       ├── 📄 preview-02.jpg (additional preview)
│   │       ├── 📄 minitile.jpg (auto-generated thumbnail)
│   │       └── 📁 mod files...
│   ├── 📁 Weapons/
│   │   ├── 📄 catprev.jpg (auto-generated category preview)
│   │   └── 📁 weapon mods...
│   └── 📁 UI/
│       ├── 📄 catprev.jpg (auto-generated category preview)
│       └── 📁 ui mods...
└── 📄 d3dx_user.ini (XXMI configuration)
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
├── 📁 XXMI/
│   ├── 📁 ZZMI/            # Zenless Zone Zero
│   │   ├── 📁 Mods/        # All mods stored here
│   │   │   ├── 📁 Characters/  # Character mods
│   │   │   ├── 📁 Weapons/     # Weapon mods
│   │   │   ├── 📁 UI/          # UI mods
│   │   │   └── 📁 Other/       # Other mods
│   │   └── 📄 d3dx_user.ini # Game configuration
│   ├── 📁 GIMI/            # Genshin Impact
│   │   ├── 📁 Mods/
│   │   └── 📄 d3dx_user.ini
│   ├── 📁 HIMI/            # Honkai Impact 3rd
│   ├── 📁 SRMI/            # Honkai: Star Rail
│   └── 📁 WWMI/            # Wuthering Waves
├── 📁 Settings/
│   ├── 📄 Settings.json    # Main application settings
│   ├── 📁 Presets/         # Game-specific preset configurations
│   │   ├── 📁 ZZ/          # Zenless Zone Zero presets
│   │   ├── 📁 GI/          # Genshin Impact presets
│   │   ├── 📁 HI/          # Honkai Impact 3rd presets
│   │   ├── 📁 SR/          # Honkai: Star Rail presets
│   │   └── 📁 WW/          # Wuthering Waves presets
│   ├── 📄 Application.log  # Application log file
│   └── 📄 StatusKeeper.log # StatusKeeper log file
├── 📁 Language/            # Multi-language support files
└── 📁 Assets/              # Application resources and icons
```

### Settings & Configuration
- **Per-Game Paths**: Custom XXMI paths for each game (mods stored in XXMI/[GameTag]/Mods)
- **Themes**: Light, Dark, Auto with backdrop effects (Mica/MicaAlt require Windows 11, Acrylic/AcrylicThin/None work on Windows 10+)
- **Global Hotkeys**: Configurable shortcuts for optimize previews, reload manager, shuffle active mods, and deactivate all mods
- **Window State**: Automatic saving of size, position, and maximized state
- **Resolution Management**: 
  - **Real-time Display**: Shows current window size with automatic updates during resize
  - **Smart Validation**: Prevents invalid sizes (minimum 1280×720, maximum monitor resolution)
  - **Default Resolution Toggle**: Option to use fixed startup size instead of remembering last size
  - **Responsive Updates**: Window size fields update automatically with 200ms throttling for smooth performance

## Development

### Building from Source
1. **Prerequisites**:
   - Visual Studio 2022 with .NET 10 SDK
   - Windows App SDK 1.8
   - WinUI 3 project templates

2. **Clone and Build**:
   ```bash
   git clone [repository-url]
   cd "FlairX Mod Manager"
   dotnet restore
   dotnet build --configuration Release
   ```

3. **Build Launcher (Single File)**:
   ```bash
   dotnet publish "FlairX-Mod-Manager Launcher/FlairX Mod Manager Launcher.csproj" -c Release -p:PublishSingleFile=true -r win-x64 --self-contained true
   ```
   Output: `FlairX-Mod-Manager Launcher\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\FlairX Mod Manager Launcher.exe`
   
   **Launcher Configuration**:
   - Self-contained: true (includes .NET runtime)
   - PublishSingleFile: true (single executable)
   - PublishTrimmed: true (reduced size)
   - EnableCompressionInSingleFile: true (compressed)
   - IncludeNativeLibrariesForSelfExtract: true
   - IncludeAllContentForSelfExtract: true
   - DebugType: none (no .pdb file)
   - DebugSymbols: false

4. **Dependencies**:
   - Microsoft.WindowsAppSDK (1.8.251106002)
   - CommunityToolkit.Common (8.2.1)
   - CommunityToolkit.WinUI.Behaviors (8.2.250402)
   - CommunityToolkit.WinUI.Controls.Segmented (8.2.250402)
   - CommunityToolkit.Labs.WinUI.Controls.MarkdownTextBlock (0.1.251030-build.2370)
   - Microsoft.Graphics.Win2D (1.3.2)
   - ReverseMarkdown (4.7.1)
   - SharpCompress (0.41.0)
   - NLua (1.7.5) - Lua scripting support
   - System.Drawing.Common (8.0.10)

### Architecture
- **Component-Based Design**: Modular architecture with specialized components and partial classes for maintainability
- **Core Components**:
  - **App**: Main application with startup logic, automatic language detection, font switching, and mod library initialization
  - **MainWindow**: Split into 6 partial classes for better organization:
    - **Navigation**: Page navigation, menu management, search functionality
    - **EventHandlers**: 
      - MainRoot_Loaded: Auto-detect hotkeys on startup (background Task.Run)
      - ReloadModsButton_Click: Full reload sequence:
        - Show LoadingWindow
        - Clear JSON cache
        - Update mod.json files with namespace info
        - Preload images
        - Regenerate menu

        - Restore last position
        - Auto-detect hotkeys
      - GameSelectionComboBox_SelectionChanged: Game switching with validation
      - NvSample_Loaded: Cache menu items for search functionality
    - **UIManagement**: UI state updates, menu generation, game selection
    - **WindowManagement**: Window sizing, positioning, state persistence, backdrop effects
    - **Hotkeys**: 
      - MainWindow_KeyDown: Local hotkey handler
      - Modifier key detection (Ctrl, Shift, Alt)
      - Hotkey string building (e.g., "Ctrl+O")
      - ExecuteOptimizePreviewsHotkey:
        - Check if on settings page
        - Show progress if window in focus
        - Silent execution if window not focused
      - ExecuteReloadManagerHotkey: Trigger full reload
      - ExecuteShuffleActiveModsHotkey: Random mod activation
      - ExecuteDeactivateAllModsHotkey: Deactivate all mods
      - IsWindowInFocus: GetForegroundWindow API check
    - **SlidingPanels**: Sliding panel animations, theme updates, panel management
  - **LoadingWindow**: 
    - Professional loading screen with progress indication
    - Backdrop effects matching main window settings
    - Windows 10/11 compatibility detection
    - Automatic fallback (Mica/MicaAlt → AcrylicThin on Windows 10)
    - Centered window (500×250px)
    - UpdateStatus method for real-time feedback
    - SetProgress for determinate progress (0-100%)
    - SetIndeterminate toggle for indeterminate mode
  - **PathManager**: 
    - Centralized path management with relative/absolute path handling
    - System directory detection (prevents writing to Program Files, System32)
    - Safe fallback to Documents folder when needed
    - Path validation and sanitization
  - **SettingsManager**: 
    - Per-game configuration with JSON persistence
    - Game-specific paths (XXMI root, ModLibrary, Presets)
    - Default restoration with game-aware defaults
    - Active mods tracking per game
  - **SecurityValidator**: 
    - Path traversal protection (blocks "..", "/", "\")
    - Reserved name checking (CON, PRN, AUX, COM1-9, LPT1-9)
    - Input sanitization for file operations
    - URL validation (http/https only)
    - Length limits (255 characters)
  - **SharedUtilities**: 
    - Win32 folder picker (SHBrowseForFolder)
    - NTFS validation (DriveInfo.DriveFormat)
    - Breadcrumb bar helpers
    - Language dictionary loading
    - Safe file opening (Process.Start with UseShellExecute)
  - **Logger**: 
    - Enhanced logging with caller information:
      - CallerMemberName attribute (method name)
      - CallerFilePath attribute (file name)
      - CallerLineNumber attribute (line number)
    - Log levels: INFO, WARNING, ERROR, PERF, DEBUG
    - Performance measurement:
      - MeasurePerformance returns IDisposable
      - Stopwatch-based timing
      - Automatic logging on dispose
    - Grid-specific logging:
      - LogGrid method with toggle
      - Respects GridLoggingEnabled setting
    - Thread-safe file operations:
      - Static lock object
      - File.AppendAllText with lock
    - Dual output:
      - Debug.WriteLine for console
      - File logging to Settings/Application.log
    - Formatted messages: [timestamp] [level] [file.method:line] message
  - **ImageCacheManager**: 
    - Dual-layer caching system:
      - **Disk cache**: minitile.jpg files (600×600px)
      - **RAM cache**: BitmapImage objects in memory
    - ConcurrentDictionary<string, CacheEntry> for thread safety
    - CacheEntry class with:
      - BitmapImage reference
      - LastAccessed timestamp (DateTime)
      - SizeBytes (long)
    - Automatic size estimation (width × height × 4 bytes RGBA)
    - LRU cleanup when threshold exceeded
    - Interlocked.Add for thread-safe size tracking
    - Cache hit/miss logging for debugging
    - Unlimited cache size by default (MAX_CACHE_SIZE_BYTES = long.MaxValue)
    - GetCachedImage/CacheImage for disk cache
    - GetCachedRamImage/CacheRamImage for RAM cache
  - **GlobalHotkeyManager**: 
    - Win32 RegisterHotKey/UnregisterHotKey API
    - Virtual key code mapping (VK_A-Z, VK_0-9, VK_F1-F12)
    - Modifier keys (MOD_CONTROL, MOD_ALT, MOD_SHIFT, MOD_WIN)
    - WM_HOTKEY message handling (0x0312)
    - Automatic cleanup on dispose
    - Hotkey action dictionary (Func<Task> delegates)
    - Focus detection (GetForegroundWindow API)
    - Silent execution when window not focused
    - Local hotkeys (KeyDown event) + Global hotkeys (Win32 API)
  - **GameBananaService**: 
    - RESTful API integration (HttpClient with 15s timeout)
    - Game ID mapping (ZZMI: 19567, GIMI: 8552, etc.)
    - JSON deserialization with JsonPropertyName attributes
    - Mod list pagination support
    - Metadata extraction from nested JSON
  - **WindowStyleHelper**: 
    - Centralized theme and backdrop management
    - Event-based updates (SettingsChanged event)
    - SystemBackdropTheme detection
    - Title bar customization (ExtendsContentIntoTitleBar)
    - Button color customization per theme
- **Pages & User Controls**:
  - **ModGridPage**: Main mod management split into 7 partial classes:
    - **Main**: Core logic, view modes (Mods/Categories/Table), tile rendering
    - **ModOperations**: Activation/deactivation, symlink creation, deletion, folder operations
    - **ContextMenu**: Right-click menu, sorting, filtering, dynamic visibility
    - **DataLoading**: Category loading, mod loading, minitile loading, data management
    - **Loading**: 
      - Background loading with volatile flags and lock objects
      - Thread-safe JSON cache with timestamp validation
      - Progressive loading with status updates
      - Thumbnail generation in background
      - File.GetLastWriteTime for cache invalidation
      - StartBackgroundLoadingIfNeeded with race condition prevention
    - **Navigation**: Category navigation, back button, folder opening
    - **Zoom**: ScaleTransform-based zooming, Ctrl+Wheel handling, 60 FPS throttling
    - **StaticUtilities**: 
      - RecreateSymlinksFromActiveMods (cleanup old + create new)
      - ApplyPreset (load JSON + recreate symlinks)
      - ClearJsonCache (thread-safe cache management)
      - FindModFolderPathStatic (recursive mod search)
      - GetOptimalImagePathStatic (minitile.jpg lookup)
      - IsSymlinkStatic (FileAttributes.ReparsePoint check)
  - **GameBananaBrowserUserControl**: 
    - GameBanana API integration with infinite scroll
    - Markdown rendering (ReverseMarkdown + MarkdownTextBlock)
    - Tilt animations on hover (3D transform)
    - Image carousel for mod details
    - NSFW blur toggle
  - **SettingsUserControl**: 
    - Comprehensive settings with BreadcrumbBar path pickers
    - Real-time validation (resolution, paths)
    - Window size update timer (200ms throttling)
    - Image optimization with progress tracking
    - Theme and backdrop selection
  - **StatusKeeperPage**: Mod state synchronization with 3 sub-pages:
    - **StatusKeeperSyncPage**: 
      - FileSystemWatcher for d3dx_user.ini monitoring
      - Periodic sync timer (10-second intervals)
      - Manual sync button
      - Backup confirmation toggle
    - **StatusKeeperBackupPage**: 
      - Create .msk backup files
      - Restore from backups with confirmation
      - Delete all backups
      - Check backup status
    - **StatusKeeperLogsPage**: 
      - Real-time log viewer (3-second auto-refresh)
      - Newest entries first (reversed display)
      - Toggle logging on/off
      - Open log in default editor
      - Clear log button
  - **PresetsUserControl**: 
    - Preset management with game-specific storage
    - Save/load/delete presets
    - Slide-in animation (300px from right, 400ms duration)
  - **FunctionsUserControl**: 
    - Lua script function management
    - Enable/disable individual functions
    - Function list with toggle switches
  - **ModDetailUserControl**: 
    - Detailed mod information panel
    - Image carousel with left/right navigation
    - Multiple preview support (up to 100 images)
    - Image counter (e.g., "3 / 7")
  - **GBAuthorUpdatePage**: 
    - GameBanana URL parsing (regex pattern)
    - Author information fetching
    - Date fetching (last checked, last updated)
    - Version tracking
    - Smart update toggle
    - Progress tracking with cancellation support
  - **ModInfoBackupPage**: 
    - Backup mod.json files and category previews
    - Up to 3 backup slots
    - Restore with confirmation
    - Delete individual backups
    - Backup info display (date, size)
  - **HotkeyFinderPage**: 
    - Automatic INI file scanning (recursive)
    - Hotkey detection from [Key] sections
    - Regex parsing for key assignments
    - Background detection on startup and reload
    - Skip disabled INI files
  - **WelcomePage**: 
    - First-run welcome screen
    - Game selection prompt
    - Localized welcome text
    - Shown when no game is selected (SelectedGameIndex = 0)
- **Dialogs**:
  - **GameBananaFileExtractionDialog**: 
    - Archive extraction with SharpCompress (ZIP, RAR, 7Z)
    - Multi-file selection support (ObservableCollection<GameBananaFileViewModel>)
    - Custom mod name and category input (TextBox controls)
    - Installation options (CheckBox toggles):
      - Download preview images from GameBanana
      - Clean install (delete existing mod)
      - Create backup before installation
      - Keep existing previews
      - Combine previews with new ones
    - Progress tracking:
      - Download progress (ProgressBar with percentage)
      - Extraction progress (file count)
      - Status text updates
    - Automatic mod.json creation with metadata:
      - Author name and URL
      - GameBanana mod ID
      - Date updated timestamp
      - Preview media URLs
    - Update detection for existing mods (shows update options grid)
  - **GameBananaUpdateDialog**: 
    - Mod update management with file selection
    - Version comparison (gbChangeDate vs dateUpdated)
    - ListView with FileViewModel items
    - Multi-file selection support
    - Download + extraction with progress
    - Update confirmation dialog
    - Automatic backup before update
    - mod.json metadata update
- **Multi-Game Support**: 
  - Game-agnostic architecture with isolated configurations
  - Per-game paths (XXMI root, ModLibrary, Presets, ActiveMods)
  - Game ID mapping for GameBanana API:
    - ZZMI: 19567 (Zenless Zone Zero)
    - GIMI: 8552 (Genshin Impact)
    - HIMI: 10349 (Honkai Impact 3rd)
    - WWMI: 20357 (Wuthering Waves)
    - SRMI: 18386 (Honkai Star Rail)
  - Automatic directory structure creation per game
  - Game-specific ActiveMods files (ZZ-ActiveMods.json, GI-ActiveMods.json, etc.)
  - Symlink recreation on game switch
- **Dynamic Menu System**: Automatic category detection and menu generation from mod library structure
- **Security-First**: 
  - Comprehensive validation, NTFS checking, and safe file operations
  - No TODO/FIXME/HACK comments in codebase (clean, production-ready code)
  - Consistent error handling throughout
  - Administrator privilege elevation when needed
- **Performance Optimized**: Smart memory management handling 900+ mods efficiently (~1MB per mod)
- **Internationalization**: 16 languages with modular structure and automatic font switching:
  - **Languages**: English, Spanish, French, Hindi, Japanese, Korean, Polish, Portuguese (BR/PT), Russian, Thai, Tagalog, Turkish, Vietnamese, Chinese (Simplified/Traditional)
  - **Modular Structure**: 
    - Main language files (en.json, es.json, etc.)
    - Feature-specific subfolders (GameBananaBrowser, GBAuthorUpdate, ModInfoBackup, StatusKeeper)
    - Separate translations for each feature module
  - **Automatic Detection**: 
    - System language detection via CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
    - Fallback to English if language not available
    - First-run auto-detection with persistent settings
  - **Font Switching**: 
    - **Noto Sans CJK**: Chinese (zh-CN, zh-TW), Japanese (ja-JP), Korean (ko-KR)
    - **Noto Sans Arabic**: Arabic (ar-SA)
    - **Noto Sans Hebrew**: Hebrew (he-IL)
    - **Noto Sans Devanagari**: Hindi (hi-IN)
    - **Noto Sans Thai**: Thai (th-TH)
    - **Noto Sans**: Default for Latin scripts
  - **Translation Loading**: SharedUtilities.LoadLanguageDictionary with subfolder support

## Troubleshooting

### Common Issues
1. **Mods not activating**: 
   - Run as Administrator on NTFS drive
   - Check if symbolic links are supported (NTFS validation)
   - Verify mod directory names don't contain invalid characters
2. **XXMI not launching**: 
   - Verify XXMI portable version in correct directory
   - Check launcher path: `XXMI\Resources\Bin\XXMI Launcher.exe`
   - Ensure working directory is set correctly
3. **Categories not appearing**: 
   - Place mods in category subdirectories (`ModLibrary/[Game]/[Category]/[ModName]/`)
   - Reload manager (Ctrl+R or reload button)
   - Check if game is selected in dropdown
4. **Missing thumbnails**: 
   - Ensure mod folders have files with "preview" in filename
   - Run image optimization from Settings
   - Check if minitile.jpg was generated (600×600px)
5. **Preview images not showing**: 
   - Run "Optimize Preview Images" from Settings to process and rename images
   - Check if preview.jpg exists (main preview)
   - Verify image format (PNG/JPG supported)
6. **Navigation buttons missing**: 
   - Only appears when multiple optimized preview images exist
   - Check for preview-01.jpg, preview-02.jpg, etc.
7. **Category thumbnails missing**: 
   - Place `catprev.*` or `preview.*` files in category directories
   - Run image optimization
   - Check if catprev.jpg was generated (600×600px)
8. **Backdrop effects not working**: 
   - Mica/MicaAlt require Windows 11 (build 22000+)
   - Automatically disabled on Windows 10
   - Windows 10 1809+ supports Acrylic/AcrylicThin/None only
   - Check Windows version: `winver` command
9. **Resolution fields showing red border**: 
   - Values below 1300×720 or above monitor resolution are invalid
   - Check monitor resolution in display settings
10. **Window size not updating in settings**: 
    - Resolution fields auto-update when "Default resolution on start" is disabled
    - 200ms throttling for smooth updates
11. **Launcher not starting main app**:
    - Check if `app\FlairX Mod Manager.exe` exists
    - Verify launcher is in correct directory structure
    - Run launcher as Administrator if needed
12. **Global hotkeys not working**:
    - Check if hotkeys are enabled in Settings
    - Verify no other application is using the same hotkey
    - Ensure a game is selected (hotkeys disabled when no game selected)
13. **High DPI scaling issues**:
    - Application uses PerMonitorV2 DPI awareness
    - Check Windows display scaling settings
    - Restart application after changing DPI settings

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







