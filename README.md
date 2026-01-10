# FlairX Mod Manager

<img align="right" width="256" height="256" alt="FlairX Mod Manager Logo" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

**The ultimate mod manager for XXMI-supported games**

FlairX Mod Manager is a powerful, feature-rich application designed specifically for managing game modifications across all major games supported by XXMI frameworks. Built with .NET 10 and WinUI 3, it offers professional-grade mod management with an intuitive interface that scales from casual users to power modders.

✨ **Key Highlights:**
- Supports 1100+ mods with optimized performance
- One-click GameBanana integration with Cloudflare support
- **Screenshot capture system** with cropping and preview management
- **Author mod search** - find all mods by specific creators
- Advanced image optimization with AI-powered cropping
- Game overlay with full controller support
- 16 languages with specialized font support
- **One-click updates** for FlairX Mod Manager (popup notification when available)
- **Automatic XXMI framework download** and installation
- **Automatic starter pack installation** for all supported games

## 🎮 Supported Games

FlairX Mod Manager works seamlessly with all games supported by XXMI frameworks:

| Game | Framework | Status |
|------|-----------|--------|
| **Zenless Zone Zero** | ZZMI | ✅ Fully Supported |
| **Genshin Impact** | GIMI | ✅ Fully Supported |
| **Honkai Impact 3rd** | HIMI | ✅ Fully Supported |
| **Honkai: Star Rail** | SRMI | ✅ Fully Supported |
| **Wuthering Waves** | WWMI | ✅ Fully Supported |

## 💻 System Requirements

**Minimum Requirements:**
- **Windows 11** (recommended) or Windows 10 22H2
- 4GB RAM (8GB recommended)
- 1280×720 display resolution
- 500MB free disk space

**Automatic Setup:**
- .NET 10 and Windows App SDK may require manual installation if Windows doesn't prompt automatically
- XXMI Framework is installed automatically if you confirm the installation prompt
- No administrator rights required for normal operation

⚠️ **Note**: Windows 11 is recommended for the best experience and latest security updates. **Microsoft ended support for Windows 10 in October 2025** - upgrade is strongly recommended for security reasons.

## 📥 Installation

1. **Download** the latest release from the releases page
2. **Extract** the files to your desired location
3. **Run** `FlairX Mod Manager Launcher.exe`
4. **Select** your game and start managing mods

**Everything else is automatic:**
- FlairX will prompt for required components if needed (.NET 10, Windows App SDK)
- XXMI frameworks are installed automatically when you confirm the installation prompt
- Essential starter packs are installed with one click
- Updates are available with one-click installation when detected

## 🚀 Quick Start Guide

### Adding Mods

**Option 1: GameBanana Browser (Recommended)**
1. Click the **"Browse GameBanana"** button in the main interface
2. Browse mod cards showing preview images, author info, and statistics
3. **Search by author**: Use `user:exampleuser` to find all mods by specific creators
4. **View author collections**: Click "View All Author's Mods" on mod detail pages
5. Complete reCAPTCHA verification if prompted
6. Click **"Download and Install"** or **"Download and Update"** on the mod card
7. Configure installation options (previews, backups, etc.)
8. Click **"Start"** to begin download and extraction
9. **Mod library reloads automatically** after installation
10. **Click the mod tile footer** to activate

**Option 2: Manual Installation**
1. Download mods from GameBanana or other sources
2. **Create a category folder** if it doesn't exist (e.g., Characters, Weapons, UI)
3. Extract to: `XXMI/[GameTag]/Mods/[Category]/[ModName]/`
   - Example: `XXMI/GIMI/Mods/Characters/Ayaka/`
   - **Category folders are created manually or during GameBanana installation**
4. Click the **reload button** (↻) in FlairX
5. **Click the mod tile footer** to activate

### Using the Game Overlay

**Access During Gameplay:**
- Press **Alt+W** (or your custom hotkey) to open the overlay
- Use **Xbox controller**: Back+Start buttons simultaneously
- Navigate with **D-Pad** or **Left Stick** (if enabled)
- **A button** to toggle mods, **B button** to close

**Overlay Features:**
- Quick mod activation/deactivation without leaving your game
- Category browsing with **LB/RB** shoulder buttons
- Filter active mods only with **Back+A**
- Always stays on top of games
- Customizable size, position, theme, and backdrop effects

### Managing Preview Images

**Adding Images:**
- **Screenshot Capture**: Use the plus (+) button on mod detail pages to capture screenshots with cropping
- **Automatic**: GameBanana mods include optimized previews automatically
- **Drag & Drop**: Select up to 100 images and drag onto any mod tile
- **Drag & Drop (Category)**: Drag a single image onto category tiles to set category preview
- **Manual**: Place images in mod folder, then run "Optimize Previews" from Functions menu

**Screenshot Capture Workflow:**
1. **Open mod details** and click the plus (+) icon next to the trash button
2. **Capture screenshots** using the built-in capture interface
3. **Crop and adjust** captured images with interactive cropping tools
4. **Add to previews** or cancel to automatically clean up temporary files
5. **Integration**: Captured images are automatically optimized and added to mod gallery

**Image Optimization Results:**
- **Mod Images**: `preview.jpg`, `preview-01.jpg`, etc. (1000×1000px in Standard mode)
- **Mod Thumbnail**: `minitile.jpg` (600×722px for fast grid loading)
- **Category Preview**: `catprev.jpg` (600×600px for hover popup)
- **Category Tile**: `catmini.jpg` (600×722px for category grid)

## 🚀 Core Features

### 🎯 **Mod Management**
- **Visual Mod Tiles**: Large preview images (277×333px) with mod names displayed in footer bars
- **One-Click Activation**: Toggle mods on/off by clicking the footer area of mod tiles
- **Smart Activation System**: Mods are activated by removing the `DISABLED_` prefix from folder names, allowing instant on/off switching
- **Category Navigation**: Left sidebar with expandable categories and "All Mods" button
- **Game Selection**: Top dropdown for switching between supported games (GIMI, HIMI, SRMI, WWMI, ZZMI)
- **Advanced Search**: Dynamic search with real-time filtering across all mods
- **Multiple View Modes**: Switch between Mods grid, Categories grid, and Table view
- **Duplicate Detection**: Identify and manage duplicate mods across categories
- **Recycle Bin Deletion**: Safe mod removal with recovery option
- **Broken mods tagging**: Tag broken mods that will allow to filter and get visual feedback

**Mod Tile Features:**
- **Footer Activation**: Click the footer area of any mod tile to toggle activation on/off
- **Hover Effects**: Activation button with "Activate"/"Deactivate" text appears on hover over the footer
- **Visual Status**: Active mods show accent color footer, inactive mods show acrylic background
- **Update Indicators**: Orange dot in top-left corner for mods with available updates
- **Drag & Drop Support**: Drop images directly onto mod tiles to add preview images
- **Context Menus**: Right-click for mod-specific actions (activate/deactivate, open folder, view details, open URL, copy name, rename, delete) and sorting options
- **Category Context Menu**: Right-click categories for folder access, copy name, and rename options
- **Sorting Context Menu**: Right-click empty space for sorting options (by name, category, date) and filtering (active mods, outdated mods)

**Detailed Mod Information:**
- **Preview Gallery**: Navigate through multiple mod images with arrow controls
- **Verification Tracking**: View when mods were last checked and verified
- **Update Monitoring**: Track last update dates and author changes
- **Author Information**: Direct links to mod creators and GameBanana pages
- **NSFW Content Control**: Per-mod adult content marking and filtering
- **StatusKeeper Integration**: Individual mod sync control with game launcher

**Individual Mod Hotkeys:**
- **Component-Specific Keys**: Assign unique hotkeys to different mod parts (color, hair, accessories, etc.)
- **Flexible Assignment**: Use any key combination (Ctrl+Number, single keys, etc.)
- **Visual Management**: Edit, reset, and toggle individual hotkey assignments
- **Real-Time Control**: Instantly switch mod components during gameplay

### 🌐 **GameBanana Integration**
- **Built-in Browser**: Browse and search GameBanana directly within the app
- **Author Search**: Find all mods by specific creators using `user:exampleuser` syntax
- **Author Mod Collections**: View complete mod portfolios with game filtering
- **Advanced Cloudflare Support**: Automatic handling of Cloudflare protection with intelligent cookie management and bypass system
- **Smart Mod Cards**: Rich mod tiles showing preview images, author, dates, and statistics
- **Installation Status**: Clear visual indicators (green "Installed" badges) for downloaded mods
- **NSFW Content Control**: Advanced filtering system with blur options for adult content
- **Metadata Display**: Complete mod information including upload/update dates, views (66.9K), and download counts (672)

**Advanced Download System:**
- **Flexible Installation Options**: Choose between full install or previews-only download
- **Preview Management**: Download, keep, or combine preview images with existing ones
- **Clean Install Mode**: Remove existing mod files before installing updates
- **Backup Creation**: Automatic backup of existing mods before updates
- **Manual Category Selection**: Choose appropriate category during installation
- **Progress Tracking**: Real-time download and extraction progress bars
- **File Information**: Detailed file data (size: 154.88 MB, downloads: 5.5K, date added)
- **Dual Action Buttons**: "Download and Update" for full installation, "Open in Browser" for web access

**GameBanana Update System:**
- **Author Update Checking**: Toggle to update all mod authors from GameBanana database
- **Smart Update Mode**: Option to only update mods with unknown or missing authors
- **Invalid URL Handling**: Skip mods that have been removed or have invalid URLs
- **Batch Operations**: Multiple update actions available:
  - **Fetch Authors**: Retrieve and update author information from GameBanana
  - **Fetch Dates**: Update publication/update dates for all mods
  - **Fetch Versions**: Get latest version information for installed mods
  - **Fetch All Previews**: Download preview images for all mods
  - **Fetch Missing Previews**: Download previews only for mods without preview images

### 🎨 **Advanced Image System**
- **Screenshot Capture**: Built-in screenshot capture with cropping and preview management
- **Multiple Optimization Modes**:
  - **Standard**: Quality optimization + thumbnail generation + auto-crop + manual crop support
  - **CategoryFull**: Manual crop inspection for category previews

- **AI-Powered Cropping**:
  - **Center Crop**: Traditional center-based cropping
  - **Smart Crop**: Edge detection for optimal framing
  - **Entropy Crop**: Focus on areas with highest detail
  - **Attention Crop**: Detect faces and important visual elements
  - **Manual Crop**: Interactive adjustment with live preview

- **Image Processing**:
  - Support for up to 100 images per mod
  - Automatic thumbnail generation (minitile, catprev, catmini)
  - Configurable JPEG quality (1-100%)
  - Multi-threaded batch processing
  - Original file preservation option
  - Drag & drop image addition with automatic optimization

**Screenshot Capture System:**
- **Capture Button**: Plus (+) icon on mod detail pages for quick screenshot capture
- **Cropping Interface**: Interactive cropping with live preview and adjustment handles
- **Preview Management**: Add captured screenshots to mod preview galleries
- **Cancellation Safety**: Automatic cleanup of captured files when cancelled
- **Integration**: Seamlessly integrates with existing image optimization pipeline

### 🖼️ **Image Optimizer Functions**
**Advanced Cropping Options:**
- **Image Crop Type**: Choose cropping method (Center, Smart, Entropy, Attention)
- **Smart Crop**: AI-powered edge detection for optimal framing
- **Entropy Crop**: Focus on areas with highest visual detail
- **Attention Crop**: Automatically detect faces and important visual elements
- **Inspect and Edit**: Enable manual cropping with live preview and frame editing
- **Auto-create Mod Thumbnails**: Skip manual selection and use first preview with selected crop mode

**Multi-threaded Processing:**
- **Parallel Threads**: Configure processing threads (31 default) for faster optimization on multi-core systems
- **Batch Processing**: Handle up to 100 images per mod simultaneously
- **Quality Control**: Adjustable JPEG quality (1-100%) with 80% default for optimal size/quality balance

**Optimization Scenarios:**
- **Manual Optimization**: Process all mod and category images with "Start" button
- **Drag & Drop Mod**: Automatically optimize images dropped onto mod tiles
- **Drag & Drop Category**: Auto-enable Inspect and Edit for category tiles (722×722 square and 600×722 rectangle)
- **GameBanana Download**: Automatic optimization for downloaded mod images

**Backup & Safety:**
- **Create Backups**: Generate ZIP archives with original files before optimization
- **Keep Original Files**: Preserve original images alongside optimized versions
- **Re-optimize Already Optimized**: When enabled, allows re-processing files that already have correct names (catprev, catmini, minitile, etc.)

### 🎮 **Game Overlay System**
- **Always-On-Top Window**: Quick mod toggle accessible during gameplay
- **Category Navigation**: Browse mods by category within the overlay
- **Active-Only Filter**: Show only active mods for quick management (Alt+A hotkey)
- **Smart Performance**: Virtualized loading - only renders visible mods for optimal performance with large collections
- **Customizable Appearance**: Configurable theme (Auto/Light/Dark) and backdrop effects (Mica/MicaAlt/Acrylic/Thin/None)
- **Cache System**: Intelligent caching of mod metadata for instant loading

**Overlay Configuration:**
- **Theme Options**: Auto, Light, Dark overlay themes
- **Backdrop Effects**: Mica, MicaAlt, Acrylic, Thin, None for window transparency
- **Test Overlay**: "Open Overlay" button to preview overlay window
- **Auto-reload Mods**: Automatically reload mods when activation changes (enables folder picker and drag & drop)

**Controller Support (SDL3):**
- **Universal Gamepad Support**: Compatible with Xbox, PlayStation, Nintendo Switch, Steam Controller, and generic controllers via SDL3
- **Advanced Navigation**: 2D grid navigation with automatic scrolling to visible elements
- **Haptic Feedback**: Configurable vibration feedback on navigation and actions (supported controllers only)
- **Enable Gamepad**: Toggle controller support for overlay navigation
- **Use Left Stick**: Navigate with analog stick instead of D-Pad
- **Vibrate on Navigation**: Haptic feedback when moving through mods (supported controllers only)
- **Controller Status**: Real-time detection with "Test" button for any connected controller

**Hotkey Controls:**
- **Toggle Hotkey**: Customizable key combination (Alt+W default) to show/hide overlay
- **Filter Active Hotkey**: Quick toggle to show only active mods (Alt+A default)

**Gamepad Button Mapping:**
- **A Button**: Select/Toggle Mod
- **B Button**: Back/Close Overlay  
- **RB/LB**: Next/Previous Category
- **Back+Start**: Toggle Overlay Combo
- **Back+A**: Filter Active Combo
- **D-Pad/Left Stick**: Navigation (Up/Down/Left/Right)

### 🎯 **Controller Support**
FlairX includes comprehensive gamepad support using SDL3:

**Supported Controllers:**
- Xbox (360, One, Series X/S)
- PlayStation (3, 4, 5)
- Nintendo Switch (Pro Controller, Joy-Cons)
- Steam Controller & Steam Deck
- Generic controllers

**Navigation:**
- **D-Pad or Left Stick**: Navigate through mod grid (configurable)
- **A Button**: Select/activate mods
- **B Button**: Go back/close overlay
- **LB/RB**: Switch categories in overlay
- **Back+Start**: Toggle overlay window (customizable)
- **Back+A**: Filter active mods only (customizable)
- **Vibration Feedback**: Optional haptic feedback on actions

### ⌨️ **Keyboard Shortcuts**

**Global Hotkeys (work system-wide):**
- **Ctrl+R**: Reload mod library
- **Ctrl+S**: Shuffle active mods (random 1 per category)
- **Ctrl+D**: Deactivate all mods
- **Alt+W**: Toggle game overlay
- **Alt+A**: Filter active mods only (in overlay and main interface)

**In-App Shortcuts:**
- **Ctrl + Mouse Wheel**: Zoom in/out (100%-250%)
- **Escape**: Close dialogs/panels

*All hotkeys are fully customizable in Settings*

### 🔒 **Performance & Safety Features**
- **File Access Queue**: Advanced system preventing file conflicts during mod operations
- **Intelligent Caching**: Smart metadata caching with timestamp validation for instant loading
- **Memory Optimization**: Lazy loading of images and automatic cache cleanup for large mod collections
- **Comprehensive Logging**: Detailed logging system with configurable levels (Grid, Debug, Info, Warning, Error)
- **Safe File Operations**: Protected file handling with automatic backup creation before modifications

### 💾 **Preset System**
- **Load Preset**: Select from dropdown of saved presets and click "Load" to apply configuration
- **Save New Preset**: Enter a custom name and click "Save" to store current mod states
- **Delete Preset**: Remove unwanted presets from the list with "Delete" button
- **Default Preset**: Built-in "Default Preset" available as baseline configuration
- **Per-Game Presets**: Separate preset collections for each supported game
- **Quick Switching**: Instantly switch between different mod configurations
- **Persistent Storage**: Presets saved automatically and restored between sessions

### 🔄 **StatusKeeper Integration**
- **XXMI Synchronization**: Keep mod states synchronized with XXMI launcher through d3dx_user.ini file
- **File Path Management**: Configure path to d3dx_user.ini file (e.g., `[...] > XXMI > ZZMI > d3dx_user.ini`)
- **Backup Safety System**: Confirm backups are created before enabling synchronization
- **Dynamic Synchronization**: Automatically sync mod status when d3dx_user.ini changes
- **Manual Synchronization**: Trigger manual sync of mod status with "Start" button
- **Three-Tab Interface**: 
  - **Synchronization**: Configure sync settings and trigger manual sync
  - **Backup**: Manage backup confirmations and safety overrides
  - **Logs**: View detailed StatusKeeper operation logs
- **Per-Mod Control**: Individual mods can be excluded from StatusKeeper sync via mod details
- **Safety Confirmations**: Multiple backup confirmation toggles to prevent data loss

### 💾 **ModInfo Backup System**
- **Create Backup**: Generate backups of all mod JSON files and preview images with "Create" button
- **Multiple Backup Slots**: Maintain up to 3 backup versions (Newest, Middle, Oldest)
- **Backup Information**: View creation dates and file counts for each backup
- **One-Click Restore**: Restore any backup with "Restore" button to recover mod configurations
- **Backup Management**: Delete unwanted backups with trash icon
- **Complete Data Protection**: Backs up both mod metadata (JSON files) and preview images
- **Automatic Versioning**: Newest backups automatically become middle/oldest as new ones are created

## 🌍 Multi-Language Support

FlairX supports **16 languages** with automatic detection:
- English, Spanish, French, German, Italian
- Japanese, Korean, Chinese (Simplified/Traditional)
- Russian, Polish, Portuguese (BR/PT)
- Hindi, Thai, Filipino, Turkish, Vietnamese

**Features:**
- Automatic language detection based on system locale
- Specialized fonts for Asian and RTL languages
- Complete UI localization including dialogs and messages
- Language-specific font optimization

## 🛠️ Settings & Configuration

### **Appearance**
- **Theme**: Automatic, Light, Dark
- **Backdrop Effect**: Mica, Mica Alt, Acrylic, Acrylic Thin, None
- **Preview Effect**: None, Frame, Accent, Parallax, Glass
- **Language**: 16 languages with automatic detection

### **Display**
- **Default Resolution on Start**: Use fixed window size instead of remembering last size
- **Default Window Size**: Configurable startup dimensions (e.g., 3862 x 2110)

### **Directories**
- **XXMI Root Directory**: Configurable path to XXMI installation with folder browser and refresh

### **Behavior**
- **Skip XXMI Launcher Startup**: Bypass launcher (disables automatic updates)
- **Move Active Mods to Top**: Prioritize active mods in the list
- **Dynamic Filtering**: Enable real-time filtering as you type
- **Animation**: Show decorative animation under the manager title
- **Grid Zoom**: Enable zoom functionality for mod grid (100%-250%)
- **Grid Logging**: Enable detailed logging for performance debugging
- **Error-Only Logging**: Log only errors and warnings to reduce file size
- **Minimize to Tray**: Minimize to system tray instead of taskbar
- **Hide NSFW Content**: Hide mods marked as NSFW in mod grid and GameBanana browser

### **Hotkeys**
- **Hotkeys Toggle**: Enable/disable all global hotkeys
- **Customizable Shortcuts**: All hotkeys can be remapped with edit and reset options
  - Reload Manager (default: Ctrl+R)
  - Shuffle Active Mods (default: Ctrl+S)
  - Deactivate All Mods (default: Ctrl+D)

### **Update System**
- **Automatic Update Check**: Built-in update checker
- **Version Display**: Shows current version and update status

## 📁 How Mods Are Organized

FlairX automatically organizes your mods using a **two-level folder structure**:

```
📁 Your FlairX Installation/
├── 📁 XXMI/                    # All game frameworks
│   ├── 📁 ZZMI/Mods/          # Zenless Zone Zero mods
│   │   ├── 📁 Characters/     # Category folder (required)
│   │   │   ├── 📁 Ayaka/      # Individual mod folder
│   │   │   ├── 📁 Ganyu/      # Individual mod folder
│   │   │   └── 📁 Zhongli/    # Individual mod folder
│   │   ├── 📁 Weapons/        # Category folder (required)
│   │   │   ├── 📁 Sword_Mod/  # Individual mod folder
│   │   │   └── 📁 Bow_Mod/    # Individual mod folder
│   │   ├── 📁 UI/             # Category folder (required)
│   │   │   └── 📁 Menu_Mod/   # Individual mod folder
│   │   └── 📁 Other/          # Category folder (optional)
│   │       └── 📁 Misc_Mod/   # Individual mod folder
│   ├── 📁 GIMI/Mods/          # Genshin Impact mods
│   │   ├── 📁 Characters/     # Same structure for each game
│   │   ├── 📁 Weapons/
│   │   └── 📁 UI/
│   └── 📁 [Other Games]/Mods/ # Other supported games
└── 📁 Settings/               # Your presets and configuration
```

### 📋 **Required Structure:**

**Two-Level Organization:**
1. **Category Folders** (1st level): Created manually or during GameBanana installation
2. **Mod Folders** (2nd level): Individual mods within each category

**Category Management:**
- **Manual creation**: You can create category folders yourself with any name
- **GameBanana installation**: Categories are suggested/created automatically during mod download
- **No predefined categories** - you can name folders however you want
- **"Other" category** is the only special category (has its own menu item)
- **Examples**: Characters, Weapons, UI, Outfits, Maps, etc. - any name works

**Important Notes:**
- **Category folders are required** - mods must be placed inside category folders
- **Mod names are automatically cleaned** - `DISABLED_` prefixes are removed from display
- **Activation works by folder renaming** - adding/removing `DISABLED_` prefix
- **Each mod needs a `mod.json` file** to be recognized by FlairX
- **Optional `hotkeys.json` file** for individual mod hotkey assignments

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
- Microsoft.WindowsAppSDK (1.8.251106002) - Modern Windows UI
- CommunityToolkit.WinUI (7.1.2) - UI components
- ppy.SDL3-CS (2025.1205.0) - Controller support
- Microsoft.Web.WebView2 (1.0.3179.45) - GameBanana browser
- SharpSevenZip (2.0.33) - Archive extraction

## 📄 License

GNU General Public License v3.0

## 🙏 Credits

**Author:** [Jank8](https://github.com/Jank8)

**AI Assistance:** [Kiro](https://kiro.dev/), [GitHub Copilot](https://github.com/features/copilot), [Qoder](https://qoder.org/)

**Special Thanks:** [XLXZ](https://github.com/XiaoLinXiaoZhu), [Noto Fonts](https://notofonts.github.io/), [Kenney Input Prompts](https://kenney.nl/assets/input-prompts), Microsoft WinUI 3, XXMI Framework

---

*Not affiliated with HoYoverse. Community tool for managing game modifications.*

**Built entirely through AI-assisted development! 🤖**