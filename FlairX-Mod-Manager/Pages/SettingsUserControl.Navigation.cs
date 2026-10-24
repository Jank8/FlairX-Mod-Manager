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
    public sealed partial class SettingsUserControl
    {
        

        
        private void SettingsNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string tag)
            {
                if (tag == "GeneralSettings")
                {
                    GeneralSettingsScrollViewer.Visibility = Visibility.Visible;
                    FunctionContentFrame.Visibility = Visibility.Collapsed;
                    AnimateContentFadeIn(GeneralSettingsScrollViewer);
                }
                else if (tag == "DevTools")
                {
                    GeneralSettingsScrollViewer.Visibility = Visibility.Collapsed;
                    FunctionContentFrame.Visibility = Visibility.Visible;
                    var navInfo = new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo();
                    FunctionContentFrame.Navigate(typeof(DevToolsPage), null, navInfo);
                    AnimateContentFadeIn(FunctionContentFrame);
                }
                else
                {
                    // Hide general settings, show function page
                    GeneralSettingsScrollViewer.Visibility = Visibility.Collapsed;
                    FunctionContentFrame.Visibility = Visibility.Visible;
                    
                    // Navigate to the appropriate function page without animation
                    var navigationTransitionInfo = new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo();
                    
                    switch (tag)
                    {
                        case "ModImporter":
                            FunctionContentFrame.Navigate(typeof(ModImporterPage), null, navigationTransitionInfo);
                            break;
                        case "GBAuthorUpdate":
                            FunctionContentFrame.Navigate(typeof(GBAuthorUpdatePage), null, navigationTransitionInfo);
                            break;
                        case "StatusKeeperPage":
                            FunctionContentFrame.Navigate(typeof(StatusKeeperPage), null, navigationTransitionInfo);
                            break;
                        case "ModInfoBackup":
                            FunctionContentFrame.Navigate(typeof(ModInfoBackupPage), null, navigationTransitionInfo);
                            break;
                        case "ImageOptimizer":
                            FunctionContentFrame.Navigate(typeof(ImageOptimizerPage), null, navigationTransitionInfo);
                            break;
                        case "GameOverlay":
                            FunctionContentFrame.Navigate(typeof(GameOverlayPage), null, navigationTransitionInfo);
                            break;
                    }
                    
                    // Animate frame content
                    AnimateContentFadeIn(FunctionContentFrame);
                }
            }
        }
        
        private void AnimateContentFadeIn(UIElement element)
        {
            // Create slide and fade animation
            var slideTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();
            element.RenderTransform = slideTransform;
            
            // Start off-screen to the right and invisible
            slideTransform.X = 300;
            element.Opacity = 0;
            
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
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeAnimation, element);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(slideAnimation);
            storyboard.Children.Add(fadeAnimation);
            
            storyboard.Begin();
        }
        


        public void ToggleDevTools()
        {
            var isVisible = DevToolsNavItem.Visibility == Visibility.Visible;
            DevToolsNavItem.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            if (!isVisible)
                SettingsNavigationView.SelectedItem = DevToolsNavItem;
        }

        public bool IsDevToolsVisible => DevToolsNavItem.Visibility == Visibility.Visible;
    }
}
