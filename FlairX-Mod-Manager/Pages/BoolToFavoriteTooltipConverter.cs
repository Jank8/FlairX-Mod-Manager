using Microsoft.UI.Xaml.Data;
using System;

namespace FlairX_Mod_Manager.Pages
{
    public class BoolToFavoriteTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isFavorite = value is bool b && b;
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            if (isFavorite)
            {
                return SharedUtilities.GetTranslation(lang, "RemoveFromFavorites_Tooltip");
            }
            else
            {
                return SharedUtilities.GetTranslation(lang, "AddToFavorites_Tooltip");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}