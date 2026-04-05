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
using EveOPreview.Mediator.Messages;
using MediatR;
using Serilog;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Helper;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
    public class ChangeSelectedProfileHandler : IRequestHandler<ChangeSelectedProfile>
    {
        private readonly IConfigurationStorage _configStorage;
        private readonly IPublisher _publisher;
        private readonly ILogger _logger;

        public ChangeSelectedProfileHandler(IConfigurationStorage configStorage, IMediator publisher, ILogger logger)
        {
            _configStorage = configStorage;
            _publisher = publisher;
            _logger = logger;
        }

        public async Task Handle(ChangeSelectedProfile notification, CancellationToken ct)
        {
            _logger.WithCallerInfo().Information("ChangeSelectedProfileHandler: Switching to profile location: {ProfileLocation}", notification.NewProfileLocation);
            _configStorage.CurrentProfile = notification.NewProfileLocation;
            _logger.Verbose("Loading configuration from new profile");
            _configStorage.Load();

            await _publisher.Publish(new SelectedProfileChangedNotification(notification.NewProfileLocation), ct);
            _logger.Verbose("Profile change completed successfully");
        }
    }
}