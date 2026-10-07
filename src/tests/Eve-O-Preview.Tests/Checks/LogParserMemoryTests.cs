using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class LogParserMemoryTests
{
    [Fact]
    public void UnrelatedMessagesDoNotCreateMatchersAndMatchingKeepsLinearEngine()
    {
        using var cache = new LogRegexCache();
        var pattern = new LogTextPattern(@"^(?<amount>[0-9]+) from (?<peer>.+?)$", "from", cache);
        Assert.False(pattern.Match("214 to Sample Pilot").Success);
        Assert.Equal(0, cache.Count);
        Assert.Equal("Sample Pilot", pattern.Match("214 from Sample Pilot").Groups["peer"].Value);
        Assert.Equal(1, cache.Count);
        var regex = cache.Get(pattern.ToString());
        Assert.True(regex.Options.HasFlag(RegexOptions.NonBacktracking));
        Assert.Equal(TimeSpan.FromMilliseconds(250), regex.MatchTimeout);
    }

    [Fact]
    public void WorkingSetIsBoundedAndRecentlyUsedMatchersSurviveEviction()
    {
        var time = new ManualTime();
        using var cache = new LogRegexCache(2, time);
        var first = cache.Get("^first$");
        time.Advance(TimeSpan.FromSeconds(1));
        var second = cache.Get("^second$");
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Same(first, cache.Get("^first$"));
        cache.Get("^third$");
        Assert.Equal(2, cache.Count);
        Assert.Same(first, cache.Get("^first$"));
        Assert.NotSame(second, cache.Get("^second$"));
        // Eviction only removes cache ownership, so a current caller stays safe.
        Assert.Matches(second, "second");
    }

    [Fact]
    public void IdleTimerReleasesMatchersWithoutAnotherLogAndRestartsForNewActivity()
    {
        var time = new ManualTime();
        using var cache = new LogRegexCache(time: time);
        var weak = Populate(cache);
        time.Advance(LogRegexCache.IdleLifetime);
        Assert.Equal(0, cache.Count);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.IsAlive);
        Assert.False(time.TimerActive);
        Assert.Matches(cache.Get("^new$"), "new");
        Assert.True(time.TimerActive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Populate(LogRegexCache cache) => new(cache.Get("^expired$"));

    [Fact]
    public void ActiveMatchersExtendOnlyTheirOwnIdleLifetime()
    {
        var time = new ManualTime();
        using var cache = new LogRegexCache(time: time);
        var active = cache.Get("^active$");
        cache.Get("^idle$");
        time.Advance(TimeSpan.FromSeconds(20));
        Assert.Same(active, cache.Get("^active$"));
        time.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(1, cache.Count);
        Assert.Same(active, cache.Get("^active$"));
    }

    [Fact]
    public void ConcurrentReadersShareOneMatcher()
    {
        using var cache = new LogRegexCache();
        var matches = new Regex[16];
        Parallel.For(0, matches.Length, i => matches[i] = cache.Get("^shared$"));
        Assert.All(matches, regex => Assert.Same(matches[0], regex));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void AllLanguageMetadataStaysSmallWithoutInitializingRegexEngines()
    {
        // Warm only the small template helpers; measure constructing a fresh
        // complete catalog, independently of any other test's shared cache.
        EveLogTemplates.Rule(1, "notify", LogEventKind.Navigation, "Sample {object}.");
        long before = GC.GetAllocatedBytesForCurrentThread();
        var catalog = EveLogLanguageCatalog.Create();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(8, catalog.Length);
        Assert.Equal(1264, catalog.Sum(x => x.Messages.Length));
        Assert.True(allocated < 16 * 1024 * 1024, $"Metadata allocated {allocated:N0} bytes.");
    }

    [Fact]
    public void LiteralFilterPreservesFlexibleWhitespaceColonsBranchesAndOptionalWeapons()
    {
        var spacing = EveLogTemplates.Rule(1, "notify", LogEventKind.Navigation, "Status: {object} ready.");
        Assert.True(spacing.Text.Match("Status：\u00a0Sample Pilot\u202fready.").Success);
        var branch = EveLogTemplates.Rule(2, "notify", LogEventKind.Navigation,
            "{[character]actor.gender -> \"He\", \"She\"} is ready.");
        Assert.True(branch.Text.Match("He is ready.").Success);
        Assert.True(branch.Text.Match("She is ready.").Success);
        var repair = EveLogTemplates.Rule(3, "combat", LogEventKind.Repair,
            "{amount} remote shield boosted to {specialObject} - {[item]type.name}");
        Assert.True(repair.Text.Match("214 remote shield boosted to Sample Pilot").Success);
        Assert.True(repair.Text.Match("214 remote shield boosted to Sample Pilot - Sample Module").Success);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        private ManualTimer _timer;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal bool TimerActive => _timer.Active;
        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
            => _timer = new ManualTimer(this, callback, state, dueTime, period);
        internal void Advance(TimeSpan duration)
        {
            _ticks += duration.Ticks;
            if (_timer.Active) _timer.Fire();
        }
        private sealed class ManualTimer(ManualTime time, TimerCallback callback, object state, TimeSpan due, TimeSpan period) : ITimer
        {
            private long _next = due == Timeout.InfiniteTimeSpan ? long.MaxValue : time._ticks + due.Ticks;
            private TimeSpan _period = period;
            internal bool Active => _next != long.MaxValue;
            internal void Fire()
            {
                if (time._ticks < _next) return;
                _next = _period == Timeout.InfiniteTimeSpan ? long.MaxValue : time._ticks + _period.Ticks;
                callback(state);
            }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            { _period = period; _next = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : time._ticks + dueTime.Ticks; return true; }
            public void Dispose() => _next = long.MaxValue;
            public ValueTask DisposeAsync() { Dispose(); return default; }
        }
    }
}
