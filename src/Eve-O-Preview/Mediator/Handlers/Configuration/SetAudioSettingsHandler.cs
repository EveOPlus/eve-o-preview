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
    sealed class SetAudioSettingsHandler : IRequestHandler<SetAudioSettings>
    {
        private readonly IProcessMonitor _processMonitor;
        private readonly IHookService _hookService;

        public SetAudioSettingsHandler(IProcessMonitor processMonitor, IHookService hookService)
        {
            _processMonitor = processMonitor;
            _hookService = hookService;
        }

        public async Task<Unit> Handle(SetAudioSettings request, CancellationToken cancellationToken)
        {
            var allKnownClients = _processMonitor.GetAllProcesses();

            var initTasks = allKnownClients.Select(client => _hookService.TryInstallHooksAsync(client));
            await Task.WhenAll(initTasks);

            var tasks = allKnownClients.Select(client => _hookService.UpdateMutedAudioAsync(client.Handle));
            await Task.WhenAll(tasks);

            return Unit.Value;
        }
    }
}