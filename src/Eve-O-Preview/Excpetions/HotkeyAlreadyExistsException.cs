using Gma.System.MouseKeyHook.HotKeys;
using System;
using System.Windows.Forms;

namespace EveOPreview.Excpetions
{
    public class HotkeyAlreadyExistsException : Exception
    {
        public Keys Keys { get; }
        public string Location1 { get; }
        public string Location2 { get; }

        public HotkeyAlreadyExistsException(Keys keys, string location1, string location2) : base($"Duplicate hotkey {keys} exists in {location1} and {location2}")
        {
            Keys = keys;
            Location1 = location1;
            Location2 = location2;
        }
    }
}