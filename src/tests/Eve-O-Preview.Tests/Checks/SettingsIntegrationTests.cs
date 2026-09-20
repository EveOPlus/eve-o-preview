using System;
using Avalonia;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using Application = System.Windows.Forms.Application;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
    [InlineData("resize-persistence")]
    [InlineData("live-settings")]
    [InlineData("resources")]
    [InlineData("shutdown")]
    [InlineData("focus-window")]
    public Task SettingsAndClientLifecycle(string scenario) => PrivateDesktopRunner.RunAsync("settings-" + scenario, output);

    internal static void RunScenario(string scenario)
    {
        if (scenario == "resize-persistence") CheckResizePersistence();
        else if (scenario == "controls") CheckControls();
        else if (scenario == "live-settings") CheckLiveSettings();
        else if (scenario == "resources") CheckResources();
        else if (scenario == "shutdown") CheckShutdown();
        else if (scenario == "focus-window") CheckWindowFocus();
        else throw new ArgumentException(scenario);
    }

    private static void CheckResizePersistence()
    {
        AppBuilder.Configure<EveOPreview.UI.WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        var config = NewConfig();
        var root = Path.Combine(Path.GetTempPath(), "eveo-resize-" + Guid.NewGuid().ToString("N"));
        int published = 0, savesRequested = 0;
        var mediator = Stub.Create<IMediator>((method, args) =>
        {
            if (method.Name == "Publish") published++;
            if (args.Length > 0 && args[0]?.GetType().Name == "SaveConfiguration") savesRequested++;
            return args.Length > 0 && args[0] is GetCurrentProfileLocation
                ? Task.FromResult(new ProfileLocation { FriendlyName = "Default" }) : Stub.Default(method.ReturnType);
        });
        try
        {
            var profiles = (ProfileManager)Activator.CreateInstance(typeof(ProfileManager), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { logger, mediator, root }, null);
            var storage = (IConfigurationStorage)Activator.CreateInstance(App.GetType("EveOPreview.Configuration.Implementation.ConfigurationStorage"),
                Stub.Create<IAppConfig>(), config, mediator, profiles, logger, Stub.Create<IGlobalEvents>(), null);
            var preferences = new ApplicationPreferences(Path.Combine(root, "settings.json"), logger);
            using var form = new WorkspaceForm(context, logger, mediator, storage, config, profiles, preferences,
                Stub.Create<EveOPreview.UI.IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)));
            config.ThumbnailSize = new Size(384, 216);
            config.ShowThumbnailFrames = false;
            storage.Save();
            var presenter = new MainFormPresenter(Stub.Create<IApplicationController>(), form, mediator, config, storage,
                Stub.Create<IGlobalEvents>(), profiles, logger);
            presenter.HandleSelectedProfileChangedNotification(new SelectedProfileChangedNotification(null));
            // A resize must not apply other pending settings or write on every mouse move.
            form.ShowThumbnailFrames = true;
            published = savesRequested = 0;
            var handler = Activator.CreateInstance(App.GetType("EveOPreview.Mediator.Handlers.Thumbnails.ThumbnailActiveSizeUpdatedHandler"), presenter, logger);
            foreach (var size in new[] { new Size(420, 240), new Size(350, 200), new Size(540, 303) })
            {
                var notification = Activator.CreateInstance(App.GetType("EveOPreview.Mediator.Messages.ThumbnailActiveSizeUpdated"), size);
                ((Task)Call(handler, "Handle", notification, CancellationToken.None)).GetAwaiter().GetResult();
                Assert.Equal(size, form.ThumbnailSize);
                Assert.Equal(size, config.ThumbnailSize);
                Assert.False(config.ShowThumbnailFrames);
            }
            Assert.Equal(0, published);
            Assert.Equal(0, savesRequested);
            ((Task)Call(presenter, "SaveOnExitAsync")).GetAwaiter().GetResult();
            Assert.True(storage.Load());
            Assert.Equal(new Size(540, 303), config.ThumbnailSize);
            Assert.False(config.ShowThumbnailFrames);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
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
        var capture = new CaptureNewHotkeyHandler(Stub.Create<IHotkeyService>((m, _) => m.Name == "CaptureAsync" ? Task.FromException<string>(new TimeoutException()) : Stub.Default(m.ReturnType)), config, logger);
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
        var keyboard = Stub.Create<IKeyboardMouseEvents>();
        var bindings = new List<HotkeyBinding>();
        HotkeyMode registeredMode = HotkeyMode.Global;
        bool registeredPassthrough = false;
        var hotkeys = Stub.Create<IHotkeyService>((method, args) =>
        {
            if (method.Name == "Replace") { bindings.Clear(); bindings.AddRange((IReadOnlyList<HotkeyBinding>)args[0]); registeredMode = (HotkeyMode)args[1]; registeredPassthrough = (bool)args[2]; }
            return Stub.Default(method.ReturnType);
        });
        var affinityPending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int activations = 0;
        int registrations = 0, unregistrations = 0;
        int inputThread = Environment.CurrentManagedThreadId;
        Action<IntPtr> activating = null;
        IntPtr foreground = new IntPtr(101);
        var window = Stub.Create<IWindowManager>((method, args) =>
        {
            if (method.Name == "GetForegroundWindowHandle") return foreground;
            if (method.Name == "GetLiveThumbnail")
            {
                registrations++;
                return Stub.Create<IDwmThumbnail>((operation, _) =>
                {
                    if (operation.Name == "Unregister") unregistrations++;
                    return operation.Name == "Update" ? true : Stub.Default(operation.ReturnType);
                });
            }
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
        string preferencesPath = Path.Combine(AppContext.BaseDirectory, "factory-graphics-" + Guid.NewGuid().ToString("N") + ".json");
        var preferences = new ApplicationPreferences(preferencesPath, logger);
        preferences.SetPreviewOverlayRenderer("Legacy");
        var factory = (IThumbnailViewFactory)Activator.CreateInstance(App.GetType("EveOPreview.View.ThumbnailViewFactory"), controller, config, preferences);
        using var factoryLifetime = (IDisposable)factory;
        var affinityMessages = new List<EveOPreview.Mediator.Messages.Process.UpdateCpuAffinity>();
        var mediator = Stub.Create<IMediator>((method, args) =>
        {
            if (method.Name != "Send") return Stub.Default(method.ReturnType);
            if (args[0] is EveOPreview.Mediator.Messages.Process.UpdateCpuAffinity message) affinityMessages.Add(message);
            return affinityPending.Task;
        });
        var manager = (IThumbnailManager)Activator.CreateInstance(App.GetType("EveOPreview.Services.ThumbnailManager"),
            mediator, config, monitor, window, factory, hotkeys, Stub.Create<IHookService>(), events, logger, null, preferences);
        using var lifetime = (IDisposable)manager;
        Call(manager, "UpdateThumbnailsList");
        Call(manager, "RefreshThumbnails");
        var originalViews = manager.GetAllKnownClients();
        try
        {
            Assert.True(registrations > 0, "The factory switch must preserve an actual initialized DWM session.");
            int registrationsBefore = registrations, unregistrationsBefore = unregistrations;
            var handles = originalViews.ToDictionary(pair => pair.Key, pair => ((Form)pair.Value).Handle);
            var sessions = originalViews.ToDictionary(pair => pair.Key, pair => pair.Value.GetType()
                .GetField("_thumbnail", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pair.Value));
            foreach (string renderer in new[] { "NativeComposition", "Legacy" })
            {
                preferences.SetPreviewOverlayRenderer(renderer);
                foreach (var pair in originalViews)
                {
                    var current = (ThumbnailView)manager.GetClientByPointer(pair.Key);
                    Assert.Same(pair.Value, current);
                    Assert.Equal(handles[pair.Key], current.Handle);
                    Assert.Same(sessions[pair.Key], current.GetType()
                        .GetField("_thumbnail", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(current));
                    Assert.Equal(pair.Value.Title, current.Title);
                    if (renderer == "Legacy") Assert.Equal(EveOPreview.View.Rendering.OverlayRendererKind.Legacy, current.OverlayRenderer);
                }
                Assert.Equal(registrationsBefore, registrations);
                Assert.Equal(unregistrationsBefore, unregistrations);
            }
        }
        finally { if (File.Exists(preferencesPath)) File.Delete(preferencesPath); }
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

        config.ThumbnailRefreshPeriod = 600;
        config.ThumbnailMinimumSize = new Size(240, 135);
        config.ThumbnailMaximumSize = new Size(700, 400);
        var runtimeSettings = new EveOPreview.Mediator.Handlers.Thumbnails.ThumbnailRuntimeSettingsUpdatedHandler(manager);
        runtimeSettings.Handle(new ThumbnailRuntimeSettingsUpdated(), CancellationToken.None).GetAwaiter().GetResult();
        foreach (var view in originalViews.Values)
        {
            Assert.Same(view, manager.GetClientByPointer(view.Id));
            Assert.Equal(config.ThumbnailMinimumSize, ((Form)view).MinimumSize);
            Assert.Equal(config.ThumbnailMaximumSize, ((Form)view).MaximumSize);
        }
        Assert.Equal(TimeSpan.FromMilliseconds(600), ((System.Windows.Threading.DispatcherTimer)manager.GetType()
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
        };
        var cycle = bindings.Single(b => b.Shortcut == "Control+F8");
        Assert.All(bindings, b => Assert.False(b.OnRelease));
        config.GlobalHotkeysOnRelease = true;
        events.PublishHotkeysChanged();
        Assert.All(bindings, b => Assert.True(b.OnRelease));
        preferences.SetDiagnosticHotkeyPassthrough(true);
        Assert.True(registeredPassthrough);
        config.UseWindowsHotkeys = true;
        events.PublishCurrentProfileChanged(new SelectedProfileChangedNotification(null));
        Assert.False(registeredPassthrough);
        Assert.All(bindings, b => Assert.False(b.OnRelease));
        Assert.Equal(HotkeyMode.OperatingSystem, registeredMode);
        Assert.Contains(bindings, binding => binding.Shortcut == "Control+F8");
        config.UseWindowsHotkeys = false;
        events.PublishCurrentProfileChanged(new SelectedProfileChangedNotification(null));
        Assert.Equal(HotkeyMode.Global, registeredMode);
        Assert.All(bindings, b => Assert.True(b.OnRelease));
        config.GlobalHotkeysOnRelease = false;
        events.PublishHotkeysChanged();
        Assert.All(bindings, b => Assert.False(b.OnRelease));
        var timer = Stopwatch.StartNew();
        cycle.Execute();
        Assert.True(timer.ElapsedMilliseconds < 500, "Pending affinity must not delay the input callback");
        Assert.Equal(1, activations); // No message pump or async continuation before activation/highlight.
        Assert.Equal(new IntPtr(102), manager.GetActiveClient()?.Id);
        foreach (var view in manager.GetAllKnownClients().Values)
            Assert.Equal(view.Id == new IntPtr(102), (bool)typeof(ThumbnailView).GetField("_isHighlightEnabled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view));
        // A second deliberate cycle is accepted immediately while unrelated async work is pending.
        cycle.Execute();
        Assert.Equal(new IntPtr(103), manager.GetActiveClient()?.Id);
        Assert.Equal(2, activations);
        var prediction = affinityMessages.Last();
        Call(manager, "ThumbnailUpdateTimerTick", null, EventArgs.Empty);
        Assert.Equal(prediction.ActiveWindowHandle, affinityMessages.Last().ActiveWindowHandle);
        Assert.Equal(prediction.NextWindowHandle, affinityMessages.Last().NextWindowHandle);
        Assert.Equal(prediction.PrevWindowHandle, affinityMessages.Last().PrevWindowHandle);
        // Ordinary Alt+Tab updates scheduling without issuing another focus request.
        foreground = new IntPtr(101);
        Call(manager, "ReconcileForegroundWindow");
        Assert.Equal(foreground, affinityMessages.Last().ActiveWindowHandle);
        Assert.Equal(IntPtr.Zero, affinityMessages.Last().NextWindowHandle);
        Assert.Equal(new IntPtr(103), affinityMessages.Last().PrevWindowHandle);
        Assert.Equal(2, activations);
        affinityPending.SetResult();
        config.CycleGroups.Clear();
        refresh.Handle(new RefreshHotkeys(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.DoesNotContain(bindings, binding => binding.Shortcut == "Control+F8");
        config.EnableCompatibilityMode = true;
        runtimeSettings.Handle(new ThumbnailRuntimeSettingsUpdated(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.All(manager.GetAllKnownClients().Values, view => Assert.Equal("StaticThumbnailView", view.GetType().Name));
        Assert.All(originalViews.Values, view => Assert.True(((Form)view).IsDisposed));
        config.EnableCompatibilityMode = false;
        events.PublishCurrentProfileChanged(new SelectedProfileChangedNotification(null));
        Assert.All(manager.GetAllKnownClients().Values, view => Assert.Equal("LiveThumbnailView", view.GetType().Name));
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
        // Query only this process: Process.HandleCount's system snapshot can itself
        // initialize runtime handles between the baseline and the second sample.
        Assert.True(Native.GetProcessHandleCount(host.Handle, out uint handlesBefore));
        for (int i = 0; i < 200; i++) monitor.GetUpdatedProcesses(out _, out _, out _);
        Assert.True(Native.GetProcessHandleCount(host.Handle, out uint handlesAfter));
        Assert.InRange((long)handlesAfter - handlesBefore, -10, 10);
        Assert.Equal(handle, Assert.Single(monitor.GetAllProcesses()).ProcessHandle);
        window.Text = "EVE - B";
        Application.DoEvents();
        monitor.GetUpdatedProcesses(out added, out var changed, out _);
        Assert.Empty(added);
        Assert.Equal("EVE - B", Assert.Single(changed).Title);
        Assert.Equal(handle, Assert.Single(changed).ProcessHandle);
        var config = NewConfig();
        using var cpu = new CpuAffinityService(logger, config);
        var cpuSets = new WindowsCpuSetApi();
        Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var original, out _));
        Assert.True(WindowsCpuSetApi.TryReadDefaultSets(handle, out var originalSets));
        try
        {
            cpu.UpdateAffinity(process, changed.Single(), process, monitor.GetAllProcesses());
            Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var actual, out _));
            Assert.Equal(original, actual);
            Assert.True(WindowsCpuSetApi.TryReadDefaultSets(handle, out var assigned));
            Assert.NotEmpty(assigned);
            var topology = cpuSets.ReadTopology();
            Assert.All(assigned, id => Assert.Contains(topology, c => c.Id == id));
            cpu.ResetAll(monitor.GetAllProcesses());
            Assert.True(WindowsCpuSetApi.TryReadDefaultSets(handle, out var restored));
            Assert.Equal(originalSets, restored);
            // Preserve a launcher/user CPU-set restriction, including nonempty reset.
            var restricted = assigned.Take(Math.Min(2, assigned.Length)).ToArray();
            Assert.True(cpuSets.TrySet(handle, restricted));
            cpu.UpdateAffinity(process, null, null, monitor.GetAllProcesses());
            Assert.True(WindowsCpuSetApi.TryReadDefaultSets(handle, out var restrictedActual));
            Assert.All(restrictedActual, id => Assert.Contains(id, restricted));
            cpu.ResetAll(monitor.GetAllProcesses());
            Assert.True(WindowsCpuSetApi.TryReadDefaultSets(handle, out var restrictedRestored));
            Assert.Equal(restricted, restrictedRestored);
            Assert.True(cpuSets.TrySet(handle, originalSets));
            Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var reset, out _));
            Assert.Equal(original, reset);
            cpu.Stop(monitor.GetAllProcesses());
            cpu.UpdateAffinity(process, null, null, monitor.GetAllProcesses());
            Assert.True(KernelNativeMethods.GetProcessAffinityMask(handle, out var afterStop, out _));
            Assert.Equal(original, afterStop);
            Assert.True(WindowsCpuSetApi.TryReadDefaultSets(handle, out var stoppedSets));
            Assert.Equal(originalSets, stoppedSets);
        }
        finally { Assert.True(cpuSets.TrySet(handle, originalSets)); }
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
