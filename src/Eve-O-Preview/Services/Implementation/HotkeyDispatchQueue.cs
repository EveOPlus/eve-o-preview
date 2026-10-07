using System;
using System.Threading;
using System.Threading.Channels;

namespace EveOPreview.Services.Implementation;

/// <summary>Bounded, ordered input-to-UI handoff. Never waits for UI work in a native callback.</summary>
internal sealed class HotkeyDispatchQueue(Action<Action> post, Action<Exception> onError)
{
    private readonly Channel<(int Generation, Action Execute)> _pending = Channel.CreateBounded<(int, Action)>(
        new BoundedChannelOptions(64) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest });
    private int _generation;
    private int _scheduled;

    public void Invalidate() => Interlocked.Increment(ref _generation);

    public void Enqueue(Action execute)
    {
        _pending.Writer.TryWrite((Volatile.Read(ref _generation), execute));
        Schedule();
    }

    private void Schedule()
    {
        if (Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0) post(Drain);
    }

    private void Drain()
    {
        // A finite batch preserves UI liveness during auto-repeat. No delay/debounce/timer.
        for (int count = 0; count < 8 && _pending.Reader.TryRead(out var item); count++)
        {
            if (item.Generation != Volatile.Read(ref _generation)) continue;
            try { item.Execute(); }
            catch (Exception ex) { onError(ex); }
        }
        Volatile.Write(ref _scheduled, 0);
        if (_pending.Reader.TryPeek(out _)) Schedule();
    }
}
