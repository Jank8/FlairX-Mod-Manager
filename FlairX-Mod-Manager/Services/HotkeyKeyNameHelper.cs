using System;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Formats a virtual key exactly the same way in every hotkey editor.
    /// </summary>
    public static class HotkeyKeyNameHelper
    {
        public static string ConvertVirtualKeyToString(Windows.System.VirtualKey key)
        {
            return key switch
            {
                >= Windows.System.VirtualKey.A and <= Windows.System.VirtualKey.Z => key.ToString(),
                >= Windows.System.VirtualKey.Number0 and <= Windows.System.VirtualKey.Number9 => ((int)key - (int)Windows.System.VirtualKey.Number0).ToString(),
                >= Windows.System.VirtualKey.NumberPad0 and <= Windows.System.VirtualKey.NumberPad9 => "NUM " + ((int)key - (int)Windows.System.VirtualKey.NumberPad0),
                >= Windows.System.VirtualKey.F1 and <= Windows.System.VirtualKey.F12 => "F" + ((int)key - (int)Windows.System.VirtualKey.F1 + 1),
                Windows.System.VirtualKey.Up => "↑",
                Windows.System.VirtualKey.Down => "↓",
                Windows.System.VirtualKey.Left => "←",
                Windows.System.VirtualKey.Right => "→",
                Windows.System.VirtualKey.Space => "SPACE",
                Windows.System.VirtualKey.Enter => "ENTER",
                Windows.System.VirtualKey.Tab => "TAB",
                Windows.System.VirtualKey.Escape => "ESC",
                Windows.System.VirtualKey.Back => "BACKSPACE",
                Windows.System.VirtualKey.Delete => "DEL",
                Windows.System.VirtualKey.Insert => "INS",
                Windows.System.VirtualKey.Home => "HOME",
                Windows.System.VirtualKey.End => "END",
                Windows.System.VirtualKey.PageUp => "PAGE UP",
                Windows.System.VirtualKey.PageDown => "PAGE DOWN",
                _ => key.ToString().ToUpper()
            };
        }
    }
}
