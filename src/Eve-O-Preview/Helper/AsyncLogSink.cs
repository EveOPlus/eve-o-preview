using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EveOPreview.Helper;

/// <summary>Bounded diagnostic output: disk writes must never block the input/render thread.</summary>
internal sealed class AsyncLogSink : ILogEventSink, IDisposable
{
    private readonly ILogger _destination;
    private readonly Channel<LogEvent> _events;
    private readonly Task _writer;
    private long _dropped;

    public AsyncLogSink(ILogger destination, int capacity = 2048)
    {
        _destination = destination;
        _events = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        _writer = Task.Run(WriteEvents);
    }

    public void Emit(LogEvent entry)
    {
        // TryWrite never waits, including when the bounded queue is full.
        if (!_events.Writer.TryWrite(entry)) Interlocked.Increment(ref _dropped);
    }

    private async Task WriteEvents()
    {
        try
        {
            await foreach (var entry in _events.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                _destination.Write(entry);
                long dropped = Interlocked.Exchange(ref _dropped, 0);
                if (dropped > 0) _destination.Warning("Diagnostic log queue full; dropped {Count} events to preserve input responsiveness", dropped);
            }
        }
        finally { (_destination as IDisposable)?.Dispose(); }
    }

    public void Dispose()
    {
        _events.Writer.TryComplete();
        // The worker owns disposal too: a blocked write/flush must not hold exit
        // indefinitely or race destination disposal with an unfinished write.
        try { _writer.WaitAsync(TimeSpan.FromMilliseconds(500)).GetAwaiter().GetResult(); }
        catch (TimeoutException) { }
    }
}
