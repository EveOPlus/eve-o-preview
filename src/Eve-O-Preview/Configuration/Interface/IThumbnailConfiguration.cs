using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Configuration.Implementation;

namespace EveOPreview.Configuration
{
    public interface IThumbnailConfiguration
    {
        List<CycleGroup> CycleGroups { get; set; }

        // 【新增】保存客户端备注的字典
        Dictionary<string, string> ClientNotes { get; set; }

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
        FontSettings TitleFontSettings { get; set; }

        Point LoginThumbnailLocation { get; set; }

        Point GetThumbnailLocation(string currentClient, string activeClient, Point defaultLocation);
        void SetThumbnailLocation(string currentClient, string activeClient, Point location);

        ClientLayout GetClientLayout(string currentClient);
        void SetClientLayout(string currentClient, ClientLayout layout);

        Keys GetClientHotkey(string currentClient);
        void SetClientHotkey(string currentClient, Keys hotkey);
        Keys StringToKey(string hotkey);

        // 【新增】获取和设置备注的方法
        string GetClientNote(string currentClient);
        void SetClientNote(string currentClient, string note);

        bool IsPriorityClient(string currentClient);

        bool IsThumbnailDisabled(string currentClient);
        void ToggleThumbnail(string currentClient, bool isDisabled);

        void ApplyRestrictions();
    }
}