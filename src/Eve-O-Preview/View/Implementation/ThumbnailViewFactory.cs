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