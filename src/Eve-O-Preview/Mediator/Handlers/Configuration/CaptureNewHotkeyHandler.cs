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
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EveOPreview.Mediator.Handlers.Configuration
{
    public class CaptureNewHotkeyHandler : IRequestHandler<CaptureNewHotkey, CaptureNewHotkeyResponse>
    {
        private readonly IKeyboardMouseEvents _keyboardMouseEvents;
        private readonly IThumbnailConfiguration _config;
        private readonly ILogger _logger;

        public CaptureNewHotkeyHandler(IKeyboardMouseEvents keyboardMouseEvents, IThumbnailConfiguration config, ILogger logger)
        {
            _keyboardMouseEvents = keyboardMouseEvents;
            _config = config;
            _logger = logger;
        }

        public Task<CaptureNewHotkeyResponse> Handle(CaptureNewHotkey request, CancellationToken cancellationToken)
        {
            _logger.WithCallerInfo().Information("Listening for a new hotkey.");
            
            var result = CaptureNextKeyUp(request);

            if (!result.IsValid)
            {
                return Task.FromResult(result);
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

            return Task.FromResult(result);
        }

        private Dictionary<Keys, string> FindAllHotkeysInCurrentConfig()
        {
            var hotkeysInConfig = new Dictionary<Keys, string>();

            if (_config.ToggleHideActiveClientsHotkeyParsed != Keys.None)
            {
                AddOrMeaningfulError(hotkeysInConfig, _config.ToggleHideActiveClientsHotkeyParsed, "Toggle Hide All Active Clients Hotkey");
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
            if (theDictionary.TryGetValue(hotkeys, out var theExistingLocation))
            {
                throw new HotkeyAlreadyExistsException(hotkeys, theExistingLocation, location);
            }

            theDictionary.Add(hotkeys, location);
        }

        private CaptureNewHotkeyResponse CaptureNextKeyUp(CaptureNewHotkey request)
        {
            var result = new CaptureNewHotkeyResponse();

            KeyEventHandler downHandler = (s, e) =>
            {
                // Ignore if the user is just tapping Ctrl/Shift/Alt by themselves
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey ||
                    e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey ||
                    e.KeyCode == Keys.Menu || e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu)
                    return;

                // Don't allow Windows key all on its own, but with something else is okay.
                if (e.KeyData == Keys.LWin || e.KeyData == Keys.RWin)
                {
                    return;
                }

                result.KeysCaptured = e.KeyData;
            };

            _keyboardMouseEvents.KeyDown += downHandler;

            try
            {
                var sw = Stopwatch.StartNew();
                while (result.KeysCaptured == default(Keys))
                {
                    Application.DoEvents();
                    Thread.Sleep(15);

                    if (sw.ElapsedMilliseconds > request.TimeoutMs)
                    {
                        _logger.WithCallerInfo().Warning($"No hotkey captures for {request.TimeoutMs} ms.");
                        result.ErrorMessage = $"Timed out. Nothing captured for {request.TimeoutMs}ms.";
                        result.IsValid = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"An unexpected error has occured: {ex.Message}";
                result.IsValid = false;
                _logger.WithCallerInfo().Error(ex, "Unhandled exception");
            }
            finally
            {
                _keyboardMouseEvents.KeyDown -= downHandler;
            }

            result.IsValid = true;

            if (result.KeysCaptured == Keys.Escape)
            {
                result.KeysCaptured = Keys.None;
                return result;
            }

            var converter = new KeysConverter();
            result.KeyString = converter.ConvertToString(result.KeysCaptured);
            
            _logger.WithCallerInfo().Information($"Captured hotkey {result.KeyString}");

            return result;
        }
    }
}