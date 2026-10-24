using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Shared search of the WinUI visual tree by element name.
    /// </summary>
    public static class VisualTreeSearch
    {
        public static T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is T parentElement && parentElement.Name == name)
            {
                return parentElement;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }

                var result = FindChildByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
