using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Text.Json;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class FunctionsUserControl : UserControl
    {
        public class FunctionInfo
        {
            public string FileName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool Enabled { get; set; }
        }

        private ObservableCollection<FunctionInfo> _functionInfos = new();

        public event EventHandler? CloseRequested; // Event to notify parent to close



        public FunctionsUserControl()
        {
            this.InitializeComponent();
            
            UpdateTexts();
            LoadFunctionsList();
            PopulateFunctionButtons();
            
            // Add slide-in animation for content
            this.Loaded += FunctionsUserControl_Loaded;
        }
        
        private void FunctionsUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Animate content sliding in from right with fade
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



        private void UpdateTexts()
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            FunctionsTitle.Text = SharedUtilities.GetTranslation(langDict, "FunctionsPage_Title");
            foreach (var func in _functionInfos)
            {
                if (func.FileName == "GBAuthorUpdate")
                {
                    var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                    func.Name = SharedUtilities.GetTranslation(lang, "GameBananaAuthorUpdate_Function");
                }
            }
            PopulateFunctionButtons();
        }

        private string GetFunctionIcon(string functionFileName)
        {
            return functionFileName switch
            {
                "GBAuthorUpdate" => "&#xE895;", // Update/Refresh icon
                "StatusKeeperPage" => "&#xE713;", // Settings icon
                "ModInfoBackup" => "&#xE8C8;", // Save icon
                "ImageOptimizer" => "&#xE91B;", // Image icon
                _ => "&#xE8B7;" // Default settings icon
            };
        }

        private string GetFunctionIconUnicode(string functionFileName)
        {
            return functionFileName switch
            {
                "GBAuthorUpdate" => "\uE895", // Update/Refresh icon (commonly available)
                "StatusKeeperPage" => "\uE713", // Settings icon (commonly available)
                "ModInfoBackup" => "\uE8C8", // Save icon (commonly available)
                "ImageOptimizer" => "\uE91B", // Image icon
                _ => "\uE8B7" // Default settings icon
            };
        }

        private string GetGameBananaFunctionName()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            return SharedUtilities.GetTranslation(lang, "GameBananaAuthorUpdate_Function");
        }

        private string GetStatusKeeperFunctionName()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            return SharedUtilities.GetTranslation(lang, "StatusKeeper_Function");
        }
        
        private string GetModInfoBackupFunctionName()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            return SharedUtilities.GetTranslation(lang, "ModInfoBackup_Function");
        }
        
        private string GetImageOptimizerFunctionName()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            return SharedUtilities.GetTranslation(lang, "ImageOptimizer_Function");
        }

        private void SaveFunctionSettings(FunctionInfo function)
        {
            string settingsDir = Path.Combine(System.AppContext.BaseDirectory, "Settings", "Functions");
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);
            string jsonPath = Path.Combine(settingsDir, function.FileName + ".json");
            var json = JsonSerializer.Serialize(new { function.Name, function.Enabled });
            File.WriteAllText(jsonPath, json);
        }

        private void LoadFunctionsList()
        {
            _functionInfos.Clear();
            string settingsDir = Path.Combine(System.AppContext.BaseDirectory, "Settings", "Functions");

            // Add GameBanana author update function
            var gbAuthorUpdateFunction = new FunctionInfo
            {
                FileName = "GBAuthorUpdate",
                Name = GetGameBananaFunctionName(),
                Enabled = true
            };
            string gbJsonPath = Path.Combine(settingsDir, gbAuthorUpdateFunction.FileName + ".json");
            if (File.Exists(gbJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(gbJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        gbAuthorUpdateFunction.Enabled = loaded.Enabled;
                        gbAuthorUpdateFunction.Name = loaded.Name;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load GBAuthorUpdate function settings", ex);
                }
            }
            _functionInfos.Add(gbAuthorUpdateFunction);

            // Add StatusKeeper function
            var statusKeeperFunction = new FunctionInfo
            {
                FileName = "StatusKeeperPage",
                Name = GetStatusKeeperFunctionName(),
                Enabled = true
            };
            string skJsonPath = Path.Combine(settingsDir, statusKeeperFunction.FileName + ".json");
            if (File.Exists(skJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(skJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        statusKeeperFunction.Enabled = loaded.Enabled;
                        statusKeeperFunction.Name = loaded.Name;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load StatusKeeper function settings", ex);
                }
            }
            _functionInfos.Add(statusKeeperFunction);

            // Add ModInfoBackup function
            var modInfoBackupFunction = new FunctionInfo
            {
                FileName = "ModInfoBackup",
                Name = GetModInfoBackupFunctionName(),
                Enabled = true
            };
            string mibJsonPath = Path.Combine(settingsDir, modInfoBackupFunction.FileName + ".json");
            if (File.Exists(mibJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(mibJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        modInfoBackupFunction.Enabled = loaded.Enabled;
                        modInfoBackupFunction.Name = loaded.Name;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load ModInfoBackup function settings", ex);
                }
            }
            _functionInfos.Add(modInfoBackupFunction);

            // Add ImageOptimizer function
            var imageOptimizerFunction = new FunctionInfo
            {
                FileName = "ImageOptimizer",
                Name = GetImageOptimizerFunctionName(),
                Enabled = true
            };
            string ioJsonPath = Path.Combine(settingsDir, imageOptimizerFunction.FileName + ".json");
            if (File.Exists(ioJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(ioJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        imageOptimizerFunction.Enabled = loaded.Enabled;
                        imageOptimizerFunction.Name = loaded.Name;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load ImageOptimizer function settings", ex);
                }
            }
            _functionInfos.Add(imageOptimizerFunction);
        }

        private void PopulateFunctionButtons()
        {
            // Update text for each function navigation item, set visibility based on enabled state
            foreach (var function in _functionInfos)
            {
                switch (function.FileName)
                {
                    case "GBAuthorUpdate":
                        // Update NavigationViewItem
                        GBAuthorUpdateNavItem.Content = "GameBanana Update";
                        GBAuthorUpdateNavItem.Visibility = function.Enabled ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case "StatusKeeperPage":
                        // Update NavigationViewItem
                        StatusKeeperNavItem.Content = "Status Keeper";
                        StatusKeeperNavItem.Visibility = function.Enabled ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case "ModInfoBackup":
                        // Update NavigationViewItem
                        ModInfoBackupNavItem.Content = "ModInfo Backup";
                        ModInfoBackupNavItem.Visibility = function.Enabled ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    
                    case "ImageOptimizer":
                        // Update NavigationViewItem
                        ImageOptimizerNavItem.Content = "Image Optimizer";
                        ImageOptimizerNavItem.Visibility = function.Enabled ? Visibility.Visible : Visibility.Collapsed;
                        break;
                }
            }
            
            // Select the first visible navigation item by default
            var firstVisibleNavItem = FunctionNavigationView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(item => item.Visibility == Visibility.Visible);
            
            if (firstVisibleNavItem != null)
            {
                FunctionNavigationView.SelectedItem = firstVisibleNavItem;
                
                var functionName = firstVisibleNavItem.Tag as string;
                var firstFunction = _functionInfos.FirstOrDefault(f => f.FileName == functionName);
                if (firstFunction != null)
                {
                    NavigateToFunction(firstFunction);
                }
            }
        }



        private void FunctionNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string functionName)
            {
                // Find function by name
                var function = _functionInfos.FirstOrDefault(f => f.FileName == functionName);
                if (function != null)
                {
                    NavigateToFunction(function);
                }
            }
        }



        private void NavigateToFunction(FunctionInfo function)
        {
            switch (function.FileName)
            {
                case "GBAuthorUpdate":
                    FunctionContentFrame.Navigate(typeof(GBAuthorUpdatePage));
                    break;

                case "StatusKeeperPage":
                    FunctionContentFrame.Navigate(typeof(StatusKeeperPage));
                    break;
                case "ModInfoBackup":
                    FunctionContentFrame.Navigate(typeof(ModInfoBackupPage));
                    break;
                
                case "ImageOptimizer":
                    FunctionContentFrame.Navigate(typeof(ImageOptimizerPage));
                    break;
            }
        }

        public void RefreshContent()
        {
            UpdateTexts();
            LoadFunctionsList();
        }
    }
}