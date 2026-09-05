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
using System.Collections.Generic;
using System.Globalization;

namespace EveOPreview.Configuration.Implementation
{
    public class AudioMuteSettings
    {
        public bool MuteJumpGateTunnel { get; set; } = false;
        
        public bool MuteLocationBanner { get; set; } = false;

        public List<uint> CustomMutedEventIds
        {
            get;
            set => field = value ?? [];
        } = [];

        public static bool TryParseCustomMutedEventIds(string text, out List<uint> eventIds)
        {
            eventIds = [];
            var seen = new HashSet<uint>();
            foreach (string part in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out uint id))
                {
                    eventIds.Clear();
                    return false;
                }
                if (seen.Add(id)) eventIds.Add(id);
            }
            return true;
        }
    }
}