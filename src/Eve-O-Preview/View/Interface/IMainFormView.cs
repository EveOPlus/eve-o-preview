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
using EveOPreview.Mediator.Messages;
using System;
using System.Collections.Generic;
using System.Drawing;
using EveOPreview.Configuration.Model;

namespace EveOPreview.View
{
	/// <summary>
	/// Main view interface
	/// Presenter uses it to access GUI properties
	/// </summary>
	public interface IMainFormView : IView
	{
		bool MinimizeToTray { get; set; }

		double ThumbnailOpacity { get; set; }

		bool EnableClientLayoutTracking { get; set; }
		bool HideActiveClientThumbnail { get; set; }
		bool MinimizeInactiveClients { get; set; }
		bool ShowThumbnailsAlwaysOnTop { get; set; }
		bool HideThumbnailsOnLostFocus { get; set; }
		bool EnablePerClientThumbnailLayouts { get; set; }

		Size ThumbnailSize { get; set; }

		bool EnableThumbnailZoom { get; set; }
		int ThumbnailZoomFactor { get; set; }
		ViewZoomAnchor ThumbnailZoomAnchor { get; set; }

		bool ShowThumbnailOverlays { get; set; }
		bool ShowThumbnailFrames { get; set; }

		bool EnableActiveClientHighlight { get; set; }
		Color ActiveClientHighlightColor { get; set; }
        FontSettings TitleFontSettings { get; set; }

        FpsLimiterSettings FpsLimiterSettings { get; set; }
        AudioMuteSettings AudioMuteSettings { get; set; }
        string ToggleHideAllActiveHotkey { get; set; }
        string MinimizeAllClientsHotkey { get; set; }
        string LoadedProfileName { get; set; }

        List<CycleGroup> CycleGroups { get; set; }

		void SetDocumentationUrl(string url);
		void SetVersionInfo(string version);
		void SetThumbnailSizeLimitations(Size minimumSize, Size maximumSize);

		void Minimize();

		void AddThumbnails(IList<IThumbnailDescription> thumbnails);
		void RemoveThumbnails(IList<IThumbnailDescription> thumbnails);
		void RefreshZoomSettings();

		Action ApplicationExitRequested { get; set; }
		Action FormActivated { get; set; }
		Action FormMinimized { get; set; }
		Action<ViewCloseRequest> FormCloseRequested { get; set; }
		Action ApplicationSettingsChanged { get; set; }
		Action ThumbnailsSizeChanged { get; set; }
		Action<string> ThumbnailStateChanged { get; set; }
		Action DocumentationLinkActivated { get; set; }
		Func<string> GetClientNameFromInput { get; set; }
        Func<string, CaptureNewHotkeyResponse> CaptureNewHotkey { get; set; }
        Action FpsLimiterChanged { get; set; }
        Action FpsLimiterEnabledChanged { get; set; }
        Action AudioSettingsChanged { get; set; }
        Action ToggleHideAllActiveClients { get; set; }
        Action MinimizeAllClients { get; set; }
        public Action CloneCurrentProfile { get; set; }
        public Action DeleteCurrentProfile { get; set; }
        Action<string> RenameCurrentProfile { get; set; }
        Action<ProfileLocation> SwitchToProfile { get; set; }
        bool IsPremium { get; set; }
        void UpdateThumbnailToggleHideAllStatus(bool notificationIsHidden);
        void UpdateProfileList(List<ProfileLocation> notificationNewProfileLocations);
    }
}
