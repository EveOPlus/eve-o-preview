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
using EveOPreview.Services;
using EveOPreview.Input;
using MediatR;
using Serilog;
using EveOPreview.Preview;
using EveOPreview.View.Rendering;

namespace EveOPreview.View
{
    sealed class LiveThumbnailView : ThumbnailView
    {
        #region Private fields
        private IPreviewSession _thumbnail;
        private readonly IPreviewBackend _backend;
        private Point _startLocation;
        private Point _endLocation;
        private IThumbnailConfiguration _config;
        private readonly ILogger _logger;
        #endregion

        public LiveThumbnailView(IWindowManager windowManager, IThumbnailConfiguration config, IThumbnailManager thumbnailManager, IMediator mediator, IGlobalPointerInput kbmEvents, ILogger logger)
            : base(windowManager, config, thumbnailManager, mediator, kbmEvents)
        {
            _logger = logger;
            this._startLocation = new Point(0, 0);
            this._endLocation = new Point(this.ClientSize);
            this._config = config;
            // Runtime IDs remain adapter-local; persisted full-title identities are unchanged.
            _backend = new WindowsDwmPreviewBackend(windowManager, () => Handle, client => new IntPtr(client.Value));
            _logger.Verbose("LiveThumbnailView created for window 0x{Handle:X}", this.Id);
        }

        protected override void RefreshThumbnail(bool forceRefresh)
        {
            if (_thumbnail == null)
            {
                _thumbnail = _backend.CreateSession(new PreviewClientId(Id.ToInt64()));
                ApplyImageBounds();
            }
            _thumbnail.Refresh(forceRefresh);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _thumbnail?.Dispose();
                _thumbnail = null;
            }
            base.Dispose(disposing);
        }

        protected override void ResizeThumbnail(int baseWidth, int baseHeight, int highlightWidthTop, int highlightWidthRight, int highlightWidthBottom, int highlightWidthLeft)
        {
            var left = 0 + highlightWidthLeft;
            var top = 0 + highlightWidthTop;
            var right = baseWidth - highlightWidthRight;
            var bottom = baseHeight - highlightWidthBottom;

            if ((this._startLocation.X == left) && (this._startLocation.Y == top) && (this._endLocation.X == right) && (this._endLocation.Y == bottom))
            {
                return; // No update required
            }
            
            _logger.Verbose("Resizing thumbnail for 0x{Handle:X}: ({Left},{Top}) -> ({Right},{Bottom})", this.Id, left, top, right, bottom);
            this._startLocation = new Point(left, top);
            this._endLocation = new Point(right, bottom);

            ApplyImageBounds();
        }

        private void ApplyImageBounds() => _thumbnail?.SetBounds(new PreviewRect(
            _startLocation.X, _startLocation.Y, _endLocation.X - _startLocation.X, _endLocation.Y - _startLocation.Y));
    }

}
