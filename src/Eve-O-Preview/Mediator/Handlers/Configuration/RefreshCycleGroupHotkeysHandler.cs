using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Mediator.Handlers.Configuration
{
    sealed class RefreshCycleGroupHotkeysHandler : IRequestHandler<RefreshCycleGroupHotkeys>
    {
        private readonly IThumbnailConfiguration _config;

        public RefreshCycleGroupHotkeysHandler(IThumbnailConfiguration Config)
        {
            _config = Config;
        }

        public async Task<Unit> Handle(RefreshCycleGroupHotkeys request, CancellationToken cancellationToken)
        {
            foreach (var cycleGroup in _config.CycleGroups)
            {
                cycleGroup.ForwardHotkeysParsedAndOrdered.Clear();
                cycleGroup.BackwardHotkeysParsedAndOrdered.Clear();

                var forwardHotkeys = cycleGroup.ForwardHotkeys?.Select(x => _config.StringToKey(x));
                if (forwardHotkeys != null)
                {
                    cycleGroup.ForwardHotkeysParsedAndOrdered.AddRange(forwardHotkeys);
                }

                var backwardHotkeys = cycleGroup.BackwardHotkeys?.Select(x => _config.StringToKey(x));
                if (backwardHotkeys != null)
                {
                    cycleGroup.BackwardHotkeysParsedAndOrdered.AddRange(backwardHotkeys);
                }
            }

            return Unit.Value;
        }
    }
}