using System;
using System.Collections.Generic;
using System.Linq;
using Magazynier.Controls;
using Magazynier.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Magazynier.Pages
{
    public sealed partial class AssetsPage : Page
    {
        private List<AssetViewModel> _allAssets = new();
        private AssetViewModel? _selectedAsset;
        private List<AssetCategory> _categories = new();
        private bool _filtersLoading = false;

        public AssetsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ApplyLocalization();
            LoadFilters();
            LoadAssets();
        }

        private void ApplyLocalization()
        {
            PageTitle.Text = LocalizationService.Get("Assets_Title");
            AddButtonText.Text = LocalizationService.Get("Assets_Add");
            SearchBox.PlaceholderText = LocalizationService.Get("Assets_Search");
            ColName.Text = LocalizationService.Get("Assets_Name");
            ColSerial.Text = LocalizationService.Get("Assets_Serial");
            ColCategory.Text = LocalizationService.Get("Assets_Category");
            ColManufacturer.Text = LocalizationService.Get("Assets_Manufacturer");
            ColAssignedTo.Text = LocalizationService.Get("Assets_AssignedTo");
            ColDecision.Text = LocalizationService.Get("Assets_DecisionNo");
            ColStatus.Text = LocalizationService.Get("Assets_Status");
            AssignButtonText.Text = LocalizationService.Get("Assets_Assign");
            ReturnButtonText.Text = LocalizationService.Get("Assets_Return");
            EmptyStateText.Text = LocalizationService.Get("Assets_NoAssets");
        }

        private void LoadFilters()
        {
            _filtersLoading = true;
            _categories = DatabaseService.GetCategories();

            // Category filter — use a proper class so DisplayMemberPath works
            var catItems = new List<CategoryFilterItem>
            {
                new() { Id = null, Name = LocalizationService.Get("Assets_FilterCategory") }
            };
            catItems.AddRange(_categories.Select(c => new CategoryFilterItem { Id = c.Id, Name = c.Name }));
            CategoryFilter.ItemsSource = catItems;
            CategoryFilter.DisplayMemberPath = "Name";
            CategoryFilter.SelectedIndex = 0;

            // Status filter
            var statusItems = new List<StatusFilterItem>
            {
                new() { Value = null, Label = LocalizationService.Get("Assets_FilterStatus") },
                new() { Value = AssetStatus.Available, Label = LocalizationService.Get("Status_Available") },
                new() { Value = AssetStatus.Assigned, Label = LocalizationService.Get("Status_Assigned") },
                new() { Value = AssetStatus.InRepair, Label = LocalizationService.Get("Status_InRepair") },
                new() { Value = AssetStatus.Retired, Label = LocalizationService.Get("Status_Retired") },
            };
            StatusFilter.ItemsSource = statusItems;
            StatusFilter.DisplayMemberPath = "Label";
            StatusFilter.SelectedIndex = 0;
            _filtersLoading = false;
        }

        private void LoadAssets()
        {
            var search = SearchBox.Text?.Trim();
            int? catId = null;
            AssetStatus? status = null;

            if (CategoryFilter.SelectedItem is CategoryFilterItem catItem)
            {
                catId = catItem.Id;
            }
            if (StatusFilter.SelectedItem is StatusFilterItem statusItem)
            {
                status = statusItem.Value;
            }

            var assets = DatabaseService.GetAssets(
                string.IsNullOrWhiteSpace(search) ? null : search,
                catId,
                status);

            _allAssets = assets.Select(a => new AssetViewModel(a)).ToList();

            if (_allAssets.Count == 0)
            {
                AssetsList.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                AssetsList.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
            }

            AssetsList.ItemsSource = _allAssets;
            UpdateActionPanel(null);
        }

        private void UpdateActionPanel(AssetViewModel? asset)
        {
            _selectedAsset = asset;
            if (asset == null)
            {
                ActionPanel.Visibility = Visibility.Collapsed;
                return;
            }
            ActionPanel.Visibility = Visibility.Visible;
            AssignButton.IsEnabled = asset.Status != AssetStatus.Assigned;
            ReturnButton.IsEnabled = asset.Status == AssetStatus.Assigned;
        }

        // ==================== EVENTS ====================

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                LoadAssets();
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
            => LoadAssets();

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_filtersLoading) LoadAssets();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_filtersLoading) LoadAssets();
        }

        private void AssetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AssetsList.SelectedItem is AssetViewModel vm)
                UpdateActionPanel(vm);
            else
                UpdateActionPanel(null);
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.AssetEditDialog(_categories, null)
            {
                XamlRoot = XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                LoadAssets();
                MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Saved"), NotificationSeverity.Success);
            }
        }

        private async void EditAsset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is AssetViewModel vm)
            {
                var dialog = new Dialogs.AssetEditDialog(_categories, vm.Asset)
                {
                    XamlRoot = XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    LoadAssets();
                    MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Saved"), NotificationSeverity.Success);
                }
            }
        }

        private async void DeleteAsset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is AssetViewModel vm)
            {
                var dialog = new ContentDialog
                {
                    Title = LocalizationService.Get("Assets_DeleteConfirm_Title"),
                    Content = LocalizationService.Get("Assets_DeleteConfirm_Message", vm.Name),
                    PrimaryButtonText = LocalizationService.Get("Dialog_Delete"),
                    CloseButtonText = LocalizationService.Get("Dialog_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    DatabaseService.DeleteAsset(vm.Asset.Id);
                    LoadAssets();
                    MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Deleted"), NotificationSeverity.Success);
                }
            }
        }

        private async void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            var users = DatabaseService.GetUsers(activeOnly: true);
            var dialog = new Dialogs.AssignDialog(_selectedAsset.Asset, users)
            {
                XamlRoot = XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                LoadAssets();
                MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Assigned"), NotificationSeverity.Success);
            }
        }

        private async void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null || _selectedAsset.Asset.AssignedUserId == null) return;

            var dialog = new ContentDialog
            {
                Title = LocalizationService.Get("Assignments_ReturnConfirm_Title"),
                Content = LocalizationService.Get("Assignments_ReturnConfirm_Message",
                    _selectedAsset.Name, _selectedAsset.Asset.AssignedUserName ?? ""),
                PrimaryButtonText = LocalizationService.Get("Dialog_Confirm"),
                CloseButtonText = LocalizationService.Get("Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Find active assignment id
                var assignments = DatabaseService.GetAssignments(assetId: _selectedAsset.Asset.Id, activeOnly: true);
                if (assignments.Count > 0)
                {
                    DatabaseService.ReturnAsset(assignments[0].Id, _selectedAsset.Asset.Id);
                    LoadAssets();
                    MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Returned"), NotificationSeverity.Success);
                }
            }
        }

        // ==================== HELPERS ====================

        private class StatusFilterItem
        {
            public AssetStatus? Value { get; set; }
            public string Label { get; set; } = "";
        }

        private class CategoryFilterItem
        {
            public int? Id { get; set; }
            public string Name { get; set; } = "";
        }
    }

    // ViewModel wrapping Asset with display-ready properties
    public class AssetViewModel
    {
        public Asset Asset { get; }

        public AssetViewModel(Asset asset) => Asset = asset;

        public string Name => Asset.Name;
        public string? SerialNumber => Asset.SerialNumber;
        public string? InventoryNumber => Asset.InventoryNumber;
        public string? CategoryName => Asset.CategoryName;
        public string? Manufacturer => Asset.Manufacturer;
        public string? Model => Asset.Model;
        public AssetStatus Status => Asset.Status;
        public string? AssignedUserName => Asset.AssignedUserName;
        public string? AssignmentDecisionNumber => Asset.AssignmentDecisionNumber;

        public string StatusLabel => Asset.Status switch
        {
            AssetStatus.Available => LocalizationService.Get("Status_Available"),
            AssetStatus.Assigned => LocalizationService.Get("Status_Assigned"),
            AssetStatus.InRepair => LocalizationService.Get("Status_InRepair"),
            AssetStatus.Retired => LocalizationService.Get("Status_Retired"),
            _ => ""
        };

        public SolidColorBrush StatusBrush => Asset.Status switch
        {
            AssetStatus.Available => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80)),
            AssetStatus.Assigned => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243)),
            AssetStatus.InRepair => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 152, 0)),
            AssetStatus.Retired => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 158, 158)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }
}
