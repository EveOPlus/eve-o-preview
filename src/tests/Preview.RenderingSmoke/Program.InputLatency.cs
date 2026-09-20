using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Mediator.Handlers.Process;
using EveOPreview.Mediator.Messages.Process;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using EveOPreview.View.Rendering;
using EveOPreview.Input;
using MediatR;
using Serilog;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    // Explicit opt-in live test; no saved profile edits or new Robin injection.
    private static int ValidateInputLatency(string[] args, IReadOnlyList<Client> sources, string output)
    {
        if (!args.Contains("--live") || sources.Count < 2) throw new InvalidOperationException("--input-latency requires --live and at least two EVE windows.");
        if (Process.GetProcessesByName("EVE-O Preview").Length != 0)
            throw new InvalidOperationException("Close EVE-O Preview before the isolated input test to avoid competing shortcuts/affinity changes.");
        foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x7F, 0x80 })
            if ((Native.GetAsyncKeyState(key) & 0x8000) != 0) throw new InvalidOperationException("Release modifiers and F16/F17 before the input test.");
        Directory.CreateDirectory(output);
        using var logger = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration")!)!;
        config.EnableClientLayoutTracking = false;
        config.EnablePerClientThumbnailLayouts = false;
        config.MinimizeInactiveClients = false;
        config.ShowThumbnailsAlwaysOnTop = true;
        config.EnableActiveClientHighlight = true;
        config.ThumbnailOpacity = 1;
        config.HideActiveClientThumbnail = false;
        config.EnableAutomaticCpuAffinity = true;
        var order = new SortedDictionary<int, string>();
        for (int i = 0; i < sources.Count; i++) order.Add(i, sources[i].Title);
        config.CycleGroups.Add(new() { ClientsOrder = order, ForwardHotkeys = ["Control+F16"], BackwardHotkeys = ["Control+F17"] });
        var monitor = (IProcessMonitor)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Services.Implementation.ProcessMonitor")!, logger)!;
        using var monitorLifetime = (IDisposable)monitor;
        monitor.GetUpdatedProcesses(out _, out _, out _);
        using var affinity = new CpuAffinityService(logger, config);
        var affinityHandler = new UpdateCpuAffinityHandler(logger, monitor, affinity);
        var mediator = LatencyMediator.Create(affinityHandler);
        var hook = new HookService(config, logger);
        var robin = sources.Select(s => new { s.Handle, Version = hook.GetVersionAsync((nint)s.Handle).GetAwaiter().GetResult() }).ToArray();
        config.FpsLimiterSettings.IsEnabled = robin.All(r => r.Version != null);
        var windows = new CountingWindowManager(new WindowManager(hook, logger)) { AllowSourceActivation = true };
        using var input = new WindowsHotkeyService(logger);
        using var mouseEvents = new WindowsGlobalPointerInput(logger);
        var preferences = new ApplicationPreferences(Path.Combine(output, "input-test.settings.json"), logger);
        config.UseWindowsHotkeys = args.Contains("--windows-hotkeys");
        var manager = (IThumbnailManager)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Services.ThumbnailManager")!,
            mediator, config, monitor, windows, NoOp.Create<IThumbnailViewFactory>(), input, hook, new GlobalEvents(), logger, null!, preferences)!;
        var views = new List<ThumbnailView>();
        var known = (Dictionary<nint, IThumbnailView>)manager.GetType().GetField("_thumbnailViews", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
        nint original = Native.GetForegroundWindow();
        bool ctrlDown = false;
        var samples = new List<InputSample>();
        double? unmatchedInputDuringUiStallMs = null;
        object? mouseChecks = null;
        long requested = 0, returned = 0;
        windows.BeforeActivation = _ => Volatile.Write(ref requested, Stopwatch.GetTimestamp());
        windows.AfterActivation = _ => Volatile.Write(ref returned, Stopwatch.GetTimestamp());
        try
        {
            if (!string.IsNullOrEmpty(input.RegistrationWarning)) throw new InvalidOperationException(input.RegistrationWarning);
            int index = 0;
            foreach (var source in sources)
            {
                var view = (ThumbnailView)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.View.LiveThumbnailView")!,
                    windows, config, manager, mediator, mouseEvents, logger)!;
                view.SetOverlayRenderer(OverlayRendererKind.NativeComposition);
                view.Id = (nint)source.Handle; view.Title = source.Title;
                view.TitleFontSettings = config.TitleFontSettings;
                view.IsOverlayEnabled = true; view.SetOpacity(1); view.SetFrames(false);
                view.ThumbnailSize = new(240, 135);
                view.ThumbnailLocation = new(12 + index++ * 250, 12);
                known.Add(view.Id, view); views.Add(view);
                view.Show(); view.SetTopMost(true); view.RestoreAndBringToFront();
            }
            // Set a known starting selection before the timed hotkey sequence.
            manager.GetType().GetMethod("SetActive")!.Invoke(manager, [new KeyValuePair<nint, IThumbnailView>(views[0].Id, views[0])]);
            Pump(TimeSpan.FromSeconds(1));
            using var wait = new SamplingWaiter();
            int current = 0;
            Native.KeyTransition(0xA2, true); ctrlDown = true;
            for (int i = 0; i < 100; i++)
            {
                // Keep Ctrl held over 50 forward cycles, then 50 backward cycles.
                bool forward = i < 50;
                current = (current + (forward ? 1 : sources.Count - 1)) % sources.Count;
                nint expected = views[current].Id;
                Volatile.Write(ref requested, 0); Volatile.Write(ref returned, 0);
                using var armed = new ManualResetEventSlim();
                using var go = new ManualResetEventSlim();
                var observer = Task.Run(() =>
                {
                    using var sampling = new SamplingWaiter();
                    armed.Set(); go.Wait();
                    var deadline = Stopwatch.StartNew();
                    while (Native.GetForegroundWindow() != expected && deadline.ElapsedMilliseconds < 1500) sampling.Wait();
                    long foreground = Native.GetForegroundWindow() == expected ? Stopwatch.GetTimestamp() : 0;
                    long accepted = 0;
                    if (foreground != 0 && SendMessageTimeout(expected, 0, 0, 0, 2, 200, out _) != 0)
                        accepted = Stopwatch.GetTimestamp();
                    return (foreground, accepted);
                });
                armed.Wait();
                long sent = Stopwatch.GetTimestamp(); go.Set();
                ushort key = forward ? (ushort)0x7F : (ushort)0x80;
                Native.KeyTransition(key, true); Native.KeyTransition(key, false);
                while (!observer.IsCompleted) { PumpEvents(); wait.Wait(); }
                var measured = observer.GetAwaiter().GetResult();
                double? Ms(long timestamp) => timestamp == 0 ? null : Stopwatch.GetElapsedTime(sent, timestamp).TotalMilliseconds;
                samples.Add(new(i, forward, expected.ToInt64(), Ms(Volatile.Read(ref requested)), Ms(Volatile.Read(ref returned)), Ms(measured.foreground), Ms(measured.accepted)));
                if (measured.foreground == 0 || requested == 0) break;
                // No catch-up bursts: a 50 ms minimum gap between input events.
                while (Stopwatch.GetElapsedTime(sent).TotalMilliseconds < 50) { PumpEvents(); wait.Wait(); }
            }
            Native.KeyTransition(0xA2, false); ctrlDown = false;
            unmatchedInputDuringUiStallMs = MeasureUnmatchedInputDuringUiStall();
            if (args.Contains("--mouse-checks")) mouseChecks = ValidateThumbnailMouse(views, manager, config);
        }
        finally
        {
            Native.KeyTransition(0x7F, false); Native.KeyTransition(0x80, false);
            if (ctrlDown) Native.KeyTransition(0xA2, false);
            manager.Stop();
            affinity.Stop(monitor.GetAllProcesses());
            ((IDisposable)manager).Dispose();
            foreach (var source in sources.Where(s => s.Minimized)) Native.ShowWindow((nint)source.Handle, 6);
            if (original != 0) Native.SetForegroundWindow(original);
        }
        object Summary(Func<InputSample, double?> select)
        {
            var values = samples.Select(select).Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray();
            double? Percentile(double p) => values.Length == 0 ? null : values[(int)Math.Ceiling((values.Length - 1) * p)];
            return new { Count = values.Length, P50Ms = Percentile(.5), P95Ms = Percentile(.95), P99Ms = Percentile(.99), MaxMs = values.Length == 0 ? (double?)null : values[^1] };
        }
        var result = new { Mode = config.UseWindowsHotkeys ? "Windows" : "Global", Clients = sources.Count, Samples = samples.Count,
            WakeExistingRobin = config.FpsLimiterSettings.IsEnabled, Robin = robin,
            Affinity = true, Renderer = "NativeComposition", HeldModifier = "Ctrl", InputToRequest = Summary(s => s.RequestMs),
            InputToForeground = Summary(s => s.ForegroundMs), InputToProcessedMessage = Summary(s => s.ProcessedMessageMs),
            RequestToForeground = Summary(s => s.ForegroundMs - s.RequestMs), RequestToProcessedMessage = Summary(s => s.ProcessedMessageMs - s.RequestMs),
            FocusMisses = samples.Count(s => s.ForegroundMs == null), MessageTimeouts = samples.Count(s => s.ProcessedMessageMs == null),
            UnmatchedInputDuring250msUiStallMs = unmatchedInputDuringUiStallMs,
            MouseChecks = mouseChecks,
            Measurement = "Foreground sampled independently at approximately 1 ms; WM_NULL response confirms message processing, not frame presentation or game input handling.", Detail = samples };
        File.WriteAllText(Path.Combine(output, "input-latency.json"), JsonSerializer.Serialize(result, JsonOptions));
        Console.WriteLine(JsonSerializer.Serialize(new { result.Mode, result.Clients, result.Samples, result.WakeExistingRobin,
            result.InputToRequest, result.InputToForeground, result.InputToProcessedMessage, result.RequestToForeground,
            result.FocusMisses, result.MessageTimeouts, result.UnmatchedInputDuring250msUiStallMs, result.MouseChecks }, JsonOptions));
        return samples.Count == 100 && samples.All(s => s.ForegroundMs != null && s.ProcessedMessageMs != null)
            && unmatchedInputDuringUiStallMs < 100 ? 0 : 2;
    }

    private static double MeasureUnmatchedInputDuringUiStall()
    {
        // A separate recipient UI thread models another application's input queue. Send only
        // F24 to this guarded test window, never to EVE. Keep the owner's UI deliberately blocked.
        var ready = new TaskCompletionSource<Form>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new Form { Text = "EVE-O input responsiveness check", Width = 360, Height = 100, KeyPreview = true };
            form.KeyDown += (_, e) => { if (e.KeyCode == Keys.F24) received.TrySetResult(Stopwatch.GetTimestamp()); };
            form.Shown += (_, _) => ready.SetResult(form);
            Application.Run(form);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var receiver = ready.Task.GetAwaiter().GetResult();
        nint handle = receiver.Handle;
        try
        {
            Native.SetForegroundWindow(handle);
            var deadline = Stopwatch.StartNew();
            while (Native.GetForegroundWindow() != handle && deadline.ElapsedMilliseconds < 1000) Pump(TimeSpan.FromMilliseconds(5));
            if (Native.GetForegroundWindow() != handle) throw new InvalidOperationException("The test input recipient could not obtain focus.");
            using var go = new ManualResetEventSlim();
            var sender = Task.Run(() =>
            {
                go.Wait();
                if (Native.GetForegroundWindow() != handle) throw new InvalidOperationException("Input-test focus changed; no key was sent.");
                long sent = Stopwatch.GetTimestamp();
                Native.KeyTransition(0x87, true); Native.KeyTransition(0x87, false);
                long accepted = received.Task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                return Stopwatch.GetElapsedTime(sent, accepted).TotalMilliseconds;
            });
            go.Set();
            Thread.Sleep(250); // Test-only UI stall, never a production input path.
            return sender.GetAwaiter().GetResult();
        }
        finally { receiver.BeginInvoke(() => receiver.Close()); thread.Join(2000); }
    }

    private sealed record InputSample(int Index, bool Forward, long Target, double? RequestMs, double? RequestReturnedMs, double? ForegroundMs, double? ProcessedMessageMs);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeout(nint window, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nint result);
}

public class LatencyMediator : DispatchProxy
{
    private UpdateCpuAffinityHandler _affinity = null!;
    public static IMediator Create(UpdateCpuAffinityHandler affinity)
    {
        var proxy = Create<IMediator, LatencyMediator>();
        ((LatencyMediator)proxy)._affinity = affinity;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (args?.FirstOrDefault() is UpdateCpuAffinity update) return _affinity.Handle(update, CancellationToken.None);
        return method!.ReturnType == typeof(Task) ? Task.CompletedTask : null;
    }
}
