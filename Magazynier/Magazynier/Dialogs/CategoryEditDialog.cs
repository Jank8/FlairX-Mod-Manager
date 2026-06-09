using System.Collections.Generic;
using Magazynier.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magazynier.Dialogs
{
    public class CategoryEditDialog : ContentDialog
    {
        private readonly AssetCategory? _existing;

        private TextBox _nameBox = null!;
        private TextBox _descriptionBox = null!;
        private ComboBox _iconCombo = null!;
        private TextBlock _errorText = null!;

        // Common device icons (glyph → friendly name)
        private static readonly List<IconOption> Icons = new()
        {
            new() { Glyph = "\uE7F8", Name = "Komputer / Computer" },
            new() { Glyph = "\uE7F4", Name = "Monitor" },
            new() { Glyph = "\uE765", Name = "Klawiatura / Keyboard" },
            new() { Glyph = "\uE962", Name = "Myszka / Mouse" },
            new() { Glyph = "\uE717", Name = "Telefon / Phone" },
            new() { Glyph = "\uE749", Name = "Drukarka / Printer" },
            new() { Glyph = "\uE8A7", Name = "Tablet" },
            new() { Glyph = "\uE7C3", Name = "Inne / Other" },
            new() { Glyph = "\uE8B7", Name = "Serwer / Server" },
            new() { Glyph = "\uE774", Name = "Sieć / Network" },
            new() { Glyph = "\uE7EF", Name = "Kamera / Camera" },
            new() { Glyph = "\uE90F", Name = "Narzędzia / Tools" },
        };

        public CategoryEditDialog(AssetCategory? existing)
        {
            _existing = existing;

            Title = existing == null
                ? LocalizationService.Get("Categories_Add")
                : LocalizationService.Get("Categories_Edit");

            PrimaryButtonText = LocalizationService.Get("Dialog_Save");
            CloseButtonText = LocalizationService.Get("Dialog_Cancel");
            DefaultButton = ContentDialogButton.Primary;

            Content = BuildContent();

            if (existing != null)
                PopulateFields(existing);
            else
                _iconCombo.SelectedIndex = 0;

            PrimaryButtonClick += OnSave;
        }

        private UIElement BuildContent()
        {
            _nameBox = new TextBox
            {
                PlaceholderText = LocalizationService.Get("Categories_Name"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            _descriptionBox = new TextBox
            {
                PlaceholderText = LocalizationService.Get("Categories_Description"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            _iconCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 8),
                ItemsSource = Icons,
                DisplayMemberPath = "Name"
            };

            _errorText = new TextBlock
            {
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)),
                FontSize = 12,
                Visibility = Visibility.Collapsed
            };

            var panel = new StackPanel { Width = 360, Spacing = 0 };
            panel.Children.Add(MakeLabel(LocalizationService.Get("Categories_Name") + " *"));
            panel.Children.Add(_nameBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Categories_Description")));
            panel.Children.Add(_descriptionBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Categories_Icon")));
            panel.Children.Add(_iconCombo);
            panel.Children.Add(_errorText);

            return panel;
        }

        private static TextBlock MakeLabel(string text) => new TextBlock
        {
            Text = text,
            FontSize = 12,
            Opacity = 0.7,
            Margin = new Thickness(0, 4, 0, 4)
        };

        private void PopulateFields(AssetCategory cat)
        {
            _nameBox.Text = cat.Name;
            _descriptionBox.Text = cat.Description ?? "";
            var match = Icons.FindIndex(i => i.Glyph == cat.IconGlyph);
            _iconCombo.SelectedIndex = match >= 0 ? match : 0;
        }

        private void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _errorText.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                _errorText.Text = LocalizationService.Get("Error_RequiredField");
                _errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            var cat = _existing ?? new AssetCategory();
            cat.Name = _nameBox.Text.Trim();
            cat.Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim();
            cat.IconGlyph = (_iconCombo.SelectedItem as IconOption)?.Glyph ?? "\uE7C3";

            DatabaseService.SaveCategory(cat);
        }

        private class IconOption
        {
            public string Glyph { get; set; } = "";
            public string Name { get; set; } = "";
        }
    }
}
