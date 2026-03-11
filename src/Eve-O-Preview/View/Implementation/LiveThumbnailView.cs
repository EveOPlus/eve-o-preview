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
using Gma.System.MouseKeyHook;
using MediatR;

namespace EveOPreview.View
{
    sealed class LiveThumbnailView : ThumbnailView
    {
        #region Private fields
        private IDwmThumbnail _thumbnail;
        private Point _startLocation;
        private Point _endLocation;
        private IThumbnailConfiguration _config;
        #endregion

        public LiveThumbnailView(IWindowManager windowManager, IThumbnailConfiguration config, IThumbnailManager thumbnailManager, IMediator mediator, IKeyboardMouseEvents kbmEvents)
            : base(windowManager, config, thumbnailManager, mediator, kbmEvents)
        {
            this._startLocation = new Point(0, 0);
            this._endLocation = new Point(this.ClientSize);
            this._config = config;
        }

        protected override void RefreshThumbnail(bool forceRefresh)
        {
            // To prevent flickering the old broken thumbnail is removed AFTER the new shiny one is created
            IDwmThumbnail obsoleteThumbnail = forceRefresh ? this._thumbnail : null;

            if ((this._thumbnail == null) || forceRefresh)
            {
                this.RegisterThumbnail();
            }

            obsoleteThumbnail?.Unregister();
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
            this._startLocation = new Point(left, top);
            this._endLocation = new Point(right, bottom);

            this._thumbnail.Move(left, top, right, bottom);
            this._thumbnail.Update();
        }

        private void RegisterThumbnail()
        {
            this._thumbnail = this.WindowManager.GetLiveThumbnail(this.Handle, this.Id);
            this._thumbnail.Move(this._startLocation.X, this._startLocation.Y, this._endLocation.X, this._endLocation.Y);
            this._thumbnail.Update();
        }
    }
}
