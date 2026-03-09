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

using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.Presenters;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
    sealed class ThumbnailListUpdatedHandler : INotificationHandler<ThumbnailListUpdated>
    {
        #region Private fields
        private readonly IMainFormPresenter _presenter;
        #endregion

        public ThumbnailListUpdatedHandler(MainFormPresenter presenter)
        {
            this._presenter = presenter;
        }

        public Task Handle(ThumbnailListUpdated notification, CancellationToken cancellationToken)
        {
            if (notification.Added.Count > 0)
            {
                this._presenter.AddThumbnails(notification.Added);
            }

            if (notification.Removed.Count > 0)
            {
                this._presenter.RemoveThumbnails(notification.Removed);
            }
            
            return Task.CompletedTask;
        }
    }
}