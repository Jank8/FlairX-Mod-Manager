# FlairX Mod Manager - Quick Start Guide

## Setup (First Time)

### 1. Extract FlairX Mod Manager
- Extract and run `FlairX Mod Manager Launcher.exe`
- Select your game from the dropdown (Genshin Impact, Star Rail, etc.)

### 2. Download XXMI Launcher (Portable Version)
- Click "Download XXMI" when prompted
- Download the **portable version** from GitHub
- Extract XXMI folder to your app directory (or custom location)

### 3. Set Up Folders (Usually Automatic)
FlairX automatically uses these folders:
- **Mod Library Directory**: `app/ModLibrary/` (organized by game)
- **XXMI Mods Directory**: `app/XXMI/[GAME]/Mods/` (where active mods are linked)

**Only change paths in Settings if:**
- You want to use a custom location for your mod library
- You installed XXMI in a different directory

## File Organization

### FlairX Mod Manager Directory Structure
```
📁 FlairX Mod Manager/
├── 📄 FlairX Mod Manager Launcher.exe (Run this file)
└── 📁 app/
    ├── 📁 ModLibirary/          (Your downloaded mods go here)
    │   ├── 📁 GI/              (Genshin Impact mods)
    │   ├── 📁 HI/              (Honkai Impact mods)
    │   ├── 📁 SR/              (Star Rail mods)
    │   ├── 📁 WW/              (Wuthering Waves mods)
    │   └── 📁 ZZ/              (Zenless Zone Zero mods)
    ├── 📁 XXMI/                (XXMI launchers and active mods)
    │   ├── 📁 GIMI/Mods/       (Active Genshin mods)
    │   ├── 📁 HIMI/Mods/       (Active Honkai mods)
    │   ├── 📁 SRMI/Mods/       (Active Star Rail mods)
    │   ├── 📁 WWMI/Mods/       (Active Wuthering Waves mods)
    │   └── 📁 ZZMI/Mods/       (Active Zenless Zone Zero mods)
    ├── 📁 Settings/            (App settings and presets)
    └── 📁 Language/            (Language files)
```

### Where to Put Downloaded Mods
Put each mod in its own folder inside the game-specific directory:

**For Genshin Impact mods:**
```
📁 app/ModLibrary/GI/
├── 📁 Lumine Celestial Embroidery/
│   ├── 📄 mod.json (created automatically by FlairX)
│   ├── 📄 TravelerGirl.ini
│   ├── 📄 preview.jpg (or any file with "preview" in name)
│   └── 📁 texture files...
├── 📁 LumineFavoniousKnight-ToggleP/
│   ├── 📄 mod.json (created automatically by FlairX)
│   ├── 📄 merged.ini
│   ├── 📄 preview777.png (preview file)
│   └── 📁 subfolders with variants...
```

**For other games, use the same pattern:**
- **Honkai Impact**: `app/ModLibrary/HI/`
- **Star Rail**: `app/ModLibrary/SR/`
- **Wuthering Waves**: `app/ModLibrary/WW/`
- **Zenless Zone Zero**: `app/ModLibrary/ZZ/`

### Active Mods (Automatic)
When you activate mods, FlairX creates symbolic links here:
```
📁 app/XXMI/GIMI/Mods/          (Active Genshin mods)
📁 app/XXMI/HIMI/Mods/          (Active Honkai mods)
📁 app/XXMI/SRMI/Mods/          (Active Star Rail mods)
📁 app/XXMI/WWMI/Mods/          (Active Wuthering Waves mods)
📁 app/XXMI/ZZMI/Mods/          (Active Zenless Zone Zero mods)
```

## How to Use

### Installing Mods
1. **Download** mod files from GameBanana/NexusMods etc.
2. **Extract** each mod to its own folder in the correct game directory:
   - Genshin Impact → `app/ModLibrary/GI/[ModName]/`
   - Star Rail → `app/ModLibrary/SR/[ModName]/`
   - Zenless Zone Zero → `app/ModLibrary/ZZ/[ModName]/`
   - etc.
3. **Click reload** button (↻) in FlairX
4. **Activate** mods by clicking on them in the grid

### Managing Mods
- **Search**: Use search bar to find mods
- **View active**: Click heart icon (♡) to see only active mods
- **Launch game**: Click play button (bottom-right)

## Common Issues

### "XXMI Launcher not found"
- Download the **portable version** of XXMI from GitHub
- Extract to `app/XXMI/` directory
- If using custom location, update path in Settings

### Mods won't activate
- **Check file system**: Both directories must be on NTFS drives
- **Verify paths**: Check Settings for correct folder paths
- FlairX will ask for admin rights automatically when needed

### Missing thumbnails
- Ensure each mod folder has a file with "preview" in the name (JPG or PNG format)
- Use Settings → "Optimize mod thumbnails" (converts to optimized JPG, removes original file)
- Example: `preview.jpg`, `preview.png`, `preview879.jpg`

## Quick Tips
- Put mods in the correct game folder: `app/ModLibrary/GI/`, `app/ModLibrary/ZZ/`, etc.
- Each mod needs its own folder with a clear name
- FlairX automatically creates `mod.json` files when you refresh or start the manager
- Preview files need "preview" in the filename: `preview.jpg`, `preview888.png`, etc.
- Thumbnail optimization converts images to optimized JPG and removes the original file
- Only activate mods you want to use in-game
- Use Presets (Functions menu) to save mod combinations
- FlairX automatically organizes active mods in the XXMI folders
