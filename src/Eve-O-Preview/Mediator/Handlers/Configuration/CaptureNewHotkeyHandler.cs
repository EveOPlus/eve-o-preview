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

using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Excpetions;
using EveOPreview.Helper;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EveOPreview.Mediator.Handlers.Configuration
{
    public class CaptureNewHotkeyHandler : IRequestHandler<CaptureNewHotkey, CaptureNewHotkeyResponse>
    {
        private readonly IHotkeyService _hotkeys;
        private readonly IThumbnailConfiguration _config;
        private readonly ILogger _logger;

        public CaptureNewHotkeyHandler(IHotkeyService hotkeys, IThumbnailConfiguration config, ILogger logger)
        {
            _hotkeys = hotkeys;
            _config = config;
            _logger = logger;
        }

        public async Task<CaptureNewHotkeyResponse> Handle(CaptureNewHotkey request, CancellationToken cancellationToken)
        {
            _logger.WithCallerInfo().Information("Listening for a new hotkey.");
            
            CaptureNewHotkeyResponse result;
            try
            {
                string shortcut = await _hotkeys.CaptureAsync(TimeSpan.FromMilliseconds(request.TimeoutMs), cancellationToken).ConfigureAwait(false);
                result = new() { IsValid = true, KeyString = shortcut, KeysCaptured = shortcut.ToHotkeys() };
            }
            catch (TimeoutException) { return new() { ErrorMessage = "Timed out. No shortcut was captured." }; }
            catch (OperationCanceledException) { return new() { ErrorMessage = "Shortcut recording cancelled." }; }
            catch (Exception ex)
            {
                _logger.Error(ex, "Shortcut capture failed");
                return new() { ErrorMessage = "Shortcut recording could not start. Try again or restart EVE-O." };
            }

            if (!result.IsValid)
            {
                return result;
            }

            try
            {
                var hotkeysInConfig = FindAllHotkeysInCurrentConfig();
                AddOrMeaningfulError(hotkeysInConfig, result.KeysCaptured, "The new hotkey captured");

            }
            catch (Exception ex)
            {
                if (ex is HotkeyAlreadyExistsException hke && 
                    (hke.Keys == request.KeysString.ToHotkeys() || hke.Keys == Keys.None))
                {
                    result.IsValid = true;
                }
                else
                {
                    result.IsValid = false;
                    result.ErrorMessage = ex.Message;
                    _logger.WithCallerInfo().Error(ex, "Failed to validate new hotkey");
                }
            }

            return result;
        }

        private Dictionary<Keys, string> FindAllHotkeysInCurrentConfig()
        {
            var hotkeysInConfig = new Dictionary<Keys, string>();

            if (_config.ToggleHideActiveClientsHotkeyParsed != Keys.None)
            {
                AddOrMeaningfulError(hotkeysInConfig, _config.ToggleHideActiveClientsHotkeyParsed, "Toggle Hide All Active Clients");
            }

            if (_config.MinimizeAllClientsHotkeyParsed != Keys.None)
            {
                AddOrMeaningfulError(hotkeysInConfig, _config.MinimizeAllClientsHotkeyParsed, "Minimize All Clients");
            }

            foreach (var cycleGroup in _config.CycleGroups)
            {
                foreach (var forwardKeyString in cycleGroup.ForwardHotkeys)
                {
                    AddOrMeaningfulError(hotkeysInConfig, forwardKeyString.ToHotkeys(), $"Cycle Group {cycleGroup.Description} Forward");
                }

                foreach (var backwardKeyString in cycleGroup.BackwardHotkeys)
                {
                    AddOrMeaningfulError(hotkeysInConfig, backwardKeyString.ToHotkeys(), $"Cycle Group {cycleGroup.Description} Backward");
                }
            }

            return hotkeysInConfig;
        }

        private void AddOrMeaningfulError(Dictionary<Keys, string> theDictionary, Keys hotkeys, string location)
        {
            if (hotkeys == Keys.None) return;
            if (theDictionary.TryGetValue(hotkeys, out var theExistingLocation))
            {
                throw new HotkeyAlreadyExistsException(hotkeys, theExistingLocation, location);
            }

            theDictionary.Add(hotkeys, location);
        }

    }
}
