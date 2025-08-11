# FlairX Mod Manager

<img align="right" width="256" height="256" alt="appicon" src="https://github.com/user-attachments/assets/f020d49b-1c4c-46a3-a97c-352e4735180f" />

A comprehensive mod management application for multiple miHoYo games, built with WinUI 3 and .NET 8. This application provides an intuitive interface for managing, organizing, and activating game modifications across Zenless Zone Zero, Genshin Impact, Honkai Impact 3rd, Honkai: Star Rail, and Wuthering Waves.




## Supported Games

- **Zenless Zone Zero (ZZZ)** - ZZMI
- **Genshin Impact (GI)** - GIMI  
- **Honkai Impact 3rd (HI)** - HIMI
- **Honkai: Star Rail (SR)** - SRMI
- **Wuthering Waves (WW)** - WWMI

## Features

### 🎮 Mod Management
- **Visual Mod Library**: Browse mods with thumbnail previews in a responsive grid layout
- **Category-based Organization**: Organize mods by categories (Characters, Weapons, UI, Effects, etc.) with automatic folder detection
- **Dynamic Menu Generation**: Categories automatically appear as menu items in the left sidebar for easy navigation
- **Flexible Category System**: Create custom categories or use standard ones - FlairX adapts to your organization
- **Mod Activation/Deactivation**: Toggle mods on/off with symbolic link management
- **Search & Filter**: Dynamic search functionality with real-time filtering across all categories simultaneously
- **Mod Details**: View detailed information including author, character, hotkeys, and URLs
- **Multi-Game Support**: Switch between different games with isolated mod libraries and category structures

### 🔧 Advanced Features
- **Preset System**: Save and load different mod configurations per game
- **XXMI Integration**: Integrated launcher for XXMI (mod injection framework)
- **Hotkey Detection**: Automatic detection of mod hotkeys from configuration files
- **StatusKeeper Integration**: Backup/restore system for mod states with dynamic synchronization
- **Namespace Support**: Modern namespace-based mod synchronization alongside classic methods
- **Multi-language Support**: Automatic language detection with font support for various scripts

### 🛠️ Utility Functions
- **Mod Library Management**: Organize mods in dedicated game-specific library folders
- **Thumbnail Optimization**: Optimize mod preview images with automatic resizing and cropping
- **File System Validation**: NTFS requirement checking for symbolic link support
- **Settings Management**: Customizable paths, themes, and preferences per game
- **Backup Management**: Comprehensive backup system for mod configurations

### 🎨 User Interface
- **Modern Design**: WinUI 3 with Fluent Design System
- **Theme Support**: Light, Dark, and Auto themes
- **Backdrop Effects**: Mica, Mica Alt, Acrylic, Acrylic Thin, or None
- **Responsive Layout**: Adaptive interface with zoom support
- **Accessibility**: Multi-language font support including Asian and RTL languages
- **Visual Effects**: Configurable animations and smooth transitions

## System Requirements

### Operating System
- **Windows 10** version 1809 (build 17763) or later
- **Windows 11** (recommended)

### Required Software
- **.NET 8 Runtime** (Windows Desktop)
- **Microsoft Visual C++ Redistributable** (latest)
- **Windows App SDK** 1.7 or later
- **XXMI Framework** (Portable version recommended)

### File System
- **NTFS partition** required for mod activation (symbolic link support)
- Administrator privileges required for symbolic link creation

### Hardware
- **RAM**: 4 GB minimum, 8 GB recommended
- **Storage**: 500 MB for application, additional space for mod libraries
- **Display**: 1280x720 minimum resolution

## Installation

### Prerequisites
1. Ensure your system meets the requirements above
2. Install .NET 8 Desktop Runtime from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Install Visual C++ Redistributable (x64) from [Microsoft](https://aka.ms/vs/17/release/vc_redist.x64.exe)
4. Install Windows App SDK from [Microsoft site](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)

### Installation Steps
1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive to your desired location (preferably on an NTFS drive)
3. Run `FlairX Mod Manager.exe` as Administrator
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
FlairX Mod Manager/
├── ModLibrary/
│   ├── ZZ/              # Zenless Zone Zero mods
│   │   ├── Characters/  # Character mods
│   │   ├── Weapons/     # Weapon mods
│   │   ├── UI/          # UI mods
│   │   └── Other/       # Other mods
│   ├── GI/              # Genshin Impact mods
│   │   ├── Characters/  # Character mods
│   │   ├── Weapons/     # Weapon mods
│   │   ├── UI/          # UI mods
│   │   └── Other/       # Other mods
│   ├── HI/              # Honkai Impact 3rd mods
│   ├── SR/              # Honkai: Star Rail mods
│   └── WW/              # Wuthering Waves mods
├── XXMI/
├── Settings/            # Application settings
├── Language/            # Language files
└── Assets/             # Application resources
```

### Settings
- **Location**: `Settings/` directory
- **Languages**: English (default), extensible via JSON files
- **Themes**: Light, Dark, Auto with various backdrop effects
- **Per-Game**: Custom paths and configurations for each supported game

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
- **MVVM Pattern**: Clean separation of UI and business logic
- **Page-based Navigation**: Modular page system for different features
- **Service Layer**: Centralized services for settings, logging, and file operations
- **Multi-Game Support**: Game-agnostic architecture with configurable paths and dynamic game selection
- **Category System**: Flexible directory-based organization with automatic detection and menu generation
- **Dynamic Path Resolution**: Smart path handling that adapts to selected games and category structures
- **Localization**: Resource-based multi-language support

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
- **miHoYo game modding communities** - Feedback and testing support
- **XXMI Framework developers** - Mod injection system

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

---

*This application is not affiliated with miHoYo/HoYoverse or any of the supported games. It is a community-developed tool for managing game modifications.*




