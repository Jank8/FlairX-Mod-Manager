# FlairX Mod Manager - Version 4.1.1

## 🎉 New Features

### Quick Update
- **Context Menu Quick Update**: New "Quick Update" option in mod context menu (replaces "Check for Updates")
  - Opens file selection dialog directly without opening GameBanana browser
  - Shows list of available files with checkboxes
  - Validates selection (Start button disabled until at least one file is selected)
  - Uses existing mod path for seamless updates
  - Supports all installation options (Clean Install, Backup, Keep Previews, etc.)

### Improved Update Flow
- **Smart Reload**: Mod grid reloads only after successful installation (not on cancel/error)
- **File Selection UI**: Scrollable list with file details (name, size, description)
- **Better Validation**: Start button enabled only when files are selected and inputs are valid

## 🐛 Bug Fixes
- Fixed cleanup after failed extraction - partially installed mods are now removed automatically
- Fixed reload triggering on dialog cancel/close - now only reloads on successful installation
- Added missing "Loading" translation for all languages

## 🌍 Translations
Added/Updated translations:
- **Quick Update** (Szybka aktualizacja, Schnellaktualisierung, Actualización rápida, etc.) - 29 languages
- **Select Files to Download** (Wybierz pliki do pobrania, etc.) - 29 languages  
- **Loading** (Ładowanie..., Wird geladen..., Cargando..., etc.) - 29 languages

### Supported Languages
English, Polish, Czech, German, Spanish, French, Italian, Japanese, Korean, Portuguese (BR), Portuguese, Russian, Thai, Tagalog, Turkish, Vietnamese, Chinese (Simplified), Chinese (Traditional), Hindi, Danish, Greek, Finnish, Hungarian, Indonesian, Dutch, Norwegian, Romanian, Swedish, Ukrainian

## 🔧 Technical Changes
- Enhanced `GameBananaFileExtractionDialog` with dynamic file selection UI
- Added `CleanupFailedInstallation()` call in error handling
- Implemented `ModInstalled` event tracking for conditional reload
- Added file selection validation in `ValidateInputs()` method

## 📝 Context Menu Changes
- **Before**: "Check for Updates" → Opens GameBanana browser
- **After**: "Quick Update" → Opens file selection dialog directly

---

**Release Date**: 2026-09-04  
**Previous Version**: 4.1.0
