using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {
        private async void ReloadModsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ReloadModsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ReloadModsButton_Click", ex);
            }
        }

        // --- Reload veil -------------------------------------------------------------------
        // The whole window is covered with the same acrylic brush as the XXMI launcher button
        // for the duration of a reload, plus a short settle period afterwards. The grid assigns
        // thumbnails from deferred dispatcher callbacks, so the seconds right after LoadAllMods
        // are exactly where the layout crash was observed - the veil has to outlive that.
        private const int ReloadOverlaySettleMs = 1500;
        private const int ReloadOverlayFadeInMs = 250;
        private const int ReloadOverlayFadeOutMs = 250;
        private int _reloadOverlayGeneration;
        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _reloadOverlayHideTimer;
        private Storyboard? _reloadOverlayFade;
        private bool _reloadOverlayHiding;

        /// <summary>
        /// Fades the veil's opacity. Any fade still running is stopped first, so a show that
        /// lands in the middle of a fade-out does not get collapsed by the stale completion.
        /// </summary>
        private void FadeReloadOverlay(double from, double to, int durationMs, Action? onCompleted = null)
        {
            _reloadOverlayFade?.Stop();
            _reloadOverlayFade = null;

            var fade = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new CubicEase
                {
                    EasingMode = to > from ? EasingMode.EaseOut : EasingMode.EaseIn
                }
            };

            Storyboard.SetTarget(fade, ReloadOverlay);
            Storyboard.SetTargetProperty(fade, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fade);
            if (onCompleted != null)
            {
                storyboard.Completed += (_, _) => onCompleted();
            }

            _reloadOverlayFade = storyboard;
            storyboard.Begin();
        }

        /// <summary>
        /// Marks the start of a reload and shows the veil. Returns a generation token that must
        /// be handed to <see cref="EndReloadOverlay"/>. Overlapping reloads each get their own
        /// token, so an early finisher cannot lift the veil while another rebuild is still running.
        /// </summary>
        private int BeginReloadOverlay()
        {
            var generation = Interlocked.Increment(ref _reloadOverlayGeneration);
            SetReloadOverlayVisible(true);
            return generation;
        }

        /// <summary>
        /// Ends the reload behind the given token and lifts the veil after a short settle delay.
        /// </summary>
        private void EndReloadOverlay(int generation)
        {
            if (generation != _reloadOverlayGeneration)
                return; // a newer reload owns the veil now

            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => EndReloadOverlay(generation));
                return;
            }

            try
            {
                _reloadOverlayHideTimer?.Stop();
                _reloadOverlayHideTimer = null;

                var timer = DispatcherQueue.CreateTimer();
                timer.Interval = TimeSpan.FromMilliseconds(ReloadOverlaySettleMs);
                timer.IsRepeating = false;
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (generation == _reloadOverlayGeneration)
                    {
                        SetReloadOverlayVisible(false);
                    }
                };
                timer.Start();
                _reloadOverlayHideTimer = timer;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to schedule reload overlay hide", ex);
                SetReloadOverlayVisible(false);
            }
        }

        /// <summary>
        /// Shows or hides the full-window reload veil. While it is visible the NavigationView is
        /// disabled as well, which closes the keyboard/gamepad routes (pane toggle, menu items)
        /// that a pointer-blocking layer alone would not cover.
        /// </summary>
        private void SetReloadOverlayVisible(bool visible)
        {
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => SetReloadOverlayVisible(visible));
                return;
            }

            try
            {
                if (visible)
                {
                    _reloadOverlayHideTimer?.Stop();
                    _reloadOverlayHideTimer = null;
                    _reloadOverlayHiding = false;

                    // Input goes first: whatever happens to the animation, the window is locked.
                    nvSample.IsEnabled = false;

                    ReloadOverlayText.Text = SharedUtilities.GetTranslation(_lang, "Loading");
                    ReloadOverlayRing.IsActive = true;

                    if (ReloadOverlay.Visibility != Visibility.Visible)
                    {
                        ReloadOverlay.Opacity = 0;
                        ReloadOverlay.Visibility = Visibility.Visible;
                        FadeReloadOverlay(0, 1, ReloadOverlayFadeInMs);
                    }
                    else if (ReloadOverlay.Opacity < 1)
                    {
                        // A fade-out was still running when the next reload started.
                        FadeReloadOverlay(ReloadOverlay.Opacity, 1, ReloadOverlayFadeInMs);
                    }

                    Logger.LogInfo("Reload overlay: visible (fade in) - main window input blocked");
                }
                else
                {
                    if (ReloadOverlay.Visibility != Visibility.Visible)
                        return;

                    _reloadOverlayHiding = true;

                    // Input comes back right away, but the veil itself keeps swallowing pointer
                    // events until it is collapsed, so nothing can be clicked during the fade.
                    nvSample.IsEnabled = true;

                    FadeReloadOverlay(ReloadOverlay.Opacity, 0, ReloadOverlayFadeOutMs, () =>
                    {
                        // A reload may have started while this fade was running - it owns the
                        // veil now and must not be collapsed underneath it.
                        if (!_reloadOverlayHiding) return;
                        _reloadOverlayHiding = false;

                        ReloadOverlay.Visibility = Visibility.Collapsed;
                        ReloadOverlayRing.IsActive = false;
                        ReloadOverlay.Opacity = 1;
                        Logger.LogInfo("Reload overlay: hidden (fade out complete) - main window input restored");
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to {(visible ? "show" : "hide")} reload overlay", ex);
            }
        }

        public async Task ReloadModsAsync()
        {
            // From here until the UI update below finishes, the window takes no input at all.
            var overlayGeneration = BeginReloadOverlay();

            // Tracks whether the UI pass made it onto the dispatcher queue. If it did not,
            // the veil has to be lifted here instead of inside that callback.
            bool uiUpdateScheduled = false;

            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        // Update mod.json files first; new folders may appear here and must be
                        // present in the list scan below.
                        LogToGridLog("REFRESH: Updating mod.json files with namespace info");
                        var app = App.Current as App;
                        if (app != null)
                        {
                            await app.EnsureModJsonInXXMIMods();
                        }
                        LogToGridLog("REFRESH: Mod.json files updated");

                        // Rebuild once, after the files are up to date. The old order rebuilt
                        // before this scan and the page then rebuilt again in Loaded.
                        LogToGridLog("REFRESH: Rebuilding mod lists");
                        ModListManager.RebuildAllLists();
                        LogToGridLog("REFRESH: Mod lists rebuilt");

                        await PreloadModImages();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error during refresh", ex);
                    }
                });

                // Update UI on main thread
                uiUpdateScheduled = DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        Logger.LogInfo("Reload: applying UI update (veil is up)");

                        // Capture the current category/view before navigating; otherwise the
                        // page is reloaded here and loaded a second time by RestoreLastPosition.
                        SaveCurrentPositionBeforeReload();

                        SetSearchBoxPlaceholder();
                        SetFooterMenuTranslations();
                        UpdateGameSelectionComboBoxTexts();

                        // Force menu regeneration even if suppressed (for reload after category creation)
                        var wasSuppressed = _suppressMenuRegeneration;
                        _suppressMenuRegeneration = false;
                        _ = GenerateModCharacterMenuAsync();
                        _suppressMenuRegeneration = wasSuppressed;

                        // LoadAllMods/LoadCategories used to run here and then again from
                        // RestoreLastPosition (which recreates ModGridPage). Navigate once.
                        Logger.LogInfo("Reload: restoring the current view once");

                        UpdateAllModsButtonState();
                        nvSample.SelectedItem = null; // Unselect active button
                        RestoreLastPosition();

                        // Auto-detect hotkeys for all mods in background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                Logger.LogInfo("Starting full hotkey detection after reload");
                                await Pages.HotkeyFinderPage.RefreshAllModsHotkeysStaticAsync();
                                Logger.LogInfo("Completed full hotkey detection after reload");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("Failed to auto-detect hotkeys after reload", ex);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error while applying reloaded mods to the UI", ex);
                    }
                    finally
                    {
                        EndReloadOverlay(overlayGeneration);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during reload", ex);
            }
            finally
            {
                if (!uiUpdateScheduled)
                {
                    // The UI pass never reached the dispatcher - never leave the veil up.
                    EndReloadOverlay(overlayGeneration);
                }
            }
        }
    }
}
