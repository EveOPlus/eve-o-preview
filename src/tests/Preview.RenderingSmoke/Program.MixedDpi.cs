using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using EveOPreview.Configuration;
using EveOPreview.Input;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using EveOPreview.View.Rendering;
using MediatR;
using Serilog;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    // Moves only harness-owned HWNDs. The operating system supplies the actual
    // WM_DPICHANGED transitions; no synthetic DPI messages or user scale changes.
    private static int ValidateMixedDpi(string output)
    {
        Directory.CreateDirectory(output);
        var displays = Screen.AllScreens.Select(screen =>
        {
            var center = new Point(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2);
            int result = MixedDpiNative.GetDpiForMonitor(MixedDpiNative.MonitorFromPoint(center, 2), 0, out uint dpi, out _);
            if (result != 0) Marshal.ThrowExceptionForHR(result);
            return new MixedDpiDisplay(screen.DeviceName, screen.Bounds, screen.WorkingArea, dpi);
        }).OrderBy(display => display.Dpi).ThenBy(display => display.Bounds.Left).ToArray();
        if (displays.Select(display => display.Dpi).Distinct().Count() < 2)
            throw new InvalidOperationException("Mixed-DPI proof requires at least two monitors with different actual scale settings.");
        nint foreground = Native.GetForegroundWindow();
        if (foreground == 0) throw new InvalidOperationException("Mixed-DPI proof requires the interactive Windows desktop.");
        using var logger = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration")!)!;
        config.EnableThumbnailSnap = false;
        config.ShowThumbnailsAlwaysOnTop = true;
        config.ShowThumbnailOverlays = true;
        config.ThumbnailOpacity = 1;
        var windows = new CountingWindowManager(new WindowManager(NoOp.Create<IHookService>(), logger));
        var primary = Screen.PrimaryScreen!.WorkingArea;
        using var source = new SolidSource(Color.FromArgb(15, 90, 180))
        {
            Location = new Point(primary.Right - 450, primary.Bottom - 300),
            ClientSize = new Size(384, 216)
        };
        source.Show();
        var checks = new List<string>();
        var stages = new List<object>();
        var pixels = new Size(337, 191); // Fractional DIPs at 125/150/200% expose rounding drift.
        void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException("Mixed-DPI proof failed: " + check);
            checks.Add(check);
        }
        ThumbnailView Create(bool frames, Point position, Size client)
        {
            var view = (ThumbnailView)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.View.LiveThumbnailView")!,
                windows, config, NoOp.Create<IThumbnailManager>(), NoOp.Create<IMediator>(), NoOp.Create<IGlobalPointerInput>(), logger)!;
            view.SetOverlayRenderer(OverlayRendererKind.NativeComposition);
            view.Id = source.Handle;
            view.Title = "EVE - Mixed DPI proof";
            view.IsOverlayEnabled = true;
            view.TitleFontSettings = config.TitleFontSettings;
            view.SetOpacity(1);
            view.SetFrames(frames);
            view.ThumbnailSize = client;
            view.ThumbnailLocation = position;
            view.Show();
            view.SetTopMost(true);
            view.RestoreAndBringToFront();
            Pump(TimeSpan.FromMilliseconds(200));
            return view;
        }
        void Verify(ThumbnailView view, MixedDpiDisplay display, bool frames, string stage)
        {
            Pump(TimeSpan.FromMilliseconds(100));
            view.Refresh(true);
            Pump(TimeSpan.FromMilliseconds(80));
            uint dpi = MixedDpiNative.GetDpiForWindow(view.Handle);
            MixedDpiNative.GetClientRect(view.Handle, out var client);
            MixedDpiNative.GetWindowRect(view.Handle, out var outer);
            var origin = Point.Empty;
            MixedDpiNative.ClientToScreen(view.Handle, ref origin);
            var overlay = Overlay(view);
            Require(dpi == display.Dpi && Math.Abs(view.RenderScaling - dpi / 96d) < .0001,
                stage + ": Avalonia and Win32 observe the target monitor DPI " + dpi);
            Require(client.Size == pixels && view.ClientSize == pixels && view.ThumbnailSize == pixels,
                stage + ": persisted/native client stays exactly 337x191 physical pixels");
            Require(outer.Size == view.Size && outer.Location == view.Location,
                stage + ": public outer geometry matches the native window rectangle");
            Require(frames ? outer.Size.Width > pixels.Width && outer.Size.Height > pixels.Height : outer.Size == pixels,
                stage + ": client and outer frame dimensions remain distinct where needed");
            Require(overlay.ClientSize == pixels && overlay.Location == origin,
                stage + ": overlay matches the physical client origin and extent");
            Require(MixedDpiNative.GetDpiForWindow(overlay.Handle) == dpi,
                stage + ": image and owned overlay use the same monitor DPI");
            Require(view.PointToClient(view.PointToScreen(new Point(13, 17))) == new Point(13, 17),
                stage + ": signed screen/client coordinates round trip exactly");
            Require(view.OverlayRenderer == OverlayRendererKind.NativeComposition,
                stage + ": overlay keeps the native composition renderer");
            stages.Add(new { Stage = stage, Frames = frames, Display = display.Name, Dpi = dpi,
                Scaling = view.RenderScaling, Client = client.Size, Outer = outer.Size, Location = outer.Location,
                ClientOrigin = origin, OverlayLocation = overlay.Location, OverlaySize = overlay.ClientSize });
        }
        try
        {
            foreach (bool frames in new[] { false, true })
            {
                string frameMode = frames ? "frame" : "borderless";
                var first = displays[0];
                using var view = Create(frames, new Point(first.Work.Left + 100, first.Work.Top + 100), pixels);
                int registrations = windows.Registrations;
                nint handle = view.Handle, overlayHandle = Overlay(view).Handle;
                int resized = 0;
                view.ThumbnailResized = _ => resized++;
                foreach (var (display, index) in displays.Concat(displays.Reverse()).Select((display, index) => (display, index)))
                {
                    var target = new Point(display.Work.Left + 100, display.Work.Top + 100);
                    view.ThumbnailLocation = target;
                    string stage = frameMode + "-move-" + index;
                    Verify(view, display, frames, stage);
                    Require(view.Location == target, stage + ": moving across DPI retains the requested signed outer origin");
                    var before = view.Bounds;
                    typeof(ThumbnailView).GetMethod("MouseEnter_Handler", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, [view, EventArgs.Empty]);
                    view.ZoomIn(ViewZoomAnchor.NW, 2);
                    Pump(TimeSpan.FromMilliseconds(80));
                    Require(view.ClientSize == new Size(pixels.Width * 2, pixels.Height * 2), stage + ": hover doubles physical client dimensions");
                    view.ZoomOut();
                    Verify(view, display, frames, stage + "-hover-restored");
                    Require(view.Bounds == before, stage + ": hover restore has no pixel drift");
                }
                Require(resized == 0, frameMode + ": monitor transitions and hover never publish a user resize");
                Require(view.Handle == handle && Overlay(view).Handle == overlayHandle && windows.Registrations == registrations,
                    frameMode + ": crossings and hover retain both HWNDs and the original DWM registration");
                // Recreate the production host from round-tripped physical settings,
                // including a scaled and a signed-coordinate monitor.
                foreach (var display in displays)
                {
                    view.ThumbnailLocation = new Point(display.Work.Left + 100, display.Work.Top + 100);
                    Verify(view, display, frames, frameMode + "-save");
                    string capturePath = Path.Combine(output, frameMode + "-dpi-" + display.Dpi + "-x-" + display.Bounds.Left + ".png");
                    CaptureOwnedPreview(view, new Dictionary<ThumbnailView, EveOPreview.UI.Previews.AvaloniaPreviewOverlayWindow>(), capturePath);
                    using (var image = new Bitmap(capturePath))
                    {
                        var center = image.GetPixel(pixels.Width / 2, pixels.Height / 2);
                        Require(Math.Abs(center.R - 15) <= 2 && Math.Abs(center.G - 90) <= 2 && Math.Abs(center.B - 180) <= 2,
                            frameMode + ": DWM image pixels survive on monitor DPI " + display.Dpi);
                    }
                    var settings = new MixedDpiGeometry(view.Location.X, view.Location.Y, view.ThumbnailSize.Width, view.ThumbnailSize.Height, frames);
                    string path = Path.Combine(output, frameMode + "-saved-geometry.json");
                    File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
                    var saved = JsonSerializer.Deserialize<MixedDpiGeometry>(File.ReadAllText(path))!;
                    view.Hide();
                    for (int reopen = 0; reopen < 2; reopen++)
                    {
                        using var restored = Create(saved.Frames, new Point(saved.X, saved.Y), new Size(saved.Width, saved.Height));
                        Verify(restored, display, frames, frameMode + "-reopen-" + reopen);
                        Require(restored.Location == new Point(saved.X, saved.Y), frameMode + ": recreated host retains saved physical position");
                    }
                    view.Show(); view.RestoreAndBringToFront();
                }
                Require(windows.FailedUpdates == 0, frameMode + ": every native DWM update succeeded");
            }
            Require(Native.GetForegroundWindow() == foreground, "actual monitor crossings, hover and host recreation preserve foreground");
            var result = new { Passed = true, Displays = displays, Checks = checks, Stages = stages,
                UserScaleSettingsChanged = false, GameplayInputSent = false, DwmRegistrations = windows.Registrations, FailedDwmUpdates = windows.FailedUpdates };
            File.WriteAllText(Path.Combine(output, "mixed-dpi.json"), JsonSerializer.Serialize(result, JsonOptions));
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }
        finally
        {
            if (Native.GetForegroundWindow() != foreground) Native.SetForegroundWindow(foreground);
        }
    }

    private sealed record MixedDpiDisplay(string Name, Rectangle Bounds, Rectangle Work, uint Dpi);
    private sealed record MixedDpiGeometry(int X, int Y, int Width, int Height, bool Frames);
    private static class MixedDpiNative
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left, Top, Right, Bottom;
            public readonly Size Size => new(Right - Left, Bottom - Top);
            public readonly Point Location => new(Left, Top);
        }
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint window);
        [DllImport("user32.dll")] public static extern nint MonitorFromPoint(Point point, uint flags);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
        [DllImport("user32.dll")] public static extern bool GetClientRect(nint window, out Rect rectangle);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(nint window, out Rect rectangle);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(nint window, ref Point point);
    }
}
