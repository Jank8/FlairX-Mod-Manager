using System;
using System.Collections.Generic;
using Magazynier.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magazynier.Dialogs
{
    /// <summary>
    /// Dialog for assigning an asset to a user with a decision number.
    /// </summary>
    public class AssignDialog : ContentDialog
    {
        private readonly Asset _asset;
        private readonly List<AppUser> _users;

        private ComboBox _userCombo = null!;
        private TextBox _decisionBox = null!;
        private TextBox _notesBox = null!;
        private DatePicker _datePicker = null!;
        private TextBlock _errorText = null!;

        public AssignDialog(Asset asset, List<AppUser> users)
        {
            _asset = asset;
            _users = users;

            Title = LocalizationService.Get("Assign_Title");
            PrimaryButtonText = LocalizationService.Get("Assign_Confirm");
            CloseButtonText = LocalizationService.Get("Dialog_Cancel");
            DefaultButton = ContentDialogButton.Primary;

            Content = BuildContent();
            PrimaryButtonClick += OnAssign;
        }

        private UIElement BuildContent()
        {
            // Asset info header
            var assetInfo = new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(30, 33, 150, 243)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var assetInfoStack = new StackPanel();
            assetInfoStack.Children.Add(new TextBlock
            {
                Text = _asset.Name,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14
            });
            if (!string.IsNullOrEmpty(_asset.SerialNumber))
            {
                assetInfoStack.Children.Add(new TextBlock
                {
                    Text = _asset.SerialNumber,
                    FontSize = 12,
                    Opacity = 0.7
                });
            }
            assetInfo.Child = assetInfoStack;

            _userCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 8),
                PlaceholderText = LocalizationService.Get("Assign_SelectUser"),
                ItemsSource = _users,
                DisplayMemberPath = "FullName"
            };

            _decisionBox = new TextBox
            {
                PlaceholderText = LocalizationService.Get("Assign_DecisionNo_Placeholder"),
                Margin = new Thickness(0, 0, 0, 8)
            };

            _datePicker = new DatePicker
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Date = DateTimeOffset.Now,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _notesBox = new TextBox
            {
                PlaceholderText = LocalizationService.Get("Assign_Notes"),
                AcceptsReturn = true,
                Height = 64,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _errorText = new TextBlock
            {
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)),
                FontSize = 12,
                Visibility = Visibility.Collapsed
            };

            var panel = new StackPanel { Width = 400, Spacing = 0 };
            panel.Children.Add(assetInfo);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assign_SelectUser") + " *"));
            panel.Children.Add(_userCombo);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assign_DecisionNo") + " *"));
            panel.Children.Add(_decisionBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assign_Date")));
            panel.Children.Add(_datePicker);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Assign_Notes")));
            panel.Children.Add(_notesBox);
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

        private void OnAssign(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _errorText.Visibility = Visibility.Collapsed;

            if (_userCombo.SelectedItem == null)
            {
                _errorText.Text = LocalizationService.Get("Error_RequiredField") + " (" + LocalizationService.Get("Assign_SelectUser") + ")";
                _errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
            if (string.IsNullOrWhiteSpace(_decisionBox.Text))
            {
                _errorText.Text = LocalizationService.Get("Error_RequiredField") + " (" + LocalizationService.Get("Assign_DecisionNo") + ")";
                _errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            var assignment = new Assignment
            {
                AssetId = _asset.Id,
                UserId = (_userCombo.SelectedItem as AppUser)!.Id,
                DecisionNumber = _decisionBox.Text.Trim(),
                AssignedAt = _datePicker.Date.DateTime,
                Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim()
            };

            DatabaseService.AssignAsset(assignment);
        }
    }
}
