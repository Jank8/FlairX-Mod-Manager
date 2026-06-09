using System;
using System.Collections.Generic;
using System.Linq;
using Magazynier.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magazynier.Dialogs
{
    /// <summary>
    /// Dialog for adding or editing an Asset. Built entirely in code-behind to avoid
    /// extra XAML files for simple forms.
    /// </summary>
    public class AssetEditDialog : ContentDialog
    {
        private readonly Asset? _existing;
        private readonly List<AssetCategory> _categories;

        private TextBox _nameBox = null!;
        private TextBox _serialBox = null!;
        private TextBox _inventoryBox = null!;
        private ComboBox _categoryCombo = null!;
        private TextBox _manufacturerBox = null!;
        private TextBox _modelBox = null!;
        private TextBox _descriptionBox = null!;
        private ComboBox _statusCombo = null!;
        private DatePicker _purchaseDatePicker = null!;
        private TextBlock _errorText = null!;

        public AssetEditDialog(List<AssetCategory> categories, Asset? existing)
        {
            _categories = categories;
            _existing = existing;

            Title = existing == null
                ? LocalizationService.Get("Assets_Add")
                : LocalizationService.Get("Assets_Edit");

            PrimaryButtonText = LocalizationService.Get("Dialog_Save");
            CloseButtonText = LocalizationService.Get("Dialog_Cancel");
            DefaultButton = ContentDialogButton.Primary;

            Content = BuildContent();

            if (existing != null)
                PopulateFields(existing);

            PrimaryButtonClick += OnSave;
        }

        private UIElement BuildContent()
        {
            _nameBox = new TextBox { PlaceholderText = LocalizationService.Get("Assets_Name"), Margin = new Thickness(0, 0, 0, 8) };
            _serialBox = new TextBox { PlaceholderText = LocalizationService.Get("Assets_Serial"), Margin = new Thickness(0, 0, 0, 8) };
            _inventoryBox = new TextBox { PlaceholderText = LocalizationService.Get("Assets_Inventory"), Margin = new Thickness(0, 0, 0, 8) };

            _categoryCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 8),
                PlaceholderText = LocalizationService.Get("Assets_Category"),
                ItemsSource = _categories,
                DisplayMemberPath = "Name"
            };

            _manufacturerBox = new TextBox { PlaceholderText = LocalizationService.Get("Assets_Manufacturer"), Margin = new Thickness(0, 0, 0, 8) };
            _modelBox = new TextBox { PlaceholderText = LocalizationService.Get("Assets_Model"), Margin = new Thickness(0, 0, 0, 8) };
            _descriptionBox = new TextBox
            {
                PlaceholderText = LocalizationService.Get("Assets_Description"),
                AcceptsReturn = true,
                Height = 72,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var statusItems = new List<StatusItem>
            {
                new() { Value = AssetStatus.Available, Label = LocalizationService.Get("Status_Available") },
                new() { Value = AssetStatus.Assigned,  Label = LocalizationService.Get("Status_Assigned") },
                new() { Value = AssetStatus.InRepair,  Label = LocalizationService.Get("Status_InRepair") },
                new() { Value = AssetStatus.Retired,   Label = LocalizationService.Get("Status_Retired") },
            };
            _statusCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 8),
                ItemsSource = statusItems,
                DisplayMemberPath = "Label",
                SelectedIndex = 0
            };

            _purchaseDatePicker = new DatePicker
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _errorText = new TextBlock
            {
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)),
                FontSize = 12,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var panel = new StackPanel { Width = 420, Spacing = 0 };
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Name") + " *"));
            panel.Children.Add(_nameBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Category") + " *"));
            panel.Children.Add(_categoryCombo);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Serial")));
            panel.Children.Add(_serialBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Inventory")));
            panel.Children.Add(_inventoryBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Manufacturer")));
            panel.Children.Add(_manufacturerBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Model")));
            panel.Children.Add(_modelBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Status")));
            panel.Children.Add(_statusCombo);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_PurchaseDate")));
            panel.Children.Add(_purchaseDatePicker);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assets_Description")));
            panel.Children.Add(_descriptionBox);
            panel.Children.Add(_errorText);

            return new ScrollViewer
            {
                Content = panel,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        }

        private static TextBlock MakeLabel(string text) => new TextBlock
        {
            Text = text,
            FontSize = 12,
            Opacity = 0.7,
            Margin = new Thickness(0, 4, 0, 4)
        };

        private void PopulateFields(Asset a)
        {
            _nameBox.Text = a.Name;
            _serialBox.Text = a.SerialNumber ?? "";
            _inventoryBox.Text = a.InventoryNumber ?? "";
            _categoryCombo.SelectedItem = _categories.FirstOrDefault(c => c.Id == a.CategoryId);
            _manufacturerBox.Text = a.Manufacturer ?? "";
            _modelBox.Text = a.Model ?? "";
            _descriptionBox.Text = a.Description ?? "";

            var statusItems = _statusCombo.ItemsSource as List<StatusItem>;
            _statusCombo.SelectedItem = statusItems?.FirstOrDefault(s => s.Value == a.Status);

            if (a.PurchaseDate.HasValue)
                _purchaseDatePicker.Date = new DateTimeOffset(a.PurchaseDate.Value);
        }

        private void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _errorText.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                _errorText.Text = LocalizationService.Get("Error_RequiredField") + " (" + LocalizationService.Get("Assets_Name") + ")";
                _errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
            if (_categoryCombo.SelectedItem == null)
            {
                _errorText.Text = LocalizationService.Get("Error_RequiredField") + " (" + LocalizationService.Get("Assets_Category") + ")";
                _errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            var asset = _existing ?? new Asset();
            asset.Name = _nameBox.Text.Trim();
            asset.SerialNumber = string.IsNullOrWhiteSpace(_serialBox.Text) ? null : _serialBox.Text.Trim();
            asset.InventoryNumber = string.IsNullOrWhiteSpace(_inventoryBox.Text) ? null : _inventoryBox.Text.Trim();
            asset.CategoryId = (_categoryCombo.SelectedItem as AssetCategory)!.Id;
            asset.Manufacturer = string.IsNullOrWhiteSpace(_manufacturerBox.Text) ? null : _manufacturerBox.Text.Trim();
            asset.Model = string.IsNullOrWhiteSpace(_modelBox.Text) ? null : _modelBox.Text.Trim();
            asset.Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim();
            asset.Status = (_statusCombo.SelectedItem as StatusItem)?.Value ?? AssetStatus.Available;

            if (_purchaseDatePicker.Date != default(DateTimeOffset))
                asset.PurchaseDate = _purchaseDatePicker.Date.DateTime;

            DatabaseService.SaveAsset(asset);
        }

        private class StatusItem
        {
            public AssetStatus Value { get; set; }
            public string Label { get; set; } = "";
        }
    }
}
