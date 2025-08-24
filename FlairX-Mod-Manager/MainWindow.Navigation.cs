using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - Navigation and search functionality
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string? selectedTag = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(selectedTag))
                {
                    if (selectedTag.StartsWith("Category_"))
                    {
                        var category = selectedTag.Substring("Category_".Length);
                        
                        // Save position for reload
                        var currentViewMode = GetCurrentViewModeString();
                        SettingsManager.SaveLastPosition(category, "ModGridPage", currentViewMode);
                        
                        // If we're already on ModGridPage, just load the category without navigating
                        if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                        {
                            // SEPARATE NAVIGATION BASED ON CURRENT VIEW MODE
                            if (modGridPage.CurrentViewMode == FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Categories)
                            {
                                modGridPage.LoadCategoryInCategoryMode(category);
                            }
                            else
                            {
                                modGridPage.LoadCategoryInDefaultMode(category);
                            }
                        }
                        else
                        {
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), $"Category:{category}", new DrillInNavigationTransitionInfo());
                        }
                    }
                    else if (selectedTag == "OtherModsPage")
                    {
                        // Save position for reload
                        var currentViewMode = GetCurrentViewModeString();
                        SettingsManager.SaveLastPosition("Other", "ModGridPage", currentViewMode);
                        
                        // Load "Other" category instead of old character-based filtering
                        if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                        {
                            // SEPARATE NAVIGATION BASED ON CURRENT VIEW MODE
                            if (modGridPage.CurrentViewMode == FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Categories)
                            {
                                modGridPage.LoadCategoryInCategoryMode("Other");
                            }
                            else
                            {
                                modGridPage.LoadCategoryInDefaultMode("Other");
                            }
                        }
                        else
                        {
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Category:Other", new DrillInNavigationTransitionInfo());
                        }
                    }
                    else if (selectedTag == "FunctionsPage")
                    {
                        // Save position for reload
                        SettingsManager.SaveLastPosition(null, "FunctionsPage", null);
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.FunctionsPage), null, new DrillInNavigationTransitionInfo());
                    }
                    else if (selectedTag == "SettingsPage")
                    {
                        // Save position for reload
                        SettingsManager.SaveLastPosition(null, "SettingsPage", null);
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
                        // Force restore view mode from settings when entering settings
                        RestoreViewModeButtonFromSettings();
                        // Also force update button state
                        UpdateAllModsButtonState();
                    }
                    else
                    {
                        var pageType = Type.GetType($"FlairX_Mod_Manager.Pages.{selectedTag}");
                        if (pageType != null)
                        {
                            contentFrame.Navigate(pageType, null, new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Navigation failed: Page type for tag '{selectedTag}' not found.");
                        }
                    }
                }
            }
        }

        private void NavigationView_PaneClosing(NavigationView sender, object args)
        {
            // Hide title and elements when panel is closing
            if (PaneTitleText != null)
                PaneTitleText.Visibility = Visibility.Collapsed;
            if (OrangeAnimationProgressBar != null)
                OrangeAnimationProgressBar.Visibility = Visibility.Collapsed;
            if (PaneContentGrid != null)
                PaneContentGrid.Visibility = Visibility.Collapsed;
            if (PaneButtonsGrid != null)
                PaneButtonsGrid.Visibility = Visibility.Collapsed;
        }

        private void NavigationView_PaneOpening(NavigationView sender, object args)
        {
            // Show title and elements when panel is opening
            if (PaneTitleText != null)
                PaneTitleText.Visibility = Visibility.Visible;
            if (OrangeAnimationProgressBar != null)
                OrangeAnimationProgressBar.Visibility = Visibility.Visible;
            if (PaneContentGrid != null)
                PaneContentGrid.Visibility = Visibility.Visible;
            if (PaneButtonsGrid != null)
                PaneButtonsGrid.Visibility = Visibility.Visible;
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            string query = sender.Text.Trim().ToLower();

            // If search is empty, restore all menu items by refreshing the character categories
            // and navigate appropriately based on current view mode
            if (string.IsNullOrEmpty(query))
            {
                // First clear the mod filter if we're on ModGridPage
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                {
                    modGridPage.FilterMods("");
                }
                
                // Then regenerate the menu on the UI thread (not in Task.Run)
                // Only generate menu if a game is selected
                if (SettingsManager.Current?.SelectedGameIndex > 0)
                {
                    _ = GenerateModCharacterMenuAsync();
                }
                
                // Navigate to appropriate page based on view mode
                if (IsCurrentlyInCategoryMode())
                {
                    // In category mode, navigate to ModGridPage and load all categories
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPageForCategories)
                    {
                        modGridPageForCategories.LoadAllCategories();
                    }
                    else
                    {
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage));
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage newModGridPageForCategories)
                            {
                                newModGridPageForCategories.LoadAllCategories();
                            }
                        });
                    }
                }
                // In mods mode, stay on current page (ModGridPage if already there)
                
                return;
            }

            // For any non-empty search, we need to regenerate the full menu first
            // then filter it, to ensure we're always filtering from the complete menu
            Task.Run(async () =>
            {
                // First regenerate the complete menu
                await GenerateModCharacterMenuAsync();
                
                // Then apply the filter on the UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Get current menu items (now the complete character categories)
                    var currentMenuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
                    var currentFooterItems = nvSample.FooterMenuItems.OfType<NavigationViewItem>().ToList();

                    nvSample.MenuItems.Clear();
                    nvSample.FooterMenuItems.Clear();

                    // Filter menu items based on search query
                    foreach (var item in currentMenuItems)
                    {
                        var tag = item.Tag?.ToString();
                        // Skip footer items - they should only be in footer, not main menu
                        if (tag == "OtherModsPage" || tag == "FunctionsPage" || tag == "PresetsPage" || tag == "SettingsPage")
                        {
                            continue;
                        }
                        if (item.Content?.ToString()?.ToLower().Contains(query) ?? false)
                        {
                            nvSample.MenuItems.Add(item);
                        }
                    }
                    
                    // Always add footer items
                    foreach (var item in currentFooterItems)
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "OtherModsPage" || tag == "FunctionsPage" || tag == "PresetsPage" || tag == "SettingsPage")
                        {
                            if (!nvSample.FooterMenuItems.Contains(item))
                                nvSample.FooterMenuItems.Add(item);
                        }
                    }
                });
            });

            // Dynamic mod filtering only if enabled in settings and query has at least 3 characters
            if (FlairX_Mod_Manager.SettingsManager.Current.DynamicModSearchEnabled)
            {
                if (!string.IsNullOrEmpty(query) && query.Length >= 3)
                {
                    // Always navigate to ModGridPage for search to ensure we search in all mods
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        // Load all mods first, then apply filter
                        modGridPage.LoadAllMods();
                        modGridPage.FilterMods(query);
                    }
                    else
                    {
                        // Navigate to ModGridPage for all mods search
                        contentFrame.Navigate(
                            typeof(FlairX_Mod_Manager.Pages.ModGridPage),
                            null,
                            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                        
                        // Apply filter after navigation and restore focus to SearchBox
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage newModGridPage)
                            {
                                newModGridPage.LoadAllMods();
                                newModGridPage.FilterMods(query);
                                // Restore focus to search box after navigation
                                RestoreSearchBoxFocus();
                            }
                        });
                    }
                }
                else if (string.IsNullOrEmpty(query))
                {
                    // Clear search - navigate appropriately based on view mode
                    if (IsCurrentlyInCategoryMode())
                    {
                        // In category mode, navigate to ModGridPage and load all categories
                        if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPageForCategories)
                        {
                            modGridPageForCategories.LoadAllCategories();
                        }
                        else
                        {
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage));
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage newModGridPageForCategories)
                                {
                                    newModGridPageForCategories.LoadAllCategories();
                                }
                            });
                        }
                    }
                    else if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        // In mods mode, just clear the filter
                        modGridPage.FilterMods(query);
                    }
                }
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = sender.Text.Trim().ToLower();
            
            // Static search requires at least 2 characters
            if (!string.IsNullOrEmpty(query) && query.Length >= 2)
            {
                // Always navigate to ModGridPage for search to ensure we search in all mods
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                {
                    // Load all mods first, then apply filter
                    modGridPage.LoadAllMods();
                    modGridPage.FilterMods(query);
                }
                else
                {
                    // Navigate to ModGridPage for all mods search
                    contentFrame.Navigate(
                        typeof(FlairX_Mod_Manager.Pages.ModGridPage),
                        null,
                        new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                    
                    // Apply filter after navigation and restore focus
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage newModGridPage)
                        {
                            newModGridPage.LoadAllMods();
                            newModGridPage.FilterMods(query);
                            // Restore focus to search box after navigation
                            RestoreSearchBoxFocus();
                        }
                    });
                }
            }
            else if (string.IsNullOrEmpty(query))
            {
                // Clear search - only if we're already on ModGridPage
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                {
                    modGridPage.FilterMods(query);
                }
            }
        }

        private void SearchBox_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Add logic here if needed
        }

        private void SearchBox_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Add logic here if needed
        }

        private void RestoreSearchBoxFocus()
        {
            // Restore focus to search box with a small delay to ensure navigation is complete
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (SearchBox != null)
                {
                    SearchBox.Focus(FocusState.Programmatic);
                }
            });
        }
    }
}