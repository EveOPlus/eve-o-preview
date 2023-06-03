using EveOPreview.Services.Interop;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.Configuration
{
	public interface IThumbnailConfiguration
	{
		List<string> CycleGroup1ForwardHotkeys { get; set; }
		List<string> CycleGroup1BackwardHotkeys { get; set; }
		Dictionary<string, int> CycleGroup1ClientsOrder { get; set; }

        List<string> CycleGroup2ForwardHotkeys { get; set; }
        List<string> CycleGroup2BackwardHotkeys { get; set; }
        Dictionary<string, int> CycleGroup2ClientsOrder { get; set; }

        List<string> CycleGroup3ForwardHotkeys { get; set; }
        List<string> CycleGroup3BackwardHotkeys { get; set; }
        Dictionary<string, int> CycleGroup3ClientsOrder { get; set; }

        List<string> CycleGroup4ForwardHotkeys { get; set; }
        List<string> CycleGroup4BackwardHotkeys { get; set; }
        Dictionary<string, int> CycleGroup4ClientsOrder { get; set; }

        Dictionary<string, Color> PerClientActiveClientHighlightColor { get; set; }

		bool MinimizeToTray { get; set; }
		int ThumbnailRefreshPeriod { get; set; }

		bool EnableCompatibilityMode { get; set; }

		double ThumbnailOpacity { get; set; }

		bool EnableClientLayoutTracking { get; set; }
		bool HideActiveClientThumbnail { get; set; }
		bool MinimizeInactiveClients { get; set; }
		bool ShowThumbnailsAlwaysOnTop { get; set; }
		bool EnablePerClientThumbnailLayouts { get; set; }

		bool HideThumbnailsOnLostFocus { get; set; }
		int HideThumbnailsDelay { get; set; }

		Size ThumbnailSize { get; set; }
		Size ThumbnailMinimumSize { get; set; }
		Size ThumbnailMaximumSize { get; set; }

		bool EnableThumbnailSnap { get; set; }

		bool ThumbnailZoomEnabled { get; set; }
		int ThumbnailZoomFactor { get; set; }
		ZoomAnchor ThumbnailZoomAnchor { get; set; }

		bool ShowThumbnailOverlays { get; set; }
		bool ShowThumbnailFrames { get; set; }

		bool EnableActiveClientHighlight { get; set; }
		Color ActiveClientHighlightColor { get; set; }
		int ActiveClientHighlightThickness { get; set; }

		Point LoginThumbnailLocation { get; set; }

		Point GetThumbnailLocation(string currentClient, string activeClient, Point defaultLocation);
		void SetThumbnailLocation(string currentClient, string activeClient, Point location);

		Rectangle GetClientCrop(string currentClient);

		ClientLayout GetClientLayout(string currentClient);
		void SetClientLayout(string currentClient, ClientLayout layout);

		Keys GetClientHotkey(string currentClient);
		void SetClientHotkey(string currentClient, Keys hotkey);
		Keys StringToKey(string hotkey);

		bool IsPriorityClient(string currentClient);

		bool IsThumbnailDisabled(string currentClient);
		void ToggleThumbnail(string currentClient, bool isDisabled);

		void ApplyRestrictions();
	}
}