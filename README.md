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
- **Character-based Organization**: Automatically categorizes mods by character with dynamic menu generation
- **Mod Activation/Deactivation**: Toggle mods on/off with symbolic link management
- **Search & Filter**: Dynamic search functionality with real-time filtering (configurable)
- **Mod Details**: View detailed information including author, character, hotkeys, and URLs
- **Multi-Game Support**: Switch between different games with isolated mod libraries

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
4. Install Windows App SDK from [Microsoft Store](https://apps.microsoft.com/detail/9pb2mz1zmb1s) or [GitHub releases](https://github.com/microsoft/WindowsAppSDK/releases)

### Installation Steps
1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive to your desired location (preferably on an NTFS drive)
3. Run `FlairX Mod Manager.exe` as Administrator
4. The application will automatically request administrator privileges if needed

### First Run Setup
1. **Game Selection**: Choose your game from the dropdown menu
2. **Language Selection**: The app will auto-detect your system language or default to English
3. **Directory Configuration**: Set up your mod library and XXMI directories for each game
4. **XXMI Integration**: Ensure XXMI is properly installed in the manager folder
5. **Mod Library**: Place your mods in the appropriate game's ModLibrary folder

## Usage

### Game Selection
1. **Select Game**: Use the dropdown menu to select your target game
2. **Automatic Setup**: The application will automatically configure paths for the selected game
3. **Isolated Libraries**: Each game maintains its own mod library and settings

### Managing Mods
1. **Adding Mods**: Place mod folders in the game-specific ModLibrary directory
2. **Activating Mods**: Click the heart icon on any mod to activate/deactivate
3. **Viewing Details**: Click on a mod name to view detailed information
4. **Searching**: Use the search bar with dynamic or static filtering

### Using Presets
1. **Creating Presets**: Activate desired mods, then save as a new preset
2. **Loading Presets**: Select a preset from the dropdown and click load
3. **Managing Presets**: Delete unwanted presets from the presets page
4. **Game-Specific**: Each game maintains its own preset collection

### XXMI Integration
1. **Launching Game**: Use the floating action button to launch XXMI
2. **Mod Injection**: Active mods are automatically available in XXMI
3. **Multi-Game Support**: XXMI launcher adapts to the selected game

### StatusKeeper Features
1. **Backup Creation**: Create backups of your mod configuration files
2. **Dynamic Sync**: Enable automatic synchronization with file watching
3. **Log Monitoring**: View detailed logs of backup operations
4. **Namespace Support**: Modern namespace-based synchronization

## Configuration

### Directory Structure
```
FlairX Mod Manager/
├── ModLibrary/
│   ├── ZZ/              # Zenless Zone Zero mods
│   ├── GI/              # Genshin Impact mods
│   ├── HI/              # Honkai Impact 3rd mods
│   ├── SR/              # Honkai: Star Rail mods
│   └── WW/              # Wuthering Waves mods
├── XXMI/
├── Settings/            # Application settings
├── Language/            # Language files
└── Assets/             # Application resources
```

### Settings File
Settings are stored in `Settings directory`:
- **SelectedGameIndex**: Currently selected game
- **Language**: Interface language
- **Theme**: UI theme preference
- **BackdropEffect**: Window backdrop effect
- **Game-specific paths**: Custom paths for mods and XXMI per game
- **Feature toggles**: Various application features

### Supported Languages
- English (default)
- Additional languages can be added via JSON files in the Language folder
- Automatic system language detection
- Font support for Asian and RTL scripts

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
- **Multi-Game Support**: Game-agnostic architecture with configurable paths
- **Localization**: Resource-based multi-language support

## Troubleshooting

### Common Issues
1. **Mods not activating**: Ensure you're running as Administrator on an NTFS drive
2. **XXMI not launching**: Verify XXMI files are in the correct game directory
3. **Thumbnails not loading**: Run the thumbnail optimization tool in settings
4. **Language not changing**: Restart the application after changing language
5. **Game switching issues**: Ensure proper directory structure for each game

### Log Files
- **Application Log**: `Settings/Application.log`
- **StatusKeeper Log**: `Settings/StatusKeeper.log`
- **Grid Log**: `Settings/GridLog.log`

### Support
- Check the application logs for detailed error information
- Ensure all system requirements are met
- Verify file permissions and NTFS support
- Confirm proper XXMI installation for your target game

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
### USER MANUAL https://github.com/Jank8/FlairX-Mod-Manager/blob/main/FlairX-Mod-Manager/USER_MANUAL.md

*This application is not affiliated with miHoYo/HoYoverse or any of the supported games. It is a community-developed tool for managing game modifications.*



