using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Presenters;
using EveOPreview.Mediator.Handlers.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.Services.Interop;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class SettingsIntegrationTests(ITestOutputHelper output)
{
    private static readonly Assembly App = typeof(MainForm).Assembly;
    private static IThumbnailConfiguration NewConfig() => (IThumbnailConfiguration)Activator.CreateInstance(App.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    private static T Control<T>(MainForm form, string name) where T : System.Windows.Forms.Control => (T)form.Controls.Find(name, true).Single();

    [Theory]
    [InlineData("controls")]
    [InlineData("live-settings")]
    [InlineData("resources")]
    [InlineData("shutdown")]
    [InlineData("focus-window")]
    public Task SettingsAndClientLifecycle(string scenario) => PrivateDesktopRunner.RunAsync("settings-" + scenario, output);

    internal static void RunScenario(string scenario)
    {
        if (scenario == "controls") CheckControls();
        else if (scenario == "live-settings") CheckLiveSettings();
        else if (scenario == "resources") CheckResources();
        else if (scenario == "shutdown") CheckShutdown();
        else if (scenario == "focus-window") CheckWindowFocus();
        else throw new ArgumentException(scenario);
    }

    private static void CheckWindowFocus()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var previous = new Form();
        using var target = new FocusProbeForm();
        previous.Show(); target.Show();
        Native.SetActiveWindow(previous.Handle);
        Assert.Equal(previous.Handle, Native.GetActiveWindow());
        bool wakeSent = false, activated = false;
        int responsivenessProbes = 0;
        target.Observe = message =>
        {
            if (message.Msg == 0) responsivenessProbes++;
            if (message.Msg == 6 && (message.WParam.ToInt64() & 0xffff) != 0)
            {
                Assert.True(wakeSent, "Wake must precede target activation messages");
                activated = true;
            }
        };
        var hooks = Stub.Create<IHookService>((method, args) =>
        {
            if (method.Name == "TellEveClientFocusIsComingAsync") wakeSent = true;
            return Stub.Default(method.ReturnType);
        });
        new WindowManager(hooks, logger).ActivateWindow(target.Handle);
        Assert.True(activated);
        Assert.Equal(target.Handle, Native.GetActiveWindow());
        Assert.Equal(0, responsivenessProbes); // A 1 FPS client must never fail a pre-focus WM_NULL gate.
    }

    private sealed class FocusProbeForm : Form
    {
        public Action<Message> Observe;
        protected override void WndProc(ref Message message) { Observe?.Invoke(message); base.WndProc(ref message); }
    }

    private static void CheckShutdown()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        using var form = new MainForm(context, logger);
        ((Form)form).Show();
        form.MinimizeToTray = false;
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int stops = 0, saves = 0;
        async Task StopOnUiContext()
        {
            stops++;
            await releaseCleanup.Task; // Deliberately requires the UI context to resume.
        }
        var mediator = Stub.Create<IMediator>((method, args) =>
        {
            if (args.Length > 0 && args[0] is GetCurrentProfileLocation) return Task.FromResult(new ProfileLocation { FriendlyName = "Default" });
            if (args.Length > 0 && args[0]?.GetType().Name == "StopService") return StopOnUiContext();
            return Stub.Default(method.ReturnType);
        });
        var storage = Stub.Create<IConfigurationStorage>((method, args) =>
        {
            if (method.Name == "Save") saves++;
            return Stub.Default(method.ReturnType);
        });
        _ = new MainFormPresenter(Stub.Create<IApplicationController>(), form, mediator, NewConfig(), storage,
            Stub.Create<IGlobalEvents>(), Stub.Create<IProfileManager>(), logger);
        form.Close();
        Assert.False(form.IsDisposed);
        Application.DoEvents();
        Assert.Equal(1, stops);
        form.Close(); // Repeated close must not start another native cleanup.
        Assert.Equal(1, stops);
        releaseCleanup.SetResult();
        var deadline = Stopwatch.StartNew();
        while (!form.IsDisposed && deadline.Elapsed < TimeSpan.FromSeconds(5)) { Application.DoEvents(); Thread.Sleep(5); }
        Assert.True(form.IsDisposed);
        Assert.Equal(1, saves);
    }

    private static void CheckControls()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        using var form = new MainForm(context, logger);
        var config = NewConfig();
        ((Form)form).Show();
        int saves = 0;
        form.ApplicationSettingsChanged = () => saves++;
        form.TitleFontSettings = new FontSettings { Name = null, Size = -1 };
        form.EnableAutomaticCpuAffinity = true;
        form.EnableAutomaticCpuAffinity = false;
        Assert.Equal(0, saves);
        Control<CheckBox>(form, "chbAutoCpuAffinity").Checked = true;
        Assert.Equal(1, saves); // Invalid font load must not leave all UI events suppressed.
        form.TitleFontSettings = config.TitleFontSettings;
        Control<TextBox>(form, "txtFontOutlineWidth").Text = "3.5";
        Call(form, "UpdateFontOutlineWidth");
        Assert.Equal(3.5f, form.TitleFontSettings.OutlineWidth);
        Control<TextBox>(form, "txtTitleOffsetLeft").Text = "";
        Control<TextBox>(form, "txtTitleOffsetTop").Text = "-9";
        Call(form, "UpdateTitleOffset");
        Assert.Equal(0, form.TitleFontSettings.PositionOffsetFromLeft);
        Assert.Equal(-9, form.TitleFontSettings.PositionOffsetFromTop);

        var group = new CycleGroup { Description = "Sparse", ClientsOrder = new() { [0] = "EVE - A", [9] = "EVE - B", [50] = "EVE - C" } };
        form.CycleGroups = new List<CycleGroup> { group };
        var clients = Control<ListBox>(form, "cycleGroupClientOrderList");
        clients.SelectedIndex = 2;
        Call(form, "cycleGroupMoveClientOrderUpButton_Click", null, EventArgs.Empty);
        Assert.Equal("EVE - C", ((KeyValuePair<int, string>)clients.SelectedItem).Value);
        Assert.Equal(1, clients.SelectedIndex);
        Call(form, "cycleGroupMoveClientOrderUpButton_Click", null, EventArgs.Empty);
        Assert.Equal("EVE - C", group.ClientsOrder[0]);
        Assert.Equal(0, clients.SelectedIndex);
        form.GetClientNameFromInput = () => null;
        Call(form, "addClientToCycleGroupButton_Click", null, EventArgs.Empty);
        Assert.Equal(3, group.ClientsOrder.Count);
        Call(form, "removeGroupButton_Click", null, EventArgs.Empty);
        Assert.Empty(form.CycleGroups);
        Assert.Empty(clients.Items.Cast<object>());
        using var input = new ClientNameInputBox();
        input.LoadKnownClients(new List<string>());
        var capture = new CaptureNewHotkeyHandler(Stub.Create<IKeyboardMouseEvents>(), config, logger);
        Assert.False(capture.Handle(new CaptureNewHotkey("", 1), CancellationToken.None).GetAwaiter().GetResult().IsValid);
    }

    private static void CheckLiveSettings()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var client = new Form { Text = "Client baseline" };
        client.Show();
        var config = NewConfig();
        config.EnableAutomaticCpuAffinity = false;
        var events = new GlobalEvents();
        var down = new List<KeyEventHandler>();
        var up = new List<KeyEventHandler>();
        var keyboard = Stub.Create<IKeyboardMouseEvents>((method, args) =>
        {
            if (method.Name == "add_KeyDown") down.Add((KeyEventHandler)args[0]);
            if (method.Name == "remove_KeyDown") down.Remove((KeyEventHandler)args[0]);
            if (method.Name == "add_KeyUp") up.Add((KeyEventHandler)args[0]);
            if (method.Name == "remove_KeyUp") up.Remove((KeyEventHandler)args[0]);
            return Stub.Default(method.ReturnType);
        });
        var affinityPending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int activations = 0;
        int inputThread = Environment.CurrentManagedThreadId;
        Action<IntPtr> activating = null;
        IntPtr foreground = new IntPtr(101);
        var window = Stub.Create<IWindowManager>((method, args) =>
        {
            if (method.Name == "GetForegroundWindowHandle") return foreground;
            if (method.Name == "GetLiveThumbnail") return Stub.Create<IDwmThumbnail>();
            if (method.Name == "ActivateWindow")
            {
                Assert.Equal(inputThread, Environment.CurrentManagedThreadId);
                activations++;
                activating?.Invoke((IntPtr)args[0]);
                foreground = (IntPtr)args[0];
            }
            return Stub.Default(method.ReturnType);
        });
        var processes = new List<IProcessInfo>();
        var pending = new List<IProcessInfo>();
        void Add(int id)
        {
            var process = Stub.Create<IProcessInfo>((method, args) => method.Name switch
            {
                "get_MainWindowHandle" => new IntPtr(id), "get_ProcessId" => id,
                "get_Title" => "EVE - " + id, _ => Stub.Default(method.ReturnType)
            });
            processes.Add(process); pending.Add(process);
        }
        Add(101); Add(102);
        var monitor = Stub.Create<IProcessMonitor>((method, args) =>
        {
            if (method.Name == "GetAllProcesses") return processes.ToList();
            if (method.Name == "GetMainProcess") return Stub.Create<IProcessInfo>();
            if (method.Name == "GetUpdatedProcesses")
            {
                args[0] = pending.ToList(); pending.Clear();
                args[1] = new List<IProcessInfo>(); args[2] = new List<IProcessInfo>();
            }
            return Stub.Default(method.ReturnType);
        });
        var controller = Stub.Create<IApplicationController>((method, args) => Activator.CreateInstance(method.GetGenericArguments()[0],
            window, config, Stub.Create<IThumbnailManager>(), Stub.Create<IMediator>(), keyboard, logger));
        var factory = (IThumbnailViewFactory)Activator.CreateInstance(App.GetType("EveOPreview.View.ThumbnailViewFactory"), controller, config);
        var mediator = Stub.Create<IMediator>((method, args) => method.Name == "Send" ? affinityPending.Task : Stub.Default(method.ReturnType));
        var manager = (IThumbnailManager)Activator.CreateInstance(App.GetType("EveOPreview.Services.ThumbnailManager"),
            mediator, config, monitor, window, factory, keyboard, Stub.Create<IHookService>(), events, logger);
        using var lifetime = (IDisposable)manager;
        Call(manager, "UpdateThumbnailsList");
        Call(manager, "RefreshThumbnails");
        var originalViews = manager.GetAllKnownClients();
        config.ThumbnailSize = new Size(500, 300);
        config.TitleFontSettings = new FontSettings { Name = "Arial", Size = 24, ForeColor = Color.Red };
        config.ShowThumbnailFrames = true;
        config.ThumbnailRefreshPeriod = 750;
        events.PublishCurrentProfileChanged(new SelectedProfileChangedNotification(null));
        foreach (var view in manager.GetAllKnownClients().Values)
        {
            Assert.Same(originalViews[view.Id], view);
            Assert.Equal(config.ThumbnailSize, view.ThumbnailSize);
            Assert.Equal(24, view.TitleFontSettings.Size);
        }
        Add(103);
        Call(manager, "UpdateThumbnailsList");
        Assert.Equal(24, manager.GetClientByPointer(new IntPtr(103)).TitleFontSettings.Size);
        Assert.Equal(TimeSpan.FromMilliseconds(750), ((System.Windows.Threading.DispatcherTimer)manager.GetType()
            .GetField("_thumbnailUpdateTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager)).Interval);

        var refresh = (IRequestHandler<RefreshHotkeys>)Activator.CreateInstance(App.GetType("EveOPreview.Mediator.Handlers.Configuration.RefreshHotkeysHandler"), config, logger, events);
        var group = new CycleGroup { ForwardHotkeys = new() { "Control+F8" }, ClientsOrder = new() { [1] = "EVE - 102", [2] = "EVE - 103" } };
        config.CycleGroups.Add(group);
        refresh.Handle(new RefreshHotkeys(), CancellationToken.None).GetAwaiter().GetResult();
        config.EnableActiveClientHighlight = true;
        config.ActiveClientHighlightThickness = 7;
        activating = target =>
        {
            Assert.Equal(target, manager.GetActiveClient().Id);
            foreach (var view in manager.GetAllKnownClients().Values)
                Assert.Equal(view.Id == target, (bool)typeof(ThumbnailView).GetField("_isHighlightEnabled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view));
        };
        var press = new KeyEventArgs(Keys.Control | Keys.F8);
        var timer = Stopwatch.StartNew();
        foreach (var handler in down.ToArray()) handler(null, press);
        Assert.True(timer.ElapsedMilliseconds < 500, "Pending affinity must not delay the input callback");
        Assert.True(press.Handled);
        Assert.Equal(1, activations); // No message pump or async continuation before activation/highlight.
        Assert.Equal(new IntPtr(102), manager.GetActiveClient()?.Id);
        var keyUp = new KeyEventArgs(Keys.F8); // Modifier was released before the main key.
        foreach (var handler in up.ToArray()) handler(null, keyUp);
        Assert.True(keyUp.Handled);
        // A second deliberate cycle is accepted immediately while unrelated async work is pending.
        foreach (var handler in down.ToArray()) handler(null, new KeyEventArgs(Keys.Control | Keys.F8));
        Assert.Equal(new IntPtr(103), manager.GetActiveClient()?.Id);
        Assert.Equal(2, activations);
        affinityPending.SetResult();
        config.CycleGroups.Clear();
        refresh.Handle(new RefreshHotkeys(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.Empty(down);
        press = new KeyEventArgs(Keys.Control | Keys.F8);
        foreach (var handler in down.ToArray()) handler(null, press);
        Assert.False(press.Handled);
        config.EnableCompatibilityMode = true;
        events.PublishCurrentProfileChanged(new SelectedProfileChangedNotification(null));
        Assert.All(manager.GetAllKnownClients().Values, view => Assert.Equal("StaticThumbnailView", view.GetType().Name));
        Assert.All(originalViews.Values, view => Assert.True(((Form)view).IsDisposed));
    }

    private static void CheckResources()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var window = new Form { Text = "EVE - A", ClientSize = new Size(500, 350) };
        window.Show();
        bool enumerate = true;
        Func<Process[]> source = () => enumerate ? new[] { Process.GetCurrentProcess() } : Array.Empty<Process>();
        var monitor = (IProcessMonitor)Activator.CreateInstance(App.GetType("EveOPreview.Services.Implementation.ProcessMonitor"),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { logger, source }, null);
        using var lifetime = (IDisposable)monitor;
        monitor.GetUpdatedProcesses(out var added, out _, out _);
        var process = Assert.Single(added);
        IntPtr handle = process.ProcessHandle;
        Assert.NotEqual(IntPtr.Zero, handle);
        using var host = Process.GetCurrentProcess();
        _ = host.Handle;
        for (int i = 0; i < 10; i++) monitor.GetUpdatedProcesses(out _, out _, out _);
        host.Refresh();
        int handlesBefore = host.HandleCount;
        for (int i = 0; i < 200; i++) monitor.GetUpdatedProcesses(out _, out _, out _);
        host.Refresh();
        Assert.InRange(host.HandleCount - handlesBefore, -10, 10);
        Assert.Equal(handle, Assert.Single(monitor.GetAllProcesses()).ProcessHandle);
        window.Text = "EVE - B";
        Application.DoEvents();
        monitor.GetUpdatedProcesses(out added, out var changed, out _);
        Assert.Empty(added);
        Assert.Equal("EVE - B", Assert.Single(changed).Title);
        Assert.Equal(handle, Assert.Single(changed).ProcessHandle);
        var config = NewConfig();
        var cpu = new CpuAffinityService(logger, config);
        Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var original, out _));
        try
        {
            cpu.UpdateAffinity(process, changed.Single(), process, monitor.GetAllProcesses());
            if (cpu.PCores.Count >= 4)
            {
                var expected = (IntPtr)typeof(CpuAffinityService).GetField("_activeMask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cpu);
                expected = (IntPtr)(expected.ToInt64() & original.ToInt64());
                Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var actual, out _));
                Assert.Equal(expected == IntPtr.Zero ? original : expected, actual);
            }
            cpu.ResetAll(monitor.GetAllProcesses());
            Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var reset, out _));
            Assert.Equal(original, reset);
            cpu.Stop(monitor.GetAllProcesses());
            cpu.UpdateAffinity(process, null, null, monitor.GetAllProcesses());
            Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var afterStop, out _));
            Assert.Equal(original, afterStop);
        }
        finally { KernelNativeMethods.SetProcessAffinityMask(handle, original); }
        var windows = new WindowManager(Stub.Create<IHookService>(), logger);
        for (int i = 0; i < 20; i++) using (var capture = windows.GetStaticThumbnail(window.Handle)) Assert.NotNull(capture);
        window.ClientSize = new Size(100, 100);
        Assert.Null(windows.GetStaticThumbnail(window.Handle));
        enumerate = false;
        monitor.GetUpdatedProcesses(out _, out _, out var removed);
        Assert.Single(removed);
        Assert.Empty(monitor.GetAllProcesses());
        Assert.Equal(IntPtr.Zero, process.ProcessHandle);
    }
}
