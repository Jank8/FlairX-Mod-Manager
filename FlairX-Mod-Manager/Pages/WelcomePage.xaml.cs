using Microsoft.UI.Xaml.Controls;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            this.InitializeComponent();
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            WelcomeText.Text = SharedUtilities.GetTranslation(lang, "Welcome_Title");
            SelectGameText.Text = SharedUtilities.GetTranslation(lang, "Welcome_SelectGame");
        }
    }
}