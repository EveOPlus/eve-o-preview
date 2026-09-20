using System;
using System.Collections.Generic;
using Keys = EveOPreview.Input.ShortcutKeys;

namespace EveOPreview.Services.Implementation;

/// <summary>Input-thread-only key state. Unmatched events do not allocate, translate text or call UI code.</summary>
internal sealed class WindowsHotkeyMatcher(Action<Action> dispatch)
{
    private readonly bool[] _down = new bool[256];
    private readonly HotkeyBinding[] _consumed = new HotkeyBinding[256];
    private Dictionary<Keys, HotkeyBinding> _bindings = new();
    private Action<Keys> _capture;
    private Keys _captured;

    public void Configure(Dictionary<Keys, HotkeyBinding> bindings, Func<int, bool> isDown, Action<Keys> capture = null)
    {
        _bindings = bindings;
        _capture = capture;
        _captured = Keys.None;
        Array.Clear(_consumed);
        Array.Clear(_down);
        // Seed held modifiers at install/profile replacement; hook callbacks update subsequent transitions.
        foreach (int key in new[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C }) _down[key] = isDown(key);
    }

    public bool Process(int key, bool down)
    {
        if ((uint)key >= 256) return false;
        _down[key] = down;
        bool modifier = key is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;
        Keys chord = (Keys)key;
        if (_down[0xA0] || _down[0xA1] || _down[0x10]) chord |= Keys.Shift;
        if (_down[0xA2] || _down[0xA3] || _down[0x11]) chord |= Keys.Control;
        if (_down[0xA4] || _down[0xA5] || _down[0x12]) chord |= Keys.Alt;

        if (_capture != null)
        {
            if (modifier) return false;
            if (down && _captured == Keys.None) _captured = chord;
            if (!down && (_captured & Keys.KeyCode) == (Keys)key)
            {
                var complete = _capture;
                _capture = null;
                complete(_captured);
            }
            return true;
        }

        if (!down)
        {
            var consumed = _consumed[key];
            _consumed[key] = null;
            // Use the original down binding even if modifiers were released first.
            if (consumed?.OnRelease == true) dispatch(consumed.Execute);
            return consumed != null;
        }
        // Keep consuming a held shortcut after modifiers change, without selecting another binding.
        if (_consumed[key] is { } held)
        {
            if (!held.OnRelease && _bindings.TryGetValue(chord, out var repeat) && repeat == held) dispatch(held.Execute);
            return true;
        }
        if (modifier || _down[0x5B] || _down[0x5C] || !_bindings.TryGetValue(chord, out var binding)) return false;
        _consumed[key] = binding;
        if (!binding.OnRelease) dispatch(binding.Execute);
        return true;
    }
}
