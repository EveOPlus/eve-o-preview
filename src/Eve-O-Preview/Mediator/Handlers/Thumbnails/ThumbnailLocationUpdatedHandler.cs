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

using EveOPreview.Configuration;
using EveOPreview.Helper;
using EveOPreview.Mediator.Messages;
using MediatR;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
    sealed class ThumbnailLocationUpdatedHandler : INotificationHandler<ThumbnailLocationUpdated>
    {
        private readonly IMediator _mediator;
        private readonly IThumbnailConfiguration _configuration;
        private readonly ILogger _logger;

        public ThumbnailLocationUpdatedHandler(IMediator mediator, IThumbnailConfiguration configuration, ILogger logger)
        {
            this._mediator = mediator;
            this._configuration = configuration;
            _logger = logger;
        }

        public Task Handle(ThumbnailLocationUpdated notification, CancellationToken cancellationToken)
        {
            _logger.WithCallerInfo().Verbose("Thumbnail location updated: {ThumbnailName} ActiveClient={ActiveClientName} Location=({X},{Y})", 
                notification.ThumbnailName, notification.ActiveClientName, notification.Location.X, notification.Location.Y);
            
            this._configuration.SetThumbnailLocation(notification.ThumbnailName, notification.ActiveClientName, notification.Location);

            return this._mediator.Send(new SaveConfiguration(), cancellationToken);
        }
    }
}