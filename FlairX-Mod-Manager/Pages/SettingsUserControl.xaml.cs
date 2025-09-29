using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
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
        private static bool _isOptimizingPreviews = false;
        private CancellationTokenSource? _previewCts;
        private FontIcon? _optimizePreviewsButtonIcon;

        public event EventHandler? CloseRequested; // Event to notify parent to close

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
            _optimizePreviewsButtonIcon = OptimizePreviewsButton.Content as FontIcon;
            SettingsManager.Load();
            LoadLanguages();
            InitializeUIState();
            
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
            AboutButtonText.Text = SharedUtilities.GetTranslation(lang, "AboutButton_Label");
            AboutButtonIcon.Glyph = "\uE946";
            
            // Handle optimization progress state (equivalent to OnNavigatedTo)
            if (_isOptimizingPreviews)
            {
                if (OptimizePreviewsProgressBar != null)
                    OptimizePreviewsProgressBar.Visibility = Visibility.Visible;
                if (OptimizePreviewsButton != null)
                    OptimizePreviewsButton.IsEnabled = false;
            }
            else
            {
                if (OptimizePreviewsProgressBar != null)
                    OptimizePreviewsProgressBar.Visibility = Visibility.Collapsed;
                if (OptimizePreviewsButton != null)
                    OptimizePreviewsButton.IsEnabled = true;
            }
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
            if (ThemeLabel != null) ThemeLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme");
            if (BackdropLabel != null) BackdropLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop");
            if (LanguageLabel != null) LanguageLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Language");
            if (OptimizePreviewsLabel != null) OptimizePreviewsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Label");
            if (DefaultResolutionOnStartLabel != null) DefaultResolutionOnStartLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultResolutionOnStart_Label");
            if (DefaultStartResolutionLabel != null) DefaultStartResolutionLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultStartResolution_Label");
            if (XXMIRootDirectoryLabel != null) XXMIRootDirectoryLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_XXMIRootDirectory");
            if (ModLibraryDirectoryLabel != null) ModLibraryDirectoryLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModLibraryDirectory");
            if (SkipXXMILauncherLabel != null) SkipXXMILauncherLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_SkipXXMILauncher_Label");
            if (ActiveModsToTopLabel != null) ActiveModsToTopLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ActiveModsToTop_Label");
            if (DynamicModSearchLabel != null) DynamicModSearchLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Label");
            if (ShowOrangeAnimationLabel != null) ShowOrangeAnimationLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShowOrangeAnimation_Label");
            if (ModGridZoomLabel != null) ModGridZoomLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModGridZoom_Label");
            if (GridLoggingLabel != null) GridLoggingLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Label");
            if (MinimizeToTrayLabel != null) MinimizeToTrayLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MinimizeToTray_Label");
            if (HotkeysHeader != null) HotkeysHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Hotkeys_Header");

            // Description texts - use null checks and fallback to empty string if missing
            if (ThemeDescription != null) ThemeDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Description") ?? string.Empty;
            if (BackdropDescription != null) BackdropDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_Description") ?? string.Empty;
            if (LanguageDescription != null) LanguageDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Language_Description") ?? string.Empty;
            if (OptimizePreviewsDescription != null) OptimizePreviewsDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Description") ?? string.Empty;
            if (DefaultResolutionOnStartDescription != null) DefaultResolutionOnStartDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultResolutionOnStart_Description") ?? string.Empty;
            if (DefaultStartResolutionDescription != null) DefaultStartResolutionDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DefaultStartResolution_Description") ?? string.Empty;
            if (XXMIRootDirectoryDescription != null) XXMIRootDirectoryDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_XXMIRootDirectory_Description") ?? string.Empty;
            if (ModLibraryDirectoryDescription != null) ModLibraryDirectoryDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModLibraryDirectory_Description") ?? string.Empty;
            if (SkipXXMILauncherDescription != null) SkipXXMILauncherDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_SkipXXMILauncher_Description") ?? string.Empty;
            if (ActiveModsToTopDescription != null) ActiveModsToTopDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ActiveModsToTop_Description") ?? string.Empty;
            if (DynamicModSearchDescription != null) DynamicModSearchDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Description") ?? string.Empty;
            if (ShowOrangeAnimationDescription != null) ShowOrangeAnimationDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShowOrangeAnimation_Description") ?? string.Empty;
            if (ModGridZoomDescription != null) ModGridZoomDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModGridZoom_Description") ?? string.Empty;
            if (GridLoggingDescription != null) GridLoggingDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Description") ?? string.Empty;
            if (MinimizeToTrayDescription != null) MinimizeToTrayDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MinimizeToTray_Description") ?? string.Empty;
            
            // Hotkey labels and descriptions - use null checks
            if (OptimizePreviewsHotkeyLabel != null) OptimizePreviewsHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Label");
            if (OptimizePreviewsHotkeyDescription != null) OptimizePreviewsHotkeyDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviewsHotkey_Description");

            if (ReloadManagerHotkeyLabel != null) ReloadManagerHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "Reload_Mods_Tooltip");
            if (ReloadManagerHotkeyDescription != null) ReloadManagerHotkeyDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ReloadManagerHotkey_Description");
            if (ShuffleActiveModsHotkeyLabel != null) ShuffleActiveModsHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShuffleActiveMods_Label");
            if (ShuffleActiveModsHotkeyDescription != null) ShuffleActiveModsHotkeyDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShuffleActiveModsHotkey_Description");
            if (DeactivateAllModsHotkeyLabel != null) DeactivateAllModsHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DeactivateAllMods_Label");
            if (DeactivateAllModsHotkeyDescription != null) DeactivateAllModsHotkeyDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DeactivateAllModsHotkey_Description");
            
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
            if (ModLibraryDirectoryDefaultButton != null) ToolTipService.SetToolTip(ModLibraryDirectoryDefaultButton, SharedUtilities.GetTranslation(lang, "SettingsPage_RestoreDefault_Tooltip"));
            if (OptimizePreviewsButton != null) ToolTipService.SetToolTip(OptimizePreviewsButton, SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Tooltip"));

            if (XXMIRootDirectoryPickButton != null) ToolTipService.SetToolTip(XXMIRootDirectoryPickButton, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
            if (ModLibraryDirectoryPickButton != null) ToolTipService.SetToolTip(ModLibraryDirectoryPickButton, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
            if (XXMIRootDirectoryBreadcrumb != null) ToolTipService.SetToolTip(XXMIRootDirectoryBreadcrumb, SharedUtilities.GetTranslation(lang, "OpenDirectory_Tooltip"));
            if (ModLibraryDirectoryBreadcrumb != null) ToolTipService.SetToolTip(ModLibraryDirectoryBreadcrumb, SharedUtilities.GetTranslation(lang, "OpenDirectory_Tooltip"));
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
            
            // Small delay to let SelectorBars fully render
            await Task.Delay(50);
            
            // Force final layout update
            ThemeSelectorBar.UpdateLayout();
            BackdropSelectorBar.UpdateLayout();
            
            // Set toggle states from settings
            DynamicModSearchToggle.IsOn = SettingsManager.Current.DynamicModSearchEnabled;
            GridLoggingToggle.IsOn = SettingsManager.Current.GridLoggingEnabled;
            MinimizeToTrayToggle.IsOn = SettingsManager.Current.MinimizeToTrayEnabled;
            HotkeysEnabledToggle.IsOn = SettingsManager.Current.HotkeysEnabled;
            
            // Update hotkeys section state
            UpdateHotkeysSectionState(SettingsManager.Current.HotkeysEnabled);

            ShowOrangeAnimationToggle.IsOn = SettingsManager.Current.ShowOrangeAnimation;
            ModGridZoomToggle.IsOn = SettingsManager.Current.ModGridZoomEnabled;
            SkipXXMILauncherToggle.IsOn = SettingsManager.Current.SkipXXMILauncherEnabled;
            ActiveModsToTopToggle.IsOn = SettingsManager.Current.ActiveModsToTopEnabled;
            
            // Set BreadcrumbBar paths
            SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, SettingsManager.GetCurrentGameXXMIRoot());
            SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, SettingsManager.GetCurrentModLibraryDirectory());
            
            // Set hotkey values from settings
            OptimizePreviewsHotkeyTextBox.Text = SettingsManager.Current.OptimizePreviewsHotkey;
            ReloadManagerHotkeyTextBox.Text = SettingsManager.Current.ReloadManagerHotkey;
            ShuffleActiveModsHotkeyTextBox.Text = SettingsManager.Current.ShuffleActiveModsHotkey;
            DeactivateAllModsHotkeyTextBox.Text = SettingsManager.Current.DeactivateAllModsHotkey;
            
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
                }
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

        private bool _wasPreviewCancelled = false;

        private async Task OptimizePreviewsAsync(CancellationToken token)
        {
            await Task.Run(() =>
            {
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !Directory.Exists(modLibraryPath)) return;
                foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                {
                    if (token.IsCancellationRequested)
                        break;
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    // Process category preview (if exists) to create category minitile
                    ProcessCategoryPreview(categoryDir, token);
                    if (token.IsCancellationRequested)
                        break;
                    
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        if (token.IsCancellationRequested)
                            break;
                        
                        ProcessModPreviewImages(modDir);
                    }
                }
            }, token);
        }

        // Public method for hotkey when on settings page - runs optimization with UI but without confirmation dialog
        public async Task ExecuteOptimizePreviewsWithUI()
        {
            if (_isOptimizingPreviews)
            {
                Logger.LogInfo("Optimization already in progress - ignoring hotkey");
                return;
            }

            _isOptimizingPreviews = true;
            _wasPreviewCancelled = false;
            _previewCts = new CancellationTokenSource();
            
            OptimizePreviewsButton.IsEnabled = true;
            OptimizePreviewsProgressBar.Visibility = Visibility.Visible;
            
            if (_optimizePreviewsButtonIcon != null)
            {
                _optimizePreviewsButtonIcon.Glyph = "\uE711"; // Cancel icon
            }

            try
            {
                await OptimizePreviewsAsync(_previewCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled
            }
            finally
            {
                _isOptimizingPreviews = false;
                _previewCts = null;
                OptimizePreviewsButton.IsEnabled = true;
                OptimizePreviewsProgressBar.Visibility = Visibility.Collapsed;
                
                if (_optimizePreviewsButtonIcon != null)
                {
                    _optimizePreviewsButtonIcon.Glyph = "\uE89E"; // Original icon
                }
            }
            
            // Show completion or cancellation message
            var lang = SharedUtilities.LoadLanguageDictionary();
            if (_wasPreviewCancelled)
            {
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                    Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Cancelled"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Success_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Completed"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        // Public method for hotkey - runs optimization without confirmation dialog
        public static async Task OptimizePreviewsDirectAsync()
        {
            try
            {
                Logger.LogInfo("Starting optimize previews via hotkey (no confirmation)");
                
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !Directory.Exists(modLibraryPath))
                {
                    Logger.LogWarning("Mod library path not found for optimize previews hotkey");
                    return;
                }

                await Task.Run(() =>
                {
                    foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                    {
                        if (!Directory.Exists(categoryDir)) continue;
                        
                        // Process category preview (if exists) to create category minitile
                        ProcessCategoryPreviewStatic(categoryDir);
                        
                        foreach (var modDir in Directory.GetDirectories(categoryDir))
                        {
                            ProcessModPreviewImagesStatic(modDir);
                        }
                    }
                });

                Logger.LogInfo("Optimize previews completed via hotkey");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during optimize previews hotkey execution", ex);
            }
        }

        // Static versions of processing methods for hotkey use
        private static void ProcessCategoryPreviewStatic(string categoryDir)
        {
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
                        // Consider optimized if it's square and 600x600
                        needsOptimization = !(img.Width == 600 && img.Height == 600);
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
                    // Create catprev.jpg (600x600 for category tiles)
                    using (var thumbBmp = new System.Drawing.Bitmap(600, 600))
                    using (var g = System.Drawing.Graphics.FromImage(thumbBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Calculate crop rectangle to make it square (center crop)
                        int size = Math.Min(img.Width, img.Height);
                        int x = (img.Width - size) / 2;
                        int y = (img.Height - size) / 2;
                        var srcRect = new System.Drawing.Rectangle(x, y, size, size);
                        var destRect = new System.Drawing.Rectangle(0, 0, 600, 600);
                        
                        g.DrawImage(img, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                        
                        // Save as JPEG catprev
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                            thumbBmp.Save(tempPath, jpegEncoder, jpegParams);
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

        private static void ProcessModPreviewImagesStatic(string modDir)
        {
            try
            {
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
                for (int i = 0; i < previewFiles.Count && i < 100; i++) // Max 100 images
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

                    // Optimize and save the image
                    OptimizePreviewImageStatic(sourceFile, targetPath);

                    // Create minitile only for the main preview (index 0)
                    if (i == 0)
                    {
                        CreateMinitileStatic(targetPath, minitileJpgPath);
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

        private static void OptimizePreviewImageStatic(string sourcePath, string targetPath)
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
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        var srcRect = new System.Drawing.Rectangle(x, y, originalSize, originalSize);
                        var destRect = new System.Drawing.Rectangle(0, 0, originalSize, originalSize);
                        g.DrawImage(src, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                    }
                    squareImage = cropped;
                }

                // Step 2: Resize if larger than 1000x1000
                int targetSize = Math.Min(originalSize, 1000);
                System.Drawing.Image finalImage = squareImage;
                
                if (originalSize > 1000)
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
                    var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                    jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                    finalImage.Save(targetPath, jpegEncoder, jpegParams);
                }

                if (finalImage != src) finalImage.Dispose();
            }
        }

        private static void CreateMinitileStatic(string sourcePath, string minitilePath)
        {
            try
            {
                using (var src = System.Drawing.Image.FromFile(sourcePath))
                using (var minitile = new System.Drawing.Bitmap(200, 200))
                using (var g = System.Drawing.Graphics.FromImage(minitile))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(src, 0, 0, 200, 200);
                    
                    var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                        jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                        minitile.Save(minitilePath, jpegEncoder, jpegParams);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create minitile: {minitilePath}", ex);
            }
        }

        private async void OptimizePreviewsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isOptimizingPreviews)
            {
                if (_previewCts != null)
                {
                    _wasPreviewCancelled = true;
                    _previewCts.Cancel();
                }
                return;
            }

            // Show confirmation dialog before starting optimization
            var lang = SharedUtilities.LoadLanguageDictionary();
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Confirm_Title"),
                Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Confirm_Message"),
                PrimaryButtonText = SharedUtilities.GetTranslation(lang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return; // User cancelled
            }
            
            _isOptimizingPreviews = true;
            _wasPreviewCancelled = false;
            _previewCts = new CancellationTokenSource();
            
            OptimizePreviewsButton.IsEnabled = true;
            OptimizePreviewsProgressBar.Visibility = Visibility.Visible;
            
            if (_optimizePreviewsButtonIcon != null)
            {
                _optimizePreviewsButtonIcon.Glyph = "\uE711"; // Cancel icon
            }

            try
            {
                await OptimizePreviewsAsync(_previewCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled
            }
            finally
            {
                _isOptimizingPreviews = false;
                _previewCts = null;
                OptimizePreviewsButton.IsEnabled = true;
                OptimizePreviewsProgressBar.Visibility = Visibility.Collapsed;
                
                if (_optimizePreviewsButtonIcon != null)
                {
                    _optimizePreviewsButtonIcon.Glyph = "\uE89E"; // Original icon
                }
            }
            
            // Show completion or cancellation message
            if (_wasPreviewCancelled)
            {
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                    Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Cancelled"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Success_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Completed"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
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
                    if (!IsNtfs(folderPath))
                        ShowNtfsWarning(folderPath, "XXMI");
                    
                    // Clean up symlinks from current mods directory before switching
                    var currentModsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                    if (Directory.Exists(currentModsPath))
                    {
                        Logger.LogInfo($"Cleaning up symlinks from current XXMI mods directory: {currentModsPath}");
                        foreach (var dir in Directory.GetDirectories(currentModsPath))
                        {
                            if (FlairX_Mod_Manager.Pages.ModGridPage.IsSymlinkStatic(dir))
                            {
                                try
                                {
                                    Directory.Delete(dir, true);
                                    Logger.LogInfo($"Removed symlink: {dir}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to remove symlink: {dir}", ex);
                                }
                            }
                        }
                    }
                    
                    // Set XXMI root for current game
                    SettingsManager.SetCurrentGameXXMIRoot(folderPath);
                    SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, folderPath);
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    
                    Logger.LogInfo($"Changed XXMI root directory to: {folderPath}");
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

            // Clean up symlinks from current directory before switching
            if (Directory.Exists(currentModsPath))
            {
                Logger.LogInfo($"Cleaning up symlinks from current directory: {currentModsPath}");
                foreach (var dir in Directory.GetDirectories(currentModsPath))
                {
                    if (FlairX_Mod_Manager.Pages.ModGridPage.IsSymlinkStatic(dir))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            Logger.LogInfo($"Removed symlink: {dir}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to remove symlink: {dir}", ex);
                        }
                    }
                }
            }

            // Remove custom XXMI root for current game to use default
            SettingsManager.Current.GameXXMIRootPaths.Remove(gameTag);
            SettingsManager.Save();
            
            SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, defaultXXMIRoot);
            FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            
            Logger.LogInfo($"Restored XXMI root to default: {defaultXXMIRoot}");
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

        private async Task ModLibraryDirectoryPickButton_ClickAsync(Button senderButton)
        {
            senderButton.IsEnabled = false;
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var hwnd = SharedUtilities.GetMainWindowHandle();
                var folderPath = await SharedUtilities.PickFolderAsync(hwnd, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (!IsNtfs(folderPath))
                        ShowNtfsWarning(folderPath, "ModLibrary");

                    // Deactivate all mods and remove symlinks
                    var activeModsPath = PathManager.GetSettingsPath("ActiveMods.json");
                    if (File.Exists(activeModsPath))
                    {
                        var allMods = new Dictionary<string, bool>();
                        var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                        foreach (var key in currentMods.Keys)
                        {
                            allMods[key] = false;
                        }
                        var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(activeModsPath, json);
                    }
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();

                    SettingsManager.SetCurrentGameModLibrary(folderPath);
                    SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, folderPath);

                    // Create default mod.json in subdirectories
                    (App.Current as FlairX_Mod_Manager.App)?.EnsureModJsonInModLibrary();

                    // Refresh manager (skip menu regeneration for directory changes)
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.RefreshUIAfterLanguageChange(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to change ModLibrary directory", ex);
            }
            senderButton.IsEnabled = true;
        }

        private async void ModLibraryDirectoryPickButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button senderButton)
                await ModLibraryDirectoryPickButton_ClickAsync(senderButton);
        }

        private void ModLibraryDirectoryDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            // If already default, do nothing
            var defaultPath = AppConstants.DEFAULT_MOD_LIBRARY_PATH;
            var currentPath = SettingsManager.GetCurrentModLibraryDirectory();
            
            Logger.LogInfo($"Restore default mod library button clicked. Current: '{currentPath}', Default: '{defaultPath}'");
            
            // If already default, do nothing
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                Logger.LogInfo("Current mod library path is null/empty, already using default - no action needed");
                return;
            }
            
            try
            {
                var currentFullPath = Path.GetFullPath(currentPath);
                var defaultFullPath = Path.GetFullPath(defaultPath);
                
                if (string.Equals(currentFullPath, defaultFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo($"Mod library is already using default path ({currentFullPath}) - no action needed");
                    return;
                }
                
                Logger.LogInfo($"Mod library path needs to be changed from '{currentFullPath}' to '{defaultFullPath}'");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to compare mod library paths", ex);
                return;
            }

            // Deactivate all mods and remove symlinks
            var activeModsPath = PathManager.GetSettingsPath("ActiveMods.json");
            if (File.Exists(activeModsPath))
            {
                var allMods = new Dictionary<string, bool>();
                var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                foreach (var key in currentMods.Keys)
                {
                    allMods[key] = false;
                }
                var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(activeModsPath, json);
            }
            FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();

            // Remove custom mod library path for current game to use default
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (!string.IsNullOrEmpty(gameTag))
            {
                SettingsManager.Current.GameModLibraryPaths.Remove(gameTag);
                SettingsManager.Save();
                
                var newDefaultPath = AppConstants.GameConfig.GetModLibraryPath(gameTag);
                SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, newDefaultPath);
            }

            // Refresh manager (skip menu regeneration for directory changes)
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshUIAfterLanguageChange(false);
            }
        }

        private void ModLibraryDirectoryBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var path = GetBreadcrumbPath(sender);
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (!string.IsNullOrEmpty(gameTag))
            {
                SettingsManager.Current.GameModLibraryPaths[gameTag] = path;
                SettingsManager.Save();
            }
        }

        private bool IsNtfs(string path)
        {
            return SharedUtilities.IsNtfsFileSystem(path);
        }

        private void ShowNtfsWarning(string path, string label)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var dialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "Ntfs_Warning_Title"),
                Content = string.Format(SharedUtilities.GetTranslation(lang, "Ntfs_Warning_Content"), label, path),
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private void ActiveModsToTopToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ActiveModsToTopEnabled = ActiveModsToTopToggle.IsOn;
            SettingsManager.Save();
        }

        private void DynamicModSearchToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.DynamicModSearchEnabled = DynamicModSearchToggle.IsOn;
            SettingsManager.Save();
        }

        private void ShowOrangeAnimationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ShowOrangeAnimation = ShowOrangeAnimationToggle.IsOn;
            SettingsManager.Save();
            
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
        }

        private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.MinimizeToTrayEnabled = MinimizeToTrayToggle.IsOn;
            SettingsManager.Save();
        }

        private void HotkeysEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.HotkeysEnabled = HotkeysEnabledToggle.IsOn;
            SettingsManager.Save();
            
            // Update hotkey section UI state
            UpdateHotkeysSectionState(HotkeysEnabledToggle.IsOn);
            
            // Refresh global hotkeys in MainWindow
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshGlobalHotkeys();
            }
        }

        private void OptimizePreviewsHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.OptimizePreviewsHotkey = textBox.Text;
                SettingsManager.Save();
                
                // Refresh global hotkeys
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.RefreshGlobalHotkeys();
                }
            }
        }

        private void ReloadManagerHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.ReloadManagerHotkey = textBox.Text;
                SettingsManager.Save();
                
                // Refresh global hotkeys
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.RefreshGlobalHotkeys();
                }
            }
        }

        private void ShuffleActiveModsHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.ShuffleActiveModsHotkey = textBox.Text;
                SettingsManager.Save();
                
                // Refresh global hotkeys
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.RefreshGlobalHotkeys();
                }
            }
        }

        private void DeactivateAllModsHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.DeactivateAllModsHotkey = textBox.Text;
                SettingsManager.Save();
                
                // Refresh global hotkeys
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.RefreshGlobalHotkeys();
                }
            }
        }

        private void HotkeyTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                e.Handled = true;
                
                var key = e.Key;
                var modifiers = new List<string>();
                
                // Check for modifier keys
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Ctrl");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Shift");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Alt");
                
                // Skip modifier-only keys
                if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift || 
                    key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows || 
                    key == Windows.System.VirtualKey.RightWindows)
                    return;
                
                // Build hotkey string
                var hotkeyParts = new List<string>(modifiers);
                hotkeyParts.Add(key.ToString());
                
                textBox.Text = string.Join("+", hotkeyParts);
            }
        }

        private void ProcessModPreviewImages(string modDir)
        {
            try
            {
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
                for (int i = 0; i < previewFiles.Count && i < 100; i++) // Max 100 images (preview.jpg + preview-01.jpg to preview-99.jpg)
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

                    // Move original file to recycle bin if it has a different name
                    if (!sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        MoveToRecycleBin(sourceFile);
                    }
                }

                // Clean up any extra preview files beyond the 100 limit
                var existingPreviews = Directory.GetFiles(modDir, "preview*.jpg")
                    .Where(f => 
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (name == "preview") return false; // Keep main preview
                        if (name.StartsWith("preview-"))
                        {
                            var suffix = name.Substring(8); // Remove "preview-"
                            return !int.TryParse(suffix, out int num) || num > 99; // Remove if not 01-99
                        }
                        return true; // Remove other preview files
                    })
                    .ToList();

                foreach (var extraFile in existingPreviews)
                {
                    MoveToRecycleBin(extraFile);
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
                        encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
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
                using (var thumbBmp = new System.Drawing.Bitmap(600, 600))
                using (var g3 = System.Drawing.Graphics.FromImage(thumbBmp))
                {
                    g3.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g3.CompositingQuality = CompositingQuality.HighQuality;
                    g3.SmoothingMode = SmoothingMode.HighQuality;
                    g3.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    var thumbRect = new System.Drawing.Rectangle(0, 0, 600, 600);
                    g3.DrawImage(previewImg, thumbRect);
                    
                    // Save as JPEG minitile
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
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
                        // Consider optimized if it's square and 600x600
                        needsOptimization = !(img.Width == 600 && img.Height == 600);
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
                    // Create catprev.jpg (600x600 for category tiles)
                    using (var thumbBmp = new System.Drawing.Bitmap(600, 600))
                    using (var g = System.Drawing.Graphics.FromImage(thumbBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Calculate crop rectangle to make it square (center crop)
                        int size = Math.Min(img.Width, img.Height);
                        int x = (img.Width - size) / 2;
                        int y = (img.Height - size) / 2;
                        var srcRect = new System.Drawing.Rectangle(x, y, size, size);
                        var destRect = new System.Drawing.Rectangle(0, 0, 600, 600);
                        
                        g.DrawImage(img, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);
                        
                        // Save as JPEG catprev
                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new System.Drawing.Imaging.EncoderParameters(1);
                            jpegParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                            thumbBmp.Save(tempPath, jpegEncoder, jpegParams);
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
                
                // Move all other catprev files to recycle bin after processing
                foreach (var catprevFile in catprevFiles)
                {
                    if (!catprevFile.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase))
                    {
                        MoveToRecycleBin(catprevFile);
                    }
                }
                
                // Move other preview files to recycle bin if they were used
                if (!catprevFiles.Contains(previewPath, StringComparer.OrdinalIgnoreCase))
                {
                    MoveToRecycleBin(previewPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview for {Path.GetFileName(categoryDir)}", ex);
            }
        }

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var dialog = new ContentDialog
            {
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot,
            };
            var stackPanel = new StackPanel();
            
            // Add title first
            var titleBlock = new TextBlock
            {
                Text = "FlairX Mod Manager",
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
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_AI"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            
            // Create AI section with Kiro, GitHub Copilot, and Qoder
            var aiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) };
            aiPanel.Children.Add(new HyperlinkButton { Content = "Kiro", NavigateUri = new Uri("https://kiro.dev/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new TextBlock { Text = ", ", VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new HyperlinkButton { Content = "GitHub Copilot", NavigateUri = new Uri("https://github.com/features/copilot"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new TextBlock { Text = " " + SharedUtilities.GetTranslation(lang, "AboutDialog_And") + " ", VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new HyperlinkButton { Content = "Qoder", NavigateUri = new Uri("https://qoder.com/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            stackPanel.Children.Add(aiPanel);
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_Fonts"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            stackPanel.Children.Add(new HyperlinkButton { Content = "Noto Fonts", NavigateUri = new Uri("https://notofonts.github.io/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) });
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
            
            var gplPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,16,0,0) };
            gplPanel.Children.Add(new HyperlinkButton { Content = SharedUtilities.GetTranslation(lang, "AboutDialog_License"), NavigateUri = new Uri("https://www.gnu.org/licenses/gpl-3.0.html#license-text") });
            stackPanel.Children.Add(gplPanel);
            
            // Add version information at the bottom
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.6.5";
            var versionBlock = new TextBlock
            {
                Text = $"{SharedUtilities.GetTranslation(lang, "AboutDialog_Version")}: {version}",
                FontSize = 12,
                Opacity = 0.7,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 12, 0, 0)
            };
            stackPanel.Children.Add(versionBlock);
            dialog.Content = stackPanel;
            await dialog.ShowAsync();
        }

        private void DefaultResolutionOnStartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.UseDefaultResolutionOnStart = DefaultResolutionOnStartToggle.IsOn;
            SettingsManager.Save();
            
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
                // Find all hotkey-related UI elements and update their state
                var hotkeyElements = new List<Control>();
                
                // Add all hotkey TextBoxes
                if (OptimizePreviewsHotkeyTextBox != null) hotkeyElements.Add(OptimizePreviewsHotkeyTextBox);
                if (ReloadManagerHotkeyTextBox != null) hotkeyElements.Add(ReloadManagerHotkeyTextBox);
                if (ShuffleActiveModsHotkeyTextBox != null) hotkeyElements.Add(ShuffleActiveModsHotkeyTextBox);
                if (DeactivateAllModsHotkeyTextBox != null) hotkeyElements.Add(DeactivateAllModsHotkeyTextBox);
                
                // Update opacity and IsEnabled for all hotkey elements
                foreach (var element in hotkeyElements)
                {
                    element.Opacity = enabled ? 1.0 : 0.5;
                    element.IsEnabled = enabled;
                }
                
                // Update hotkey labels opacity
                if (OptimizePreviewsHotkeyLabel != null) OptimizePreviewsHotkeyLabel.Opacity = enabled ? 1.0 : 0.5;
                if (ReloadManagerHotkeyLabel != null) ReloadManagerHotkeyLabel.Opacity = enabled ? 1.0 : 0.5;
                if (ShuffleActiveModsHotkeyLabel != null) ShuffleActiveModsHotkeyLabel.Opacity = enabled ? 1.0 : 0.5;
                if (DeactivateAllModsHotkeyLabel != null) DeactivateAllModsHotkeyLabel.Opacity = enabled ? 1.0 : 0.5;
                
                // Update hotkey descriptions opacity
                if (OptimizePreviewsHotkeyDescription != null) OptimizePreviewsHotkeyDescription.Opacity = enabled ? 0.7 : 0.3;
                if (ReloadManagerHotkeyDescription != null) ReloadManagerHotkeyDescription.Opacity = enabled ? 0.7 : 0.3;
                if (ShuffleActiveModsHotkeyDescription != null) ShuffleActiveModsHotkeyDescription.Opacity = enabled ? 0.7 : 0.3;
                if (DeactivateAllModsHotkeyDescription != null) DeactivateAllModsHotkeyDescription.Opacity = enabled ? 0.7 : 0.3;
                
                // Update hotkeys header opacity
                if (HotkeysHeader != null) HotkeysHeader.Opacity = enabled ? 1.0 : 0.5;
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
        }

        // Clean up event subscription when control is unloaded
        private void SettingsUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.WindowSizeChanged -= OnWindowSizeChanged;
            _windowSizeUpdateTimer?.Stop();
        }
    }
}