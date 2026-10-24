using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameBananaBrowserUserControl
    {
        private GameBananaService.ModDetailsResponse? _currentModDetails;
        private ObservableCollection<Models.GameBananaFileViewModel> _detailFiles = new();
        private bool _currentModIsNSFW = false;
        // Source mod path - when opened from mod library, this is the path of the mod we're checking for updates
        private string? _sourceModPath = null;
        
        // Pending mod URL to load after control is added to visual tree
        internal string? _pendingModUrl = null;
        
        // Track if any mod was installed during this session (internal for MainWindow access)
        internal bool _modWasInstalled = false;
        
        // When opened via a mod URL (e.g. from mod detail panel), the mod page is the root of the nav stack.
        // Back from the root should close the panel, not load the homepage.
        private bool _openedWithUrl = false;

        private bool IsModInstalled(string profileUrl)
        {
            return !string.IsNullOrEmpty(GetInstalledModPath(profileUrl));
        }

        private async Task<string?> GetInstalledModPathAsync(string profileUrl)
        {
            return await Task.Run(() => GetInstalledModPath(profileUrl));
        }

        private string? GetInstalledModPath(string profileUrl)
        {
            try
            {
                var modLibraryPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !System.IO.Directory.Exists(modLibraryPath))
                    return null;

                // Extract mod ID from profile URL
                var profileModId = GameBananaService.ExtractModIdFromUrl(profileUrl);
                if (profileModId == null)
                    return null;

                // Search all mod.json files in mod library
                foreach (var categoryDir in System.IO.Directory.GetDirectories(modLibraryPath))
                {
                    foreach (var modDir in System.IO.Directory.GetDirectories(categoryDir))
                    {
                        var modJsonPath = System.IO.Path.Combine(modDir, "mod.json");
                        if (System.IO.File.Exists(modJsonPath))
                        {
                            try
                            {
                                // Use direct File.ReadAllText — this runs in Task.Run (background thread),
                                // bypassing FileAccessQueue to avoid deadlocks with concurrent UI operations
                                var jsonContent = System.IO.File.ReadAllText(modJsonPath);
                                var modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(jsonContent);
                                
                                if (modData != null && modData.TryGetValue("url", out var urlElement))
                                {
                                    var url = urlElement.GetString();
                                    if (!string.IsNullOrEmpty(url) && url != "https://")
                                    {
                                        // Extract mod ID from stored URL and compare
                                        var storedModId = GameBananaService.ExtractModIdFromUrl(url);
                                        if (storedModId != null && storedModId == profileModId)
                                        {
                                            return modDir;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Skip invalid mod.json files
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking if mod is installed", ex);
            }

            return null;
        }
        
        private async Task UpdateDateCheckedForMod(string profileUrl)
        {
            try
            {
                var modLibraryPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !System.IO.Directory.Exists(modLibraryPath))
                    return;

                // Search for the mod by URL
                foreach (var categoryDir in System.IO.Directory.GetDirectories(modLibraryPath))
                {
                    foreach (var modDir in System.IO.Directory.GetDirectories(categoryDir))
                    {
                        var modJsonPath = System.IO.Path.Combine(modDir, "mod.json");
                        if (System.IO.File.Exists(modJsonPath))
                        {
                            try
                            {
                                // First check if this is the right mod (read-only check)
                                var jsonContent = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath);
                                var checkData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                                
                                if (checkData != null && checkData.TryGetValue("url", out var urlObj))
                                {
                                    var url = urlObj?.ToString();
                                    if (!string.IsNullOrEmpty(url) && url.Equals(profileUrl, StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Found the mod - do atomic read-modify-write
                                        await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                                        {
                                            var currentJson = await System.IO.File.ReadAllTextAsync(modJsonPath);
                                            var modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson);
                                            if (modData != null)
                                            {
                                                modData["dateChecked"] = DateTime.Now.ToString("yyyy-MM-dd");
                                                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                                                var updatedJson = System.Text.Json.JsonSerializer.Serialize(modData, jsonOptions);
                                                await System.IO.File.WriteAllTextAsync(modJsonPath, updatedJson);
                                            }
                                        });
                                        
                                        Logger.LogInfo($"Updated dateChecked for mod at {modJsonPath}");
                                        return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error updating dateChecked for mod at {modJsonPath}", ex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating dateChecked", ex);
            }
        }

        private async Task ShowModDetailsAsync(int modId)
        {
            Logger.LogInfo($"[GBBrowser] ShowModDetailsAsync start, modId={modId}, currentState={_currentState}");
            try
            {
                // Push current state to navigation stack before navigating (unless suppressed)
                if (!_suppressNavigationPush)
                {
                    if (_currentState == NavigationState.ModsList)
                    {
                        _navigationStack.Push(new NavigationEntry(
                            NavigationState.ModsList,
                            Search: _currentSearch,
                            ScrollOffset: _modsScrollViewer?.VerticalOffset ?? 0,
                            Page: _currentPage,
                            Filter: _currentCategoryFilter,
                            SortOrder: _currentSortOrder,
                            CharacterCategoryId: _selectedCharacterCategoryId,
                            TimeRange: _currentTimeRange,
                            SecondarySort: _currentSecondarySort));
                    }
                    else if (_currentState == NavigationState.ModDetails && _currentModDetails != null)
                    {
                        _navigationStack.Push(new NavigationEntry(
                            NavigationState.ModDetails,
                            ModId: _currentModDetails.Id));
                    }
                    else if (_currentState == NavigationState.AuthorMods && _currentAuthorId.HasValue)
                    {
                        _navigationStack.Push(new NavigationEntry(
                            NavigationState.AuthorMods,
                            AuthorId: _currentAuthorId,
                            AuthorName: _currentAuthorName,
                            ScrollOffset: _modsScrollViewer?.VerticalOffset ?? 0));
                    }
                }

                // Clear source mod path when navigating to different mod
                // This prevents dialog from thinking we're updating the original mod
                if (_currentModDetails == null || _currentModDetails.Id != modId)
                {
                    _sourceModPath = null;
                    Logger.LogInfo($"Cleared _sourceModPath when navigating to different mod (ID: {modId})");
                }
                
                // Show loading state in details panel first
                DetailLoadingPanel.Visibility = Visibility.Visible;
                DetailAuthor.Visibility = Visibility.Collapsed;
                DetailImage.Visibility = Visibility.Collapsed;
                DetailDescriptionTitle.Visibility = Visibility.Collapsed;
                DetailFilesTitle.Visibility = Visibility.Collapsed;
                DetailFilesList.Visibility = Visibility.Collapsed;
                DetailDownloadButton.Visibility = Visibility.Collapsed;
                DetailDownloadPreviewsButton.Visibility = Visibility.Collapsed;
                DetailOpenBrowserButton.Visibility = Visibility.Collapsed;
                
                // Animate transition to details only if not already showing details
                if (_currentState != NavigationState.ModDetails)
                {
                    AnimateContentSwitch(ModsListGrid, DetailsPanel);
                }
                _currentState = NavigationState.ModDetails;
                
                // Scroll details to top
                DetailsScrollViewer.ScrollToVerticalOffset(0);
                
                // Update back button icon
                UpdateBackButtonIcon();

                // Load mod details
                _currentModDetails = await GameBananaService.GetModDetailsAsync(modId);

                if (_currentModDetails == null)
                {
                    // API error - go back without clearing navigation history
                    CloseDetailsPanelNoHistory();
                    UpdateBackButtonIcon();
                    ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                    ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                    ConnectionErrorBar.IsOpen = true;
                    return;
                }
                
                // Check if mod is private or unavailable (API returns data but with null fields)
                if (!_currentModDetails.IsAvailable)
                {
                    // Mod is private or has been removed - go back without clearing history
                    CloseDetailsPanelNoHistory();
                    UpdateBackButtonIcon();
                    ModUnavailableBar.Title = SharedUtilities.GetTranslation(_lang, "ModUnavailableTitle") ?? "Mod Unavailable";
                    ModUnavailableBar.Message = SharedUtilities.GetTranslation(_lang, "ModUnavailableMessage") ?? "This mod is private or has been removed.";
                    ModUnavailableBar.IsOpen = true;
                    return;
                }

                // NSFW state decides whether comments require a login. Prefer the list's
                // IsRated flag (reliable) and fall back to the detail API flags for mods
                // opened directly by URL.
                var listMod = _mods.FirstOrDefault(m => m.Id == modId)
                              ?? _authorMods.FirstOrDefault(m => m.Id == modId);
                _currentModIsNSFW = listMod?.IsRated == true || _currentModDetails.IsNSFW();
                
                // Check if mod is installed and update UI
                bool isInstalled = false;
                if (!string.IsNullOrEmpty(_currentModDetails.ProfileUrl))
                {
                    Logger.LogInfo($"[ShowModDetails] Checking if mod is installed (scanning mod library)...");
                    isInstalled = !string.IsNullOrEmpty(await GetInstalledModPathAsync(_currentModDetails.ProfileUrl));
                    Logger.LogInfo($"[ShowModDetails] Mod installed: {isInstalled}");
                    
                    // Update dateChecked in mod.json if this mod is installed
                    if (isInstalled)
                    {
                        _ = UpdateDateCheckedForMod(_currentModDetails.ProfileUrl);
                    }
                }
                
                // Show/hide installed badge
                DetailInstalledBadge.Visibility = isInstalled ? Visibility.Visible : Visibility.Collapsed;
                
                // Update download button text based on installation status
                DetailDownloadButtonText.Text = isInstalled 
                    ? SharedUtilities.GetTranslation(_lang, "DownloadAndUpdate")
                    : SharedUtilities.GetTranslation(_lang, "DownloadAndInstall");

                // Update UI
                TitleText.Text = _currentModDetails.Name;
                
                // Show version if available
                if (!string.IsNullOrEmpty(_currentModDetails.Version))
                {
                    DetailVersionText.Text = _currentModDetails.Version;
                    DetailVersionBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailVersionBadge.Visibility = Visibility.Collapsed;
                }
                
                DetailAuthor.Text = _currentModDetails.Submitter?.Name ?? "Unknown";
                
                // Load description in MarkdownTextBlock
                LoadDescriptionInMarkdown(_currentModDetails.Description);
                Logger.LogInfo($"[ShowModDetails] Description loaded, loading avatar...");
                
                // Load author avatar
                if (!string.IsNullOrEmpty(_currentModDetails.Submitter?.AvatarUrl))
                {
                    try
                    {
                        DetailAuthorAvatar.Source = new BitmapImage(new Uri(_currentModDetails.Submitter.AvatarUrl));
                    }
                    catch
                    {
                        DetailAuthorAvatar.Source = null;
                    }
                }
                else
                {
                    DetailAuthorAvatar.Source = null;
                }
                
                Logger.LogInfo($"[ShowModDetails] Avatar done, loading category...");
                
                // Set profile link
                DetailAuthorProfileLink.Tag = _currentModDetails.Submitter?.ProfileUrl;
                
                // Load category info
                var category = _currentModDetails.Category;
                if (category != null)
                {
                    DetailCategory.Text = !string.IsNullOrWhiteSpace(category.Name)
                        ? category.Name
                        : _currentModDetails.RootCategory?.Name ?? "Unknown";

                    // The API sometimes returns a relative icon path.
                    var categoryIconUrl = category.IconUrl;
                    if (!string.IsNullOrEmpty(categoryIconUrl) && categoryIconUrl.StartsWith("/", StringComparison.Ordinal))
                        categoryIconUrl = "https://gamebanana.com" + categoryIconUrl;

                    if (!string.IsNullOrEmpty(categoryIconUrl))
                    {
                        try
                        {
                            DetailCategoryIcon.Source = new BitmapImage(new Uri(categoryIconUrl));
                        }
                        catch
                        {
                            DetailCategoryIcon.Source = null;
                        }
                    }
                    else
                    {
                        DetailCategoryIcon.Source = null;
                    }

                    Logger.LogInfo(
                        $"[ShowModDetails] Category: name='{category.Name}', id={category.Id}, " +
                        $"url='{category.ProfileUrl}', icon='{category.IconUrl}', " +
                        $"root='{_currentModDetails.RootCategory?.Name}'");
                }
                else
                {
                    DetailCategory.Text = _currentModDetails.RootCategory?.Name ?? "Unknown";
                    DetailCategoryIcon.Source = null;
                    Logger.LogWarning("[ShowModDetails] Category is missing from the API response");
                }

                // Load preview images into slider
                _detailPreviewImages.Clear();
                _currentDetailImageIndex = 0;
                Logger.LogInfo($"[ShowModDetails] Loading preview images...");
                
                if (_currentModDetails.PreviewMedia?.Images != null && _currentModDetails.PreviewMedia.Images.Count > 0)
                {
                    foreach (var image in _currentModDetails.PreviewMedia.Images)
                    {
                        var imageUrl = $"{image.BaseUrl}/{image.File530 ?? image.File}";
                        _detailPreviewImages.Add(imageUrl);
                    }
                    
                    LoadCurrentDetailImage();
                    UpdateDetailImageNavigation();
                    Logger.LogInfo($"[ShowModDetails] Preview images queued: {_detailPreviewImages.Count}");
                }

                Logger.LogInfo($"[ShowModDetails] Preview images done, loading files...");
                // Load files
                _detailFiles.Clear();
                if (_currentModDetails.Files != null && _currentModDetails.Files.Count > 0)
                {
                    var sizeLabel = SharedUtilities.GetTranslation(_lang, "Size");
                    var downloadsLabel = SharedUtilities.GetTranslation(_lang, "Downloads");
                    var addedLabel = SharedUtilities.GetTranslation(_lang, "Added");
                    
                    foreach (var file in _currentModDetails.Files)
                    {
                        _detailFiles.Add(new Models.GameBananaFileViewModel
                        {
                            Id = file.Id,
                            FileName = file.FileName,
                            FileSize = file.FileSize,
                            Description = file.Description,
                            DownloadUrl = file.DownloadUrl,
                            DownloadCount = file.DownloadCount,
                            DateAdded = file.DateAdded,
                            SizeLabel = sizeLabel,
                            DownloadsLabel = downloadsLabel,
                            AddedLabel = addedLabel
                        });
                    }

                    DetailFilesList.ItemsSource = _detailFiles;
                    
                    // Files are unselected by default
                    foreach (var file in _detailFiles)
                    {
                        file.IsSelected = false;
                    }
                    
                    DetailDownloadButton.IsEnabled = true;
                }
                else
                {
                    DetailDownloadButton.IsEnabled = false;
                    DetailDownloadButton.Content = SharedUtilities.GetTranslation(_lang, "NoFilesAvailable");
                }

                // Show content (WebView/TextBlock visibility is set in LoadDescriptionInWebView)
                Logger.LogInfo($"[ShowModDetails] Setting visibility of UI elements...");
                DetailLoadingPanel.Visibility = Visibility.Collapsed;
                DetailAuthor.Visibility = Visibility.Visible;
                DetailImage.Visibility = Visibility.Visible;
                DetailDescriptionTitle.Visibility = Visibility.Visible;
                DetailFilesTitle.Visibility = Visibility.Visible;
                DetailFilesList.Visibility = Visibility.Visible;
                DetailDownloadButton.Visibility = Visibility.Visible;
                
                // Show Download Previews button always, but enable only if mod is installed and has preview images
                bool hasPreviewImages = _currentModDetails.PreviewMedia?.Images != null && 
                                       _currentModDetails.PreviewMedia.Images.Any(img => img.Type == "screenshot");
                DetailDownloadPreviewsButton.Visibility = hasPreviewImages ? Visibility.Visible : Visibility.Collapsed;
                DetailDownloadPreviewsButton.IsEnabled = isInstalled && hasPreviewImages;
                
                DetailOpenBrowserButton.Visibility = Visibility.Visible;
                
                // Reset comments section for new mod
                _commentsLoaded = false;
                _commentsHasMore = false;
                _commentsLoadingMore = false;
                _comments.Clear();
                try { _commentsWebView?.Close(); } catch { }
                _commentsWebView = null;
                CommentsSection.Visibility = Visibility.Collapsed;
                CommentsLoadingPanel.Visibility = Visibility.Collapsed;
                CommentsErrorText.Visibility = Visibility.Collapsed;
                CommentsLoginButton.Content = SharedUtilities.GetTranslation(_lang, "Comments_Login") ?? "Login";
                CommentsLoginButton.Visibility = Visibility.Collapsed;
                CommentsLoadMoreButton.Content = new TextBlock
                {
                    Text = SharedUtilities.GetTranslation(_lang, "LoadMore") ?? "Load More"
                };
                CommentsLoadMoreButton.Visibility = Visibility.Collapsed;
                ShowCommentsButton.Visibility = Visibility.Visible;
                ShowCommentsButtonText.Text = SharedUtilities.GetTranslation(_lang, "ShowComments") ?? "Show Comments";
                ShowCommentsIcon.Glyph = "\uE8F2";
                
                // Ensure size changed event is attached for markdown images
                if (!_isAttachedToSizeChanged)
                {
                    DetailDescriptionScrollViewer.SizeChanged += DetailDescriptionScrollViewer_SizeChanged;
                    _isAttachedToSizeChanged = true;
                }
                
                // Attach to the markdown text block layout updated event (remove first to avoid duplicates)
                DetailDescriptionMarkdown.LayoutUpdated -= DetailDescriptionMarkdown_LayoutUpdated;
                DetailDescriptionMarkdown.LayoutUpdated += DetailDescriptionMarkdown_LayoutUpdated;
                Logger.LogInfo($"[ShowModDetails] ShowModDetailsAsync COMPLETE for modId={modId}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mod details", ex);
                CloseDetailsPanelNoHistory();
                UpdateBackButtonIcon();
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }

        private async void DetailOpenBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentModDetails != null && !string.IsNullOrEmpty(_currentModDetails.ProfileUrl))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(_currentModDetails.ProfileUrl));
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.DetailOpenBrowserButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.DetailOpenBrowserButton_Click failed", ex);
            }
        }

        private async Task OpenAuthorProfileAsync(string? profileUrl)
        {
            if (string.IsNullOrWhiteSpace(profileUrl))
            {
                return;
            }

            // Shared mini-browser: the GameBanana session (cookies, Cloudflare clearance and
            // User-Agent) is already present there.
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                var authorName = _currentModDetails?.Submitter?.Name;
                await mainWindow.ShowBrowserPanelAsync(
                    profileUrl,
                    string.IsNullOrWhiteSpace(authorName) ? "GameBanana" : authorName);
                return;
            }

            await Windows.System.Launcher.LaunchUriAsync(new Uri(profileUrl));
        }

        private async void DetailAuthorProfileLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var profileUrl = (sender as HyperlinkButton)?.Tag as string;
                profileUrl ??= _currentModDetails?.Submitter?.ProfileUrl;
                await OpenAuthorProfileAsync(profileUrl);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.DetailAuthorProfileLink_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.DetailAuthorProfileLink_Click failed", ex);
            }
        }

        // The avatar and the author name are also a shortcut to the same profile.
        private async void AuthorProfile_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                e.Handled = true;
                await OpenAuthorProfileAsync(_currentModDetails?.Submitter?.ProfileUrl);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.AuthorProfile_Tapped failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.AuthorProfile_Tapped failed", ex);
            }
        }

        private async void DetailAuthorModsLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentModDetails?.Submitter != null)
                {
                    _currentAuthorId = _currentModDetails.Submitter.Id;
                    _currentAuthorName = _currentModDetails.Submitter.Name;

                    // Navigate to author mods view
                    await ShowAuthorModsAsync();
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.DetailAuthorModsLink_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.DetailAuthorModsLink_Click failed", ex);
            }
        }

        internal async Task LoadModDetailsFromUrlAsync(string modUrl)
        {
            Logger.LogInfo($"[GBBrowser] LoadModDetailsFromUrlAsync called, url={modUrl}, currentState={_currentState}");
            try
            {
                var urlPattern = new System.Text.RegularExpressions.Regex(@"gamebanana\.com/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var match = urlPattern.Match(modUrl);
                
                if (match.Success && int.TryParse(match.Groups[1].Value, out int modId))
                {
                    Logger.LogInfo($"Loading mod details from URL: {modUrl}, ID: {modId}");
                    // Push current state only now that URL is confirmed valid.
                    // Skip pushing when opened via URL from constructor — the mod page IS the root,
                    // so back should close the panel rather than navigate to the homepage.
                    if (!_openedWithUrl)
                    {
                        if (_currentState == NavigationState.ModsList)
                        {
                            _navigationStack.Push(new NavigationEntry(
                                NavigationState.ModsList,
                                Search: _currentSearch,
                                ScrollOffset: _modsScrollViewer?.VerticalOffset ?? 0,
                                Page: _currentPage,
                                Filter: _currentCategoryFilter,
                                SortOrder: _currentSortOrder,
                                CharacterCategoryId: _selectedCharacterCategoryId,
                                TimeRange: _currentTimeRange,
                                SecondarySort: _currentSecondarySort));
                        }
                        else if (_currentState == NavigationState.ModDetails && _currentModDetails != null)
                        {
                            _navigationStack.Push(new NavigationEntry(
                                NavigationState.ModDetails,
                                ModId: _currentModDetails.Id));
                        }
                        else if (_currentState == NavigationState.AuthorMods && _currentAuthorId.HasValue)
                        {
                            _navigationStack.Push(new NavigationEntry(
                                NavigationState.AuthorMods,
                                AuthorId: _currentAuthorId,
                                AuthorName: _currentAuthorName,
                                ScrollOffset: _modsScrollViewer?.VerticalOffset ?? 0));
                        }
                    }
                    // After the initial URL load, subsequent navigations (e.g. author mods) should push normally
                    _openedWithUrl = false;
                    await ShowModDetailsAsyncNoHistory(modId);
                    UpdateBackButtonIcon();
                }
                else
                {
                    Logger.LogWarning($"Could not parse mod ID from URL: {modUrl}");
                    await LoadModsAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load mod details from URL: {modUrl}", ex);
                await LoadModsAsync();
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }

        private async void DetailCategoryLink_Click(object sender, RoutedEventArgs e)
        {
            var details = _currentModDetails;
            var category = details?.Category;
            if (details == null || category == null)
            {
                Logger.LogWarning("Category link clicked, but the mod has no category data");
                return;
            }

            var categoryId = category.Id;
            var categoryName = category.Name ?? string.Empty;

            try
            {
                Logger.LogInfo($"[CategoryLink] Opening in-app character skins for '{categoryName}' (id={categoryId})");

                // Leave the details view first - it clears _currentModDetails.
                CloseDetailsPanelNoHistory();

                // Reuse the existing "Character skins" filter instead of opening a browser.
                // Suppress the combo handlers so they do not start a second, unfiltered load.
                _suppressCategoryFilterEvents = true;
                try
                {
                    _currentCategoryFilter = CategoryFilter.CharacterSkins;
                    CategoryFilterComboBox.SelectedIndex = 1;
                    CharacterFilterComboBox.Visibility = Visibility.Visible;
                    SortOrderComboBox.Visibility = Visibility.Visible;
                    SortOrderComboBox.SelectedIndex = 0;
                }
                finally
                {
                    _suppressCategoryFilterEvents = false;
                }

                _currentSortOrder = GameBananaService.CategorySortOrder.LatestUpdated;
                _currentSearch = null;
                SearchBox.Text = string.Empty;
                _currentPage = 1;
                UpdateRecentWindowAvailability();

                // Rebuilds the character dropdown and preselects the clicked category when
                // it is one of the character skins; otherwise it falls back to "all".
                await LoadCharacterOptionsAsync(categoryId > 0 ? categoryId : null);

                // The nested _aCategory object does not always expose _idRow. Resolve the
                // option by its display name as a second chance before loading the list.
                if (categoryId <= 0 && !string.IsNullOrWhiteSpace(categoryName))
                {
                    var byName = _characterOptions.FirstOrDefault(option =>
                        string.Equals(option.DisplayName, categoryName, StringComparison.CurrentCultureIgnoreCase));
                    if (byName != null)
                        categoryId = byName.CategoryId;
                }

                if (categoryId > 0 && _characterOptions.Any(option => option.CategoryId == categoryId))
                {
                    _isUpdatingCharacterSelection = true;
                    try
                    {
                        _selectedCharacterCategoryId = categoryId;
                        var match = CharacterFilterComboBox.Items
                            .OfType<ComboBoxItem>()
                            .FirstOrDefault(item =>
                                item.Tag is GameBananaCharacterOption option &&
                                option.CategoryId == categoryId);
                        if (match != null)
                            CharacterFilterComboBox.SelectedItem = match;
                    }
                    finally
                    {
                        _isUpdatingCharacterSelection = false;
                    }
                }

                await LoadModsAsync();

                var gameName = GetGameName(_gameTag);
                var titleFormat = SharedUtilities.GetTranslation(_lang, "BrowseTitle");
                TitleText.Text = string.IsNullOrWhiteSpace(categoryName)
                    ? string.Format(titleFormat, gameName)
                    : $"{string.Format(titleFormat, gameName)} - {categoryName}";

                Logger.LogInfo($"[CategoryLink] Loaded character skins for '{categoryName}' (id={categoryId})");
            }
            catch (Exception ex)
            {
                Logger.LogError("[CategoryLink] Failed to open the category in the built-in browser", ex);
            }
        }
        
        /// <summary>
        /// Refreshes the installation status of all mods in the browser
        /// </summary>
        public void RefreshModInstallationStatus()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Update main mods collection
                foreach (var mod in _mods)
                {
                    var wasInstalled = mod.IsInstalled;
                    var isNowInstalled = IsModInstalled(mod.ProfileUrl);
                    if (wasInstalled != isNowInstalled)
                    {
                        mod.IsInstalled = isNowInstalled;
                        Logger.LogInfo($"Updated installation status for mod: {mod.Name} - Installed: {isNowInstalled}");
                    }
                }
                
                // Update author mods collection
                foreach (var mod in _authorMods)
                {
                    var wasInstalled = mod.IsInstalled;
                    var isNowInstalled = IsModInstalled(mod.ProfileUrl);
                    if (wasInstalled != isNowInstalled)
                    {
                        mod.IsInstalled = isNowInstalled;
                        Logger.LogInfo($"Updated installation status for author mod: {mod.Name} - Installed: {isNowInstalled}");
                    }
                }
                
                // Update details panel if currently showing a mod
                if (_currentModDetails != null && !string.IsNullOrEmpty(_currentModDetails.ProfileUrl))
                {
                    var isInstalled = IsModInstalled(_currentModDetails.ProfileUrl);
                    DetailInstalledBadge.Visibility = isInstalled ? Visibility.Visible : Visibility.Collapsed;
                    
                    // Update download button text
                    var lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");
                    DetailDownloadButtonText.Text = isInstalled 
                        ? SharedUtilities.GetTranslation(lang, "DownloadAndUpdate")
                        : SharedUtilities.GetTranslation(lang, "DownloadAndInstall");
                }
            });
        }
    }
}

