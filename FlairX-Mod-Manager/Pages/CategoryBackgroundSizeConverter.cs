using Microsoft.UI.Xaml.Data;
using System;

namespace FlairX_Mod_Manager.Pages
{
    public class CategoryBackgroundSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isCategory && isCategory)
            {
                // For categories, return smaller size (single button)
                return parameter?.ToString() switch
                {
                    "width" => 48.0,
                    "height" => 48.0,
                    _ => 48.0
                };
            }
            else
            {
                // For mods, return original size (three buttons)
                return parameter?.ToString() switch
                {
                    "width" => 48.0,
                    "height" => 124.0,
                    _ => 124.0
                };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}