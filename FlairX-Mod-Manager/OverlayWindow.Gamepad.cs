using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using WinRT;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    public sealed partial class OverlayWindow
    {
        
        // Gamepad support — shared instance from MainWindow (not owned here)
        private GamepadManager? _gamepadManager;
        private bool _ownsGamepadManager = false;
        
        // Held buttons for combo detection
        private HashSet<string> _heldButtons = new();

        // Flag to ignore input briefly after overlay opens (prevents combo buttons from triggering)
        private bool _ignoreInputAfterShow = true; // Start with true, enable after first full show

        // Key-repeat state for held navigation buttons
        private string? _repeatButton = null;
        private System.Threading.CancellationTokenSource? _repeatCts = null;
        private const int REPEAT_INITIAL_DELAY_MS = 400;
        private const int REPEAT_INTERVAL_MS = 100;


        private void AttachGamepad(GamepadManager? sharedManager)
        {
            if (!SettingsManager.Current.GamepadEnabled)
            {
                Logger.LogInfo("Gamepad support disabled in settings");
                return;
            }

            if (sharedManager == null)
            {
                Logger.LogInfo("No shared gamepad manager available");
                return;
            }

            _gamepadManager = sharedManager;
            _ownsGamepadManager = false; // MainWindow owns it, we just subscribe

            _gamepadManager.ButtonPressed += OnGamepadButtonPressed;
            _gamepadManager.ButtonReleased += OnGamepadButtonReleased;
            _gamepadManager.LeftThumbstickMoved += OnLeftThumbstickMoved;
            _gamepadManager.StickMoved += OnGamepadStickMoved;
            _gamepadManager.RawAxisMoved += OnGamepadRawAxisMoved;

            Logger.LogInfo("Gamepad attached to overlay (shared XInput instance)");
        }

        /// <summary>
        /// Called from MainWindow when gamepad settings change — re-attach to new instance.
        /// </summary>
        public void RefreshGamepad(GamepadManager? sharedManager)
        {
            DetachGamepad();
            AttachGamepad(sharedManager);
        }

        private void DetachGamepad()
        {
            if (_gamepadManager == null) return;

            _gamepadManager.ButtonPressed -= OnGamepadButtonPressed;
            _gamepadManager.ButtonReleased -= OnGamepadButtonReleased;
            _gamepadManager.LeftThumbstickMoved -= OnLeftThumbstickMoved;
            _gamepadManager.StickMoved -= OnGamepadStickMoved;
            _gamepadManager.RawAxisMoved -= OnGamepadRawAxisMoved;

            // Only dispose if we own it (we never do in current design)
            if (_ownsGamepadManager)
                _gamepadManager.Dispose();

            _gamepadManager = null;
        }

        private void OnGamepadButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // Ignore input if overlay is not visible
                    if (!IsOverlayVisible) return;
                    
                    // Ignore all input until all buttons are released after opening overlay
                    if (_ignoreInputAfterShow)
                    {
                        Logger.LogInfo($"Ignoring gamepad input after show: {e.GetButtonDisplayName()}");
                        return;
                    }
                    
                    var settings = SettingsManager.Current;
                    var buttonName = e.GetButtonDisplayName();
                    _heldButtons.Add(buttonName);

                    Logger.LogInfo($"Overlay gamepad button: {buttonName}");
                    
                    // Check for filter active combo
                    var filterCombo = settings.GamepadFilterActiveCombo ?? "Back+A";
                    var filterComboButtons = new HashSet<string>(filterCombo.Split('+'));
                    if (_heldButtons.SetEquals(filterComboButtons))
                    {
                        ToggleActiveOnlyFilter();
                        _gamepadManager?.Vibrate(0, 30000, 300);
                        _heldButtons.Clear();
                        return;
                    }
                    
                    // Navigate in grid - D-Pad for 4-direction movement (only if left stick is disabled)
                    if (!settings.GamepadUseLeftStick)
                    {
                        if (IsButtonMatch(buttonName, settings.GamepadDPadUp))
                        {
                            Logger.LogInfo("Navigate UP");
                            NavigateModGrid(0, -1);
                            StartRepeat(buttonName, () => NavigateModGrid(0, -1));
                            return;
                        }
                        else if (IsButtonMatch(buttonName, settings.GamepadDPadDown))
                        {
                            Logger.LogInfo("Navigate DOWN");
                            NavigateModGrid(0, 1);
                            StartRepeat(buttonName, () => NavigateModGrid(0, 1));
                            return;
                        }
                        else if (IsButtonMatch(buttonName, settings.GamepadDPadLeft))
                        {
                            Logger.LogInfo("Navigate LEFT");
                            NavigateModGrid(-1, 0);
                            StartRepeat(buttonName, () => NavigateModGrid(-1, 0));
                            return;
                        }
                        else if (IsButtonMatch(buttonName, settings.GamepadDPadRight))
                        {
                            Logger.LogInfo("Navigate RIGHT");
                            NavigateModGrid(1, 0);
                            StartRepeat(buttonName, () => NavigateModGrid(1, 0));
                            return;
                        }
                    }
                    
                    // Next/Prev category — also support repeat
                    if (IsButtonMatch(buttonName, settings.GamepadNextCategoryButton))
                    {
                        NavigateCategory(1);
                        StartRepeat(buttonName, () => NavigateCategory(1));
                        return;
                    }
                    else if (IsButtonMatch(buttonName, settings.GamepadPrevCategoryButton))
                    {
                        NavigateCategory(-1);
                        StartRepeat(buttonName, () => NavigateCategory(-1));
                        return;
                    }
                    
                    // Select/Toggle mod
                    if (IsButtonMatch(buttonName, settings.GamepadSelectButton))
                    {
                        ToggleSelectedMod();
                    }
                    // Toggle category favorite
                    else if (IsButtonMatch(buttonName, settings.GamepadCategoryFavoriteButton))
                    {
                        Logger.LogInfo($"Overlay Gamepad: {buttonName} button pressed - toggling category favorite");
                        ToggleCategoryFavorite();
                    }
                    // Toggle mod favorite
                    else if (IsButtonMatch(buttonName, settings.GamepadModFavoriteButton))
                    {
                        Logger.LogInfo($"Overlay Gamepad: {buttonName} button pressed - toggling mod favorite");
                        ToggleModFavorite();
                    }
                    // Back/Close
                    else if (IsButtonMatch(buttonName, settings.GamepadBackButton))
                    {
                        Hide();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error handling gamepad input in overlay", ex);
                }
            });
        }

        private void OnGamepadButtonReleased(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            _heldButtons.Remove(buttonName);
            
            // Stop repeat if the released button was the one repeating
            if (_repeatButton == buttonName)
                StopRepeat();
        }

        private void StartRepeat(string buttonName, Action action)
        {
            StopRepeat();
            _repeatButton = buttonName;
            _repeatCts = new System.Threading.CancellationTokenSource();
            var token = _repeatCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(REPEAT_INITIAL_DELAY_MS, token);
                    while (!token.IsCancellationRequested)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (token.IsCancellationRequested) return;

                            // Self-healing guard: if the release event was missed and the overlay
                            // is gone, stop repeating instead of driving an invisible window.
                            if (!IsOverlayVisible)
                            {
                                StopRepeat();
                                return;
                            }

                            action();
                        });
                        await Task.Delay(REPEAT_INTERVAL_MS, token);
                    }
                }
                catch (TaskCanceledException) { }
            });
        }

        private void StopRepeat()
        {
            _repeatCts?.Cancel();
            _repeatCts = null;
            _repeatButton = null;
        }

        private void OnLeftThumbstickMoved(object? sender, ThumbstickEventArgs e)
        {
            // Only handle if left stick navigation is enabled and overlay is visible
            if (!SettingsManager.Current.GamepadUseLeftStick) return;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // Ignore input if overlay is not visible
                    if (!IsOverlayVisible) return;
                    
                    if (e.IsUp)
                    {
                        Logger.LogInfo("Left stick UP");
                        NavigateModGrid(0, -1);
                    }
                    else if (e.IsDown)
                    {
                        Logger.LogInfo("Left stick DOWN");
                        NavigateModGrid(0, 1);
                    }
                    else if (e.IsLeft)
                    {
                        Logger.LogInfo("Left stick LEFT");
                        NavigateModGrid(-1, 0);
                    }
                    else if (e.IsRight)
                    {
                        Logger.LogInfo("Left stick RIGHT");
                        NavigateModGrid(1, 0);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error handling left thumbstick input", ex);
                }
            });
        }

        private void OnGamepadStickMoved(object? sender, GamepadButtonEventArgs e)
        {
            // This method is kept for compatibility but discrete scrolling is now handled by OnGamepadRawAxisMoved
            // Only handle non-right-stick movements here if needed for other functionality
        }

        private void OnGamepadRawAxisMoved(object? sender, SDL3RawAxisEventArgs e)
        {
            // The gamepad poll loop raises this ~62 times per second for the entire lifetime of
            // the process. It used to marshal every single one to the UI thread and then return
            // immediately when the overlay was hidden - about 223k wasted UI wake-ups per hour.
            // _gamepadManager.IsOverlayActive mirrors IsOverlayVisible but is a plain bool that is
            // safe to read from the polling thread.
            if (_gamepadManager?.IsOverlayActive != true)
                return;

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // Ignore input if overlay is not visible
                    if (!IsOverlayVisible) return;
                    
                    var settings = SettingsManager.Current;
                    
                    // Handle right stick for smooth hotkeys scrolling
                    if (settings.GamepadUseRightStickForHotkeys && HotkeysScrollViewer != null)
                    {
                        var rightY = e.GetNormalizedRightY(); // -1.0 to 1.0
                        
                        // Apply deadzone (about 15% of full range)
                        const float deadzone = 0.15f;
                        if (Math.Abs(rightY) > deadzone)
                        {
                            // Remove deadzone and rescale to full range
                            var adjustedY = (Math.Abs(rightY) - deadzone) / (1.0f - deadzone);
                            adjustedY *= Math.Sign(rightY); // Restore sign
                            
                            // Calculate scroll speed based on deflection
                            // Max speed: 800 pixels per second, scaled by deflection
                            var maxScrollSpeed = 800.0;
                            var scrollSpeed = maxScrollSpeed * Math.Abs(adjustedY);
                            
                            // Calculate scroll amount for this frame (assuming ~60fps polling)
                            var scrollAmount = scrollSpeed * (16.0 / 1000.0); // 16ms frame time
                            
                            // Apply direction (positive Y = down = scroll down)
                            scrollAmount *= Math.Sign(adjustedY);
                            
                            // Apply scroll
                            var currentOffset = HotkeysScrollViewer.VerticalOffset;
                            var newOffset = currentOffset + scrollAmount;
                            
                            // Clamp to valid range
                            newOffset = Math.Max(0, Math.Min(newOffset, HotkeysScrollViewer.ScrollableHeight));
                            
                            HotkeysScrollViewer.ScrollToVerticalOffset(newOffset);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error handling gamepad raw axis input", ex);
                }
            });
        }

        private void ScrollHotkeysPanel(int direction)
        {
            try
            {
                if (HotkeysScrollViewer == null) return;
                
                // Scroll by one hotkey row height (approximately 56px including margin)
                var scrollAmount = 56.0 * direction;
                var newOffset = HotkeysScrollViewer.VerticalOffset + scrollAmount;
                
                // Clamp to valid range
                newOffset = Math.Max(0, Math.Min(newOffset, HotkeysScrollViewer.ScrollableHeight));
                
                HotkeysScrollViewer.ScrollToVerticalOffset(newOffset);
                
                // Vibrate on scroll if enabled
                if (SettingsManager.Current.GamepadVibrateOnNavigation)
                {
                    _gamepadManager?.Vibrate(0, 10000, 30);
                }
                
                Logger.LogInfo($"Scrolled hotkeys panel to offset: {newOffset}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error scrolling hotkeys panel", ex);
            }
        }

        private bool IsButtonMatch(string pressedButton, string configuredButton)
        {
            return string.Equals(pressedButton, configuredButton, StringComparison.OrdinalIgnoreCase);
        }

        private int GetItemsPerRow()
        {
            try
            {
                // Calculate items per row based on grid width
                // Tile width is 208 + 16 spacing = 224 (from UniformGridLayout MinItemWidth=220)
                var scrollViewer = ModsRepeater?.Parent as ScrollViewer;
                if (scrollViewer == null) return 2; // Default fallback
                
                var availableWidth = scrollViewer.ActualWidth - 8; // Margin
                if (availableWidth <= 0) return 2; // Window not yet loaded
                
                var itemWidth = 220.0; // MinItemWidth from UniformGridLayout
                if (itemWidth <= 0) return 2; // Safety check
                
                var result = (int)(availableWidth / itemWidth);
                return Math.Max(1, result);
            }
            catch
            {
                return 2; // Safe fallback
            }
        }

        private async void NavigateModGrid(int deltaX, int deltaY)
        {
            try
            {
                if (OverlayMods.Count == 0) return;

                // Guard: if current index is out of range (e.g. collection changed during async load), reset it
                if (_selectedModIndex >= OverlayMods.Count)
                {
                    _selectedModIndex = -1;
                    _selectedMod = null;
                }

                // Deselect previous
                if (_selectedModIndex >= 0 && _selectedModIndex < OverlayMods.Count)
                {
                    OverlayMods[_selectedModIndex].IsSelected = false;
                }

                // Initialize selection if none
                if (_selectedModIndex < 0)
                {
                    _selectedModIndex = 0;
                    if (OverlayMods.Count > 0)
                    {
                        OverlayMods[_selectedModIndex].IsSelected = true;
                        _selectedMod = OverlayMods[_selectedModIndex];
                        UpdateHotkeysPanel(_selectedMod);
                    }
                    return;
                }

                var itemsPerRow = GetItemsPerRow();
                if (itemsPerRow <= 0) itemsPerRow = 1; // Safety: prevent division by zero

                var currentRow = _selectedModIndex / itemsPerRow;
                var currentCol = _selectedModIndex % itemsPerRow;
                var totalRows = (OverlayMods.Count + itemsPerRow - 1) / itemsPerRow;
                if (totalRows <= 0) totalRows = 1; // Safety

                // Calculate new position
                var newCol = currentCol + deltaX;
                var newRow = currentRow + deltaY;

                // Wrap columns
                if (newCol < 0)
                {
                    newCol = itemsPerRow - 1;
                    newRow--; // Move to previous row
                }
                else if (newCol >= itemsPerRow)
                {
                    newCol = 0;
                    newRow++; // Move to next row
                }

                // Wrap rows
                if (newRow < 0)
                    newRow = totalRows - 1;
                else if (newRow >= totalRows)
                    newRow = 0;

                // Calculate new index
                var newIndex = newRow * itemsPerRow + newCol;

                // Clamp to valid range
                if (newIndex >= OverlayMods.Count)
                {
                    // If we went past the end, go to last item
                    newIndex = OverlayMods.Count - 1;
                }

                _selectedModIndex = Math.Max(0, Math.Min(newIndex, OverlayMods.Count - 1));

                // Select new
                OverlayMods[_selectedModIndex].IsSelected = true;
                _selectedMod = OverlayMods[_selectedModIndex];

                // Update hotkeys panel for newly selected mod
                UpdateHotkeysPanel(_selectedMod);

                // Scroll selected mod into view
                await ScrollModIntoView(_selectedModIndex);

                // Vibrate on navigation if enabled
                if (SettingsManager.Current.GamepadVibrateOnNavigation)
                {
                    _gamepadManager?.Vibrate(0, 15000, 50);
                }

                Logger.LogInfo($"Gamepad grid navigation: index {_selectedModIndex} (row {newRow}, col {newCol})");
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("OverlayWindow.NavigateModGrid failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("OverlayWindow.NavigateModGrid failed", ex);
            }
        }
        
        private async Task ScrollModIntoView(int index)
        {
            if (ModsScrollViewer == null || ModsRepeater == null) return;
            if (index < 0 || index >= OverlayMods.Count) return;
            
            try
            {
                var itemsPerRow = GetItemsPerRow();
                if (itemsPerRow <= 0) itemsPerRow = 1;

                // UniformGridLayout: MinItemHeight=262, MinRowSpacing=16
                const double itemHeight = 262.0;
                const double rowSpacing = 16.0;
                var rowHeight = itemHeight + rowSpacing;

                var row = index / itemsPerRow;
                var itemTop = row * rowHeight;
                var itemBottom = itemTop + itemHeight;

                var viewportHeight = ModsScrollViewer.ViewportHeight;
                var currentOffset = ModsScrollViewer.VerticalOffset;

                // Only scroll if item is not fully visible
                if (itemTop < currentOffset || itemBottom > currentOffset + viewportHeight)
                {
                    // Center the row in viewport
                    var targetOffset = itemTop - (viewportHeight / 2) + (itemHeight / 2);
                    targetOffset = Math.Max(0, Math.Min(targetOffset, ModsScrollViewer.ScrollableHeight));
                    ModsScrollViewer.ChangeView(null, targetOffset, null, false);
                    Logger.LogInfo($"Scrolled to mod at index {index} (row {row}), offset: {targetOffset}");
                }

                // Also try element-based scroll as fallback if viewport size not yet known
                if (viewportHeight <= 0)
                {
                    UIElement? element = null;
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        element = ModsRepeater.TryGetElement(index);
                        if (element != null) break;
                        await Task.Delay(100);
                    }
                    if (element != null)
                    {
                        var transform = element.TransformToVisual(ModsScrollViewer);
                        var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                        var elementHeight = element.ActualSize.Y;
                        var vp = ModsScrollViewer.ViewportHeight;
                        var cur = ModsScrollViewer.VerticalOffset;
                        var target = cur + position.Y - (vp / 2) + (elementHeight / 2);
                        target = Math.Max(0, Math.Min(target, ModsScrollViewer.ScrollableHeight));
                        ModsScrollViewer.ChangeView(null, target, null, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScrollModIntoView error: {ex.Message}");
            }
        }

        private void ToggleSelectedMod()
        {
            if (_selectedModIndex >= 0 && _selectedModIndex < OverlayMods.Count)
            {
                var mod = OverlayMods[_selectedModIndex];
                ToggleMod(mod, vibrate: true);
            }
        }

        private void NavigateCategory(int direction)
        {
            if (OverlayCategories.Count == 0) return;

            var currentIndex = OverlayCategories.ToList().FindIndex(c => c.IsSelected);
            var newIndex = currentIndex + direction;

            // Wrap around
            if (newIndex < 0)
                newIndex = OverlayCategories.Count - 1;
            else if (newIndex >= OverlayCategories.Count)
                newIndex = 0;

            SelectCategory(OverlayCategories[newIndex]);
            
            // Scroll to make selected category visible
            ScrollCategoryIntoView(newIndex);
            
            // Reset mod selection
            _selectedModIndex = -1;
            _selectedMod = null;
            UpdateHotkeysPanel(null);
            
            // Vibrate on category change (right motor only - small)
            _gamepadManager?.Vibrate(0, 30000, 300);
        }
        
        private void ScrollCategoryIntoView(int index)
        {
            try
            {
                // Try to get the actual element from ItemsRepeater
                var element = CategoriesRepeater.TryGetElement(index);
                if (element != null)
                {
                    // Get element's position relative to the ScrollViewer
                    var transform = element.TransformToVisual(CategoriesScrollViewer);
                    var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    
                    var elementHeight = element.ActualSize.Y;
                    var currentOffset = CategoriesScrollViewer.VerticalOffset;
                    var viewportHeight = CategoriesScrollViewer.ViewportHeight;
                    
                    // Calculate absolute position in scrollable content
                    var absoluteTop = currentOffset + position.Y;
                    var absoluteBottom = absoluteTop + elementHeight;
                    
                    // Check if item is above visible area
                    if (absoluteTop < currentOffset)
                    {
                        CategoriesScrollViewer.ChangeView(null, absoluteTop, null);
                    }
                    // Check if item is below visible area
                    else if (absoluteBottom > currentOffset + viewportHeight)
                    {
                        var newOffset = absoluteBottom - viewportHeight;
                        CategoriesScrollViewer.ChangeView(null, newOffset, null);
                    }
                }
                else
                {
                    // Fallback: estimate position based on index
                    // Each category item is approximately 56px tall
                    const double itemHeight = 56;
                    var targetOffset = index * itemHeight;
                    CategoriesScrollViewer.ChangeView(null, targetOffset, null);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to scroll category into view", ex);
            }
        }
    }
}
