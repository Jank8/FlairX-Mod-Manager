# ZZZ Mod Manager X


<img align="center" width="256" height="256" alt="appicon" src="https://github.com/user-attachments/assets/69b40b1f-0b89-417b-81a7-70f9507b3c42" />


A comprehensive mod management application for Zenless Zone Zero (ZZZ) game, built with WinUI 3 and .NET 8. This application provides an intuitive interface for managing, organizing, and activating game modifications.

## Features

### 🎮 Mod Management
- **Visual Mod Library**: Browse mods with thumbnail previews in a grid layout
- **Character-based Organization**: Automatically categorizes mods by character
- **Mod Activation/Deactivation**: Toggle mods on/off with symbolic link management
- **Search & Filter**: Dynamic search functionality with real-time filtering
- **Mod Details**: View detailed information including author, character, hotkeys, and URLs

### 🔧 Advanced Features
- **Preset System**: Save and load different mod configurations
- **XXMI Integration**: Add launcher for XXMI (mod injection framework) into app directory.
- **Hotkey Detection**: Automatic detection of mod hotkeys from configuration files
- **Backup Management**: StatusKeeper integration for mods state backup/restore
- **Multi-language Support**: Supports multiple languages with automatic detection

### 🛠️ Utility Functions
- **Mod Library Management**: Collect all your mods in a dedicated library folder
- **Thumbnail Optimization**: Optimize mod preview images for use with manager
- **File System Validation**: NTFS requirement checking for symbolic link support
- **Settings Management**: Customizable paths, themes, and preferences

### 🎨 User Interface
- **Modern Design**: WinUI 3 with Fluent Design System
- **Theme Support**: Light, Dark, and Auto themes
- **Responsive Layout**: Adaptive interface that works on different screen sizes
- **Accessibility**: Multi-language font support including Asian and RTL languages
- **Visual Effects**: Mica backdrop and smooth animations

## System Requirements

### Operating System
- **Windows 10** version 1809 (build 17763) or later
- **Windows 11** (recommended)

### Required Software
- **.NET 8 Runtime** (Windows Desktop)
- **Microsoft Visual C++ Redistributable** (latest)
- **Windows App SDK** 1.8 or later

### File System
- **NTFS partition** required for mod activation (symbolic link support)
- Administrator privileges required for symbolic link creation

### Hardware
- **RAM**: 4 GB minimum, 8 GB recommended
- **Storage**: 500 MB for application, additional space for mod library
- **Display**: 1280x720 minimum resolution

## Installation

### Prerequisites
1. Ensure your system meets the requirements above
2. Install .NET 8 Desktop Runtime from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Install Visual C++ Redistributable (x64) from [Microsoft](https://aka.ms/vs/17/release/vc_redist.x64.exe)
4. Install Windows App SDK 1.8+ from [Microsoft Store](https://apps.microsoft.com/detail/9pb2mz1zmb1s) or [GitHub releases](https://github.com/microsoft/WindowsAppSDK/releases)

### Installation Steps
1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive to your desired location (preferably on an NTFS drive)
3. Run `ZZZ Mod Manager X.exe` as Administrator
4. The application will automatically request administrator privileges if needed

### First Run Setup
1. **Language Selection**: The app will auto-detect your system language or default to English (more languages comming)
2. **Directory Configuration**: Set up your mod library and XXMI directories
3. **XXMI Integration**: Ensure XXMI is properly installed in manager folder
4. **Mod Library**: Place your mods in the ModLibrary folder

## Usage

### Managing Mods
1. **Adding Mods**: Place mod folders in the ModLibrary directory
2. **Activating Mods**: Click the heart icon on any mod to activate/deactivate
3. **Viewing Details**: Click on a mod name to view detailed information
4. **Searching**: Use the search bar to find specific mods quickly

### Using Presets
1. **Creating Presets**: Activate desired mods, then save as a new preset
2. **Loading Presets**: Select a preset from the dropdown and click load
3. **Managing Presets**: Delete unwanted presets from the presets page

### XXMI Integration
1. **Launching Game**: Use the floating action button to launch XXMI
2. **Mod Injection**: Active mods are automatically available in XXMI

### StatusKeeper Features
1. **Backup Creation**: Create backups of your mods original ini files
2. **Sync Management**: Enable automatic synchronization
3. **Log Monitoring**: View detailed logs of backup operations

## Configuration

### Directory Structure
```
ZZZ Mod Manager X/
├── ModLibrary/          # Your mod collection
├── XXMI/               # XXMI framework files
├── Settings/           # Application settings
├── Language/           # Language files
└── Assets/            # Application resources
```

### Settings File
Settings are stored in `Settings/Settings.json`:
- **Language**: Interface language
- **Theme**: UI theme preference
- **Directories**: Custom paths for mods and XXMI
- **Features**: Toggle various application features

### Supported Languages
- English
- Chinese (Simplified/Traditional)
- Japanese
- Korean
- Arabic
- Hebrew
- Hindi
- Thai
- Polish
- And more...

## Development

### Building from Source
1. **Prerequisites**:
   - Visual Studio 2022 with .NET 8 SDK
   - Windows App SDK 1.8
   - WinUI 3 project templates

2. **Clone and Build**:
   ```bash
   git clone [repository-url]
   cd "ZZZ Mod Manager X"
   dotnet restore
   dotnet build --configuration Release
   ```

3. **Dependencies**:
   - Microsoft.WindowsAppSDK
   - CommunityToolkit.WinUI.Behaviors
   - CommunityToolkit.WinUI.Controls.Segmented
   - NLua
   - System.Drawing.Common
   - Microsoft.Graphics.Win2D

### Architecture
- **MVVM Pattern**: Clean separation of UI and business logic
- **Page-based Navigation**: Modular page system for different features
- **Service Layer**: Centralized services for settings, logging, and file operations
- **Localization**: Resource-based multi-language support

## Troubleshooting

### Common Issues
1. **Mods not activating**: Ensure you're running as Administrator on an NTFS drive
2. **XXMI not launching**: Verify XXMI files are in the correct directory
3. **Thumbnails not loading**: Run the thumbnail optimization tool in settings
4. **Language not changing**: Restart the application after changing language

### Log Files
- **Application Log**: `Settings/Application.log`
- **StatusKeeper Log**: `Settings/StatusKeeper.log`

### Support
- Check the application logs for detailed error information
- Ensure all system requirements are met
- Verify file permissions and NTFS support

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
- **ZZZ modding community** - Feedback and testing support

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.
