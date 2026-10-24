using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using FlairX_Mod_Manager;

namespace FlairX_Mod_Manager.Services
{
    public partial class GameBananaService
    {

        /// <summary>
        /// Render a page using WebView2 and return the HTML after JavaScript execution
        /// </summary>
        private static async Task<string?> RenderPageWithWebView2Async(string url)
        {
            Microsoft.UI.Xaml.Controls.WebView2? webView = null;
            Microsoft.UI.Xaml.Controls.Grid? hiddenContainer = null;
            MainWindow? mainWindow = null;
            Windows.Foundation.TypedEventHandler<
                Microsoft.Web.WebView2.Core.CoreWebView2,
                Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs>? navigationHandler = null;

            try
            {
                Logger.LogInfo($"Initializing WebView2 for URL: {url}");

                // Check if WebView2 runtime is available
                try
                {
                    var version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
                    Logger.LogInfo($"WebView2 runtime available: {version}");
                }
                catch
                {
                    Logger.LogError("WebView2 runtime not installed");
                    var wv2App = App.Current as App;
                    var wv2Window = wv2App?.MainWindow as MainWindow;
                    if (wv2Window != null)
                    {
                        var runtimePrompt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        if (!wv2Window.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                try
                                {
                                    await FlairX_Mod_Manager.DispatcherQueueExtensions.EnsureWebView2Available(wv2Window.Content.XamlRoot);
                                }
                                finally
                                {
                                    runtimePrompt.TrySetResult(true);
                                }
                            }
                            catch (System.OperationCanceledException)
                            {
                                Logger.LogDebug("GameBananaService.RenderPageWithWebView2Async failed: canceled");
                            }
                            catch (System.Exception ex)
                            {
                                Logger.LogError("GameBananaService.RenderPageWithWebView2Async failed", ex);
                            }
                        }))
                        {
                            Logger.LogError("Could not enqueue the WebView2 runtime prompt");
                            return null;
                        }

                        await runtimePrompt.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    }
                    return null;
                }

                var app = App.Current as App;
                mainWindow = app?.MainWindow as MainWindow;

                if (mainWindow == null)
                {
                    Logger.LogError("Could not get main window for WebView2");
                    return null;
                }

                var initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var navigationCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (!mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        webView = new Microsoft.UI.Xaml.Controls.WebView2();
                        hiddenContainer = mainWindow.GetHiddenWebViewContainer();
                        hiddenContainer.Children.Add(webView);

                        await webView.EnsureCoreWebView2Async();
                        var hiddenUserAgent = CloudflareBypassService.GetCachedUserAgent();
                        if (!string.IsNullOrEmpty(hiddenUserAgent))
                        {
                            webView.CoreWebView2.Settings.UserAgent = hiddenUserAgent;
                        }
                        webView.CoreWebView2.NavigationStarting += (_, args) =>
                        {
                            if (!WebViewNavigationPolicy.IsAllowed(args.Uri))
                            {
                                args.Cancel = true;
                                Logger.LogWarning($"Blocked hidden WebView navigation outside GameBanana: {args.Uri}");
                            }
                        };
                        Logger.LogInfo("WebView2 initialized in hidden container, navigating to URL");

                        navigationHandler = async (sender, args) =>
                        {
                            if (args.IsSuccess)
                            {
                                Logger.LogInfo("WebView2 navigation completed successfully");
                                await CloudflareBypassService.CaptureFromWebViewAsync(webView.CoreWebView2);
                                navigationCompleted.TrySetResult(true);
                            }
                            else
                            {
                                Logger.LogError($"WebView2 navigation failed: {args.WebErrorStatus}");
                                navigationCompleted.TrySetResult(false);
                            }
                        };
                        webView.CoreWebView2.NavigationCompleted += navigationHandler;

                        webView.CoreWebView2.Navigate(url);
                        initialized.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error initializing WebView2: {ex.Message}", ex);
                        initialized.TrySetException(ex);
                        navigationCompleted.TrySetResult(false);
                    }
                }))
                {
                    Logger.LogError("Could not enqueue WebView2 initialization");
                    return null;
                }

                try
                {
                    await initialized.Task.WaitAsync(TimeSpan.FromSeconds(20));
                }
                catch (TimeoutException)
                {
                    Logger.LogError("WebView2 initialization timed out after 20 seconds");
                    return null;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"WebView2 initialization failed: {ex.Message}", ex);
                    return null;
                }

                bool navigationSucceeded;
                try
                {
                    navigationSucceeded = await navigationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(45));
                }
                catch (TimeoutException)
                {
                    Logger.LogError("WebView2 navigation timed out after 45 seconds");
                    return null;
                }

                if (!navigationSucceeded || webView == null)
                {
                    return null;
                }

                var rendered = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (!mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        Logger.LogInfo("WebView2 navigation finished; expanding category tree");
                        var lastClusterCount = -1;
                        var stablePolls = 0;

                        for (var attempt = 0; attempt < 40; attempt++)
                        {
                            var stateJson = await webView.CoreWebView2.ExecuteScriptAsync(@"
                                (function() {
                                    var clicked = 0;
                                    document.querySelectorAll('.PrimeTree_NodeToggleButton').forEach(function(btn) {
                                        var li = btn.closest('li');
                                        if (li && li.getAttribute('aria-expanded') === 'false') {
                                            btn.click();
                                            clicked++;
                                        }
                                    });
                                    return {
                                        clicked: clicked,
                                        clusters: document.querySelectorAll('div.Cluster').length
                                    };
                                })();
                            ");

                            using var state = JsonDocument.Parse(stateJson);
                            var root = state.RootElement;
                            var clicked = root.GetProperty("clicked").GetInt32();
                            var clusterCount = root.GetProperty("clusters").GetInt32();

                            if (clicked == 0 && clusterCount > 0 && clusterCount == lastClusterCount)
                            {
                                stablePolls++;
                                if (stablePolls >= 3)
                                {
                                    Logger.LogInfo($"WebView2 category DOM stabilized at {clusterCount} clusters");
                                    break;
                                }
                            }
                            else
                            {
                                stablePolls = 0;
                            }

                            lastClusterCount = clusterCount;
                            await Task.Delay(250);
                        }

                        var renderedHtml = await webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
                        if (!string.IsNullOrEmpty(renderedHtml) && renderedHtml.StartsWith("\"") && renderedHtml.EndsWith("\""))
                        {
                            renderedHtml = JsonSerializer.Deserialize<string>(renderedHtml);
                        }

                        Logger.LogInfo($"Retrieved rendered HTML, length: {renderedHtml?.Length ?? 0}");
                        rendered.TrySetResult(renderedHtml);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error rendering category DOM in WebView2: {ex.Message}", ex);
                        rendered.TrySetResult(null);
                    }
                }))
                {
                    Logger.LogError("Could not enqueue WebView2 DOM rendering");
                    return null;
                }

                try
                {
                    return await rendered.Task.WaitAsync(TimeSpan.FromSeconds(20));
                }
                catch (TimeoutException)
                {
                    Logger.LogError("WebView2 category DOM rendering timed out after 20 seconds");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to render page with WebView2: {ex.Message}", ex);
                return null;
            }
            finally
            {
                if (webView != null && mainWindow != null)
                {
                    try
                    {
                        var cleanedUp = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        if (mainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                if (navigationHandler != null && webView.CoreWebView2 != null)
                                {
                                    webView.CoreWebView2.NavigationCompleted -= navigationHandler;
                                }

                                hiddenContainer?.Children.Remove(webView);
                                webView.Close();
                                Logger.LogInfo("WebView2 cleaned up successfully");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"Error cleaning up WebView2: {ex.Message}");
                            }
                            finally
                            {
                                cleanedUp.TrySetResult(true);
                            }
                        }))
                        {
                            await cleanedUp.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        }
                        else
                        {
                            Logger.LogWarning("Could not enqueue WebView2 cleanup");
                        }
                    }
                    catch (TimeoutException)
                    {
                        Logger.LogWarning("WebView2 cleanup timed out after 5 seconds");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error during WebView2 cleanup: {ex.Message}");
                    }
                }
            }
        }


        /// <summary>
        /// Comment model for mod comments (supports nested replies)
        /// </summary>
        public class ModComment
        {
            public string AuthorName { get; set; } = "";
            public string? AvatarUrl { get; set; }
            public string Body { get; set; } = "";
            public long DatePosted { get; set; }
            public List<ModComment> Replies { get; set; } = new();
        }

        /// <summary>
        /// Get comments for a mod by rendering the mod page with WebView2.
        /// Clicks all "Load Replies" buttons before extracting, so replies are included.
        /// Structure: div.Post.Flow > header(.Poster .MemberLink span, .AvatarImage, time[datetime]) + article.RichText
        /// Replies live inside .Replies .Post.Flow within the parent post.
        /// </summary>
        public static async Task<(List<ModComment>? Comments, bool HasMore, Microsoft.UI.Xaml.Controls.WebView2? WebView)> GetModCommentsAsync(int modId, bool loadAllPages = false)
        {
            try
            {
                var url = $"https://gamebanana.com/mods/{modId}";
                Logger.LogGameBanana($"=== GetModCommentsAsync START modId={modId}");

                var app = App.Current as App;
                var mainWindow = app?.MainWindow as MainWindow;
                if (mainWindow == null)
                {
                    Logger.LogGameBananaError("Could not get main window for WebView2 comments fetch");
                    return (null, false, null);
                }

                var tcs = new TaskCompletionSource<(string? Json, Microsoft.UI.Xaml.Controls.WebView2? WebView)>();

                mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        Microsoft.UI.Xaml.Controls.WebView2? webView = null;
                        try
                        {
                            Logger.LogGameBanana("Creating WebView2 instance and adding to hidden container");
                            webView = new Microsoft.UI.Xaml.Controls.WebView2();
                            // Add to hidden container so it has a real viewport - required for scroll/lazy-load
                            var container = mainWindow.GetHiddenWebViewContainer();
                            container.Children.Add(webView);
                            await webView.EnsureCoreWebView2Async();
                            webView.CoreWebView2.NavigationStarting += (_, args) =>
                            {
                                if (!WebViewNavigationPolicy.IsAllowed(args.Uri))
                                {
                                    args.Cancel = true;
                                    Logger.LogWarning($"Blocked hidden WebView navigation outside GameBanana: {args.Uri}");
                                }
                            };
                            Logger.LogGameBanana("WebView2 initialized OK, added to hidden container");

                            // cf_clearance is bound to the exact User-Agent that obtained it. Using the
                            // WebView default UA invalidates the clearance and Cloudflare answers with an
                            // empty body, which looks exactly like "no comments on this mod".
                            var cachedUserAgent = CloudflareBypassService.GetCachedUserAgent();
                            if (!string.IsNullOrEmpty(cachedUserAgent))
                            {
                                webView.CoreWebView2.Settings.UserAgent = cachedUserAgent;
                                Logger.LogGameBanana("Applied cached User-Agent to hidden WebView2");
                            }

                            // The shared WebView2 profile already contains the login cookies.
                            // Nothing is injected or copied through a plaintext cache.
                            var cachedCookies = CloudflareBypassService.GetCachedCookies();
                            if (string.IsNullOrEmpty(cachedCookies))
                            {
                                Logger.LogGameBanana("No in-memory session yet; WebView2 profile cookies will be used automatically");
                            }

                            webView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                            {
                                try
                                {
                                    Logger.LogGameBanana($"NavigationCompleted: IsSuccess={args.IsSuccess} WebErrorStatus={args.WebErrorStatus}");
                                    if (!args.IsSuccess)
                                    {
                                        tcs.TrySetResult((null, null));
                                        return;
                                    }


                                    // Keep the HttpClient cookie header in sync with the shared
                                    // WebView2 profile after every successful load.
                                    await CloudflareBypassService.CaptureFromWebViewAsync(webView.CoreWebView2);

                                    Logger.LogGameBanana("Waiting 1s for initial render...");
                                    await Task.Delay(1000);

                                    // Scroll to bottom immediately to trigger lazy-load
                                    Logger.LogGameBanana("Scrolling to bottom to trigger lazy-load");
                                    await webView.CoreWebView2.ExecuteScriptAsync(@"window.scrollTo(0, document.body.scrollHeight);");

                                    // Poll until .Posts.Flow has children (Vue renders them async)
                                    Logger.LogGameBanana("Polling for posts to appear (max 15s)...");
                                    int pollCount = 0;
                                    while (pollCount < 30)
                                    {
                                        await Task.Delay(300);
                                        var countStr = await webView.CoreWebView2.ExecuteScriptAsync(@"document.querySelectorAll('.Posts.Flow > .Post.Flow').length.toString();");
                                        Logger.LogGameBanana($"Poll {pollCount}: postCount={countStr}");
                                        // Scroll again each poll to keep triggering lazy-load
                                        await webView.CoreWebView2.ExecuteScriptAsync(@"window.scrollTo(0, document.body.scrollHeight);");
                                        if (countStr != "\"0\"" && countStr != "0") break;
                                        pollCount++;
                                    }

                                    var pageInfo = await webView.CoreWebView2.ExecuteScriptAsync(@"
                                                    (function() {
                                                        var posts = document.querySelectorAll('.Posts.Flow > .Post.Flow');
                                                        var btns = [];
                                                        document.querySelectorAll('button.ExtendedContentButton').forEach(function(b) { btns.push(b.textContent.trim()); });
                                                        var postsEl = document.querySelector('.Posts.Flow');
                                                        return JSON.stringify({ postCount: posts.length, scrollH: document.body.scrollHeight, buttons: btns, ariaExpanded: postsEl ? postsEl.getAttribute('aria-expanded') : 'null' });
                                                    })();
                                                ");
                                    Logger.LogGameBanana($"Page state after polling: {pageInfo}");

                                    // Diagnostic: what the page actually says (login wall vs empty
                                    // comments vs a layout change) - the selector alone cannot
                                    // distinguish those cases.
                                    try
                                    {
                                        var bodyPreview = await webView.CoreWebView2.ExecuteScriptAsync(
                                            "document.body ? document.body.innerText.slice(0, 400) : 'NO BODY'");
                                        Logger.LogGameBanana($"Body preview: {bodyPreview}");
                                    }
                                    catch (Exception bodyEx)
                                    {
                                        Logger.LogGameBananaError("Failed to read body preview", bodyEx);
                                    }

                                    if (loadAllPages)
                                    {
                                        for (int attempt = 0; attempt < 20; attempt++)
                                        {
                                            var hasMore = await webView.CoreWebView2.ExecuteScriptAsync(@"
                                                            (function() {
                                                                var btns = document.querySelectorAll('button.ExtendedContentButton');
                                                                for (var i = 0; i < btns.length; i++) {
                                                                    if (btns[i].textContent.indexOf('Load More') !== -1) {
                                                                        btns[i].click();
                                                                        window.scrollTo(0, document.body.scrollHeight);
                                                                        return 'true';
                                                                    }
                                                                }
                                                                return 'false';
                                                            })();
                                                        ");
                                            Logger.LogGameBanana($"Load More attempt {attempt}: result={hasMore}");
                                            if (hasMore != "\"true\"" && hasMore != "true") break;
                                            await Task.Delay(800);
                                            await webView.CoreWebView2.ExecuteScriptAsync(@"window.scrollTo(0, document.body.scrollHeight);");
                                            await Task.Delay(400);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogGameBanana("loadAllPages=false, skipping Load More clicks");
                                    }

                                    var postCount = await webView.CoreWebView2.ExecuteScriptAsync(@"document.querySelectorAll('.Posts.Flow > .Post.Flow').length.toString();");
                                    Logger.LogGameBanana($"Post count before Load Replies: {postCount}");

                                    await webView.CoreWebView2.ExecuteScriptAsync(@"
                                                    (function() {
                                                        var btns = document.querySelectorAll('button.ExtendedContentButton');
                                                        btns.forEach(function(b) {
                                                            if (b.textContent.indexOf('Load Replies') !== -1) {
                                                                try { b.click(); } catch(e) {}
                                                            }
                                                        });
                                                    })();
                                                ");
                                    await Task.Delay(800);

                                    var json = await webView.CoreWebView2.ExecuteScriptAsync(@"
                                                    (function() {
                                                        function extractPost(el) {
                                                            var authorEl = el.querySelector(':scope > header .Poster .MemberLink span');
                                                            var authorUpic = el.querySelector(':scope > header .Poster .MemberLink img.Upic');
                                                            var author = '';
                                                            if (authorEl) {
                                                                author = authorEl.textContent.trim();
                                                            } else if (authorUpic) {
                                                                var altText = authorUpic.getAttribute('alt') || '';
                                                                var uPicIdx = altText.toLowerCase().lastIndexOf(' upic');
                                                                author = (uPicIdx !== -1 ? altText.substring(0, uPicIdx) : altText).trim();
                                                            }
                                                            if (!author) {
                                                                var memberLink = el.querySelector(':scope > header .Poster .MemberLink');
                                                                if (memberLink) author = memberLink.textContent.trim();
                                                            }
                                                            var avatarEl = el.querySelector(':scope > header .AvatarImage');
                                                            var timeEl   = el.querySelector(':scope > header time');
                                                            var bodyEl   = el.querySelector(':scope > article.RichText');
                                                            var avatar = avatarEl ? avatarEl.src : '';
                                                            var date   = timeEl   ? (timeEl.getAttribute('datetime') || '') : '';
                                                            var body   = bodyEl   ? bodyEl.innerText.trim() : '';
                                                            var isDefaultAvatar = avatar.indexOf('/defaults/avatar') !== -1;
                                                            var replyEls = el.querySelectorAll(':scope > div > .Replies > .Post.Flow');
                                                            var replies = [];
                                                            replyEls.forEach(function(r) {
                                                                var rp = extractPost(r);
                                                                if (rp) replies.push(rp);
                                                            });
                                                            if (!author || !body) return null;
                                                            return { author: author, avatar: isDefaultAvatar ? '' : avatar, date: date, body: body, replies: replies };
                                                        }
                                                        var container = document.querySelector('.Posts.Flow');
                                                        if (!container) return JSON.stringify({ error: 'no .Posts.Flow', html: document.body.innerHTML.substring(0, 300) });
                                                        var topPosts = container.querySelectorAll(':scope > .Post.Flow');
                                                        var results = [];
                                                        topPosts.forEach(function(p) { var post = extractPost(p); if (post) results.push(post); });
                                                        // Check if Load More button exists
                                                        var hasMore = false;
                                                        var allBtns = document.querySelectorAll('button.ExtendedContentButton');
                                                        for (var i = 0; i < allBtns.length; i++) {
                                                            if (allBtns[i].textContent.indexOf('Load More') !== -1) { hasMore = true; break; }
                                                        }
                                                        return JSON.stringify({ comments: results, hasMore: hasMore });
                                                    })();
                                                ");

                                    Logger.LogGameBanana($"JS result length={json?.Length ?? 0} first500={json?.Substring(0, Math.Min(500, json?.Length ?? 0))}");
                                    tcs.TrySetResult((json, webView));
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogGameBananaError("Error in NavigationCompleted", ex);
                                    tcs.TrySetResult((null, null));
                                }
                                finally
                                {
                                    try { webView?.Close(); try { container.Children.Remove(webView); } catch { } } catch { }
                                }
                            };

                            Logger.LogGameBanana($"Navigating to {url}");
                            webView.CoreWebView2.Navigate(url);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogGameBananaError("Error initializing WebView2 for comments", ex);
                            try { webView?.Close(); } catch { }
                            tcs.TrySetResult((null, null));
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("GameBananaService.GetModCommentsAsync failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("GameBananaService.GetModCommentsAsync failed", ex);
                    }
                });

                var timeoutTask = Task.Delay(40000);
                var completed = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completed == timeoutTask)
                {
                    Logger.LogGameBananaError("Timed out waiting for comments (40s)");
                    return (null, false, null);
                }

                var tcsResult = await tcs.Task;
                var rawJson = tcsResult.Json;
                var returnedWebView = tcsResult.WebView;
                Logger.LogGameBanana($"TCS result length={rawJson?.Length ?? 0}");

                if (string.IsNullOrEmpty(rawJson) || rawJson == "null")
                {
                    Logger.LogGameBanana("rawJson null/empty - returning empty list");
                    return (new List<ModComment>(), false, null);
                }

                if (rawJson.StartsWith("\"") && rawJson.EndsWith("\""))
                {
                    rawJson = System.Text.Json.JsonSerializer.Deserialize<string>(rawJson) ?? rawJson;
                    Logger.LogGameBanana($"Unwrapped JSON length={rawJson?.Length ?? 0}");
                }

                Logger.LogGameBanana($"Final JSON first 500: {rawJson?.Substring(0, Math.Min(500, rawJson?.Length ?? 0))}");

                if (rawJson == "[]")
                {
                    Logger.LogGameBanana("Empty array - no comments");
                    return (new List<ModComment>(), false, returnedWebView);
                }

                // Parse new format { comments: [...], hasMore: bool }
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(rawJson!);
                    if (doc.RootElement.TryGetProperty("comments", out var commentsEl) &&
                        doc.RootElement.TryGetProperty("hasMore", out var hasMoreEl))
                    {
                        var comments = new List<ModComment>();
                        foreach (var el in commentsEl.EnumerateArray())
                            ParseCommentElement(el, comments);
                        var hasMore = hasMoreEl.GetBoolean();
                        Logger.LogGameBanana($"=== GetModCommentsAsync END: {comments.Count} comments, hasMore={hasMore}");
                        return (comments, hasMore, returnedWebView);
                    }
                }
                catch { }

                // Fallback: plain array
                var result = ParseCommentsFromJson(rawJson!);
                Logger.LogGameBanana($"=== GetModCommentsAsync END (fallback): {result.Count} comments");
                return (result, false, returnedWebView);
            }
            catch (Exception ex)
            {
                Logger.LogGameBananaError($"Failed to get comments for mod {modId}", ex);
                return (null, false, null);
            }
        }

        /// <summary>
        /// Extract comments from an already-loaded WebView2 (no navigation needed).
        /// Optionally clicks Load More first.
        /// </summary>
        public static async Task<(List<ModComment> Comments, bool HasMore)> ExtractCommentsFromWebViewAsync(
            Microsoft.UI.Xaml.Controls.WebView2 webView, bool clickLoadMore = false)
        {
            try
            {
                if (clickLoadMore)
                {
                    // Click Load More and wait for new comments to render
                    var clicked = await webView.CoreWebView2.ExecuteScriptAsync(@"
                        (function() {
                            var btns = document.querySelectorAll('button.ExtendedContentButton');
                            for (var i = 0; i < btns.length; i++) {
                                if (btns[i].textContent.indexOf('Load More') !== -1) {
                                    btns[i].click();
                                    window.scrollTo(0, document.body.scrollHeight);
                                    return 'true';
                                }
                            }
                            return 'false';
                        })();
                    ");
                    Logger.LogGameBanana($"ExtractCommentsFromWebViewAsync: clickLoadMore result={clicked}");
                    if (clicked == "\"true\"" || clicked == "true")
                        await System.Threading.Tasks.Task.Delay(900);
                }

                // Click Load Replies for any new posts
                await webView.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        var btns = document.querySelectorAll('button.ExtendedContentButton');
                        btns.forEach(function(b) {
                            if (b.textContent.indexOf('Load Replies') !== -1) {
                                try { b.click(); } catch(e) {}
                            }
                        });
                    })();
                ");
                await System.Threading.Tasks.Task.Delay(500);

                var json = await webView.CoreWebView2.ExecuteScriptAsync(@"
                    (function() {
                        function extractPost(el) {
                            var authorEl = el.querySelector(':scope > header .Poster .MemberLink span');
                            var authorUpic = el.querySelector(':scope > header .Poster .MemberLink img.Upic');
                            var author = '';
                            if (authorEl) {
                                author = authorEl.textContent.trim();
                            } else if (authorUpic) {
                                var altText = authorUpic.getAttribute('alt') || '';
                                var uPicIdx = altText.toLowerCase().lastIndexOf(' upic');
                                author = (uPicIdx !== -1 ? altText.substring(0, uPicIdx) : altText).trim();
                            }
                            if (!author) {
                                var memberLink = el.querySelector(':scope > header .Poster .MemberLink');
                                if (memberLink) author = memberLink.textContent.trim();
                            }
                            var avatarEl = el.querySelector(':scope > header .AvatarImage');
                            var timeEl   = el.querySelector(':scope > header time');
                            var bodyEl   = el.querySelector(':scope > article.RichText');
                            var avatar = avatarEl ? avatarEl.src : '';
                            var date   = timeEl   ? (timeEl.getAttribute('datetime') || '') : '';
                            var body   = bodyEl   ? bodyEl.innerText.trim() : '';
                            var isDefaultAvatar = avatar.indexOf('/defaults/avatar') !== -1;
                            var replyEls = el.querySelectorAll(':scope > div > .Replies > .Post.Flow');
                            var replies = [];
                            replyEls.forEach(function(r) {
                                var rp = extractPost(r);
                                if (rp) replies.push(rp);
                            });
                            if (!author || !body) return null;
                            return { author: author, avatar: isDefaultAvatar ? '' : avatar, date: date, body: body, replies: replies };
                        }
                        var container = document.querySelector('.Posts.Flow');
                        if (!container) return JSON.stringify({ comments: [], hasMore: false });
                        var topPosts = container.querySelectorAll(':scope > .Post.Flow');
                        var results = [];
                        topPosts.forEach(function(p) { var post = extractPost(p); if (post) results.push(post); });
                        var hasMore = false;
                        var allBtns = document.querySelectorAll('button.ExtendedContentButton');
                        for (var i = 0; i < allBtns.length; i++) {
                            if (allBtns[i].textContent.indexOf('Load More') !== -1) { hasMore = true; break; }
                        }
                        return JSON.stringify({ comments: results, hasMore: hasMore });
                    })();
                ");

                if (json != null && (json.StartsWith("\"") && json.EndsWith("\"")))
                    json = System.Text.Json.JsonSerializer.Deserialize<string>(json) ?? json;

                Logger.LogGameBanana($"ExtractCommentsFromWebViewAsync: json length={json?.Length ?? 0}");

                using var doc = System.Text.Json.JsonDocument.Parse(json ?? "{}");
                var comments = new List<ModComment>();
                var hasMoreResult = false;

                if (doc.RootElement.TryGetProperty("comments", out var commentsEl))
                    foreach (var el in commentsEl.EnumerateArray())
                        ParseCommentElement(el, comments);

                if (doc.RootElement.TryGetProperty("hasMore", out var hasMoreEl))
                    hasMoreResult = hasMoreEl.GetBoolean();

                return (comments, hasMoreResult);
            }
            catch (Exception ex)
            {
                Logger.LogGameBananaError("ExtractCommentsFromWebViewAsync failed", ex);
                return (new List<ModComment>(), false);
            }
        }

        private static List<ModComment> ParseCommentsFromJson(string json)
        {
            var comments = new List<ModComment>();
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return comments;

                foreach (var el in doc.RootElement.EnumerateArray())
                    ParseCommentElement(el, comments);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to parse comments JSON: {ex.Message}", ex);
            }
            return comments;
        }

        private static void ParseCommentElement(System.Text.Json.JsonElement el, List<ModComment> target)
        {
            var author  = el.TryGetProperty("author", out var a)  ? a.GetString() ?? "" : "";
            var body    = el.TryGetProperty("body",   out var b)  ? b.GetString() ?? "" : "";
            var avatar  = el.TryGetProperty("avatar", out var av) ? av.GetString() : null;
            var dateStr = el.TryGetProperty("date",   out var d)  ? d.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(body))
                return;

            long timestamp = 0;
            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            {
                timestamp = new DateTimeOffset(dt).ToUnixTimeSeconds();
            }

            var comment = new ModComment
            {
                AuthorName = author,
                AvatarUrl  = string.IsNullOrEmpty(avatar) ? null : avatar,
                Body       = body,
                DatePosted = timestamp
            };

            // Recurse into replies
            if (el.TryGetProperty("replies", out var repliesEl) &&
                repliesEl.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var r in repliesEl.EnumerateArray())
                    ParseCommentElement(r, comment.Replies);
            }

            target.Add(comment);
        }
    }
}
