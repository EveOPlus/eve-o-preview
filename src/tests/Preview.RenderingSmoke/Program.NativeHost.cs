using System.Text.Json;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Input;
using EveOPreview.Preview;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.Services.Implementation;
using EveOPreview.View;
using EveOPreview.View.Rendering;
using MediatR;
using Serilog;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    // Real compositor regression with owned solid-color source/backdrop fixtures.
    // Screen readback is validation only; production remains persistent DWM + DComp.
    private static int ValidateNativeHost(string output)
    {
        Directory.CreateDirectory(output);
        nint foreground = Native.GetForegroundWindow();
        if (foreground == 0) throw new InvalidOperationException("Host proof requires the interactive Windows desktop.");
        using var logger = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration")!)!;
        config.EnableThumbnailSnap = false; config.ShowThumbnailFrames = false;
        config.ShowThumbnailsAlwaysOnTop = true; config.ShowThumbnailOverlays = true;
        config.ActiveClientHighlightColor = Color.Lime; config.ThumbnailOpacity = 1;
        var windows = new CountingWindowManager(new WindowManager(NoOp.Create<IHookService>(), logger));
        var screen = Screen.PrimaryScreen!.WorkingArea;
        var location = new Point(screen.Left + 60, screen.Top + 80);
        var size = new Size(384, 216);
        var sourceColor = Color.FromArgb(15, 90, 180);
        using var source = new SolidSource(sourceColor) { Location = new(screen.Right - 410, screen.Bottom - 260), ClientSize = size };
        using var backdrop = new SolidSource(Color.Black) { Location = location, ClientSize = size, TopMost = true };
        source.Show(); backdrop.Show();
        using var view = (ThumbnailView)Activator.CreateInstance(typeof(ThumbnailView).Assembly.GetType("EveOPreview.View.LiveThumbnailView")!,
            windows, config, NoOp.Create<IThumbnailManager>(), NoOp.Create<IMediator>(), NoOp.Create<IGlobalPointerInput>(), logger)!;
        var stages = new List<object>();
        var checks = new List<string>();
        var portable = new Dictionary<ThumbnailView, EveOPreview.UI.Previews.AvaloniaPreviewOverlayWindow>();
        void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException("Native host proof failed: " + check);
            checks.Add(check);
        }
        Bitmap Capture(string stage)
        {
            Pump(TimeSpan.FromMilliseconds(100));
            string path = Path.Combine(output, stage + ".png");
            CaptureOwnedPreview(view, portable, path);
            return new Bitmap(path);
        }
        static bool Near(Color actual, Color expected, int tolerance = 2) =>
            Math.Abs(actual.R - expected.R) <= tolerance && Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance;
        static string Argb(Color color) => unchecked((uint)color.ToArgb()).ToString("X8");
        try
        {
            view.SetOverlayRenderer(OverlayRendererKind.NativeComposition);
            view.Id = source.Handle; view.Title = "EVE - Native host proof";
            view.IsOverlayEnabled = false; view.SetOpacity(1); view.SetFrames(false);
            view.ThumbnailSize = size; view.ThumbnailLocation = location;
            view.Show(); view.SetTopMost(true); view.RestoreAndBringToFront();
            Require(view.OverlayRenderer == OverlayRendererKind.NativeComposition, "native renderer initialized on the Avalonia overlay HWND");
            nint imageHandle = view.Handle, overlayHandle = Overlay(view).Handle;
            int registrations = windows.Registrations;
            using (var image = Capture("opaque-image"))
            {
                var center = image.GetPixel(size.Width / 2, size.Height / 2);
                stages.Add(new { Stage = "opaque-image", Center = Argb(center) });
                Require(Near(center, sourceColor), "DWM source image reaches the Avalonia destination");
            }
            view.SetOpacity(.5);
            using (var image = Capture("half-image"))
            {
                var center = image.GetPixel(size.Width / 2, size.Height / 2);
                var expected = Color.FromArgb(8, 45, 90);
                stages.Add(new { Stage = "half-image", Center = Argb(center), Expected = Argb(expected) });
                Require(Near(center, expected), "50% whole-window opacity blends the DWM image over the black fixture");
            }
            view.SetOpacity(1);
            view.IsOverlayEnabled = true;
            view.TitleFontSettings = new FontSettings { Name = "Arial", Size = 22, ForeColor = Color.White, OutlineColor = Color.Black, OutlineWidth = 2 };
            view.SetHighlight(true, 5); view.Refresh(true);
            var glyphs = new List<Point>();
            using (var image = Capture("border-title"))
            {
                for (int y = 6; y < 70; y++)
                    for (int x = 6; x < image.Width - 6; x++)
                        if (image.GetPixel(x, y).ToArgb() == Color.White.ToArgb()) glyphs.Add(new(x, y));
                Require(glyphs.Count > 20, "fully opaque title glyph pixels are present before tint");
            }
            // Unlike a short alert, DamageTint covers the entire destination. This
            // catches AddVisual(NULL) ordering errors hidden by inner alert clipping.
            view.SetDamageFlash(null, 0xFFFF0000);
            using (var image = Capture("full-damage-tint"))
            {
                var edges = new[] { image.GetPixel(size.Width / 2, 0), image.GetPixel(size.Width / 2, size.Height - 1),
                    image.GetPixel(0, size.Height / 2), image.GetPixel(size.Width - 1, size.Height / 2) };
                int retained = glyphs.Count(point => image.GetPixel(point.X, point.Y).ToArgb() == Color.White.ToArgb());
                var center = image.GetPixel(size.Width / 2, size.Height / 2);
                stages.Add(new { Stage = "full-damage-tint", Center = Argb(center), Edges = edges.Select(Argb), TitlePixels = glyphs.Count, RetainedTitlePixels = retained });
                Require(Near(center, Color.Red, 0), "full DamageTint is actually composed above the source image");
                Require(edges.All(edge => edge.ToArgb() == Color.Lime.ToArgb()), "all four active borders remain above full DamageTint");
                Require(retained == glyphs.Count, "all opaque title glyphs remain above full DamageTint");
            }
            view.SetDamageFlash(null, 0x80FF0000);
            using (var image = Capture("half-damage-tint"))
            {
                var center = image.GetPixel(size.Width / 2, size.Height / 2);
                stages.Add(new { Stage = "half-damage-tint", Center = Argb(center) });
                Require(Near(center, Color.FromArgb(135, 45, 90)), "premultiplied tint alpha preserves the source beneath it");
            }
            view.SetDamageFlash(null, null); view.Hide(); Pump(TimeSpan.FromMilliseconds(50)); view.Show(); view.RestoreAndBringToFront();
            using (var image = Capture("restored")) Require(Near(image.GetPixel(size.Width / 2, size.Height / 2), sourceColor), "image survives hide/show and opacity restoration");
            Require(view.Handle == imageHandle && Overlay(view).Handle == overlayHandle, "image and overlay HWNDs survive hide/show");
            Require(windows.Registrations == registrations && windows.FailedUpdates == 0, "DWM relationship remains registered and healthy");
            Require(Native.GetForegroundWindow() == foreground, "host creation, opacity, tint and show preserve foreground");
            Require((Native.GetWindowLongPtr(imageHandle, -20).ToInt64() & 0x08000080) == 0x08000080 &&
                (Native.GetWindowLongPtr(overlayHandle, -20).ToInt64() & 0x080000A0) == 0x080000A0, "both HWNDs are nonactivating tool windows and overlay is pointer-transparent");
            view.ClearBorder();
            view.SetOverlayRenderer(OverlayRendererKind.Legacy); view.Refresh(true);
            using (var image = Capture("compatibility-image"))
            {
                Require(view.OverlayRenderer == OverlayRendererKind.Legacy, "explicit compatibility selection initializes the Avalonia retained renderer");
                Require(Near(image.GetPixel(size.Width / 2, size.Height / 2), sourceColor), "compatibility selection preserves the native DWM source image");
                Require(glyphs.All(point => image.GetPixel(point.X, point.Y).ToArgb() == Color.White.ToArgb()), "compatibility preserves the same opaque title glyph pixels");
            }
            view.SetDamageFlash(null, 0x80FF0000);
            using (var image = Capture("compatibility-tint"))
                Require(Near(image.GetPixel(size.Width / 2, size.Height / 2), Color.FromArgb(135, 45, 90)), "compatibility tint has the same compositor alpha as native tint");
            view.SetDamageFlash(0xFFFF0000, null, .5);
            using (var image = Capture("compatibility-title-blend"))
                Require(glyphs.All(point => Near(image.GetPixel(point.X, point.Y), Color.FromArgb(255, 128, 128))), "compatibility title color blends without reducing glyph alpha");
            view.SetDamageFlash(null, 0xFFFF0000); view.SetHighlight(true, 5); view.Refresh(true);
            using (var image = Capture("compatibility-full-tint-frame"))
            {
                var edges = new[] { image.GetPixel(size.Width / 2, 0), image.GetPixel(size.Width / 2, size.Height - 1),
                    image.GetPixel(0, size.Height / 2), image.GetPixel(size.Width - 1, size.Height / 2) };
                Require(edges.All(edge => edge.ToArgb() == Color.Lime.ToArgb()), "compatibility keeps all four inset-frame edges visible during full DamageTint");
                Require(glyphs.All(point => image.GetPixel(point.X, point.Y).ToArgb() == Color.White.ToArgb()), "compatibility title stays above full DamageTint with an active frame");
            }
            view.ClearBorder();
            using (var image = Capture("compatibility-cleared-frame"))
                Require(image.GetPixel(0, size.Height / 2).ToArgb() == Color.Red.ToArgb(), "clearing compatibility selection restores tint across the whole image");
            Require(view.Handle == imageHandle && windows.Registrations == registrations, "renderer replacement preserves the image HWND and DWM relationship");
            view.Dispose();
            Require(windows.Unregistrations == registrations, "closing the production view unregisters its DWM relationship");
            var sourceLifetime = ValidateSourceLifetime(output, logger);
            var result = new { Passed = true, Checks = checks, Stages = stages, ImageHandle = imageHandle.ToInt64(), OverlayHandle = overlayHandle.ToInt64(),
                DwmRegistrations = registrations, SourceLifetime = sourceLifetime };
            File.WriteAllText(Path.Combine(output, "host-proof.json"), JsonSerializer.Serialize(result, JsonOptions));
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }
        finally
        {
            if (Native.GetForegroundWindow() != foreground) Native.SetForegroundWindow(foreground);
        }
    }

    private sealed class SolidSource : Form
    {
        public SolidSource(Color color) { BackColor = color; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; }
        protected override bool ShowWithoutActivation => true;
    }
}
