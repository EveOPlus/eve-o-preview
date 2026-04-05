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

using System;
using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Helper;
using Serilog;

namespace EveOPreview.Mediator.Handlers.Configuration
{
    sealed class RefreshHotkeysHandler : IRequestHandler<RefreshHotkeys>
    {
        private readonly IThumbnailConfiguration _config;
        private readonly ILogger _logger;

        public RefreshHotkeysHandler(IThumbnailConfiguration Config, ILogger logger)
        {
            _config = Config;
            _logger = logger;
        }

        public Task Handle(RefreshHotkeys request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.Verbose("RefreshHotkeys: Refreshing all hotkey configurations");
                int cycleGroupCount = _config.CycleGroups.Count;

                foreach (var cycleGroup in _config.CycleGroups)
                {
                    cycleGroup.ForwardHotkeys.RemoveAll(x => x == null);
                    cycleGroup.BackwardHotkeys.RemoveAll(x => x == null);

                    cycleGroup.ForwardHotkeysParsedAndOrdered.Clear();
                    cycleGroup.BackwardHotkeysParsedAndOrdered.Clear();

                    var forwardHotkeys = cycleGroup.ForwardHotkeys?.Select(HotkeyHelpers.ToHotkeys);
                    if (forwardHotkeys != null)
                    {
                        cycleGroup.ForwardHotkeysParsedAndOrdered.AddRange(forwardHotkeys);
                    }

                    var backwardHotkeys = cycleGroup.BackwardHotkeys?.Select(HotkeyHelpers.ToHotkeys);
                    if (backwardHotkeys != null)
                    {
                        cycleGroup.BackwardHotkeysParsedAndOrdered.AddRange(backwardHotkeys);
                    }

                    _logger.Verbose("Cycle group '{Description}': Forward={ForwardCount}, Backward={BackwardCount}",
                        cycleGroup.Description, cycleGroup.ForwardHotkeysParsedAndOrdered.Count, cycleGroup.BackwardHotkeysParsedAndOrdered.Count);
                }

                _config.ToggleHideActiveClientsHotkeyParsed = _config.ToggleHideActiveClientsHotkey.ToHotkeys();
                _config.MinimizeAllClientsHotkeyParsed = _config.MinimizeAllClientsHotkey.ToHotkeys();
                
                _logger.Information("Hotkeys refreshed successfully: {CycleGroupCount} groups configured", cycleGroupCount);
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error refreshing hotkeys");
                return Task.FromException(exception);
            }
        }
    }
}