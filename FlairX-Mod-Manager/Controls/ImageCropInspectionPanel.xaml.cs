using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using SharpImage = SixLabors.ImageSharp.Image;

namespace FlairX_Mod_Manager.Controls
{
    /// <summary>
    /// Item for batch crop processing
    /// </summary>
    public class BatchCropItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string ImageType { get; set; } = "";
        public BitmapImage? Thumbnail { get; set; }
        public Image<Rgba32>? SourceImage { get; set; }
        public SixLabors.ImageSharp.Rectangle InitialCropRect { get; set; }
        public SixLabors.ImageSharp.Rectangle? CropRect { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public int TargetIndex { get; set; }
        public bool IsProtected { get; set; }
        public CropAction Action { get; set; } = CropAction.Confirm;
        
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(ItemBorderBrush)); OnPropertyChanged(nameof(ItemBorderThickness)); }
        }
        
        private bool _isEdited;
        public bool IsEdited 
        { 
            get => _isEdited;
            set { _isEdited = value; OnPropertyChanged(nameof(IsEdited)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusColor)); }
        }
        
        public string StatusText 
        { 
            get 
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                return Action == CropAction.Delete ? 
                    (SharedUtilities.GetTranslation(lang, "CropPanel_Status_WillBeDeleted") ?? "Will be deleted") : 
                    Action == CropAction.Skip ? 
                    (SharedUtilities.GetTranslation(lang, "CropPanel_Status_NoCrop") ?? "No crop") :
                    IsEdited ? 
                    (SharedUtilities.GetTranslation(lang, "CropPanel_Status_Edited") ?? "Edited") : 
                    (SharedUtilities.GetTranslation(lang, "CropPanel_Status_Pending") ?? "Pending");
            }
        }
        public string StatusIcon => Action == CropAction.Delete ? "\uE74D" : 
                                    Action == CropAction.Skip ? "\uE8BB" :
                                    IsEdited ? "\uE73E" : "\uE8B7";
        public SolidColorBrush StatusColor => Action == CropAction.Delete ? new SolidColorBrush(Microsoft.UI.Colors.Red) :
                                              Action == CropAction.Skip ? new SolidColorBrush(Microsoft.UI.Colors.Orange) :
                                              IsEdited ? new SolidColorBrush(Microsoft.UI.Colors.Green) : 
                                              new SolidColorBrush(Microsoft.UI.Colors.Gray);
        
        // Border for selected item highlight
        public SolidColorBrush ItemBorderBrush => IsSelected 
            ? (SolidColorBrush)Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        public Thickness ItemBorderThickness => IsSelected ? new Thickness(2) : new Thickness(1);
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class ImageCropInspectionPanel : UserControl
    {

        private bool _isInitialized = false;

        public event EventHandler? CloseRequested;

        public ImageCropInspectionPanel()
        {
            this.InitializeComponent();
            this.Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Called when panel is unloaded - cancel any pending operation
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelOperation();
        }

        /// <summary>
        /// Cancel the current operation (called when panel is closed without explicit action)
        /// </summary>
        public void CancelOperation()
        {
            // Don't cancel if user is navigating back to file selection
            if (_choosingDifferentFile) return;

            // Only set result if not already completed
            if (_completionSource != null && !_completionSource.Task.IsCompleted)
            {
                _completionSource.SetResult(new CropResult { Action = CropAction.Cancel, CropRectangle = _cropRect });
            }
            
            if (_batchCompletionSource != null && !_batchCompletionSource.Task.IsCompleted)
            {
                _batchCompletionSource.SetResult(null); // Return null to indicate cancellation
            }
        }

        private void OnLayoutUpdated(object? sender, object e)
        {
            if (!_isInitialized && ImageCanvas.ActualWidth > 0 && ImageCanvas.ActualHeight > 0)
            {
                _isInitialized = true;
                // Unsubscribe immediately to prevent multiple calls
                try { this.LayoutUpdated -= OnLayoutUpdated; } catch { }
                
                // Delay slightly to ensure transforms are ready
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateCropOverlay();
                });
            }
        }
    }

    public enum CropAction
    {
        Confirm,    // Proceed with cropping and optimization
        Skip,       // Skip optimization, only rename file
        Delete,     // Delete/remove file completely
        Cancel,     // Cancel entire optimization process
        ChooseDifferentFile // Go back to file selection panel
    }

    public class CropResult
    {
        public CropAction Action { get; set; }
        public SixLabors.ImageSharp.Rectangle CropRectangle { get; set; }
    }

    public class BatchCropResult
    {
        public string FilePath { get; set; } = "";
        public string ImageType { get; set; } = "";
        public CropAction Action { get; set; }
        public SixLabors.ImageSharp.Rectangle CropRectangle { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public int TargetIndex { get; set; }
    }
}
