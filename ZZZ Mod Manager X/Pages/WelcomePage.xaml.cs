using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            this.InitializeComponent();
            UpdateTexts();
        }

        private Dictionary<string, string> _lang = new();

        private void LoadLanguage()
        {
            _lang = SharedUtilities.LoadLanguageDictionary();
        }

        private string T(string key)
        {
            return SharedUtilities.GetTranslation(_lang, key);
        }

        private void UpdateTexts()
        {
            LoadLanguage();
            WelcomeText.Text = T("Welcome_Title");
            SelectGameText.Text = T("Welcome_SelectGame");
        }
    }
}