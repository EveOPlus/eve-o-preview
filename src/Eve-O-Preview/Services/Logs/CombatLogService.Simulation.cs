using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.UI;

namespace EveOPreview.Services.Logs;

public sealed partial class CombatLogService
{
    private sealed record SimulationCommand(CombatSimulation Request, IReadOnlyList<string> Titles,
        IReadOnlyList<SimulationSource> Sources, TaskCompletionSource<CommandResult> Result);
    private readonly ConcurrentQueue<SimulationCommand> _simulationCommands = new();
    private readonly Dictionary<string, CombatSimulationSequence> _sequences = new(StringComparer.Ordinal);
    private CombatLogStore _simulationStore;
    private int _simulating;
    private long _simulationOffset;
    private string _simulationSource = "";
    private bool _simulationUsesLogParser;
    private readonly record struct DamageIndicators(DateTimeOffset? Any, DateTimeOffset? Player, DateTimeOffset? AnyStarted, DateTimeOffset? PlayerStarted);
    private readonly Dictionary<string, DamageIndicators> _realIndicators = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, DamageIndicators> _simulationIndicators;
    public bool IsSimulating => Volatile.Read(ref _simulating) != 0;
    public event Action SimulationEnded;

    /// <summary>The host resolves visible targets; the worker owns the entire temporary run.
    /// The overview, augments and future CombatEvent subscribers receive normal production data.</summary>
    public async Task<CommandResult> RunSimulationAsync(CombatSimulation request, IReadOnlyList<string> titles)
    {
        if (_disposed) return CommandResult.Error("Log service has stopped.");
        IReadOnlyList<SimulationSource> sources = [];
        if (!request.Stop && request.UseStaticData)
        {
            if (_catalog.StaticData is null) return CommandResult.Error("Download FC static data in Data setup.");
            try { sources = (await _catalog.StaticData.ReadSimulationCatalogAsync().ConfigureAwait(false)).Resolve(request); }
            catch (ArgumentException ex) { return CommandResult.Error(ex.Message); }
        }
        if (_disposed) return CommandResult.Error("Log service has stopped.");
        var result = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _simulationCommands.Enqueue(new(request, titles.ToArray(), sources, result)); Wake(); return await result.Task.ConfigureAwait(false);
    }

    private void ProcessSimulationCommands(CombatLogStore store)
    {
        while (_simulationCommands.TryDequeue(out var command))
        {
            try
            {
                EndSimulation();
                if (!command.Request.Stop)
                {
                    if (!_running || _preferences.Theme == "Legacy" || command.Titles.Count == 0)
                    { command.Result.TrySetResult(CommandResult.Error("Start previews and choose a visible thumbnail.")); continue; }
                    _simulationStore = store?.CreateMemoryCopy() ?? new CombatLogStore(":memory:", _now());
                    _simulationIndicators = new(_realIndicators, StringComparer.OrdinalIgnoreCase);
                    _simulationOffset = 0; _simulationSource = "simulation-" + Guid.NewGuid().ToString("N");
                    _simulationUsesLogParser = command.Request.UseStaticData;
                    foreach (string title in command.Titles)
                    {
                        var options = _settings.Overlays.GetValueOrDefault(title) ?? _settings.DefaultOverlay;
                        _sequences[title] = new(command.Request with { FullTitle = title }, _now(), options.EventDurationSeconds,
                            sources: command.Sources, windowSeconds: _settings.WindowSeconds);
                    }
                    Volatile.Write(ref _simulating, 1);
                    AdvanceSimulation();
                }
                PublishCurrent(store);
                command.Result.TrySetResult(command.Request.Stop ? CommandResult.Ok("Simulation stopped.")
                    : CommandResult.OkFormat($"Simulating on {command.Titles.Count} thumbnail(s)."));
            }
            catch (Exception)
            {
                EndSimulation(); Notify();
                command.Result.TrySetResult(CommandResult.Error("Could not start the temporary simulation."));
            }
        }
    }

    private void AdvanceSimulation()
    {
        if (_simulationStore is null) return;
        if (!_running || _preferences.Theme == "Legacy" || _sequences.Values.All(x => x.Until <= _now())) { EndSimulation(); return; }
        foreach (var sequence in _sequences.Values)
        {
            sequence.Advance(_now());
            foreach (var generated in sequence.DrainEvents())
            {
                // The user-facing simulator creates realistic log text and enters
                // through the live parser. Hidden ammunition or NPC gun metadata
                // must never bypass the evidence rules used for real damage.
                var entry = _simulationUsesLogParser ? EveLogParser.Parse(
                    FormattableString.Invariant($"[ {generated.Timestamp.UtcDateTime:yyyy.MM.dd HH:mm:ss} ] (combat) {generated.Text}"),
                    new(generated.Character), false, _catalog, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orion Voss" })
                    : generated;
                if (entry is null) continue;
                long offset = ++_simulationOffset;
                // Use the same durable-store schema/aggregation and event dispatch as
                // real parsed entries, on an in-memory clone. Real commits also reach
                // this clone while the original store continues to retain only real logs.
                var file = new StoredLogFile("Gamelogs/simulation.txt", new(_simulationSource + entry.Character, 0, offset, "utf-8", ""), new(entry.Character));
                _simulationStore.Commit(file, [new(offset, entry)]);
                AcceptCombatEntry(entry, simulated: true);
            }
        }
    }

    private void AcceptCombatEntry(ParsedLogEntry entry, bool simulated)
    {
        if (entry.Effect == CombatEffect.Damage && entry.Direction == DamageDirection.Incoming && entry.Amount > 0)
        {
            var now = _now();
            void Observe(Dictionary<string, DamageIndicators> indicators)
            {
                var previous = indicators.GetValueOrDefault(entry.Character);
                // Hits extend the deadline without restarting the phase. Otherwise
                // rapid incoming fire would continually restart the visible half.
                DateTimeOffset Start(DateTimeOffset? first, DateTimeOffset? last) => first is { } began && last is { } recent
                    && recent <= now && recent.AddSeconds(_settings.FlashSeconds) > now ? began : now;
                bool player = entry.Kind == CombatantKind.Player;
                indicators[entry.Character] = new(now, player ? now : previous.Player,
                    Start(previous.AnyStarted, previous.Any), player ? Start(previous.PlayerStarted, previous.Player) : previous.PlayerStarted);
            }
            if (!simulated) Observe(_realIndicators);
            if (_simulationIndicators is not null) Observe(_simulationIndicators);
        }
        NotifyCombat(new(entry, simulated));
    }

    private void EndSimulation()
    {
        if (_simulationStore is null) return;
        _simulationStore.Dispose(); _simulationStore = null; _sequences.Clear();
        _simulationIndicators = null;
        Volatile.Write(ref _simulating, 0);
        Volatile.Write(ref _snapshot, _lastRealSnapshot);
        try { SimulationEnded?.Invoke(); }
        catch (Exception ex) { _logger.Warning("Simulation presentation subscriber failed ({ErrorType})", ex.GetType().Name); }
    }

    private void PublishCurrent(CombatLogStore store)
    {
        if ((_simulationStore ?? store) is { } visible) Publish(visible);
        else
        {
            Volatile.Write(ref _snapshot, new(_status, _directory, _now(), _settings.WindowSeconds, [], []));
            _lastRealSnapshot = _snapshot;
            _clock.Change(Timeout.Infinite, Timeout.Infinite); Notify();
        }
    }
}
