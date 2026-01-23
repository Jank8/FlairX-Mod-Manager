using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FlairX_Mod_Manager.Dialogs
{
    public sealed partial class CategoryConflictDialog : ContentDialog
    {
        private System.Collections.Generic.Dictionary<string, string> _lang = new();

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
            
            // Set data
            CategoryNameText.Text = categoryName;
            
            // Populate active mods list
            var activeModsCollection = new ObservableCollection<string>();
            foreach (var modName in activeModNames)
            {
                activeModsCollection.Add(modName);
            }
            ActiveModsList.ItemsSource = activeModsCollection;
        }
    }
}