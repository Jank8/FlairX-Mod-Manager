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
    public sealed partial class UsersPage : Page
    {
        private List<AppUser> _allUsers = new();

        public UsersPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ApplyLocalization();
            LoadUsers();
        }

        private void ApplyLocalization()
        {
            PageTitle.Text = LocalizationService.Get("Users_Title");
            AddButtonText.Text = LocalizationService.Get("Users_Add");
            SearchBox.PlaceholderText = LocalizationService.Get("Users_Search");
            ColName.Text = LocalizationService.Get("Assets_Name");
            ColDept.Text = LocalizationService.Get("Users_Department");
            ColEmail.Text = LocalizationService.Get("Users_Email");
            ColPhone.Text = LocalizationService.Get("Users_Phone");
            ColActive.Text = LocalizationService.Get("Users_Active");
            EmptyStateText.Text = LocalizationService.Get("Users_NoUsers");
        }

        private void LoadUsers(string? search = null)
        {
            _allUsers = DatabaseService.GetUsers();

            var filtered = string.IsNullOrWhiteSpace(search)
                ? _allUsers
                : _allUsers.Where(u =>
                    u.FullName.Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    (u.Department?.Contains(search, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (u.Email?.Contains(search, System.StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

            var vms = filtered.Select(u => new UserViewModel(u)).ToList();

            if (vms.Count == 0)
            {
                UsersList.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                UsersList.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
            }

            UsersList.ItemsSource = vms;
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                LoadUsers(sender.Text);
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
            => LoadUsers(sender.Text);

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.UserEditDialog(null) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                LoadUsers();
                MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Saved"), NotificationSeverity.Success);
            }
        }

        private async void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is UserViewModel vm)
            {
                var dialog = new Dialogs.UserEditDialog(vm.User) { XamlRoot = XamlRoot };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    LoadUsers();
                    MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Saved"), NotificationSeverity.Success);
                }
            }
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is UserViewModel vm)
            {
                if (DatabaseService.UserHasActiveAssignments(vm.User.Id))
                {
                    var errDialog = new ContentDialog
                    {
                        Title = LocalizationService.Get("Error_Title"),
                        Content = LocalizationService.Get("Users_HasAssignments_Error"),
                        CloseButtonText = LocalizationService.Get("Dialog_OK"),
                        XamlRoot = XamlRoot
                    };
                    await errDialog.ShowAsync();
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = LocalizationService.Get("Users_DeleteConfirm_Title"),
                    Content = LocalizationService.Get("Users_DeleteConfirm_Message", vm.User.FullName),
                    PrimaryButtonText = LocalizationService.Get("Dialog_Delete"),
                    CloseButtonText = LocalizationService.Get("Dialog_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    DatabaseService.DeleteUser(vm.User.Id);
                    LoadUsers();
                    MainWindow.Instance?.ShowNotification(LocalizationService.Get("Success_Deleted"), NotificationSeverity.Success);
                }
            }
        }
    }

    public class UserViewModel
    {
        public AppUser User { get; }
        public UserViewModel(AppUser user) => User = user;

        public string FullName => User.FullName;
        public string? Department => User.Department;
        public string? Email => User.Email;
        public string? Phone => User.Phone;
        public bool IsActive => User.IsActive;

        public string ActiveGlyph => User.IsActive ? "\uE73E" : "\uE711";
        public SolidColorBrush ActiveBrush => User.IsActive
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 158, 158));
    }
}
