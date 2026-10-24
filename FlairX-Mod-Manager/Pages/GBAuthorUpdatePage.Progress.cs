using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GBAuthorUpdatePage
    {

        // Thread-safe progress reporting
        private static readonly object _lockObject = new();
        private static event Action? ProgressChanged;

        private static void NotifyProgressChanged()
        {
            lock (_lockObject)
            {
                ProgressChanged?.Invoke();
            }
        }

        private void OnProgressChanged()
        {
            DispatcherQueue.TryEnqueue(UpdateProgressBarUI);
        }

        // Thread-safe static fields with proper locking
        private static volatile bool _isFetchingVersions = false;
        private static volatile bool _isFetchingAllPreviews = false;
        private static volatile bool _isFetchingMissingPreviews = false;
        private static int _success = 0, _fail = 0, _skip = 0;
        private static List<string> _skippedMods = new();
        private static List<string> _failedMods = new();
        private static string _currentProcessingMod = "";
        private static int _totalMods = 0;
        private static double _progressValue = 0;
        private static int _processedCount = 0;
        private static int _totalCount = 0;
        
        public static bool IsUpdatingAuthors 
        { 
            get { lock (_lockObject) { return _isFetchingVersions; } }
            private set { lock (_lockObject) { _isFetchingVersions = value; } }
        }
        
        public static double ProgressValue 
        { 
            get { lock (_lockObject) { return _progressValue; } }
            private set { lock (_lockObject) { _progressValue = value; } }
        }
        
        private static void SafeIncrementSuccess()
        {
            lock (_lockObject) { _success++; }
        }
        
        private static void SafeIncrementFail()
        {
            lock (_lockObject) { _fail++; }
        }
        
        private static void SafeIncrementSkip()
        {
            lock (_lockObject) { _skip++; }
        }
        
        private static void SafeAddSkippedMod(string mod)
        {
            lock (_lockObject) { _skippedMods.Add(mod); }
        }
        
        private static void SafeAddFailedMod(string mod)
        {
            lock (_lockObject) { _failedMods.Add(mod); }
        }
        
        private static void SafeSetCurrentProcessingMod(string modName)
        {
            lock (_lockObject)
            {
                _currentProcessingMod = modName;
            }
        }

        /// <summary>
        /// <summary>
        /// Update button states - disable all buttons when any operation is running
        /// </summary>
        private void UpdateButtonStates()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            
            // Check if any operation is currently running
            bool anyOperationRunning = _isFetchingVersions || 
                                     _isFetchingAllPreviews || _isFetchingMissingPreviews || _isCreatingCategoryFolders;
            
            // Update All Data button (formerly Update Authors)
            if (UpdateButton != null && UpdateButtonText != null)
            {
                if (_isFetchingVersions)
                {
                    UpdateButton.IsEnabled = true; // Keep enabled so user can cancel
                    UpdateButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    UpdateButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    UpdateButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
            
            // Fetch All Previews button
            if (FetchAllPreviewsButton != null && FetchAllPreviewsButtonText != null)
            {
                if (_isFetchingAllPreviews)
                {
                    FetchAllPreviewsButton.IsEnabled = true; // Keep enabled so user can cancel
                    FetchAllPreviewsButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    FetchAllPreviewsButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    FetchAllPreviewsButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
            
            // Fetch Missing Previews button
            if (FetchMissingPreviewsButton != null && FetchMissingPreviewsButtonText != null)
            {
                if (_isFetchingMissingPreviews)
                {
                    FetchMissingPreviewsButton.IsEnabled = true; // Keep enabled so user can cancel
                    FetchMissingPreviewsButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    FetchMissingPreviewsButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    FetchMissingPreviewsButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
            
            // Create Category Folders button
            if (CreateCategoryFoldersButton != null && CreateCategoryFoldersButtonText != null)
            {
                if (_isCreatingCategoryFolders)
                {
                    CreateCategoryFoldersButton.IsEnabled = true; // Keep enabled so user can cancel
                    CreateCategoryFoldersButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    CreateCategoryFoldersButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    CreateCategoryFoldersButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
        }

        private void UpdateProgressBarUI()
        {
            // Update button states - disable all buttons when any operation is running
            UpdateButtonStates();
            
            if (UpdateProgressBar != null)
            {
                if (_isFetchingVersions)
                {
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (UpdateProgressStatusText != null)
                    {
                        UpdateProgressStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            UpdateProgressStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            UpdateProgressStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    UpdateProgressBar.Value = 0;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (UpdateProgressStatusText != null)
                    {
                        UpdateProgressStatusText.Visibility = Visibility.Collapsed;
                        UpdateProgressStatusText.Text = "";
                    }
                }
            }
            
            if (FetchAllPreviewsProgressBar != null)
            {
                if (_isFetchingAllPreviews)
                {
                    FetchAllPreviewsProgressBar.Visibility = Visibility.Visible;
                    FetchAllPreviewsProgressBar.IsIndeterminate = false;
                    FetchAllPreviewsProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (FetchAllPreviewsStatusText != null)
                    {
                        FetchAllPreviewsStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            FetchAllPreviewsStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            FetchAllPreviewsStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    FetchAllPreviewsProgressBar.Value = 0;
                    FetchAllPreviewsProgressBar.IsIndeterminate = false;
                    FetchAllPreviewsProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (FetchAllPreviewsStatusText != null)
                    {
                        FetchAllPreviewsStatusText.Visibility = Visibility.Collapsed;
                        FetchAllPreviewsStatusText.Text = "";
                    }
                }
            }
            
            if (FetchMissingPreviewsProgressBar != null)
            {
                if (_isFetchingMissingPreviews)
                {
                    FetchMissingPreviewsProgressBar.Visibility = Visibility.Visible;
                    FetchMissingPreviewsProgressBar.IsIndeterminate = false;
                    FetchMissingPreviewsProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (FetchMissingPreviewsStatusText != null)
                    {
                        FetchMissingPreviewsStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            FetchMissingPreviewsStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            FetchMissingPreviewsStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    FetchMissingPreviewsProgressBar.Value = 0;
                    FetchMissingPreviewsProgressBar.IsIndeterminate = false;
                    FetchMissingPreviewsProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (FetchMissingPreviewsStatusText != null)
                    {
                        FetchMissingPreviewsStatusText.Visibility = Visibility.Collapsed;
                        FetchMissingPreviewsStatusText.Text = "";
                    }
                }
            }
            
            if (CreateCategoryFoldersProgressBar != null)
            {
                if (_isCreatingCategoryFolders)
                {
                    CreateCategoryFoldersProgressBar.Visibility = Visibility.Visible;
                    CreateCategoryFoldersProgressBar.IsIndeterminate = false;
                    CreateCategoryFoldersProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (CreateCategoryFoldersStatusText != null)
                    {
                        CreateCategoryFoldersStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            CreateCategoryFoldersStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            CreateCategoryFoldersStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    CreateCategoryFoldersProgressBar.Value = 0;
                    CreateCategoryFoldersProgressBar.IsIndeterminate = false;
                    CreateCategoryFoldersProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (CreateCategoryFoldersStatusText != null)
                    {
                        CreateCategoryFoldersStatusText.Visibility = Visibility.Collapsed;
                        CreateCategoryFoldersStatusText.Text = "";
                    }
                }
            }
        }
    }
}
