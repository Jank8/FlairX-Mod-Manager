using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage : Page
    {
        private double _baseTileSize = 277;
        private double _baseTileSizeWide = 592;

        private double GetCurrentBaseTileSize() => SettingsManager.Current.UseWideTileFormat ? _baseTileSizeWide : _baseTileSize;

        private void InitializeGridItemSizes()
        {
            if (ModsGrid?.ItemsPanelRoot is WrapGrid wrapGrid)
            {
                wrapGrid.ClearValue(WrapGrid.ItemWidthProperty);
                wrapGrid.ClearValue(WrapGrid.ItemHeightProperty);
            }
        }

        public void UpdateGridItemSizes()
        {
            if (ModsGrid == null) return;
            if (ModsGrid.ItemsPanelRoot is WrapGrid wrapGrid)
            {
                wrapGrid.ClearValue(WrapGrid.ItemWidthProperty);
                wrapGrid.ClearValue(WrapGrid.ItemHeightProperty);
            }
            ModsGrid.InvalidateArrange();
            ModsGrid.UpdateLayout();
            if (ModsScrollViewer != null)
            {
                ModsScrollViewer.InvalidateScrollInfo();
                ModsScrollViewer.UpdateLayout();
            }
        }
    }
}
