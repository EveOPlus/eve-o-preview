using System;
using System.Collections.Concurrent;

namespace EveOPreview.Services.Logs;

/// <summary>Shared current-system knowledge, independent of combat totals and the visible client list.
/// Log replay and future ESI observations update the same value. Older observations cannot roll it back.</summary>
public sealed class CharacterSystemCache
{
    private sealed record KnownSystem(string Name, DateTimeOffset ObservedAt);
    private readonly ConcurrentDictionary<string, KnownSystem> _systems = new(StringComparer.OrdinalIgnoreCase);
    public event Action Changed;

    public string GetSystem(string character) => _systems.TryGetValue(character, out var system) ? system.Name : null;

    public void Observe(string character, string system, DateTimeOffset observedAt)
    {
        if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(system)) return;
        var next = new KnownSystem(system, observedAt);
        while (true)
        {
            if (_systems.TryGetValue(character, out var previous))
            {
                if (observedAt < previous.ObservedAt || previous == next) return;
                if (!_systems.TryUpdate(character, next, previous)) continue;
                if (previous.Name != system) Changed?.Invoke();
            }
            else
            {
                if (!_systems.TryAdd(character, next)) continue;
                Changed?.Invoke();
            }
            return;
        }
    }
}
