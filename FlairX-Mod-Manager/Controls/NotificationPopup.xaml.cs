using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading;

namespace FlairX_Mod_Manager.Controls
{
    public enum NotificationSeverity
    {
        Success,
        Warning,
        Error,
        Info
    }

    public sealed partial class NotificationPopup : UserControl
    {
        private Timer? _autoCloseTimer;
        private Window? _parentWindow;

        public NotificationPopup()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Attach to a window so the popup can position itself correctly
        /// </summary>
        public void AttachToWindow(Window window)
        {
            _parentWindow = window;
        }

        public void Show(string message, NotificationSeverity severity = NotificationSeverity.Info, int autoCloseMs = 0)
        {
            // Set icon and color based on severity
            switch (severity)
            {
                case NotificationSeverity.Success:
                    SeverityIcon.Glyph = "\uE73E"; // Checkmark
                    SeverityIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80));
                    break;
                case NotificationSeverity.Warning:
                    SeverityIcon.Glyph = "\uE7BA"; // Warning
                    SeverityIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 193, 7));
                    break;
                case NotificationSeverity.Error:
                    SeverityIcon.Glyph = "\uEA39"; // Error badge
                    SeverityIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54));
                    break;
                default:
                    SeverityIcon.Glyph = "\uE946"; // Info
                    SeverityIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243));
                    break;
            }

            MessageText.Text = message;

            // Position centered at top of window
            UpdatePosition();

            // Cancel previous timer
            _autoCloseTimer?.Dispose();
            _autoCloseTimer = null;

            // Fade in
            PopupBorder.Opacity = 0;
            PopupRoot.IsOpen = true;
            FadeIn();

            if (autoCloseMs > 0)
            {
                _autoCloseTimer = new Timer(_ =>
                {
                    DispatcherQueue.TryEnqueue(() => FadeOut(() =>
                    {
                        PopupRoot.IsOpen = false;
                    }));
                }, null, autoCloseMs, Timeout.Infinite);
            }
        }

        public void Close()
        {
            _autoCloseTimer?.Dispose();
            _autoCloseTimer = null;
            FadeOut(() => PopupRoot.IsOpen = false);
        }

        private void FadeIn()
        {
            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0, To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, PopupBorder);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Opacity");
            sb.Children.Add(anim);
            sb.Begin();
        }

        private void FadeOut(Action onComplete)
        {
            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1, To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
            };
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, PopupBorder);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Opacity");
            sb.Children.Add(anim);
            sb.Completed += (s, e) => onComplete();
            sb.Begin();
        }

        private void UpdatePosition()
        {
            if (_parentWindow == null) return;

            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_parentWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                var windowWidth = appWindow.Size.Width;

                // Measure popup width after layout
                PopupBorder.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                var popupWidth = PopupBorder.DesiredSize.Width;
                if (popupWidth < 1) popupWidth = 300; // fallback

                // Center horizontally, 16px from top
                PopupRoot.HorizontalOffset = (windowWidth - popupWidth) / 2.0;
                PopupRoot.VerticalOffset = 16;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"NotificationPopup: Failed to position popup: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
