using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
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
        using var logger = new LoggerConfiguration().CreateLogger();
        using var hotkeys = new WindowsHotkeyService(logger, _ => { });
        // Separate from the concurrently running registration-lifecycle fixture's F16/F17 chords.
        HotkeyBinding[] bindings = [new("Control+Shift+F23", () => { })];
        hotkeys.Replace(bindings, Enum.Parse<HotkeyMode>(mode));
        var moves = new List<MouseEventHandler>(); var ups = new List<MouseEventHandler>();
        var mouse = Stub.Create<IKeyboardMouseEvents>((method, args) =>
        {
            if (method.Name == "add_MouseMove") moves.Add((MouseEventHandler)args[0]);
            if (method.Name == "remove_MouseMove") moves.Remove((MouseEventHandler)args[0]);
            if (method.Name == "add_MouseUp") ups.Add((MouseEventHandler)args[0]);
            if (method.Name == "remove_MouseUp") ups.Remove((MouseEventHandler)args[0]);
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
        using var view = new Preview(config, Stub.Create<IWindowManager>(), mouse, mediator)
        { Id = (IntPtr)101, Title = "EVE - Mouse test", IsOverlayEnabled = true, ThumbnailSize = new(320, 180), ThumbnailLocation = new(30, 30) };
        void Call(string name, params object[] args) => typeof(ThumbnailView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, args);
        void Press(MouseButtons button, Keys modifiers = Keys.None) => Call("MouseDownEventHandler", new MouseEventArgs(button, 1, 100, 80, 0), modifiers);
        void Release()
        {
            foreach (var handler in ups.ToArray()) handler(null, new(MouseButtons.Left, 1, 0, 0, 0));
            Assert.Empty(moves); Assert.Empty(ups);
        }
        void Move(int x, int y)
        {
            Point origin = (Point)typeof(ThumbnailView).GetField("_baseMousePosition", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            foreach (var handler in moves.ToArray()) handler(null, new(MouseButtons.None, 0, origin.X + x, origin.Y + y, 0));
        }
        view.Show(); Application.DoEvents();
        int activated = 0, deactivated = 0, entered = 0, left = 0;
        view.ThumbnailActivated = _ => activated++;
        view.ThumbnailDeactivated = (_, external) => { Assert.False(external); deactivated++; };
        view.ThumbnailFocused = _ => entered++;
        view.ThumbnailLostFocus = _ => left++;
        Press(MouseButtons.Left); Press(MouseButtons.Left);
        Assert.Equal(2, activated);
        Press(MouseButtons.Left, Keys.Control);
        Assert.Equal(1, deactivated);
        Press(MouseButtons.Left, Keys.Control | Keys.Shift); Press(MouseButtons.Middle);
        Assert.Equal(2, activated); Assert.Equal(1, deactivated);
        foreach (var target in view.Overlay.Controls.Cast<Control>().Where(c => c.Name is "OverlayLabel" or "OverlayAreaPictureBox"))
        {
            typeof(Control).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, [new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0)]);
        }
        Assert.Equal(4, activated); // Image, overlay background and title retain their routes.
        Call("MouseEnter_Handler", view, EventArgs.Empty);
        Size originalSize = view.Size;
        view.ZoomIn(ViewZoomAnchor.NW, 2);
        Assert.True(view.Width > originalSize.Width);
        view.ZoomOut();
        Assert.Equal(originalSize, view.Size);
        Call("MouseLeave_Handler", view, EventArgs.Empty);
        Assert.Equal(1, entered); Assert.Equal(1, left);
        Press(MouseButtons.Right);
        var menu = (ContextMenuStrip)typeof(ThumbnailView).GetField("thumbnailContextMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        Assert.True(menu.Visible);
        Call("MouseUp_Handler", view, new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0));
        menu.Close();
        Call("menuMinimize_Click", null, EventArgs.Empty);
        Call("minimizeAllToolStripMenuItem_Click", null, EventArgs.Empty);
        Assert.Equal(new[] { "MinimizeClient", "MinimizeAllClients" }, messages);
        Call("menuReposition_Click", null, EventArgs.Empty);
        Assert.Single(moves); Assert.Single(ups);
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
        Call("menuReposition_Click", null, EventArgs.Empty);
        view.Close();
        Assert.Empty(moves); Assert.Empty(ups);
    }
}
