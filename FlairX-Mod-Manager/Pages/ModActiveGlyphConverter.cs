using Microsoft.UI.Xaml.Data;
using System;

namespace FlairX_Mod_Manager.Pages
{
    public class ModActiveGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // value to IsActive (bool)
            return (value is bool b && b) ? "\uEB52" : "\uEB51";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value?.ToString() == "\uEB52";
        }
    }
}
