using Microsoft.UI.Xaml.Controls;

namespace ZZZ_Mod_Manager_X.Pages
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
            WelcomeText.Text = LanguageManager.Instance.T("Welcome_Title");
            SelectGameText.Text = LanguageManager.Instance.T("Welcome_SelectGame");
        }
    }
}