using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FlairX_Mod_Manager.Dialogs
{
    public class CategoryDeleteDialog
    {
        public static async System.Threading.Tasks.Task<bool> ShowAsync(string categoryName, int modCount, XamlRoot? xamlRoot = null)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            // Get XamlRoot from App.MainWindow if not provided
            if (xamlRoot == null)
            {
                var app = Application.Current as App;
                xamlRoot = app?.MainWindow?.Content?.XamlRoot;
            }
            
            var dialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "CategoryDelete_Title"),
                PrimaryButtonText = SharedUtilities.GetTranslation(lang, "CategoryDelete_Delete"),
                CloseButtonText = SharedUtilities.GetTranslation(lang, "CategoryDelete_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            };

            var stackPanel = new StackPanel { Spacing = 16 };

            // Warning message
            var warningText = new TextBlock
            {
                Text = string.Format(
                    SharedUtilities.GetTranslation(lang, "CategoryDelete_Warning"),
                    categoryName,
                    modCount
                ),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(warningText);

            // Confirmation checkbox
            var confirmCheckBox = new CheckBox
            {
                Content = SharedUtilities.GetTranslation(lang, "CategoryDelete_Confirm")
            };
            stackPanel.Children.Add(confirmCheckBox);

            dialog.Content = stackPanel;

            // Disable primary button initially
            dialog.IsPrimaryButtonEnabled = false;

            // Enable primary button only when checkbox is checked
            confirmCheckBox.Checked += (s, e) => dialog.IsPrimaryButtonEnabled = true;
            confirmCheckBox.Unchecked += (s, e) => dialog.IsPrimaryButtonEnabled = false;

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary && confirmCheckBox.IsChecked == true;
        }
    }
}
