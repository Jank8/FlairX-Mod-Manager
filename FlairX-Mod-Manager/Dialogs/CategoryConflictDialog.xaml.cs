using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FlairX_Mod_Manager.Dialogs
{
    public sealed partial class CategoryConflictDialog : ContentDialog
    {
        private System.Collections.Generic.Dictionary<string, string> _lang = new();
        
        public bool DontAskAgain { get; private set; } = false;

        public CategoryConflictDialog(string categoryName, string newModName, List<string> activeModNames)
        {
            this.InitializeComponent();
            
            // Load language translations
            _lang = SharedUtilities.LoadLanguageDictionary();
            
            // Set localized text
            PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "CategoryConflict_Activate");
            SecondaryButtonText = SharedUtilities.GetTranslation(_lang, "CategoryConflict_Cancel");
            
            MainMessageText.Text = SharedUtilities.GetTranslation(_lang, "CategoryConflict_Message");
            CategoryLabel.Text = SharedUtilities.GetTranslation(_lang, "CategoryConflict_Category");
            ActiveModsLabel.Text = SharedUtilities.GetTranslation(_lang, "CategoryConflict_ActiveMods");
            DontAskAgainCheckBox.Content = SharedUtilities.GetTranslation(_lang, "CategoryConflict_DontAskAgain");
            
            // Set data
            CategoryNameText.Text = categoryName;
            
            // Populate active mods list
            var activeModsCollection = new ObservableCollection<string>();
            foreach (var modName in activeModNames)
            {
                activeModsCollection.Add(modName);
            }
            ActiveModsList.ItemsSource = activeModsCollection;
            
            // Handle button clicks
            PrimaryButtonClick += CategoryConflictDialog_PrimaryButtonClick;
        }
        
        private void CategoryConflictDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Save "don't ask again" preference only if user confirms activation
            DontAskAgain = DontAskAgainCheckBox.IsChecked == true;
        }
    }
}