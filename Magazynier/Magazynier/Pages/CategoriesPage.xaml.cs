using System;
using System.Collections.Generic;
using Magazynier.Controls;
using Magazynier.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Magazynier.Pages
{
    public sealed partial class CategoriesPage : Page
    {
        public CategoriesPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ApplyLocalization();
            LoadCategories();
        }

        private void ApplyLocalization()
        {
            PageTitle.Text = LocalizationService.Get("Categories_Title");
            AddButtonText.Text = LocalizationService.Get("Categories_Add");
            ColName.Text = LocalizationService.Get("Categories_Name");
            ColDesc.Text = LocalizationService.Get("Categories_Description");
            EmptyStateText.Text = LocalizationService.Get("Categories_NoCategories");
        }

        private void LoadCategories()
        {
            var cats = DatabaseService.GetCategories();
            if (cats.Count == 0)
            {
                CategoriesList.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                CategoriesList.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
            }
            CategoriesList.ItemsSource = cats;
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.CategoryEditDialog(null) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                LoadCategories();
                MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Saved"), NotificationSeverity.Success);
            }
        }

        private async void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is AssetCategory cat)
            {
                var dialog = new Dialogs.CategoryEditDialog(cat) { XamlRoot = XamlRoot };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    LoadCategories();
                    MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Saved"), NotificationSeverity.Success);
                }
            }
        }

        private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is AssetCategory cat)
            {
                if (DatabaseService.CategoryHasAssets(cat.Id))
                {
                    var errDialog = new ContentDialog
                    {
                        Title = LocalizationService.Get("Error_Title"),
                        Content = LocalizationService.Get("Categories_HasAssets_Error"),
                        CloseButtonText = LocalizationService.Get("Dialog_OK"),
                        XamlRoot = XamlRoot
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = LocalizationService.Get("Categories_DeleteConfirm_Title"),
                    Content = LocalizationService.Get("Categories_DeleteConfirm_Message", cat.Name),
                    PrimaryButtonText = LocalizationService.Get("Dialog_Delete"),
                    CloseButtonText = LocalizationService.Get("Dialog_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    DatabaseService.DeleteCategory(cat.Id);
                    LoadCategories();
                    MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Deleted"), NotificationSeverity.Success);
                }
            }
        }
    }
}
