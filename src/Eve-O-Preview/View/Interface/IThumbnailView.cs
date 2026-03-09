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
using EveOPreview.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View
{
    public interface IThumbnailView : IView
    {
        IntPtr Id { get; set; }
        string Title { get; set; }
        FontSettings TitleFontSettings { get; set; }
        bool IsActive { get; set; }
        Point ThumbnailLocation { get; set; }
        Size ThumbnailSize { get; set; }
        bool IsOverlayEnabled { get; set; }

        bool IsKnownHandle(IntPtr handle);

        void SetSizeLimitations(Size minimumSize, Size maximumSize);
        void SetOpacity(double opacity);
        void SetFrames(bool enable);
        void SetTopMost(bool enableTopmost);
        void SetHighlight();
        void SetHighlight(bool enabled, int width);

        void ZoomIn(ViewZoomAnchor anchor, int zoomFactor);
        void ZoomOut();

        void RegisterHotkey(Keys hotkey);
        void UnregisterHotkey();

        void Refresh(bool forceRefresh);

        Action<IntPtr> ThumbnailResized { get; set; }
        Action<IntPtr> ThumbnailMoved { get; set; }
        Action<IntPtr> ThumbnailFocused { get; set; }
        Action<IntPtr> ThumbnailLostFocus { get; set; }

        Action<IntPtr> ThumbnailActivated { get; set; }
        Action<IntPtr, bool> ThumbnailDeactivated { get; set; }

        IWindowManager WindowManager { get; }
        void SetDefaultBorderColor();
        void ClearBorder();
    }
}