using System;
using System.Collections.Generic;
using System.Linq;
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

using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace EveOPreview.Configuration.Implementation
{
    public class CycleGroup
    {
        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("ForwardHotkeys")]
        public List<string> ForwardHotkeys { get; set; } = new List<string>();

        [JsonProperty("BackwardHotkeys")]
        public List<string> BackwardHotkeys { get; set; } = new List<string>();

        [JsonProperty("ClientsOrder")]
        public SortedDictionary<int, string> ClientsOrder { get; set; } = new SortedDictionary<int, string>();

        [JsonIgnore]
        public List<Keys> ForwardHotkeysParsedAndOrdered { get; } = new List<Keys>();

        [JsonIgnore]
        public List<Keys> BackwardHotkeysParsedAndOrdered { get; } = new List<Keys>();
    }
}