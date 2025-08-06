using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace FlairX_Mod_Manager.Pages
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool invert = parameter?.ToString() == "False" || parameter?.ToString() == "Inverse";
            bool flag = value is bool b && b;
            if (invert) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility v)
            {
                bool invert = parameter?.ToString() == "False" || parameter?.ToString() == "Inverse";
                bool result = v == Visibility.Visible;
                return invert ? !result : result;
            }
            return false;
        }
    }
}