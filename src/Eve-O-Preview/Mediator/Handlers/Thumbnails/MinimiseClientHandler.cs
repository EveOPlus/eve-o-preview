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
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
    public class MinimizeClientHandler : IRequestHandler<MinimizeClient>
    {
        private readonly IWindowManager _windowManager;
        private readonly ILogger _logger;

        public MinimizeClientHandler(IWindowManager windowManager, ILogger logger)
        {
            _windowManager = windowManager;
            _logger = logger;
        }

        public Task Handle(MinimizeClient request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.Verbose("MinimizeClient: Minimizing window handle 0x{Handle:X}", request.HandleToMinimize);
                _windowManager.MinimizeWindow(request.HandleToMinimize, true);
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error minimizing client window 0x{Handle:X}", request.HandleToMinimize);
                return Task.FromException(exception);
            }
        }
    }
}