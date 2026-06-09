using Magazynier.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Magazynier.Dialogs
{
    public class UserEditDialog : ContentDialog
    {
        private readonly AppUser? _existing;

        private TextBox _firstNameBox = null!;
        private TextBox _lastNameBox = null!;
        private TextBox _departmentBox = null!;
        private TextBox _emailBox = null!;
        private TextBox _phoneBox = null!;
        private ToggleSwitch _activeToggle = null!;
        private TextBlock _errorText = null!;

        public UserEditDialog(AppUser? existing)
        {
            _existing = existing;

            Title = existing == null
                ? LocalizationService.Get("Users_Add")
                : LocalizationService.Get("Users_Edit");

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
            _firstNameBox = new TextBox { PlaceholderText = LocalizationService.Get("Users_FirstName"), Margin = new Thickness(0, 0, 0, 8) };
            _lastNameBox = new TextBox { PlaceholderText = LocalizationService.Get("Users_LastName"), Margin = new Thickness(0, 0, 0, 8) };
            _departmentBox = new TextBox { PlaceholderText = LocalizationService.Get("Users_Department"), Margin = new Thickness(0, 0, 0, 8) };
            _emailBox = new TextBox { PlaceholderText = LocalizationService.Get("Users_Email"), Margin = new Thickness(0, 0, 0, 8) };
            _phoneBox = new TextBox { PlaceholderText = LocalizationService.Get("Users_Phone"), Margin = new Thickness(0, 0, 0, 8) };
            _activeToggle = new ToggleSwitch
            {
                IsOn = true,
                OnContent = LocalizationService.Get("Users_Active"),
                OffContent = LocalizationService.Get("Users_Active"),
                Margin = new Thickness(0, 0, 0, 8)
            };

            _errorText = new TextBlock
            {
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)),
                FontSize = 12,
                Visibility = Visibility.Collapsed
            };

            var panel = new StackPanel { Width = 380, Spacing = 0 };
            panel.Children.Add(MakeLabel(LocalizationService.Get("Users_FirstName") + " *"));
            panel.Children.Add(_firstNameBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Users_LastName") + " *"));
            panel.Children.Add(_lastNameBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Users_Department")));
            panel.Children.Add(_departmentBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Users_Email")));
            panel.Children.Add(_emailBox);
            panel.Children.Add(MakeLabel(LocalizationService.Get("Users_Phone")));
            panel.Children.Add(_phoneBox);
            panel.Children.Add(_activeToggle);
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

        private void PopulateFields(AppUser u)
        {
            _firstNameBox.Text = u.FirstName;
            _lastNameBox.Text = u.LastName;
            _departmentBox.Text = u.Department ?? "";
            _emailBox.Text = u.Email ?? "";
            _phoneBox.Text = u.Phone ?? "";
            _activeToggle.IsOn = u.IsActive;
        }

        private void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _errorText.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(_firstNameBox.Text))
            {
                _errorText.Text = LocalizationService.Get("Error_RequiredField") + " (" + LocalizationService.Get("Users_FirstName") + ")";
                _errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
            if (string.IsNullOrWhiteSpace(_lastNameBox.Text))
            {
                _errorText.Text = LocalizationService.Get("Error_RequiredField") + " (" + LocalizationService.Get("Users_LastName") + ")";
                _errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            var user = _existing ?? new AppUser();
            user.FirstName = _firstNameBox.Text.Trim();
            user.LastName = _lastNameBox.Text.Trim();
            user.Department = string.IsNullOrWhiteSpace(_departmentBox.Text) ? null : _departmentBox.Text.Trim();
            user.Email = string.IsNullOrWhiteSpace(_emailBox.Text) ? null : _emailBox.Text.Trim();
            user.Phone = string.IsNullOrWhiteSpace(_phoneBox.Text) ? null : _phoneBox.Text.Trim();
            user.IsActive = _activeToggle.IsOn;

            DatabaseService.SaveUser(user);
        }
    }
}
