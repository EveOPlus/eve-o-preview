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
using EveOPreview.Helper;
using EveOPreview.Mediator.Messages;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Serilog;

namespace EveOPreview.Mediator.Handlers.Configuration
{
    sealed class SaveConfigurationHandler : IRequestHandler<SaveConfiguration>
    {
        private readonly IConfigurationStorage _storage;
        private readonly IThumbnailConfiguration _config;
        private readonly ILogger _logger;

        public SaveConfigurationHandler(IConfigurationStorage storage, IThumbnailConfiguration config, ILogger logger)
        {
            this._storage = storage;
            _config = config;
            _logger = logger;
        }

        public Task Handle(SaveConfiguration message, CancellationToken cancellationToken)
        {
            try
            {
                _logger.WithCallerInfo().Verbose("Saving configuration to storage");
                this._storage.Save();
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to save configuration");
                return Task.FromException(exception);
            }
        }
    }
}