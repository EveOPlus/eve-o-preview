using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Avalonia;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using EveOPreview.Input;
using ContextMenu = Avalonia.Controls.ContextMenu;
using MenuItem = Avalonia.Controls.MenuItem;
using Separator = Avalonia.Controls.Separator;
using Avalonia.VisualTree;
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
    [InlineData("CycleSkipping")]
    [InlineData("ThumbnailMenuOrdering")]
    [InlineData("ThumbnailMenuHover")]
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
        IntPtr predicted = IntPtr.Zero;
        Action<IntPtr> activating = null;
        var windowManager = Stub.Create<IWindowManager>((method, args) =>
        {
            if (method.Name == "GetForegroundWindowHandle") return foreground;
            if (method.Name == "PredictUpcomingClient") predicted = (IntPtr)args[0];
            if (method.Name == "ActivateWindow")
            {
                activating?.Invoke((IntPtr)args[0]);
                foreground = (IntPtr)args[0];
            }
            return Stub.Default(method.ReturnType);
        });
        var keyboard = Stub.Create<IGlobalPointerInput>();
        var sentMessages = new List<object>();
        var mediator = Stub.Create<IMediator>((method, args) =>
        {
            if (method.Name == "Send") sentMessages.Add(args[0]);
            return args.FirstOrDefault() is EveOPreview.Mediator.Messages.SetClientCycleSkipped skip
                ? new EveOPreview.Mediator.Handlers.Thumbnails.SetClientCycleSkippedHandler(config).Handle(skip, default) : Stub.Default(method.ReturnType);
        });
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
        var factory = Stub.Create<IThumbnailViewFactory>((method, args) => new Preview(config, windowManager, keyboard, mediator)
        {
            Id = (IntPtr)args[0], Title = (string)args[1], ThumbnailSize = (Size)args[2]
        });
        using var logger = new LoggerConfiguration().CreateLogger();
        var manager = (IThumbnailManager)Activator.CreateInstance(assembly.GetType("EveOPreview.Services.ThumbnailManager"),
            mediator, config, processMonitor, windowManager, factory, Stub.Create<IHotkeyService>(),
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
                case "ThumbnailMenuHover":
                    config.ThumbnailZoomEnabled = true;
                    config.ThumbnailZoomFactor = 2;
                    config.ThumbnailOpacity = .6;
                    a.Location = new Point(200, 200);
                    var hoverMenu = (ContextMenu)typeof(ThumbnailView).GetField("thumbnailContextMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
                    var baseBounds = a.Bounds;
                    typeof(ThumbnailView).GetMethod("MouseEnter_Handler", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, [a, EventArgs.Empty]);
                    var hoverBounds = a.Bounds;
                    Check(hoverBounds.Size != baseBounds.Size && a.Opacity == 1, "fixture must enter a zoomed, opaque preview");
                    typeof(ThumbnailView).GetMethod("MouseDownEventHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(a, [new GlobalPointerEventArgs(PointerButtons.Right, PointerButtons.Right, new Point(100, 60), ShortcutKeys.None), ShortcutKeys.None]);
                    typeof(ThumbnailView).GetMethod("MouseLeave_Handler", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, [a, EventArgs.Empty]);
                    for (int tick = 0; tick < 5; tick++) { TestAvalonia.Pump(); Refresh(); Thread.Sleep(100); }
                    Check(hoverMenu.IsOpen, "first-open menu must survive hover leave and refresh ticks");
                    Check(a.Bounds == hoverBounds && a.Opacity == 1, "entering the menu must retain hover geometry and opacity");
                    config.HideThumbnailsOnLostFocus = true;
                    foreground = AvaloniaMenuTest.Handle(hoverMenu);
                    Refresh();
                    Check(a.IsActive && hoverMenu.IsOpen, "the open menu must count as part of its thumbnail for focus-based hiding");
                    Check(views.All(v => IsAbove(AvaloniaMenuTest.Handle(hoverMenu), v.Overlay.Handle)), "refresh must not raise thumbnail overlays above the open menu");
                    hoverMenu.Close(); TestAvalonia.Pump();
                    Check(a.Bounds == baseBounds && Math.Abs(a.Opacity - .6) < .01, "dismissing the menu must release the deferred hover effect");
                    AvaloniaMenuTest.Open(hoverMenu, a); a.Hide(); TestAvalonia.Pump();
                    Check(!hoverMenu.IsOpen && !a.IsContextMenuOpen, "hiding a thumbnail must close its menu and release menu state");
                    break;
                case "ThumbnailMenuOrdering":
                    var quickMenu = (ContextMenu)typeof(ThumbnailView).GetField("thumbnailContextMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
                    a.Location = new Point(Screen.PrimaryScreen.WorkingArea.Left + 200, Screen.PrimaryScreen.WorkingArea.Top + 200);
                    foreach (var theme in new[] { "Light", "Dark", "Legacy" })
                    {
                        EveOPreview.View.CustomControl.NativeMenuTheme.CurrentTheme = theme;
                        foreach (bool skipFirst in new[] { false, true, false })
                        {
                            EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailMenuOrder = skipFirst
                                ? EveOPreview.UI.ThumbnailMenuActions.Normalize(["skip-cycling", "resize", "move"])
                                : EveOPreview.UI.ThumbnailMenuActions.DefaultOrder;
                            string firstItem = skipFirst ? "menuCycleSkip" : "menuMinimize";
                            foreach (bool resuming in skipFirst ? new[] { false, true } : new[] { false })
                            {
                                var clickPosition = new Point(100, 60);
                                var screenPosition = a.PointToScreen(clickPosition);
                                typeof(ThumbnailView).GetMethod("MouseDownEventHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                                    .Invoke(a, [new GlobalPointerEventArgs(PointerButtons.Right, PointerButtons.Right, clickPosition, ShortcutKeys.None), ShortcutKeys.None]);
                                TestAvalonia.Pump();
                                Check(quickMenu.IsOpen, "first right-click opens the thumbnail menu");
                                Check(((Avalonia.Controls.Control)quickMenu.Items[0]).Name == firstItem, "the configured action must be first in every theme");
                                var hitItem = AvaloniaMenuTest.AtScreen(quickMenu, screenPosition);
                                var firstControl = (Avalonia.Controls.Control)quickMenu.Items[0];
                                Check(hitItem?.Name == firstItem, $"the configured action must be under the original pointer position (expected {firstItem}, hit {hitItem?.Name ?? "none"}, pointer {screenPosition}, scale {a.RenderScaling}, popup {quickMenu.PointToScreen(default)}, first {firstControl.PointToScreen(default)} size {firstControl.Bounds.Size})");
                                Check(quickMenu.Items.OfType<MenuItem>().Count() == 5, "reordering must retain all five actions");
                                Check(quickMenu.Items.OfType<Separator>().Count() == (skipFirst ? 0 : 2), "only explicitly configured dividers may appear");
                                if (!skipFirst)
                                    Check(((Avalonia.Controls.Control)quickMenu.Items[2]).Name == "divider:minimize" && ((Avalonia.Controls.Control)quickMenu.Items[4]).Name == "divider:skip", "default dividers must follow Minimize All and Skip");
                                if (skipFirst) Check(((MenuItem)quickMenu.Items[0]).Header.ToString().StartsWith(resuming ? "Resume" : "Skip"), "the first action must describe its current skip state");
                                int sentBefore = sentMessages.Count;
                                AvaloniaMenuTest.RightClick(quickMenu, screenPosition);
                                Check(sentMessages.Count == sentBefore + 1 && sentMessages.Last().GetType().Name == (skipFirst ? "SetClientCycleSkipped" : "MinimizeClient"),
                                    "the second right-click must issue only the configured action");
                                Check(config.IsClientCycleSkipped(a.Title) == (skipFirst && !resuming), "skip-first must toggle while minimize leaves cycling unchanged");
                                quickMenu.Close();
                            }
                        }
                    }
                    var placedDivider = "divider:" + Guid.NewGuid().ToString("N");
                    var customLayout = new[] { "skip-cycling", placedDivider, "minimize", "minimize-all", "move", "resize" };
                    EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailMenuOrder = customLayout;
                    foreach (var palette in EveOPreview.UI.ThumbnailMenuThemes.All)
                    {
                        EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailTheme = palette.Id;
                        AvaloniaMenuTest.Open(quickMenu, a); TestAvalonia.Pump();
                        Check(((Avalonia.Controls.Control)quickMenu.Items[1]).Name == placedDivider && quickMenu.Items.OfType<Separator>().Count() == 1,
                            "theme changes must preserve explicit divider placement");
                        if (!SystemInformation.HighContrast)
                        {
                            Check(((Avalonia.Media.ISolidColorBrush)quickMenu.Background).Color == Avalonia.Media.Color.Parse(palette.Background) && ((Avalonia.Media.ISolidColorBrush)quickMenu.Foreground).Color == Avalonia.Media.Color.Parse(palette.Foreground),
                                "native menu uses the selected " + palette.Name + " palette");
                            string directory = System.IO.Path.Combine(AppContext.BaseDirectory, "native-menu-themes");
                            System.IO.Directory.CreateDirectory(directory);
                            AvaloniaMenuTest.Capture(quickMenu, System.IO.Path.Combine(directory, palette.Id + ".png"));
                        }
                        quickMenu.Close();
                    }
                    AvaloniaMenuTest.Open(quickMenu, a);
                    VerifyHighContrastMenu(quickMenu);
                    quickMenu.Close();
                    a.Refresh(false);
                    foreach (var local in new[] { new Point(4, 4), new Point(35, 27), new Point(100, 60) })
                    {
                        var clicked = a.Overlay.PointToScreen(local);
                        var packed = (IntPtr)((clicked.Y << 16) | (clicked.X & 0xffff));
                        Check(AvaloniaMenuTest.SendMessage(a.Overlay.Handle, 0x0084, IntPtr.Zero, packed) == (IntPtr)(-1),
                            "title/image overlay is input-transparent at its actual native coordinates");
                        typeof(ThumbnailView).GetMethod("MouseDownEventHandler", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(a, [new GlobalPointerEventArgs(PointerButtons.Right, PointerButtons.Right, a.PointToClient(clicked), ShortcutKeys.None), ShortcutKeys.None]);
                        TestAvalonia.Pump();
                        Check(AvaloniaMenuTest.AtScreen(quickMenu, clicked)?.Name == "menuCycleSkip",
                            "right-clicking through the overlay places the first action under the original pointer");
                        quickMenu.Close();
                    }
                    break;
                case "CycleSkipping":
                    var order = new SortedDictionary<int, string> { [3] = a.Title, [8] = b.Title, [20] = "EVE - Offline", [50] = c.Title };
                    var otherOrder = new SortedDictionary<int, string> { [1] = c.Title, [2] = b.Title, [3] = a.Title };
                    var menu = (ContextMenu)typeof(ThumbnailView).GetField("thumbnailContextMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(b);
                    var skipItem = menu.Items.OfType<MenuItem>().Single(item => item.Name == "menuCycleSkip");
                    skipItem.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                    Check(config.IsClientCycleSkipped(b.Title), "thumbnail context menu skips the character");
                    AvaloniaMenuTest.Open(menu, b);
                    Check(skipItem.Header?.ToString() == "Resume cycling this character", "context menu reflects global skip state");
                    menu.Close();
                    string Next(bool forward, string title, SortedDictionary<int, string> sequence) => (string)manager.GetType()
                        .GetMethod("FindNextClientInCycleGroup", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, [forward, title, sequence]);
                    Check(Next(true, a.Title, order) == c.Title, "forward skips disabled and offline members");
                    Check(Next(false, c.Title, order) == a.Title, "backward skips disabled members");
                    Check(Next(true, b.Title, order) == c.Title && Next(false, b.Title, order) == a.Title, "a skipped active character retains its sequence position");
                    Check(Next(true, c.Title, otherOrder) == a.Title, "skip is shared by another group");
                    Check(Next(true, "Not in group", order) == a.Title, "outside-group activation begins at first eligible member");
                    a.ThumbnailActivated(a.Id);
                    manager.GetType().GetMethod("CycleNextClient").Invoke(manager, [true, order]);
                    Check(manager.GetActiveClient().Id == c.Id, "actual cycling respects skip state");
                    Check(predicted == a.Id, "next-client prediction also bypasses the skipped character");
                    b.ThumbnailActivated(b.Id);
                    Check(manager.GetActiveClient().Id == b.Id, "manual thumbnail activation still works");
                    config.SetClientCycleSkipped(a.Title, true); config.SetClientCycleSkipped(c.Title, true);
                    Check(Next(true, b.Title, order) == null, "all-skipped order has no destination");
                    manager.GetType().GetMethod("CycleNextClient").Invoke(manager, [true, order]);
                    Check(manager.GetActiveClient().Id == b.Id, "all skipped leaves the active client unchanged");
                    skipItem.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                    Check(!config.IsClientCycleSkipped(b.Title), "thumbnail menu resumes the character");
                    Check(Next(true, a.Title, order) == b.Title && Next(true, b.Title, order) == b.Title, "one eligible character wraps safely");
                    Check(order.Count == 4, "temporary skips never remove saved order entries");
                    break;
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
                    activating = _ => Check(manager.GetActiveClient()?.Id == c.Id, "focus requested before preview maintenance");
                    manager.GetType().GetMethod("SetActive").Invoke(manager,
                        [new KeyValuePair<IntPtr, IThumbnailView>(c.Id, c)]);
                    activating = null;
                    c.Refreshing = null;
                    AssertStack(c, b, a); // No refresh tick.
                    Check(Native.GetActiveWindow() == client.Handle, "immediate cycling preserves focus");
                    break;

                case "ImmediateThumbnailActivation":
                    config.EnableActiveClientHighlight = true;
                    Native.SetActiveWindow(client.Handle);
                    activating = _ =>
                    {
                        Check(manager.GetActiveClient()?.Id == a.Id, "selection committed before activation");
                    };
                    a.Refreshing = _ => throw new Exception("Image capture must not delay immediate activation");
                    a.ThumbnailActivated(a.Id);
                    Check((bool)typeof(ThumbnailView).GetField("_isHighlightEnabled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(a),
                        "border applied immediately after focus without a timer tick");
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
    private static void VerifyHighContrastMenu(ContextMenu menu)
    {
        var colors = new Dictionary<int, Avalonia.Media.Color>
        {
            [5] = Avalonia.Media.Colors.Black, [8] = Avalonia.Media.Colors.White,
            [13] = Avalonia.Media.Colors.Navy, [14] = Avalonia.Media.Colors.Yellow,
            [17] = Avalonia.Media.Colors.Silver
        };
        var item = menu.Items.OfType<MenuItem>().First();
        var separator = menu.Items.OfType<Separator>().First();
        var layout = item.GetVisualDescendants().OfType<Avalonia.Controls.Border>().Single(x => x.Name == "PART_LayoutRoot");
        var header = item.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().Single(x => x.Name == "PART_HeaderPresenter");
        static Avalonia.Media.Color ColorOf(Avalonia.Media.IBrush brush) => ((Avalonia.Media.ISolidColorBrush)brush).Color;
        foreach (string theme in new[] { "Light", "Dark", "Legacy" })
        {
            EveOPreview.View.CustomControl.NativeMenuTheme.CurrentTheme = theme;
            EveOPreview.View.CustomControl.NativeMenuTheme.Apply(menu, true, true, index => colors[index]);
            item.IsSelected = false;
            TestAvalonia.Pump();
            Check(ColorOf(menu.Background) == colors[5] && ColorOf(header.Foreground) == colors[8],
                "high contrast overrides the " + theme + " menu and its actual Fluent text presenter");
            Check(ColorOf(separator.Background) == colors[8], "the actual Fluent separator consumes the contrast border resource");
            item.IsSelected = true;
            TestAvalonia.Pump();
            Check(ColorOf(layout.Background) == colors[13] && ColorOf(header.Foreground) == colors[14],
                "the actual selected Fluent menu item uses the system highlight pair");
            item.IsSelected = false;
            item.IsEnabled = false;
            TestAvalonia.Pump();
            Check(ColorOf(header.Foreground) == colors[17], "disabled Fluent menu text uses the system gray color");
            item.IsEnabled = true;
        }
        EveOPreview.View.CustomControl.NativeMenuTheme.Apply(menu, true, false, _ => throw new Exception("Normal themes must not read contrast colors."));
        TestAvalonia.Pump();
        var palette = EveOPreview.UI.ThumbnailMenuThemes.Resolve(EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailTheme, "Legacy");
        Check(ColorOf(menu.Background) == Avalonia.Media.Color.Parse(palette.Background) && ColorOf(header.Foreground) == Avalonia.Media.Color.Parse(palette.Foreground),
            "leaving high contrast restores the selected menu palette without stale template colors");
    }

    private static void Check(bool condition, string message)
    {
        Assert.True(condition, message);
        Console.WriteLine("PASS: " + message);
    }


}

internal sealed class Preview : ThumbnailView
{
    public Preview(IThumbnailConfiguration config, IWindowManager wm, IGlobalPointerInput keyboard, IMediator mediator = null)
        : base(wm, config, null, mediator, keyboard) { }
    public ThumbnailOverlay Overlay => (ThumbnailOverlay)typeof(ThumbnailView).GetField("_overlay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
    public Action<bool> Refreshing;
    protected override void RefreshThumbnail(bool forceRefresh) => Refreshing?.Invoke(forceRefresh);
    protected override void ResizeThumbnail(int width, int height, int top, int right, int bottom, int left) { }
}
