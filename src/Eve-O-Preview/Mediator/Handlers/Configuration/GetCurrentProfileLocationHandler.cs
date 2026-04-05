//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using MediatR;
using Serilog;

namespace EveOPreview.Mediator.Handlers.Configuration;

public class GetCurrentProfileLocationHandler : IRequestHandler<GetCurrentProfileLocation, ProfileLocation>
{
    private readonly IConfigurationStorage _configStorage;
    private readonly IProfileManager _profileManager;
    private readonly ILogger _logger;

    public GetCurrentProfileLocationHandler(IConfigurationStorage configStorage, IProfileManager profileManager, ILogger logger)
    {
        _configStorage = configStorage;
        _profileManager = profileManager;
        _logger = logger;
    }

    public Task<ProfileLocation> Handle(GetCurrentProfileLocation request, CancellationToken cancellationToken)
    {
        var currentConfigProfile = _configStorage.CurrentProfile;

        if (string.IsNullOrWhiteSpace(currentConfigProfile?.FullPath))
        {
            _logger.Verbose("GetCurrentProfileLocation: Current profile path is null/empty, retrieving default profile");
            currentConfigProfile = _profileManager.GetDefaultProfileLocation();
            _logger.Information("GetCurrentProfileLocation: Using default profile: {ProfilePath}", currentConfigProfile?.FullPath);
        }
        else
        {
            _logger.Verbose("GetCurrentProfileLocation: Current profile: {ProfilePath}", currentConfigProfile.FullPath);
        }

        return Task.FromResult(currentConfigProfile);
    }
}