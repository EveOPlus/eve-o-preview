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

using EveOPreview.Helper;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
    sealed class ThumbnailConfiguredSizeUpdatedHandler : INotificationHandler<ThumbnailConfiguredSizeUpdated>
    {
        private readonly IThumbnailManager _manager;
        private readonly ILogger _logger;

        public ThumbnailConfiguredSizeUpdatedHandler(IThumbnailManager manager, ILogger logger)
        {
            this._manager = manager;
            _logger = logger;
        }

        public Task Handle(ThumbnailConfiguredSizeUpdated notification, CancellationToken cancellationToken)
        {
            _logger.WithCallerInfo().Verbose("ThumbnailConfiguredSizeUpdated: Updating thumbnail sizes for all clients");
            this._manager.UpdateThumbnailsSize();

            return Task.CompletedTask;
        }
    }
}