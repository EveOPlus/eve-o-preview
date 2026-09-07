using System;
using System.Windows.Forms;
using Avalonia.Controls;
using Avalonia.Win32.Interoperability;
using EveOPreview.Services.Interop;

namespace EveOPreview.View.CustomControl;

/// <summary>Keeps the Avalonia island at the Windows host's current monitor scale.</summary>
internal sealed class WorkspaceAvaloniaHost : WinFormsAvaloniaControlHost
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SynchronizeDpi(FindForm()?.DeviceDpi ?? DeviceDpi);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        SynchronizeDpi(FindForm()?.DeviceDpi ?? DeviceDpi);
    }

    internal void SynchronizeDpi(int dpi)
    {
        if (!IsHandleCreated || IsDisposed || Content is null || dpi <= 0) return;
        var root = TopLevel.GetTopLevel(Content);
        if (root is null || Math.Abs(root.RenderScaling - dpi / 96.0) < 0.0001) return;
        var window = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (window == IntPtr.Zero) return;

        // Avalonia 11.3's embedded Win32 window handles WM_DPICHANGED, whereas Windows
        // sends child HWNDs WM_DPICHANGED_AFTERPARENT. Translate the host's DPI to the
        // existing island; do not recreate its handle/content (drafts, focus, popups).
        // Its suggested RECT must be parent-relative, NOT the desktop form rectangle.
        var bounds = new RECT(0, 0, ClientSize.Width, ClientSize.Height);
        User32NativeMethods.SendMessage(window, 0x02E0, new IntPtr(dpi | (dpi << 16)), ref bounds);
        // Docking may already have given the child these pixel bounds before its
        // scale changed. In that case SetWindowPos emits no WM_SIZE, leaving the
        // root's logical ClientSize stale. Refresh it without a resize or flicker.
        User32NativeMethods.SendMessage(window, 0x0005, 0, (ClientSize.Height << 16) | ClientSize.Width);
    }
}
