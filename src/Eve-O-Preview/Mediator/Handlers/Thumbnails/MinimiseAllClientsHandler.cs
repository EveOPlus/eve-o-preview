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
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
    public class MinimizeAllClientsHandler : IRequestHandler<MinimizeAllClients>
    {
        private readonly IWindowManager _windowManager;
        private readonly IProcessMonitor _processMonitor;
        private readonly ILogger _logger;

        public MinimizeAllClientsHandler(IWindowManager windowManager, IProcessMonitor processMonitor, ILogger logger)
        {
            _windowManager = windowManager;
            _processMonitor = processMonitor;
            _logger = logger;
        }

        public Task Handle(MinimizeAllClients request, CancellationToken cancellationToken)
        {
            try
            {
                var allProcesses = _processMonitor.GetAllProcesses();
                _logger.WithCallerInfo().Verbose("MinimizeAllClients handler: Minimizing {ProcessCount} processes", allProcesses.Count);
                
                int minimizedCount = 0;
                foreach (var process in allProcesses)
                {
                    _logger.Verbose("Minimizing process: {Title} (Handle: 0x{Handle:X})", process.Title, process.MainWindowHandle);
                    _windowManager.MinimizeWindow(process.MainWindowHandle, true);
                    minimizedCount++;
                }

                _logger.Verbose("MinimizeAllClients completed: {MinimizedCount} processes minimized", minimizedCount);
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error while minimizing all clients");
                return Task.FromException(exception);
            }
        }
    }
}