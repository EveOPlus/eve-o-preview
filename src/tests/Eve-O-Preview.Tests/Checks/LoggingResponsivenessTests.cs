using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.View;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class LoggingResponsivenessTests
{
    [Fact]
    public async Task BlockedDiagnosticOutputDoesNotBlockProducersOrGrowWithoutBound()
    {
        using var release = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        var destination = new BlockingSink(entered, release);
        var fileLogger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(destination).CreateLogger();
        var asyncSink = (ILogEventSink)Activator.CreateInstance(typeof(ThumbnailView).Assembly
            .GetType("EveOPreview.Helper.AsyncLogSink")!, fileLogger, 4)!;
        var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(asyncSink).CreateLogger();
        try
        {
            logger.Verbose("first");
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            // The destination remains blocked for this entire burst. Input-thread
            // logging must complete even after filling the four-entry test queue.
            await Task.Run(() =>
            {
                for (int i = 0; i < 1000; i++) logger.Verbose("Switch {Index}", i);
            }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Empty(destination.Events);
        }
        finally { release.Set(); logger.Dispose(); }
        Assert.Contains(destination.Events, entry => entry.RenderMessage() == "first");
        Assert.Contains(destination.Events, entry => entry.Level == LogEventLevel.Warning && entry.RenderMessage().Contains("dropped"));
        Assert.InRange(destination.Events.Count, 2, 6); // First event + bounded queue + overflow summary, drained at shutdown.
    }

    private sealed class BlockingSink(ManualResetEventSlim entered, ManualResetEventSlim release) : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent entry)
        {
            entered.Set();
            release.Wait();
            Events.Enqueue(entry);
        }
    }
}
