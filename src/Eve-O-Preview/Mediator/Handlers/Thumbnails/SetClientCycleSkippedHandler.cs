using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Thumbnails;

public sealed class SetClientCycleSkippedHandler(IThumbnailConfiguration configuration) : IRequestHandler<SetClientCycleSkipped>
{
    public Task Handle(SetClientCycleSkipped request, CancellationToken cancellationToken)
    {
        configuration.SetClientCycleSkipped(request.Title, request.Skipped);
        return Task.CompletedTask;
    }
}
