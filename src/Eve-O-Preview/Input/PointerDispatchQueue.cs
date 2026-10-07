using System;
using System.Drawing;

namespace EveOPreview.Input;

/// <summary>One pending dispatcher job, one latest movement and a bounded release tail.</summary>
internal sealed class PointerDispatchQueue
{
    private readonly object _gate = new();
    private readonly PointerSample[] _releases = new PointerSample[8];
    private readonly Action<Action> _post;
    private readonly Action _drain;
    private readonly Func<int> _generation;
    private readonly Action<GlobalPointerEventArgs> _move, _up;
    private PointerSample _latestMove;
    private int _head, _count;
    private bool _hasMove, _pending;
    private readonly record struct PointerSample(Point Position, PointerButtons Button, PointerButtons Buttons, ShortcutKeys Modifiers, int Generation);

    public PointerDispatchQueue(Action<Action> post, Func<int> generation, Action<GlobalPointerEventArgs> move, Action<GlobalPointerEventArgs> up)
    {
        _post = post; _generation = generation; _move = move; _up = up; _drain = Drain;
    }

    public void Enqueue(Point position, PointerButtons button, PointerButtons buttons, ShortcutKeys modifiers, int generation)
    {
        var sample = new PointerSample(position, button, buttons, modifiers, generation);
        bool post;
        // No callbacks, native calls or waits under the packet-copy gate.
        lock (_gate)
        {
            if (button == PointerButtons.None) { _latestMove = sample; _hasMove = true; }
            else
            {
                _hasMove = false; // A release carries the final movement position.
                if (_count == _releases.Length) { _head = (_head + 1) % _releases.Length; _count--; }
                _releases[(_head + _count++) % _releases.Length] = sample;
            }
            post = !_pending; _pending = true;
        }
        if (post) _post(_drain);
    }

    private void Drain()
    {
        // Yield after one bounded batch even under continuous high-rate input.
        for (int i = 0; i <= _releases.Length; i++)
        {
            PointerSample sample;
            lock (_gate)
            {
                if (_count > 0) { sample = _releases[_head]; _head = (_head + 1) % _releases.Length; _count--; }
                else if (_hasMove) { sample = _latestMove; _hasMove = false; }
                else { _pending = false; return; }
            }
            if (sample.Generation != _generation()) continue;
            var args = new GlobalPointerEventArgs(sample.Button, sample.Buttons, sample.Position, sample.Modifiers);
            _move(args);
            if (sample.Button != PointerButtons.None && sample.Generation == _generation()) _up(args);
        }
        bool repost;
        lock (_gate) { repost = _count > 0 || _hasMove; if (!repost) _pending = false; }
        if (repost) _post(_drain);
    }
}
