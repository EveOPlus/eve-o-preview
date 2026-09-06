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

using System.Collections.Generic;
using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace EveOPreview.Configuration.Implementation
{
    sealed class ThumbnailConfiguration : IThumbnailConfiguration
    {
        #region Private fields
        private bool _enablePerClientThumbnailLayouts;
        private bool _enableClientLayoutTracking;
        #endregion

        public ThumbnailConfiguration()
        {
            this.ConfigVersion = 3;

            this.CycleGroups = new List<CycleGroup>();

            this.PerClientActiveClientHighlightColor = new Dictionary<string, Color>
            {
                {"EVE - Example Toon 1", Color.Red},
                {"EVE - Example Toon 2", Color.Green}
            };

            this.PerClientLayout = new Dictionary<string, Dictionary<string, Point>>();
            this.FlatLayout = new Dictionary<string, Point>();
            this.ClientLayout = new Dictionary<string, ClientLayout>();
            this.DisableThumbnail = new Dictionary<string, bool>();
            this.PriorityClients = new List<string>();

            this.MinimizeToTray = false;
            this.ThumbnailRefreshPeriod = 500;

            this.EnableCompatibilityMode = false;

            this.ThumbnailOpacity = 0.5;

            this.EnableClientLayoutTracking = false;
            this.HideActiveClientThumbnail = false;
            this.MinimizeInactiveClients = false;
            this.ShowThumbnailsAlwaysOnTop = true;
            this.EnablePerClientThumbnailLayouts = false;

            this.HideThumbnailsOnLostFocus = false;
            this.HideThumbnailsDelay = 2; // 2 thumbnails refresh cycles (1.0 sec)

            this.ThumbnailSize = new Size(384, 216);
            this.ThumbnailMinimumSize = new Size(192, 108);
            this.ThumbnailMaximumSize = new Size(960, 540);

            this.EnableThumbnailSnap = true;

            this.ThumbnailZoomEnabled = false;
            this.ThumbnailZoomFactor = 2;
            this.ThumbnailZoomAnchor = ZoomAnchor.NW;

            this.ShowThumbnailOverlays = true;
            this.ShowThumbnailFrames = false;

            this.EnableActiveClientHighlight = false;
            this.ActiveClientHighlightColor = Color.GreenYellow;
            this.ActiveClientHighlightThickness = 3;

            this.TitleFontSettings = new FontSettings();

            this.LoginThumbnailLocation = new Point(5, 5);

            this.FpsLimiterSettings = new FpsLimiterSettings();
            this.AudioMuteSettings = new AudioMuteSettings();

            this.EnableAutomaticCpuAffinity = true;
        }

        [JsonProperty("ConfigVersion")]
        public int ConfigVersion { get; set; }

        [JsonProperty("CycleGroups")]
        public List<CycleGroup> CycleGroups { get; set; } = new List<CycleGroup>();

        [JsonProperty("PerClientActiveClientHighlightColor")]
        public Dictionary<string, Color> PerClientActiveClientHighlightColor { get; set; }

        public bool MinimizeToTray { get; set; }
        public int ThumbnailRefreshPeriod { get; set; }

        [JsonProperty("CompatibilityMode")]
        public bool EnableCompatibilityMode { get; set; }

        [JsonProperty("ThumbnailsOpacity")]
        public double ThumbnailOpacity { get; set; }

        public bool EnableClientLayoutTracking
        {
            get => this._enableClientLayoutTracking;
            set
            {
                if (!value)
                {
                    this.ClientLayout?.Clear();
                }

                this._enableClientLayoutTracking = value;
            }
        }

        public bool HideActiveClientThumbnail { get; set; }
        public bool MinimizeInactiveClients { get; set; }
        public bool ShowThumbnailsAlwaysOnTop { get; set; }

        public bool EnablePerClientThumbnailLayouts
        {
            get => this._enablePerClientThumbnailLayouts;
            set
            {
                if (!value)
                {
                    this.PerClientLayout?.Clear();
                }

                this._enablePerClientThumbnailLayouts = value;
            }
        }

        public bool HideThumbnailsOnLostFocus { get; set; }
        public int HideThumbnailsDelay { get; set; }

        public Size ThumbnailSize { get; set; }
        public Size ThumbnailMaximumSize { get; set; }
        public Size ThumbnailMinimumSize { get; set; }

        public bool EnableThumbnailSnap { get; set; }

        [JsonProperty("EnableThumbnailZoom")]
        public bool ThumbnailZoomEnabled { get; set; }
        public int ThumbnailZoomFactor { get; set; }
        public ZoomAnchor ThumbnailZoomAnchor { get; set; }

        public bool ShowThumbnailOverlays { get; set; }
        public bool ShowThumbnailFrames { get; set; }

        public bool EnableActiveClientHighlight { get; set; }

        public Color ActiveClientHighlightColor { get; set; }

        public int ActiveClientHighlightThickness { get; set; }
        
        public string ToggleHideActiveClientsHotkey { get; set; }
        public string MinimizeAllClientsHotkey { get; set; }

        [JsonIgnore]
        public Keys ToggleHideActiveClientsHotkeyParsed { get; set; }

        [JsonIgnore]
        public Keys MinimizeAllClientsHotkeyParsed { get; set; }
        
        public FontSettings TitleFontSettings { get; set; }

        [JsonProperty("LoginThumbnailLocation")]
        public Point LoginThumbnailLocation { get; set; }
        
        public FpsLimiterSettings FpsLimiterSettings { get; set; }

        public AudioMuteSettings AudioMuteSettings { get; set; }




        [JsonProperty]
        private Dictionary<string, Dictionary<string, Point>> PerClientLayout { get; set; }
        
        [JsonProperty]
        private Dictionary<string, Point> FlatLayout { get; set; }
        
        [JsonProperty]
        private Dictionary<string, ClientLayout> ClientLayout { get; set; }
        
        [JsonProperty]
        private Dictionary<string, bool> DisableThumbnail { get; set; }
        
        [JsonProperty]
        private List<string> PriorityClients { get; set; }

        [JsonProperty]
        public bool EnableAutomaticCpuAffinity { get; set; }

        public Point GetThumbnailLocation(string currentClient, string activeClient, Point defaultLocation)
        {
            Point location;

            // What this code does:
            // If Per-Client layouts are enabled
            //    and client name is known
            //    and there is a separate thumbnails layout for this client
            //    and this layout contains an entry for the current client
            // then return that entry
            // otherwise try to get client layout from the flat all-clients layout
            // If there is no layout too then use the default one
            if (this.EnablePerClientThumbnailLayouts && !string.IsNullOrEmpty(activeClient))
            {
                Dictionary<string, Point> layoutSource;
                if (this.PerClientLayout.TryGetValue(activeClient, out layoutSource) && layoutSource.TryGetValue(currentClient, out location))
                {
                    return location;
                }
            }

            return this.FlatLayout.TryGetValue(currentClient, out location) ? location : defaultLocation;
        }

        public void SetThumbnailLocation(string currentClient, string activeClient, Point location)
        {
            Dictionary<string, Point> layoutSource;

            if (this.EnablePerClientThumbnailLayouts)
            {
                if (string.IsNullOrEmpty(activeClient))
                {
                    return;
                }

                if (!this.PerClientLayout.TryGetValue(activeClient, out layoutSource))
                {
                    layoutSource = new Dictionary<string, Point>();
                    this.PerClientLayout[activeClient] = layoutSource;
                }
            }
            else
            {
                layoutSource = this.FlatLayout;
            }

            layoutSource[currentClient] = location;
        }

        public ClientLayout GetClientLayout(string currentClient)
        {
            ClientLayout layout;
            this.ClientLayout.TryGetValue(currentClient, out layout);

            return layout;
        }

        public void SetClientLayout(string currentClient, ClientLayout layout)
        {
            this.ClientLayout[currentClient] = layout;
        }
        public bool IsPriorityClient(string currentClient)
        {
            return this.PriorityClients.Contains(currentClient);
        }

        [JsonIgnore] 
        public bool IsTemporarilyHidingAllThumbnails { get; set; } = false;

        public bool IsThumbnailDisabled(string currentClient)
        {
            return IsTemporarilyHidingAllThumbnails || 
                   this.DisableThumbnail.TryGetValue(currentClient, out bool isDisabled) && isDisabled;
        }

        public void ToggleThumbnail(string currentClient, bool isDisabled)
        {
            this.DisableThumbnail[currentClient] = isDisabled;
        }

        /// <summary>
        /// Applies restrictions to different parameters of the config
        /// </summary>
        public void ApplyRestrictions()
        {
            CycleGroups ??= new List<CycleGroup>();
            CycleGroups.RemoveAll(x => x == null);
            foreach (var group in CycleGroups)
            {
                group.ForwardHotkeys ??= new List<string>();
                group.BackwardHotkeys ??= new List<string>();
                group.ClientsOrder ??= new SortedDictionary<int, string>();
                foreach (var key in group.ClientsOrder.Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToArray())
                    group.ClientsOrder.Remove(key);
            }
            PerClientActiveClientHighlightColor ??= new Dictionary<string, Color>();
            PerClientLayout ??= new Dictionary<string, Dictionary<string, Point>>();
            foreach (var key in PerClientLayout.Where(x => x.Value == null).Select(x => x.Key).ToArray()) PerClientLayout.Remove(key);
            FlatLayout ??= new Dictionary<string, Point>();
            ClientLayout ??= new Dictionary<string, ClientLayout>();
            DisableThumbnail ??= new Dictionary<string, bool>();
            PriorityClients ??= new List<string>();
            FpsLimiterSettings ??= new FpsLimiterSettings();
            FpsLimiterSettings.FpsFocused = Math.Clamp(FpsLimiterSettings.FpsFocused, 0, 1000);
            FpsLimiterSettings.FpsBackground = Math.Clamp(FpsLimiterSettings.FpsBackground, 0, 1000);
            FpsLimiterSettings.FpsPredictingFocus = Math.Clamp(FpsLimiterSettings.FpsPredictingFocus, 0, 1000);
            AudioMuteSettings ??= new AudioMuteSettings();
            AudioMuteSettings.CustomMutedEventIds ??= new List<uint>();
            TitleFontSettings ??= new FontSettings();
            if (string.IsNullOrWhiteSpace(TitleFontSettings.Name)) TitleFontSettings.Name = "Arial";
            if (!float.IsFinite(TitleFontSettings.Size) || TitleFontSettings.Size <= 0) TitleFontSettings.Size = 14.25f;
            TitleFontSettings.Size = Math.Clamp(TitleFontSettings.Size, 1, 200);
            if (!float.IsFinite(TitleFontSettings.OutlineWidth)) TitleFontSettings.OutlineWidth = 3;
            TitleFontSettings.OutlineWidth = Math.Clamp(TitleFontSettings.OutlineWidth, 0, 20);
            TitleFontSettings.Style &= FontStyle.Bold | FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout;
            ThumbnailMinimumSize = new Size(Math.Clamp(ThumbnailMinimumSize.Width, 1, 960), Math.Clamp(ThumbnailMinimumSize.Height, 1, 540));
            ThumbnailMaximumSize = new Size(Math.Clamp(ThumbnailMaximumSize.Width, ThumbnailMinimumSize.Width, 960), Math.Clamp(ThumbnailMaximumSize.Height, ThumbnailMinimumSize.Height, 540));
            if (!Enum.IsDefined(ThumbnailZoomAnchor)) ThumbnailZoomAnchor = ZoomAnchor.NW;
            HideThumbnailsDelay = Math.Max(0, HideThumbnailsDelay);
            this.ThumbnailRefreshPeriod = ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailRefreshPeriod, 300, 1000);
            this.ThumbnailSize = new Size(ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailSize.Width, this.ThumbnailMinimumSize.Width, this.ThumbnailMaximumSize.Width),
                ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailSize.Height, this.ThumbnailMinimumSize.Height, this.ThumbnailMaximumSize.Height));
            this.ThumbnailOpacity = ThumbnailConfiguration.ApplyRestrictions((int)(this.ThumbnailOpacity * 100.00), 20, 100) / 100.00;
            this.ThumbnailZoomFactor = ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailZoomFactor, 2, 10);
            this.ActiveClientHighlightThickness = ThumbnailConfiguration.ApplyRestrictions(this.ActiveClientHighlightThickness, 1, 6);
        }

        private static int ApplyRestrictions(int value, int minimum, int maximum)
        {
            if (value <= minimum)
            {
                return minimum;
            }

            if (value >= maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}