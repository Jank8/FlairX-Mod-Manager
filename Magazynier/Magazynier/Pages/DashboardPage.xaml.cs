using System;
using System.Collections.Generic;
using System.Linq;
using Magazynier.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Magazynier.Pages
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ApplyLocalization();
            LoadData();
        }

        private void ApplyLocalization()
        {
            PageTitle.Text = LocalizationService.Get("Dashboard_Title");
            StatTotalLabel.Text = LocalizationService.Get("Dashboard_TotalAssets");
            StatAvailableLabel.Text = LocalizationService.Get("Dashboard_Available");
            StatAssignedLabel.Text = LocalizationService.Get("Dashboard_Assigned");
            StatInRepairLabel.Text = LocalizationService.Get("Dashboard_InRepair");
            StatRetiredLabel.Text = LocalizationService.Get("Dashboard_Retired");
            RecentAssignmentsTitle.Text = LocalizationService.Get("Dashboard_RecentAssignments");
            NoRecentText.Text = LocalizationService.Get("Dashboard_NoRecentAssignments");
        }

        private void LoadData()
        {
            var (total, available, assigned, inRepair, retired) = DatabaseService.GetAssetStats();
            StatTotal.Text = total.ToString();
            StatAvailable.Text = available.ToString();
            StatAssigned.Text = assigned.ToString();
            StatInRepair.Text = inRepair.ToString();
            StatRetired.Text = retired.ToString();

            var recentAssignments = DatabaseService.GetAssignments(activeOnly: false)
                .Take(10)
                .Select(a => new RecentAssignmentViewModel
                {
                    AssetName = a.AssetName ?? "",
                    AssetSerial = a.AssetSerial ?? "",
                    UserName = a.UserName ?? "",
                    DecisionNumber = a.DecisionNumber,
                    AssignedAtFormatted = a.AssignedAt.ToString("dd.MM.yyyy"),
                })
                .ToList();

            if (recentAssignments.Count == 0)
            {
                RecentAssignmentsList.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                NoRecentText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                RecentAssignmentsList.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                NoRecentText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                RecentAssignmentsList.ItemsSource = recentAssignments;
            }
        }

        private class RecentAssignmentViewModel
        {
            public string AssetName { get; set; } = "";
            public string AssetSerial { get; set; } = "";
            public string UserName { get; set; } = "";
            public string DecisionNumber { get; set; } = "";
            public string AssignedAtFormatted { get; set; } = "";
        }
    }
}
