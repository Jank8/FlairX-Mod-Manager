using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using FlairX_Mod_Manager;
using FlairX_Mod_Manager.Models;
using FlairX_Mod_Manager.Services;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using SharpImage = SixLabors.ImageSharp.Image;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class SettingsUserControl : UserControl
    {
        private readonly string LanguageFolderPath = PathManager.GetAbsolutePath("Language");
        private Microsoft.UI.Xaml.DispatcherTimer? _windowSizeUpdateTimer;
        private bool _categoryManagementChanged = false;
        private Dictionary<string, string> _languages = new(); // displayName, filePath
        private Dictionary<string, string> _fileNameByDisplayName = new();

        public class SettingsClosedEventArgs : EventArgs
        {
            public bool CategoryManagementChanged { get; set; }
        }

        public event EventHandler<SettingsClosedEventArgs>? CloseRequested; // Event to notify parent to close

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

        public SettingsUserControl(string? initialTab = null)
        {
            this.InitializeComponent();
            SettingsManager.Load();
            LoadLanguages();
            InitializeUIState();
            LoadSettingsHotkeys();
            LoadCategoryManagement();
            
            // Initialize timer for delayed window size updates
            _windowSizeUpdateTimer = new Microsoft.UI.Xaml.DispatcherTimer();
            _windowSizeUpdateTimer.Interval = TimeSpan.FromMilliseconds(200);
            _windowSizeUpdateTimer.Tick += WindowSizeUpdateTimer_Tick;
            
            // Subscribe to window size changes
            MainWindow.WindowSizeChanged += OnWindowSizeChanged;
            
            // Subscribe to loaded/unloaded events
            this.Loaded += SettingsUserControl_Loaded;
            this.Unloaded += SettingsUserControl_Unloaded;
            
            if (SettingsNavigationView != null && GeneralSettingsNavItem != null)
                SettingsNavigationView.SelectedItem = GeneralSettingsNavItem;
        }

        public void SelectGBAuthorUpdateTab()
        {
            if (GBAuthorUpdateNavItem != null)
                SettingsNavigationView.SelectedItem = GBAuthorUpdateNavItem;
        }

        public void NavigateToTab(string tag)
        {
            if (SettingsNavigationView == null) return;
            
            var item = SettingsNavigationView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == tag);
            if (item != null)
                SettingsNavigationView.SelectedItem = item;

            // Manually trigger the navigation logic
            if (tag == "GeneralSettings")
            {
                GeneralSettingsScrollViewer.Visibility = Visibility.Visible;
                FunctionContentFrame.Visibility = Visibility.Collapsed;
            }
            else
            {
                GeneralSettingsScrollViewer.Visibility = Visibility.Collapsed;
                FunctionContentFrame.Visibility = Visibility.Visible;

                var navInfo = new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo();
                switch (tag)
                {
                    case "ModImporter":
                        FunctionContentFrame.Navigate(typeof(ModImporterPage), null, navInfo);
                        break;
                    case "GBAuthorUpdate":
                        FunctionContentFrame.Navigate(typeof(GBAuthorUpdatePage), null, navInfo);
                        break;
                    case "StatusKeeperPage":
                        FunctionContentFrame.Navigate(typeof(StatusKeeperPage), null, navInfo);
                        break;
                    case "ModInfoBackup":
                        FunctionContentFrame.Navigate(typeof(ModInfoBackupPage), null, navInfo);
                        break;
                    case "ImageOptimizer":
                        FunctionContentFrame.Navigate(typeof(ImageOptimizerPage), null, navInfo);
                        break;
                    case "GameOverlay":
                        FunctionContentFrame.Navigate(typeof(GameOverlayPage), null, navInfo);
                        break;
                }
            }
        }
        
        private void SettingsUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Refresh settings when page is loaded to ensure SelectorBars display correctly
            LoadCurrentSettings();
            
            // Add slide-in animation for content
            var slideTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();
            MainGrid.RenderTransform = slideTransform;
            
            // Start off-screen to the right and invisible
            slideTransform.X = 300;
            MainGrid.Opacity = 0;
            
            var slideAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 300,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            
            var fadeAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideAnimation, slideTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideAnimation, "X");
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeAnimation, MainGrid);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(slideAnimation);
            storyboard.Children.Add(fadeAnimation);
            
            storyboard.Begin();
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Notify parent to close the panel with category management change info
            CloseRequested?.Invoke(this, new SettingsClosedEventArgs 
            { 
                CategoryManagementChanged = _categoryManagementChanged 
            });
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
            if (AutoReloadModsToggleLabel != null && AutoReloadModsToggle != null)
                AutoReloadModsToggleLabel.Text = AutoReloadModsToggle.IsOn ? onText : offText;
            if (ActiveModsToTopToggleLabel != null && ActiveModsToTopToggle != null)
                ActiveModsToTopToggleLabel.Text = ActiveModsToTopToggle.IsOn ? onText : offText;
            if (AutoDeactivateConflictingModsToggleLabel != null && AutoDeactivateConflictingModsToggle != null)
                AutoDeactivateConflictingModsToggleLabel.Text = AutoDeactivateConflictingModsToggle.IsOn ? onText : offText;
            if (DynamicModSearchToggleLabel != null && DynamicModSearchToggle != null)
                DynamicModSearchToggleLabel.Text = DynamicModSearchToggle.IsOn ? onText : offText;
            if (ShowOrangeAnimationToggleLabel != null && ShowOrangeAnimationToggle != null)
                ShowOrangeAnimationToggleLabel.Text = ShowOrangeAnimationToggle.IsOn ? onText : offText;
            if (GridLoggingToggleLabel != null && GridLoggingToggle != null)
                GridLoggingToggleLabel.Text = GridLoggingToggle.IsOn ? onText : offText;
            if (ErrorOnlyLoggingToggleLabel != null && ErrorOnlyLoggingToggle != null)
                ErrorOnlyLoggingToggleLabel.Text = ErrorOnlyLoggingToggle.IsOn ? onText : offText;
            if (AnonymizeLogsToggleLabel != null && AnonymizeLogsToggle != null)
                AnonymizeLogsToggleLabel.Text = AnonymizeLogsToggle.IsOn ? onText : offText;
            if (MinimizeToTrayToggleLabel != null && MinimizeToTrayToggle != null)
                MinimizeToTrayToggleLabel.Text = MinimizeToTrayToggle.IsOn ? onText : offText;
            if (BlurNSFWToggleLabel != null && BlurNSFWToggle != null)
                BlurNSFWToggleLabel.Text = BlurNSFWToggle.IsOn ? onText : offText;
            if (HideEmptyCategoriesToggleLabel != null && HideEmptyCategoriesToggle != null)
                HideEmptyCategoriesToggleLabel.Text = HideEmptyCategoriesToggle.IsOn ? onText : offText;
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
            ReportIssueButtonText.Text = SharedUtilities.GetTranslation(lang, "ReportIssue_Button") ?? "Report Issue";
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
            if (AutoReloadModsLabel != null) AutoReloadModsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoReloadMods_Label") ?? "Auto Reload Mods";
            if (ActiveModsToTopLabel != null) ActiveModsToTopLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ActiveModsToTop_Label");
            if (AutoDeactivateConflictingModsLabel != null) AutoDeactivateConflictingModsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoDeactivateConflictingMods_Label");
            if (DynamicModSearchLabel != null) DynamicModSearchLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Label");
            if (ShowOrangeAnimationLabel != null) ShowOrangeAnimationLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShowOrangeAnimation_Label");
            if (GridLoggingLabel != null) GridLoggingLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Label");
            if (ErrorOnlyLoggingLabel != null) ErrorOnlyLoggingLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ErrorOnlyLogging_Label");
            if (AnonymizeLogsLabel != null) AnonymizeLogsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AnonymizeLogs_Label");
            if (MinimizeToTrayLabel != null) MinimizeToTrayLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MinimizeToTray_Label");
            if (BlurNSFWLabel != null) BlurNSFWLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HideNSFW_Label");
            if (HideEmptyCategoriesLabel != null) HideEmptyCategoriesLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HideEmptyCategories_Label") ?? "Hide Empty Categories";
            if (CategoryManagementHeader != null) CategoryManagementHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_CategoryManagement_Header") ?? "Category Management";
            if (PinnedCategoriesLabel != null) PinnedCategoriesLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_PinnedCategories_Label") ?? "Pinned Categories";
            if (HiddenCategoriesLabel != null) HiddenCategoriesLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HiddenCategories_Label") ?? "Hidden Categories";
            if (ShuffleExcludedCategoriesLabel != null) ShuffleExcludedCategoriesLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShuffleExcludedCategories_Label") ?? "Shuffle Excluded Categories";
            if (ConflictExcludedCategoriesLabel != null) ConflictExcludedCategoriesLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ConflictExcludedCategories_Label") ?? "Conflict Excluded Categories";
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
            if (AutoReloadModsDescription != null) AutoReloadModsDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoReloadMods_Description") ?? "Automatically send F10 to reload mods when the overlay closes";
            if (ActiveModsToTopDescription != null) ActiveModsToTopDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ActiveModsToTop_Description") ?? string.Empty;
            if (AutoDeactivateConflictingModsDescription != null) AutoDeactivateConflictingModsDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoDeactivateConflictingMods_Description") ?? string.Empty;
            if (DynamicModSearchDescription != null) DynamicModSearchDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Description") ?? string.Empty;
            if (ShowOrangeAnimationDescription != null) ShowOrangeAnimationDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShowOrangeAnimation_Description") ?? string.Empty;
            if (GridLoggingDescription != null) GridLoggingDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Description") ?? string.Empty;
            if (ErrorOnlyLoggingDescription != null) ErrorOnlyLoggingDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ErrorOnlyLogging_Description") ?? string.Empty;
            if (AnonymizeLogsDescription != null) AnonymizeLogsDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AnonymizeLogs_Description") ?? string.Empty;
            if (MinimizeToTrayDescription != null) MinimizeToTrayDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_MinimizeToTray_Description") ?? string.Empty;
            if (BlurNSFWDescription != null) BlurNSFWDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HideNSFW_Description") ?? string.Empty;
            if (HideEmptyCategoriesDescription != null) HideEmptyCategoriesDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HideEmptyCategories_Description") ?? "Hide categories that contain no mods";
            if (BlacklistLabel != null) BlacklistLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Blacklist_Label") ?? "GameBanana Blacklist";
            if (BlacklistDescription != null) BlacklistDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Blacklist_Description") ?? "Hide mods from specific authors or containing certain keywords";
            if (BlacklistedAuthorsHeader != null) BlacklistedAuthorsHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_BlacklistedAuthors_Header") ?? "Blacklisted Authors";
            if (BlacklistAuthorTextBox != null) BlacklistAuthorTextBox.PlaceholderText = SharedUtilities.GetTranslation(lang, "SettingsPage_BlacklistedAuthors_Placeholder") ?? "Enter author name...";
            if (AddBlacklistAuthorButton != null) AddBlacklistAuthorButton.Content = SharedUtilities.GetTranslation(lang, "SettingsPage_Blacklist_Add") ?? "Add";
            if (BlacklistedKeywordsHeader != null) BlacklistedKeywordsHeader.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_BlacklistedKeywords_Header") ?? "Blacklisted Keywords";
            if (BlacklistTagTextBox != null) BlacklistTagTextBox.PlaceholderText = SharedUtilities.GetTranslation(lang, "SettingsPage_BlacklistedKeywords_Placeholder") ?? "Enter keyword to filter...";
            if (AddBlacklistTagButton != null) AddBlacklistTagButton.Content = SharedUtilities.GetTranslation(lang, "SettingsPage_Blacklist_Add") ?? "Add";
            if (PinnedCategoriesDescription != null) PinnedCategoriesDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_PinnedCategories_Description") ?? "Pin categories to footer menu for quick access";
            if (HiddenCategoriesDescription != null) HiddenCategoriesDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_HiddenCategories_Description") ?? "Hide categories from the navigation menu";
            if (ShuffleExcludedCategoriesDescription != null) ShuffleExcludedCategoriesDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShuffleExcludedCategories_Description") ?? "Exclude categories from shuffle hotkey (keeps mods as-is)";
            if (ConflictExcludedCategoriesDescription != null) ConflictExcludedCategoriesDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ConflictExcludedCategories_Description") ?? "Exclude categories from conflict detection (all mods remain active)";
            if (AddPinnedCategoryComboBox != null) AddPinnedCategoryComboBox.PlaceholderText = SharedUtilities.GetTranslation(lang, "SettingsPage_PinnedCategories_Placeholder") ?? string.Empty;
            if (AddHiddenCategoryComboBox != null) AddHiddenCategoryComboBox.PlaceholderText = SharedUtilities.GetTranslation(lang, "SettingsPage_HiddenCategories_Placeholder") ?? string.Empty;
            if (AddShuffleExcludedCategoryComboBox != null) AddShuffleExcludedCategoryComboBox.PlaceholderText = SharedUtilities.GetTranslation(lang, "SettingsPage_ShuffleExcludedCategories_Placeholder") ?? string.Empty;
            if (AddConflictExcludedCategoryComboBox != null) AddConflictExcludedCategoryComboBox.PlaceholderText = SharedUtilities.GetTranslation(lang, "SettingsPage_ConflictExcludedCategories_Placeholder") ?? string.Empty;
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
            if (GridLoggingToggle != null) ToolTipService.SetToolTip(GridLoggingToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Tooltip"));

            if (ActiveModsToTopToggle != null) ToolTipService.SetToolTip(ActiveModsToTopToggle, SharedUtilities.GetTranslation(lang, "ActiveModsToTop_Tooltip"));
            if (XXMIRootDirectoryDefaultButton != null) ToolTipService.SetToolTip(XXMIRootDirectoryDefaultButton, SharedUtilities.GetTranslation(lang, "SettingsPage_RestoreDefault_Tooltip"));
            if (XXMIRootDirectoryPickButton != null) ToolTipService.SetToolTip(XXMIRootDirectoryPickButton, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
                        if (XXMIRootDirectoryBreadcrumb != null) ToolTipService.SetToolTip(XXMIRootDirectoryBreadcrumb, SharedUtilities.GetTranslation(lang, "OpenDirectory_Tooltip"));
                        if (DynamicModSearchToggle != null) ToolTipService.SetToolTip(DynamicModSearchToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Tooltip"));

            // Bottom buttons
            if (CheckUpdatesButtonText != null) CheckUpdatesButtonText.Text = SharedUtilities.GetTranslation(lang, "CheckForUpdates");
            if (ReportIssueButtonText != null) ReportIssueButtonText.Text = SharedUtilities.GetTranslation(lang, "ReportIssue_Button") ?? "Report Issue";
            if (AboutButtonText != null) AboutButtonText.Text = SharedUtilities.GetTranslation(lang, "AboutButton_Label");

            // Reload hotkey rows with new language
            if (SettingsHotkeysPanel != null)
                LoadSettingsHotkeys();
        }

        private async void LoadCurrentSettings()
        {
            try
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
                AnonymizeLogsToggle.IsOn = SettingsManager.Current.AnonymizeLogsEnabled;
                MinimizeToTrayToggle.IsOn = SettingsManager.Current.MinimizeToTrayEnabled;
                BlurNSFWToggle.IsOn = SettingsManager.Current.HideNSFWMods;
                HideEmptyCategoriesToggle.IsOn = SettingsManager.Current.HideEmptyCategories;
                HotkeysEnabledToggle.IsOn = SettingsManager.Current.HotkeysEnabled;

                // Load blacklist
                LoadBlacklistSettings();

                // Set download settings
                FastDownloadToggle.IsOn = SettingsManager.GetFastDownloadEnabled();
                MaxConnectionsNumberBox.Value = SettingsManager.GetMaxDownloadConnections();

                // Update hotkeys section state
                UpdateHotkeysSectionState(SettingsManager.Current.HotkeysEnabled);

                ShowOrangeAnimationToggle.IsOn = SettingsManager.Current.ShowOrangeAnimation;
                SkipXXMILauncherToggle.IsOn = SettingsManager.Current.SkipXXMILauncherEnabled;
                AutoReloadModsToggle.IsOn = SettingsManager.Current.SendF10OnModChange;
                if (SettingsManager.Current.SendF10OnModChange)
                {
                    GameOverlayPage.EnsureBackgroundKeypressIni(true);
                }
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
                        // Get the client area size (content area without title bar and borders)
                        if (mainWindow.Content is FrameworkElement content)
                        {
                            DefaultStartWidthTextBox.Text = ((int)content.ActualWidth).ToString();
                            DefaultStartHeightTextBox.Text = ((int)content.ActualHeight).ToString();
                        }
                        else
                        {
                            // Fallback to window size if content is not available
                            DefaultStartWidthTextBox.Text = appWindow.Size.Width.ToString();
                            DefaultStartHeightTextBox.Text = appWindow.Size.Height.ToString();
                        }
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
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("SettingsUserControl.LoadCurrentSettings failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("SettingsUserControl.LoadCurrentSettings failed", ex);
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

        private void AnonymizeLogsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.AnonymizeLogsEnabled = AnonymizeLogsToggle.IsOn;
            Logger.AnonymizeOutput = AnonymizeLogsToggle.IsOn;
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
            // Synchronize both NSFW settings (for mod grid and GameBanana browser)
            SettingsManager.Current.HideNSFWMods = BlurNSFWToggle.IsOn;
            SettingsManager.Current.BlurNSFWThumbnails = BlurNSFWToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
            
            // Dynamically filter NSFW mods in ModGridPage
            var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
            if (mainWindow?.CurrentModGridPage != null)
            {
                mainWindow.CurrentModGridPage.FilterNSFWMods(SettingsManager.Current.HideNSFWMods);
            }
        }

        private void HideEmptyCategoriesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.HideEmptyCategories = HideEmptyCategoriesToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();

            // Mark category management as changed so menu rebuilds on panel close too
            _categoryManagementChanged = true;

            var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
            // Reload category tiles in ModGridPage
            if (mainWindow?.CurrentModGridPage != null)
            {
                mainWindow.CurrentModGridPage.ReloadIfInCategoryView();
            }
            // Rebuild left column navigation menu — bypass suppression so it updates live while settings panel is open
            if (mainWindow != null)
            {
                _ = mainWindow.ForceGenerateModCharacterMenuAsync();
            }
        }

        public void RefreshContent()
        {
            LoadLanguages();
            LoadCurrentSettings();
            UpdateTexts();
        }

        private void SkipXXMILauncherToggle_Toggled(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo($"SkipXXMILauncher toggle changed to: {SkipXXMILauncherToggle.IsOn}");
            SettingsManager.Current.SkipXXMILauncherEnabled = SkipXXMILauncherToggle.IsOn;
            SettingsManager.Save();
            Logger.LogInfo($"SkipXXMILauncher setting saved: {SettingsManager.Current.SkipXXMILauncherEnabled}");
            UpdateToggleLabels();
        }

        private void AutoReloadModsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.SendF10OnModChange = AutoReloadModsToggle.IsOn;
            SettingsManager.Save();
            
            GameOverlayPage.EnsureBackgroundKeypressIni(AutoReloadModsToggle.IsOn);
            
            // Update toggle label
            var lang = SharedUtilities.LoadLanguageDictionary();
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            if (AutoReloadModsToggleLabel != null)
                AutoReloadModsToggleLabel.Text = AutoReloadModsToggle.IsOn ? onText : offText;
            
            Logger.LogInfo($"Auto-reload mods: {AutoReloadModsToggle.IsOn}");
        }

        // Clean up event subscription when control is unloaded
        private void SettingsUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            MainWindow.WindowSizeChanged -= OnWindowSizeChanged;
            _windowSizeUpdateTimer?.Stop();

            // Leaving the page in the middle of recording used to leave hotkeys disabled in
            // memory; restore the user's preference so the next settings save persists it.
            if (_activeHotkeyEditBox != null)
            {
                _activeHotkeyEditBox = null;
                SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
            }
        }

    }
}
