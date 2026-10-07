using System.Reflection;
using EveOPreview.Configuration;
using EveOPreview.Input;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using EveOPreview.View.Rendering;
using MediatR;
using Serilog;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    // Discovery snapshots are fixtures; all manager creation/removal, windows and
    // DWM relationships below are production paths with real source HWNDs.
    private static object ValidateSourceLifetime(string output, ILogger logger)
    {
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration")!)!;
        config.ThumbnailSize = new Size(384, 216); config.ThumbnailOpacity = 1;
        config.EnableThumbnailSnap = false; config.ShowThumbnailFrames = false;
        config.ShowThumbnailsAlwaysOnTop = true; config.ShowThumbnailOverlays = false;
        config.HideThumbnailsOnLostFocus = false; config.HideActiveClientThumbnail = false;
        config.EnableClientLayoutTracking = false; config.EnableAutomaticCpuAffinity = false;
        var monitor = new SourceMonitor();
        var windows = new CountingWindowManager(new WindowManager(NoOp.Create<IHookService>(), logger));
        var screen = Screen.PrimaryScreen!.WorkingArea;
        var point = new Point(screen.Left + 60, screen.Top + 80);
        IThumbnailManager? manager = null;
        var factory = new SourceViewFactory((handle, title, size) =>
        {
            var result = (ThumbnailView)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.View.LiveThumbnailView")!,
                windows, config, manager!, NoOp.Create<IMediator>(), NoOp.Create<IGlobalPointerInput>(), logger)!;
            result.SetOverlayRenderer(OverlayRendererKind.NativeComposition);
            result.Id = handle; result.Title = title; result.ThumbnailSize = size; result.ThumbnailLocation = point;
            return result;
        });
        manager = (IThumbnailManager)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Services.ThumbnailManager")!,
            NoOp.Create<IMediator>(), config, monitor, windows, factory, NoOp.Create<IHotkeyService>(), NoOp.Create<IHookService>(), NoOp.Create<IGlobalEvents>(), logger)!;
        using var managerLifetime = (IDisposable)manager;
        var known = (Dictionary<nint, IThumbnailView>)manager.GetType().GetField("_thumbnailViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        void Discover()
        {
            manager.GetType().GetMethod("UpdateThumbnailsList", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, null);
            manager.GetType().GetMethod("RefreshThumbnails", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, null);
            Pump(TimeSpan.FromMilliseconds(100));
        }
        var checks = new List<string>();
        void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException("Source lifetime proof failed: " + check);
            checks.Add(check);
        }
        void Pixel(ThumbnailView view, Color color, string name)
        {
            string path = Path.Combine(output, name + ".png");
            CaptureOwnedPreview(view, new(), path);
            using var image = new Bitmap(path);
            Require(image.GetPixel(image.Width / 2, image.Height / 2).ToArgb() == color.ToArgb(), name + ": actual DWM pixels match the live source");
        }
        nint foreground = Native.GetForegroundWindow();
        using var original = new SolidSource(Color.RoyalBlue) { Text = "EVE - Reconnect proof", Location = new(screen.Right - 410, screen.Bottom - 260), ClientSize = config.ThumbnailSize };
        original.Show();
        monitor.Next = new SourceInfo(original.Handle, original.Text);
        Discover();
        var first = (ThumbnailView)known[original.Handle];
        nint firstImage = first.Handle, firstOverlay = Overlay(first).Handle;
        int registered = windows.Registrations;
        Pixel(first, Color.RoyalBlue, "source-created");
        Native.ShowWindow(original.Handle, 7); Pump(TimeSpan.FromMilliseconds(100)); first.Refresh(true);
        Require(Native.IsIconic(original.Handle), "owned source actually minimizes");
        Require(first.Handle == firstImage && Overlay(first).Handle == firstOverlay && windows.Registrations == registered, "source minimization retains both host HWNDs and DWM registration");
        Native.ShowWindow(original.Handle, 4); Pump(TimeSpan.FromMilliseconds(150)); first.Refresh(true);
        Require(!Native.IsIconic(original.Handle), "owned source restores without activation");
        Pixel(first, Color.RoyalBlue, "source-restored");
        nint firstSource = original.Handle;
        original.Close(); monitor.Next = null; Discover();
        Require(known.Count == 0 && first.IsDisposed, "normal manager discovery closes the host when its source exits");
        Require(windows.Unregistrations == registered, "source exit releases the obsolete native DWM relationship");
        using var replacement = new SolidSource(Color.MediumSeaGreen) { Text = "EVE - Reconnect proof", Location = new(screen.Right - 410, screen.Bottom - 260), ClientSize = config.ThumbnailSize };
        replacement.Show(); monitor.Next = new SourceInfo(replacement.Handle, replacement.Text); Discover();
        var second = (ThumbnailView)known[replacement.Handle];
        Require(second.Id != firstSource && !ReferenceEquals(first, second), "same-title reconnect receives the new source HWND and a fresh production host");
        Require(windows.Registrations == registered + 1, "reconnect creates exactly one native image relationship");
        Pixel(second, Color.MediumSeaGreen, "source-reconnected");
        managerLifetime.Dispose();
        Require(second.IsDisposed && windows.Unregistrations == windows.Registrations, "manager disposal closes the replacement host and balances all native registrations");
        Require(windows.FailedUpdates == 0, "source lifetime transitions make no failed DWM updates");
        Require(Native.GetForegroundWindow() == foreground, "source minimize, restore, exit and reconnect preserve foreground");
        return new { Passed = true, Checks = checks, windows.Registrations, windows.Unregistrations, windows.FailedUpdates };
    }

    private sealed record SourceInfo(nint MainWindowHandle, string Title) : IProcessInfo
    {
        public nint ProcessHandle => nint.Zero;
        public int ProcessId => Environment.ProcessId;
    }
    private sealed class SourceMonitor : IProcessMonitor
    {
        private SourceInfo? _current;
        public SourceInfo? Next { get; set; }
        public IProcessInfo GetMainProcess() => new SourceInfo(nint.Zero, "Proof controller");
        public ICollection<IProcessInfo> GetAllProcesses() => _current is null ? [] : [_current];
        public IProcessInfo LookupCachedProcessByWindowHandle(nint windowHandle) => _current?.MainWindowHandle == windowHandle ? _current : null!;
        public void GetUpdatedProcesses(out ICollection<IProcessInfo> added, out ICollection<IProcessInfo> updated, out ICollection<IProcessInfo> removed)
        {
            bool changed = _current?.MainWindowHandle != Next?.MainWindowHandle;
            added = changed && Next is not null ? [Next] : [];
            removed = changed && _current is not null ? [_current] : [];
            updated = []; _current = Next;
        }
    }
    private sealed class SourceViewFactory(Func<nint, string, Size, IThumbnailView> create) : IThumbnailViewFactory
    {
        public IThumbnailView Create(nint id, string title, Size size) => create(id, title, size);
    }
}
