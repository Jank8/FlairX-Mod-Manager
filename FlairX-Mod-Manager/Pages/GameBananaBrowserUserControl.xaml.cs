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
    public sealed partial class GameBananaBrowserUserControl : UserControl
    {
        private enum CategoryFilter
        {
            AllMods,
            CharacterSkins
        }
        private System.Collections.Generic.Dictionary<string, string> _lang = new();

        public class CommentViewModel
        {
            public string AuthorName { get; set; } = "";
            public string? AvatarUrl { get; set; }
            public string Body { get; set; } = "";
            public long DatePosted { get; set; }
            public bool IsReply { get; set; } = false;
            public ObservableCollection<CommentViewModel> Replies { get; set; } = new();
            public bool HasReplies => Replies.Count > 0;
            public Visibility RepliesVisibility => HasReplies ? Visibility.Visible : Visibility.Collapsed;

            private BitmapImage? _avatarSource;
            public BitmapImage? AvatarSource
            {
                get
                {
                    if (_avatarSource == null && !string.IsNullOrEmpty(AvatarUrl))
                    {
                        try { _avatarSource = new BitmapImage(new Uri(AvatarUrl)); } catch { }
                    }
                    return _avatarSource;
                }
            }

            public string DateFormatted
            {
                get
                {
                    if (DatePosted == 0) return "";
                    var dt = DateTimeOffset.FromUnixTimeSeconds(DatePosted).LocalDateTime;
                    var diff = DateTime.Now - dt;
                    if (diff.TotalDays >= 365) return $"{(int)(diff.TotalDays / 365)}y ago";
                    if (diff.TotalDays >= 30)  return $"{(int)(diff.TotalDays / 30)}mo ago";
                    if (diff.TotalDays >= 1)   return $"{(int)diff.TotalDays}d ago";
                    if (diff.TotalHours >= 1)  return $"{(int)diff.TotalHours}h ago";
                    if (diff.TotalMinutes >= 1) return $"{(int)diff.TotalMinutes}m ago";
                    return "just now";
                }
            }
        }
        
        // Markdown image resizing
        private bool _isAttachedToSizeChanged = false;

        public class ModViewModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public string AuthorProfileUrl { get; set; } = "";
            public string? AuthorAvatarUrl { get; set; }
            public string ProfileUrl { get; set; } = "";
            public string? ImageUrl { get; set; }
            public string CategoryName { get; set; } = "";
            public string CategoryUrl { get; set; } = "";
            public string? CategoryIconUrl { get; set; }
            public int DownloadCount { get; set; }
            public int LikeCount { get; set; }
            public int CommentCount { get; set; }
            public int ViewCount { get; set; }
            public long DateAdded { get; set; }
            public long DateModified { get; set; }
            public long DateUpdated { get; set; }
            public bool IsRated { get; set; } = false;
            public bool IsNSFW { get; set; } = false;
            
            private bool _isInstalled = false;
            public bool IsInstalled 
            { 
                get => _isInstalled;
                set
                {
                    if (_isInstalled != value)
                    {
                        _isInstalled = value;
                        OnPropertyChanged(nameof(IsInstalled));
                        OnPropertyChanged(nameof(IsInstalledVisibility));
                    }
                }
            }
            public Visibility IsInstalledVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
            public string InstalledText { get; set; } = "Installed";
            
            public string DownloadCountFormatted => FormatCount(DownloadCount);
            public string LikeCountFormatted => FormatCount(LikeCount);
            public string ViewCountFormatted => FormatCount(ViewCount);
            public string DateAddedFormatted => FormatDate(DateAdded);
            public string DateModifiedFormatted => FormatDate(DateModified);
            public string DateUpdatedFormatted => FormatDate(DateUpdated);
            public Visibility HasUpdate => (DateUpdated > 0 && DateUpdated != DateAdded) ? Visibility.Visible : Visibility.Collapsed;

            private BitmapImage? _imageSource;
            public BitmapImage? ImageSource
            {
                get => _imageSource;
                set
                {
                    if (_imageSource != value)
                    {
                        _imageSource = value;
                        OnPropertyChanged(nameof(ImageSource));
                    }
                }
            }

            private double _imageOpacity = 0;
            public double ImageOpacity
            {
                get => _imageOpacity;
                set
                {
                    if (_imageOpacity != value)
                    {
                        _imageOpacity = value;
                        OnPropertyChanged(nameof(ImageOpacity));
                    }
                }
            }

            private static string FormatDate(long timestamp)
            {
                if (timestamp == 0) return "";
                
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                // Return date in local format (e.g., "16.11.2024" or "11/16/2024" depending on locale)
                return date.ToShortDateString();
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Event for closing the panel - includes info if mod was installed
        public event EventHandler<BrowserClosedEventArgs>? CloseRequested;
        
        public class BrowserClosedEventArgs : EventArgs
        {
            public bool ModWasInstalled { get; set; }
        }

        public GameBananaBrowserUserControl(string gameTag, string? modUrl = null, string? sourceModPath = null)
        {
            InitializeComponent();

            // Reflect the stored GameBanana session as soon as the panel appears, so the header
            // does not claim "Login" while a valid session is already saved on disk.
            this.Loaded += (_, _) => _ = RefreshSessionWidgetAsync();
            CreateCharacterFilterComboBox();
            CreateRecentWindowComboBoxes();
            _gameTag = gameTag;
            _sourceModPath = sourceModPath;
            
            // Add event handlers
            this.Unloaded += GameBananaBrowserUserControl_Unloaded;
            
            // Load language
            _lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");
            
            // Set window title based on game
            var gameName = GetGameName(gameTag);
            var titleFormat = SharedUtilities.GetTranslation(_lang, "BrowseTitle");
            TitleText.Text = string.Format(titleFormat, gameName);

            // Set UI text
            SearchBox.PlaceholderText = SharedUtilities.GetTranslation(_lang, "SearchPlaceholder") ?? "Search mods or enter URL/ID...";
            DetailInstalledBadgeText.Text = SharedUtilities.GetTranslation(_lang, "Installed");
            DetailAuthorLabel.Text = SharedUtilities.GetTranslation(_lang, "Author");
            DetailViewProfileText.Text = SharedUtilities.GetTranslation(_lang, "ViewProfile");
            DetailViewAuthorModsText.Text = SharedUtilities.GetTranslation(_lang, "ViewAllAuthorMods");
            DetailCategoryLabel.Text = SharedUtilities.GetTranslation(_lang, "Category");
            DetailCategoryViewText.Text = SharedUtilities.GetTranslation(_lang, "ViewCategory");
            DetailPreviewHeader.Text = SharedUtilities.GetTranslation(_lang, "Preview");
            DetailDescriptionTitle.Text = SharedUtilities.GetTranslation(_lang, "Description");
            DetailFilesTitle.Text = SharedUtilities.GetTranslation(_lang, "Files");
            DetailOpenBrowserButtonText.Text = SharedUtilities.GetTranslation(_lang, "OpenInBrowser");
            DetailDownloadPreviewsButtonText.Text = SharedUtilities.GetTranslation(_lang, "DownloadPreviews");
            RetryButton.Content = SharedUtilities.GetTranslation(_lang, "Retry");
            StarterPackButtonText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Button") ?? "Starter Pack";
            ToolTipService.SetToolTip(StarterPackButton, SharedUtilities.GetTranslation(_lang, "StarterPack_Button_Tooltip") ?? "Download Starter Pack for this game");
            LoadMoreButtonText.Text = SharedUtilities.GetTranslation(_lang, "LoadMore");
            LoadMoreMainButtonText.Text = SharedUtilities.GetTranslation(_lang, "LoadMore");

            // Populate category filter ComboBox
            CategoryFilterComboBox.Items.Clear();
            CategoryFilterComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Filter_AllMods") ?? "All Mods");
            CategoryFilterComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Filter_CharacterSkins") ?? "Character Skins");
            CategoryFilterComboBox.SelectedIndex = 0;

            var parentCategoryId = GameBananaService.GetCharacterCategoryId(_gameTag);
            ApplyCharacterOptions(
                GameBananaCharacterFilter.BuildOptions(
                    Array.Empty<GameBananaService.CategoryRecord>(),
                    parentCategoryId,
                    GetTranslationOrDefault("Filter_AllCharacters", "All Characters")),
                parentCategoryId,
                false);

            // Populate sort order ComboBox (only visible for Character Skins filter)
            SortOrderComboBox.Items.Clear();
            SortOrderComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Sort_LatestUpdated") ?? "Latest Updated");
            SortOrderComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Sort_Newest") ?? "Newest");
            SortOrderComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Sort_Oldest") ?? "Oldest");
            SortOrderComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Sort_MostLiked") ?? "Most Liked");
            SortOrderComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Sort_MostViewed") ?? "Most Viewed");
            SortOrderComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Sort_MostDownloaded") ?? "Most Downloaded");
            SortOrderComboBox.Items.Add(SharedUtilities.GetTranslation(_lang, "Sort_MostCommented") ?? "Most Commented");
            SortOrderComboBox.SelectedIndex = 0;

            _isUpdatingRecentWindowControls = true;
            try
            {
                TimeRangeComboBox.Header = GetTranslationOrDefault("TimeRange_Header", "Time Range");
                TimeRangeComboBox.Items.Add(GetTranslationOrDefault("TimeRange_Last30Days", "Last 30 Days"));
                TimeRangeComboBox.Items.Add(GetTranslationOrDefault("TimeRange_Last90Days", "Last 90 Days"));
                TimeRangeComboBox.Items.Add(GetTranslationOrDefault("TimeRange_Last180Days", "Last 180 Days"));
                TimeRangeComboBox.Items.Add(GetTranslationOrDefault("TimeRange_Unlimited", "Unlimited (up to 500)"));
                TimeRangeComboBox.SelectedIndex = 1;

                SecondarySortComboBox.Header = GetTranslationOrDefault("WindowSort_Header", "Sort Within Range");
                SecondarySortComboBox.Items.Add(GetTranslationOrDefault("WindowSort_LatestUpdated", "Latest Updated"));
                SecondarySortComboBox.Items.Add(GetTranslationOrDefault("WindowSort_MostLiked", "Most Liked"));
                SecondarySortComboBox.Items.Add(GetTranslationOrDefault("WindowSort_MostDownloaded", "Most Downloaded"));
                SecondarySortComboBox.Items.Add(GetTranslationOrDefault("WindowSort_MostCommented", "Most Commented"));
                SecondarySortComboBox.SelectedIndex = 0;
            }
            finally
            {
                _isUpdatingRecentWindowControls = false;
            }
            UpdateRecentWindowAvailability();

            // Set loading / empty state text
            LoadingText.Text = SharedUtilities.GetTranslation(_lang, "Loading") ?? "Loading...";
            EmptyText.Text = SharedUtilities.GetTranslation(_lang, "NoModsFound") ?? "No mods found";
            CommentsLoadingText.Text = SharedUtilities.GetTranslation(_lang, "LoadingComments") ?? "Loading comments...";
            
            // Hide Starter Pack button if not available for this game
            StarterPackButton.Visibility = Dialogs.StarterPackDialog.IsStarterPackAvailable(_gameTag) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            
            ModsGridView.ItemsSource = _mods;
            CommentsList.ItemsSource = _comments;
            
            // Attach to the named ScrollViewer directly
            ModsScrollViewer.ViewChanged += ModsScrollViewer_ViewChanged;
            _modsScrollViewer = ModsScrollViewer;
            Logger.LogInfo("ScrollViewer attached directly from XAML");
            
            // If modUrl is provided, load mod details after control is loaded into visual tree
            if (!string.IsNullOrEmpty(modUrl))
            {
                _openedWithUrl = true;
                _pendingModUrl = modUrl;
                this.Loaded += GameBananaBrowserUserControl_Loaded;
            }
            else
            {
                // Load mods (sections will be extracted from loaded mods)
                _ = LoadModsAsync();
            }
        }

        private string GetGameName(string gameTag)
        {
            return gameTag switch
            {
                "ZZMI" => "Zenless Zone Zero",
                "GIMI" => "Genshin Impact",
                "HIMI" => "Honkai Impact 3rd",
                "WWMI" => "Wuthering Waves",
                "SRMI" => "Honkai Star Rail",
                "EFMI" => "Arknights: Endfield",
                _ => "Game"
            };
        }

        private async void StarterPackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if Starter Pack is available for this game
                if (!Dialogs.StarterPackDialog.IsStarterPackAvailable(_gameTag))
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "StarterPack_NotAvailable") ?? "Starter Pack is not available for this game yet.");
                    return;
                }

                // Show the Starter Pack dialog
                var starterPackDialog = new Dialogs.StarterPackDialog(_gameTag);
                starterPackDialog.XamlRoot = this.XamlRoot;
                
                // Subscribe to installation complete event to reload mods
                starterPackDialog.InstallationComplete += async (s, args) =>
                {
                    try
                    {
                        if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                        {
                            await mainWindow.ReloadModsAsync();
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("GameBananaBrowserUserControl.StarterPackButton_Click failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("GameBananaBrowserUserControl.StarterPackButton_Click failed", ex);
                    }
                };
                
                await starterPackDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show Starter Pack dialog", ex);
            }
        }

        private void ModsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ModViewModel mod)
            {
                // Show details panel
                _currentModIsNSFW = mod.IsRated;
                _ = ShowModDetailsAsync(mod.Id);
            }
        }

        private void ModTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModViewModel mod)
            {
                // Show details panel
                _currentModIsNSFW = mod.IsRated;
                _ = ShowModDetailsAsync(mod.Id);
            }
        }

        private void ModTile_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            // Context menu will show automatically via ContextFlyout
        }

        /// <summary>
        /// Check if a mod should be filtered out based on blacklist settings
        /// </summary>
        private bool IsModBlacklisted(string authorName, string modName)
        {
            // Check blacklisted authors
            if (!string.IsNullOrEmpty(authorName) && 
                SettingsManager.Current.GameBananaBlacklistedAuthors.Any(a => 
                    a.Equals(authorName, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogDebug($"Mod from blacklisted author: {authorName}");
                return true;
            }

            // Check blacklisted tags/keywords in name
            if (!string.IsNullOrEmpty(modName) && 
                SettingsManager.Current.GameBananaBlacklistedTags.Any(tag => 
                    modName.Contains(tag, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogDebug($"Mod with blacklisted keyword in name: {modName}");
                return true;
            }

            return false;
        }

        private async void BlacklistAuthor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModViewModel mod)
                {
                    var authorName = mod.AuthorName;
                    
                    if (string.IsNullOrWhiteSpace(authorName))
                    {
                        Logger.LogWarning("Cannot blacklist author: author name is empty");
                        return;
                    }

                    // Add to blacklist
                    if (!SettingsManager.Current.GameBananaBlacklistedAuthors.Contains(authorName))
                    {
                        SettingsManager.Current.GameBananaBlacklistedAuthors.Add(authorName);
                        SettingsManager.Save();
                        Logger.LogInfo($"Blacklisted author: {authorName}");

                        // Show confirmation
                        var langDict = SharedUtilities.LoadLanguageDictionary();
                        var message = SharedUtilities.GetTranslation(langDict, "GameBanana_AuthorBlacklisted");
                        if (string.IsNullOrEmpty(message))
                            message = $"Author '{authorName}' has been blacklisted and will no longer appear in search results.";
                        else
                            message = string.Format(message, authorName);

                        var dialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(langDict, "GameBanana_Blacklist_Title") ?? "Blacklist",
                            Content = message,
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();

                        // Reload mods to apply filter
                        await LoadModsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error blacklisting author", ex);
            }
        }

        private async void BlacklistModName_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModViewModel mod)
                {
                    var modName = mod.Name;
                    
                    if (string.IsNullOrWhiteSpace(modName))
                    {
                        Logger.LogWarning("Cannot blacklist mod: mod name is empty");
                        return;
                    }

                    // Add to blacklist (as a tag/keyword)
                    if (!SettingsManager.Current.GameBananaBlacklistedTags.Contains(modName))
                    {
                        SettingsManager.Current.GameBananaBlacklistedTags.Add(modName);
                        SettingsManager.Save();
                        Logger.LogInfo($"Blacklisted mod name: {modName}");

                        // Show confirmation
                        var langDict = SharedUtilities.LoadLanguageDictionary();
                        var message = SharedUtilities.GetTranslation(langDict, "GameBanana_ModNameBlacklisted");
                        if (string.IsNullOrEmpty(message))
                            message = $"Mods containing '{modName}' in their name will no longer appear in search results.";
                        else
                            message = string.Format(message, modName);

                        var dialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(langDict, "GameBanana_Blacklist_Title") ?? "Blacklist",
                            Content = message,
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();

                        // Reload mods to apply filter
                        await LoadModsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error blacklisting mod name", ex);
            }
        }

        // Author avatar hover effect
        private void AuthorAvatar_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private void AuthorAvatar_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        // Category icon hover effect
        private void CategoryIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailCategoryIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailCategoryIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private void CategoryIcon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailCategoryIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailCategoryIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private void DetailDescriptionScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMarkdownImages();
        }

        // Tile hover effects - scale only the image
        private void ModTile_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    // Find the Image element (it's in Grid.Row="0" Grid.RowSpan="2")
                    var image = FindVisualChild<Image>(button);
                    if (image == null) return;
                    
                    // Create scale transform if it doesn't exist
                    if (image.RenderTransform is not ScaleTransform)
                    {
                        image.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                        image.RenderTransform = new ScaleTransform();
                    }
                    
                    var scaleTransform = (ScaleTransform)image.RenderTransform;
                    
                    // Animate scale to 1.10 (10% larger)
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.10,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                    storyboard.Children.Add(scaleXAnim);
                    
                    var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.10,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
                    storyboard.Children.Add(scaleYAnim);
                    
                    storyboard.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerEntered", ex);
                }
            }
        }

        private void ModTile_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    var image = FindVisualChild<Image>(button);
                    if (image?.RenderTransform is not ScaleTransform scaleTransform) return;
                    
                    // Animate scale back to 1.0
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                    storyboard.Children.Add(scaleXAnim);
                    
                    var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
                    storyboard.Children.Add(scaleYAnim);
                    
                    storyboard.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerExited", ex);
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void GameBananaBrowserUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from pointer events
            if (DetailImageCoordinateField != null)
            {
                DetailImageCoordinateField.PointerMoved -= DetailImageCoordinateField_PointerMoved_Parallax;
                DetailImageCoordinateField.PointerExited -= DetailImageCoordinateField_PointerExited_Parallax;
                DetailImageCoordinateField.PointerMoved -= DetailImageCoordinateField_PointerMoved_Glassmorphism;
                DetailImageCoordinateField.PointerExited -= DetailImageCoordinateField_PointerExited_Glassmorphism;
            }
        }
    }
}

