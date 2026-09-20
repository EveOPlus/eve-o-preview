using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Serilog;

namespace EveOPreview.Input;

/// <summary>Installs a mouse-only global hook while a move/resize subscription exists.</summary>
public sealed class WindowsGlobalPointerInput : IGlobalPointerInput
{
    private const int CommandMessage = 0x8000 + 75;
    private readonly ILogger _logger;
    private readonly Thread _thread;
    private readonly HookProcedure _callback;
    private readonly PointerDispatchQueue _dispatch;
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WindowsMessageWindow _window;
    private IntPtr _hook;
    private EventHandler<GlobalPointerEventArgs> _move, _up;
    private bool _disposed;
    private int _generation;

    public WindowsGlobalPointerInput(ILogger logger)
    {
        _logger = logger;
        _callback = Callback;
        _dispatch = new PointerDispatchQueue(action => Dispatcher.UIThread.Post(action, DispatcherPriority.Input),
            () => Volatile.Read(ref _generation), args => { if (!_disposed) _move?.Invoke(this, args); },
            args => { if (!_disposed) _up?.Invoke(this, args); });
        var desktop = GetThreadDesktop(GetCurrentThreadId());
        _thread = new Thread(() => Run(desktop)) { IsBackground = true, Name = "EVE-O pointer input" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Task.GetAwaiter().GetResult();
    }

    public event EventHandler<GlobalPointerEventArgs> MouseMove
    {
        add
        {
            Dispatcher.UIThread.VerifyAccess(); _move += value;
            try { UpdateSubscription(); } catch { _move -= value; throw; }
        }
        remove { Dispatcher.UIThread.VerifyAccess(); _move -= value; UpdateSubscription(removing: true); }
    }
    public event EventHandler<GlobalPointerEventArgs> MouseUp
    {
        add
        {
            Dispatcher.UIThread.VerifyAccess(); _up += value;
            try { UpdateSubscription(); } catch { _up -= value; throw; }
        }
        remove { Dispatcher.UIThread.VerifyAccess(); _up -= value; UpdateSubscription(removing: true); }
    }
    public Point Position
    {
        get { if (!GetCursorPos(out var position)) throw new Win32Exception(Marshal.GetLastWin32Error()); return position; }
        set { if (!SetCursorPos(value.X, value.Y)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }
    private static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    public PointerButtons Buttons => (Down(1) ? PointerButtons.Left : 0) | (Down(2) ? PointerButtons.Right : 0) |
        (Down(4) ? PointerButtons.Middle : 0) | (Down(5) ? PointerButtons.XButton1 : 0) | (Down(6) ? PointerButtons.XButton2 : 0);
    public ShortcutKeys Modifiers => (Down(0x10) ? ShortcutKeys.Shift : 0) | (Down(0x11) ? ShortcutKeys.Control : 0) | (Down(0x12) ? ShortcutKeys.Alt : 0);

    private void UpdateSubscription(bool removing = false)
    {
        if (_disposed)
        {
            if (removing) return;
            throw new ObjectDisposedException(nameof(WindowsGlobalPointerInput));
        }
        bool listen = _move != null || _up != null;
        try { Invoke(() =>
        {
            if (listen && _hook == IntPtr.Zero)
            {
                Interlocked.Increment(ref _generation);
                _hook = SetWindowsHookEx(14, _callback, GetModuleHandle(null), 0);
                if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else if (!listen) RemoveHook();
        }); }
        catch (Exception ex) when (removing && ex is InvalidOperationException or Win32Exception or TimeoutException)
        {
            // Releasing an owned window must remain possible after native input has
            // failed. A new subscription still reports failure and rolls back above.
            Interlocked.Increment(ref _generation);
            _logger.Warning(ex, "Pointer input was unavailable while removing a subscription");
        }
    }

    private IntPtr Callback(int code, IntPtr message, IntPtr data)
    {
        try
        {
            int id = message.ToInt32();
            if (code >= 0 && (id == 0x200 || id is 0x202 or 0x205 or 0x208 or 0x20C))
            {
                var position = new Point(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4));
                var button = id switch { 0x202 => PointerButtons.Left, 0x205 => PointerButtons.Right, 0x208 => PointerButtons.Middle,
                    0x20C => (Marshal.ReadInt32(data, 8) >> 16) == 1 ? PointerButtons.XButton1 : PointerButtons.XButton2, _ => PointerButtons.None };
                _dispatch.Enqueue(position, button, Buttons & ~button, Modifiers, Volatile.Read(ref _generation));
            }
        }
        catch (Exception ex) { ThreadPool.QueueUserWorkItem(_ => _logger.Error(ex, "Pointer callback failed")); }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    private void RemoveHook()
    {
        Interlocked.Increment(ref _generation);
        if (_hook == IntPtr.Zero) return;
        if (!UnhookWindowsHookEx(_hook)) throw new Win32Exception(Marshal.GetLastWin32Error());
        _hook = IntPtr.Zero;
    }
    private void Run(IntPtr desktop)
    {
        try
        {
            if (!SetThreadDesktop(desktop)) throw new Win32Exception(Marshal.GetLastWin32Error());
            _window = new WindowsMessageWindow((message, _, _) =>
            {
                if (message != CommandMessage) return false;
                while (_commands.TryDequeue(out var action)) action();
                return true;
            });
            _ready.TrySetResult();
            WindowsMessageWindow.Run();
        }
        catch (Exception ex) { _ready.TrySetException(ex); _logger.Error(ex, "Pointer input thread failed"); }
        finally
        {
            try { RemoveHook(); } catch (Exception ex) { _logger.Error(ex, "Pointer hook cleanup failed"); }
            finally { _window?.Dispose(); }
        }
    }
    private void Invoke(Action action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_thread.IsAlive) throw new InvalidOperationException("The pointer input thread has stopped.");
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() => { try { action(); done.TrySetResult(); } catch (Exception ex) { done.TrySetException(ex); } });
        if (!PostMessage(_window.Handle, CommandMessage, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        done.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            if (_thread.IsAlive) Invoke(() => { try { RemoveHook(); } finally { WindowsMessageWindow.ExitThread(); } });
        }
        catch (Exception ex) { _logger.Warning(ex, "Pointer input was unavailable during disposal"); }
        _disposed = true; _move = null; _up = null;
        Interlocked.Increment(ref _generation);
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int hook, HookProcedure callback, IntPtr module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetThreadDesktop(IntPtr desktop);
}
