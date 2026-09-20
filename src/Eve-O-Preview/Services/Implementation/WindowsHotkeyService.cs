using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using Serilog;

namespace EveOPreview.Services.Implementation;

/// <summary>
/// Both Windows input mechanisms live on a dedicated message-loop thread. The hook only
/// matches/suppresses keys and posts work; it never waits for the UI, focus, graphics or IPC.
/// </summary>
public sealed class WindowsHotkeyService : IHotkeyService
{
    private const int CommandMessage = 0x8000 + 73, PassedInputMessage = 0x8000 + 74, HotkeyMessage = 0x0312;
    private readonly ILogger _logger;
    private readonly HotkeyDispatchQueue _dispatch;
    private readonly HotkeyDispatchQueue _passedDispatch;
    private readonly WindowsHotkeyMatcher _matcher;
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly object _lifetime = new();
    private readonly Thread _thread;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HookProc _hookCallback; // Root delegate for the entire native hook lifetime.
    private InputWindow _window;
    private IntPtr _hook;
    private Dictionary<Keys, HotkeyBinding> _bindings = new();
    private readonly Dictionary<int, HotkeyBinding> _registered = new();
    private HotkeyMode _mode;
    private bool _diagnosticPassthrough;
    private bool _passingInput;
    private Action _actionAfterPassthrough;
    private readonly Queue<Action> _passedActions = new();
    private bool _passedInputPosted;
    private TaskCompletionSource<Keys> _capture;
    private bool _disposed;
    private FormattableString[] _warnings = [];
    public IReadOnlyList<FormattableString> RegistrationWarnings => Volatile.Read(ref _warnings);
    public string RegistrationWarning => string.Join(" ", RegistrationWarnings.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public WindowsHotkeyService(ILogger logger) : this(logger, UiPost(DispatcherPriority.Send), UiPost(DispatcherPriority.Background)) { }

    internal WindowsHotkeyService(ILogger logger, Action<Action> post, Action<Action> postPassed = null)
    {
        _logger = logger;
        _dispatch = new(post, ex => logger.Error(ex, "Hotkey action failed"));
        // Diagnostic actions yield to already pending input; normal hotkeys keep Send priority.
        _passedDispatch = new(postPassed ?? post, ex => logger.Error(ex, "Diagnostic hotkey action failed"));
        _matcher = new(action =>
        {
            if (_passingInput) _actionAfterPassthrough = action;
            else _dispatch.Enqueue(action);
        });
        _hookCallback = KeyboardCallback;
        var desktop = GetThreadDesktop(GetCurrentThreadId());
        _thread = new Thread(() => Run(desktop)) { IsBackground = true, Name = "EVE-O keyboard input" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Task.GetAwaiter().GetResult();
    }

    private static Action<Action> UiPost(DispatcherPriority priority)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        // Send priority is processed under continuous input. This is BeginInvoke, never Invoke.
        return action => dispatcher.BeginInvoke(priority, action);
    }

    public void Replace(IReadOnlyList<HotkeyBinding> bindings, HotkeyMode mode, bool diagnosticPassthrough = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parsed = new Dictionary<Keys, HotkeyBinding>();
        var invalid = new List<string>();
        var converter = new KeysConverter();
        foreach (var binding in bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Shortcut)) continue;
            try
            {
                var key = (Keys)converter.ConvertFromInvariantString(binding.Shortcut);
                if (key == Keys.None) continue;
                int code = (int)(key & Keys.KeyCode);
                if (code == 0 || code > 255 || code is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5)
                    throw new ArgumentException("A shortcut needs a non-modifier key.");
                parsed.TryAdd(key, binding); // Same first-match priority as the former event subscribers.
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or NotSupportedException)
            { invalid.Add(binding.Shortcut); }
        }
        Invoke(() =>
        {
            InvalidateDispatch();
            _bindings = parsed;
            _mode = mode;
            _diagnosticPassthrough = diagnosticPassthrough && mode == HotkeyMode.Global;
            // A profile refresh during capture replaces the dormant set, never the capture hook.
            if (_capture == null) ApplyNative();
            if (invalid.Count > 0) _warnings = [.. _warnings, $"Invalid shortcuts: {string.Join(", ", invalid)}"];
        });
    }

    public async Task<string> CaptureAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var completion = new TaskCompletionSource<Keys>(TaskCreationOptions.RunContinuationsAsynchronously);
        Invoke(() =>
        {
            if (_capture != null) throw new InvalidOperationException("A shortcut is already being recorded.");
            InvalidateDispatch();
            RemoveNative(); // Release every RegisterHotKey subscription BEFORE listening.
            _capture = completion;
            try
            {
                _matcher.Configure(new(), IsDown, key => completion.TrySetResult(key));
                InstallHook();
            }
            catch { _capture = null; ApplyNative(); throw; }
        });
        try
        {
            var key = await completion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return (key & Keys.KeyCode) == Keys.Escape ? null : new KeysConverter().ConvertToInvariantString(key);
        }
        finally
        {
            Invoke(() =>
            {
                _capture = null;
                InvalidateDispatch();
                ApplyNative(); // Success, Escape, timeout and cancellation all restore the latest profile.
            }, ignoreDisposed: true);
        }
    }

    private void ApplyNative()
    {
        RemoveNative();
        _warnings = [];
        if (_bindings.Count == 0) return; // No keyboard hook at all without bindings/capture.
        if (_mode == HotkeyMode.Global)
        {
            _matcher.Configure(_bindings, IsDown);
            try { InstallHook(); }
            catch (Win32Exception ex)
            {
                _warnings = [$"Global shortcuts could not start. Try Windows hotkeys or restart EVE-O."];
                _logger.Warning(ex, "Could not install keyboard hook");
            }
            return;
        }
        var unavailable = new List<string>();
        int id = 0;
        foreach (var (chord, binding) in _bindings)
        {
            uint modifiers = 0;
            if ((chord & Keys.Alt) != 0) modifiers |= 1;
            if ((chord & Keys.Control) != 0) modifiers |= 2;
            if ((chord & Keys.Shift) != 0) modifiers |= 4;
            // Preserve OS repeat behavior. MOD_NOREPEAT would change held-key cycling.
            if (++id < 0xC000 && RegisterHotKey(_window.Handle, id, modifiers, (uint)(chord & Keys.KeyCode)))
                _registered.Add(id, binding);
            else unavailable.Add(binding.Shortcut);
        }
        if (unavailable.Count > 0)
        {
            _warnings = [$"Windows could not register these shortcuts (they may be reserved or in use): {string.Join(", ", unavailable)}"];
            _logger.Warning("{HotkeyWarning}", RegistrationWarning);
        }
    }

    private static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private void InstallHook()
    {
        _hook = SetWindowsHookEx(13, _hookCallback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private void RemoveNative()
    {
        if (_hook != IntPtr.Zero)
        {
            if (!UnhookWindowsHookEx(_hook)) throw new Win32Exception(Marshal.GetLastWin32Error());
            _hook = IntPtr.Zero;
        }
        foreach (int id in _registered.Keys)
            if (!UnregisterHotKey(_window.Handle, id)) throw new Win32Exception(Marshal.GetLastWin32Error());
        _registered.Clear();
        // WM_HOTKEY already queued for an old profile must not fire a reused registration ID.
        while (PeekMessage(out _, _window.Handle, HotkeyMessage, HotkeyMessage, 1)) { }
    }

    private IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
    {
        Action passedAction = null;
        if (code >= 0 && (message == (IntPtr)0x100 || message == (IntPtr)0x104 || message == (IntPtr)0x101 || message == (IntPtr)0x105))
        {
            // No logging, text conversion, configuration reads, native activation or UI waits here.
            try
            {
                // Recording still suppresses its input and never runs hotkey actions.
                _passingInput = _diagnosticPassthrough && _capture == null;
                _actionAfterPassthrough = null;
                bool matched = _matcher.Process(Marshal.ReadInt32(data), message == (IntPtr)0x100 || message == (IntPtr)0x104);
                passedAction = _actionAfterPassthrough;
                if (matched && !_passingInput)
                    return (IntPtr)1;
            }
            catch (Exception ex)
            {
                // Never let a managed exception escape across the native callback boundary.
                ThreadPool.QueueUserWorkItem(_ => _logger.Error(ex, "Keyboard callback failed"));
            }
            finally { _passingInput = false; _actionAfterPassthrough = null; }
        }
        var result = CallNextHookEx(IntPtr.Zero, code, message, data);
        // Diagnostic-only handoff: pass the original event onward first, then post to our
        // input loop so UI dispatch cannot start inside this callback. This is not an
        // acknowledgement that the target application has processed its input/game frame.
        if (passedAction != null) QueuePassedAction(passedAction);
        return result;
    }

    private void QueuePassedAction(Action action)
    {
        if (_passedActions.Count == 64) _passedActions.Dequeue();
        _passedActions.Enqueue(action);
        if (_passedInputPosted) return;
        _passedInputPosted = PostMessage(_window.Handle, PassedInputMessage, IntPtr.Zero, IntPtr.Zero);
        if (!_passedInputPosted) _passedActions.Clear();
    }

    private void InvalidateDispatch()
    {
        _dispatch.Invalidate();
        _passedDispatch.Invalidate();
        _passedActions.Clear();
    }

    private void Run(IntPtr desktop)
    {
        try
        {
            if (!SetThreadDesktop(desktop)) throw new Win32Exception(Marshal.GetLastWin32Error());
            _window = new InputWindow(this);
            _ready.TrySetResult();
            Application.Run();
        }
        catch (Exception ex) { _ready.TrySetException(ex); _logger.Error(ex, "Keyboard input thread failed"); }
        finally
        {
            if (_window != null)
            {
                try { RemoveNative(); }
                catch (Exception ex) { _logger.Error(ex, "Keyboard cleanup failed"); }
                _window.DestroyHandle();
            }
        }
    }

    private void Invoke(Action action, bool ignoreDisposed = false)
    {
        lock (_lifetime)
        {
            if (_disposed)
            {
                if (ignoreDisposed) return;
                throw new ObjectDisposedException(nameof(WindowsHotkeyService));
            }
            if (Thread.CurrentThread == _thread) { action(); return; }
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _commands.Enqueue(() =>
            {
                try { action(); done.SetResult(); }
                catch (Exception ex) { done.SetException(ex); }
            });
            if (!PostMessage(_window.Handle, CommandMessage, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            done.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Invoke(() =>
        {
            _disposed = true;
            InvalidateDispatch();
            _capture?.TrySetCanceled();
            _capture = null;
            try { RemoveNative(); }
            finally { Application.ExitThread(); }
        }, ignoreDisposed: true);
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    private sealed class InputWindow : NativeWindow
    {
        private readonly WindowsHotkeyService _owner;
        public InputWindow(WindowsHotkeyService owner)
        {
            _owner = owner;
            CreateHandle(new CreateParams { Parent = (IntPtr)(-3), Caption = "EVE-O input" });
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == CommandMessage)
            {
                while (_owner._commands.TryDequeue(out var action)) action();
                return;
            }
            if (message.Msg == HotkeyMessage)
            {
                if (_owner._capture == null && _owner._registered.TryGetValue(message.WParam.ToInt32(), out var binding))
                    _owner._dispatch.Enqueue(binding.Execute);
                return;
            }
            if (message.Msg == PassedInputMessage)
            {
                _owner._passedInputPosted = false;
                while (_owner._passedActions.TryDequeue(out var action)) _owner._passedDispatch.Enqueue(action);
                return;
            }
            base.WndProc(ref message);
        }
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage { public IntPtr Hwnd; public uint Message; public IntPtr WParam, LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int hook, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out NativeMessage message, IntPtr window, int first, int last, uint remove);
}
