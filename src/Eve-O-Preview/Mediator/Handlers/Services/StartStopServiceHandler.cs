using System.Linq;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
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

        public StartStopServiceHandler(IThumbnailManager manager, IProcessMonitor procMonitor, IHookService hook)
        {
            this._manager = manager;
            _procMonitor = procMonitor;
            _hook = hook;
        }

        public Task<Unit> Handle(StartService message, CancellationToken cancellationToken)
        {
            this._manager.Start();

            return Unit.Task;
        }

        public async Task<Unit> Handle(StopService message, CancellationToken cancellationToken)
        {
            this._manager.Stop();

            var processes = _procMonitor.GetAllProcesses();
            var tasks = processes.Select(client => _hook.DisableFpsLimiterAsync(client.Handle));
            await Task.WhenAll(tasks).ConfigureAwait(false);

            return Unit.Value;
        }
    }
}