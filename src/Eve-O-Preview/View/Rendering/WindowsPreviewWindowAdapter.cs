using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace EveOPreview.View.Rendering;

/// <summary>
/// Windows policy for an Avalonia preview top-level. All public geometry is in
/// native pixels; only Width/Height cross the Avalonia device-independent boundary.
/// The adapter does not own the window or any image/compositor relationship.
/// </summary>
public sealed class WindowsPreviewWindowAdapter : IDisposable
{
    private readonly Window _window;
    private readonly bool _clickThrough;
    private readonly Win32Properties.CustomWindowStylesCallback _styles;
    private readonly Win32Properties.CustomWndProcHookCallback _messages;
    private bool _disposed;
    private double _opacity = 1;
    private bool _layeredOpacity;

    public WindowsPreviewWindowAdapter(Window window, bool clickThrough = false)
    {
        _window = window;
        _clickThrough = clickThrough;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        _styles = Styles;
        _messages = Messages;
        Win32Properties.AddWindowStylesCallback(window, _styles);
        Win32Properties.AddWndProcHookCallback(window, _messages);
        EnsureStyles();
    }

    public IntPtr Handle => _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    public Point Location
    {
        get { GetWindowRect(Handle, out var rectangle); return new(rectangle.Left, rectangle.Top); }
        set => _window.Position = new PixelPoint(value.X, value.Y);
    }
    public Size ClientSize
    {
        get { GetClientRect(Handle, out var rectangle); return new(rectangle.Right, rectangle.Bottom); }
        set
        {
            int width = Math.Max(1, value.Width), height = Math.Max(1, value.Height);
            // Avalonia keeps its retained layout in DIPs; the native correction
            // avoids repeated truncation at fractional monitor scales.
            _window.Width = width / _window.RenderScaling;
            _window.Height = height / _window.RenderScaling;
            var rectangle = new NativeRect { Right = width, Bottom = height };
            if (!AdjustWindowRectExForDpi(ref rectangle, (uint)GetWindowLongPtr(Handle, -16).ToInt64(), false,
                    (uint)GetWindowLongPtr(Handle, -20).ToInt64(), GetDpiForWindow(Handle))) return;
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, 0x0016);
        }
    }
    public Size Size
    {
        get { GetWindowRect(Handle, out var rectangle); return new(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top); }
        set
        {
            var oldClient = ClientSize; var oldOuter = Size;
            ClientSize = new(Math.Max(1, value.Width - oldOuter.Width + oldClient.Width), Math.Max(1, value.Height - oldOuter.Height + oldClient.Height));
        }
    }
    public Rectangle ClientScreenBounds => new(PointToScreen(Point.Empty), ClientSize);
    public Point PointToScreen(Point point) { var native = new NativePoint { X = point.X, Y = point.Y }; ClientToScreen(Handle, ref native); return new(native.X, native.Y); }
    public Point PointToClient(Point point) { var native = new NativePoint { X = point.X, Y = point.Y }; ScreenToClient(Handle, ref native); return new(native.X, native.Y); }

    public void EnsureStyles()
    {
        if (_disposed || Handle == IntPtr.Zero) return;
        uint style = (uint)GetWindowLongPtr(Handle, -16).ToInt64(), extended = (uint)GetWindowLongPtr(Handle, -20).ToInt64();
        var next = Styles(style, extended);
        if (next.Style != style) SetWindowLongPtr(Handle, -16, new IntPtr(next.Style));
        if (next.ExStyle != extended) SetWindowLongPtr(Handle, -20, new IntPtr(next.ExStyle));
    }

    public bool Restore(bool topmost, bool raise)
    {
        EnsureStyles();
        if (IsIconic(Handle)) ShowWindow(Handle, 4); // SW_SHOWNOACTIVATE
        uint flags = 0x0001 | 0x0002 | 0x0010 | 0x0040 | 0x0200; // no size/move/activate/owner-order; show
        if (!raise) flags |= 0x0004;
        return SetWindowPos(Handle, topmost ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, flags);
    }

    public void SetOpacity(double opacity)
    {
        _opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;
        _layeredOpacity |= _opacity < 1;
        EnsureStyles();
        if (_layeredOpacity) SetLayeredWindowAttributes(Handle, 0, (byte)Math.Round(_opacity * 255), 2);
    }

    private (uint Style, uint ExStyle) Styles(uint style, uint extended)
    {
        style &= ~(0x00080000u | 0x00020000u | 0x00010000u); // no system/ minimize/maximize commands
        extended = (extended | 0x08000080u) & ~0x00040000u; // noactivate/toolwindow, no appwindow
        if (_clickThrough) extended |= 0x00000020;
        if (_layeredOpacity) extended |= 0x00080000;
        return (style, extended);
    }

    private IntPtr Messages(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0021) { handled = true; return new IntPtr(3); } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
        if (_clickThrough && message == 0x0084) { handled = true; return new IntPtr(-1); }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Win32Properties.RemoveWindowStylesCallback(_window, _styles);
        Win32Properties.RemoveWndProcHookCallback(_window, _messages);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRectExForDpi(ref NativeRect rectangle, uint style, bool menu, uint extended, uint dpi);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
}
