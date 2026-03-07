using System;
using System.Collections.Generic;
using System.Linq;
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