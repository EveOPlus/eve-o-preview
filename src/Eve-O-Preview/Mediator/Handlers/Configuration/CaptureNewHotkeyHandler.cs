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

            foreach (var client in _config.ClientHotkey)
            {
                AddOrMeaningfulError(hotkeysInConfig, client.Value.ToHotkeys(), $"Client Hotkey {client.Key}");
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