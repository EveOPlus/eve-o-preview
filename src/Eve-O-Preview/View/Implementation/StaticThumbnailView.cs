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
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Services;
using Gma.System.MouseKeyHook;
using MediatR;

namespace EveOPreview.View
{
    sealed class StaticThumbnailView : ThumbnailView
    {
        #region Private fields
        private readonly PictureBox _thumbnail;
        private IThumbnailConfiguration _config;
        #endregion

        public StaticThumbnailView(IWindowManager windowManager, IThumbnailConfiguration config, IThumbnailManager thumbnailManager, IMediator mediator, IKeyboardMouseEvents kbmEvents)
            : base(windowManager, config, thumbnailManager, mediator, kbmEvents)
        {
            this._thumbnail = new StaticThumbnailImage
            {
                TabStop = false,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Location = new Point(0, 0),
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height)
            };
            this.Controls.Add(this._thumbnail);
            this._config = config;
        }

        protected override void RefreshThumbnail(bool forceRefresh)
        {
            if (!forceRefresh)
            {
                return;
            }

            var thumbnail = this.WindowManager.GetStaticThumbnail(this.Id);
            if (thumbnail != null)
            {
                var oldImage = this._thumbnail.Image;
                this._thumbnail.Image = thumbnail;
                oldImage?.Dispose();
            }
        }

        protected override void ResizeThumbnail(int baseWidth, int baseHeight, int highlightWidthTop, int highlightWidthRight, int highlightWidthBottom, int highlightWidthLeft)
        {
            var left = 0 + highlightWidthLeft;
            var top = 0 + highlightWidthTop;
            if (this.IsLocationUpdateRequired(this._thumbnail.Location, left, top))
            {
                this._thumbnail.Location = new Point(left, top);
            }

            var width = baseWidth - highlightWidthLeft - highlightWidthRight;
            var height = baseHeight - highlightWidthTop - highlightWidthBottom;
            if (this.IsSizeUpdateRequired(this._thumbnail.Size, width, height))
            {
                this._thumbnail.Size = new Size(width, height);
            }
        }

        private bool IsLocationUpdateRequired(Point currentLocation, int left, int top)
        {
            return (currentLocation.X != left) || (currentLocation.Y != top);
        }

        private bool IsSizeUpdateRequired(Size currentSize, int width, int height)
        {
            return (currentSize.Width != width) || (currentSize.Height != height);
        }
    }
}