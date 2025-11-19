using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    /// <summary>
    /// ModGridPage partial class - Zoom functionality
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        private double _zoomFactor = 1.0;
        private double _baseTileSize = 277;
        private double _baseDescHeight = 56;

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                // Only allow enlarging, minimum is 1.0 (100%)
                double clamped = Math.Max(1.0, Math.Min(2.5, value));
                if (_zoomFactor != clamped)
                {
                    _zoomFactor = clamped;
                    
                    // Update grid sizes asynchronously to avoid blocking mouse wheel
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        UpdateGridItemSizes();
                    });
                    
                    // Save zoom level to settings
                    FlairX_Mod_Manager.SettingsManager.Current.ZoomLevel = clamped;
                    FlairX_Mod_Manager.SettingsManager.Save();
                    
                    // Update zoom indicator in main window
                    var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.UpdateZoomIndicator(clamped);
                    }
                }
            }
        }

        public void ResetZoom()
        {
            ZoomFactor = 1.0;
        }

        private void ApplyScalingToContainer(GridViewItem container, FrameworkElement root)
        {
            if (Math.Abs(ZoomFactor - 1.0) < 0.001) // At 100% zoom
            {
                // Remove transform completely at 100% to match original state
                root.RenderTransform = null;
                
                // Clear container size to let it auto-size naturally
                container.ClearValue(FrameworkElement.WidthProperty);
                container.ClearValue(FrameworkElement.HeightProperty);
            }
            else
            {
                // Apply ScaleTransform for other zoom levels
                var scaleTransform = new ScaleTransform
                {
                    ScaleX = ZoomFactor,
                    ScaleY = ZoomFactor,
                    CenterX = _baseTileSize / 2,
                    CenterY = (_baseTileSize + _baseDescHeight) / 2
                };
                
                root.RenderTransform = scaleTransform;
                container.Width = _baseTileSize * ZoomFactor + (24 * ZoomFactor);
                container.Height = (_baseTileSize + _baseDescHeight) * ZoomFactor + (24 * ZoomFactor);
            }
        }

        private async void UpdateGridItemSizes()
        {
            // Use ScaleTransform approach instead of manual resizing
            if (ModsGrid != null)
            {
                // Update WrapGrid ItemWidth/ItemHeight for proportional layout
                if (ModsGrid.ItemsPanelRoot is WrapGrid wrapGrid)
                {
                    if (Math.Abs(ZoomFactor - 1.0) < 0.001) // At 100% zoom
                    {
                        // Reset to original auto-sizing at 100%
                        wrapGrid.ClearValue(WrapGrid.ItemWidthProperty);
                        wrapGrid.ClearValue(WrapGrid.ItemHeightProperty);
                    }
                    else
                    {
                        var scaledMargin = 24 * ZoomFactor;
                        wrapGrid.ItemWidth = _baseTileSize * ZoomFactor + scaledMargin;
                        wrapGrid.ItemHeight = (_baseTileSize + _baseDescHeight) * ZoomFactor + scaledMargin;
                    }
                }

                // ONLY update visible containers, not all 1100+ items!
                // This is the key fix for zoom scroll performance
                var items = ModsGrid.Items.ToList();
                var batchSize = 20; // Process 20 items at a time
                
                for (int i = 0; i < items.Count; i += batchSize)
                {
                    // Yield to UI thread every batch to keep scroll responsive
                    await Task.Yield();
                    
                    var batchEnd = Math.Min(i + batchSize, items.Count);
                    for (int j = i; j < batchEnd; j++)
                    {
                        var item = items[j];
                        var container = ModsGrid.ContainerFromItem(item) as GridViewItem;
                        if (container?.ContentTemplateRoot is FrameworkElement root)
                        {
                            ApplyScalingToContainer(container, root);
                        }
                    }
                }

                ModsGrid.InvalidateArrange();
                ModsGrid.UpdateLayout();
                
                // Force extents recalculation for zoom - fixes wheel event routing
                ModsGrid.InvalidateMeasure();
                if (ModsScrollViewer != null)
                {
                    ModsScrollViewer.InvalidateScrollInfo();
                    ModsScrollViewer.UpdateLayout();
                }
            }
        }

        private void ModsGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            
            // Apply scaling to ALL newly generated containers, not just when zoom != 1.0
            if (args.ItemContainer is GridViewItem container)
            {
                container.Loaded += (s, e) => 
                {
                    if (container.ContentTemplateRoot is FrameworkElement root)
                    {
                        ApplyScalingToContainer(container, root);
                    }
                };
            }
        }

        private void ModsScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // Early exit if zoom is disabled - don't even check modifiers
            if (!FlairX_Mod_Manager.SettingsManager.Current.ModGridZoomEnabled)
                return;
            
            // Only handle zoom if Ctrl is pressed
            if ((e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) == Windows.System.VirtualKeyModifiers.Control)
            {
                var properties = e.GetCurrentPoint(ModsScrollViewer).Properties;
                var delta = properties.MouseWheelDelta;
                
                var oldZoom = _zoomFactor;
                if (delta > 0)
                {
                    ZoomFactor += 0.05; // 5% step
                }
                else if (delta < 0)
                {
                    ZoomFactor -= 0.05; // 5% step
                }
                
                if (oldZoom != _zoomFactor)
                {
                    e.Handled = true;
                }
            }
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            // Handle Ctrl+0 for zoom reset if zoom is enabled
            if (e.Key == Windows.System.VirtualKey.Number0 && 
                (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down &&
                FlairX_Mod_Manager.SettingsManager.Current.ModGridZoomEnabled)
            {
                ResetZoom();
                e.Handled = true;
                return;
            }
            
            base.OnKeyDown(e);
        }
    }
}