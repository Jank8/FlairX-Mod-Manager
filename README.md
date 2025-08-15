# FlairX Mod Manager

<img align="right" width="256" height="256" alt="appicon" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

A comprehensive mod management application for multiple miHoYo games, built with WinUI 3 and .NET 8. This application provides an intuitive interface for managing, organizing, and activating game modifications across Zenless Zone Zero, Genshin Impact, Honkai Impact 3rd, Honkai: Star Rail, and Wuthering Waves.




## Supported Games

- **Zenless Zone Zero (ZZZ)** - ZZMI
- **Genshin Impact (GI)** - GIMI  
- **Honkai Impact 3rd (HI)** - HIMI
- **Honkai: Star Rail (SR)** - SRMI
- **Wuthering Waves (WW)** - WWMI

## Key Features

### 🎮 Mod Management
- **Visual Mod Library**: Browse mods with thumbnails in a responsive grid layout
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
- **Multi-language Support**: Automatic language detection with specialized fonts for international scripts
- **Administrator Privileges**: Automatic elevation for symbolic link creation and file operations

### 🎨 User Interface
- **Modern Design**: WinUI 3 with Fluent Design System and smooth animations
- **Theme Support**: Light, Dark, and Auto themes with system integration
- **Backdrop Effects**: Mica, Acrylic variants, or solid colors matching your preference
- **Responsive Layout**: Adaptive interface with zoom support and window state persistence
- **Accessibility**: Multi-language font support including Chinese, Japanese, Korean, Arabic, Hebrew, Hindi, and Thai
- **Loading Experience**: Professional loading screen with progress indication during startup

### 🛠️ Technical Features
- **Security Validation**: Path traversal protection and safe file operations
- **NTFS Validation**: Automatic file system checking for symbolic link support
- **Directory Management**: Automatic creation of required directories with proper structure
- **Error Handling**: Comprehensive error handling with detailed logging
- **Performance Optimization**: Efficient image caching and background processing
- **Global Hotkeys**: Configurable keyboard shortcuts for common operations

## System Requirements

### Operating System
- **Windows 10** version 1809 (build 17763) or later
- **Windows 11** (recommended for best backdrop effects)

### Required Software
- **.NET 8 Runtime** (Windows Desktop)
- **Microsoft Visual C++ Redistributable** (latest)
- **Windows App SDK** 1.7 or later
- **XXMI Framework** (Portable version recommended)

### File System & Permissions
- **NTFS partition** required for mod activation (symbolic link support)
- **Administrator privileges** automatically requested for symbolic link creation
- Application validates NTFS file system and shows warnings for incompatible drives

### Hardware
- **RAM**: 4 GB minimum, 8 GB recommended
- **Storage**: 500 MB for application, additional space for mod libraries
- **Display**: 1280x720 minimum resolution (supports up to 20000x15000)
- **Default Window Size**: 1650x820 pixels

## Installation

### Prerequisites
1. Ensure your system meets the requirements above
2. Install .NET 8 Desktop Runtime from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Install Visual C++ Redistributable (x64) from [Microsoft](https://aka.ms/vs/17/release/vc_redist.x64.exe)
4. Install Windows App SDK from [Microsoft site](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)

### Installation Steps
1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive to your desired location (preferably on an NTFS drive)
3. Run `FlairX Mod Manager Launcher.exe`
4. The application will automatically request administrator privileges if needed

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
3. **Click reload** (↻) to detect new mods
4. **Activate** by clicking heart icons

### Directory Structure Example
```
📁 app/ModLibrary/GI/
├── 📁 Characters/
│   └── 📁 Ayaka_Outfit/
│       ├── 📄 mod.json (auto-created)
│       ├── 📄 preview.jpg
│       └── 📁 mod files...
├── 📁 Weapons/
└── 📁 UI/
```

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
- **Themes**: Light, Dark, Auto with Mica, Acrylic, or solid backdrop effects
- **Global Hotkeys**: Configurable shortcuts (Ctrl+O, Ctrl+R, Ctrl+S, Ctrl+D)
- **Window State**: Automatic saving of size, position, and maximized state
- **Language Support**: Automatic detection with specialized font loading
- **Cache Management**: Configurable image cache limits (100 disk, 50 RAM)

## Development

### Building from Source
1. **Prerequisites**:
   - Visual Studio 2022 with .NET 8 SDK
   - Windows App SDK 1.7+
   - WinUI 3 project templates

2. **Clone and Build**:
   ```bash
   git clone [repository-url]
   cd "FlairX Mod Manager"
   dotnet restore
   dotnet build --configuration Release
   ```

3. **Dependencies**:
   - Microsoft.WindowsAppSDK (1.7.250606001)
   - CommunityToolkit.WinUI.Behaviors (8.2.250402)
   - CommunityToolkit.WinUI.Controls.Segmented (8.2.250402)
   - Microsoft.Graphics.Win2D (1.3.2)
   - NLua (1.7.5)
   - System.Drawing.Common (8.0.10)

### Architecture
- **Component-Based Design**: Modular architecture with specialized components
- **Core Components**:
  - **App**: Main application with startup logic, language detection, and mod library initialization
  - **MainWindow**: Navigation, game selection, search functionality, and backdrop effects
  - **LoadingWindow**: Professional loading screen with progress indication
  - **PathManager**: Centralized path management with security validation
  - **SettingsManager**: Per-game configuration and settings persistence
  - **SecurityValidator**: Path traversal protection and input sanitization
  - **SharedUtilities**: Common utilities for file operations and UI helpers
  - **Logger**: Thread-safe logging with multiple severity levels
- **Multi-Game Support**: Game-agnostic architecture with configurable paths per game
- **Dynamic Menu System**: Automatic category detection and menu generation from mod library structure
- **Security-First**: Comprehensive validation and safe file operations throughout
- **Internationalization**: Multi-language support with automatic font switching for different scripts

## Troubleshooting

### Common Issues
1. **Mods not activating**: Run as Administrator on NTFS drive
2. **XXMI not launching**: Verify XXMI portable version in correct directory
3. **Categories not appearing**: Place mods in category subdirectories (`ModLibrary/[Game]/[Category]/[ModName]/`)
4. **Missing thumbnails**: Ensure mod folders have files with "preview" in filename

### Log Files
Check `Settings/` directory for: `Application.log`, `StatusKeeper.log`, `GridLog.log`

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





