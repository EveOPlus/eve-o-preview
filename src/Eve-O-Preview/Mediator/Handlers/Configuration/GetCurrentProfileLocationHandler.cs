using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Configuration;

public class GetCurrentProfileLocationHandler : IRequestHandler<GetCurrentProfileLocation, ProfileLocation>
{
    private readonly IConfigurationStorage _configStorage;
    private readonly IProfileManager _profileManager;

    public GetCurrentProfileLocationHandler(IConfigurationStorage configStorage, IProfileManager profileManager)
    {
        _configStorage = configStorage;
        _profileManager = profileManager;
    }

    public Task<ProfileLocation> Handle(GetCurrentProfileLocation request, CancellationToken cancellationToken)
    {
        var currentConfigProfile = _configStorage.CurrentProfile;

        if (string.IsNullOrWhiteSpace(currentConfigProfile?.FullPath))
        {
            currentConfigProfile = _profileManager.GetDefaultProfileLocation();
        }

        return Task.FromResult(currentConfigProfile);
    }
}