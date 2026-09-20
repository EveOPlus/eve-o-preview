using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using EveOPreview.Input;
using MediatR;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class ThumbnailMouseTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Global")]
    [InlineData("OperatingSystem")]
    public Task ThumbnailGesturesSurviveHotkeyModeAndCaptureChanges(string mode) => PrivateDesktopRunner.RunAsync("thumbnail-mouse-" + mode, output);

    internal static void Check(string mode)
    {
        TestAvalonia.Initialize();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var hotkeys = new WindowsHotkeyService(logger, _ => { });
        // Separate from the concurrently running registration-lifecycle fixture's F16/F17 chords.
        HotkeyBinding[] bindings = [new("Control+Shift+F23", () => { })];
        hotkeys.Replace(bindings, Enum.Parse<HotkeyMode>(mode));
        var moves = new List<EventHandler<GlobalPointerEventArgs>>();
        var ups = new List<EventHandler<GlobalPointerEventArgs>>();
        Point cursor = new(120, 120);
        PointerButtons buttons = PointerButtons.None;
        ShortcutKeys modifiers = ShortcutKeys.None;
        var mouse = Stub.Create<IGlobalPointerInput>((method, args) =>
        {
            if (method.Name == "add_MouseMove") moves.Add((EventHandler<GlobalPointerEventArgs>)args[0]);
            if (method.Name == "remove_MouseMove") moves.Remove((EventHandler<GlobalPointerEventArgs>)args[0]);
            if (method.Name == "add_MouseUp") ups.Add((EventHandler<GlobalPointerEventArgs>)args[0]);
            if (method.Name == "remove_MouseUp") ups.Remove((EventHandler<GlobalPointerEventArgs>)args[0]);
            if (method.Name == "set_Position") cursor = (Point)args[0];
            if (method.Name == "get_Position") return cursor;
            if (method.Name == "get_Buttons") return buttons;
            if (method.Name == "get_Modifiers") return modifiers;
            Assert.DoesNotContain("KeyDown", method.Name); Assert.DoesNotContain("KeyUp", method.Name);
            return Stub.Default(method.ReturnType);
        });
        var messages = new List<string>();
        var mediator = Stub.Create<IMediator>((method, args) =>
        {
            if (method.Name == "Send") { messages.Add(args[0].GetType().Name); return Task.CompletedTask; }
            return Stub.Default(method.ReturnType);
        });
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        var known = new Dictionary<IntPtr, IThumbnailView>();
        var manager = Stub.Create<IThumbnailManager>((method, _) => method.Name == "GetAllKnownClients" ? known : Stub.Default(method.ReturnType));
        using var view = new PointerPreview(config, Stub.Create<IWindowManager>(), mouse, mediator, manager)
        { Id = (IntPtr)101, Title = "EVE - Mouse test", IsOverlayEnabled = true, ThumbnailSize = new(320, 180), ThumbnailLocation = new(30, 30) };
        void Call(string name, params object[] args) => typeof(ThumbnailView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, args);
        void Press(PointerButtons button, ShortcutKeys keys = ShortcutKeys.None)
        {
            buttons = button;
            Call("MouseDownEventHandler", new GlobalPointerEventArgs(button, button, new(100, 80), keys), keys);
        }
        void Release()
        {
            buttons = PointerButtons.None;
            foreach (var handler in ups.ToArray()) handler(null, new(PointerButtons.Left, buttons, cursor, modifiers));
            Assert.Empty(moves); Assert.Empty(ups);
            Assert.False(view.IsInteracting);
        }
        void Move(int x, int y)
        {
            Point origin = (Point)typeof(ThumbnailView).GetField("_baseMousePosition", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            cursor = new(origin.X + x, origin.Y + y);
            foreach (var handler in moves.ToArray()) handler(null, new(PointerButtons.None, buttons, cursor, modifiers));
        }
        view.Show(); TestAvalonia.Pump();
        Assert.Equal(new IntPtr(3), SendMessage(view.Handle, 0x0021, IntPtr.Zero, IntPtr.Zero));
        int activated = 0, deactivated = 0, entered = 0, left = 0;
        view.ThumbnailActivated = _ => activated++;
        view.ThumbnailDeactivated = (_, external) => { Assert.False(external); deactivated++; };
        view.ThumbnailFocused = _ => entered++;
        view.ThumbnailLostFocus = _ => left++;
        Press(PointerButtons.Left); Press(PointerButtons.Left);
        Assert.Equal(2, activated);
        Press(PointerButtons.Left, ShortcutKeys.Control);
        Assert.Equal(1, deactivated);
        Press(PointerButtons.Left, ShortcutKeys.Control | ShortcutKeys.Shift); Press(PointerButtons.Middle);
        Assert.Equal(2, activated); Assert.Equal(1, deactivated);
        // This fixture is deliberately independent of the production host and is
        // never shipped. A zero source handle cannot pass the focus assertion.
        using var focusSource = new System.Windows.Forms.Form { Text = "Thumbnail input focus fixture" };
        focusSource.Show();
        Native.SetActiveWindow(focusSource.Handle);
        Assert.Equal(focusSource.Handle, Native.GetActiveWindow());
        var overlay = typeof(ThumbnailView).GetField("_overlay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        var overlayHandle = (IntPtr)overlay.GetType().GetProperty("Handle").GetValue(overlay);
        foreach (var local in new[] { new Point(12, 12), new Point(150, 100) })
        {
            Point screen = view.PointToScreen(local);
            Assert.Equal(new IntPtr(-1), SendMessage(overlayHandle, 0x0084, IntPtr.Zero, Pack(screen)));
            SendMessage(view.Handle, 0x0201, new IntPtr(1), Pack(local));
            SendMessage(view.Handle, 0x0202, IntPtr.Zero, Pack(local));
            TestAvalonia.Pump();
            Assert.Equal(focusSource.Handle, Native.GetActiveWindow());
        }
        Assert.Equal(4, activated); // The title/image overlay passes native hit testing through to the same input host.
        Call("MouseEnter_Handler", view, EventArgs.Empty);
        Size originalSize = view.Size;
        view.ZoomIn(ViewZoomAnchor.NW, 2);
        Assert.True(view.Width > originalSize.Width);
        view.ZoomOut();
        Assert.Equal(originalSize, view.Size);
        Call("MouseLeave_Handler", view, EventArgs.Empty);
        Assert.Equal(1, entered); Assert.Equal(1, left);
        Press(PointerButtons.Right);
        var menu = (ContextMenu)typeof(ThumbnailView).GetField("thumbnailContextMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        Assert.True(menu.IsOpen);
        Call("MouseUp_Handler", view, EventArgs.Empty);
        menu.Close();
        Call("menuMinimize_Click", null, EventArgs.Empty);
        Call("minimizeAllToolStripMenuItem_Click", null, EventArgs.Empty);
        Assert.Equal(new[] { "MinimizeClient", "MinimizeAllClients" }, messages);
        Call("menuReposition_Click", null, EventArgs.Empty);
        Assert.Single(moves); Assert.Single(ups);
        Assert.True(view.IsInteracting);
        Point start = view.Location;
        Move(24, 13); Assert.Equal(new Point(start.X + 24, start.Y + 13), view.Location);
        hotkeys.Replace(bindings, mode == "Global" ? HotkeyMode.OperatingSystem : HotkeyMode.Global);
        Assert.Single(moves); Assert.Single(ups);
        using (var cancel = new CancellationTokenSource())
        {
            var capture = hotkeys.CaptureAsync(TimeSpan.FromSeconds(5), cancel.Token);
            Move(7, 3); Assert.Equal(new Point(start.X + 31, start.Y + 16), view.Location);
            cancel.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => capture.GetAwaiter().GetResult());
            Assert.Single(moves); Assert.Single(ups);
        }
        Release();
        Call("resizeThumbnailToolStripMenuItem_Click", null, EventArgs.Empty);
        Size before = view.Size; Move(20, 10);
        Assert.Equal(new Size(before.Width + 20, before.Height + 10), view.Size);
        Release();
        Call("resizeThumbnailToolStripMenuItem_Click", null, EventArgs.Empty);
        var ratioSize = view.ClientSize;
        modifiers = ShortcutKeys.Shift;
        Move(40, 100);
        Assert.Equal(ratioSize.Width + 40, view.ClientSize.Width);
        Assert.InRange(System.Math.Abs((double)view.ClientSize.Width / view.ClientSize.Height
            - (double)ratioSize.Width / ratioSize.Height), 0, .02);
        int leavesBeforeRelease = left;
        var resizedBounds = view.Bounds;
        Assert.False(new Rectangle(Point.Empty, view.ClientSize).Contains(view.PointToClient(cursor)));
        Call("MouseLeave_Handler", view, EventArgs.Empty);
        Assert.Equal(leavesBeforeRelease, left); // Defer while the final resize geometry is still changing.
        modifiers = ShortcutKeys.None;
        Release();
        Assert.Equal(leavesBeforeRelease + 1, left);
        Assert.Equal(resizedBounds, view.Bounds); // Releasing hover must not restore an obsolete size.

        Call("resizeThumbnailToolStripMenuItem_Click", null, EventArgs.Empty);
        leavesBeforeRelease = left;
        cursor = view.PointToScreen(new Point(view.ClientSize.Width + 20, view.ClientSize.Height + 20));
        Call("MouseLeave_Handler", view, EventArgs.Empty);
        cursor = view.PointToScreen(new Point(10, 10));
        Call("MouseEnter_Handler", view, EventArgs.Empty);
        Release();
        Assert.Equal(leavesBeforeRelease, left); // Reentry cancels the deferred leave using actual pointer position.

        // Exercise production drag dispatch, rather than only the pure snap math:
        // magnetic correction is visible before release and never consumes raw travel.
        config.EnableThumbnailSnap = true;
        view.SetFrames(false);
        view.ClientSize = new(200, 120);
        view.Location = new(390, 310);
        using var neighbour = new PointerPreview(config, Stub.Create<IWindowManager>(), mouse, mediator, manager)
        { Id = (IntPtr)102, Title = "EVE - Snap neighbour", ThumbnailSize = new(200, 120), ThumbnailLocation = new(600, 300) };
        neighbour.SetFrames(false);
        neighbour.Show();
        known[view.Id] = view;
        known[neighbour.Id] = neighbour;
        Call("menuReposition_Click", null, EventArgs.Empty);
        Move(4, 0);
        Assert.Equal(400, view.Location.X);
        Assert.True(view.IsInteracting);
        Move(-13, 0);
        Assert.Equal(381, view.Location.X);
        modifiers = ShortcutKeys.Shift;
        Move(13, 0);
        Assert.Equal(394, view.Location.X);
        modifiers = ShortcutKeys.None;
        Move(0, 0);
        Assert.Equal(400, view.Location.X);
        Release();

        // A framed native drag shares the same live policy through WM_MOVING.
        view.SetFrames(true);
        view.Location = new(600 - view.Size.Width - 10, 310);
        SendMessage(view.Handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
        cursor = new(cursor.X + 4, cursor.Y);
        var proposed = new NativeRectangle { Left = view.Location.X + 4, Top = 310,
            Right = view.Location.X + 4 + view.Size.Width, Bottom = 310 + view.Size.Height };
        Assert.Equal(new IntPtr(1), SendMoving(view.Handle, 0x0216, IntPtr.Zero, ref proposed));
        Assert.Equal(600, proposed.Right);
        SendMessage(view.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
        Assert.False(view.IsInteracting);

        int userResizeNotifications = 0;
        view.ThumbnailResized = _ => userResizeNotifications++;
        var requestedPixels = new Size(337, 191);
        view.ClientSize = requestedPixels;
        foreach (int dpi in new[] { 96, 120, 144, 192, 96 })
        {
            view.SetFrames(false);
            Assert.Equal(requestedPixels, view.ClientSize);
            view.SetFrames(true);
            Assert.Equal(requestedPixels, view.ClientSize);
            var bounds = view.Bounds;
            var suggested = new NativeRectangle { Left = bounds.Left, Top = bounds.Top,
                Right = bounds.Right, Bottom = bounds.Bottom };
            SendMoving(view.Handle, 0x02E0, new IntPtr(dpi | (dpi << 16)), ref suggested);
            TestAvalonia.Pump();
            Assert.Equal(requestedPixels, view.ClientSize);
        }
        Assert.Equal(0, userResizeNotifications); // Frame/DPI housekeeping is never a persisted user resize.
        var previousOuter = view.Size;
        SendMessage(view.Handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
        Assert.True(Native.SetWindowPos(view.Handle, IntPtr.Zero, 0, 0, previousOuter.Width + 20,
            previousOuter.Height + 10, 0x0016));
        leavesBeforeRelease = left;
        cursor = view.PointToScreen(new Point(view.ClientSize.Width + 10, view.ClientSize.Height + 10));
        Call("MouseLeave_Handler", view, EventArgs.Empty);
        SendMessage(view.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
        Assert.Equal(leavesBeforeRelease + 1, left); // Framed native resizing has the same deferred-hover cleanup.
        Assert.True(userResizeNotifications > 0);
        Assert.Equal(new Size(requestedPixels.Width + 20, requestedPixels.Height + 10), view.ClientSize);

        Call("menuReposition_Click", null, EventArgs.Empty);
        view.Close();
        Assert.Empty(moves); Assert.Empty(ups);
    }

    private static IntPtr Pack(Point point) => new(unchecked((point.Y << 16) | (point.X & 0xffff)));
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMoving(IntPtr handle, uint message, IntPtr wParam, ref NativeRectangle rectangle);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRectangle { public int Left, Top, Right, Bottom; }

    private sealed class PointerPreview(IThumbnailConfiguration config, IWindowManager windows,
        IGlobalPointerInput pointer, IMediator mediator, IThumbnailManager manager) : ThumbnailView(windows, config, manager, mediator, pointer)
    {
        protected override void RefreshThumbnail(bool forceRefresh) { }
        protected override void ResizeThumbnail(int width, int height, int top, int right, int bottom, int left) { }
    }
}
