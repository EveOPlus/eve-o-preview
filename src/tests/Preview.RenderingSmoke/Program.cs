using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using EveOPreview.Input;
using MediatR;
using Serilog;
using Avalonia;
using EveOPreview.Preview;
using EveOPreview.UI;
using EveOPreview.UI.Previews;
using EveOPreview.View.Rendering;
using Application = System.Windows.Forms.Application;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
            if (args.Contains("--host-proof")) return ValidateNativeHost(options.Output);
            if (args.Contains("--mixed-dpi")) return ValidateMixedDpi(options.Output);
            if (args.Contains("--diagnostic-input")) return ValidateDiagnosticInput(options.Output);
            var liveSources = Native.FindClients();
            if (args.Contains("--input-latency")) return ValidateInputLatency(args, liveSources, options.Output);
            if (args.Contains("--production-hotkeys")) return ValidateProductionHotkeys(args, liveSources, options.Output);
            if (options.List)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { Foreground = Native.GetForegroundWindow().ToInt64(), Sources = liveSources }, JsonOptions));
                return 0;
            }

            Directory.CreateDirectory(options.Output);
            using var logger = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();
            using var pointerInput = args.Contains("--mouse-checks") ? new WindowsGlobalPointerInput(logger) : null;
            var sources = new List<Client>();
            var mockWindows = new List<MockClient>();
            var views = new List<ThumbnailView>();
            var portableOverlays = new Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow>();
            var restoredSources = new List<Client>();
            using var currentProcess = Process.GetCurrentProcess();
            long initialForeground = Native.GetForegroundWindow().ToInt64();
            try
            {
                if (options.Live)
                {
                    sources.AddRange(liveSources);
                    if (sources.Count == 0) throw new InvalidOperationException("No visible exefile client HWNDs are accessible on this desktop. --list reports discovery without creating windows.");
                    if (initialForeground == 0) throw new InvalidOperationException("Live validation requires a nonzero interactive foreground window.");
                    if (options.RestoreSources)
                    {
                        foreach (var source in sources.Where(source => source.Minimized))
                        {
                            restoredSources.Add(source);
                            Native.ShowWindow(new IntPtr(source.Handle), 4); // SW_SHOWNOACTIVATE: preserve focus and normal bounds.
                        }
                        Pump(TimeSpan.FromSeconds(2));
                    }
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var mock = new MockClient(i);
                        mock.Show();
                        mockWindows.Add(mock);
                        sources.Add(new(mock.Handle.ToInt64(), Environment.ProcessId, mock.Text, false));
                    }
                }

                var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration")!)!;
                config.EnableThumbnailSnap = false;
                config.ShowThumbnailsAlwaysOnTop = true;
                config.ShowThumbnailFrames = false;
                config.ShowThumbnailOverlays = true;
                IHookService hook = NoOp.Create<IHookService>();
                if (options.WakeExisting)
                {
                    config.FpsLimiterSettings.IsEnabled = true;
                    hook = new HookService(config, logger);
                    foreach (var source in sources)
                        if (!hook.Ping(new IntPtr(source.Handle))) throw new InvalidOperationException("Existing Robin pipe did not respond; the harness will not inject or install it.");
                }
                var wm = new WindowManager(hook, logger);
                if (!wm.IsCompositionEnabled) throw new InvalidOperationException("DWM composition is unavailable.");
                var renderer = new CountingWindowManager(wm);
                var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("No screen is available.");
                int columns = Math.Max(1, screen.WorkingArea.Width / (options.Width + 12));
                for (int i = 0; i < options.Count; i++)
                {
                    var source = sources[i % sources.Count];
                    var view = (ThumbnailView)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.View.LiveThumbnailView")!,
                        renderer, config, NoOp.Create<IThumbnailManager>(), NoOp.Create<IMediator>(), pointerInput ?? (IGlobalPointerInput)NoOp.Create<IGlobalPointerInput>(), logger)!;
                    view.SetOverlayRenderer(options.Renderer == "native" ? OverlayRendererKind.NativeComposition : OverlayRendererKind.Legacy);
                    view.Id = new IntPtr(source.Handle);
                    view.Title = source.Title;
                    view.TitleFontSettings = config.TitleFontSettings;
                    view.IsOverlayEnabled = options.Renderer != "avalonia";
                    view.SetOpacity(1);
                    view.SetFrames(false);
                    view.ThumbnailSize = new Size(options.Width, options.Height);
                    view.ThumbnailLocation = new Point(screen.WorkingArea.Left + 12 + (i % columns) * (options.Width + 12),
                        screen.WorkingArea.Top + 12 + (i / columns) * (options.Height + 12));
                    views.Add(view);
                    view.Show();
                    view.SetTopMost(true);
                    view.RestoreAndBringToFront();
                    if (options.Renderer == "native" && view.OverlayRenderer != OverlayRendererKind.NativeComposition)
                        throw new InvalidOperationException("Native composition fell back to Legacy; this is not a valid native benchmark.");
                    var stats = options.Effects == "none" ? Array.Empty<OverlayStat>() : new[] { new OverlayStat("Test text", "Synthetic", 0xFF65BBFF), new OverlayStat("Test counter", "1", 0xFFFFCB70) };
                    if (options.Renderer == "avalonia")
                    {
                        var window = new AvaloniaPreviewOverlayWindow();
                        portableOverlays.Add(view, window);
                        window.ResizePixels(new PreviewSize(options.Width, options.Height));
                        window.Position = new PixelPoint(view.Location.X, view.Location.Y);
                        var font = config.TitleFontSettings;
                        window.Renderer.SetScene(new OverlayScene
                        {
                            Title = source.Title.Replace("EVE - ", ""),
                            Font = new OverlayFont(font.Name, font.Size, (OverlayFontStyle)font.Style,
                                unchecked((uint)font.ForeColor.ToArgb()), unchecked((uint)font.OutlineColor.ToArgb()),
                                font.OutlineWidth, font.PositionOffsetFromLeft, font.PositionOffsetFromTop),
                            Stats = stats
                        });
                        window.Show();
                        var hwnd = window.TryGetPlatformHandle()!.Handle;
                        Native.SetWindowLongPtr(hwnd, -8, view.Handle); // Owner HWND.
                        Native.SetWindowLongPtr(hwnd, -20, new IntPtr(Native.GetWindowLongPtr(hwnd, -20).ToInt64() | 0x080000A0)); // No activation, tool window, pointer-transparent.
                        Native.SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x53);
                    }
                    else view.SetOverlayStats(stats);
                }
                Pump(TimeSpan.FromSeconds(1));
                VerifyWindows(views);
                if (args.Contains("--combat-simulation"))
                    return ValidateCombatSimulation(views, renderer, config, logger, options);
                long foregroundAfterShow = Native.GetForegroundWindow().ToInt64();
                bool creationCapturedFocus = OwnsForeground(views, portableOverlays);
                if (creationCapturedFocus && initialForeground != 0)
                {
                    Native.SetForegroundWindow(new IntPtr(initialForeground));
                    Pump(TimeSpan.FromMilliseconds(250));
                    if (OwnsForeground(views, portableOverlays)) throw new InvalidOperationException("Cannot restore the pre-validation foreground after creating preview windows.");
                }
                var externalBefore = ReadExternal(sources);
                var before = Metrics.Read(currentProcess);
                var graphicsBefore = ReadGraphics(views, portableOverlays);
                int initialRegistrations = renderer.Registrations;
                var updateDurations = new List<double>();
                var slowMaintenanceCalls = new List<object>();
                int focusCaptures = 0;
                var timer = Stopwatch.StartNew();
                var maintenance = Stopwatch.StartNew();
                var alertTimer = Stopwatch.StartNew();
                var sampleTimer = Stopwatch.StartNew();
                var resourceSamples = new List<object>();
                int alerts = 0;
                while (timer.Elapsed.TotalSeconds < options.Seconds)
                {
                    PumpEvents();
                    if (options.Effects is "one" or "all" && alertTimer.Elapsed.TotalSeconds >= 2)
                    {
                        ShowAlerts(views, portableOverlays, options.Effects);
                        alerts += options.Effects == "one" ? 1 : views.Count;
                        alertTimer.Restart();
                    }
                    if (maintenance.ElapsedMilliseconds >= 500)
                    {
                        long focusBefore = Native.GetForegroundWindow().ToInt64();
                        long start = Stopwatch.GetTimestamp();
                        renderer.MaintenanceBatch = updateDurations.Count + 1;
                        foreach (var (view, index) in views.Select((view, index) => (view, index)))
                        {
                            renderer.MaintenanceDwmMilliseconds = 0;
                            long viewStart = Stopwatch.GetTimestamp();
                            view.Refresh(true);
                            double viewElapsed = Stopwatch.GetElapsedTime(viewStart).TotalMilliseconds;
                            if (viewElapsed > 25) slowMaintenanceCalls.Add(new
                            {
                                Seconds = timer.Elapsed.TotalSeconds, Batch = renderer.MaintenanceBatch, Preview = index + 1,
                                ElapsedMilliseconds = viewElapsed, DwmUpdateMilliseconds = renderer.MaintenanceDwmMilliseconds,
                                RemainingHostMilliseconds = viewElapsed - renderer.MaintenanceDwmMilliseconds
                            });
                        }
                        renderer.MaintenanceBatch = 0;
                        updateDurations.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                        if (Native.GetForegroundWindow().ToInt64() != focusBefore && OwnsForeground(views, portableOverlays)) focusCaptures++;
                        maintenance.Restart();
                    }
                    if (sampleTimer.Elapsed.TotalSeconds >= 5)
                    {
                        resourceSamples.Add(new { Seconds = timer.Elapsed.TotalSeconds, Metrics = Metrics.Read(currentProcess) });
                        sampleTimer.Restart();
                    }
                    Thread.Sleep(10);
                }
                var after = Metrics.Read(currentProcess);
                double elapsed = timer.Elapsed.TotalSeconds;
                var externalAfter = ReadExternal(sources);
                var graphicsAfter = ReadGraphics(views, portableOverlays);
                if (renderer.Registrations != initialRegistrations) throw new InvalidOperationException("Healthy DWM relationships were replaced during ordinary maintenance.");
                if (renderer.FailedUpdates != 0) throw new InvalidOperationException($"DWM update failed {renderer.FailedUpdates} times.");
                if (focusCaptures != 0) throw new InvalidOperationException("A preview captured foreground focus during maintenance.");
                foreach (var view in views)
                {
                    long foreground = Native.GetForegroundWindow().ToInt64();
                    view.SetHighlight(true, 3);
                    view.Refresh(false);
                    if (!view.RestoreAndBringToFront()) throw new InvalidOperationException("Native preview/overlay raise failed.");
                    view.ClearBorder();
                    view.Refresh(false);
                    if (Native.GetForegroundWindow().ToInt64() != foreground && OwnsForeground(views, portableOverlays)) throw new InvalidOperationException("Border/raise captured foreground focus.");
                }
                VerifyWindows(views);
                object? zoomCheck = options.ZoomFactor > 1 ? ExerciseZoom(views[0], portableOverlays, currentProcess, options.ZoomFactor) : null;
                object? staggeredCheck = null;
                ActiveHighlightResult? activeHighlightCheck = options.ActiveHighlight || options.RapidSwitch
                    ? CaptureActiveHighlight(views, portableOverlays, renderer, config, logger, options.Output, initialForeground, options.RapidSwitch, args.Contains("--mouse-checks")) : null;
                Pump(TimeSpan.FromMilliseconds(250));
                if (options.Capture)
                {
                    foreach (var view in views) { view.ClearAlerts(); if (portableOverlays.TryGetValue(view, out var window)) window.Renderer.ClearAlerts(); }
                    Pump(TimeSpan.FromMilliseconds(100));
                    foreach (var (view, index) in views.Select((view, index) => (view, index))) CaptureOwnedPreview(view, portableOverlays, Path.Combine(options.Output, $"preview-{index + 1}.png"));
                    if (options.Effects == "staggered") staggeredCheck = CaptureStaggered(views, portableOverlays, options.Output);
                    else if (options.Effects != "none")
                    {
                        ShowAlerts(views, portableOverlays, options.Effects);
                        Pump(TimeSpan.FromMilliseconds(100));
                        foreach (var (view, index) in views.Select((view, index) => (view, index)))
                        {
                            CaptureOwnedPreview(view, portableOverlays, Path.Combine(options.Output, $"alert-{index + 1}.png"));
                        }
                        Pump(TimeSpan.FromMilliseconds(1200));
                        foreach (var (view, index) in views.Select((view, index) => (view, index)))
                        {
                            CaptureOwnedPreview(view, portableOverlays, Path.Combine(options.Output, $"expired-{index + 1}.png"));
                        }
                    }
                }
                var result = new
                {
                    TimestampUtc = DateTime.UtcNow, options.Live, options.RestoreSources, options.Count, options.Width, options.Height,
                    options.Renderer, options.Effects, options.WakeExisting, AlertsSubmitted = alerts, Sources = sources,
                    TestAlertShakePixels = 0,
                    InitialForeground = initialForeground, ForegroundAfterShow = foregroundAfterShow,
                    CreationCapturedFocus = creationCapturedFocus,
                    PreviewHandles = views.Select(view => new { Preview = view.Handle.ToInt64(), Overlay = Overlay(view).Handle.ToInt64() }).ToArray(),
                    FinalForeground = Native.GetForegroundWindow().ToInt64(), FocusCaptures = focusCaptures,
                    RendererCounts = new { renderer.Registrations, renderer.Updates, renderer.FailedUpdates, renderer.Unregistrations },
                    ElapsedSeconds = elapsed, LogicalProcessors = Environment.ProcessorCount,
                    CpuCorePercent = (after.CpuMilliseconds - before.CpuMilliseconds) / elapsed / 10,
                    MaintenanceMilliseconds = new { Samples = updateDurations.Count, P50 = Percentile(updateDurations, .5), P95 = Percentile(updateDurations, .95), P99 = Percentile(updateDurations, .99) },
                    SlowMaintenanceCalls = slowMaintenanceCalls, SlowDwmUpdates = renderer.SlowUpdates,
                    Before = before, After = after, ResourceSamples = resourceSamples, GraphicsBefore = graphicsBefore, GraphicsAfter = graphicsAfter,
                    ZoomCheck = zoomCheck,
                    StaggeredCheck = staggeredCheck,
                    ActiveHighlightCheck = activeHighlightCheck,
                    ExternalBefore = externalBefore, ExternalAfter = externalAfter,
                    Limitations = "Own process CPU/resources and maintenance counters; optional switching timings cover request/submission/foreground observation, not monitor scan-out or game-frame latency. CPU affinity is stubbed. Wake/prediction uses stubs unless WakeExisting is true; that mode only contacts already-installed Robin pipes. No injection, FPS configuration change, GPU/VRAM or combat telemetry. Repeated previews of two sources are not independent workloads."
                };
                string json = JsonSerializer.Serialize(result, JsonOptions);
                File.WriteAllText(Path.Combine(options.Output, "result.json"), json);
                Console.WriteLine(json);
                return activeHighlightCheck is { Passed: false } ? 1 : 0;
            }
            finally
            {
                if (initialForeground != 0 && Native.GetForegroundWindow().ToInt64() != initialForeground) Native.SetForegroundWindow(new IntPtr(initialForeground));
                foreach (var window in portableOverlays.Values) window.Close();
                foreach (var view in views) { view.Close(); view.Dispose(); }
                foreach (var mock in mockWindows) { mock.Close(); mock.Dispose(); }
                foreach (var source in restoredSources) Native.ShowWindow(new IntPtr(source.Handle), 7); // SW_SHOWMINNOACTIVE: return only originally minimized clients to that state.
                PumpEvents();
                if (restoredSources.Any(source => !Native.IsIconic(new IntPtr(source.Handle)))) throw new InvalidOperationException("An originally minimized source did not return to minimized state.");
            }
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static int ValidateProductionHotkeys(string[] args, IReadOnlyList<Client> sources, string output)
    {
        int pidIndex = Array.IndexOf(args, "--production-pid");
        if (pidIndex < 0 || pidIndex + 1 >= args.Length || sources.Count != 2)
            throw new ArgumentException("Production validation requires --production-pid and exactly two running EVE clients.");
        using var process = Process.GetProcessById(int.Parse(args[pidIndex + 1]));
        if (process.ProcessName != "EVE-O Preview") throw new InvalidOperationException("The supplied PID is not EVE-O Preview.");
        var appDirectory = Path.GetDirectoryName(process.MainModule!.FileName)!;
        // Require the same explicit F16 two-client group in every local profile so
        // an unknown active-profile choice cannot turn this into gameplay input.
        var profiles = Directory.GetFiles(Path.Combine(appDirectory, "Profiles"), "EVE-O Preview.json", SearchOption.AllDirectories);
        if (profiles.Length == 0) throw new InvalidOperationException("No production profiles available to verify the F16 binding.");
        foreach (var path in profiles)
        {
            using var profile = JsonDocument.Parse(File.ReadAllText(path));
            bool binding = profile.RootElement.GetProperty("CycleGroups").EnumerateArray().Any(group =>
                group.GetProperty("ForwardHotkeys").EnumerateArray().Any(key => key.GetString() == "F16") &&
                group.GetProperty("ClientsOrder").EnumerateObject().Select(value => value.Value.GetString()).ToHashSet()
                    .SetEquals(sources.Select(source => source.Title)));
            if (!binding || profile.RootElement.GetProperty("ActiveClientHighlightColor").GetString() != "Lime" ||
                profile.RootElement.GetProperty("ThumbnailsOpacity").GetDouble() != 1 ||
                !profile.RootElement.GetProperty("EnableActiveClientHighlight").GetBoolean())
                throw new InvalidOperationException("This bounded production check requires F16 mapped to these two clients and an opaque Lime active border in every profile.");
        }
        var previews = Native.FindPreviewWindows(process.Id, sources);
        if (previews.Count != 2) throw new InvalidOperationException("Two visible production previews could not be identified by PID and source title.");
        Directory.CreateDirectory(output);
        var originalForeground = Native.GetForegroundWindow();
        Native.GetCursorPos(out var cursor);
        var samples = new List<object>(); var pixelSamples = new List<object>();
        int missedFocusDeadlines = 0, missedBorders = 0;
        using var sampler = new SamplingWaiter();
        try
        {
            var first = previews[0];
            var point = new Point(first.Bounds.Left + first.Bounds.Width / 2, first.Bounds.Top + first.Bounds.Height / 2);
            Native.SetCursorPos(point.X, point.Y);
            Native.WaitForMessages(20);
            VerifyInput();
            var hit = Native.WindowFromPoint(point);
            var hitRoot = Native.GetAncestor(hit, 2);
            bool ownedNativeOverlay = Native.GetWindow(hitRoot, 4) == first.Hwnd &&
                (Native.GetWindowLongPtr(hitRoot, -20).ToInt64() & 0x00200000) != 0;
            if (hitRoot != first.Hwnd && !ownedNativeOverlay)
                throw new InvalidOperationException($"The guarded production preview is obscured; refusing to click. Preview={first.Hwnd}, hit={hitRoot}, owner={Native.GetWindow(hitRoot, 4)}, point={point}");
            Native.ClickMouse();
            var startup = Stopwatch.StartNew();
            while (Native.GetForegroundWindow().ToInt64() != sources[0].Handle && startup.ElapsedMilliseconds < 1000)
            { PumpEvents(); Native.WaitForMessages(1); }
            if (Native.GetForegroundWindow().ToInt64() != sources[0].Handle) throw new InvalidOperationException("The initial production preview click did not activate its source.");
            Pump(TimeSpan.FromMilliseconds(100));
            var clock = Stopwatch.StartNew();
            double nextInput = 0, previousInput = 0;
            for (int i = 0; i < 200; i++)
            {
                while (clock.Elapsed.TotalMilliseconds < nextInput) { PumpEvents(); sampler.Wait(); }
                VerifyInput();
                int index = (i + 1) % 2;
                double sentAt = clock.Elapsed.TotalMilliseconds;
                // A missed deadline must not generate catch-up bursts. Record the
                // actual cadence, and never send faster than 20 switches/second.
                nextInput = sentAt + 50;
                var input = Stopwatch.StartNew();
                Native.PressF16();
                while (Native.GetForegroundWindow().ToInt64() != sources[index].Handle && input.Elapsed.TotalMilliseconds < 50)
                { PumpEvents(); sampler.Wait(); }
                double elapsed = input.Elapsed.TotalMilliseconds;
                long foreground = Native.GetForegroundWindow().ToInt64();
                bool focused = foreground == sources[index].Handle;
                if (!focused || elapsed >= 50) missedFocusDeadlines++;
                samples.Add(new { Index = i, ExpectedSource = sources[index].Handle, ActualForeground = foreground,
                    InputAtMilliseconds = sentAt, InputIntervalMilliseconds = i == 0 ? (double?)null : sentAt - previousInput,
                    ForegroundObservedMilliseconds = elapsed, ForegroundConfirmed = focused });
                previousInput = sentAt;
            }
            double rapidElapsed = clock.Elapsed.TotalSeconds;
            var positions = previews.Select(view => new Point(view.Bounds.Left + view.Bounds.Width / 2, view.Bounds.Top)).ToArray();
            var origin = new Point(positions.Min(p => p.X), positions.Min(p => p.Y));
            using var pixels = new Bitmap(positions.Max(p => p.X) - origin.X + 1, positions.Max(p => p.Y) - origin.Y + 1);
            using var graphics = Graphics.FromImage(pixels);
            int Pixel(int index) => pixels.GetPixel(positions[index].X - origin.X, positions[index].Y - origin.Y).ToArgb();
            for (int i = 0; i < 20; i++)
            {
                VerifyInput();
                int index = (i + 1) % 2;
                var input = Stopwatch.StartNew();
                Native.PressF16();
                bool correct;
                do
                {
                    PumpEvents();
                    graphics.CopyFromScreen(origin, Point.Empty, pixels.Size);
                    correct = Pixel(index) == Color.Lime.ToArgb() && Pixel(1 - index) != Color.Lime.ToArgb();
                } while (!correct && input.ElapsedMilliseconds < 250);
                pixelSamples.Add(new { Index = i, VisibleBorderObservedMilliseconds = input.Elapsed.TotalMilliseconds, CorrectPixels = correct,
                    ActualForeground = Native.GetForegroundWindow().ToInt64(), ExpectedSource = sources[index].Handle });
                // Border correctness is independent of whether Windows has already
                // completed the asynchronous source activation at this screenshot.
                if (!correct) missedBorders++;
                Pump(TimeSpan.FromMilliseconds(50));
            }
            var result = new { TimestampUtc = DateTime.UtcNow, ProductionPid = process.Id, ProcessPath = process.MainModule!.FileName,
                Hotkey = "F16", TargetSwitchesPerSecond = 20, Switches = 200, ElapsedSeconds = rapidElapsed,
                MissedFocus50msDeadlines = missedFocusDeadlines, IncorrectBorders = missedBorders,
                Passed = missedFocusDeadlines == 0 && missedBorders == 0, UsesProductionWakeAndAffinity = true,
                Samples = samples, PixelSamples = pixelSamples, PixelTimingsIncludeReadback = true };
            string json = JsonSerializer.Serialize(result, JsonOptions);
            File.WriteAllText(Path.Combine(output, "production-result.json"), json);
            Console.WriteLine(json);
            return missedFocusDeadlines == 0 && missedBorders == 0 ? 0 : 1;
        }
        finally
        {
            Native.SetCursorPos(cursor.X, cursor.Y);
            Native.SetForegroundWindow(originalForeground);
        }
        void VerifyInput()
        {
            if (process.HasExited || Native.GetAsyncKeyState(0x10) < 0 || Native.GetAsyncKeyState(0x11) < 0 || Native.GetAsyncKeyState(0x12) < 0 || Native.GetAsyncKeyState(0x7F) < 0)
                throw new InvalidOperationException("Production process exited or a modifier/F16 is held; stopping automation.");
        }
    }
    // Validation-only sampling timer. Coarse message-wait timeouts can round a
    // requested 1 ms wait up to a scheduler tick and distort focus observations.
    private sealed class SamplingWaiter : IDisposable
    {
        private nint _timer = CreateWaitableTimerExW(0, null, 2, 0x00100002); // high resolution; synchronize + modify
        public SamplingWaiter()
        {
            if (_timer == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        public void Wait()
        {
            long due = -10_000; // relative 1 ms, in 100 ns units
            if (!SetWaitableTimer(_timer, ref due, 0, 0, 0, false))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            if (MsgWaitForMultipleObjectsEx(1, ref _timer, uint.MaxValue, 0x04FF, 4) == uint.MaxValue)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        public void Dispose() { if (_timer != 0) { CloseHandle(_timer); _timer = 0; } }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint CreateWaitableTimerExW(nint attributes, string? name, uint flags, uint access);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWaitableTimer(nint timer, ref long due, int period, nint callback, nint state, [MarshalAs(UnmanagedType.Bool)] bool resume);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint MsgWaitForMultipleObjectsEx(uint count, ref nint handles, uint timeout, uint mask, uint flags);
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(nint handle);
    }
    private static bool OwnsForeground(IEnumerable<ThumbnailView> views, Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow> portableOverlays)
    {
        var hwnd = Native.GetForegroundWindow();
        return views.Any(view => hwnd == view.Handle || hwnd == Overlay(view).Handle)
            || portableOverlays.Values.Any(window => hwnd == window.TryGetPlatformHandle()?.Handle);
    }
    private static ThumbnailOverlay Overlay(ThumbnailView view) => (ThumbnailOverlay)typeof(ThumbnailView).GetField("_overlay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
    private static object[] ReadGraphics(IEnumerable<ThumbnailView> views, Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow> portableOverlays) => views.Select(view =>
    {
        if (portableOverlays.TryGetValue(view, out var window)) return (object)new { Backend = "avalonia", window.Renderer.SceneRenderCount, window.Renderer.SceneUpdateCount };
        var renderer = typeof(ThumbnailOverlay).GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Overlay(view));
        return renderer is NativeCompositionOverlayRenderer native ? (object)new { Backend = "native", native.SurfaceUploadCount, native.CommitCount, native.SceneSurfacePixels, native.AlertSurfacePixels } : new { Backend = "legacy" };
    }).ToArray();
    private static object ExerciseZoom(ThumbnailView view, Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow> portableOverlays, Process process, int factor)
    {
        var bounds = view.Bounds;
        var foreground = Native.GetForegroundWindow();
        var before = Metrics.Read(process);
        typeof(ThumbnailView).GetMethod("SaveWindowSizeAndLocation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null);
        try
        {
            view.ZoomIn(ViewZoomAnchor.NW, factor);
            view.Refresh(true);
            if (portableOverlays.TryGetValue(view, out var window)) window.ResizePixels(new PreviewSize(view.ClientSize.Width, view.ClientSize.Height));
            ShowAlerts([view], portableOverlays, "one");
            // The regular hover path already has the pointer inside before ZoomIn.
            // This scripted call must not synthesize a later MouseEnter over the
            // enlarged window and overwrite its original hover geometry snapshot.
            Native.DwmFlush();
            var zoomed = view.Bounds;
            var graphics = ReadGraphics([view], portableOverlays);
            var metrics = Metrics.Read(process);
            if (Native.GetForegroundWindow() != foreground) throw new InvalidOperationException("Hover zoom captured foreground.");
            return new { Factor = factor, OriginalBounds = bounds, ZoomedBounds = zoomed, Before = before, Zoomed = metrics, Graphics = graphics };
        }
        finally
        {
            view.ClearAlerts();
            view.ZoomOut();
            view.Refresh(true);
            if (portableOverlays.TryGetValue(view, out var window))
            {
                window.Renderer.ClearAlerts();
                window.ResizePixels(new PreviewSize(view.ClientSize.Width, view.ClientSize.Height));
            }
            if (view.Bounds != bounds) throw new InvalidOperationException("Hover zoom did not restore the saved preview geometry.");
        }
    }
    private static object CaptureStaggered(IReadOnlyList<ThumbnailView> views, Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow> portableOverlays, string output)
    {
        if (views.Count < 2) throw new InvalidOperationException("Staggered validation requires at least two previews.");
        void Alert(ThumbnailView view, PreviewAlert alert)
        {
            if (portableOverlays.TryGetValue(view, out var window)) window.Renderer.ShowAlert(alert);
            else view.ShowAlert(alert);
        }
        var watch = Stopwatch.StartNew();
        var phases = new List<object>();
        void Until(double seconds) { double remaining = seconds - watch.Elapsed.TotalSeconds; if (remaining > 0) Pump(TimeSpan.FromSeconds(remaining)); }
        void CapturePhase(string name)
        {
            double elapsed = watch.Elapsed.TotalSeconds;
            for (int i = 0; i < 2; i++) CaptureOwnedPreview(views[i], portableOverlays, Path.Combine(output, name + $"-{i + 1}.png"));
            phases.Add(new { Phase = name, ElapsedSeconds = elapsed, Graphics = ReadGraphics(views.Take(2), portableOverlays) });
        }
        Alert(views[0], new PreviewAlert(Color: 0xFFFF4038, DurationSeconds: 2, Intensity: .65, ShakePixels: 0));
        Until(.45);
        CapturePhase("staggered-first-only");
        Until(.8);
        string firstBeforeSecond = JsonSerializer.Serialize(ReadGraphics([views[0]], portableOverlays));
        double secondStarted = watch.Elapsed.TotalSeconds;
        Alert(views[1], new PreviewAlert(Color: 0xFF35C9FF, DurationSeconds: 1.8, Intensity: .85, ShakePixels: 0));
        string firstAfterSecond = JsonSerializer.Serialize(ReadGraphics([views[0]], portableOverlays));
        if (firstBeforeSecond != firstAfterSecond) throw new InvalidOperationException("Starting the second alert changed the first renderer's resources or transaction count.");
        Until(1.1);
        CapturePhase("staggered-overlap");
        Until(2.15);
        CapturePhase("staggered-second-only");
        Until(secondStarted + 2.1);
        CapturePhase("staggered-expired");
        return new { FirstDurationSeconds = 2, FirstShakePixels = 0, SecondStartedSeconds = secondStarted, SecondDurationSeconds = 1.8, SecondShakePixels = 0, FirstUnchangedWhenStartingSecond = true, Phases = phases };
    }
    private sealed record ActiveHighlightResult(bool Passed, IReadOnlyList<object> Stages, IReadOnlyList<object> ActivationRequests,
        int RegistrationsBefore, int RegistrationsAfter, object? RapidSwitching, object? MouseChecks);
    private static ActiveHighlightResult CaptureActiveHighlight(IReadOnlyList<ThumbnailView> views, Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow> portableOverlays,
        CountingWindowManager windowManager, IThumbnailConfiguration config, ILogger logger, string output, long initialForeground, bool rapidSwitch, bool mouseChecks)
    {
        if (views.Count != 2 || views[0].Id == views[1].Id) throw new InvalidOperationException("Active highlight validation requires two distinct source clients.");
        config.EnableActiveClientHighlight = true;
        config.ActiveClientHighlightColor = Color.GreenYellow;
        config.MinimizeInactiveClients = false;
        config.HideActiveClientThumbnail = false;
        config.EnableClientLayoutTracking = false;
        config.ThumbnailOpacity = 1; // Pixel comparisons require an opaque configured frame.
        config.ThumbnailRefreshPeriod = 60_000; // External-focus checks must finish without a discovery tick.
        var manager = (IThumbnailManager)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Services.ThumbnailManager")!,
            NoOp.Create<IMediator>(), config, NoOp.Create<IProcessMonitor>(), windowManager, NoOp.Create<IThumbnailViewFactory>(),
            NoOp.Create<IHotkeyService>(), NoOp.Create<IHookService>(), NoOp.Create<IGlobalEvents>(), logger)!;
        var known = (Dictionary<IntPtr, IThumbnailView>)manager.GetType().GetField("_thumbnailViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        foreach (var view in views) known.Add(view.Id, view);
        var selection = manager.GetType().GetMethod("SetActive")!;
        var clickedHandler = manager.GetType().GetMethod("ThumbnailActivated", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Action? enteringActivation = null;
        foreach (var view in views) view.ThumbnailActivated = hwnd => { enteringActivation?.Invoke(); clickedHandler.Invoke(manager, [hwnd]); };
        NativeCompositionOverlayRenderer GraphicsFor(ThumbnailView view) => (NativeCompositionOverlayRenderer)typeof(ThumbnailOverlay)
            .GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Overlay(view))!;
        bool Highlighted(ThumbnailView view) => (bool)typeof(ThumbnailView).GetField("_isHighlightEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        int registrations = windowManager.Registrations;
        var stages = new List<object>();
        var activationRequests = new List<object>();
        bool passed = true;
        Native.GetCursorPos(out var originalCursor);
        var clickAttempted = new bool[2];
        Color Edge(ThumbnailView view, string filename)
        {
            CaptureOwnedPreview(view, portableOverlays, Path.Combine(output, filename));
            using var bitmap = new Bitmap(Path.Combine(output, filename));
            return bitmap.GetPixel(bitmap.Width / 2, 0);
        }
        void Select(int index)
        {
            long commitsBefore = 0;
            int updatesBefore = 0;
            string? activationOrderFailure = null;
            // Establish the boundary when the production click callback actually
            // starts. Pointer travel and its message pump may complete unrelated
            // pending resize/layout work before that callback on another process.
            enteringActivation = () => { commitsBefore = views.Sum(view => GraphicsFor(view).CommitCount); updatesBefore = windowManager.Updates; };
            windowManager.BeforeActivation = hwnd =>
            {
                if (manager.GetActiveClient()?.Id != hwnd || views.Sum(view => GraphicsFor(view).CommitCount) != commitsBefore || windowManager.Updates != updatesBefore)
                {
                    activationOrderFailure = $"Graphics maintenance preceded the source focus request: selected={manager.GetActiveClient()?.Id}, requested={hwnd}, commits={commitsBefore}->{views.Sum(view => GraphicsFor(view).CommitCount)}, updates={updatesBefore}->{windowManager.Updates}.";
                    throw new InvalidOperationException(activationOrderFailure);
                }
            };
            bool clicked = false;
            long clickTarget = 0;
            if (!clickAttempted[index])
            {
                clickAttempted[index] = true;
                views[index].RestoreAndBringToFront();
                var point = views[index].PointToScreen(new Point(views[index].ClientSize.Width / 2, views[index].ClientSize.Height / 2));
                Native.SetCursorPos(point.X, point.Y);
                Pump(TimeSpan.FromMilliseconds(30));
                Native.GetCursorPos(out var actualPointer);
                var hit = Native.WindowFromPoint(actualPointer);
                var root = Native.GetAncestor(hit, 2);
                if ((root == views[index].Handle || root == Overlay(views[index]).Handle) &&
                    Native.GetAsyncKeyState(0x10) >= 0 && Native.GetAsyncKeyState(0x11) >= 0 && Native.GetAsyncKeyState(0x12) >= 0)
                {
                    // Verify again immediately before input. Never click a source
                    // game window or another application's window.
                    Native.GetCursorPos(out var verifiedPointer);
                    if (verifiedPointer != actualPointer || Native.WindowFromPoint(verifiedPointer) != hit) throw new InvalidOperationException("Preview click target changed before input.");
                    clickTarget = hit.ToInt64();
                    Native.ClickMouse();
                    clicked = true;
                }
            }
            if (!clicked) { enteringActivation(); selection.Invoke(manager, [new KeyValuePair<IntPtr, IThumbnailView>(views[index].Id, views[index])]); }
            var activationWait = Stopwatch.StartNew();
            while ((Native.GetForegroundWindow() != views[index].Id || manager.GetActiveClient()?.Id != views[index].Id) && activationWait.Elapsed.TotalSeconds < 2) Pump(TimeSpan.FromMilliseconds(25));
            if (!Highlighted(views[index]) || Highlighted(views[1 - index]))
                throw new InvalidOperationException(activationOrderFailure ?? $"Selection frame was not submitted after activation: source={index}, clicked={clicked}, selected={manager.GetActiveClient()?.Id}, requested={views[index].Id}, foreground={Native.GetForegroundWindow()}, highlighted={Highlighted(views[index])}, previous={Highlighted(views[1 - index])}.");
            passed &= Native.GetForegroundWindow() == views[index].Id;
            activationRequests.Add(new { RequestedSource = views[index].Id.ToInt64(), ActualForeground = Native.GetForegroundWindow().ToInt64(),
                SourceBecameForeground = Native.GetForegroundWindow() == views[index].Id, WaitMilliseconds = activationWait.Elapsed.TotalMilliseconds,
                OwnPreviewClicked = clicked, VerifiedClickHandle = clickTarget });
            if (OwnsForeground(views, portableOverlays)) throw new InvalidOperationException("A preview captured foreground during source activation.");
        }
        windowManager.AllowSourceActivation = true;
        try
        {
            foreach (var testCase in new[] { (Thickness: 1, Color: Color.GreenYellow), (Thickness: 5, Color: Color.DeepSkyBlue) })
            {
                int thickness = testCase.Thickness;
                config.ActiveClientHighlightThickness = thickness;
                config.ActiveClientHighlightColor = testCase.Color;
                foreach (var view in views) view.ClearAlerts();
                Select(0);
                var firstEdge = Edge(views[0], $"highlight-{thickness}-first-1.png");
                Edge(views[1], $"highlight-{thickness}-first-2.png");
                Select(1);
                var cleared = Edge(views[0], $"highlight-{thickness}-second-1.png");
                var baseline = Edge(views[1], $"highlight-{thickness}-second-2.png");
                bool correctColor = baseline.ToArgb() == testCase.Color.ToArgb() && firstEdge.ToArgb() == testCase.Color.ToArgb();
                bool clearedPrevious = cleared.ToArgb() != testCase.Color.ToArgb();
                var foregroundBeforeFlash = Native.GetForegroundWindow();
                views[1].ShowAlert(new PreviewAlert(DurationSeconds: 1, Intensity: 1, ShakePixels: 0));
                Pump(TimeSpan.FromMilliseconds(100));
                var during = Edge(views[1], $"highlight-{thickness}-flash-2.png");
                bool visibleDuringFlash = during.ToArgb() == baseline.ToArgb();
                Pump(TimeSpan.FromMilliseconds(1150));
                var expired = Edge(views[1], $"highlight-{thickness}-expired-2.png");
                bool retainedAfter = expired.ToArgb() == baseline.ToArgb();
                bool stillSelected = manager.GetActiveClient()?.Id == views[1].Id && Highlighted(views[1]) && !Highlighted(views[0]);
                bool focusPreserved = Native.GetForegroundWindow() == foregroundBeforeFlash;
                passed &= correctColor && clearedPrevious && visibleDuringFlash && retainedAfter && stillSelected && focusPreserved;
                stages.Add(new { Thickness = thickness, ConfiguredColor = testCase.Color.Name, AlertIntensity = 1, AlertShakePixels = 0,
                    CorrectConfiguredColor = correctColor, BorderUnchangedDuringFlash = visibleDuringFlash,
                    PreviousBorderCleared = clearedPrevious,
                    BorderRetainedAfterExpiry = retainedAfter, SelectionCorrect = stillSelected, ForegroundPreserved = focusPreserved,
                    BaselineArgb = unchecked((uint)baseline.ToArgb()), DuringFlashArgb = unchecked((uint)during.ToArgb()), ExpiredArgb = unchecked((uint)expired.ToArgb()) });
            }
            object? rapid = rapidSwitch ? MeasureRapidSwitching() : null;
            enteringActivation = null;
            windowManager.BeforeActivation = null;
            object? mouse = mouseChecks ? ValidateThumbnailMouse(views, manager, config) : null;
            passed &= windowManager.Registrations == registrations;
            return new(passed, stages, activationRequests, registrations, windowManager.Registrations, rapid, mouse);
        }
        finally
        {
            windowManager.AllowSourceActivation = false;
            windowManager.BeforeActivation = null;
            windowManager.AfterActivation = null;
            foreach (var view in views) { view.ClearAlerts(); view.ClearBorder(); }
            foreach (var view in views) view.ThumbnailActivated = null;
            known.Clear();
            ((IDisposable)manager).Dispose();
            Native.SetCursorPos(originalCursor.X, originalCursor.Y);
            if (initialForeground != 0) Native.SetForegroundWindow(new IntPtr(initialForeground));
        }

        object MeasureRapidSwitching()
        {
            windowManager.BeforeActivation = null;
            manager.Start();
            if (manager.GetType().GetField("_foregroundObserver", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager) == null)
                throw new InvalidOperationException("Foreground observer did not start.");
            // Warm both retained color pixels. The measured loop uses the production
            // cycling route; native IPC/CPU-affinity collaborators remain explicit stubs.
            Select(0); Select(1);
            var order = new SortedDictionary<int, string> { [0] = views[0].Title, [1] = views[1].Title };
            var cycle = manager.GetType().GetMethod("CycleNextClient")!.CreateDelegate<Action<bool, SortedDictionary<int, string>>>(manager);
            var focusEntry = new List<double>(); var focusReturn = new List<double>();
            var outlineSubmitted = new List<double>(); var foregroundObserved = new List<double>();
            var samples = new List<object>();
            int missed = 0, deadlinesMissed = 0, updatesBefore = windowManager.Updates;
            long uploadsBefore = views.Sum(view => GraphicsFor(view).SurfaceUploadCount);
            var run = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++)
            {
                while (run.Elapsed.TotalMilliseconds < i * 50) { PumpEvents(); Native.WaitForMessages(1); }
                int index = i % 2;
                long commitsBefore = views.Sum(view => GraphicsFor(view).CommitCount);
                int requests = 0;
                var input = Stopwatch.StartNew();
                windowManager.BeforeActivation = hwnd =>
                {
                    requests++;
                    if (hwnd != views[index].Id || views.Sum(view => GraphicsFor(view).CommitCount) != commitsBefore)
                        throw new InvalidOperationException("Focus was delayed by graphics during cycling.");
                    focusEntry.Add(input.Elapsed.TotalMilliseconds);
                };
                windowManager.AfterActivation = _ => focusReturn.Add(input.Elapsed.TotalMilliseconds);
                cycle(true, order);
                double submitted = input.Elapsed.TotalMilliseconds;
                outlineSubmitted.Add(submitted);
                bool selected = manager.GetActiveClient()?.Id == views[index].Id && GraphicsFor(views[index]).ActiveBorder != null && GraphicsFor(views[1 - index]).ActiveBorder == null;
                while (Native.GetForegroundWindow() != views[index].Id && input.ElapsedMilliseconds < 250)
                { PumpEvents(); Native.WaitForMessages(1); }
                double observed = input.Elapsed.TotalMilliseconds;
                foregroundObserved.Add(observed);
                bool focused = Native.GetForegroundWindow() == views[index].Id;
                if (requests != 1 || !selected || !focused) missed++;
                if (observed >= 50) deadlinesMissed++;
                samples.Add(new { Index = i, Source = views[index].Id.ToInt64(), FrameSubmittedMilliseconds = submitted,
                    ForegroundObservedMilliseconds = observed, CorrectSelection = selected, ForegroundConfirmed = focused, FocusRequests = requests });
            }
            windowManager.BeforeActivation = _ => throw new InvalidOperationException("Foreground notification must never reactivate a source.");
            windowManager.AfterActivation = null;
            int imageUpdates = windowManager.Updates - updatesBefore;
            long uploads = views.Sum(view => GraphicsFor(view).SurfaceUploadCount) - uploadsBefore;
            double elapsed = run.Elapsed.TotalSeconds;
            Pump(TimeSpan.FromMilliseconds(50)); // Drain the final cycle's queued foreground notification.
            var external = new List<double>(); var presented = new List<double>();
            var externalSamples = new List<object>();
            // A separate, instrumented pass verifies external foreground notifications
            // and visible border pixels. Screen reads/waits are never used in production
            // or included in the sustained-cycle submission benchmark above.
            var edgePoints = views.Select(view => view.PointToScreen(new Point(view.ClientSize.Width / 2, 0))).ToArray();
            var sampleOrigin = new Point(edgePoints.Min(p => p.X), edgePoints.Min(p => p.Y));
            using var pixels = new Bitmap(edgePoints.Max(p => p.X) - sampleOrigin.X + 1, edgePoints.Max(p => p.Y) - sampleOrigin.Y + 1);
            using var pixelReader = Graphics.FromImage(pixels);
            int Pixel(int index) => pixels.GetPixel(edgePoints[index].X - sampleOrigin.X, edgePoints[index].Y - sampleOrigin.Y).ToArgb();
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    int index = i % 2;
                    var input = Stopwatch.StartNew();
                    bool foregroundAccepted = Native.SetForegroundWindow(views[index].Id);
                    while (manager.GetActiveClient()?.Id != views[index].Id && input.ElapsedMilliseconds < 500)
                    { PumpEvents(); Native.WaitForMessages(1); }
                    external.Add(input.Elapsed.TotalMilliseconds);
                    var point = views[index].PointToScreen(new Point(views[index].ClientSize.Width / 2, 0));
                    var other = views[1 - index].PointToScreen(new Point(views[1 - index].ClientSize.Width / 2, 0));
                    int argb = config.ActiveClientHighlightColor.ToArgb();
                    bool pixelsCorrect;
                    do
                    {
                        PumpEvents();
                        pixelReader.CopyFromScreen(sampleOrigin, Point.Empty, pixels.Size);
                        pixelsCorrect = Pixel(index) == argb && Pixel(1 - index) != argb;
                    } while (!pixelsCorrect && input.ElapsedMilliseconds < 500);
                    presented.Add(input.Elapsed.TotalMilliseconds);
                    externalSamples.Add(new { Index = i, ForegroundAccepted = foregroundAccepted, SelectedSource = manager.GetActiveClient()?.Id.ToInt64(),
                        ActualForeground = Native.GetForegroundWindow().ToInt64(), ExpectedArgb = argb,
                        SelectedPixelArgb = Pixel(index), PreviousPixelArgb = Pixel(1 - index),
                        RendererBorder = GraphicsFor(views[index]).ActiveBorder, PixelsCorrect = pixelsCorrect });
                    if (!pixelsCorrect || manager.GetActiveClient()?.Id != views[index].Id || Native.GetForegroundWindow() != views[index].Id) missed++;
                    Pump(TimeSpan.FromMilliseconds(30));
                }
                CaptureOwnedPreview(views[0], portableOverlays, Path.Combine(output, "external-final-first.png"));
                CaptureOwnedPreview(views[1], portableOverlays, Path.Combine(output, "external-final-second.png"));
            }
            finally { manager.Stop(); }
            bool success = missed == 0 && deadlinesMissed == 0 && imageUpdates == 0 && uploads == 0;
            passed &= success;
            return new { Passed = success, Switches = 200, TargetSwitchesPerSecond = 20, ElapsedSeconds = elapsed,
                MissedSelections = missed, Missed50msDeadlines = deadlinesMissed, DwmImageUpdates = imageUpdates, SurfaceUploads = uploads,
                FocusRequestEntryMilliseconds = Distribution(focusEntry), FocusRequestReturnedMilliseconds = Distribution(focusReturn),
                OutlineSubmittedMilliseconds = Distribution(outlineSubmitted), ForegroundObservedMilliseconds = Distribution(foregroundObserved),
                ExternalFocusToOutlineSubmittedMilliseconds = Distribution(external),
                ExternalFocusToPixelsObservedMilliseconds = Distribution(presented),
                PixelMeasurementIncludesScreenReadback = true, Samples = samples, ExternalSamples = externalSamples };
        }
        static object Distribution(List<double> values)
        {
            var sorted = values.Order().ToArray();
            return new { Count = sorted.Length, Median = sorted[sorted.Length / 2], P95 = sorted[(int)((sorted.Length - 1) * .95)],
                P99 = sorted[(int)((sorted.Length - 1) * .99)], Maximum = sorted[^1] };
        }
    }
    private static void VerifyWindows(IEnumerable<ThumbnailView> views)
    {
        foreach (var view in views)
        {
            if (!Native.IsWindowVisible(view.Handle) || !Native.IsWindowVisible(Overlay(view).Handle)) throw new InvalidOperationException("Preview/overlay native visibility was lost.");
            bool above = false;
            for (var hwnd = Native.GetWindow(view.Handle, 3); hwnd != IntPtr.Zero; hwnd = Native.GetWindow(hwnd, 3)) if (hwnd == Overlay(view).Handle) { above = true; break; }
            if (!above) throw new InvalidOperationException("Overlay is below its DWM image.");
        }
    }
    private static void ShowAlerts(IEnumerable<ThumbnailView> views, Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow> portableOverlays, string effects)
    {
        foreach (var view in effects == "one" ? views.Take(1) : views)
        {
            var alert = new PreviewAlert(DurationSeconds: 1, Intensity: .3, ShakePixels: 0);
            if (portableOverlays.TryGetValue(view, out var window)) window.Renderer.ShowAlert(alert);
            else view.ShowAlert(alert);
        }
    }
    private static void CaptureOwnedPreview(ThumbnailView view, Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow> portableOverlays, string path)
    {
        view.RestoreAndBringToFront();
        if (portableOverlays.TryGetValue(view, out var window)) Native.SetWindowPos(window.TryGetPlatformHandle()!.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x53);
        Native.DwmFlush();
        var bounds = new Rectangle(view.PointToScreen(Point.Empty), view.ClientSize);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    private static void PumpEvents()
    {
        Application.DoEvents(); // WinForms is retained only for synthetic source/input fixtures.
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }
    private static void Pump(TimeSpan duration) { var timer = Stopwatch.StartNew(); while (timer.Elapsed < duration) { PumpEvents(); Thread.Sleep(10); } }
    private static double Percentile(List<double> values, double percentile) => values.Count == 0 ? 0 : values.Order().ElementAt(Math.Clamp((int)Math.Ceiling(values.Count * percentile) - 1, 0, values.Count - 1));
    private static List<object> ReadExternal(IEnumerable<Client> clients)
    {
        var result = new List<object>();
        foreach (int pid in clients.Select(client => client.ProcessId).Concat(Process.GetProcessesByName("dwm").Select(process => { using (process) return process.Id; })).Distinct())
        {
            try { using var process = Process.GetProcessById(pid); result.Add(new { ProcessId = pid, Metrics = Metrics.Read(process) }); }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { result.Add(new { ProcessId = pid, Unavailable = error.Message }); }
        }
        return result;
    }
}

internal sealed record Options(bool List, bool Live, bool Capture, bool RestoreSources, int Count, int Width, int Height, int Seconds, string Output, string Renderer, string Effects, int ZoomFactor, bool ActiveHighlight, bool RapidSwitch, bool WakeExisting)
{
    public static Options Parse(string[] args)
    {
        string Value(string name, string fallback) { int index = Array.IndexOf(args, name); return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing value for {name}."); }
        string renderer = Value("--renderer", "legacy");
        string effects = Value("--effects", "none");
        if (renderer is not ("legacy" or "native" or "avalonia")) throw new ArgumentException("--renderer must be legacy, native or avalonia.");
        if (effects is not ("none" or "one" or "all" or "staggered")) throw new ArgumentException("--effects must be none, one, all or staggered.");
        if (renderer == "legacy" && effects != "none") throw new ArgumentException("Legacy does not support rich overlays; benchmark effects with native or avalonia.");
        if ((args.Contains("--active-highlight") || args.Contains("--rapid-switch")) && renderer != "native") throw new ArgumentException("Highlight/switch validation requires --renderer native.");
        if (args.Contains("--mouse-checks") && !args.Contains("--input-latency") && !args.Contains("--active-highlight") && !args.Contains("--rapid-switch"))
            throw new ArgumentException("--mouse-checks requires --active-highlight, --rapid-switch or --input-latency.");
        if (args.Contains("--wake-existing") && (!args.Contains("--live") || !args.Contains("--rapid-switch"))) throw new ArgumentException("--wake-existing requires --live --rapid-switch and already-running Robin endpoints.");
        return new(args.Contains("--list"), args.Contains("--live"), args.Contains("--capture"), args.Contains("--restore-sources"), Math.Clamp(int.Parse(Value("--count", "2")), 1, 24),
            Math.Clamp(int.Parse(Value("--width", "384")), 160, 1024), Math.Clamp(int.Parse(Value("--height", "216")), 90, 768),
            Math.Clamp(int.Parse(Value("--seconds", "15")), 1, 600), Path.GetFullPath(Value("--output", Path.Combine(AppContext.BaseDirectory, "rendering-smoke"))), renderer, effects,
            Math.Clamp(int.Parse(Value("--zoom", "1")), 1, 25), args.Contains("--active-highlight"), args.Contains("--rapid-switch"), args.Contains("--wake-existing"));
    }
}
internal sealed record Client(long Handle, int ProcessId, string Title, bool Minimized);
internal sealed record Metrics(double CpuMilliseconds, long WorkingSet, long PrivateBytes, int Handles, uint GdiObjects, uint UserObjects)
{
    public static Metrics Read(Process process) { process.Refresh(); return new(process.TotalProcessorTime.TotalMilliseconds, process.WorkingSet64, process.PrivateMemorySize64, process.HandleCount, Native.GetGuiResources(process.Handle, 0), Native.GetGuiResources(process.Handle, 1)); }
}

internal sealed class CountingWindowManager(IWindowManager inner) : IWindowManager
{
    public int Registrations, Updates, FailedUpdates, Unregistrations;
    public int MaintenanceBatch;
    public double MaintenanceDwmMilliseconds;
    public List<object> SlowUpdates { get; } = new();
    public bool AllowSourceActivation;
    public Action<IntPtr>? BeforeActivation;
    public Action<IntPtr>? AfterActivation;
    public bool IsCompositionEnabled => inner.IsCompositionEnabled;
    public bool IsCurrentlySwitching { get => inner.IsCurrentlySwitching; set => inner.IsCurrentlySwitching = value; }
    public IntPtr GetForegroundWindowHandle() => inner.GetForegroundWindowHandle();
    public IDwmThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source) { Registrations++; return new CountingThumbnail(inner.GetLiveThumbnail(destination, source), this); }
    public Image GetStaticThumbnail(IntPtr source) => inner.GetStaticThumbnail(source);
    public (int Left, int Top, int Right, int Bottom) GetWindowPosition(IntPtr handle) => inner.GetWindowPosition(handle);
    public bool IsWindowMaximized(IntPtr handle) => inner.IsWindowMaximized(handle);
    public bool IsWindowMinimized(IntPtr handle) => inner.IsWindowMinimized(handle);
    // Validation must never change a source client, send IPC or install hooks.
    public void ActivateWindow(IntPtr handle)
    {
        if (!AllowSourceActivation) throw new InvalidOperationException("Source activation requires the explicit active-highlight check.");
        BeforeActivation?.Invoke(handle);
        inner.ActivateWindow(handle);
        AfterActivation?.Invoke(handle);
    }
    public void MinimizeWindow(IntPtr handle, bool enableAnimation) => throw new InvalidOperationException("Source mutation is not part of this renderer harness.");
    public void MoveWindow(IntPtr handle, int left, int top, int width, int height) => throw new InvalidOperationException("Source mutation is not part of this renderer harness.");
    public void MaximizeWindow(IntPtr handle) => throw new InvalidOperationException("Source mutation is not part of this renderer harness.");
    public void PredictUpcomingClient(IntPtr upcomingHandle)
    {
        if (!AllowSourceActivation) throw new InvalidOperationException("Prediction requires the explicit switching check.");
        inner.PredictUpcomingClient(upcomingHandle); // No-op unless --wake-existing explicitly selects existing endpoints.
    }
    private sealed class CountingThumbnail(IDwmThumbnail inner, CountingWindowManager owner) : IDwmThumbnail
    {
        public void Register(IntPtr destination, IntPtr source) => inner.Register(destination, source);
        public void Move(int left, int top, int right, int bottom) => inner.Move(left, top, right, bottom);
        public bool Update()
        {
            owner.Updates++;
            long start = Stopwatch.GetTimestamp();
            try { bool result = inner.Update(); if (!result) owner.FailedUpdates++; return result; }
            finally
            {
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (owner.MaintenanceBatch > 0) owner.MaintenanceDwmMilliseconds += elapsed;
                if (elapsed > 25) owner.SlowUpdates.Add(new { Batch = owner.MaintenanceBatch, ElapsedMilliseconds = elapsed });
            }
        }
        public void Unregister() { owner.Unregistrations++; inner.Unregister(); }
    }
}

public class NoOp : DispatchProxy
{
    public static T Create<T>() where T : class => Create<T, NoOp>();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var type = method!.ReturnType;
        if (method.Name == "GetMainProcess") return Create<IProcessInfo>();
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type == typeof(Task<bool>)) return Task.FromResult(true);
        return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
    }
}

internal sealed class MockClient : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly int _index;
    public MockClient(int index)
    {
        _index = index; Text = $"EVE - Mock renderer source {index + 1}"; ClientSize = new Size(900, 600);
        StartPosition = FormStartPosition.Manual; Location = new Point(30 + index * 80, 430);
        BackColor = Color.FromArgb(15, 27, 39); DoubleBuffered = true;
        _timer.Tick += (_, _) => Invalidate(); _timer.Start();
    }
    protected override bool ShowWithoutActivation => true;
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        for (int i = 0; i < 40; i++) e.Graphics.FillEllipse(Brushes.LightSlateGray, (i * 149 + _index * 21) % Width, (i * 89) % Height, 2, 2);
        float x = 50 + (float)((_clock.Elapsed.TotalSeconds * 90) % (Width - 100));
        e.Graphics.FillEllipse(Brushes.SteelBlue, x, 230, 70, 70);
        e.Graphics.DrawString(Text + "\n" + _clock.Elapsed.ToString(@"mm\:ss\.ff"), SystemFonts.DefaultFont, Brushes.White, 20, 20);
    }
    protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }
}

internal static class Native
{
    public sealed record PreviewWindow(nint Hwnd, Rectangle Bounds);
    public static List<PreviewWindow> FindPreviewWindows(int pid, IReadOnlyList<Client> sources)
    {
        var windows = new Dictionary<string, PreviewWindow>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint owner);
            if (owner == pid && IsWindowVisible(hwnd))
            {
                var title = new StringBuilder(512); GetWindowText(hwnd, title, title.Capacity);
                if (sources.Any(source => source.Title == title.ToString()))
                {
                    GetClientRect(hwnd, out var bounds); var origin = new Point(); ClientToScreen(hwnd, ref origin);
                    windows[title.ToString()] = new(hwnd, new Rectangle(origin.X, origin.Y, bounds.Right, bounds.Bottom));
                }
            }
            return true;
        }, 0);
        return sources.Where(source => windows.ContainsKey(source.Title)).Select(source => windows[source.Title]).ToList();
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref Point point);
    public static List<Client> FindClients()
    {
        var ids = Process.GetProcessesByName("exefile").Select(process => { using (process) return process.Id; }).ToHashSet();
        var clients = new List<Client>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (ids.Contains((int)pid) && IsWindowVisible(hwnd))
            {
                var title = new StringBuilder(512); GetWindowText(hwnd, title, title.Capacity);
                if (title.Length > 0) clients.Add(new(hwnd.ToInt64(), (int)pid, title.ToString(), IsIconic(hwnd)));
            }
            return true;
        }, IntPtr.Zero);
        return clients.OrderBy(client => client.ProcessId).ToList();
    }
    private delegate bool EnumWindowProc(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder title, int maximum);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint MsgWaitForMultipleObjectsEx(uint count, nint handles, uint milliseconds, uint wakeMask, uint flags);
    public static void WaitForMessages(uint milliseconds) => MsgWaitForMultipleObjectsEx(0, 0, milliseconds, 0x04FF, 4);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] private struct Input { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public MouseInput Mouse; [FieldOffset(8)] public KeyboardInput Keyboard; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] input, int size);
    public static Func<Point, bool>? MouseDownGuard;
    private static void VerifyMouseDown()
    {
        if (MouseDownGuard is null) return;
        GetCursorPos(out var point);
        if (!MouseDownGuard(point)) throw new InvalidOperationException($"Actual cursor {point} is outside the owned preview/menu; no mouse button was sent.");
    }
    public static void ClickMouse()
    {
        VerifyMouseDown();
        Input[] input = [new() { Type = 0, Mouse = new() { Flags = 2 } }, new() { Type = 0, Mouse = new() { Flags = 4 } }];
        if (SendInput(2, input, Marshal.SizeOf<Input>()) != 2) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not send the verified own-preview click.");
    }
    public static void PressF16()
    {
        Input[] input = [new() { Type = 1, Keyboard = new() { Key = 0x7F } }, new() { Type = 1, Keyboard = new() { Key = 0x7F, Flags = 2 } }];
        if (SendInput(2, input, Marshal.SizeOf<Input>()) != 2) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not send the verified production cycling hotkey.");
    }
    public static void KeyTransition(ushort key, bool down)
    {
        Input[] input = [new() { Type = 1, Keyboard = new() { Key = key, Flags = down ? 0u : 2u } }];
        if (SendInput(1, input, Marshal.SizeOf<Input>()) != 1)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not send the test hotkey transition.");
    }
    public static void MouseTransition(bool right, bool down)
    {
        if (down) VerifyMouseDown();
        Input[] input = [new() { Type = 0, Mouse = new() { Flags = right ? down ? 8u : 16u : down ? 2u : 4u } }];
        if (SendInput(1, input, Marshal.SizeOf<Input>()) != 1)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not send the guarded thumbnail mouse transition.");
    }
    public static void MoveMouseTo(int x, int y)
    {
        var desktop = SystemInformation.VirtualScreen;
        Input[] input = [new() { Type = 0, Mouse = new()
        {
            X = (int)Math.Round((x - desktop.Left) * 65535.0 / (desktop.Width - 1)),
            Y = (int)Math.Round((y - desktop.Top) * 65535.0 / (desktop.Height - 1)), Flags = 0xC001
        } }];
        if (SendInput(1, input, Marshal.SizeOf<Input>()) != 1)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not send guarded thumbnail mouse movement.");
    }
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process, uint flag);
    [DllImport("dwmapi.dll")] public static extern int DwmFlush();
}
