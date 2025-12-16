using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using FlairX_Mod_Manager.Pages;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - Sliding panels functionality
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private void ShowSettingsPanel()
        {
            // Suppress menu regeneration while settings panel is open
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow._suppressMenuRegeneration = true;
            }
            
            var settingsControl = new SettingsUserControl();
            ShowSlidingPanel(settingsControl, "Settings");
        }

        private void ShowFunctionsPanel()
        {
            var functionsControl = new FunctionsUserControl();
            ShowSlidingPanel(functionsControl, "Functions");
        }

        private void ShowPresetsPanel()
        {
            var presetsControl = new PresetsUserControl();
            ShowSlidingPanel(presetsControl, "Presets");
        }

        public void ShowModDetailPanel(string modDirectory)
        {
            var modDetailControl = new ModDetailUserControl();
            _ = modDetailControl.LoadModDetails(modDirectory);
            ShowSlidingPanel(modDetailControl, "Mod Details");
        }

        public void NavigateToModDetails(string modDirectory)
        {
            // Same as ShowModDetailPanel - opens mod details in a new sliding panel
            // This will overlay on top of any existing panels (like Presets)
            ShowModDetailPanel(modDirectory);
        }

        public void ShowGameBananaBrowserPanel(string gameTag, string? modUrl = null)
        {
            var browserControl = new GameBananaBrowserUserControl(gameTag, modUrl);
            var lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");
            var gameName = gameTag switch
            {
                "ZZMI" => "Zenless Zone Zero",
                "GIMI" => "Genshin Impact",
                "HIMI" => "Honkai Impact 3rd",
                "WWMI" => "Wuthering Waves",
                "SRMI" => "Honkai Star Rail",
                _ => "Game"
            };
            var title = string.Format(SharedUtilities.GetTranslation(lang, "BrowseTitle"), gameName);
            ShowSlidingPanel(browserControl, title);
        }
        
        public void ShowImagePreviewPanel(System.Collections.Generic.List<string> imagePaths, int startIndex = 0, string title = "Preview")
        {
            var previewControl = new ImagePreviewUserControl();
            previewControl.LoadImages(imagePaths, startIndex, title);
            ShowSlidingPanel(previewControl, title);
        }



        // Method to update panel background when theme changes
        public void UpdateSlidingPanelTheme()
        {
            try
            {
                Logger.LogInfo("Updating sliding panel theme");
                
                // Find the MainRoot grid and look for overlay
                if (this.Content is NavigationView navView)
                {
                    Logger.LogInfo($"Found NavigationView, parent type: {navView.Parent?.GetType().Name}");
                    
                    if (navView.Parent is Grid mainRoot)
                    {
                        Logger.LogInfo($"Found MainRoot grid with {mainRoot.Children.Count} children");
                        SearchForPanelInGrid(mainRoot);
                    }
                    else
                    {
                        Logger.LogWarning("NavigationView parent is not Grid");
                    }
                }
                else
                {
                    Logger.LogWarning("Content is not NavigationView");
                    
                    // Try alternative approach - look for NavigationView in content
                    if (this.Content is FrameworkElement rootElement)
                    {
                        Logger.LogInfo($"Root element type: {rootElement.GetType().Name}");
                        
                        // Try to find NavigationView by name
                        var foundNavView = rootElement.FindName("nvSample") as NavigationView;
                        if (foundNavView != null)
                        {
                            Logger.LogInfo("Found NavigationView by name nvSample");
                            
                            if (foundNavView.Parent is Grid mainRoot)
                            {
                                Logger.LogInfo($"Found MainRoot grid with {mainRoot.Children.Count} children");
                                SearchForPanelInGrid(mainRoot);
                            }
                            else
                            {
                                Logger.LogWarning($"NavigationView parent is not Grid, it's: {foundNavView.Parent?.GetType().Name}");
                            }
                        }
                        else
                        {
                            Logger.LogWarning("Could not find NavigationView by name nvSample");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("PANEL: Failed to update sliding panel theme", ex);
            }
            
            Logger.LogInfo("Sliding panel theme updated");
        }

        private void SearchForPanelInGrid(Grid mainRoot)
        {
            // Look for overlay grid (should be the last child with transparent background)
            var overlays = mainRoot.Children.OfType<Grid>()
                .Where(g => g.Background is SolidColorBrush brush && 
                          brush.Color == Microsoft.UI.Colors.Transparent)
                .ToList();
            
            var overlay = overlays.LastOrDefault();
            if (overlay != null)
            {
                var borders = overlay.Children.OfType<Border>().ToList();
                var dialogContainer = borders.FirstOrDefault(b => b.HorizontalAlignment == HorizontalAlignment.Right);
                
                if (dialogContainer != null)
                {
                    Logger.LogInfo("Panel found, updating background");
                    UpdatePanelBackground(dialogContainer);
                }
                else
                {
                    // Try any border as fallback
                    var anyBorder = borders.FirstOrDefault();
                    if (anyBorder != null)
                    {
                        Logger.LogInfo("Using fallback panel");
                        UpdatePanelBackground(anyBorder);
                    }
                }
            }
        }



        private void UpdatePanelBackground(Border dialogContainer)
        {
            // Get current app theme
            string appTheme = FlairX_Mod_Manager.SettingsManager.Current.Theme ?? "Auto";
            bool isDarkTheme = false;
            
            if (appTheme == "Dark")
                isDarkTheme = true;
            else if (appTheme == "Light")
                isDarkTheme = false;
            else if (this.Content is FrameworkElement rootElement)
                isDarkTheme = rootElement.ActualTheme == ElementTheme.Dark;

            Logger.LogInfo($"Applying {(isDarkTheme ? "dark" : "light")} theme to panel");

            // Create new background and border brush
            Microsoft.UI.Xaml.Media.AcrylicBrush dialogAcrylicBrush;
            Brush borderBrush;
            
            if (isDarkTheme)
            {
                dialogAcrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                {
                    TintColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32),
                    TintOpacity = 0.85,
                    FallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32)
                };
                borderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(80, 255, 255, 255));
            }
            else
            {
                dialogAcrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                {
                    TintColor = Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248),
                    TintOpacity = 0.85,
                    FallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248)
                };
                borderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(80, 0, 0, 0));
            }

            // Update background immediately (matches system theme change speed)
            dialogContainer.Background = dialogAcrylicBrush;
            dialogContainer.BorderBrush = borderBrush;
        }



        private void ShowSlidingPanel(UserControl userControl, string title)
        {
            try
            {
                // Get current app theme and create appropriate background
                string appTheme = FlairX_Mod_Manager.SettingsManager.Current.Theme ?? "Auto";
                bool isDarkTheme = false;
                
                if (appTheme == "Dark")
                    isDarkTheme = true;
                else if (appTheme == "Light")
                    isDarkTheme = false;
                else if (this.Content is FrameworkElement rootElement)
                    isDarkTheme = rootElement.ActualTheme == ElementTheme.Dark;

                // Create transparent overlay
                var overlay = new Grid
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                // No size restrictions - UserControls will auto-size
                
                // Create dialog with acrylic background for blur effect
                Microsoft.UI.Xaml.Media.AcrylicBrush dialogAcrylicBrush;
                Brush borderBrush;
                
                if (isDarkTheme)
                {
                    dialogAcrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                    {
                        TintColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32),
                        TintOpacity = 0.85,
                        FallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32)
                    };
                    borderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(80, 255, 255, 255));
                }
                else
                {
                    dialogAcrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                    {
                        TintColor = Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248),
                        TintOpacity = 0.85,
                        FallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248)
                    };
                    borderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(80, 0, 0, 0));
                }

                // Create panel sliding from right - full height, width to menu edge
                var dialogContainer = new Border
                {
                    Background = dialogAcrylicBrush,
                    CornerRadius = new CornerRadius(12, 0, 0, 0), // Rounded only on top-left
                    HorizontalAlignment = HorizontalAlignment.Stretch, // All panels stretch to full width
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(320, 0, 0, 0), // Start after menu (320px menu width)
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1, 0, 0, 0) // Only left border
                };

                // Create main grid for content - fill entire container
                var mainGrid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                
                // Set UserControl to fill available space completely
                userControl.HorizontalAlignment = HorizontalAlignment.Stretch;
                userControl.VerticalAlignment = VerticalAlignment.Stretch;
                userControl.Margin = new Thickness(12); // Smaller margins for more space
                
                // Set sizing for all controls
                userControl.Width = double.NaN; // Auto width - fill available space
                userControl.Height = double.NaN; // Auto height - fill container

                mainGrid.Children.Add(userControl);
                dialogContainer.Child = mainGrid;
                overlay.Children.Add(dialogContainer);

                // Add slide-in animation from right
                var slideTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();
                dialogContainer.RenderTransform = slideTransform;
                
                // Start off-screen to the right
                slideTransform.X = 800;
                
                // Function to start animation
                Action startAnimation = () =>
                {
                    // Animate sliding in with a slightly longer duration for FunctionsUserControl
                    var duration = userControl is FunctionsUserControl ? 
                        new Duration(TimeSpan.FromMilliseconds(350)) : 
                        new Duration(TimeSpan.FromMilliseconds(300));
                    
                    var slideAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 800,
                        To = 0,
                        Duration = duration,
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideAnimation, slideTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideAnimation, "X");
                    
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    storyboard.Children.Add(slideAnimation);
                    storyboard.Begin();
                };

                // Start animation immediately for all controls
                startAnimation();

                // Add overlay to parent grid for fullscreen effect
                Grid? parentGrid = null;
                
                // Try to find the Frame's parent grid (fullscreen but not affecting menu)
                var current = this.Content as FrameworkElement; // this.Content is NavigationView
                while (current != null && !(current is Grid))
                {
                    current = current.Parent as FrameworkElement;
                }
                parentGrid = current as Grid; // This should be MainRoot

                if (parentGrid != null)
                {
                    parentGrid.Children.Add(overlay);

                    // Function to close with slide-out animation
                    Action closeWithAnimation = () =>
                    {
                        // Create slide-out animation with consistent duration
                        var duration = userControl is FunctionsUserControl ? 
                            new Duration(TimeSpan.FromMilliseconds(300)) : 
                            new Duration(TimeSpan.FromMilliseconds(250));
                        
                        var slideOutAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = dialogContainer.ActualWidth > 0 ? dialogContainer.ActualWidth : 800,
                            Duration = duration,
                            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                        };
                        
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideOutAnimation, slideTransform);
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideOutAnimation, "X");
                        
                        var slideOutStoryboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                        slideOutStoryboard.Children.Add(slideOutAnimation);
                        
                        // Remove overlay when animation completes
                        slideOutStoryboard.Completed += (s, e) => 
                        {
                            parentGrid.Children.Remove(overlay);
                            
                            // Restore menu regeneration when settings panel is closed
                            if (userControl is SettingsUserControl && App.Current is App app && app.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow._suppressMenuRegeneration = false;
                            }
                        };
                        slideOutStoryboard.Begin();
                    };

                    // Handle CloseRequested event from UserControl
                    if (userControl is SettingsUserControl settingsControl)
                    {
                        settingsControl.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    else if (userControl is PresetsUserControl presetsControl)
                    {
                        presetsControl.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    else if (userControl is FunctionsUserControl functionsControl)
                    {
                        functionsControl.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    else if (userControl is Pages.GameBananaBrowserUserControl gameBananaControl)
                    {
                        gameBananaControl.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    else if (userControl is ModDetailUserControl modDetailControl)
                    {
                        modDetailControl.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    else if (userControl is Controls.ImageCropInspectionPanel cropPanel)
                    {
                        cropPanel.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    else if (userControl is Controls.MinitileSourceSelectionPanel minitilePanel)
                    {
                        minitilePanel.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    else if (userControl is ImagePreviewUserControl imagePreviewControl)
                    {
                        imagePreviewControl.CloseRequested += (s, args) => closeWithAnimation();
                    }
                    
                    // Click outside to close
                    overlay.Tapped += (s, args) =>
                    {
                        if (ReferenceEquals(args.OriginalSource, overlay))
                            closeWithAnimation();
                    };

                    // Escape key handler
                    overlay.KeyDown += (s, args) =>
                    {
                        if (args.Key == Windows.System.VirtualKey.Escape)
                        {
                            closeWithAnimation();
                            args.Handled = true;
                        }
                    };

                    // Make overlay focusable to receive key events
                    overlay.IsTabStop = true;
                    overlay.Focus(FocusState.Programmatic);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show sliding panel for {title}", ex);
            }
        }
    }
}