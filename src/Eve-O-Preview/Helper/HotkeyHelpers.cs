using System;
using System.Windows.Forms;
using Serilog;

namespace EveOPreview.Helper
{
    public static class HotkeyHelpers
    {
        public static Keys ToHotkeys(this string stringKey)
        {
            try
            {
                object rawValue = (new KeysConverter()).ConvertFromInvariantString(stringKey);
                return rawValue != null ? (Keys)rawValue : Keys.None;
            }
            catch (Exception ex)
            {
                Log.Logger.WithCallerInfo().Warning($"Unable to parse hotkey value '{stringKey}'");
                return Keys.None;
            }
        }
    }
}