using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using System;

namespace FlairX_Mod_Manager
{
    public static class HotkeyIconHelper
    {
        // Kenney Input Prompts font paths - skopiowane z ModDetailUserControl
        private const string KenneyXboxFont = "ms-appx:///Assets/KenneyFonts/kenney_input_xbox_series.ttf#Kenney Input Xbox Series";
        private const string KenneyPlayStationFont = "ms-appx:///Assets/KenneyFonts/kenney_input_playstation_series.ttf#Kenney Input PlayStation Series";
        private const string KenneySwitchFont = "ms-appx:///Assets/KenneyFonts/kenney_input_nintendo_switch.ttf#Kenney Input Nintendo Switch";
        private const string KenneyKeyboardFont = "ms-appx:///Assets/KenneyFonts/kenney_input_keyboard_mouse.ttf#Kenney Input Keyboard & Mouse";
        private const string KenneyGenericFont = "ms-appx:///Assets/KenneyFonts/kenney_input_generic.ttf#Kenney Input Generic";
        private const string KenneySteamDeckFont = "ms-appx:///Assets/KenneyFonts/kenney_input_steam_deck.ttf#Kenney Input Steam Deck";
        
        public static readonly Dictionary<string, (string Glyph, string Font)> HotkeyIconMap = new()
        {
            // Xbox Series (kenney_input_xbox_series font)
            ["XB A"] = ("\uE004", KenneyXboxFont),
            ["XB B"] = ("\uE006", KenneyXboxFont),
            ["XB X"] = ("\uE01E", KenneyXboxFont),
            ["XB Y"] = ("\uE020", KenneyXboxFont),
            ["XB A Color"] = ("\uE00C", KenneyXboxFont),
            ["XB B Color"] = ("\uE00E", KenneyXboxFont),
            ["XB X Color"] = ("\uE010", KenneyXboxFont),
            ["XB Y Color"] = ("\uE012", KenneyXboxFont),
            ["XB LB"] = ("\uE043", KenneyXboxFont),
            ["XB RB"] = ("\uE049", KenneyXboxFont),
            ["XB LT"] = ("\uE047", KenneyXboxFont),
            ["XB RT"] = ("\uE04D", KenneyXboxFont),
            ["XB LS"] = ("\uE045", KenneyXboxFont),
            ["XB RS"] = ("\uE04B", KenneyXboxFont),
            ["XB Start"] = ("\uE018", KenneyXboxFont),
            ["XB Back"] = ("\uE008", KenneyXboxFont),
            ["XB Menu"] = ("\uE014", KenneyXboxFont),
            ["XB View"] = ("\uE01C", KenneyXboxFont),
            ["XB Home"] = ("\uE041", KenneyXboxFont),
            ["XB Guide"] = ("\uE041", KenneyXboxFont),
            ["XB Share"] = ("\uE016", KenneyXboxFont),
            ["XB ↑"] = ("\uE036", KenneyXboxFont),
            ["XB ↓"] = ("\uE025", KenneyXboxFont),
            ["XB ←"] = ("\uE029", KenneyXboxFont),
            ["XB →"] = ("\uE02C", KenneyXboxFont),
            ["XB DPAD"] = ("\uE022", KenneyXboxFont),
            ["XB ↑↓"] = ("\uE037", KenneyXboxFont),
            ["XB ←→"] = ("\uE026", KenneyXboxFont),
            ["XB L↑"] = ("\uE055", KenneyXboxFont),
            ["XB L↓"] = ("\uE050", KenneyXboxFont),
            ["XB L←"] = ("\uE052", KenneyXboxFont),
            ["XB L→"] = ("\uE054", KenneyXboxFont),
            ["XB R↑"] = ("\uE05D", KenneyXboxFont),
            ["XB R↓"] = ("\uE058", KenneyXboxFont),
            ["XB R←"] = ("\uE05A", KenneyXboxFont),
            ["XB R→"] = ("\uE05C", KenneyXboxFont),
            ["XB L CLICK"] = ("\uE053", KenneyXboxFont),
            ["XB R CLICK"] = ("\uE05B", KenneyXboxFont),
            ["XB P1"] = ("\uE043", KenneyXboxFont), // Paddle (using LB icon)
            ["XB P2"] = ("\uE043", KenneyXboxFont), // Paddle (using LB icon)
            ["XB P3"] = ("\uE049", KenneyXboxFont), // Paddle (using RB icon)
            ["XB P4"] = ("\uE049", KenneyXboxFont), // Paddle (using RB icon)
            
            // PlayStation Series (kenney_input_playstation_series font)
            ["PS ×"] = ("\uE049", KenneyPlayStationFont),
            ["PS ○"] = ("\uE03F", KenneyPlayStationFont),
            ["PS □"] = ("\uE04F", KenneyPlayStationFont),
            ["PS △"] = ("\uE051", KenneyPlayStationFont),
            ["PS × Color"] = ("\uE043", KenneyPlayStationFont),
            ["PS ○ Color"] = ("\uE041", KenneyPlayStationFont),
            ["PS □ Color"] = ("\uE045", KenneyPlayStationFont),
            ["PS △ Color"] = ("\uE047", KenneyPlayStationFont),
            ["PS L1"] = ("\uE076", KenneyPlayStationFont),
            ["PS R1"] = ("\uE07E", KenneyPlayStationFont),
            ["PS L2"] = ("\uE07A", KenneyPlayStationFont),
            ["PS R2"] = ("\uE082", KenneyPlayStationFont),
            ["PS L3"] = ("\uE04B", KenneyPlayStationFont),
            ["PS R3"] = ("\uE04D", KenneyPlayStationFont),
            ["PS Share"] = ("\uE00B", KenneyPlayStationFont),
            ["PS Options"] = ("\uE022", KenneyPlayStationFont),
            ["PS Touchpad"] = ("\uE030", KenneyPlayStationFont),
            ["PS4 Options"] = ("\uE009", KenneyPlayStationFont),
            ["PS Home"] = ("\uE087", KenneyKeyboardFont), // PS button (outline for gamepad)
            ["PS5 Create"] = ("\uE01C", KenneyPlayStationFont),
            ["PS5 Options"] = ("\uE022", KenneyPlayStationFont),
            ["PS5 Mute"] = ("\uE020", KenneyPlayStationFont),
            ["PS ↑"] = ("\uE05F", KenneyPlayStationFont),
            ["PS ↓"] = ("\uE056", KenneyPlayStationFont),
            ["PS ←"] = ("\uE05A", KenneyPlayStationFont),
            ["PS →"] = ("\uE05D", KenneyPlayStationFont),
            ["PS DPAD"] = ("\uE053", KenneyPlayStationFont),
            ["PS ↑↓"] = ("\uE060", KenneyPlayStationFont),
            ["PS ←→"] = ("\uE057", KenneyPlayStationFont),
            ["PS L↑"] = ("\uE068", KenneyPlayStationFont),
            ["PS L↓"] = ("\uE063", KenneyPlayStationFont),
            ["PS L←"] = ("\uE065", KenneyPlayStationFont),
            ["PS L→"] = ("\uE067", KenneyPlayStationFont),
            ["PS R↑"] = ("\uE070", KenneyPlayStationFont),
            ["PS R↓"] = ("\uE06B", KenneyPlayStationFont),
            ["PS R←"] = ("\uE06D", KenneyPlayStationFont),
            ["PS R→"] = ("\uE06F", KenneyPlayStationFont),
            ["PS L CLICK"] = ("\uE066", KenneyPlayStationFont),
            ["PS R CLICK"] = ("\uE06E", KenneyPlayStationFont),
            
            // Nintendo Switch (kenney_input_nintendo_switch font)
            ["NIN A"] = ("\uE004", KenneySwitchFont),
            ["NIN B"] = ("\uE006", KenneySwitchFont),
            ["NIN X"] = ("\uE018", KenneySwitchFont),
            ["NIN Y"] = ("\uE01A", KenneySwitchFont),
            ["NIN L"] = ("\uE00A", KenneySwitchFont),
            ["NIN R"] = ("\uE010", KenneySwitchFont),
            ["NIN ZL"] = ("\uE01C", KenneySwitchFont),
            ["NIN ZR"] = ("\uE01E", KenneySwitchFont),
            ["NIN +"] = ("\uE00E", KenneySwitchFont),
            ["NIN -"] = ("\uE00C", KenneySwitchFont),
            ["NIN Home"] = ("\uE087", KenneyKeyboardFont), // outline for gamepad
            ["NIN ↑"] = ("\uE03C", KenneySwitchFont),
            ["NIN ↓"] = ("\uE033", KenneySwitchFont),
            ["NIN ←"] = ("\uE037", KenneySwitchFont),
            ["NIN →"] = ("\uE03A", KenneySwitchFont),
            ["NIN DPAD"] = ("\uE031", KenneySwitchFont),
            ["NIN ↑↓"] = ("\uE03E", KenneySwitchFont),
            ["NIN ←→"] = ("\uE035", KenneySwitchFont),
            ["NIN SL"] = ("\uE012", KenneySwitchFont),
            ["NIN SR"] = ("\uE014", KenneySwitchFont),
            ["NIN L↑"] = ("\uE060", KenneySwitchFont),
            ["NIN L↓"] = ("\uE05B", KenneySwitchFont),
            ["NIN L←"] = ("\uE05D", KenneySwitchFont),
            ["NIN L→"] = ("\uE05F", KenneySwitchFont),
            ["NIN R↑"] = ("\uE068", KenneySwitchFont),
            ["NIN R↓"] = ("\uE063", KenneySwitchFont),
            ["NIN R←"] = ("\uE065", KenneySwitchFont),
            ["NIN R→"] = ("\uE067", KenneySwitchFont),
            ["NIN L CLICK"] = ("\uE05E", KenneySwitchFont),
            ["NIN R CLICK"] = ("\uE066", KenneySwitchFont),
            ["NIN Capture"] = ("\uE016", KenneySwitchFont),
            
            // Generic gamepad (kenney_input_generic font)
            ["GP STICK"] = ("\uE01B", KenneyGenericFont), ["GP STICK↑"] = ("\uE022", KenneyGenericFont), ["GP STICK↓"] = ("\uE01C", KenneyGenericFont),
            ["GP STICK←"] = ("\uE01E", KenneyGenericFont), ["GP STICK→"] = ("\uE020", KenneyGenericFont), ["GP STICK CLICK"] = ("\uE01F", KenneyGenericFont),
            ["GP STICK↑↓"] = ("\uE023", KenneyGenericFont), ["GP STICK←→"] = ("\uE01D", KenneyGenericFont),
            ["GP BTN"] = ("\uE000", KenneyGenericFont), ["GP BTN CIRCLE"] = ("\uE001", KenneyGenericFont), ["GP BTN SQUARE"] = ("\uE007", KenneyGenericFont),
            ["GP TRIGGER A"] = ("\uE00A", KenneyGenericFont), ["GP TRIGGER B"] = ("\uE00D", KenneyGenericFont), ["GP TRIGGER C"] = ("\uE010", KenneyGenericFont),
            ["GP JOYSTICK"] = ("\uE013", KenneyGenericFont), ["GP JOYSTICK←"] = ("\uE015", KenneyGenericFont), ["GP JOYSTICK→"] = ("\uE01A", KenneyGenericFont),
            
            // Steam Deck (kenney_input_steam_deck font)
            ["SD A"] = ("\uE001", KenneySteamDeckFont), ["SD B"] = ("\uE003", KenneySteamDeckFont), ["SD X"] = ("\uE01D", KenneySteamDeckFont), ["SD Y"] = ("\uE01F", KenneySteamDeckFont),
            ["SD L1"] = ("\uE007", KenneySteamDeckFont), ["SD R1"] = ("\uE013", KenneySteamDeckFont), ["SD L2"] = ("\uE009", KenneySteamDeckFont), ["SD R2"] = ("\uE015", KenneySteamDeckFont),
            ["SD L4"] = ("\uE00B", KenneySteamDeckFont), ["SD R4"] = ("\uE017", KenneySteamDeckFont), ["SD L5"] = ("\uE00D", KenneySteamDeckFont), ["SD R5"] = ("\uE019", KenneySteamDeckFont),
            ["SD Guide"] = ("\uE005", KenneySteamDeckFont), ["SD Options"] = ("\uE00F", KenneySteamDeckFont), ["SD View"] = ("\uE01B", KenneySteamDeckFont), ["SD Quick"] = ("\uE011", KenneySteamDeckFont),
            ["SD ↑"] = ("\uE02C", KenneySteamDeckFont), ["SD ↓"] = ("\uE023", KenneySteamDeckFont), ["SD ←"] = ("\uE027", KenneySteamDeckFont), ["SD →"] = ("\uE02A", KenneySteamDeckFont),
            ["SD DPAD"] = ("\uE021", KenneySteamDeckFont), ["SD ↑↓"] = ("\uE02E", KenneySteamDeckFont), ["SD ←→"] = ("\uE025", KenneySteamDeckFont),
            ["SD L↑"] = ("\uE036", KenneySteamDeckFont), ["SD L↓"] = ("\uE031", KenneySteamDeckFont), ["SD L←"] = ("\uE033", KenneySteamDeckFont), ["SD L→"] = ("\uE035", KenneySteamDeckFont),
            ["SD R↑"] = ("\uE03E", KenneySteamDeckFont), ["SD R↓"] = ("\uE039", KenneySteamDeckFont), ["SD R←"] = ("\uE03B", KenneySteamDeckFont), ["SD R→"] = ("\uE03D", KenneySteamDeckFont),
            ["SD L CLICK"] = ("\uE034", KenneySteamDeckFont), ["SD R CLICK"] = ("\uE03C", KenneySteamDeckFont),
            ["SD TRACKPAD L"] = ("\uE04B", KenneySteamDeckFont), ["SD TRACKPAD R"] = ("\uE05E", KenneySteamDeckFont),
            
            // Keyboard & Mouse (kenney_input_keyboard_&_mouse font)
            ["↑"] = ("\uE023", KenneyKeyboardFont), ["↓"] = ("\uE01D", KenneyKeyboardFont), ["←"] = ("\uE01F", KenneyKeyboardFont), ["→"] = ("\uE021", KenneyKeyboardFont),
            ["ARROWS"] = ("\uE025", KenneyKeyboardFont),
            
            // Keyboard modifiers
            ["CTRL"] = ("\uE054", KenneyKeyboardFont), ["ALT"] = ("\uE017", KenneyKeyboardFont), ["SHIFT"] = ("\uE0BD", KenneyKeyboardFont), ["TAB"] = ("\uE0CB", KenneyKeyboardFont),
            ["ENTER"] = ("\uE05E", KenneyKeyboardFont), ["ESC"] = ("\uE062", KenneyKeyboardFont), ["SPACE"] = ("\uE0C5", KenneyKeyboardFont), ["BACKSPACE"] = ("\uE038", KenneyKeyboardFont),
            ["DEL"] = ("\uE058", KenneyKeyboardFont), ["INS"] = ("\uE08A", KenneyKeyboardFont), ["HOME"] = ("\uE086", KenneyKeyboardFont), ["END"] = ("\uE05C", KenneyKeyboardFont),
            ["PAGE UP"] = ("\uE0A7", KenneyKeyboardFont), ["PAGE DOWN"] = ("\uE0A5", KenneyKeyboardFont), ["CAPS"] = ("\uE048", KenneyKeyboardFont),
            ["FN"] = ("\uE080", KenneyKeyboardFont), ["PRINT"] = ("\uE0AD", KenneyKeyboardFont), ["NUMLOCK"] = ("\uE098", KenneyKeyboardFont),
            ["WIN"] = ("\uE0D9", KenneyKeyboardFont), ["CMD"] = ("\uE052", KenneyKeyboardFont), ["OPTION"] = ("\uE0A0", KenneyKeyboardFont),
            
            // Function keys
            ["F1"] = ("\uE067", KenneyKeyboardFont), ["F2"] = ("\uE06F", KenneyKeyboardFont), ["F3"] = ("\uE071", KenneyKeyboardFont), ["F4"] = ("\uE073", KenneyKeyboardFont),
            ["F5"] = ("\uE075", KenneyKeyboardFont), ["F6"] = ("\uE077", KenneyKeyboardFont), ["F7"] = ("\uE079", KenneyKeyboardFont), ["F8"] = ("\uE07B", KenneyKeyboardFont),
            ["F9"] = ("\uE07D", KenneyKeyboardFont), ["F10"] = ("\uE068", KenneyKeyboardFont), ["F11"] = ("\uE06A", KenneyKeyboardFont), ["F12"] = ("\uE06C", KenneyKeyboardFont),
            
            // Letters A-Z
            ["A"] = ("\uE015", KenneyKeyboardFont), ["B"] = ("\uE036", KenneyKeyboardFont), ["C"] = ("\uE046", KenneyKeyboardFont), ["D"] = ("\uE056", KenneyKeyboardFont),
            ["E"] = ("\uE05A", KenneyKeyboardFont), ["F"] = ("\uE066", KenneyKeyboardFont), ["G"] = ("\uE082", KenneyKeyboardFont), ["H"] = ("\uE084", KenneyKeyboardFont),
            ["I"] = ("\uE088", KenneyKeyboardFont), ["J"] = ("\uE08C", KenneyKeyboardFont), ["K"] = ("\uE08E", KenneyKeyboardFont), ["L"] = ("\uE090", KenneyKeyboardFont),
            ["M"] = ("\uE092", KenneyKeyboardFont), ["N"] = ("\uE096", KenneyKeyboardFont), ["O"] = ("\uE09E", KenneyKeyboardFont), ["P"] = ("\uE0A3", KenneyKeyboardFont),
            ["Q"] = ("\uE0AF", KenneyKeyboardFont), ["R"] = ("\uE0B5", KenneyKeyboardFont), ["S"] = ("\uE0B9", KenneyKeyboardFont), ["T"] = ("\uE0C9", KenneyKeyboardFont),
            ["U"] = ("\uE0D3", KenneyKeyboardFont), ["V"] = ("\uE0D5", KenneyKeyboardFont), ["W"] = ("\uE0D7", KenneyKeyboardFont), ["X"] = ("\uE0DB", KenneyKeyboardFont),
            ["Y"] = ("\uE0DD", KenneyKeyboardFont), ["Z"] = ("\uE0DF", KenneyKeyboardFont),
            
            // Numbers 0-9
            ["0"] = ("\uE001", KenneyKeyboardFont), ["1"] = ("\uE003", KenneyKeyboardFont), ["2"] = ("\uE005", KenneyKeyboardFont), ["3"] = ("\uE007", KenneyKeyboardFont),
            ["4"] = ("\uE009", KenneyKeyboardFont), ["5"] = ("\uE00B", KenneyKeyboardFont), ["6"] = ("\uE00D", KenneyKeyboardFont), ["7"] = ("\uE00F", KenneyKeyboardFont),
            ["8"] = ("\uE011", KenneyKeyboardFont), ["9"] = ("\uE013", KenneyKeyboardFont),
            
            // Mouse
            ["LMB"] = ("\uE0E3", KenneyKeyboardFont), ["RMB"] = ("\uE0E7", KenneyKeyboardFont), ["MMB"] = ("\uE0E9", KenneyKeyboardFont),
            ["MOUSE"] = ("\uE0E1", KenneyKeyboardFont), ["SCROLL↑"] = ("\uE0ED", KenneyKeyboardFont), ["SCROLL↓"] = ("\uE0EA", KenneyKeyboardFont), ["SCROLL↑↓"] = ("\uE0EF", KenneyKeyboardFont),
            ["MOUSE←→"] = ("\uE0E2", KenneyKeyboardFont), ["MOUSE↑↓"] = ("\uE0F2", KenneyKeyboardFont),
            
            // Punctuation
            ["-"] = ("\uE094", KenneyKeyboardFont), ["+"] = ("\uE0AB", KenneyKeyboardFont), ["="] = ("\uE060", KenneyKeyboardFont),
            [","] = ("\uE050", KenneyKeyboardFont), ["."] = ("\uE0A9", KenneyKeyboardFont), ["/"] = ("\uE0C3", KenneyKeyboardFont),
            [";"] = ("\uE0BB", KenneyKeyboardFont), ["'"] = ("\uE01B", KenneyKeyboardFont), ["["] = ("\uE044", KenneyKeyboardFont), ["]"] = ("\uE03E", KenneyKeyboardFont),
            ["\\"] = ("\uE0C1", KenneyKeyboardFont), ["`"] = ("\uE0D1", KenneyKeyboardFont), ["*"] = ("\uE034", KenneyKeyboardFont)
        };
        
        public static StackPanel CreateKeysPanelFromCombo(string keyCombo, Brush keyBackground, double iconSize = 64)
        {
            var keysPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            var keyParts = keyCombo.Split('+');
            
            for (int i = 0; i < keyParts.Length; i++)
            {
                var keyPart = keyParts[i].Trim();
                if (string.IsNullOrEmpty(keyPart)) continue;
                
                // Try case-sensitive first, then case-insensitive lookup
                (string Glyph, string Font)? iconData = null;
                
                if (HotkeyIconMap.TryGetValue(keyPart, out var exactMatch))
                {
                    iconData = exactMatch;
                }
                else
                {
                    // Try case-insensitive lookup
                    var caseInsensitiveMatch = HotkeyIconMap.FirstOrDefault(kvp => 
                        string.Equals(kvp.Key, keyPart, StringComparison.OrdinalIgnoreCase));
                    if (!caseInsensitiveMatch.Equals(default(KeyValuePair<string, (string, string)>)))
                    {
                        iconData = caseInsensitiveMatch.Value;
                    }
                }
                
                if (iconData.HasValue)
                {
                    var icon = new FontIcon
                    {
                        Glyph = iconData.Value.Glyph,
                        FontFamily = new FontFamily(iconData.Value.Font),
                        FontSize = iconSize,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    keysPanel.Children.Add(icon);
                }
                else
                {
                    // Fallback to text for unknown keys
                    var keyText = new TextBlock
                    {
                        Text = keyPart,
                        FontSize = iconSize / 4,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Padding = new Thickness(8, 4, 8, 4)
                    };
                    var keyBorder = new Border
                    {
                        Background = keyBackground,
                        CornerRadius = new CornerRadius(4),
                        Child = keyText
                    };
                    keysPanel.Children.Add(keyBorder);
                }
                
                if (i < keyParts.Length - 1)
                {
                    var plusText = new TextBlock
                    {
                        Text = "+",
                        FontSize = iconSize / 3,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = 0.6
                    };
                    keysPanel.Children.Add(plusText);
                }
            }
            return keysPanel;
        }
    }
}