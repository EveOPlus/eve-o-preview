using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EveOPreview.Input;

/// <summary>Message-only native window for a dedicated input thread; owns no desktop UI or framework loop.</summary>
internal sealed class WindowsMessageWindow : IDisposable
{
    private readonly WindowProcedure _procedure;
    private readonly Func<uint, IntPtr, IntPtr, bool> _process;
    private readonly string _className = "EveOPreview.Input." + Guid.NewGuid().ToString("N");
    private readonly IntPtr _module = GetModuleHandle(null);
    public IntPtr Handle { get; private set; }

    public WindowsMessageWindow(Func<uint, IntPtr, IntPtr, bool> process)
    {
        _process = process;
        _procedure = WndProc;
        var definition = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = _procedure, Instance = _module, ClassName = _className };
        if (RegisterClassEx(ref definition) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        Handle = CreateWindowEx(0, _className, "EVE-O input", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, _module, IntPtr.Zero);
        if (Handle == IntPtr.Zero) { UnregisterClass(_className, _module); throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }

    private IntPtr WndProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam) =>
        _process(message, wParam, lParam) ? IntPtr.Zero : DefWindowProc(window, message, wParam, lParam);

    public static void Run()
    {
        int result;
        while ((result = GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
        if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void ExitThread() => PostQuitMessage(0);
    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        DestroyWindow(Handle);
        Handle = IntPtr.Zero;
        UnregisterClass(_className, _module);
        GC.KeepAlive(_procedure);
    }

    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size, Style; public WindowProcedure Procedure; public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background; public string MenuName, ClassName; public IntPtr SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Message { public IntPtr Window; public uint Id; public IntPtr WParam, LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass definition);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, IntPtr module);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(int extendedStyle, string className, string caption, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessage(out Message message, IntPtr window, uint first, uint last);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
}
