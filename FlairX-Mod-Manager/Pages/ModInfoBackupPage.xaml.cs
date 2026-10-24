using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModInfoBackupPage : Page
    {
        private string ModLibraryPath => SharedUtilities.GetSafeXXMIModsPath();
        private const int MaxBackups = 3;

        public ModInfoBackupPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            UpdateBackupInfo();
            UpdateButtonStates();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            
            // Create backup section
            CreateBackupsTitle.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_CreateTitle");
            CreateBackupsDescription.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_CreateDescription");
            // Button text is handled by UpdateButtonStates()
            
            // Backups header
            BackupsHeader.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_BackupsHeader");
            
            // Backup titles
            RestoreBackup1Title.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Backup1Title");
            RestoreBackup2Title.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Backup2Title");
            RestoreBackup3Title.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Backup3Title");
            
            // Restore buttons - text is handled by UpdateButtonStates()
            
            // Delete button tooltips
            ToolTipService.SetToolTip(DeleteBackup1Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup2Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup3Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
        }


    }
}
