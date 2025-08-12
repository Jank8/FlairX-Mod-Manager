using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace FlairX_Mod_Manager.Pages
{
    public class CategoryButtonVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isCategory)
            {
                string? param = parameter as string;
                
                // For categories, hide activate and delete buttons, show only folder button
                if (isCategory)
                {
                    return param == "folder" ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    // For mods, show all buttons
                    return Visibility.Visible;
                }
            }
            
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}