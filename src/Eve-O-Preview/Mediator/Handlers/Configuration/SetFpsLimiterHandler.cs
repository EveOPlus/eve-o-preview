using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Mediator.Handlers.Configuration
{
    sealed class SetFpsLimiterHandler : IRequestHandler<SetFpsLimiter>
    {
        private readonly IProcessMonitor _processMonitor;
        private readonly IFpsLimiterService _fpsLimiterService;

        public SetFpsLimiterHandler(IProcessMonitor processMonitor, IFpsLimiterService fpsLimiterService)
        {
            _processMonitor = processMonitor;
            _fpsLimiterService = fpsLimiterService;
        }

        public async Task<Unit> Handle(SetFpsLimiter request, CancellationToken cancellationToken)
        {
            var allKnownClients = _processMonitor.GetAllProcesses();

            var tasks = allKnownClients.Select(client => _fpsLimiterService.UpdateTargetFpsAsync(client.Handle));
            await Task.WhenAll(tasks);

            return Unit.Value;
        }
    }
}