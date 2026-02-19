using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using FlairX_Mod_Manager;
using FlairX_Mod_Manager.Models;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class SettingsUserControl : UserControl
    {
        private readonly string LanguageFolderPath = PathManager.GetAbsolutePath("Language");
        private Microsoft.UI.Xaml.DispatcherTimer? _windowSizeUpdateTimer;
        
        // Constants and structures for MoveToRecycleBin
        private const int FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public int wFunc;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
        private Dictionary<string, string> _languages = new(); // displayName, filePath
        private Dictionary<string, string> _fileNameByDisplayName = new();

        public event EventHandler? CloseRequested; // Event to notify parent to close
        
        // Hotkey management fields
        private TextBox? _activeHotkeyEditBox;
        private bool _previousHotkeysEnabled = true;
        
        // Hotkey definitions
        private readonly List<(string Key, string LabelKey, string DescKey, string Icon)> _hotkeyDefinitions = new()
        {
            ("ReloadManagerHotkey", "Reload_Mods_Tooltip", "SettingsPage_ReloadManagerHotkey_Description", "\uE72C"),
            ("ShuffleActiveModsHotkey", "SettingsPage_ShuffleActiveMods_Label", "SettingsPage_ShuffleActiveModsHotkey_Description", "\uE8B1"),
            ("DeactivateAllModsHotkey", "SettingsPage_DeactivateAllMods_Label", "SettingsPage_DeactivateAllModsHotkey_Description", "\uE711")
        };

        // Set BreadcrumbBar to path segments with icon at the beginning
        private void SetBreadcrumbBar(BreadcrumbBar bar, string path)
        {
            SharedUtilities.SetBreadcrumbBarPath(bar, path);
        }

        // Improved breadcrumb path aggregation
        private string GetBreadcrumbPath(BreadcrumbBar bar)
        {
            return SharedUtilities.GetBreadcrumbBarPath(bar);
        }

        public SettingsUserControl()
        {
            this.InitializeComponent();
            SettingsManager.Load();
            LoadLanguages();
            InitializeUIState();
            LoadSettingsHotkeys();
            
            // Initialize timer for delayed window size updates
            _windowSizeUpdateTimer = new Microsoft.UI.Xaml.DispatcherTimer();
            _windowSizeUpdateTimer.Interval = TimeSpan.FromMilliseconds(200); // 200ms delay
            _windowSizeUpdateTimer.Tick += WindowSizeUpdateTimer_Tick;
            
            // Subscribe to window size changes
            MainWindow.WindowSizeChanged += OnWindowSizeChanged;
            
            // Subscribe to loaded/unloaded events
            this.Loaded += SettingsUserControl_Loaded;
            this.Unloaded += SettingsUserControl_Unloaded;
        }
        
        private void SettingsUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Refresh settings when page is loaded to ensure SelectorBars display correctly
            LoadCurrentSettings();
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Notify parent to close the panel
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateToggleLabels()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
            if (DefaultResolutionOnStartToggleLabel != null && DefaultResolutionOnStartToggle != null)
                DefaultResolutionOnStartToggleLabel.Text = DefaultResolutionOnStartToggle.IsOn ? onText : offText;
            if (SkipXXMILauncherToggleLabel != null && SkipXXMILauncherToggle != null)
                SkipXXMILauncherToggleLabel.Text = SkipXXMILauncherToggle.IsOn ? onText : offText;
            if (ActiveModsToTopToggleLabel != null && ActiveModsToTopToggle != null)
                ActiveModsToTopToggleLabel.Text = ActiveModsToTopToggle.IsOn ? onText : offText;
            if (AutoDeactivateConflictingModsToggleLabel != null && AutoDeactivateConflictingModsToggle != null)
                AutoDeactivateConflictingModsToggleLabel.Text = AutoDeactivateConflictingModsToggle.IsOn ? onText : offText;
            if (DynamicModSearchToggleLabel != null && DynamicModSearchToggle != null)
                DynamicModSearchToggleLabel.Text = DynamicModSearchToggle.IsOn ? onText : offText;
            if (ShowOrangeAnimationToggleLabel != null && ShowOrangeAnimationToggle != null)
                ShowOrangeAnimationToggleLabel.Text = ShowOrangeAnimationToggle.IsOn ? onText : offText;
            if (ModGridZoomToggleLabel != null && ModGridZoomToggle != null)
                ModGridZoomToggleLabel.Text = ModGridZoomToggle.IsOn ? onText : offText;
            if (GridLoggingToggleLabel != null && GridLoggingToggle != null)
                GridLoggingToggleLabel.Text = GridLoggingToggle.IsOn ? onText : offText;
            if (ErrorOnlyLoggingToggleLabel != null && ErrorOnlyLoggingToggle != null)
                ErrorOnlyLoggingToggleLabel.Text = ErrorOnlyLoggingToggle.IsOn ? onText : offText;
            if (MinimizeToTrayToggleLabel != null && MinimizeToTrayToggle != null)
                MinimizeToTrayToggleLabel.Text = MinimizeToTrayToggle.IsOn ? onText : offText;
            if (BlurNSFWToggleLabel != null && BlurNSFWToggle != null)
                BlurNSFWToggleLabel.Text = BlurNSFWToggle.IsOn ? onText : offText;
            if (HotkeysEnabledToggleLabel != null && HotkeysEnabledToggle != null)
                HotkeysEnabledToggleLabel.Text = HotkeysEnabledToggle.IsOn ? onText : offText;
            if (FastDownloadToggleLabel != null && FastDownloadToggle != null)
                FastDownloadToggleLabel.Text = FastDownloadToggle.IsOn ? onText : offText;
        }
        
        private void BackButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }

        private void BackButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }
        
        private void InitializeUIState()
        {
            // Load current settings first
            LoadCurrentSettings();
            
            // Disable Mica options on Windows 10
            DisableMicaOptionsOnWindows10();
            
            // Load language dictionary once and reuse it
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            // Update all texts and icons once at the end
            UpdateTexts(lang);
            CheckUpdatesButtonText.Text = SharedUtilities.GetTranslation(lang, "CheckForUpdates");
            AboutButtonText.Text = SharedUtilities.GetTranslation(lang, "AboutButton_Label");
            AboutButtonIcon.Glyph = "\uE946";
            
        }
        
        private void DisableMicaOptionsOnWindows10()
        {
            // Check if running on Windows 10 (build < 22000 = Windows 11)
            bool isWindows10 = Environment.OSVersion.Version.Build < 22000;
            
            if (isWindows10)
            {
                // Disable Mica and MicaAlt options
                if (BackdropSelectorMica != null)
                {
                    BackdropSelectorMica.IsEnabled = false;
                    BackdropSelectorMica.Opacity = 0.5;
                }
                
                if (BackdropSelectorMicaAlt != null)
                {
                    BackdropSelectorMicaAlt.IsEnabled = false;
                    BackdropSelectorMicaAlt.Opacity = 0.5;
                }
                
                // If current setting is Mica or MicaAlt, switch to AcrylicThin
                string currentBackdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
                if (currentBackdrop == "Mica" || currentBackdrop == "MicaAlt")
                {
                    SettingsManager.Current.BackdropEffect = "AcrylicThin";
                    SettingsManager.Save();
                    
                    // Update selection without triggering event
                    BackdropSelectorBar.SelectionChanged -= BackdropSelectorBar_SelectionChanged;
                    BackdropSelectorBar.SelectedItem = BackdropSelectorAcrylicThin;
                    BackdropSelectorBar.SelectionChanged += BackdropSelectorBar_SelectionChanged;
                    
                    // Apply the new backdrop effect
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.ApplyBackdropEffect("AcrylicThin");
                    }
                }
            }
        }
        
        private void UpdateTexts(Dictionary<string, string>? lang = null)
        {
            // Load language dictionary only if not provided
            lang ??= SharedUtilities.LoadLanguageDictionary();
            
            // Main labels - use null checks
            if (SettingsTitle != null) SettingsTitle.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Title");
            
            // Section headers
            if (AppearanceHeader != null) AppearanceHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AppearanceHeader");
            if (DisplayHeader != null) DisplayHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DisplayHeader");
            if (DirectoriesHeader != null) DirectoriesHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DirectoriesHeader");
            if (BehaviorHeader != null) BehaviorHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_BehaviorHeader");
            if (DownloadHeader != null) DownloadHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DownloadHeader");
            
            if (ThemeLabel != null) ThemeLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme");
            if (BackdropLabel != null) BackdropLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop");
            if (PreviewEffectLabel != null) PreviewEffectLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewEffect");
            if (LanguageLabel != null) LanguageLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Language");
            if (DefaultResolutionOnStartLabel != null) DefaultResolutionOnStartLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultResolutionOnStart_Label");
            if (DefaultStartResolutionLabel != null) DefaultStartResolutionLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultStartResolution_Label");
            if (XXMIRootDirectoryLabel != null) XXMIRootDirectoryLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_XXMIRootDirectory");
                        if (SkipXXMILauncherLabel != null) SkipXXMILauncherLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_SkipXXMILauncher_Label");
            if (ActiveModsToTopLabel != null) ActiveModsToTopLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ActiveModsToTop_Label");
            if (AutoDeactivateConflictingModsLabel != null) AutoDeactivateConflictingModsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoDeactivateConflictingMods_Label");
            if (DynamicModSearchLabel != null) DynamicModSearchLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Label");
            if (ShowOrangeAnimationLabel != null) ShowOrangeAnimationLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShowOrangeAnimation_Label");
            if (ModGridZoomLabel != null) ModGridZoomLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModGridZoom_Label");
            if (GridLoggingLabel != null) GridLoggingLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Label");
            if (ErrorOnlyLoggingLabel != null) ErrorOnlyLoggingLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ErrorOnlyLogging_Label");
            if (MinimizeToTrayLabel != null) MinimizeToTrayLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MinimizeToTray_Label");
            if (BlurNSFWLabel != null) BlurNSFWLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HideNSFW_Label");
            if (FastDownloadLabel != null) FastDownloadLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_FastDownload_Label");
            if (MaxConnectionsLabel != null) MaxConnectionsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MaxConnections_Label");
            if (HotkeysHeader != null) HotkeysHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Hotkeys_Header");

            // Description texts - use null checks and fallback to empty string if missing
            if (ThemeDescription != null) ThemeDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Description") ?? string.Empty;
            if (BackdropDescription != null) BackdropDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_Description") ?? string.Empty;
            if (PreviewEffectDescription != null) PreviewEffectDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewEffect_Description") ?? string.Empty;
            
            // Preview effect ComboBox items
            if (PreviewEffectNone != null) PreviewEffectNone.Content = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewEffect_None") ?? "Default";
            if (PreviewEffectBorder != null) PreviewEffectBorder.Content = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewEffect_Border") ?? "Border";
            if (PreviewEffectAccent != null) PreviewEffectAccent.Content = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewEffect_Accent") ?? "Accent";
            if (PreviewEffectParallax != null) PreviewEffectParallax.Content = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewEffect_Parallax") ?? "Parallax";
            if (PreviewEffectGlassmorphism != null) PreviewEffectGlassmorphism.Content = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewEffect_Glassmorphism") ?? "Glassmorphism";
            if (LanguageDescription != null) LanguageDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Language_Description") ?? string.Empty;
            if (DefaultResolutionOnStartDescription != null) DefaultResolutionOnStartDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultResolutionOnStart_Description") ?? string.Empty;
            if (DefaultStartResolutionDescription != null) DefaultStartResolutionDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultStartResolution_Description") ?? string.Empty;
            if (XXMIRootDirectoryDescription != null) XXMIRootDirectoryDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_XXMIRootDirectory_Description") ?? string.Empty;
                        if (SkipXXMILauncherDescription != null) SkipXXMILauncherDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_SkipXXMILauncher_Description") ?? string.Empty;
            if (ActiveModsToTopDescription != null) ActiveModsToTopDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ActiveModsToTop_Description") ?? string.Empty;
            if (AutoDeactivateConflictingModsDescription != null) AutoDeactivateConflictingModsDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoDeactivateConflictingMods_Description") ?? string.Empty;
            if (DynamicModSearchDescription != null) DynamicModSearchDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Description") ?? string.Empty;
            if (ShowOrangeAnimationDescription != null) ShowOrangeAnimationDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShowOrangeAnimation_Description") ?? string.Empty;
            if (ModGridZoomDescription != null) ModGridZoomDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModGridZoom_Description") ?? string.Empty;
            if (GridLoggingDescription != null) GridLoggingDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Description") ?? string.Empty;
            if (ErrorOnlyLoggingDescription != null) ErrorOnlyLoggingDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ErrorOnlyLogging_Description") ?? string.Empty;
            if (MinimizeToTrayDescription != null) MinimizeToTrayDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MinimizeToTray_Description") ?? string.Empty;
            if (BlurNSFWDescription != null) BlurNSFWDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HideNSFW_Description") ?? string.Empty;
            if (FastDownloadDescription != null) FastDownloadDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_FastDownload_Description") ?? string.Empty;
            if (MaxConnectionsDescription != null) MaxConnectionsDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MaxConnections_Description") ?? string.Empty;
            
            // ToggleSwitch labels - set initial state
            UpdateToggleLabels();
            
            // Theme SelectorBar texts - use null checks
            if (ThemeSelectorAutoText != null) ThemeSelectorAutoText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Auto");
            if (ThemeSelectorLightText != null) ThemeSelectorLightText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Light");
            if (ThemeSelectorDarkText != null) ThemeSelectorDarkText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Dark");
            
            // Backdrop SelectorBar texts - already have null checks
            if (BackdropSelectorMicaText != null) BackdropSelectorMicaText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_Mica");
            if (BackdropSelectorMicaAltText != null) BackdropSelectorMicaAltText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_MicaAlt");
            if (BackdropSelectorAcrylicText != null) BackdropSelectorAcrylicText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_Acrylic");
            if (BackdropSelectorAcrylicThinText != null) BackdropSelectorAcrylicThinText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_AcrylicThin");

            if (BackdropSelectorNoneText != null) BackdropSelectorNoneText.Text = SharedUtilities.GetTranslation(lang, "None");
            
            // Tooltips - use null checks
            if (ModGridZoomToggle != null) ToolTipService.SetToolTip(ModGridZoomToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_ModGridZoom_Tooltip"));
            if (GridLoggingToggle != null) ToolTipService.SetToolTip(GridLoggingToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Tooltip"));

            if (ActiveModsToTopToggle != null) ToolTipService.SetToolTip(ActiveModsToTopToggle, SharedUtilities.GetTranslation(lang, "ActiveModsToTop_Tooltip"));
            if (XXMIRootDirectoryDefaultButton != null) ToolTipService.SetToolTip(XXMIRootDirectoryDefaultButton, SharedUtilities.GetTranslation(lang, "SettingsPage_RestoreDefault_Tooltip"));
            if (XXMIRootDirectoryPickButton != null) ToolTipService.SetToolTip(XXMIRootDirectoryPickButton, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
                        if (XXMIRootDirectoryBreadcrumb != null) ToolTipService.SetToolTip(XXMIRootDirectoryBreadcrumb, SharedUtilities.GetTranslation(lang, "OpenDirectory_Tooltip"));
                        if (DynamicModSearchToggle != null) ToolTipService.SetToolTip(DynamicModSearchToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Tooltip"));
        }

        private void LoadLanguages()
        {
            try
            {
                LanguageComboBox.Items.Clear();
                _languages.Clear();
                _fileNameByDisplayName.Clear();
                
                // Remove AUTO option - auto-detection happens automatically on first app start
                
                if (Directory.Exists(LanguageFolderPath))
                {
                    var files = Directory.GetFiles(LanguageFolderPath, "*.json");
                    foreach (var file in files)
                    {
                        string displayName = System.IO.Path.GetFileNameWithoutExtension(file);
                        try
                        {
                            // Use faster file reading with smaller buffer for language files
                            var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                            if (dict != null && dict.TryGetValue("Language_DisplayName", out var langName) && !string.IsNullOrWhiteSpace(langName))
                            {
                                displayName = langName;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to parse language file: {file}", ex);
                            // Continue with filename as display name
                        }
                        
                        LanguageComboBox.Items.Add(displayName);
                        _languages[displayName] = file;
                        _fileNameByDisplayName[displayName] = file;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load languages", ex);
                // Ensure at least one language is available
                if (LanguageComboBox.Items.Count == 0)
                {
                    LanguageComboBox.Items.Add("English");
                    _languages["English"] = "en.json";
                    _fileNameByDisplayName["English"] = "en.json";
                }
            }
        }

        private async void LoadCurrentSettings()
        {
            // Set ComboBox to selected language from settings
            string? selectedFile = SettingsManager.Current.LanguageFile;
            string displayName = string.Empty;
            
            // Find the display name for the current language file
            if (!string.IsNullOrEmpty(selectedFile) && selectedFile != "auto")
            {
                displayName = _fileNameByDisplayName.FirstOrDefault(x => System.IO.Path.GetFileName(x.Value) == selectedFile).Key ?? string.Empty;
            }
            
            if (!string.IsNullOrEmpty(displayName))
                LanguageComboBox.SelectedItem = displayName;
            else if (LanguageComboBox.Items.Count > 0)
                LanguageComboBox.SelectedIndex = 0;
                
            // Small delay to ensure SelectorBar controls are fully loaded
            await Task.Delay(100);
                
            // Set theme SelectorBar to selected from settings
            string theme = SettingsManager.Current.Theme ?? "Auto";
            ThemeSelectorBar.SelectionChanged -= ThemeSelectorBar_SelectionChanged; // Temporarily unsubscribe
            
            // Force layout update to ensure all items are rendered
            ThemeSelectorBar.UpdateLayout();
            ThemeSelectorBar.InvalidateArrange();
            
            foreach (SelectorBarItem item in ThemeSelectorBar.Items)
            {
                if ((string)item.Tag == theme)
                {
                    ThemeSelectorBar.SelectedItem = item;
                    break;
                }
            }
            ThemeSelectorBar.SelectionChanged += ThemeSelectorBar_SelectionChanged; // Re-subscribe
            
            // Set backdrop SelectorBar to selected from settings
            string backdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            BackdropSelectorBar.SelectionChanged -= BackdropSelectorBar_SelectionChanged; // Temporarily unsubscribe
            
            // Force layout update to ensure all items are rendered
            BackdropSelectorBar.UpdateLayout();
            BackdropSelectorBar.InvalidateArrange();
            
            foreach (SelectorBarItem item in BackdropSelectorBar.Items)
            {
                if ((string)item.Tag == backdrop)
                {
                    BackdropSelectorBar.SelectedItem = item;
                    break;
                }
            }
            BackdropSelectorBar.SelectionChanged += BackdropSelectorBar_SelectionChanged; // Re-subscribe
            
            // Set preview effect ComboBox to selected from settings
            string previewEffect = SettingsManager.Current.PreviewEffect ?? "None";
            PreviewEffectComboBox.SelectionChanged -= PreviewEffectComboBox_SelectionChanged; // Temporarily unsubscribe
            foreach (ComboBoxItem item in PreviewEffectComboBox.Items)
            {
                if ((string)item.Tag == previewEffect)
                {
                    PreviewEffectComboBox.SelectedItem = item;
                    break;
                }
            }
            PreviewEffectComboBox.SelectionChanged += PreviewEffectComboBox_SelectionChanged; // Re-subscribe
            
            // Small delay to let SelectorBars fully render
            await Task.Delay(50);
            
            // Force final layout update
            ThemeSelectorBar.UpdateLayout();
            BackdropSelectorBar.UpdateLayout();
            
            // Set toggle states from settings
            DynamicModSearchToggle.IsOn = SettingsManager.Current.DynamicModSearchEnabled;
            GridLoggingToggle.IsOn = SettingsManager.Current.GridLoggingEnabled;
            ErrorOnlyLoggingToggle.IsOn = SettingsManager.Current.ErrorOnlyLogging;
            MinimizeToTrayToggle.IsOn = SettingsManager.Current.MinimizeToTrayEnabled;
            BlurNSFWToggle.IsOn = SettingsManager.Current.BlurNSFWThumbnails;
            HotkeysEnabledToggle.IsOn = SettingsManager.Current.HotkeysEnabled;
            
            // Set download settings
            FastDownloadToggle.IsOn = SettingsManager.GetFastDownloadEnabled();
            MaxConnectionsNumberBox.Value = SettingsManager.GetMaxDownloadConnections();
            
            // Update hotkeys section state
            UpdateHotkeysSectionState(SettingsManager.Current.HotkeysEnabled);

            ShowOrangeAnimationToggle.IsOn = SettingsManager.Current.ShowOrangeAnimation;
            ModGridZoomToggle.IsOn = SettingsManager.Current.ModGridZoomEnabled;
            SkipXXMILauncherToggle.IsOn = SettingsManager.Current.SkipXXMILauncherEnabled;
            ActiveModsToTopToggle.IsOn = SettingsManager.Current.ActiveModsToTopEnabled;
            AutoDeactivateConflictingModsToggle.IsOn = SettingsManager.Current.AutoDeactivateConflictingMods;
            
            // Set BreadcrumbBar paths
            SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, SettingsManager.GetCurrentGameXXMIRoot());
            // ModLibraryDirectoryBreadcrumb removed - no longer needed
            
            // Load hotkeys using new manager
            LoadSettingsHotkeys();
            
            // Set default resolution on start settings
            DefaultResolutionOnStartToggle.IsOn = SettingsManager.Current.UseDefaultResolutionOnStart;
            
            // Show current window size instead of stored values
            var mainWindow = (App.Current as App)?.MainWindow;
            if (mainWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                if (appWindow != null)
                {
                    DefaultStartWidthTextBox.Text = appWindow.Size.Width.ToString();
                    DefaultStartHeightTextBox.Text = appWindow.Size.Height.ToString();
                }
                else
                {
                    // Fallback to stored values if can't get current size
                    DefaultStartWidthTextBox.Text = SettingsManager.Current.DefaultStartWidth.ToString();
                    DefaultStartHeightTextBox.Text = SettingsManager.Current.DefaultStartHeight.ToString();
                }
            }
            else
            {
                // Fallback to stored values if can't access MainWindow
                DefaultStartWidthTextBox.Text = SettingsManager.Current.DefaultStartWidth.ToString();
                DefaultStartHeightTextBox.Text = SettingsManager.Current.DefaultStartHeight.ToString();
            }
            
            // Enable/disable resolution input boxes based on toggle state
            DefaultStartWidthTextBox.IsEnabled = SettingsManager.Current.UseDefaultResolutionOnStart;
            DefaultStartHeightTextBox.IsEnabled = SettingsManager.Current.UseDefaultResolutionOnStart;
        }



        // Event handlers - copying from original SettingsPage
        private async void ThemeSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            Logger.LogInfo("ThemeSelectorBar_SelectionChanged called");
            
            if (sender.SelectedItem is SelectorBarItem item && item.Tag is string theme)
            {
                Logger.LogInfo($"Theme selected: {theme}");
                // Get current effective theme before change
                ElementTheme currentEffectiveTheme = GetEffectiveTheme(SettingsManager.Current.Theme ?? "Auto");
                
                SettingsManager.Current.Theme = theme;
                SettingsManager.Save();
                
                // Set application theme first
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ApplyThemeToTitleBar(theme);
                    if (mainWindow.Content is FrameworkElement root)
                    {
                        if (theme == "Light")
                            root.RequestedTheme = ElementTheme.Light;
                        else if (theme == "Dark")
                            root.RequestedTheme = ElementTheme.Dark;
                        else
                            root.RequestedTheme = ElementTheme.Default;
                        
                        // Small delay to let the theme change take effect
                        await Task.Delay(10);
                        
                        // Now get the new effective theme after the change
                        ElementTheme newEffectiveTheme = GetEffectiveThemeAfterChange(theme, root);
                        
                        // Refresh backdrop effect if using "None" to update background color
                        string currentBackdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
                        if (currentBackdrop == "None")
                        {
                            mainWindow.ApplyBackdropEffect("None");
                        }
                        
                        // Notify other windows about theme change
                        WindowStyleHelper.NotifySettingsChanged();
                        
                        // Only update panel theme if the effective theme actually changed
                        if (currentEffectiveTheme != newEffectiveTheme)
                        {
                            Logger.LogInfo($"Theme changed from {currentEffectiveTheme} to {newEffectiveTheme}");
                            
                            // Small delay to ensure theme change is fully applied
                            await Task.Delay(100);
                            
                            // Update the sliding panel background instead of closing/reopening
                            Logger.LogInfo("Calling UpdateSlidingPanelTheme");
                            mainWindow.UpdateSlidingPanelTheme();
                        }
                        else
                        {
                            Logger.LogInfo($"Theme not changed - current: {currentEffectiveTheme}, new: {newEffectiveTheme}");
                        }
                    }
                }
            }
        }
        
        private ElementTheme GetEffectiveThemeAfterChange(string themeSetting, FrameworkElement root)
        {
            if (themeSetting == "Light")
                return ElementTheme.Light;
            else if (themeSetting == "Dark")
                return ElementTheme.Dark;
            else // "Auto"
            {
                // For Auto, get the actual theme that will be applied
                return root.ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
            }
        }
        
        private ElementTheme GetEffectiveTheme(string themeSetting)
        {
            if (themeSetting == "Light")
                return ElementTheme.Light;
            else if (themeSetting == "Dark")
                return ElementTheme.Dark;
            else // "Auto"
            {
                // For Auto, determine based on current system/app theme
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    if (mainWindow.Content is FrameworkElement root)
                    {
                        // Get the actual current theme being displayed
                        return root.ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                    }
                }
                // Fallback to Light if we can't determine
                return ElementTheme.Light;
            }
        }

        private void BackdropSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is SelectorBarItem item && item.Tag is string backdrop)
            {
                SettingsManager.Current.BackdropEffect = backdrop;
                SettingsManager.Save();
                
                // Apply backdrop change
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ApplyBackdropEffect(backdrop);
                    
                    // Notify other windows about settings change
                    WindowStyleHelper.NotifySettingsChanged();
                }
            }
        }

        private void PreviewEffectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PreviewEffectComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string effect)
            {
                SettingsManager.Current.PreviewEffect = effect;
                SettingsManager.Save();
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is string displayName && _fileNameByDisplayName.TryGetValue(displayName, out var filePath))
            {
                var fileName = Path.GetFileName(filePath);
                
                // IMPORTANT: Save to SettingsManager FIRST before loading language
                SettingsManager.Current.LanguageFile = fileName;
                SettingsManager.Save();
                
                // Update texts locally first to avoid flicker
                UpdateTexts();
                
                // Refresh the entire UI in MainWindow without re-navigating to settings
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.RefreshUIAfterLanguageChange();
                    // No need to navigate back to SettingsPage - we're already here and updated
                }
            }
        }

        // Static versions of processing methods for hotkey use
        public static void ProcessCategoryPreviewStatic(string categoryDir, OptimizationMode mode = OptimizationMode.Standard)
        {
            // Standard mode: optimize quality, generate catprev.jpg and catmini.jpg
            // CategoryFull mode: same but with manual crop inspection (handled by ImageOptimizationService)
            
            // Create backup if enabled
            if (SettingsManager.Current.ImageOptimizerCreateBackups)
            {
                var filesToBackup = Directory.GetFiles(categoryDir)
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("preview") || fileName.StartsWith("catprev") || fileName.StartsWith("catmini")) &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                if (filesToBackup.Count > 0)
                {
                    CreateBackupZip(categoryDir, filesToBackup);
                }
            }
            
            var catprevJpgPath = Path.Combine(categoryDir, "catprev.jpg");
            
            // Look for existing catprev files (catprev.png, catprev.jpg) and other preview files
            var catprevFiles = Directory.GetFiles(categoryDir)
                .Where(f => 
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return fileName.StartsWith("catprev") &&
                           (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();
            
            // Look for other preview files in category directory (catpreview.*, preview.*, etc.)
            var otherPreviewFiles = Directory.GetFiles(categoryDir)
                .Where(f => 
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return (fileName.StartsWith("catpreview") || fileName.StartsWith("preview")) &&
                           !fileName.StartsWith("catprev") &&
                           (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();
            
            // Combine all preview files, prioritizing catprev files
            var allPreviewFiles = catprevFiles.Concat(otherPreviewFiles).ToArray();
            
            if (allPreviewFiles.Length == 0) return;
            
            // Check if we need to optimize existing catprev.jpg
            bool needsOptimization = true;
            if (File.Exists(catprevJpgPath))
            {
                try
                {
                    using (var img = System.Drawing.Image.FromFile(catprevJpgPath))
                    {
                        // Consider optimized if it's 600x722
                        needsOptimization = !(img.Width == 600 && img.Height == 722);
                    }
                }
                catch
                {
                    needsOptimization = true;
                }
            }
            
            // Skip if catprev.jpg already exists and is optimized, and no other catprev files to process
            if (!needsOptimization && catprevFiles.Length <= 1 && catprevFiles.All(f => f.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase)))
                return;
            
            var previewPath = allPreviewFiles[0]; // Take first preview file found
            
            try
            {
                // Create temporary path if we're optimizing existing catprev.jpg
                var tempPath = catprevJpgPath;
                if (previewPath.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase))
                {
                    tempPath = Path.Combine(categoryDir, "catprev_temp.jpg");
                }
                
                using (var img = System.Drawing.Image.FromFile(previewPath))
                {
                    // Create catprev.jpg (722x722 for category preview)
                    using (var thumbBmp = new System.Drawing.Bitmap(722, 722))
                    using (var g = System.Drawing.Graphics.FromImage(thumbBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Calculate crop rectangle using selected algorithm
                        var cropTypeStr = SettingsManager.Current.ImageCropType ?? "Center";
                        var cropType = Enum.TryParse<Services.CropType>(cropTypeStr, out var parsed) ? parsed : Services.CropType.Center;
                        var srcRect = Services.ImageCropService.CalculateCropRectangle(img, 722, 722, cropType);
                        var destRect = new System.Drawing.Rectangle(0, 0, 722, 722);
                        g.DrawImage(img, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                        
                        // Save as JPEG catprev
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                            thumbBmp.Save(tempPath, jpegEncoder, jpegParams);
                        }
                    }
                    
                    // Create catmini.jpg (600x722 for category tiles) from same source
                    var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                    using (var miniThumb = new System.Drawing.Bitmap(600, 722))
                    using (var g2 = System.Drawing.Graphics.FromImage(miniThumb))
                    {
                        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g2.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g2.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Calculate crop rectangle using selected algorithm
                        var cropTypeStr2 = SettingsManager.Current.ImageCropType ?? "Center";
                        var cropType2 = Enum.TryParse<Services.CropType>(cropTypeStr2, out var parsed2) ? parsed2 : Services.CropType.Center;
                        var srcRect2 = Services.ImageCropService.CalculateCropRectangle(img, 600, 722, cropType2);
                        var destRect2 = new System.Drawing.Rectangle(0, 0, 600, 722);
                        g2.DrawImage(img, destRect2, srcRect2, System.Drawing.GraphicsUnit.Pixel);
                        
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                            miniThumb.Save(catminiPath, jpegEncoder, jpegParams);
                        }
                    }
                }
                
                // Handle file replacement if we used temp path
                if (!tempPath.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(catprevJpgPath))
                        File.Delete(catprevJpgPath);
                    File.Move(tempPath, catprevJpgPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview in {categoryDir}", ex);
            }
        }

        private static void GenerateCategoryMiniaturesOnly(string categoryDir, string previewPath)
        {
            try
            {
                Logger.LogInfo($"Generating category miniatures only for: {categoryDir}");
                
                using (var img = System.Drawing.Image.FromFile(previewPath))
                {
                    // Generate catprev.jpg (722x722)
                    var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                    using (var thumbBmp = new System.Drawing.Bitmap(722, 722))
                    using (var g = System.Drawing.Graphics.FromImage(thumbBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Square crop for catprev
                        int size = Math.Min(img.Width, img.Height);
                        int x = (img.Width - size) / 2;
                        int y = (img.Height - size) / 2;
                        
                        var srcRect = new System.Drawing.Rectangle(x, y, size, size);
                        var destRect = new System.Drawing.Rectangle(0, 0, 722, 722);
                        g.DrawImage(img, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                        
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                            thumbBmp.Save(catprevPath, jpegEncoder, jpegParams);
                        }
                    }
                    
                    // Generate catmini.jpg (600x722)
                    var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                    using (var miniThumb = new System.Drawing.Bitmap(600, 722))
                    using (var g2 = System.Drawing.Graphics.FromImage(miniThumb))
                    {
                        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g2.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g2.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        double targetRatio = 600.0 / 722.0;
                        double sourceRatio = (double)img.Width / img.Height;
                        int srcWidth, srcHeight, srcX, srcY;
                        if (sourceRatio > targetRatio)
                        {
                            srcHeight = img.Height;
                            srcWidth = (int)(srcHeight * targetRatio);
                            srcX = (img.Width - srcWidth) / 2;
                            srcY = 0;
                        }
                        else
                        {
                            srcWidth = img.Width;
                            srcHeight = (int)(srcWidth / targetRatio);
                            srcX = 0;
                            srcY = (img.Height - srcHeight) / 2;
                        }
                        var srcRect = new System.Drawing.Rectangle(srcX, srcY, srcWidth, srcHeight);
                        var destRect = new System.Drawing.Rectangle(0, 0, 600, 722);
                        g2.DrawImage(img, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                        
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                            miniThumb.Save(catminiPath, jpegEncoder, jpegParams);
                        }
                    }
                }
                
                Logger.LogInfo("Category miniatures generated successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to generate category miniatures for {categoryDir}", ex);
            }
        }
        
        private static void CopyPreviewFilesStatic(string modDir)
        {
            try
            {
                Logger.LogInfo($"Copying preview files (no optimization) in: {modDir}");
                
                // Find all preview image files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f)
                    .ToList();
                
                if (previewFiles.Count == 0) return;
                
                // Copy files to standard names without optimization
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    var ext = Path.GetExtension(sourceFile);
                    string targetFileName = i == 0 ? $"preview{ext}" : $"preview-{i:D2}{ext}";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    if (!sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(sourceFile, targetPath, true);
                        Logger.LogInfo($"Copied: {Path.GetFileName(sourceFile)} -> {targetFileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to copy preview files in {modDir}", ex);
            }
        }
        
        private static void RenamePreviewFilesStatic(string modDir)
        {
            try
            {
                Logger.LogInfo($"Renaming preview files in: {modDir}");
                
                // Find all preview image files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f)
                    .ToList();
                
                if (previewFiles.Count == 0) return;
                
                // Rename files to standard names
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    var ext = Path.GetExtension(sourceFile);
                    string targetFileName = i == 0 ? $"preview{ext}" : $"preview-{i:D2}{ext}";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    if (!sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Use temp name to avoid conflicts
                        var tempPath = Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{ext}");
                        File.Move(sourceFile, tempPath);
                        File.Move(tempPath, targetPath);
                        Logger.LogInfo($"Renamed: {Path.GetFileName(sourceFile)} -> {targetFileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to rename preview files in {modDir}", ex);
            }
        }
        
        private static void CreateBackupZip(string directory, List<string> filesToBackup)
        {
            try
            {
                if (filesToBackup.Count == 0) return;
                
                // Create backup filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"preview_backup_{timestamp}.zip";
                var backupPath = Path.Combine(directory, backupFileName);
                
                // Create ZIP archive
                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    foreach (var file in filesToBackup)
                    {
                        if (File.Exists(file))
                        {
                            var entryName = Path.GetFileName(file);
                            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                            Logger.LogInfo($"Added to backup: {entryName}");
                        }
                    }
                }
                
                Logger.LogInfo($"Created backup: {backupFileName}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create backup ZIP - aborting optimization", ex);
                throw; // Re-throw to abort optimization
            }
        }

        public static void ProcessModPreviewImagesStatic(string modDir, OptimizationMode mode = OptimizationMode.Standard)
        {
            try
            {
                // Check available disk space before optimization
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(modDir) ?? "C:\\");
                    long availableSpace = drive.AvailableFreeSpace;
                    long requiredSpace = 100 * 1024 * 1024; // 100 MB minimum
                    
                    if (availableSpace < requiredSpace)
                    {
                        Logger.LogError($"Insufficient disk space: {availableSpace / (1024 * 1024)} MB available, {requiredSpace / (1024 * 1024)} MB required");
                        throw new IOException($"Insufficient disk space. Available: {availableSpace / (1024 * 1024)} MB, Required: {requiredSpace / (1024 * 1024)} MB");
                    }
                }
                catch (Exception ex) when (!(ex is IOException))
                {
                    Logger.LogWarning($"Could not check disk space: {ex.Message}");
                    // Continue anyway if we can't check disk space
                }
                
                // Standard mode: optimize quality, generate preview.jpg and minitile.jpg
                // Create backup if enabled
                if (SettingsManager.Current.ImageOptimizerCreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(modDir)
                        .Where(f => 
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return (fileName.StartsWith("preview") || fileName == "minitile.jpg") &&
                                   (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                    
                    if (filesToBackup.Count > 0)
                    {
                        CreateBackupZip(modDir, filesToBackup);
                    }
                }
                
                // Find all preview*.png and preview*.jpg files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f) // Sort to ensure consistent ordering
                    .ToList();

                if (previewFiles.Count == 0) return;

                var minitileJpgPath = Path.Combine(modDir, "minitile.jpg");
                bool needsMinitile = !File.Exists(minitileJpgPath);

                // Process each preview file
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);

                    // Skip if target already exists and is optimized
                    if (File.Exists(targetPath) && IsImageOptimizedStatic(targetPath))
                    {
                        // Only create minitile for main preview if missing
                        if (i == 0 && needsMinitile)
                        {
                            CreateMinitileStatic(targetPath, minitileJpgPath);
                        }
                        continue;
                    }

                    // Standard optimization
                    try
                    {
                        OptimizePreviewImageStatic(sourceFile, targetPath, mode);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to optimize {sourceFile}, continuing with remaining images", ex);
                        continue; // Skip this image and continue with next
                    }

                    // Create minitile only for the main preview (index 0)
                    if (i == 0)
                    {
                        try
                        {
                            CreateMinitileStatic(targetPath, minitileJpgPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create minitile for {targetPath}, continuing", ex);
                        }
                    }
                }
                
                // Clean up any extra preview files beyond the limit
                var existingPreviews = Directory.GetFiles(modDir, "preview*.*")
                    .Where(f => 
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        var ext = Path.GetExtension(f).ToLower();
                        
                        // Skip if not an image file
                        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") return false;
                        
                        if (name == "preview") return false; // Keep main preview
                        if (name.StartsWith("preview-"))
                        {
                            var suffix = name.Substring(8); // Remove "preview-"
                            return !int.TryParse(suffix, out int num) || num > (AppConstants.MAX_PREVIEW_IMAGES - 1);
                        }
                        return true; // Remove other preview files
                    })
                    .ToList();

                foreach (var extraFile in existingPreviews)
                {
                    try
                    {
                        File.Delete(extraFile);
                        Logger.LogInfo($"Deleted excess preview file: {Path.GetFileName(extraFile)}");
                    }
                    catch (Exception deleteEx)
                    {
                        Logger.LogError($"Failed to delete excess file: {extraFile}", deleteEx);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process preview images in {modDir}", ex);
            }
        }

        private static bool IsImageOptimizedStatic(string imagePath)
        {
            try
            {
                using (var img = System.Drawing.Image.FromFile(imagePath))
                {
                    // Consider optimized if it's square and not larger than 1000x1000
                    return img.Width == img.Height && img.Width <= 1000;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void OptimizePreviewImageStatic(string sourcePath, string targetPath, OptimizationMode mode = OptimizationMode.Standard)
        {
            try
            {
                Logger.LogInfo($"Optimizing image: {sourcePath} -> {targetPath} (Mode: {mode})");
                
                if (!File.Exists(sourcePath))
                {
                    Logger.LogError($"Source file does not exist: {sourcePath}");
                    return;
                }
                
                using (var src = System.Drawing.Image.FromFile(sourcePath))
                {
                // Standard mode: optimize quality without cropping/resizing
                bool shouldCropAndResize = false;
                
                // Crop to square (1:1 ratio) if needed (disabled in Standard mode)
                bool needsCrop = shouldCropAndResize && (src.Width != src.Height);
                
                System.Drawing.Image squareImage = src;
                if (needsCrop)
                {
                    // Get crop type from settings
                    var cropTypeStr = SettingsManager.Current.ImageCropType ?? "Center";
                    var cropType = Enum.TryParse<Services.CropType>(cropTypeStr, out var parsed) 
                        ? parsed 
                        : Services.CropType.Center;
                    
                    // Calculate crop rectangle using the selected algorithm
                    int cropSize = Math.Min(src.Width, src.Height);
                    var cropRect = Services.ImageCropService.CalculateCropRectangle(src, cropSize, cropSize, cropType);
                    
                    var cropped = new System.Drawing.Bitmap(cropRect.Width, cropRect.Height);
                    using (var g = System.Drawing.Graphics.FromImage(cropped))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        var destRect = new System.Drawing.Rectangle(0, 0, cropRect.Width, cropRect.Height);
                        g.DrawImage(src, destRect, cropRect, System.Drawing.GraphicsUnit.Pixel);
                    }
                    squareImage = cropped;
                }

                // Resize if larger than 1000x1000 (disabled in Standard mode)
                int currentSize = squareImage.Width; // After cropping, image is square
                int targetSize = shouldCropAndResize ? Math.Min(currentSize, 1000) : currentSize;
                System.Drawing.Image finalImage = squareImage;
                
                if (shouldCropAndResize && currentSize > 1000)
                {
                    var resized = new System.Drawing.Bitmap(targetSize, targetSize);
                    using (var g = System.Drawing.Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(squareImage, 0, 0, targetSize, targetSize);
                    }
                    finalImage = resized;
                    if (squareImage != src) squareImage.Dispose();
                }

                // Step 3: Save as JPEG
                var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                if (jpegEncoder != null)
                {
                    Logger.LogInfo($"Saving to {targetPath}");
                    var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                    jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                    finalImage.Save(targetPath, jpegEncoder, jpegParams);
                    Logger.LogInfo("Image saved successfully");
                }
                else
                {
                    Logger.LogError("JPEG encoder not found");
                }

                if (finalImage != src) finalImage.Dispose();
                
                Logger.LogInfo("Image optimization completed successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to optimize image {sourcePath}", ex);
                // Don't re-throw - let caller handle continuation
            }
            
            // Delete original file if it's different from target (unless KeepOriginals is enabled)
            if (!SettingsManager.Current.ImageOptimizerKeepOriginals)
            {
                try
                {
                    if (!sourcePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(sourcePath))
                    {
                        File.Delete(sourcePath);
                        Logger.LogInfo($"Deleted original preview: {sourcePath}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to delete original preview: {sourcePath}", ex);
                }
            }
            else
            {
                Logger.LogInfo($"Keeping original file: {sourcePath}");
            }
        }

        private static void CreateMinitileStatic(string sourcePath, string minitilePath)
        {
            try
            {
                using (var src = System.Drawing.Image.FromFile(sourcePath))
                using (var minitile = new System.Drawing.Bitmap(600, 722))
                using (var g = System.Drawing.Graphics.FromImage(minitile))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    
                    // Get crop type from settings
                    var cropTypeStr = SettingsManager.Current.ImageCropType ?? "Center";
                    var cropType = Enum.TryParse<Services.CropType>(cropTypeStr, out var parsed) 
                        ? parsed 
                        : Services.CropType.Center;
                    
                    // Calculate crop rectangle using the selected algorithm
                    var srcRect = Services.ImageCropService.CalculateCropRectangle(src, 600, 722, cropType);
                    var destRect = new System.Drawing.Rectangle(0, 0, 600, 722);
                    g.DrawImage(src, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                    
                    var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                        jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                        minitile.Save(minitilePath, jpegEncoder, jpegParams);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create minitile: {minitilePath}", ex);
            }
        }

        private async Task XXMIRootDirectoryPickButton_ClickAsync(Button senderButton)
        {
            senderButton.IsEnabled = false;
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var hwnd = SharedUtilities.GetMainWindowHandle();
                var folderPath = await SharedUtilities.PickFolderAsync(hwnd, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
                if (!string.IsNullOrEmpty(folderPath))
                {
                    // Clean up from current mods directory before switching

                    
                    // Set XXMI root for current game
                    SettingsManager.SetCurrentGameXXMIRoot(folderPath);
                    SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, folderPath);
                    // RecreateSymlinksFromActiveMods removed - no longer needed
                    
                    FlairX_Mod_Manager.Logger.LogInfo($"Changed XXMI root directory to: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to change XXMI root directory", ex);
            }
            senderButton.IsEnabled = true;
        }

        private async void XXMIRootDirectoryPickButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button senderButton)
                await XXMIRootDirectoryPickButton_ClickAsync(senderButton);
        }

        private void XXMIRootDirectoryDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (string.IsNullOrEmpty(gameTag))
                return;

            var currentModsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            var defaultXXMIRoot = @".\XXMI";
            var defaultModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
            
            // If already using default, do nothing
            if (string.Equals(Path.GetFullPath(currentModsPath), Path.GetFullPath(defaultModsPath), StringComparison.OrdinalIgnoreCase))
                return;



            // Remove custom XXMI root for current game to use default
            SettingsManager.Current.GameXXMIRootPaths.Remove(gameTag);
            SettingsManager.Save();
            
            SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, defaultXXMIRoot);
            // RecreateSymlinksFromActiveMods removed - no longer needed
            
            FlairX_Mod_Manager.Logger.LogInfo($"Restored XXMI root to default: {defaultXXMIRoot}");
        }

        private void XXMIRootDirectoryBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var path = GetBreadcrumbPath(sender);
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (!string.IsNullOrEmpty(gameTag))
            {
                SettingsManager.Current.GameXXMIRootPaths[gameTag] = path;
                SettingsManager.Save();
            }
        }

        // ModLibrary methods removed - mods stored in XXMI/Mods only

        private void ActiveModsToTopToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ActiveModsToTopEnabled = ActiveModsToTopToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
        }

        private void AutoDeactivateConflictingModsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.AutoDeactivateConflictingMods = AutoDeactivateConflictingModsToggle.IsOn;
            
            // Reset "don't ask again" preference when toggling this setting
            // This allows the dialog to show again if user re-enables manual conflict resolution
            SettingsManager.Current.DontAskCategoryConflictAgain = false;
            
            SettingsManager.Save();
            UpdateToggleLabels();
            
            Logger.LogInfo($"AutoDeactivateConflictingMods toggled to {AutoDeactivateConflictingModsToggle.IsOn}, reset DontAskCategoryConflictAgain");
        }

        private void DynamicModSearchToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.DynamicModSearchEnabled = DynamicModSearchToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
        }

        private void ShowOrangeAnimationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ShowOrangeAnimation = ShowOrangeAnimationToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
            
            // Refresh animation in MainWindow
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateOrangeAnimationVisibility(ShowOrangeAnimationToggle.IsOn);
            }
        }

        private void ModGridZoomToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ModGridZoomEnabled = ModGridZoomToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();

            // If the user disabled zooming, reset zoom to 100% immediately
            if (!ModGridZoomToggle.IsOn)
            {
                try
                {
                    var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        // contentFrame is a private field on MainWindow; use reflection to access it safely
                        var field = mainWindow.GetType().GetField("contentFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            var frame = field.GetValue(mainWindow) as Microsoft.UI.Xaml.Controls.Frame;
                            var modGridPage = frame?.Content as FlairX_Mod_Manager.Pages.ModGridPage;
                            modGridPage?.ResetZoom();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to reset mod grid zoom after disabling zoom setting", ex);
                }
            }
        }

        private void GridLoggingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.GridLoggingEnabled = GridLoggingToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
        }

        private void ErrorOnlyLoggingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ErrorOnlyLogging = ErrorOnlyLoggingToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
        }

        private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.MinimizeToTrayEnabled = MinimizeToTrayToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
        }

        private void BlurNSFWToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.BlurNSFWThumbnails = BlurNSFWToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
            
            // Dynamically filter NSFW mods in ModGridPage
            var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
            if (mainWindow?.CurrentModGridPage != null)
            {
                mainWindow.CurrentModGridPage.FilterNSFWMods(BlurNSFWToggle.IsOn);
            }
        }

        private void HotkeysEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.HotkeysEnabled = HotkeysEnabledToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
            
            // Update hotkey section UI state
            UpdateHotkeysSectionState(HotkeysEnabledToggle.IsOn);
            
            // Refresh global hotkeys in MainWindow
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshGlobalHotkeys();
            }
        }

        private void ProcessModPreviewImages(string modDir)
        {
            try
            {
                // Create backup if enabled
                if (SettingsManager.Current.ImageOptimizerCreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(modDir)
                        .Where(f => 
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return (fileName.StartsWith("preview") || fileName == "minitile.jpg") &&
                                   (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                    
                    if (filesToBackup.Count > 0)
                    {
                        CreateBackupZip(modDir, filesToBackup);
                    }
                }
                
                // Find all preview*.png and preview*.jpg files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f) // Sort to ensure consistent ordering
                    .ToList();

                if (previewFiles.Count == 0) return;

                var minitileJpgPath = Path.Combine(modDir, "minitile.jpg");
                bool needsMinitile = !File.Exists(minitileJpgPath);

                // Process each preview file
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);

                    // Skip if target already exists and is optimized
                    if (File.Exists(targetPath) && IsImageOptimized(targetPath))
                    {
                        // Only create minitile for main preview if missing
                        if (i == 0 && needsMinitile)
                        {
                            CreateMinitile(targetPath, minitileJpgPath);
                        }
                        continue;
                    }

                    // Optimize and save the image
                    OptimizePreviewImage(sourceFile, targetPath);

                    // Create minitile only for the main preview (index 0)
                    if (i == 0)
                    {
                        CreateMinitile(targetPath, minitileJpgPath);
                    }

                    // Delete original file if it's different from target (unless KeepOriginals is enabled)
                    if (!SettingsManager.Current.ImageOptimizerKeepOriginals)
                    {
                        if (!sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Delete(sourceFile);
                                Logger.LogInfo($"Deleted original preview: {sourceFile}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to delete original preview: {sourceFile}", ex);
                            }
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"Keeping original file: {sourceFile}");
                    }
                }

                // Clean up any extra preview files beyond the limit
                var existingPreviews = Directory.GetFiles(modDir, "preview*.*")
                    .Where(f => 
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        var ext = Path.GetExtension(f).ToLower();
                        
                        // Skip if not an image file
                        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") return false;
                        
                        if (name == "preview") return false; // Keep main preview
                        if (name.StartsWith("preview-"))
                        {
                            var suffix = name.Substring(8); // Remove "preview-"
                            return !int.TryParse(suffix, out int num) || num > (AppConstants.MAX_PREVIEW_IMAGES - 1);
                        }
                        return true; // Remove other preview files
                    })
                    .ToList();

                foreach (var extraFile in existingPreviews)
                {
                    try
                    {
                        File.Delete(extraFile);
                        Logger.LogInfo($"Deleted excess preview file: {Path.GetFileName(extraFile)}");
                    }
                    catch (Exception deleteEx)
                    {
                        Logger.LogError($"Failed to delete excess file: {extraFile}", deleteEx);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process preview images in {modDir}", ex);
            }
        }

        private bool IsImageOptimized(string imagePath)
        {
            try
            {
                using (var img = System.Drawing.Image.FromFile(imagePath))
                {
                    // Consider optimized if it's square and not larger than 1000x1000
                    return img.Width == img.Height && img.Width <= 1000;
                }
            }
            catch
            {
                return false;
            }
        }

        private void OptimizePreviewImage(string sourcePath, string targetPath)
        {
            using (var src = System.Drawing.Image.FromFile(sourcePath))
            {
                // Step 1: Crop to square (1:1 ratio) if needed
                int originalSize = Math.Min(src.Width, src.Height);
                int x = (src.Width - originalSize) / 2;
                int y = (src.Height - originalSize) / 2;
                bool needsCrop = src.Width != src.Height;
                
                System.Drawing.Image squareImage = src;
                if (needsCrop)
                {
                    var cropped = new System.Drawing.Bitmap(originalSize, originalSize);
                    using (var g = System.Drawing.Graphics.FromImage(cropped))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        var srcRect = new System.Drawing.Rectangle(x, y, originalSize, originalSize);
                        var destRect = new System.Drawing.Rectangle(0, 0, originalSize, originalSize);
                        g.DrawImage(src, destRect, srcRect, GraphicsUnit.Pixel);
                    }
                    squareImage = cropped;
                }
                
                // Step 2: Scale down only if larger than 1000x1000 (no upscaling)
                int finalSize = Math.Min(originalSize, 1000);
                
                using (var finalBmp = new System.Drawing.Bitmap(finalSize, finalSize))
                using (var g2 = System.Drawing.Graphics.FromImage(finalBmp))
                {
                    g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g2.CompositingQuality = CompositingQuality.HighQuality;
                    g2.SmoothingMode = SmoothingMode.HighQuality;
                    g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    var rect = new System.Drawing.Rectangle(0, 0, finalSize, finalSize);
                    g2.DrawImage(squareImage, rect);
                    
                    var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (encoder != null)
                    {
                        var encParams = new EncoderParameters(1);
                        encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                        finalBmp.Save(targetPath, encoder, encParams);
                    }
                }
                
                // Dispose cropped image if we created one
                if (needsCrop && squareImage != src)
                {
                    squareImage.Dispose();
                }
            }
        }

        private void CreateMinitile(string previewPath, string minitilePath)
        {
            try
            {
                using (var previewImg = System.Drawing.Image.FromFile(previewPath))
                using (var thumbBmp = new System.Drawing.Bitmap(600, 722))
                using (var g3 = System.Drawing.Graphics.FromImage(thumbBmp))
                {
                    g3.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g3.CompositingQuality = CompositingQuality.HighQuality;
                    g3.SmoothingMode = SmoothingMode.HighQuality;
                    g3.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    
                    // Calculate crop to 600:722 ratio (center crop)
                    double targetRatio = 600.0 / 722.0;
                    double sourceRatio = (double)previewImg.Width / previewImg.Height;
                    
                    int srcWidth, srcHeight, srcX, srcY;
                    
                    if (sourceRatio > targetRatio)
                    {
                        // Source is wider - crop width
                        srcHeight = previewImg.Height;
                        srcWidth = (int)(srcHeight * targetRatio);
                        srcX = (previewImg.Width - srcWidth) / 2;
                        srcY = 0;
                    }
                    else
                    {
                        // Source is taller - crop height
                        srcWidth = previewImg.Width;
                        srcHeight = (int)(srcWidth / targetRatio);
                        srcX = 0;
                        srcY = (previewImg.Height - srcHeight) / 2;
                    }
                    
                    // Apply smart cropping if enabled
                    var cropRect = new System.Drawing.Rectangle(srcX, srcY, srcWidth, srcHeight);
                    if (SettingsManager.Current.ImageCropType != "Center")
                    {
                        var cropType = Enum.Parse<CropType>(SettingsManager.Current.ImageCropType);
                        cropRect = ImageCropService.CalculateCropRectangle(previewImg, 600, 722, cropType);
                    }
                    
                    var destRect = new System.Drawing.Rectangle(0, 0, 600, 722);
                    g3.DrawImage(previewImg, destRect, cropRect, System.Drawing.GraphicsUnit.Pixel);
                    
                    // Save as JPEG minitile
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                        thumbBmp.Save(minitilePath, jpegEncoder, jpegParams);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create minitile from {previewPath}", ex);
            }
        }

        private void MoveToRecycleBin(string path)
        {
            try
            {
                var shf = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = path + '\0', // Must be null-terminated
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
                };
                
                int result = SHFileOperation(ref shf);
                if (result != 0)
                {
                    Logger.LogWarning($"Failed to move file to recycle bin (error {result}), falling back to permanent deletion: {path}");
                    File.Delete(path); // Fallback to permanent deletion
                }
                else
                {
                    Logger.LogInfo($"Moved file to recycle bin: {path}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to move file to recycle bin, falling back to permanent deletion: {path}. Error: {ex.Message}");
                try
                {
                    File.Delete(path); // Fallback to permanent deletion
                }
                catch (Exception deleteEx)
                {
                    Logger.LogError($"Failed to delete file permanently: {path}. Error: {deleteEx.Message}");
                }
            }
        }

        private void ProcessCategoryPreview(string categoryDir, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            
            // Create backup if enabled
            if (SettingsManager.Current.ImageOptimizerCreateBackups)
            {
                var filesToBackup = Directory.GetFiles(categoryDir)
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("preview") || fileName.StartsWith("catprev") || fileName.StartsWith("catmini")) &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                if (filesToBackup.Count > 0)
                {
                    CreateBackupZip(categoryDir, filesToBackup);
                }
            }
            
            var catprevJpgPath = Path.Combine(categoryDir, "catprev.jpg");
            var catminiJpgPath = Path.Combine(categoryDir, "catmini.jpg");
            
            // Look for existing catprev files (catprev.png, catprev.jpg) and other preview files
            var catprevFiles = Directory.GetFiles(categoryDir)
                .Where(f => 
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return fileName.StartsWith("catprev") &&
                           (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();
            
            // Look for other preview files in category directory (catpreview.*, preview.*, etc.)
            var otherPreviewFiles = Directory.GetFiles(categoryDir)
                .Where(f => 
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return (fileName.StartsWith("catpreview") || fileName.StartsWith("preview")) &&
                           !fileName.StartsWith("catprev") &&
                           (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();
            
            // Combine all preview files, prioritizing catprev files
            var allPreviewFiles = catprevFiles.Concat(otherPreviewFiles).ToArray();
            
            if (allPreviewFiles.Length == 0) return;
            
            // Check if we need to optimize existing catprev.jpg and catmini.jpg
            bool needsOptimization = true;
            bool needsCatmini = !File.Exists(catminiJpgPath);
            
            if (File.Exists(catprevJpgPath))
            {
                try
                {
                    using (var img = System.Drawing.Image.FromFile(catprevJpgPath))
                    {
                        // Consider optimized if it's 600x722
                        needsOptimization = !(img.Width == 600 && img.Height == 722);
                    }
                }
                catch
                {
                    needsOptimization = true;
                }
            }
            
            // Skip if both files exist and are optimized, and no other catprev files to process
            if (!needsOptimization && !needsCatmini && catprevFiles.Length <= 1 && catprevFiles.All(f => f.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase)))
                return;
            
            var previewPath = allPreviewFiles[0]; // Take first preview file found
            
            try
            {
                // Create temporary path if we're optimizing existing catprev.jpg
                var tempPath = catprevJpgPath;
                if (previewPath.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase))
                {
                    tempPath = Path.Combine(categoryDir, "catprev_temp.jpg");
                }
                
                using (var img = System.Drawing.Image.FromFile(previewPath))
                {
                    // Create catprev.jpg (722x722 square for category preview)
                    using (var thumbBmp = new System.Drawing.Bitmap(722, 722))
                    using (var g = System.Drawing.Graphics.FromImage(thumbBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Calculate crop to square (center crop)
                        int size = Math.Min(img.Width, img.Height);
                        int srcX = (img.Width - size) / 2;
                        int srcY = (img.Height - size) / 2;
                        
                        // Apply smart cropping if enabled
                        var cropRect = new System.Drawing.Rectangle(srcX, srcY, size, size);
                        if (SettingsManager.Current.ImageCropType != "Center")
                        {
                            var cropType = Enum.Parse<CropType>(SettingsManager.Current.ImageCropType);
                            cropRect = ImageCropService.CalculateCropRectangle(img, 722, 722, cropType);
                        }
                        
                        var destRect = new System.Drawing.Rectangle(0, 0, 722, 722);
                        g.DrawImage(img, destRect, cropRect, System.Drawing.GraphicsUnit.Pixel);
                        
                        // Save as JPEG catprev
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                            thumbBmp.Save(tempPath, jpegEncoder, jpegParams);
                        }
                    }
                    
                    // Create catmini.jpg (600x722 for category grid tiles) from same source
                    var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                    using (var miniThumb = new System.Drawing.Bitmap(600, 722))
                    using (var g2 = System.Drawing.Graphics.FromImage(miniThumb))
                    {
                        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g2.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g2.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Calculate crop for 600x722 aspect ratio
                        double targetAspect = 600.0 / 722.0;
                        double sourceAspect = (double)img.Width / img.Height;
                        int cropWidth, cropHeight, x, y;
                        
                        if (sourceAspect > targetAspect)
                        {
                            cropHeight = img.Height;
                            cropWidth = (int)(cropHeight * targetAspect);
                            x = (img.Width - cropWidth) / 2;
                            y = 0;
                        }
                        else
                        {
                            cropWidth = img.Width;
                            cropHeight = (int)(cropWidth / targetAspect);
                            x = 0;
                            y = (img.Height - cropHeight) / 2;
                        }
                        
                        // Apply smart cropping if enabled
                        var srcRect = new System.Drawing.Rectangle(x, y, cropWidth, cropHeight);
                        if (SettingsManager.Current.ImageCropType != "Center")
                        {
                            var cropType = Enum.Parse<CropType>(SettingsManager.Current.ImageCropType);
                            srcRect = ImageCropService.CalculateCropRectangle(img, 600, 722, cropType);
                        }
                        var destRect = new System.Drawing.Rectangle(0, 0, 600, 722);
                        g2.DrawImage(img, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                        
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)SettingsManager.Current.ImageOptimizerJpegQuality);
                            miniThumb.Save(catminiPath, jpegEncoder, jpegParams);
                        }
                    }
                }
                
                // If we used a temporary file, replace the original
                if (!tempPath.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(catprevJpgPath))
                        File.Delete(catprevJpgPath);
                    File.Move(tempPath, catprevJpgPath);
                }
                
                // Delete all other catprev files after processing (unless KeepOriginals is enabled)
                if (!SettingsManager.Current.ImageOptimizerKeepOriginals)
                {
                    foreach (var catprevFile in catprevFiles)
                    {
                        if (!catprevFile.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Delete(catprevFile);
                                Logger.LogInfo($"Deleted original category preview: {catprevFile}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to delete original category preview: {catprevFile}", ex);
                            }
                        }
                    }
                    
                    // Delete other preview files if they were used
                    if (!catprevFiles.Contains(previewPath, StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(previewPath);
                            Logger.LogInfo($"Deleted original preview: {previewPath}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to delete original preview: {previewPath}", ex);
                        }
                    }
                }
                else
                {
                    Logger.LogInfo($"Keeping original category preview files (KeepOriginals enabled)");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview for {Path.GetFileName(categoryDir)}", ex);
            }
        }



        private void DefaultResolutionOnStartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.UseDefaultResolutionOnStart = DefaultResolutionOnStartToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
            
            // Enable/disable the resolution input boxes
            DefaultStartWidthTextBox.IsEnabled = DefaultResolutionOnStartToggle.IsOn;
            DefaultStartHeightTextBox.IsEnabled = DefaultResolutionOnStartToggle.IsOn;
        }

        private void DefaultStartWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && int.TryParse(textBox.Text, out int width))
            {
                // Get full screen resolution (not just work area)
                var bounds = Microsoft.UI.Windowing.DisplayArea.Primary.OuterBounds;
                int maxWidth = bounds.Width;
                int minWidth = 1300; // Use MainWindow MIN_WIDTH constant
                
                if (width >= minWidth && width <= maxWidth)
                {
                    SettingsManager.Current.DefaultStartWidth = width;
                    SettingsManager.Save();
                    textBox.BorderBrush = null; // Reset border color
                }
                else
                {
                    // Set red border to indicate invalid value
                    textBox.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            }
        }

        private void DefaultStartHeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && int.TryParse(textBox.Text, out int height))
            {
                // Get full screen resolution (not just work area)
                var bounds = Microsoft.UI.Windowing.DisplayArea.Primary.OuterBounds;
                int maxHeight = bounds.Height;
                int minHeight = 720; // Use MainWindow MIN_HEIGHT constant
                
                if (height >= minHeight && height <= maxHeight)
                {
                    SettingsManager.Current.DefaultStartHeight = height;
                    SettingsManager.Save();
                    textBox.BorderBrush = null; // Reset border color
                }
                else
                {
                    // Set red border to indicate invalid value
                    textBox.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            }
        }

        public void RefreshContent()
        {
            LoadLanguages();
            LoadCurrentSettings();
            UpdateTexts();
        }

        private void OnWindowSizeChanged(object? sender, EventArgs e)
        {
            // Restart the timer - this will delay the update by 200ms
            _windowSizeUpdateTimer?.Stop();
            _windowSizeUpdateTimer?.Start();
        }

        private void WindowSizeUpdateTimer_Tick(object? sender, object e)
        {
            // Stop the timer and update the resolution fields
            _windowSizeUpdateTimer?.Stop();
            UpdateResolutionFields();
        }

        private void UpdateResolutionFields()
        {
            // Only update if toggle is OFF (when it's ON, user controls the values)
            if (!SettingsManager.Current.UseDefaultResolutionOnStart)
            {
                var mainWindow = (App.Current as App)?.MainWindow;
                if (mainWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    
                    if (appWindow != null)
                    {
                        DefaultStartWidthTextBox.Text = appWindow.Size.Width.ToString();
                        DefaultStartHeightTextBox.Text = appWindow.Size.Height.ToString();
                    }
                }
            }
        }



        private void UpdateHotkeysSectionState(bool enabled)
        {
            try
            {
                // Update hotkeys header opacity
                if (HotkeysHeader != null) HotkeysHeader.Opacity = enabled ? 1.0 : 0.5;
                
                // Update the entire SettingsHotkeysPanel opacity and disable/enable all children
                if (SettingsHotkeysPanel != null)
                {
                    SettingsHotkeysPanel.Opacity = enabled ? 1.0 : 0.5;
                    
                    // Enable/disable all child controls in the hotkeys panel
                    foreach (var child in SettingsHotkeysPanel.Children)
                    {
                        if (child is Control control)
                        {
                            control.IsEnabled = enabled;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating hotkeys section state", ex);
            }
        }

        private void SkipXXMILauncherToggle_Toggled(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo($"SkipXXMILauncher toggle changed to: {SkipXXMILauncherToggle.IsOn}");
            SettingsManager.Current.SkipXXMILauncherEnabled = SkipXXMILauncherToggle.IsOn;
            SettingsManager.Save();
            Logger.LogInfo($"SkipXXMILauncher setting saved: {SettingsManager.Current.SkipXXMILauncherEnabled}");
            UpdateToggleLabels();
        }

        // Clean up event subscription when control is unloaded
        private void SettingsUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.WindowSizeChanged -= OnWindowSizeChanged;
            _windowSizeUpdateTimer?.Stop();
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            CheckUpdatesButton.IsEnabled = false;
            
            var result = await UpdateChecker.CheckForUpdatesAsync();
            
            if (result == null)
            {
                var errorDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Error_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "UpdateCheckFailed"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                CheckUpdatesButton.IsEnabled = true;
                return;
            }
            
            if (result.Value.updateAvailable)
            {
                var updateDialog = new FlairX_Mod_Manager.Dialogs.ManagerUpdateDialog(
                    result.Value.latestVersion,
                    result.Value.downloadUrl,
                    result.Value.changelog
                );
                updateDialog.XamlRoot = this.XamlRoot;
                await updateDialog.ShowAsync();
            }
            else
            {
                var infoDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Information"),
                    Content = $"{SharedUtilities.GetTranslation(lang, "LatestVersion")} ({UpdateChecker.GetCurrentVersion()})",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await infoDialog.ShowAsync();
            }
            
            CheckUpdatesButton.IsEnabled = true;
        }
        
        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var currentVersion = UpdateChecker.GetCurrentVersion();
            
            var dialog = new ContentDialog
            {
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot,
            };
            var stackPanel = new StackPanel();
            
            // Add title first
            var titleBlock = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(lang, "App_Name"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 22,
                Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,16)
            };
            stackPanel.Children.Add(titleBlock);
            
            // Add avatar image under title
            try
            {
                var avatarPath = Path.Combine(PathManager.GetAbsolutePath("Assets"), "avatar.png");
                
                if (File.Exists(avatarPath))
                {
                    // Create square border container with rounded corners
                    var avatarBorder = new Microsoft.UI.Xaml.Controls.Border
                    {
                        Width = 80,
                        Height = 80,
                        CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8), // Rounded corners
                        BorderThickness = new Microsoft.UI.Xaml.Thickness(2),
                        BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12),
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
                    };
                    
                    var avatarImage = new Microsoft.UI.Xaml.Controls.Image
                    {
                        Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                    };
                    
                    var avatarBitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    avatarBitmap.UriSource = new Uri($"file:///{avatarPath.Replace('\\', '/')}");
                    avatarImage.Source = avatarBitmap;
                    
                    avatarBorder.Child = avatarImage;
                    stackPanel.Children.Add(avatarBorder);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to load avatar image: {ex.Message}");
            }
            
            // Add GitHub link directly under avatar (no "Author" label)
            stackPanel.Children.Add(new HyperlinkButton { Content = "Jank8", NavigateUri = new Uri("https://github.com/Jank8"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,16), HorizontalAlignment = HorizontalAlignment.Center });
            
            // Add version information and update button
            var versionBlock = new TextBlock
            {
                Text = $"{SharedUtilities.GetTranslation(lang, "AboutDialog_Version")}: {currentVersion}",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(versionBlock);
            
            // Credits section
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_AI"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,8,0,4) });
            
            // Create AI section with Kiro, GitHub Copilot, and Qoder
            var aiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) };
            aiPanel.Children.Add(new HyperlinkButton { Content = "Kiro", NavigateUri = new Uri("https://kiro.dev/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new TextBlock { Text = ", ", VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new HyperlinkButton { Content = "GitHub Copilot", NavigateUri = new Uri("https://github.com/features/copilot"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new TextBlock { Text = " " + SharedUtilities.GetTranslation(lang, "AboutDialog_And") + " ", VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new HyperlinkButton { Content = "Qoder", NavigateUri = new Uri("https://qoder.com/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            stackPanel.Children.Add(aiPanel);
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_Fonts"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            var fontsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) };
            fontsPanel.Children.Add(new HyperlinkButton { Content = "Noto Fonts", NavigateUri = new Uri("https://notofonts.github.io/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            fontsPanel.Children.Add(new TextBlock { Text = ", ", VerticalAlignment = VerticalAlignment.Center });
            fontsPanel.Children.Add(new HyperlinkButton { Content = "Kenney Input Prompts", NavigateUri = new Uri("https://kenney.nl/assets/input-prompts"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            stackPanel.Children.Add(fontsPanel);
            
            // 7-Zip section
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_Compression"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            stackPanel.Children.Add(new HyperlinkButton { Content = "7-Zip", NavigateUri = new Uri("https://www.7-zip.org/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) });
            
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_Thanks"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            var thanksPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            thanksPanel.Children.Add(new StackPanel {
                Orientation = Orientation.Vertical,
                Children = {
                    new HyperlinkButton { Content = "XLXZ", NavigateUri = new Uri("https://github.com/XiaoLinXiaoZhu"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0), HorizontalAlignment = HorizontalAlignment.Left },
                }
            });
            thanksPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_For"), VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(8,0,8,0) });
            thanksPanel.Children.Add(new StackPanel {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Children = {
                    new HyperlinkButton { Content = "Source code", NavigateUri = new Uri("https://github.com/XiaoLinXiaoZhu/XX-Mod-Manager/blob/main/plugins/recognizeModInfoPlugin.js"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0), HorizontalAlignment = HorizontalAlignment.Left },
                }
            });
            stackPanel.Children.Add(thanksPanel);
            
            var gplPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,8,0,0) };
            gplPanel.Children.Add(new HyperlinkButton { Content = SharedUtilities.GetTranslation(lang, "AboutDialog_License"), NavigateUri = new Uri("https://www.gnu.org/licenses/gpl-3.0.html#license-text") });
            stackPanel.Children.Add(gplPanel);
            
            dialog.Content = stackPanel;
            await dialog.ShowAsync();
        }

        /// <summary>
        #region Hotkey Management
        
        private void LoadSettingsHotkeys()
        {
            try
            {
                SettingsHotkeysPanel.Children.Clear();
                
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                foreach (var (key, labelKey, descKey, icon) in _hotkeyDefinitions)
                {
                    var hotkeyValue = GetHotkeyValue(key);
                    var label = SharedUtilities.GetTranslation(lang, labelKey);
                    var description = SharedUtilities.GetTranslation(lang, descKey);
                    
                    var hotkeyRow = CreateSettingsHotkeyRow(key, hotkeyValue, label, description, icon);
                    SettingsHotkeysPanel.Children.Add(hotkeyRow);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load settings hotkeys", ex);
            }
        }
        
        private string GetHotkeyValue(string key)
        {
            return key switch
            {
                "ReloadManagerHotkey" => SettingsManager.Current.ReloadManagerHotkey,
                "ShuffleActiveModsHotkey" => SettingsManager.Current.ShuffleActiveModsHotkey,
                "DeactivateAllModsHotkey" => SettingsManager.Current.DeactivateAllModsHotkey,
                _ => ""
            };
        }
        
        private void SetHotkeyValue(string key, string value)
        {
            switch (key)
            {
                case "ReloadManagerHotkey":
                    SettingsManager.Current.ReloadManagerHotkey = value;
                    break;
                case "ShuffleActiveModsHotkey":
                    SettingsManager.Current.ShuffleActiveModsHotkey = value;
                    break;
                case "DeactivateAllModsHotkey":
                    SettingsManager.Current.DeactivateAllModsHotkey = value;
                    break;
            }
            SettingsManager.Save();
            
            // Refresh global hotkeys
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshGlobalHotkeys();
            }
        }
        
        private Border CreateSettingsHotkeyRow(string key, string keyCombo, string label, string description, string iconGlyph)
        {
            var keyBackground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HotkeyKeyBackground"];
            
            var rowBorder = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Height = 60,
                Tag = key
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Keys
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons
            
            // Create keys panel
            var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(keyCombo, keyBackground, 64);
            Grid.SetColumn(keysPanel, 0);
            grid.Children.Add(keysPanel);
            
            // Description
            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.9
            };
            Grid.SetColumn(labelText, 2);
            grid.Children.Add(labelText);
            
            // Buttons panel
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var defaultBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.6 };
            var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            var accentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B));
            
            // Edit button
            var editBtn = CreateHotkeyActionButton("\uE70F", defaultBrush);
            editBtn.Tag = key;
            editBtn.PointerPressed += SettingsEditButton_PointerPressed;
            var lang = SharedUtilities.LoadLanguageDictionary();
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(editBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_EditHotkey_Tooltip"));
            buttonsPanel.Children.Add(editBtn);
            
            // Save button (initially hidden)
            var saveBtn = CreateHotkeyActionButton("\uE73E", accentBrush);
            saveBtn.Tag = key;
            saveBtn.Visibility = Visibility.Collapsed;
            saveBtn.PointerPressed += SettingsSaveButton_PointerPressed;
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(saveBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_SaveHotkey_Tooltip"));
            buttonsPanel.Children.Add(saveBtn);
            
            // Restore default button
            var restoreBtn = CreateHotkeyActionButton("\uE777", defaultBrush);
            restoreBtn.Tag = key;
            restoreBtn.PointerPressed += SettingsRestoreButton_PointerPressed;
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(restoreBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_RestoreDefaultHotkey_Tooltip"));
            buttonsPanel.Children.Add(restoreBtn);
            
            Grid.SetColumn(buttonsPanel, 5);
            grid.Children.Add(buttonsPanel);
            
            rowBorder.Child = grid;
            return rowBorder;
        }
        
        private Border CreateHotkeyActionButton(string glyph, Microsoft.UI.Xaml.Media.Brush foreground)
        {
            var border = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 16,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform { ScaleX = 1, ScaleY = 1 },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };
            
            border.Child = icon;
            
            border.PointerEntered += (s, e) => {
                border.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                if (icon.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform st) { st.ScaleX = 1.1; st.ScaleY = 1.1; }
            };
            border.PointerExited += (s, e) => {
                border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                if (icon.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform st) { st.ScaleX = 1.0; st.ScaleY = 1.0; }
            };
            
            return border;
        }
        
        private void SettingsEditButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return; // Block if already editing
            
            if (sender is Border editBorder && editBorder.Tag is string key)
            {
                var parent = editBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Disable global hotkeys during editing
                _previousHotkeysEnabled = SettingsManager.Current.HotkeysEnabled;
                SettingsManager.Current.HotkeysEnabled = false;
                
                // Show save button, hide edit button
                editBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 1 && parent.Children[1] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with editable TextBox
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                if (keysPanel != null)
                {
                    var currentValue = GetHotkeyValue(key);
                    var editBox = new TextBox
                    {
                        Text = currentValue,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        MinWidth = 120,
                        Height = 32,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = key,
                        PlaceholderText = "Press keys..."
                    };
                    editBox.PreviewKeyDown += SettingsHotkeyEditBox_PreviewKeyDown;
                    
                    int idx = grid.Children.IndexOf(keysPanel);
                    Grid.SetColumn(editBox, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, editBox);
                    editBox.Focus(FocusState.Programmatic);
                    
                    _activeHotkeyEditBox = editBox;
                }
            }
        }
        
        private async void SettingsSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border saveBorder && saveBorder.Tag is string key)
            {
                var parent = saveBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Find edit TextBox
                var editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == key);
                if (editBox == null) return;
                
                string newKeyCombo = editBox.Text.Trim();
                if (string.IsNullOrEmpty(newKeyCombo)) newKeyCombo = GetHotkeyValue(key);
                
                // Check for conflicts
                var conflict = SharedUtilities.GetConflictingKeyboardHotkey(newKeyCombo, key);
                if (conflict != null)
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "HotkeyConflict_Title"),
                        Content = SharedUtilities.GetTranslation(lang, "HotkeyConflict_Message"),
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return;
                }
                
                // Update settings
                SetHotkeyValue(key, newKeyCombo);
                
                // Replace TextBox with keys panel
                var keyBackground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HotkeyKeyBackground"];
                var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(newKeyCombo, keyBackground, 64);
                
                int idx = grid.Children.IndexOf(editBox);
                Grid.SetColumn(keysPanel, 0);
                grid.Children.RemoveAt(idx);
                grid.Children.Insert(idx, keysPanel);
                
                // Hide save, show edit
                saveBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 0 && parent.Children[0] is Border editBtn)
                    editBtn.Visibility = Visibility.Visible;
                
                _activeHotkeyEditBox = null;
                
                // Restore global hotkeys
                SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
            }
        }
        
        private void SettingsRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return; // Block if editing
            
            if (sender is Border restoreBorder && restoreBorder.Tag is string key)
            {
                var defaultValue = GetDefaultHotkeyValue(key);
                if (string.IsNullOrEmpty(defaultValue)) return;
                
                var parent = restoreBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Update settings
                SetHotkeyValue(key, defaultValue);
                
                // Update keys panel
                var keyBackground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HotkeyKeyBackground"];
                var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(defaultValue, keyBackground, 64);
                
                var existingKeysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                if (existingKeysPanel != null)
                {
                    int idx = grid.Children.IndexOf(existingKeysPanel);
                    Grid.SetColumn(keysPanel, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, keysPanel);
                }
            }
        }
        
        private string GetDefaultHotkeyValue(string key)
        {
            return key switch
            {
                "ReloadManagerHotkey" => "Ctrl+R",
                "ShuffleActiveModsHotkey" => "Ctrl+S",
                "DeactivateAllModsHotkey" => "Ctrl+D",
                _ => ""
            };
        }
        
        private void SettingsHotkeyEditBox_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (sender is not TextBox editBox) return;
            
            e.Handled = true;
            
            var modifiers = new List<string>();
            
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("Ctrl");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("Shift");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("Alt");
            
            var key = e.Key;
            
            // Skip modifier-only keys
            if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift ||
                key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows ||
                key == Windows.System.VirtualKey.RightWindows || key == Windows.System.VirtualKey.LeftControl ||
                key == Windows.System.VirtualKey.RightControl || key == Windows.System.VirtualKey.LeftShift ||
                key == Windows.System.VirtualKey.RightShift || key == Windows.System.VirtualKey.LeftMenu ||
                key == Windows.System.VirtualKey.RightMenu)
                return;
            
            // Convert key to display string
            string keyStr = ConvertVirtualKeyToString(key);
            
            if (modifiers.Count > 0)
                editBox.Text = string.Join("+", modifiers) + "+" + keyStr;
            else
                editBox.Text = keyStr;
        }
        
        private string ConvertVirtualKeyToString(Windows.System.VirtualKey key)
        {
            return key switch
            {
                >= Windows.System.VirtualKey.A and <= Windows.System.VirtualKey.Z => key.ToString(),
                >= Windows.System.VirtualKey.Number0 and <= Windows.System.VirtualKey.Number9 => ((int)key - (int)Windows.System.VirtualKey.Number0).ToString(),
                >= Windows.System.VirtualKey.NumberPad0 and <= Windows.System.VirtualKey.NumberPad9 => "NUM " + ((int)key - (int)Windows.System.VirtualKey.NumberPad0),
                >= Windows.System.VirtualKey.F1 and <= Windows.System.VirtualKey.F12 => "F" + ((int)key - (int)Windows.System.VirtualKey.F1 + 1),
                Windows.System.VirtualKey.Up => "↑",
                Windows.System.VirtualKey.Down => "↓",
                Windows.System.VirtualKey.Left => "←",
                Windows.System.VirtualKey.Right => "→",
                Windows.System.VirtualKey.Space => "SPACE",
                Windows.System.VirtualKey.Enter => "ENTER",
                Windows.System.VirtualKey.Tab => "TAB",
                Windows.System.VirtualKey.Escape => "ESC",
                Windows.System.VirtualKey.Back => "BACKSPACE",
                Windows.System.VirtualKey.Delete => "DEL",
                Windows.System.VirtualKey.Insert => "INS",
                Windows.System.VirtualKey.Home => "HOME",
                Windows.System.VirtualKey.End => "END",
                Windows.System.VirtualKey.PageUp => "PAGE UP",
                Windows.System.VirtualKey.PageDown => "PAGE DOWN",
                _ => key.ToString().ToUpper()
            };
        }
        

        #region Download Settings
        
        private void FastDownloadToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.SetFastDownloadEnabled(FastDownloadToggle.IsOn);
            UpdateToggleLabels();
        }
        
        private void MaxConnectionsNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (sender.Value >= 1 && sender.Value <= 8)
            {
                SettingsManager.SetMaxDownloadConnections((int)sender.Value);
            }
        }
        
        #endregion

        #endregion
    }
}