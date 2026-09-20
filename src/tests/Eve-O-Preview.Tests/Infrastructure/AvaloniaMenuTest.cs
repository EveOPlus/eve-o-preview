using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace EveOPreview.Tests.Infrastructure;

internal static class AvaloniaMenuTest
{
    public static IntPtr Handle(ContextMenu menu) => TopLevel.GetTopLevel(menu)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    public static MenuItem AtScreen(ContextMenu menu, System.Drawing.Point screen)
    {
        var point = menu.PointToClient(new PixelPoint(screen.X, screen.Y));
        var hit = menu.InputHitTest(point) as Visual;
        return hit as MenuItem ?? hit?.GetVisualAncestors().OfType<MenuItem>().FirstOrDefault();
    }
    public static void RightClick(ContextMenu menu, System.Drawing.Point screen)
    {
        var handle = Handle(menu);
        if (handle == IntPtr.Zero) throw new InvalidOperationException("The actual menu popup must have an HWND.");
        var point = new NativePoint { X = screen.X, Y = screen.Y };
        ScreenToClient(handle, ref point);
        var coordinates = (IntPtr)((point.Y << 16) | (point.X & 0xffff));
        SendMessage(handle, 0x0204, (IntPtr)2, coordinates);
        SendMessage(handle, 0x0205, IntPtr.Zero, coordinates);
        TestAvalonia.Pump();
    }
    public static void Open(ContextMenu menu, Control owner)
    {
        menu.Open(owner);
        TestAvalonia.Pump();
    }
    public static void Capture(ContextMenu menu, string path)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(menu.Bounds.Width), (int)Math.Ceiling(menu.Bounds.Height)));
        bitmap.Render(menu);
        bitmap.Save(path);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
