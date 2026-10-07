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
using EveOPreview.View.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.View
{
    sealed class ThumbnailViewFactory : IThumbnailViewFactory, IDisposable
    {
        private readonly IApplicationController _controller;
        private readonly IThumbnailConfiguration _configuration;
        private readonly ApplicationPreferences _preferences;
        private readonly HashSet<ThumbnailView> _views = new();

        public ThumbnailViewFactory(IApplicationController controller, IThumbnailConfiguration configuration)
            : this(controller, configuration, null) { }

        public ThumbnailViewFactory(IApplicationController controller, IThumbnailConfiguration configuration, ApplicationPreferences preferences)
        {
            this._controller = controller;
            this._configuration = configuration;
            _preferences = preferences;
            if (_preferences != null) _preferences.Changed += ApplyOverlayPreference;
        }

        private OverlayRendererKind RendererKind => _preferences?.PreviewOverlayRenderer == "NativeComposition"
            ? OverlayRendererKind.NativeComposition : OverlayRendererKind.Legacy;

        private void ApplyOverlayPreference()
        {
            foreach (var view in _views.ToArray())
            {
                if (view.IsDisposed) { _views.Remove(view); continue; }
                view.SetOverlayRenderer(RendererKind);
            }
        }

        public void Dispose()
        {
            if (_preferences != null) _preferences.Changed -= ApplyOverlayPreference;
            _views.Clear();
        }

        public IThumbnailView Create(IntPtr id, string title, Size size)
        {
            IThumbnailView view = this._configuration.EnableCompatibilityMode
                ? (IThumbnailView)this._controller.Create<StaticThumbnailView>()
                : (IThumbnailView)this._controller.Create<LiveThumbnailView>();

            view.Id = id;
            view.Title = title;
            view.ThumbnailSize = size;
            view.TitleFontSettings = this._configuration.TitleFontSettings;
            if (view is ThumbnailView nativeView)
            {
                nativeView.SetOverlayRenderer(RendererKind);
                _views.Add(nativeView);
                nativeView.Disposed += (_, _) => _views.Remove(nativeView);
            }

            return view;
        }
    }
}
