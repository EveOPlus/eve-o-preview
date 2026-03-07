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
        private readonly IHookService _hookService;

        public SetFpsLimiterEnabledHandler(IThumbnailConfiguration config, IProcessMonitor processMonitor, IHookService hookService)
        {
            _config = config;
            _processMonitor = processMonitor;
            _hookService = hookService;
        }

        public async Task<Unit> Handle(SetFpsLimiterEnabled request, CancellationToken cancellationToken)
        {
            var allKnownClients = _processMonitor.GetAllProcesses();

            if (_config.FpsLimiterSettings.IsEnabled && _config.IsPremium)
            {
                var tasks = allKnownClients.Select(client => _hookService.TryInstallHooksAsync(client));
                await Task.WhenAll(tasks);
            }
            else
            {
                var tasks = allKnownClients.Select(client => _hookService.DisableFpsLimiterAsync(client.Handle));
                await Task.WhenAll(tasks);
            }

            return Unit.Value;
        }
    }
}