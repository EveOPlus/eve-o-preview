using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Thumbnails;

public sealed class ThumbnailRuntimeSettingsUpdatedHandler(IThumbnailManager thumbnails) : INotificationHandler<ThumbnailRuntimeSettingsUpdated>
{
    public Task Handle(ThumbnailRuntimeSettingsUpdated notification, CancellationToken cancellationToken)
    {
        thumbnails.ApplyRuntimeSettings();
        return Task.CompletedTask;
    }
}
