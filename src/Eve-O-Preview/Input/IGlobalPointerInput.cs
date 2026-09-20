using System;
using System.Drawing;

namespace EveOPreview.Input;

[Flags]
public enum PointerButtons { None = 0, Left = 1, Right = 2, Middle = 4, XButton1 = 8, XButton2 = 16 }

public sealed class GlobalPointerEventArgs(PointerButtons button, PointerButtons buttons, Point location, ShortcutKeys modifiers) : EventArgs
{
    public PointerButtons Button { get; } = button;
    public PointerButtons Buttons { get; } = buttons;
    public Point Location { get; } = location;
    public int X => Location.X;
    public int Y => Location.Y;
    public ShortcutKeys Modifiers { get; } = modifiers;
}

/// <summary>Global desktop-pixel gestures. Events run on the UI owner and subscriptions own the native hook.</summary>
public interface IGlobalPointerInput : IDisposable
{
    event EventHandler<GlobalPointerEventArgs> MouseMove;
    event EventHandler<GlobalPointerEventArgs> MouseUp;
    Point Position { get; set; }
    PointerButtons Buttons { get; }
    ShortcutKeys Modifiers { get; }
}
