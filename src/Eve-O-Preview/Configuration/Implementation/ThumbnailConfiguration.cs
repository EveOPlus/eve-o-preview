using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EveOPreview.Services.Interop;
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
			this.ConfigVersion = 1;

            this.CycleGroup1ForwardHotkeys = new List<string> { "F14", "Control+F14" };
            this.CycleGroup1BackwardHotkeys = new List<string> { "F13", "Control+F13" };
            this.CycleGroup1ClientsOrder = new Dictionary<string, int>
            {
                { "EVE - Example DPS Toon 1", 1 },
                { "EVE - Example DPS Toon 2", 2 },
                { "EVE - Example DPS Toon 3", 3 }
            };

            this.CycleGroup2ForwardHotkeys = new List<string> { "F16", "Control+F16" };
            this.CycleGroup2BackwardHotkeys = new List<string> { "F15", "Control+F15" };
            this.CycleGroup2ClientsOrder = new Dictionary<string, int>
            {
                { "EVE - Example Logi Toon 1", 1 },
                { "EVE - Example Scout Toon 2", 2 },
                { "EVE - Example Tackle Toon 3", 3 }
            };

            this.CycleGroup3ForwardHotkeys = new List<string> { "F14", "Control+F18" };
            this.CycleGroup3BackwardHotkeys = new List<string> { "F13", "Control+F17" };
            this.CycleGroup3ClientsOrder = new Dictionary<string, int>
            {
                { "EVE - Example DPS Toon 4", 1 },
                { "EVE - Example DPS Toon 5", 2 },
                { "EVE - Example DPS Toon 6", 3 }
            };

            this.CycleGroup4ForwardHotkeys = new List<string> { "F16", "Control+F20" };
            this.CycleGroup4BackwardHotkeys = new List<string> { "F15", "Control+F19" };
            this.CycleGroup4ClientsOrder = new Dictionary<string, int>
            {
                { "EVE - Example Logi Toon 4", 1 },
                { "EVE - Example Scout Toon 5", 2 },
                { "EVE - Example Tackle Toon 6", 3 }
            };

            this.PerClientActiveClientHighlightColor = new Dictionary<string, Color>
			{
				{"EVE - Example Toon 1", Color.Red},
				{"EVE - Example Toon 2", Color.Green}
			};

			this.PerClientLayout = new Dictionary<string, Dictionary<string, Point>>();
			this.FlatLayout = new Dictionary<string, Point>();
            this.ClientLayout = new Dictionary<string, ClientLayout>();
            this.ClientCrop = new Dictionary<string, Rectangle>();
            this.ClientHotkey = new Dictionary<string, string>();
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

			this.LoginThumbnailLocation = new Point(5, 5);
		}


		[JsonProperty("ConfigVersion")]
		public int ConfigVersion { get; set; }

		[JsonProperty("CycleGroup1ForwardHotkeys")]
		public List<string> CycleGroup1ForwardHotkeys { get; set; }

		[JsonProperty("CycleGroup1BackwardHotkeys")]
		public List<string> CycleGroup1BackwardHotkeys { get; set; }

		[JsonProperty("CycleGroup1ClientsOrder")]
		public Dictionary<string, int> CycleGroup1ClientsOrder { get; set; }

        [JsonProperty("CycleGroup2ForwardHotkeys")]
        public List<string> CycleGroup2ForwardHotkeys { get; set; }

        [JsonProperty("CycleGroup2BackwardHotkeys")]
        public List<string> CycleGroup2BackwardHotkeys { get; set; }

        [JsonProperty("CycleGroup2ClientsOrder")]
        public Dictionary<string, int> CycleGroup2ClientsOrder { get; set; }

        [JsonProperty("CycleGroup3ForwardHotkeys")]
        public List<string> CycleGroup3ForwardHotkeys { get; set; }

        [JsonProperty("CycleGroup3BackwardHotkeys")]
        public List<string> CycleGroup3BackwardHotkeys { get; set; }

        [JsonProperty("CycleGroup3ClientsOrder")]
        public Dictionary<string, int> CycleGroup3ClientsOrder { get; set; }

        [JsonProperty("CycleGroup4ForwardHotkeys")]
        public List<string> CycleGroup4ForwardHotkeys { get; set; }

        [JsonProperty("CycleGroup4BackwardHotkeys")]
        public List<string> CycleGroup4BackwardHotkeys { get; set; }

        [JsonProperty("CycleGroup4ClientsOrder")]
        public Dictionary<string, int> CycleGroup4ClientsOrder { get; set; }

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
					this.ClientLayout.Clear();
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
					this.PerClientLayout.Clear();
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

		[JsonProperty("LoginThumbnailLocation")]
		public Point LoginThumbnailLocation { get; set; }

		[JsonProperty]
		private Dictionary<string, Dictionary<string, Point>> PerClientLayout { get; set; }
		[JsonProperty]
		private Dictionary<string, Point> FlatLayout { get; set; }
		[JsonProperty]
        private Dictionary<string, ClientLayout> ClientLayout { get; set; }
        [JsonProperty]
        private Dictionary<string, Rectangle> ClientCrop{ get; set; }
        [JsonProperty]
        private Dictionary<string, string> ClientHotkey { get; set; }
		[JsonProperty]
		private Dictionary<string, bool> DisableThumbnail { get; set; }
		[JsonProperty]
		private List<string> PriorityClients { get; set; }

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

        public Rectangle GetClientCrop(string currentClient)
        {
            Rectangle crop;
            this.ClientCrop.TryGetValue(currentClient, out crop);
            return crop;
        }

        public void SetClientLayout(string currentClient, ClientLayout layout)
		{
			this.ClientLayout[currentClient] = layout;
		}

		public Keys GetClientHotkey(string currentClient)
		{
			string hotkey;
			if (this.ClientHotkey.TryGetValue(currentClient, out hotkey))
			{
				// Protect from incorrect values
				object rawValue = (new KeysConverter()).ConvertFromInvariantString(hotkey);
				return rawValue != null ? (Keys)rawValue : Keys.None;
			}

			return Keys.None;
		}

		public void SetClientHotkey(string currentClient, Keys hotkey)
		{
			this.ClientHotkey[currentClient] = (new KeysConverter()).ConvertToInvariantString(hotkey);
		}

		public Keys StringToKey(string hotkey)
		{
			object rawValue = (new KeysConverter()).ConvertFromInvariantString(hotkey);
			return rawValue != null ? (Keys)rawValue : Keys.None;
		}

		public bool IsPriorityClient(string currentClient)
		{
			return this.PriorityClients.Contains(currentClient);
		}

		public bool IsThumbnailDisabled(string currentClient)
		{
			return this.DisableThumbnail.TryGetValue(currentClient, out bool isDisabled) && isDisabled;
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