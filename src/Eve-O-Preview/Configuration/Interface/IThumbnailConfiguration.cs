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

using EveOPreview.Configuration.Implementation;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.Configuration
{
    public interface IThumbnailConfiguration
    {
        int ConfigVersion { get; set; }
        List<CycleGroup> CycleGroups { get; set; }

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

        FpsLimiterSettings FpsLimiterSettings { get; set; }
        AudioMuteSettings AudioMuteSettings { get; set; }

        string PremiumLicenseKey { get; set; }
        bool IsPremium { get; set; }
        string ToggleHideActiveClientsHotkey { get; set; }
        string MinimizeAllClientsHotkey { get; set; }
        Keys ToggleHideActiveClientsHotkeyParsed { get; set; }
        Keys MinimizeAllClientsHotkeyParsed { get; set; }

        Point GetThumbnailLocation(string currentClient, string activeClient, Point defaultLocation);
        void SetThumbnailLocation(string currentClient, string activeClient, Point location);

        ClientLayout GetClientLayout(string currentClient);
        void SetClientLayout(string currentClient, ClientLayout layout);
        
        bool IsPriorityClient(string currentClient);

        bool IsTemporarilyHidingAllThumbnails { get; set; }
        bool IsThumbnailDisabled(string currentClient);
        void ToggleThumbnail(string currentClient, bool isDisabled);

        void ApplyRestrictions();
    }
}