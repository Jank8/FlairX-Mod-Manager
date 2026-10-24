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
        
        // Comments
        private ObservableCollection<CommentViewModel> _comments = new();
        private bool _commentsLoaded = false;
        private bool _commentsHasMore = false;
        private bool _commentsLoadingMore = false;
        private Microsoft.UI.Xaml.Controls.WebView2? _commentsWebView = null;

        private async void ShowCommentsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentModDetails == null) return;

                // Toggle: if already loaded and visible, collapse
                if (_commentsLoaded && CommentsSection.Visibility == Visibility.Visible)
                {
                    CommentsSection.Visibility = Visibility.Collapsed;
                    ShowCommentsIcon.Glyph = "\uE8F2";
                    ShowCommentsButtonText.Text = SharedUtilities.GetTranslation(_lang, "ShowComments") ?? "Show Comments";
                    return;
                }

                // Show section
                CommentsSection.Visibility = Visibility.Visible;
                ShowCommentsIcon.Glyph = "\uE8F2";
                ShowCommentsButtonText.Text = SharedUtilities.GetTranslation(_lang, "ShowComments") ?? "Show Comments";

                // If already loaded, just show
                if (_commentsLoaded) return;

                // Load more is only meaningful after a first batch produced comments.
                CommentsLoadMoreButton.Visibility = Visibility.Collapsed;

                // Load comments (first page only — no Load More clicks). The login panel is
                // never opened automatically: the comments area explains the two possible
                // reasons for an empty list and offers a Login button instead.
                CommentsLoadingPanel.Visibility = Visibility.Visible;
                CommentsErrorText.Visibility = Visibility.Collapsed;
                CommentsLoginButton.Visibility = Visibility.Collapsed;
                CommentsLoadingText.Text = SharedUtilities.GetTranslation(_lang, "LoadingComments") ?? "Loading comments...";
                _comments.Clear();

                var hasSession = !string.IsNullOrEmpty(CloudflareBypassService.GetCachedCookies());
                var loginHint = SharedUtilities.GetTranslation(_lang, "Comments_LoginInfo");
                var loadFailed = SharedUtilities.GetTranslation(_lang, "CommentsLoadFailed") ?? "Failed to load comments.";
                var noComments = SharedUtilities.GetTranslation(_lang, "NoComments") ?? "No comments yet.";

                try
                {
                    var (comments, hasMore, webView) = await GameBananaService.GetModCommentsAsync(_currentModDetails.Id, loadAllPages: false);
                    _commentsWebView = webView;

                    CommentsLoadingPanel.Visibility = Visibility.Collapsed;

                    if (comments == null)
                    {
                        CommentsErrorText.Text = hasSession ? loadFailed : $"{loadFailed} {loginHint}";
                        CommentsErrorText.Visibility = Visibility.Visible;
                        CommentsLoginButton.Visibility = hasSession ? Visibility.Collapsed : Visibility.Visible;
                        return;
                    }

                    if (comments.Count == 0)
                    {
                        // With a session, an empty list simply means "no comments" - there is
                        // nothing to log in for.
                        CommentsErrorText.Text = hasSession ? noComments : $"{noComments} {loginHint}";
                        CommentsErrorText.Visibility = Visibility.Visible;
                        CommentsLoginButton.Visibility = hasSession ? Visibility.Collapsed : Visibility.Visible;
                        CommentsLoadMoreButton.Visibility = Visibility.Collapsed;
                        _commentsLoaded = true;
                        return;
                    }

                    foreach (var c in comments)
                    {
                        var vm = new CommentViewModel
                        {
                            AuthorName = c.AuthorName,
                            AvatarUrl = c.AvatarUrl,
                            Body = c.Body,
                            DatePosted = c.DatePosted
                        };
                        foreach (var r in c.Replies)
                        {
                            vm.Replies.Add(new CommentViewModel
                            {
                                AuthorName = r.AuthorName,
                                AvatarUrl = r.AvatarUrl,
                                Body = r.Body,
                                DatePosted = r.DatePosted,
                                IsReply = true
                            });
                        }
                        _comments.Add(vm);
                    }

                    _commentsHasMore = hasMore;
                    CommentsLoadMoreButton.Visibility = (hasMore && _comments.Count > 0)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    CommentsLoginButton.Visibility = Visibility.Collapsed;
                    _commentsLoaded = true;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load comments", ex);
                    CommentsLoadingPanel.Visibility = Visibility.Collapsed;
                    CommentsErrorText.Text = hasSession ? loadFailed : $"{loadFailed} {loginHint}";
                    CommentsErrorText.Visibility = Visibility.Visible;
                    CommentsLoginButton.Visibility = hasSession ? Visibility.Collapsed : Visibility.Visible;
                    CommentsLoadMoreButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.ShowCommentsButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.ShowCommentsButton_Click failed", ex);
            }
        }

        private async void CommentsLoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommentsLoginButton.IsEnabled = false;

                var loggedIn = await ShowLoginOverlayAsync();
                if (!loggedIn)
                {
                    return;
                }

                // Session changed - load the comments again through the normal path.
                _commentsLoaded = false;
                CommentsErrorText.Visibility = Visibility.Collapsed;
                CommentsLoginButton.Visibility = Visibility.Collapsed;
                ShowCommentsButton_Click(ShowCommentsButton, new RoutedEventArgs());
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.CommentsLoginButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.CommentsLoginButton_Click failed", ex);
            }
            finally
            {
                CommentsLoginButton.IsEnabled = true;
            }
        }

        private async void LoadMoreComments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentModDetails == null || _commentsLoadingMore) return;

                _commentsLoadingMore = true;
                CommentsLoadMoreButton.IsEnabled = false;
                CommentsLoadMoreButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                        {
                            new ProgressRing { IsActive = true, Width = 16, Height = 16 },
                            new TextBlock { Text = SharedUtilities.GetTranslation(_lang, "LoadingComments") ?? "Loading..." }
                        }
                };

                try
                {
                    List<GameBananaService.ModComment>? comments;
                    bool hasMore;

                    // Always re-fetch instead of reusing the previous WebView2.
                    //
                    // GameBananaService closes the hidden WebView2 in a finally block right after it
                    // publishes the result, so the instance handed back here is already closed and
                    // detached. Reusing it made "Load More" run a script against a dead control and
                    // then clear the list. A fresh fetch costs roughly a second but always works.
                    var result = await GameBananaService.GetModCommentsAsync(_currentModDetails.Id, loadAllPages: true);
                    comments = result.Comments;
                    hasMore = result.HasMore;
                    _commentsWebView = null;

                    if (comments != null)
                    {
                        _comments.Clear();
                        foreach (var c in comments)
                        {
                            var vm = new CommentViewModel
                            {
                                AuthorName = c.AuthorName,
                                AvatarUrl = c.AvatarUrl,
                                Body = c.Body,
                                DatePosted = c.DatePosted
                            };
                            foreach (var r in c.Replies)
                            {
                                vm.Replies.Add(new CommentViewModel
                                {
                                    AuthorName = r.AuthorName,
                                    AvatarUrl = r.AvatarUrl,
                                    Body = r.Body,
                                    DatePosted = r.DatePosted,
                                    IsReply = true
                                });
                            }
                            _comments.Add(vm);
                        }
                    }

                    _commentsHasMore = hasMore;
                    CommentsLoadMoreButton.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load more comments", ex);
                }
                finally
                {
                    _commentsLoadingMore = false;
                    CommentsLoadMoreButton.IsEnabled = true;
                    CommentsLoadMoreButton.Content = new TextBlock { Text = SharedUtilities.GetTranslation(_lang, "LoadMore") ?? "Load More" };
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.LoadMoreComments_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.LoadMoreComments_Click failed", ex);
            }
        }
    }
}
