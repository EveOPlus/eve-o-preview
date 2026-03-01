using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
    sealed class ThumbnailTitleFontSettingsUpdatedHandler : INotificationHandler<ThumbnailFontTitleSettingsUpdated>
    {
        private readonly IThumbnailManager _manager;

        public ThumbnailTitleFontSettingsUpdatedHandler(IThumbnailManager manager)
        {
            this._manager = manager;
        }

        public Task Handle(ThumbnailFontTitleSettingsUpdated notification, CancellationToken cancellationToken)
        {
            this._manager.UpdateThumbnailTitleFont();

            return Task.CompletedTask;
        }
    }
}