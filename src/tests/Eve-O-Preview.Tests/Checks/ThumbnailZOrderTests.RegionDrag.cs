using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using EveOPreview.Configuration;
using EveOPreview.Input;
using EveOPreview.Services;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed partial class ThumbnailZOrderTests
{
    private static void RunRegionDragScenario(string scenario, IThumbnailConfiguration config, IThumbnailManager manager,
        Preview[] views, Action refresh, Action<Point, ShortcutKeys, PointerButtons> pointer, Action move, Action release,
        Func<bool> unsubscribed, Action<bool> failSave, Func<int> saves)
    {
        EveOPreview.View.CustomControl.NativeMenuTheme.CurrentTheme = scenario.EndsWith("Light") ? "Light" : "Dark";
        config.ThumbnailRegions.Add(new() { Id = "left", Name = "Left", X = 50, Y = 50, Width = 400, Height = 250 });
        config.ThumbnailRegions.Add(new() { Id = "right", Name = "Right", X = 650, Y = 200, Width = 300, Height = 200 });
        var a = views[0]; var b = views[1]; var c = views[2];
        var handles = views.Select(view => view.Handle).ToArray();
        config.ClientRegionAssignments[b.Title] = "left";
        config.SetThumbnailLocation(a.Title, "", new(-500, 100));
        config.SetThumbnailLocation(c.Title, "", new(-500, 500));
        manager.ApplyRegionLayout();
        IntPtr active = Native.GetActiveWindow(), foreground = Native.GetForegroundWindow();
        Assert.NotEqual(IntPtr.Zero, active);

        static object Field(Preview view, string name) => typeof(ThumbnailView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        static void Invoke(Preview view, string name, params object[] args) => typeof(ThumbnailView)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, args);
        static Dictionary<string, ThumbnailRegionWindow> Highlights(Preview view) => (Dictionary<string, ThumbnailRegionWindow>)Field(view, "_regionDragHighlights");
        void MoveTo(Point point, ShortcutKeys keys = ShortcutKeys.None)
        {
            pointer(point, keys, PointerButtons.Right); move(); TestAvalonia.Pump();
        }
        void Drop(Preview view, Point point, ShortcutKeys keys = ShortcutKeys.None)
        {
            pointer(point, keys, PointerButtons.None); release(); TestAvalonia.Pump();
            Assert.Empty(Highlights(view)); Assert.False(view.IsInteracting); Assert.True(unsubscribed());
            Assert.Equal(active, Native.GetActiveWindow()); Assert.Equal(foreground, Native.GetForegroundWindow());
        }
        void HoldDrag(Preview view)
        {
            pointer(view.PointToScreen(new Point(40, 40)), ShortcutKeys.None, PointerButtons.Right);
            RegionSendMessage(view.Handle, 0x0204, new IntPtr(2), new IntPtr((40 << 16) | 40));
            Invoke(view, "holdRightClickToMoveTimer_Tick", null, EventArgs.Empty);
            TestAvalonia.Pump();
            Assert.True(view.IsInteracting); Assert.Equal(2, Highlights(view).Count);
        }
        void AssertDock(Preview view, string id, Point location, Size size)
        {
            Assert.Equal(id, config.GetThumbnailRegion(view.Title)?.Id);
            Assert.Equal(location, view.Location); Assert.Equal(size, view.ThumbnailSize);
        }

        // Menu movement exposes every region, even an occupied one, without intercepting input.
        int before = saves();
        Invoke(a, "menuReposition_Click", null, EventArgs.Empty);
        Assert.Equal(2, Highlights(a).Count);
        foreach (var highlight in Highlights(a).Values)
        {
            IntPtr hwnd = highlight.TryGetPlatformHandle().Handle;
            Assert.True(IsAbove(hwnd, b.Overlay.Handle));
            Assert.Equal(new IntPtr(-1), RegionSendMessage(hwnd, 0x0084, IntPtr.Zero, IntPtr.Zero));
            Assert.Equal(new IntPtr(3), RegionSendMessage(hwnd, 0x0021, IntPtr.Zero, IntPtr.Zero));
        }
        MoveTo(new(42, 130)); // Within 12 pixels of the left region, not inside it.
        Assert.Equal("left", Field(a, "_regionDockCandidate"));
        Assert.Null(config.GetThumbnailRegion(a.Title)); Assert.Equal(before, saves());
        Point dragging = a.Location; refresh(); Assert.Equal(dragging, a.Location);
        using (var image = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(400, 250)))
        {
            image.Render(Highlights(a)["left"]);
            image.Save(System.IO.Path.Combine(AppContext.BaseDirectory, scenario + ".png"));
        }
        Drop(a, new(42, 130));
        AssertDock(a, "left", new(50, 50), new(400, 250));
        AssertDock(b, "left", new(50, 50), new(400, 250));
        Assert.Equal(before + 1, saves());

        // Holding right-click transfers a docked thumbnail without requiring Undock.
        before = saves(); HoldDrag(a); MoveTo(new(800, 260));
        Assert.Equal("left", config.GetThumbnailRegion(a.Title)?.Id);
        Assert.NotEqual(new Point(50, 50), a.Location);
        dragging = a.Location; refresh(); Assert.Equal(dragging, a.Location);
        Assert.Equal(before, saves());
        Drop(a, new(800, 260));
        AssertDock(a, "right", new(650, 200), new(300, 200));
        AssertDock(b, "left", new(50, 50), new(400, 250));
        Assert.Equal(before + 1, saves());

        // Dropping outside removes the assignment and keeps the dragged position/size.
        before = saves(); HoldDrag(a); MoveTo(new(1500, 900)); Drop(a, new(1500, 900));
        Assert.Null(config.GetThumbnailRegion(a.Title));
        Assert.Equal(new Size(300, 200), a.ThumbnailSize);
        dragging = a.Location; refresh(); Assert.Equal(dragging, a.Location);
        Assert.Equal(dragging, config.GetThumbnailLocation(a.Title, "", Point.Empty));
        Assert.Equal(before + 1, saves());
        HoldDrag(a); MoveTo(new(800, 260)); Drop(a, new(800, 260));
        AssertDock(a, "right", new(650, 200), new(300, 200));

        // Shift and cancellation retain the original dock.
        before = saves();
        HoldDrag(a); MoveTo(new(150, 100), ShortcutKeys.Shift);
        Assert.Null(Field(a, "_regionDockCandidate"));
        Drop(a, new(150, 100), ShortcutKeys.Shift);
        AssertDock(a, "right", new(650, 200), new(300, 200)); Assert.Equal(before, saves());
        HoldDrag(a); MoveTo(new(150, 100));
        var transientHandles = Highlights(a).Values.Select(window => window.TryGetPlatformHandle().Handle).ToArray();
        a.Hide(); Assert.Empty(Highlights(a)); Assert.True(unsubscribed());
        Assert.All(transientHandles, hwnd => Assert.False(Native.IsWindowVisible(hwnd)));
        a.Show(); refresh(); AssertDock(a, "right", new(650, 200), new(300, 200));

        // Failed persistence cannot silently transfer the thumbnail or lose its prior assignment.
        failSave(true); HoldDrag(a); MoveTo(new(150, 100)); Drop(a, new(150, 100));
        AssertDock(a, "right", new(650, 200), new(300, 200));
        HoldDrag(a); MoveTo(new(1500, 900)); Drop(a, new(1500, 900));
        AssertDock(a, "right", new(650, 200), new(300, 200));
        failSave(false);
        Invoke(c, "menuReposition_Click", null, EventArgs.Empty);
        MoveTo(new(150, 100)); failSave(true); Drop(c, new(150, 100)); Assert.Null(config.GetThumbnailRegion(c.Title));
        failSave(false);

        // A profile/runtime application ends the gesture and destroys all drag highlights.
        HoldDrag(a); MoveTo(new(150, 100));
        config.ThumbnailRegions = config.ThumbnailRegions.Select(region => region with { }).ToList();
        manager.ApplyRuntimeSettings();
        Assert.Empty(Highlights(a)); Assert.True(unsubscribed()); Assert.False(a.IsInteracting);
        Drop(a, new(150, 100)); AssertDock(a, "right", new(650, 200), new(300, 200));

        // Framed native moves share target selection and commit; native cancellation cannot dock.
        c.SetFrames(true); c.Location = new(-500, 500);
        void NativeMove(bool cancel)
        {
            pointer(new(-400, 500), ShortcutKeys.None, PointerButtons.Left);
            RegionSendMessage(c.Handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
            pointer(new(150, 100), ShortcutKeys.None, PointerButtons.Left);
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf<DockNativeRect>());
            try
            {
                Marshal.StructureToPtr(new DockNativeRect { Left = 50, Top = 100, Right = 50 + c.Width, Bottom = 100 + c.Height }, memory, false);
                RegionSendMessage(c.Handle, 0x0216, IntPtr.Zero, memory);
                Assert.Equal(2, Highlights(c).Count);
                if (!cancel)
                {
                    var moved = Marshal.PtrToStructure<DockNativeRect>(memory);
                    c.Location = new(moved.Left, moved.Top);
                }
                RegionSendMessage(c.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
            }
            finally { Marshal.FreeHGlobal(memory); }
            TestAvalonia.Pump(); Assert.Empty(Highlights(c));
        }
        NativeMove(true); Assert.Null(config.GetThumbnailRegion(c.Title));
        NativeMove(false); AssertDock(c, "left", new(50, 50), new(400, 250));
        Assert.Equal(handles, views.Select(view => view.Handle).ToArray());
        Assert.False(manager.CanUndoThumbnailEdit); // Region assignment changes invalidate geometry-only undo.

        // Disabling drag docking keeps assigned previews locked; manual Undock remains available.
        config.EnableRegionDragDocking = false; manager.ApplyRegionLayout();
        pointer(a.PointToScreen(new Point(40, 40)), ShortcutKeys.None, PointerButtons.Right);
        RegionSendMessage(a.Handle, 0x0204, new IntPtr(2), new IntPtr((40 << 16) | 40));
        Invoke(a, "holdRightClickToMoveTimer_Tick", null, EventArgs.Empty);
        MoveTo(new(150, 100)); Drop(a, new(150, 100));
        AssertDock(a, "right", new(650, 200), new(300, 200));
        manager.UndockThumbnail(c.Id).GetAwaiter().GetResult();
        Invoke(c, "menuReposition_Click", null, EventArgs.Empty);
        Assert.Empty(Highlights(c)); MoveTo(new(150, 100)); Drop(c, new(150, 100));
        Assert.Null(config.GetThumbnailRegion(c.Title));
        config.EnableRegionDragDocking = true;

        config.EnableThumbnailRegions = false; manager.ApplyRegionLayout();
        Assert.Null(config.GetThumbnailRegion(a.Title)); Assert.Equal("right", config.ClientRegionAssignments[a.Title]);
        Invoke(a, "menuReposition_Click", null, EventArgs.Empty);
        Assert.Empty(Highlights(a)); MoveTo(new(150, 100)); Drop(a, new(150, 100));
        config.EnableThumbnailRegions = true; manager.ApplyRegionLayout();
        AssertDock(a, "right", new(650, 200), new(300, 200));
        Assert.Equal(handles, views.Select(view => view.Handle).ToArray());

        // Legacy retains its existing feature set, including no automatic region drag targets.
        EveOPreview.View.CustomControl.NativeMenuTheme.CurrentTheme = "Legacy";
        manager.UndockThumbnail(c.Id).GetAwaiter().GetResult();
        Invoke(c, "menuReposition_Click", null, EventArgs.Empty);
        Assert.Empty(Highlights(c)); MoveTo(new(150, 100)); Drop(c, new(150, 100));
        Assert.Null(config.GetThumbnailRegion(c.Title));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DockNativeRect { public int Left, Top, Right, Bottom; }
}
