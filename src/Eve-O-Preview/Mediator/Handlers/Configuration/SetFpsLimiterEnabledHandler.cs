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
    sealed class SetFpsLimiterEnabledHandler : IRequestHandler<SetFpsLimiterEnabled>
    {
        private readonly IThumbnailConfiguration _config;
        private readonly IProcessMonitor _processMonitor;
        private readonly IFpsLimiterService _fpsLimiterService;

        public SetFpsLimiterEnabledHandler(IThumbnailConfiguration config, IProcessMonitor processMonitor, IFpsLimiterService fpsLimiterService)
        {
            _config = config;
            _processMonitor = processMonitor;
            _fpsLimiterService = fpsLimiterService;
        }

        public async Task<Unit> Handle(SetFpsLimiterEnabled request, CancellationToken cancellationToken)
        {
            var allKnownClients = _processMonitor.GetAllProcesses();

            if (_config.FpsLimiterSettings.IsEnabled && _config.IsPremium)
            {
                var tasks = allKnownClients.Select(client => _fpsLimiterService.TryInstallFpsLimiterIntoClientAsync(client));
                await Task.WhenAll(tasks);
            }
            else
            {
                var tasks = allKnownClients.Select(client => _fpsLimiterService.DisableFpsLimiterAsync(client.Handle));
                await Task.WhenAll(tasks);
            }

            return Unit.Value;
        }
    }
}