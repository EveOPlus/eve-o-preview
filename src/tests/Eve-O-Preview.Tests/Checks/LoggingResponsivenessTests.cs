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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownDoesNotWaitIndefinitelyForDiagnosticWriteOrDisposal(bool blockDisposal)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var disposed = new ManualResetEventSlim();
        var destination = new ShutdownSink(entered, release, disposed, blockDisposal);
        var fileLogger = new LoggerConfiguration().WriteTo.Sink(destination).CreateLogger();
        var asyncSink = (ILogEventSink)Activator.CreateInstance(typeof(ThumbnailView).Assembly
            .GetType("EveOPreview.Helper.AsyncLogSink")!, fileLogger, 4)!;
        var logger = new LoggerConfiguration().WriteTo.Sink(asyncSink).CreateLogger();
        Task exit = null;
        try
        {
            logger.Information("Last diagnostic before exit");
            if (!blockDisposal) Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            exit = Task.Run(logger.Dispose, TestContext.Current.CancellationToken);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            await exit.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Assert.False(disposed.IsSet); // Exit returned while the destination was still blocked.
        }
        finally
        {
            release.Set();
            if (exit is not null) await exit.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            else logger.Dispose();
            Assert.True(disposed.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
    }

    private sealed class ShutdownSink(ManualResetEventSlim entered, ManualResetEventSlim release,
        ManualResetEventSlim disposed, bool blockDisposal) : ILogEventSink, IDisposable
    {
        public void Emit(LogEvent entry)
        {
            if (!blockDisposal) { entered.Set(); release.Wait(); }
        }
        public void Dispose()
        {
            if (blockDisposal) { entered.Set(); release.Wait(); }
            disposed.Set();
        }
    }

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
