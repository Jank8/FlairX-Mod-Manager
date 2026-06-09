using System;
using System.Collections.Generic;
using System.Linq;
using Magazynier.Controls;
using Magazynier.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Magazynier.Pages
{
    public sealed partial class AssignmentsPage : Page
    {
        private List<AssignmentViewModel> _allItems = new();

        public AssignmentsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ApplyLocalization();
            LoadAssignments();
        }

        private void ApplyLocalization()
        {
            PageTitle.Text = LocalizationService.Get("Assignments_Title");
            ShowActiveText.Text = LocalizationService.Get("Assignments_Active");
            ShowHistoryText.Text = LocalizationService.Get("Assignments_History");
            SearchBox.PlaceholderText = LocalizationService.Get("Users_Search");
            ColAsset.Text = LocalizationService.Get("Assignments_Asset");
            ColUser.Text = LocalizationService.Get("Assignments_User");
            ColDecision.Text = LocalizationService.Get("Assignments_DecisionNo");
            ColAssignedAt.Text = LocalizationService.Get("Assignments_AssignedAt");
            ColReturnedAt.Text = LocalizationService.Get("Assignments_ReturnedAt");
            EmptyStateText.Text = LocalizationService.Get("Assignments_NoAssignments");
        }

        private void LoadAssignments()
        {
            // ShowActive = only active (no return date), ShowHistory = all
            bool activeOnly = ShowHistoryToggle.IsChecked != true;
            var search = SearchBox.Text?.Trim();

            var assignments = DatabaseService.GetAssignments(activeOnly: activeOnly);

            if (!string.IsNullOrWhiteSpace(search))
            {
                assignments = assignments.Where(a =>
                    (a.AssetName?.Contains(search, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.UserName?.Contains(search, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
                    a.DecisionNumber.Contains(search, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }

            _allItems = assignments.Select(a => new AssignmentViewModel(a)).ToList();

            if (_allItems.Count == 0)
            {
                AssignmentsList.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                AssignmentsList.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
            }

            AssignmentsList.ItemsSource = _allItems;
        }

        private void FilterToggle_Changed(object sender, RoutedEventArgs e)
            => LoadAssignments();

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                LoadAssignments();
        }

        private async void ReturnAsset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is AssignmentViewModel vm)
            {
                var dialog = new ContentDialog
                {
                    Title = LocalizationService.Get("Assignments_ReturnConfirm_Title"),
                    Content = LocalizationService.Get("Assignments_ReturnConfirm_Message",
                        vm.AssetName, vm.UserName),
                    PrimaryButtonText = LocalizationService.Get("Dialog_Confirm"),
                    CloseButtonText = LocalizationService.Get("Dialog_Cancel"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    DatabaseService.ReturnAsset(vm.Assignment.Id, vm.Assignment.AssetId);
                    LoadAssignments();
                    MainWindow.Instance?.ShowNotification(
                        LocalizationService.Get("Success_Returned"), NotificationSeverity.Success);
                }
            }
        }
    }

    public class AssignmentViewModel
    {
        public Assignment Assignment { get; }
        public AssignmentViewModel(Assignment a) => Assignment = a;

        public string AssetName => Assignment.AssetName ?? "";
        public string AssetSerial => Assignment.AssetSerial ?? "";
        public string UserName => Assignment.UserName ?? "";
        public string DecisionNumber => Assignment.DecisionNumber;
        public string AssignedAtFormatted => Assignment.AssignedAt.ToString("dd.MM.yyyy");
        public string ReturnedAtFormatted => Assignment.ReturnedAt?.ToString("dd.MM.yyyy") ?? "—";
        public Visibility ReturnButtonVisibility =>
            Assignment.IsActive ? Visibility.Visible : Visibility.Collapsed;
    }
}
