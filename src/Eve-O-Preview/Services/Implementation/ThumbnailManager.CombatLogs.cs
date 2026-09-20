using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Preview;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using EveOPreview.View;

namespace EveOPreview.Services;

sealed partial class ThumbnailManager
{
    private CombatLogService _combatLogs;
    private ApplicationPreferences _logPreferences;
    private Dispatcher _logDispatcher;
    private int _logRefreshQueued;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CombatOverlayEvent> _pendingCombat = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (CombatOverlayEvent Event, DateTimeOffset Until)> _combatEvents = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer _combatExpiry;
    private DateTimeOffset? _nextDamageFlash;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CombatOverlayEvent> _pendingRealCombat = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (CombatOverlayEvent Event, DateTimeOffset Until)> _realCombatEvents = new(StringComparer.OrdinalIgnoreCase);
    private int _restoreRealCombat;

    private void InitializeCombatLogs(CombatLogService logs, ApplicationPreferences preferences)
    {
        _combatLogs = logs; _logPreferences = preferences; _logDispatcher = Dispatcher.UIThread;
        if (logs is not null)
        {
            logs.LogsChanged += QueueCombatOverlays; logs.CombatEvent += ReceiveCombatEvent; logs.SimulationRequested += SimulateCombatEvent;
            logs.CurrentSystems.Changed += QueueCombatOverlays;
            logs.SimulationEnded += RestoreRealCombat;
            _combatExpiry = new DispatcherTimer(DispatcherPriority.Background);
            _combatExpiry.Tick += (_, _) =>
            {
                _combatExpiry.Stop();
                if (_combatEvents.Values.Any(x => x.Until <= DateTimeOffset.UtcNow)) ApplyCombatOverlays();
                else ApplyDamageFlashes();
            };
        }
        if (preferences is not null) preferences.Changed += QueueCombatOverlays;
    }

    private void QueueCombatOverlays()
    {
        if (_stopped || Interlocked.Exchange(ref _logRefreshQueued, 1) != 0) return;
        _logDispatcher.Post(() =>
        {
            Interlocked.Exchange(ref _logRefreshQueued, 0);
            if (!_stopped) ApplyCombatOverlays();
        }, DispatcherPriority.Background);
    }

    private void ApplyCombatOverlays()
    {
        if (_thumbnailViews is null) return;
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _pendingRealCombat.ToArray())
        {
            if (_pendingRealCombat.TryRemove(pair.Key, out var value))
            {
                var settings = _combatLogs.ReadLogSettings();
                var options = settings.Overlays.GetValueOrDefault("EVE - " + value.Entry.Character) ?? settings.DefaultOverlay;
                _realCombatEvents[pair.Key] = (value, now.AddSeconds(options.EventDurationSeconds));
            }
        }
        foreach (var title in _realCombatEvents.Where(x => x.Value.Until <= now).Select(x => x.Key).ToArray()) _realCombatEvents.Remove(title);
        if (Interlocked.Exchange(ref _restoreRealCombat, 0) != 0)
        {
            _combatEvents.Clear();
            foreach (var pair in _realCombatEvents) _combatEvents[pair.Key] = pair.Value;
        }
        foreach (var pair in _pendingCombat.ToArray())
            if (_pendingCombat.TryRemove(pair.Key, out var value)) SetCombatEvent(pair.Key, value);
        _nextDamageFlash = null;
        foreach (var view in _thumbnailViews.Values) ApplyCombatOverlay(view);
        foreach (var title in _combatEvents.Where(x => x.Value.Until <= DateTimeOffset.UtcNow).Select(x => x.Key).ToArray()) _combatEvents.Remove(title);
        ScheduleCombatWake();
    }

    private void ScheduleCombatWake()
    {
        DateTimeOffset? next = _combatEvents.Count > 0 ? _combatEvents.Values.Min(x => x.Until) : null;
        if (_nextDamageFlash is { } flash && (next is null || flash < next)) next = flash;
        _combatExpiry?.Stop();
        if (next is { } wake && !_stopped)
        {
            _combatExpiry.Interval = TimeSpan.FromMilliseconds(Math.Max(1, (wake - DateTimeOffset.UtcNow).TotalMilliseconds));
            _combatExpiry.Start();
        }
        else if (_stopped) _combatEvents.Clear();
    }

    private void ApplyDamageFlashes()
    {
        // Fade ticks change retained opacity/colour only. They never format DPS,
        // reconstruct appearance dictionaries, read logs or query the database.
        if (_thumbnailViews is null || _combatLogs is null) return;
        _nextDamageFlash = null;
        var settings = _combatLogs.ReadLogSettings();
        var snapshot = _combatLogs.ReadLogs();
        var now = DateTimeOffset.UtcNow;
        bool available = !_stopped && _logPreferences?.Theme != "Legacy" && (settings.Enabled || _combatLogs.IsSimulating);
        foreach (var view in _thumbnailViews.Values)
            if (view is ThumbnailView thumbnail && !thumbnail.IsDisposed)
            {
                var character = view.Title?.StartsWith("EVE - ", StringComparison.Ordinal) == true
                    ? snapshot.Characters.FirstOrDefault(x => x.Name.Equals(view.Title[6..], StringComparison.OrdinalIgnoreCase)) : null;
                ApplyDamageFlash(thumbnail, character, settings, now, available);
            }
        ScheduleCombatWake();
    }

    private void ApplyDamageFlash(ThumbnailView thumbnail, CharacterCombatSnapshot character, CombatLogSettings settings, DateTimeOffset now, bool available)
    {
        var flash = available ? CombatDamageFlash.Evaluate(character, settings, now) : default;
        thumbnail.SetDamageFlash(flash.TitleHighlighted ? CombatOverlayFormatter.Color(settings.FlashColor) : null, flash.ThumbnailTint, flash.Intensity);
        if (flash.NextChangeAt is { } next && (_nextDamageFlash is null || next < _nextDamageFlash)) _nextDamageFlash = next;
    }

    private void ApplyCombatOverlay(IThumbnailView view)
    {
        if (_combatLogs is null || view is not ThumbnailView thumbnail || thumbnail.IsDisposed) return;
        var settings = _combatLogs.ReadLogSettings();
        var options = settings.Overlays.GetValueOrDefault(view.Title) ?? settings.DefaultOverlay;
        var appearance = options.GetAppearance();
        var snapshot = _combatLogs.ReadLogs();
        var character = view.Title?.StartsWith("EVE - ", StringComparison.Ordinal) == true
            ? snapshot.Characters.FirstOrDefault(x => x.Name.Equals(view.Title[6..], StringComparison.OrdinalIgnoreCase)) : null;
        IReadOnlyList<OverlayStat> stats = [];
        bool receiving = settings.Enabled || _combatLogs.IsSimulating;
        bool available = !_stopped && _logPreferences?.Theme != "Legacy";
        ApplyDamageFlash(thumbnail, character, settings, DateTimeOffset.UtcNow, available && receiving);
        thumbnail.SetSystemName(available && options.SolarSystem ? _combatLogs.CurrentSystems.GetSystem(view.Title?.StartsWith("EVE - ", StringComparison.Ordinal) == true ? view.Title[6..] : "") ?? "" : "",
            CombatOverlayFormatter.Color(appearance.SystemColor), options.SystemPlacement, options.SystemFontSize);
        if (available && receiving && view.Title?.StartsWith("EVE - ", StringComparison.Ordinal) == true)
        {
            CombatOverlayEvent Active(string lane) => _combatEvents.TryGetValue(view.Title + "\0" + lane, out var active)
                && active.Until > DateTimeOffset.UtcNow ? active.Event : null;
            stats = CombatOverlayFormatter.Meter(character, options, Active("in"), Active("out"));
        }
        thumbnail.SetOverlayStats(stats, options.GetStatsStyle());
        thumbnail.SetTitlePosition(available ? options.TitlePosition : OverlayPosition.TopLeft);
    }

    private void DisposeCombatLogs()
    {
        if (_combatLogs is not null)
        {
            _combatLogs.LogsChanged -= QueueCombatOverlays; _combatLogs.CombatEvent -= ReceiveCombatEvent;
            _combatLogs.SimulationRequested -= SimulateCombatEvent;
            _combatLogs.CurrentSystems.Changed -= QueueCombatOverlays;
            _combatLogs.SimulationEnded -= RestoreRealCombat;
        }
        _combatExpiry?.Stop(); _pendingCombat.Clear(); _combatEvents.Clear(); _pendingRealCombat.Clear(); _realCombatEvents.Clear();
        if (_logPreferences is not null) _logPreferences.Changed -= QueueCombatOverlays;
    }

    private void ReceiveCombatEvent(CombatOverlayEvent notification)
    {
        // Repair rates come from the complete rolling snapshot, not latest-event coalescing.
        if (_stopped || notification.Entry.Effect != CombatEffect.Damage) return;
        string lane = notification.Entry.Direction == DamageDirection.Incoming ? "in" : "out";
        string key = "EVE - " + notification.Entry.Character + "\0" + lane;
        if (!notification.Simulated) _pendingRealCombat[key] = notification;
        _pendingCombat[key] = notification;
        QueueCombatOverlays();
    }
    private void SetCombatEvent(string key, CombatOverlayEvent notification)
    {
        var settings = _combatLogs.ReadLogSettings();
        var options = settings.Overlays.GetValueOrDefault("EVE - " + notification.Entry.Character) ?? settings.DefaultOverlay;
        _combatEvents[key] = (notification, DateTimeOffset.UtcNow.AddSeconds(options.EventDurationSeconds));
    }
    private void RestoreRealCombat()
    {
        _pendingCombat.Clear(); Interlocked.Exchange(ref _restoreRealCombat, 1); QueueCombatOverlays();
    }
    private Task<CommandResult> SimulateCombatEvent(CombatSimulation simulation)
    {
        Task<CommandResult> Error(string message) => Task.FromResult(CommandResult.Error(message));
        if (!_logDispatcher.CheckAccess()) return Error("Run simulation from the workspace UI.");
        if (simulation.Stop) return _combatLogs.RunSimulationAsync(simulation, []);
        if (_stopped || _logPreferences?.Theme == "Legacy") return Error("Start previews and use a modern theme to simulate.");
        var targets = _thumbnailViews.Values.OfType<ThumbnailView>().Where(x => x.IsActive && x.Visible && !x.IsDisposed
            && x.Title?.StartsWith("EVE - ", StringComparison.Ordinal) == true && (simulation.AllVisibleThumbnails || x.Title == simulation.FullTitle)).ToArray();
        if (targets.Length == 0) return Error("Show a character's thumbnail before simulating.");
        return _combatLogs.RunSimulationAsync(simulation, targets.Select(x => x.Title).Distinct(StringComparer.Ordinal).ToArray());
    }
}
