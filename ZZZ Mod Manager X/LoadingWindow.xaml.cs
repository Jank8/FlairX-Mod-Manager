using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;

namespace ZZZ_Mod_Manager_X
{
    public sealed partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            this.InitializeComponent();
            
            // Set window properties
            this.Title = "ZZZ Mod Manager X - Loading";
            
            // Configure window
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            
            if (appWindow != null)
            {
                // Set window size - larger to fit content properly
                appWindow.Resize(new Windows.Graphics.SizeInt32(500, 250));
                
                // Center the window
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centerX = (displayArea.WorkArea.Width - 500) / 2;
                    var centerY = (displayArea.WorkArea.Height - 250) / 2;
                    appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
                }
                
                // Set window properties
                appWindow.SetIcon("app.ico");
                
                // Configure title bar
                if (appWindow.TitleBar != null)
                {
                    appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                }
            }
        }
        
        public void UpdateStatus(string status)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = status;
            });
        }
        
        public void SetProgress(double value)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                LoadingProgressBar.IsIndeterminate = false;
                LoadingProgressBar.Value = value;
            });
        }
        
        public void SetIndeterminate(bool isIndeterminate)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                LoadingProgressBar.IsIndeterminate = isIndeterminate;
            });
        }
    }
}