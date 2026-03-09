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
using System.Drawing;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveOPreview.View
{
    sealed class ThumbnailViewFactory : IThumbnailViewFactory
    {
        private readonly IApplicationController _controller;
        private readonly bool _isCompatibilityModeEnabled;
        private readonly FontSettings _titleFontSettings;

        public ThumbnailViewFactory(IApplicationController controller, IThumbnailConfiguration configuration)
        {
            this._controller = controller;
            this._isCompatibilityModeEnabled = configuration.EnableCompatibilityMode;
            this._titleFontSettings = configuration.TitleFontSettings;
        }

        public IThumbnailView Create(IntPtr id, string title, Size size)
        {
            IThumbnailView view = this._isCompatibilityModeEnabled
                ? (IThumbnailView)this._controller.Create<StaticThumbnailView>()
                : (IThumbnailView)this._controller.Create<LiveThumbnailView>();

            view.Id = id;
            view.Title = title;
            view.ThumbnailSize = size;
            view.TitleFontSettings = this._titleFontSettings;

            return view;
        }
    }
}