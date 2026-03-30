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

namespace EveOPreview.Mediator.Handlers.Services
{
    sealed class StartStopServiceHandler : IRequestHandler<StartService>, IRequestHandler<StopService>
    {
        private readonly IThumbnailManager _manager;
        private readonly IProcessMonitor _procMonitor;
        private readonly IHookService _hook;
        private readonly ICpuAffinityService _cpuAffinityService;

        public StartStopServiceHandler(IThumbnailManager manager, IProcessMonitor procMonitor, IHookService hook, ICpuAffinityService cpuAffinityService)
        {
            this._manager = manager;
            _procMonitor = procMonitor;
            _hook = hook;
            _cpuAffinityService = cpuAffinityService;
        }

        public Task Handle(StartService message, CancellationToken cancellationToken)
        {
            try
            {
                this._manager.Start();
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        }

        public async Task Handle(StopService message, CancellationToken cancellationToken)
        {
            this._manager.Stop();

            var processes = _procMonitor.GetAllProcesses();
            _cpuAffinityService.ResetAll(processes);
            var tasks = processes.Select(client => _hook.DisableFpsLimiterAsync(client.MainWindowHandle));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}