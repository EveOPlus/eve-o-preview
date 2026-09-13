using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Serilog;

namespace EveOPreview.Services;

/// <summary>UI-thread foreground notifications; never injects a callback into another process.</summary>
internal sealed class ForegroundWindowObserver : IDisposable
{
    private readonly WinEventCallback _callback; // Keep the native delegate rooted until UnhookWinEvent.
    private nint _hook;

    public ForegroundWindowObserver(Action<nint> changed, ILogger logger)
    {
        _callback = (_, _, hwnd, _, _, _, _) =>
        {
            if (_hook == 0 || hwnd == 0) return;
            try { changed(hwnd); }
            catch (Exception ex) { logger.Error(ex, "Updating foreground-client appearance failed"); }
        };
        // EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT. The registering UI
        // thread's existing message loop delivers callbacks, without polling.
        _hook = SetWinEventHook(3, 3, 0, _callback, 0, 0, 0);
        if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_hook == 0) return;
        UnhookWinEvent(_hook);
        _hook = 0;
        GC.KeepAlive(_callback);
    }

    private delegate void WinEventCallback(nint hook, uint eventType, nint hwnd,
        int objectId, int childId, uint threadId, uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module,
        WinEventCallback callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);
}
