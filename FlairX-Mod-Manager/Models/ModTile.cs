using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;

namespace FlairX_Mod_Manager.Pages
{
    public class ModTile : INotifyPropertyChanged
    {
        public ModTile()
        {
            // Initialize translations
            var langDict = SharedUtilities.LoadLanguageDictionary();
            _activateText = SharedUtilities.GetTranslation(langDict, "ModTile_Activate");
            _deactivateText = SharedUtilities.GetTranslation(langDict, "ModTile_Deactivate");
            _openDirectoryText = SharedUtilities.GetTranslation(langDict, "ModTile_OpenDirectory");
        }
        
        private string _name = "";
        public string Name 
        { 
            get => _name; 
            set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } 
        }
        
        private string _directory = "";
        public string Directory 
        { 
            get => _directory; 
            set { if (_directory != value) { _directory = value; OnPropertyChanged(nameof(Directory)); } } 
        }
        
        public string ImagePath { get; set; } = "";
        public bool IsCategory { get; set; } = false; // New property to distinguish categories from mods
        public string Category { get; set; } = "";
        public string Author { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime LastChecked { get; set; } = DateTime.MinValue;
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;
        
        private bool _hasUpdate = false;
        public bool HasUpdate
        {
            get => _hasUpdate;
            set { if (_hasUpdate != value) { _hasUpdate = value; OnPropertyChanged(nameof(HasUpdate)); } }
        }
        
        public string LastCheckedFormatted => LastChecked == DateTime.MinValue ? "Never" : LastChecked.ToShortDateString();
        public string LastUpdatedFormatted => LastUpdated == DateTime.MinValue ? "Never" : LastUpdated.ToShortDateString();
        
        private BitmapImage? _imageSource;
        public BitmapImage? ImageSource
        {
            get => _imageSource;
            set { if (_imageSource != value) { _imageSource = value; OnPropertyChanged(nameof(ImageSource)); } }
        }
        
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
        }
        
        private bool _isHovered;
        public bool IsHovered
        {
            get => _isHovered;
            set { if (_isHovered != value) { _isHovered = value; OnPropertyChanged(nameof(IsHovered)); } }
        }
        
        private bool _isFolderHovered;
        public bool IsFolderHovered
        {
            get => _isFolderHovered;
            set { if (_isFolderHovered != value) { _isFolderHovered = value; OnPropertyChanged(nameof(IsFolderHovered)); } }
        }
        
        private bool _isDeleteHovered;
        public bool IsDeleteHovered
        {
            get => _isDeleteHovered;
            set { if (_isDeleteHovered != value) { _isDeleteHovered = value; OnPropertyChanged(nameof(IsDeleteHovered)); } }
        }
        
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
        }
        
        private bool _isBeingDeleted = false;
        public bool IsBeingDeleted
        {
            get => _isBeingDeleted;
            set { if (_isBeingDeleted != value) { _isBeingDeleted = value; OnPropertyChanged(nameof(IsBeingDeleted)); } }
        }
        
        private string _activateText = "";
        public string ActivateText
        {
            get => _activateText;
            set { if (_activateText != value) { _activateText = value; OnPropertyChanged(nameof(ActivateText)); } }
        }
        
        private string _deactivateText = "";
        public string DeactivateText
        {
            get => _deactivateText;
            set { if (_deactivateText != value) { _deactivateText = value; OnPropertyChanged(nameof(DeactivateText)); } }
        }
        
        private string _openDirectoryText = "";
        public string OpenDirectoryText
        {
            get => _openDirectoryText;
            set { if (_openDirectoryText != value) { _openDirectoryText = value; OnPropertyChanged(nameof(OpenDirectoryText)); } }
        }
        
        private bool _isBroken = false;
        public bool IsBroken
        {
            get => _isBroken;
            set { if (_isBroken != value) { _isBroken = value; OnPropertyChanged(nameof(IsBroken)); } }
        }
        
        private bool _isFavorite = false;
        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite != value) { _isFavorite = value; OnPropertyChanged(nameof(IsFavorite)); } }
        }
        
        // Removed IsInViewport - using new scroll-based lazy loading instead
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
