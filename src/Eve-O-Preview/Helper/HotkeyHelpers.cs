//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Windows.Forms;
using Serilog;

namespace EveOPreview.Helper
{
    public static class HotkeyHelpers
    {
        public static Keys ToHotkeys(this string stringKey)
        {
            if (string.IsNullOrWhiteSpace(stringKey))
            {
                return Keys.None;
            }

            try
            {
                object rawValue = (new KeysConverter()).ConvertFromInvariantString(stringKey);
                return rawValue != null ? (Keys)rawValue : Keys.None;
            }
            catch (Exception ex)
            {
                Log.Logger.WithCallerInfo().Warning(ex, $"Unable to parse hotkey value '{stringKey}'");
                return Keys.None;
            }
        }
    }
}