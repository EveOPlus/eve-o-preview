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
using EveOPreview.Mediator.Messages.Process;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using MediatR;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Mediator.Handlers.Process;

public class ResetAllCpuAffinityHandler : IRequestHandler<ResetAllCpuAffinity>
{
    private readonly ILogger _logger;
    private readonly IProcessMonitor _processMonitor;
    private readonly ICpuAffinityService _cpyAffinityService;

    public ResetAllCpuAffinityHandler(ILogger logger, IProcessMonitor processMonitor, ICpuAffinityService cpyAffinityService)
    {
        _logger = logger;
        _processMonitor = processMonitor;
        _cpyAffinityService = cpyAffinityService;
    }

    public Task Handle(ResetAllCpuAffinity request, CancellationToken cancellationToken)
    {
        _logger.WithCallerInfo().Verbose("ResetAllCpuAffinity handler invoked");
        
        var allProcesses = _processMonitor.GetAllProcesses();
        _logger.Verbose("Retrieved {ProcessCount} processes for CPU affinity reset", allProcesses.Count);

        _cpyAffinityService.ResetAll(allProcesses);

        return Task.CompletedTask;
    }
}