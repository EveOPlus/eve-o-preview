using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using EveOPreview.Tests.Infrastructure;

using System.Threading.Tasks;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class ThumbnailZOrderTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("ActivationOrder")]
    [InlineData("CompetingTopmostWindow")]
    [InlineData("HiddenWindowRecovery")]
    [InlineData("MinimizedWindowRecovery")]
    [InlineData("HideAll")]
    [InlineData("ConfiguredHiding")]
    [InlineData("FocusLoss")]
    [InlineData("AlwaysOnTopSetting")]
    [InlineData("ImmediateCycleActivation")]
    [InlineData("ImmediateThumbnailActivation")]
    [InlineData("ImmediateActivationRespectsHiding")]
    public Task OverlayWindowBehavior(string scenario) => PrivateDesktopRunner.RunAsync(scenario, output);

    internal static void RunScenario(string scenario)
    {
        var assembly = typeof(ThumbnailView).Assembly;
        var config = (IThumbnailConfiguration)Activator.CreateInstance(
            assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        config.ShowThumbnailsAlwaysOnTop = true;
        config.EnableThumbnailSnap = false;
        config.HideThumbnailsDelay = 0;
        IntPtr foreground = new IntPtr(101);
        Action<IntPtr> activating = null;
        var windowManager = Stub.Create<IWindowManager>((method, args) =>
        {
            if (method.Name == "GetForegroundWindowHandle") return foreground;
            if (method.Name == "ActivateWindow")
            {
                activating?.Invoke((IntPtr)args[0]);
                foreground = (IntPtr)args[0];
            }
            return Stub.Default(method.ReturnType);
        });
        var keyboard = Stub.Create<IKeyboardMouseEvents>();
        var mediator = Stub.Create<IMediator>();
        var mainProcess = Stub.Create<IProcessInfo>();
        var pending = new List<IProcessInfo>();
        foreach (int id in new[] { 101, 102, 103 })
        {
            int clientId = id;
            pending.Add(Stub.Create<IProcessInfo>((method, args) => method.Name switch
            {
                "get_MainWindowHandle" => new IntPtr(clientId),
                "get_Title" => "EVE - " + clientId,
                _ => Stub.Default(method.ReturnType)
            }));
        }
        var processMonitor = Stub.Create<IProcessMonitor>((method, args) =>
        {
            if (method.Name == "GetMainProcess") return mainProcess;
            if (method.Name == "GetUpdatedProcesses")
            {
                args[0] = pending.ToList();
                pending.Clear();
                args[1] = new List<IProcessInfo>();
                args[2] = new List<IProcessInfo>();
            }
            return Stub.Default(method.ReturnType);
        });
        var factory = Stub.Create<IThumbnailViewFactory>((method, args) => new Preview(config, windowManager, keyboard)
        {
            Id = (IntPtr)args[0], Title = (string)args[1], ThumbnailSize = (Size)args[2]
        });
        using var logger = new LoggerConfiguration().CreateLogger();
        var manager = (IThumbnailManager)Activator.CreateInstance(assembly.GetType("EveOPreview.Services.ThumbnailManager"),
            mediator, config, processMonitor, windowManager, factory, keyboard,
            Stub.Create<IHookService>(), Stub.Create<IGlobalEvents>(), logger);
        Call(manager, "UpdateThumbnailsList");
        var views = manager.GetAllKnownClients().Values.Cast<Preview>().OrderBy(v => v.Id.ToInt64()).ToArray();
        var a = views[0]; var b = views[1]; var c = views[2];
        using var client = new Form { Text = "Simulated EVE client" };
        client.Show();
        try
        {
            void Refresh() => Call(manager, "RefreshThumbnails");
            void Activate(Preview view, string focusCheck = null)
            {
                // This desktop is intentionally not the input desktop. Set the calling
                // thread's active window explicitly instead of requesting foreground focus.
                Native.SetActiveWindow(client.Handle);
                IntPtr activeBefore = Native.GetActiveWindow();
                if (activeBefore != client.Handle)
                    throw new Exception($"Cannot activate simulated client: expected 0x{client.Handle:X}, actual 0x{activeBefore:X}");
                IntPtr foregroundBefore = Native.GetForegroundWindow();
                manager.GetType().GetMethod("SetActive").Invoke(manager,
                    new object[] { new KeyValuePair<IntPtr, IThumbnailView>(view.Id, view) });
                Refresh();
                if (focusCheck != null)
                {
                    IntPtr activeAfter = Native.GetActiveWindow();
                    IntPtr foregroundAfter = Native.GetForegroundWindow();
                    Check(activeAfter == activeBefore && foregroundAfter == foregroundBefore,
                        $"{focusCheck} (active 0x{activeBefore:X} -> 0x{activeAfter:X}, " +
                        $"foreground 0x{foregroundBefore:X} -> 0x{foregroundAfter:X})");
                }
            }

            Refresh();
            Activate(b, "raising previews preserves active window and foreground");
            Activate(c, "raising previews preserves active window and foreground");
            Activate(b, "raising previews preserves active window and foreground");

            switch (scenario)
            {
                case "ActivationOrder":
                    AssertStack(b, c, a);
                    Check(views.All(v => Topmost(v.Handle) && Topmost(v.Overlay.Handle)), "persistent topmost on previews and overlays");
                    break;

                case "CompetingTopmostWindow":
                    using (var other = new Form { Text = "Another topmost window", TopMost = true })
                    {
                        Native.SetWindowPos(other.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x53);
                        Refresh();
                        Check(IsAbove(other.Handle, b.Overlay.Handle), "ordinary timer ticks do not reassert z-order");
                        Activate(b, "same-client activation preserves focus");
                        Check(IsAbove(b.Overlay.Handle, other.Handle), "same-client activation reasserts topmost order");
                    }
                    break;

                case "HiddenWindowRecovery":
                    foreach (var view in views)
                    {
                        Native.ShowWindow(view.Handle, 0);
                        Native.ShowWindow(view.Overlay.Handle, 0);
                        Native.SetWindowPos(view.Handle, new IntPtr(-2), 0, 0, 0, 0, 0x13);
                    }
                    Check(views.All(v => v.IsActive && !Native.IsWindowVisible(v.Handle)), "simulate stale visibility flags");
                    Activate(c, "restoring hidden previews preserves focus");
                    Check(views.All(v => Native.IsWindowVisible(v.Handle) && Native.IsWindowVisible(v.Overlay.Handle)
                        && Topmost(v.Handle) && Topmost(v.Overlay.Handle)), "activation restores hidden windows and native topmost flags");
                    AssertStack(c, b, a);
                    break;

                case "MinimizedWindowRecovery":
                    Native.ShowWindow(b.Handle, 7);
                    Check(Native.IsIconic(b.Handle), "simulate minimized preview");
                    Activate(b, "restoring minimized previews preserves focus");
                    Check(!Native.IsIconic(b.Handle) && Native.IsWindowVisible(b.Overlay.Handle), "activation restores minimized preview");
                    break;

                case "HideAll":
                    config.IsTemporarilyHidingAllThumbnails = true;
                    Refresh(); Activate(a);
                    Check(views.All(v => !v.IsActive && !Native.IsWindowVisible(v.Handle)
                        && !Native.IsWindowVisible(v.Overlay.Handle)), "Hide All survives client activation");
                    config.IsTemporarilyHidingAllThumbnails = false;
                    Refresh();
                    AssertStack(a, b, c);
                    break;

                case "ConfiguredHiding":
                    config.ToggleThumbnail(a.Title, true);
                    config.HideActiveClientThumbnail = true;
                    Activate(b);
                    Check(!Native.IsWindowVisible(a.Handle) && !Native.IsWindowVisible(b.Handle), "individual and active-client hiding respected");
                    Check(Native.IsWindowVisible(c.Handle), "eligible preview remains visible");
                    break;

                case "FocusLoss":
                    config.HideThumbnailsOnLostFocus = true;
                    foreground = new IntPtr(999);
                    Refresh();
                    Check(views.All(v => !Native.IsWindowVisible(v.Handle)), "focus-loss hiding respected");
                    foreground = b.Id;
                    Refresh();
                    AssertStack(b, c, a);
                    break;

                case "AlwaysOnTopSetting":
                    config.ShowThumbnailsAlwaysOnTop = false;
                    Activate(c);
                    Check(views.All(v => !Topmost(v.Handle) && !Topmost(v.Overlay.Handle)), "Always on top off is respected");
                    config.ShowThumbnailsAlwaysOnTop = true;
                    Refresh();
                    AssertStack(c, b, a);
                    Check(views.All(v => Topmost(v.Handle) && Topmost(v.Overlay.Handle)), "enabling Always on top restores activation order");
                    break;

                case "ImmediateCycleActivation":
                    Native.SetActiveWindow(client.Handle);
                    c.Refreshing = _ => AssertStack(c, b, a); // Image work must not precede the native raise either.
                    activating = _ => AssertStack(c, b, a); // Must already be raised when client activation starts.
                    manager.GetType().GetMethod("SetActive").Invoke(manager,
                        [new KeyValuePair<IntPtr, IThumbnailView>(c.Id, c)]);
                    activating = null;
                    c.Refreshing = null;
                    AssertStack(c, b, a); // No refresh tick.
                    Check(Native.GetActiveWindow() == client.Handle, "immediate cycling preserves focus");
                    break;

                case "ImmediateThumbnailActivation":
                    Native.SetActiveWindow(client.Handle);
                    activating = _ =>
                    {
                        AssertStack(a, b, c);
                        Check(manager.GetActiveClient()?.Id == a.Id, "selection committed before activation");
                        Check((bool)typeof(ThumbnailView).GetField("_isHighlightEnabled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(a),
                            "border applied before activation");
                    };
                    a.Refreshing = _ => throw new Exception("Image capture must not delay immediate activation");
                    a.ThumbnailActivated(a.Id);
                    a.Refreshing = null;
                    activating = null;
                    AssertStack(a, b, c); // No UI continuation or refresh tick.
                    Check(Native.GetActiveWindow() == client.Handle, "immediate thumbnail activation preserves focus");
                    break;

                case "ImmediateActivationRespectsHiding":
                    foreach (string mode in new[] { "HideAll", "Individual", "Active", "TopmostOff", "Inactive" })
                    {
                        config.IsTemporarilyHidingAllThumbnails = mode == "HideAll";
                        config.ToggleThumbnail(a.Title, mode == "Individual");
                        config.HideActiveClientThumbnail = mode == "Active";
                        config.ShowThumbnailsAlwaysOnTop = mode != "TopmostOff";
                        a.IsActive = mode != "Inactive";
                        Native.ShowWindow(a.Handle, 0);
                        Native.ShowWindow(a.Overlay.Handle, 0);
                        // Observe at entry to client activation, before any refresh reconciles visibility.
                        activating = _ => Check(!Native.IsWindowVisible(a.Handle) && !Native.IsWindowVisible(a.Overlay.Handle),
                            "immediate activation respects " + mode);
                        manager.GetType().GetMethod("SetActive").Invoke(manager,
                            [new KeyValuePair<IntPtr, IThumbnailView>(a.Id, a)]);
                    }
                    activating = null;
                    break;

                default:
                    throw new ArgumentException("Unknown thumbnail scenario: " + scenario);
            }
        }
        finally
        {
            manager.Stop();
            foreach (var view in views) { view.Close(); view.Dispose(); }
        }
    }

    private static void Call(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static bool Topmost(IntPtr handle) => (Native.GetWindowLong(handle, -20) & 8) != 0;
    private static bool IsAbove(IntPtr upper, IntPtr lower)
    {
        for (IntPtr h = Native.GetWindow(lower, 3); h != IntPtr.Zero; h = Native.GetWindow(h, 3))
            if (h == upper) return true;
        return false;
    }
    private static void AssertStack(params Preview[] topToBottom)
    {
        var expected = topToBottom.SelectMany(v => new[] { v.Overlay.Handle, v.Handle }).ToArray();
        Check(expected.All(Native.IsWindowVisible), "ordered previews and labels are visible");
        if (!expected.Zip(expected.Skip(1), IsAbove).All(result => result))
        {
            var names = topToBottom.SelectMany(v => new[] { (v.Overlay.Handle, v.Title + " overlay"), (v.Handle, v.Title) }).ToDictionary(x => x.Handle, x => x.Item2);
            Console.WriteLine("Actual order: " + string.Join(", ", expected.OrderBy(h => expected.Count(k => IsAbove(k, h))).Select(h => names[h])));
        }
        Check(expected.Zip(expected.Skip(1), IsAbove).All(result => result),
            "preview/overlay stack: " + string.Join(", ", topToBottom.Select(v => v.Title)));
    }
    private static void Check(bool condition, string message)
    {
        Assert.True(condition, message);
        Console.WriteLine("PASS: " + message);
    }


}

internal sealed class Preview : ThumbnailView
{
    public Preview(IThumbnailConfiguration config, IWindowManager wm, IKeyboardMouseEvents keyboard)
        : base(wm, config, null, null, keyboard) { }
    public Form Overlay => (Form)typeof(ThumbnailView).GetField("_overlay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
    public Action<bool> Refreshing;
    protected override void RefreshThumbnail(bool forceRefresh) => Refreshing?.Invoke(forceRefresh);
    protected override void ResizeThumbnail(int width, int height, int top, int right, int bottom, int left) { }
}
