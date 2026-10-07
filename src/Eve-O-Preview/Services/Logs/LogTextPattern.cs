#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace EveOPreview.Services.Logs;

/// <summary>Keep template metadata cheap; only plausible messages need a regex.</summary>
internal sealed class LogTextPattern(string expression, string requiredLiteral, LogRegexCache? cache = null)
{
    public Match Match(string text) => !text.Contains(requiredLiteral, StringComparison.Ordinal)
        ? System.Text.RegularExpressions.Match.Empty
        : (cache ?? LogRegexCache.Shared).Get(expression).Match(text);

    // Language signatures must not instantiate matchers or extend their lifetime.
    public override string ToString() => expression;
}

/// <summary>
/// Nonbacktracking matchers retain sizeable Unicode decision-tree caches. Share
/// only a bounded working set, and release idle entries even when no logs arrive.
/// A caller's local reference keeps an evicted matcher safe for an in-flight match.
/// </summary>
internal sealed class LogRegexCache : IDisposable
{
    internal const int MaximumPatterns = 32;
    internal static readonly TimeSpan IdleLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);
    internal static LogRegexCache Shared { get; } = new();
    private sealed class Entry(Regex regex, long lastUse)
    {
        internal Regex Regex { get; } = regex;
        internal long LastUse = lastUse;
    }
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private bool _disposed;

    internal LogRegexCache(int capacity = MaximumPatterns, TimeProvider? time = null)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _time = time ?? TimeProvider.System;
        _timer = _time.CreateTimer(_ => Expire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    internal int Count { get { lock (_gate) return _entries.Count; } }

    internal Regex Get(string expression)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            long now = _time.GetTimestamp();
            RemoveExpired(now);
            if (_entries.TryGetValue(expression, out var entry))
            {
                entry.LastUse = now;
                return entry.Regex;
            }
            if (_entries.Count >= _capacity)
                _entries.Remove(_entries.MinBy(x => x.Value.LastUse).Key);
            var regex = new Regex(expression, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                TimeSpan.FromMilliseconds(250));
            if (_entries.Count == 0) _timer.Change(SweepInterval, SweepInterval);
            _entries.Add(expression, new(regex, _time.GetTimestamp()));
            return regex;
        }
    }

    private void RemoveExpired(long now)
    {
        // The cache is small. Avoid an extra expiry list on every parsed line.
        foreach (var pair in _entries)
            if (_time.GetElapsedTime(pair.Value.LastUse, now) >= IdleLifetime) _entries.Remove(pair.Key);
    }

    private void Expire()
    {
        lock (_gate)
        {
            if (_disposed) return;
            RemoveExpired(_time.GetTimestamp());
            if (_entries.Count == 0) _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
            _entries.Clear();
        }
    }
}
