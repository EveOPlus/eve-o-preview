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
using System.Linq;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Services.Interface;
using Serilog;

namespace EveOPreview.Mediator.Handlers.Services
{
    sealed class StartStopServiceHandler : IRequestHandler<StartService>, IRequestHandler<StopService>
    {
        private readonly IThumbnailManager _manager;
        private readonly IProcessMonitor _procMonitor;
        private readonly IHookService _hook;
        private readonly ICpuAffinityService _cpuAffinityService;
        private readonly ILogger _logger;

        public StartStopServiceHandler(IThumbnailManager manager, IProcessMonitor procMonitor, IHookService hook, ICpuAffinityService cpuAffinityService, ILogger logger)
        {
            this._manager = manager;
            _procMonitor = procMonitor;
            _hook = hook;
            _cpuAffinityService = cpuAffinityService;
            _logger = logger;
        }

        public Task Handle(StartService message, CancellationToken cancellationToken)
        {
            try
            {
                _logger.Verbose("StartStopServiceHandler: Starting thumbnail manager service");
                this._manager.Start();
                _logger.Verbose("Thumbnail manager service started successfully");
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error starting thumbnail manager service");
                return Task.FromException(exception);
            }
        }

        public async Task Handle(StopService message, CancellationToken cancellationToken)
        {
            try
            {
                _logger.Information("StartStopServiceHandler: Stopping thumbnail manager service");
                this._manager.Stop();

                var processes = _procMonitor.GetAllProcesses();
                _logger.Information("Resetting CPU affinity and FPS limiter for {ProcessCount} clients", processes.Count);
                
                _cpuAffinityService.ResetAll(processes);
                var tasks = processes.Select(client => _hook.DisableFpsLimiterAsync(client.MainWindowHandle));
                await Task.WhenAll(tasks).ConfigureAwait(false);
                
                _logger.Information("Thumbnail manager service stopped successfully");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error stopping thumbnail manager service");
            }
        }
    }
}