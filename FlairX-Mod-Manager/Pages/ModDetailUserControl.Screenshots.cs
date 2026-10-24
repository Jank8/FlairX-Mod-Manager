using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModDetailUserControl
    {

        private async void DeletePreviewButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                e.Handled = true;
                if (_availablePreviewImages.Count == 0 || _currentImageIndex < 0 || _currentImageIndex >= _availablePreviewImages.Count) return;

                var currentImagePath = _availablePreviewImages[_currentImageIndex];
                var fileName = Path.GetFileName(currentImagePath);

                // Show confirmation dialog
                var lang = SharedUtilities.LoadLanguageDictionary();
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "ModDetailPage_DeletePreview_Title"),
                    Content = string.Format(SharedUtilities.GetTranslation(lang, "ModDetailPage_DeletePreview_Content"), fileName),
                    PrimaryButtonText = SharedUtilities.GetTranslation(lang, "ModDetailPage_DeletePreview_Confirm"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "ModDetailPage_DeletePreview_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        // Clear image source before deleting
                        ModImage.Source = null;

                        // Small delay to ensure file handle is released
                        await Task.Delay(50);

                        // Delete the file
                        if (File.Exists(currentImagePath))
                        {
                            File.Delete(currentImagePath);
                            Logger.LogInfo($"Deleted preview image: {currentImagePath}");
                        }

                        // Remove from list
                        _availablePreviewImages.RemoveAt(_currentImageIndex);

                        // Adjust index if needed
                        if (_availablePreviewImages.Count == 0)
                        {
                            _currentImageIndex = 0;
                        }
                        else if (_currentImageIndex >= _availablePreviewImages.Count)
                        {
                            _currentImageIndex = _availablePreviewImages.Count - 1;
                        }

                        // Reload current image and update navigation
                        LoadCurrentImage();
                        UpdateImageNavigation();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete preview image: {currentImagePath}", ex);

                        // Show error dialog
                        if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.DeletePreviewButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.DeletePreviewButton_Click failed", ex);
            }
        }
        
        private void DeletePreviewButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // No animation needed
        }
        
        private void DeletePreviewButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // No animation needed
        }

        private async void CaptureScreenshotButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                e.Handled = true;

                try
                {
                    // Get screenshot directory from settings or use default
                    string screenshotDir = SettingsManager.Current.ScreenshotCaptureDirectory;
                    if (string.IsNullOrEmpty(screenshotDir))
                    {
                        screenshotDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "Screenshots");
                    }
                    else
                    {
                        // Expand environment variables like %USERPROFILE%
                        screenshotDir = Environment.ExpandEnvironmentVariables(screenshotDir);
                    }

                    // Ensure directory exists
                    if (!Directory.Exists(screenshotDir))
                    {
                        Directory.CreateDirectory(screenshotDir);
                    }

                    // Start screenshot capture mode with crop panel
                    await StartScreenshotCaptureMode(screenshotDir);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error starting screenshot capture mode", ex);

                    var lang = SharedUtilities.LoadLanguageDictionary();
                    if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.CaptureScreenshotButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.CaptureScreenshotButton_Click failed", ex);
            }
        }
        
        private void CaptureScreenshotButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // No animation needed
        }
        
        private void CaptureScreenshotButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // No animation needed
        }

        private async Task StartScreenshotCaptureMode(string screenshotDirectory)
        {
            if (string.IsNullOrEmpty(_currentModDirectory))
            {
                Logger.LogWarning("Cannot start screenshot capture - no mod directory");
                return;
            }
            
            try
            {
                // Get MainWindow instance
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                if (mainWindow == null)
                {
                    Logger.LogError("Cannot access MainWindow for screenshot capture");
                    return;
                }
                
                // Open screenshot capture crop panel
                var capturedFiles = await mainWindow.OpenScreenshotCaptureCropPanel(_currentModDirectory, screenshotDirectory);
                
                if (capturedFiles != null && capturedFiles.Count > 0)
                {
                    Logger.LogInfo($"Screenshot capture completed with {capturedFiles.Count} files");
                    
                    // Add small delay to ensure crop panel dialogs are fully closed
                    await Task.Delay(300);
                    
                    // Run optimization after all dialogs are closed
                    Logger.LogInfo($"Running optimization for {capturedFiles.Count} captured screenshots");
                    
                    // Create optimization context for screenshot capture
                    var context = Services.ImageOptimizationService.GetOptimizationContext(Services.OptimizationTrigger.Manual);
                    context.AllowUIInteraction = true; // Allow UI interaction for minitile selection
                    context.InspectAndEditEnabled = false; // No crop inspection for preview files
                    context.Mode = FlairX_Mod_Manager.Models.OptimizationMode.Standard;
                    context.CreateMinitile = true; // Create minitile (will show selection dialog unless AutoCreateModThumbnails is enabled)
                    
                    await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(_currentModDirectory, context);
                    Logger.LogInfo("Screenshot optimization completed successfully");
                    
                    // Reload mods to refresh thumbnails
                    if (App.Current is App app && app.MainWindow is MainWindow mw)
                    {
                        await mw.ReloadModsAsync();
                    }
                }
                else
                {
                    // Show cancellation message to user
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var cancelledMessage = SharedUtilities.GetTranslation(lang, "ScreenshotCapture_Cancelled") ?? "Screenshot capture was cancelled and captured files were deleted";
                    Logger.LogInfo(cancelledMessage);
                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(cancelledMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in screenshot capture mode", ex);
                throw;
            }
        }
    }
}
