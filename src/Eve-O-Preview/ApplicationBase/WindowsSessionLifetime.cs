using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace EveOPreview;

/// <summary>
/// Avalonia 11.3.20's hidden dispatch window raises desktop ShutdownRequested during
/// WM_QUERYENDSESSION. Windows has not committed to ending the session at that point.
/// Intercept only the session pair so cancelled OS queries preserve windows, drafts and services.
/// See Avalonia 11.3.20 Win32Platform.WndProc; revisit when changing framework versions.
/// </summary>
internal sealed class WindowsSessionLifetime : IDisposable
{
    private readonly SubclassProcedure _procedure;
    private readonly Action _confirmed;
    private readonly ILogger _logger;
    internal IntPtr MessageWindow { get; private set; }

    public WindowsSessionLifetime(Action confirmed, ILogger logger)
    {
        _confirmed = confirmed; _logger = logger; _procedure = Procedure;
        EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
        {
            var name = new StringBuilder(256);
            GetClassName(window, name, name.Capacity);
            if (!name.ToString().StartsWith("AvaloniaMessageWindow ", StringComparison.Ordinal)) return true;
            MessageWindow = window;
            return false;
        }, IntPtr.Zero);
        if (MessageWindow == IntPtr.Zero) throw new InvalidOperationException("The Avalonia Windows dispatch window is unavailable.");
        if (!SetWindowSubclass(MessageWindow, _procedure, 0x45564F53, UIntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr Procedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        if (message == 0x0011) return new IntPtr(1);
        if (message == 0x0016)
        {
            if (wParam != IntPtr.Zero)
                try { _confirmed(); } catch (Exception ex) { _logger.Error(ex, "Windows session cleanup failed"); }
            return IntPtr.Zero;
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }
    public void Dispose()
    {
        if (MessageWindow == IntPtr.Zero) return;
        RemoveWindowSubclass(MessageWindow, _procedure, 0x45564F53);
        MessageWindow = IntPtr.Zero;
        GC.KeepAlive(_procedure);
    }
    private delegate bool EnumerateWindow(IntPtr window, IntPtr parameter);
    private delegate IntPtr SubclassProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, EnumerateWindow callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(IntPtr window, SubclassProcedure callback, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProcedure callback, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
