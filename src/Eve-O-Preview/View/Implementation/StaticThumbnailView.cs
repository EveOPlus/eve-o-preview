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
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Preview;
using EveOPreview.View.Rendering;
using EveOPreview.Input;
using MediatR;
using Serilog;

namespace EveOPreview.View
{
    sealed class StaticThumbnailView : ThumbnailView
    {
        private readonly IPreviewSession _thumbnail;

        public StaticThumbnailView(IWindowManager windowManager, IThumbnailConfiguration config, IThumbnailManager thumbnailManager,
            IMediator mediator, IGlobalPointerInput kbmEvents, ILogger logger)
            : base(windowManager, config, thumbnailManager, mediator, kbmEvents)
        {
            // The adapter resolves the current source when it captures, including the
            // initial factory assignment that occurs after this constructor.
            var backend = new WindowsStaticPreviewBackend(windowManager, ImageSurface, _ => Id);
            _thumbnail = backend.CreateSession(default);
            _thumbnail.SetBounds(new PreviewRect(0, 0, ClientSize.Width, ClientSize.Height));
        }

        protected override void RefreshThumbnail(bool forceRefresh) => _thumbnail.Refresh(forceRefresh);

        protected override void ResizeThumbnail(int baseWidth, int baseHeight, int highlightWidthTop,
            int highlightWidthRight, int highlightWidthBottom, int highlightWidthLeft) =>
            _thumbnail.SetBounds(new PreviewRect(highlightWidthLeft, highlightWidthTop,
                baseWidth - highlightWidthLeft - highlightWidthRight, baseHeight - highlightWidthTop - highlightWidthBottom));

        protected override void Dispose(bool disposing)
        {
            if (disposing) _thumbnail?.Dispose();
            base.Dispose(disposing);
        }
    }
}
