using EveOPreview.Preview;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View.Rendering;
using EveOPreview.View;
using EveOPreview.Configuration.Implementation;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using EveOPreview.UI;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using Gma.System.MouseKeyHook;
using MediatR;
using Serilog;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class NativeOverlayRenderingTests(ITestOutputHelper output)
{
    [Fact]
    public Task NativeSceneChangesDoNotRequireAFrameLoopOrStealFocus() =>
        PrivateDesktopRunner.RunAsync("native-overlay-retained", output);

    [Fact]
    public Task NativeDevicesSurviveIndependentPreviewLifetimes() =>
        PrivateDesktopRunner.RunAsync("native-overlay-lifetime", output);

    [Fact]
    public Task NativeHostPreservesTitleSettingsWhenFallingBackAfterDeviceLoss() =>
        PrivateDesktopRunner.RunAsync("native-overlay-host", output);

    [Fact]
    public Task NativeInitializationFailureCreatesAUsableCompatibilityWindow() =>
        PrivateDesktopRunner.RunAsync("native-overlay-initialization", output);

    [Fact]
    public Task MaintenanceQuarantinesARemovedSharedDeviceAndRecoversIdleOverlays() =>
        PrivateDesktopRunner.RunAsync("native-overlay-health", output);

    [Fact]
    public Task FocusPrecedesImmediateRetainedHighlightWithoutDwmUpdatesDuringRapidSwitching() =>
        PrivateDesktopRunner.RunAsync("native-overlay-highlight", output);

    [Fact]
    public Task AlertBoundsClipTheAnimatedChildWithoutUploadingOrRestartingSurfaces() =>
        PrivateDesktopRunner.RunAsync("native-overlay-clipping", output);

    [Theory]
    [InlineData(CycleMarkerStyle.CircleSlash)]
    [InlineData(CycleMarkerStyle.Cross)]
    [InlineData(CycleMarkerStyle.Pause)]
    public void HidingTheTitlePreservesTheSelectedMarkerOnAnAlphaSurface(CycleMarkerStyle style)
    {
        var scene = new OverlayScene { Title = "Must stay hidden", ShowTitle = false, CycleSkipped = true,
            MarkerStyle = style, MarkerColor = 0xFF00FF00, Font = new OverlayFont(Size: 20, OffsetX: 13, OffsetY: 17) };
        using var bitmap = OverlaySceneRasterizer.Render(scene, new PreviewSize(160, 80));
        int markerPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A == 0) continue;
                Assert.InRange(x, 12, 37);
                Assert.InRange(y, 17, 44);
                if (pixel.G > 180 && pixel.R < 40) markerPixels++;
            }
        Assert.True(markerPixels > 8, "The configured skip marker must remain visible when the title is disabled.");
        using var empty = OverlaySceneRasterizer.Render(scene with { CycleSkipped = false }, new PreviewSize(160, 80));
        Assert.All(Enumerable.Range(0, empty.Width), x =>
            Assert.All(Enumerable.Range(0, empty.Height), y => Assert.Equal(0, empty.GetPixel(x, y).A)));
    }

    [Fact]
    public void NativeTitleRetainsFontOffsetsOutlineAndSmoothAlphaEdges()
    {
        var scene = new OverlayScene { Title = "Abc", Font = new OverlayFont(Family: "Consolas", Size: 24,
            Style: OverlayFontStyle.Bold | OverlayFontStyle.Italic, Foreground: 0xFFFFFFFF,
            Outline: 0xFFFF0000, OutlineWidth: 2, OffsetX: 12, OffsetY: 9) };
        using var first = OverlaySceneRasterizer.Render(scene, new PreviewSize(180, 80));
        using var moved = OverlaySceneRasterizer.Render(scene with { Font = scene.Font with { OffsetX = 25, OffsetY = 20 } }, new PreviewSize(180, 80));
        int foreground = 0, outline = 0, alpha = 0;
        for (int y = 0; y < first.Height - 11; y++)
            for (int x = 0; x < first.Width - 13; x++)
            {
                Color pixel = first.GetPixel(x, y);
                Assert.Equal(pixel.ToArgb(), moved.GetPixel(x + 13, y + 11).ToArgb());
                if (pixel.A > 240 && pixel.R > 240 && pixel.G > 240 && pixel.B > 240) foreground++;
                if (pixel.A > 200 && pixel.R > 200 && pixel.G < 40) outline++;
                if (pixel.A > 0 && pixel.A < 255) alpha++;
            }
        Assert.True(foreground > 20 && outline > 20 && alpha > 20, "Title must retain fill, outline, and antialiased alpha edges.");
    }

    [Fact]
    public void SettingsPreviewUsesNativeFontAndMarkerRenderingWhenSelected()
    {
        var settings = new Dictionary<string, string>
        {
            ["PreviewOverlayRenderer"] = "NativeComposition", ["ThumbnailWidth"] = "220", ["ThumbnailHeight"] = "90",
            ["TitleFontName"] = "Consolas", ["TitleFontSize"] = "21.5", ["TitleFontStyle"] = "Bold, Italic",
            ["TitleFontForeColor"] = "#FFFFFF", ["TitleFontOutlineColor"] = "#FF0000", ["TitleFontOutlineWidth"] = "2.5",
            ["TitleFontOffsetLeft"] = "17", ["TitleFontOffsetTop"] = "12", ["CycleSkipIndicatorStyle"] = "Cross",
            ["CycleSkipIndicatorColor"] = "#00FF00"
        };
        var renderer = new WindowsWorkspacePreviewRenderer();
        var rendered = renderer.RenderPreview(new WorkspacePreviewRequest(settings, "EVE - Native sample", Active: false, CycleSkipped: true));
        using var stream = new MemoryStream(rendered.Png);
        using var actual = new Bitmap(stream);
        using var expected = new Bitmap(220, 90);
        using (var graphics = Graphics.FromImage(expected))
        {
            graphics.Clear(Color.FromArgb(16, 24, 37));
            OverlaySceneRasterizer.Draw(graphics, new OverlayScene
            {
                Title = "Native sample", CycleSkipped = true, MarkerStyle = CycleMarkerStyle.Cross, MarkerColor = 0xFF00FF00,
                Font = new OverlayFont("Consolas", 21.5f, OverlayFontStyle.Bold | OverlayFontStyle.Italic,
                    0xFFFFFFFF, 0xFFFF0000, 2.5f, 17, 12)
            }, new PreviewSize(220, 90));
        }
        Assert.Equal(expected.Size, actual.Size);
        for (int y = 0; y < actual.Height; y++)
            for (int x = 0; x < actual.Width; x++)
                Assert.Equal(expected.GetPixel(x, y).ToArgb(), actual.GetPixel(x, y).ToArgb());
    }

    [Fact]
    public void CroppedAssetsPreserveVisibleInkAtNegativeOffsetsAndWrappedText()
    {
        var method = typeof(OverlaySceneRasterizer).GetMethod("RenderAsset", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var scene in new[]
        {
            new OverlayScene { Title = "A title that wraps", Font = new OverlayFont(Size: 24, OffsetX: -8, OffsetY: -4, OutlineWidth: 3), CycleSkipped = true },
            new OverlayScene { Title = "Small", Font = new OverlayFont(Size: 14.25f, OffsetX: 10, OffsetY: 20, OutlineWidth: 2.5f) },
            new OverlayScene { ShowTitle = false, CycleSkipped = true, MarkerStyle = CycleMarkerStyle.Pause, Font = new OverlayFont(OffsetX: -3, OffsetY: 11) },
            new OverlayScene { ShowTitle = false, Stats = [new OverlayStat("Test counter", "84"), new OverlayStat("Test label", "Sample")] },
            new OverlayScene { ShowTitle = false, CycleSkipped = true, Font = new OverlayFont(OffsetX: 131, OffsetY: 8) },
            new OverlayScene { ShowTitle = false, CycleSkipped = true, Font = new OverlayFont(OffsetX: 8, OffsetY: 100) },
            new OverlayScene { ShowTitle = false, CycleSkipped = true, Font = new OverlayFont(OffsetX: 400, OffsetY: 500) }
        })
        {
            var size = new PreviewSize(130, 100);
            using var complete = OverlaySceneRasterizer.Render(scene, size);
            var renderedAsset = method.Invoke(null, [scene, size, true, true]);
            if (renderedAsset == null)
            {
                for (int y = 0; y < size.Height; y++)
                    for (int x = 0; x < size.Width; x++)
                        Assert.Equal(0, complete.GetPixel(x, y).A);
                continue;
            }
            using var asset = Assert.IsAssignableFrom<IDisposable>(renderedAsset);
            var assetType = asset.GetType();
            var cropped = Assert.IsType<Bitmap>(assetType.GetProperty("Bitmap")!.GetValue(asset));
            int left = (int)assetType.GetProperty("X")!.GetValue(asset)!;
            int top = (int)assetType.GetProperty("Y")!.GetValue(asset)!;
            using var restored = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(restored)) graphics.DrawImageUnscaled(cropped, left, top);
            for (int y = 0; y < size.Height; y++)
                for (int x = 0; x < size.Width; x++)
                    Assert.Equal(complete.GetPixel(x, y).ToArgb(), restored.GetPixel(x, y).ToArgb());
        }
    }

    internal static void RunScenario(string scenario)
    {
        using var client = new Form { Text = "EVE - Native overlay simulated client", ClientSize = new Size(320, 180) };
        client.Show(); client.Activate(); Application.DoEvents();
        Native.SetActiveWindow(client.Handle);
        Assert.Equal(client.Handle, Native.GetActiveWindow());
        nint foreground = Native.GetForegroundWindow();
        if (scenario == "highlight")
        {
            CheckActiveHighlight(client, foreground);
            return;
        }
        if (scenario == "health")
        {
            CheckDeviceMaintenance(client, foreground);
            return;
        }
        if (scenario == "initialization")
        {
            using var overlay = new OccupiedCompositionOverlay(client) { ClientSize = new Size(320, 180) };
            overlay.SetOverlayLabel("Fallback title"); overlay.Show();
            Assert.Equal(OverlayRendererKind.Legacy, overlay.RendererKind);
            int style = Native.GetWindowLong(overlay.Handle, -20);
            Assert.Equal(0, style & 0x00200000);
            Assert.NotEqual(0, style & 0x00080000);
            Assert.True(Native.IsWindowVisible(overlay.Handle));
            Assert.Equal(client.Handle, Native.GetActiveWindow());
            Assert.Equal(foreground, Native.GetForegroundWindow());
            return;
        }
        if (scenario == "host")
        {
            CheckProductionHost(client, foreground);
            return;
        }
        using var host = new CompositionHost { Owner = client, ClientSize = new Size(320, 180) };
        host.Show();
        using var renderer = new NativeCompositionOverlayRenderer(host.Handle);
        var scene = new OverlayScene { Title = "Retained scene", CycleSkipped = true, Font = new OverlayFont(Size: 18),
            Stats = [new OverlayStat("Test counter", "500")] };
        renderer.Resize(new PreviewSize(320, 180)); renderer.SetScene(scene); renderer.WaitForPendingCommit();
        Assert.Equal(client.Handle, Native.GetActiveWindow());
        Assert.Equal(foreground, Native.GetForegroundWindow());

        if (scenario == "retained")
        {
            long uploads = renderer.SurfaceUploadCount, commits = renderer.CommitCount;
            for (int count = 0; count < 500; count++)
            {
                renderer.Resize(new PreviewSize(320, 180));
                renderer.SetScene(scene with { Stats = [new OverlayStat("Test counter", "500")] });
                renderer.SetOpacity(1); renderer.SetVisible(true);
            }
            Assert.Equal(uploads, renderer.SurfaceUploadCount);
            Assert.Equal(commits, renderer.CommitCount);
            renderer.ShowAlert(new PreviewAlert(DurationSeconds: 0.05, ShakePixels: 4));
            renderer.WaitForPendingCommit();
            long alertUploads = renderer.SurfaceUploadCount;
            renderer.ShowAlert(new PreviewAlert(DurationSeconds: 0.05, Intensity: 0.5, ShakePixels: 2));
            Assert.Equal(alertUploads, renderer.SurfaceUploadCount);
            renderer.SetVisible(false);
            commits = renderer.CommitCount;
            renderer.ShowAlert(new PreviewAlert(Color: 0xFF00FF00));
            Assert.Equal(commits, renderer.CommitCount);
            Assert.Equal(alertUploads, renderer.SurfaceUploadCount);
            renderer.ClearAlerts();
            Assert.Equal(commits, renderer.CommitCount);
            renderer.SetVisible(true); renderer.WaitForPendingCommit();
            renderer.Resize(new PreviewSize(9600, 5400));
            renderer.ShowAlert(new PreviewAlert());
            Assert.InRange(renderer.SceneSurfacePixels, 1, 100_000);
            Assert.Equal(2, renderer.AlertSurfacePixels);
            uploads = renderer.SurfaceUploadCount;
            renderer.SetScene(scene with { Stats = [new OverlayStat("Test counter", "501")] });
            Assert.Equal(uploads + 1, renderer.SurfaceUploadCount);
            Console.WriteLine($"9600x5400 viewport retains {renderer.SceneSurfacePixels} title/stat pixels and {renderer.AlertSurfacePixels} alert pixels.");
        }
        else if (scenario == "clipping")
        {
            renderer.ShowAlert(new PreviewAlert(DurationSeconds: 0.1, Intensity: 1, ShakePixels: 24));
            long uploads = renderer.SurfaceUploadCount;
            foreach (var (requested, expected) in new (PreviewRect? Requested, PreviewRect Expected)[]
            {
                (new PreviewRect(2, 1, 316, 178), new PreviewRect(2, 1, 316, 178)),
                (new PreviewRect(11, 6, 298, 168), new PreviewRect(11, 6, 298, 168)),
                (new PreviewRect(-10, -20, 50, 70), new PreviewRect(0, 0, 40, 50)),
                (new PreviewRect(320, 180, 1, 1), new PreviewRect(320, 180, 0, 0)),
                (new PreviewRect(8, 9, -5, -6), new PreviewRect(8, 9, 0, 0)),
                (new PreviewRect(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue), new PreviewRect(320, 180, 0, 0)),
                (null, new PreviewRect(0, 0, 320, 180))
            })
            {
                renderer.SetScene(scene with { AlertBounds = requested });
                renderer.WaitForPendingCommit();
                Assert.Equal(expected, renderer.AlertClipBounds);
                Assert.Equal(uploads, renderer.SurfaceUploadCount);
            }
            long commits = renderer.CommitCount;
            Thread.Sleep(150); Application.DoEvents();
            Assert.Equal(commits, renderer.CommitCount);
            Assert.Equal(new PreviewRect(0, 0, 320, 180), renderer.AlertClipBounds);
            renderer.ClearAlerts();
            Assert.Equal(new PreviewRect(0, 0, 320, 180), renderer.AlertClipBounds);
        }
        else
        {
            // A survivor exercises a shared hardware device while other preview
            // targets, content surfaces, and in-flight animations are disposed.
            for (int cycle = 0; cycle < 24; cycle++)
            {
                using var secondHost = new CompositionHost { Owner = client, ClientSize = new Size(240, 140) };
                using var second = new NativeCompositionOverlayRenderer(secondHost.Handle);
                second.Resize(new PreviewSize(240, 140)); second.SetScene(scene);
                long survivorCommits = renderer.CommitCount, survivorUploads = renderer.SurfaceUploadCount;
                second.ShowAlert(new PreviewAlert(DurationSeconds: 10, ShakePixels: 8));
                Assert.Equal(survivorCommits, renderer.CommitCount);
                Assert.Equal(survivorUploads, renderer.SurfaceUploadCount);
                second.SetScene(scene with { Title = "Changing title " + cycle });
                second.Resize(new PreviewSize(220, 130));
                renderer.SetScene(scene with { Title = "Surviving title " + cycle });
                renderer.WaitForPendingCommit();
            }
            renderer.ShowAlert(new PreviewAlert(ReducedMotion: true)); renderer.ClearAlerts();
            renderer.SetScene(scene); renderer.WaitForPendingCommit();
        }
        Assert.Equal(client.Handle, Native.GetActiveWindow());
        Assert.Equal(foreground, Native.GetForegroundWindow());
    }

    private static void CheckProductionHost(Form client, nint foreground)
    {
        using var overlay = new ThumbnailOverlay(client, (_, _) => { }, OverlayRendererKind.NativeComposition)
        { ClientSize = new Size(320, 180), Location = client.Location };
        overlay.SetOverlayLabel("Configured native title");
        overlay.SetOverlayFont(new FontSettings { Name = "Consolas", Size = 19, Style = FontStyle.Bold,
            ForeColor = Color.White, OutlineColor = Color.Red, OutlineWidth = 2,
            PositionOffsetFromLeft = 17, PositionOffsetFromTop = 13 });
        overlay.SetCycleSkipIndicator(true, "Pause", Color.Lime);
        overlay.Show(); overlay.Opacity = 0.7;
        Assert.Equal(OverlayRendererKind.NativeComposition, overlay.RendererKind);
        Assert.True(overlay.GraphicsCapabilities.HasFlag(OverlayCapabilities.CompositorAnimations));
        int styles = Native.GetWindowLong(overlay.Handle, -20);
        Assert.NotEqual(0, styles & 0x00200000);
        Assert.NotEqual(0, styles & 0x08000000);
        Assert.Equal(0, styles & 0x00080000);
        var point = overlay.PointToScreen(new Point(40, 40));
        Assert.Equal(new nint(-1), SendMessage(overlay.Handle, 0x0084, 0, (nint)((point.Y << 16) | (point.X & 0xffff))));
        Assert.Equal(client.Handle, Native.GetActiveWindow());
        Assert.Equal(foreground, Native.GetForegroundWindow());
        overlay.Hide(); overlay.Show();
        typeof(ThumbnailOverlay).GetMethod("UseCompatibilityRenderer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(overlay, [new COMException("Simulated graphics-device removal", unchecked((int)0x887A0005))]);
        Assert.Equal(OverlayRendererKind.Legacy, overlay.RendererKind);
        Assert.Equal(OverlayCapabilities.Title | OverlayCapabilities.CycleMarker, overlay.GraphicsCapabilities);
        styles = Native.GetWindowLong(overlay.Handle, -20);
        Assert.Equal(0, styles & 0x00200000);
        Assert.NotEqual(0, styles & 0x00080000);
        var label = Assert.IsAssignableFrom<Control>(typeof(ThumbnailOverlay)
            .GetField("OverlayLabel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay));
        Assert.True(label.Visible);
        Assert.Equal("Configured native title", label.Text);
        Assert.Equal(19, label.Font.Size);
        Assert.Equal(new Point(17, 13), label.Location);
        Assert.Equal(0.7, overlay.Opacity);
        Assert.Equal(client.Handle, Native.GetActiveWindow());
        Assert.Equal(foreground, Native.GetForegroundWindow());
    }

    private static void CheckDeviceMaintenance(Form client, nint foreground)
    {
        using var first = new ThumbnailOverlay(client, (_, _) => { }, OverlayRendererKind.NativeComposition);
        using var second = new ThumbnailOverlay(client, (_, _) => { }, OverlayRendererKind.NativeComposition);
        first.SetOverlayLabel("First independent title"); second.SetOverlayLabel("Second independent title");
        first.Show(); second.Show();
        var firstRenderer = Renderer(first);
        var secondRenderer = Renderer(second);
        var shared = Device(firstRenderer);
        Assert.Same(shared, Device(secondRenderer));
        int probeCalls = 0, simulatedReason = 0;
        shared.GetType().GetField("_healthProbe", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(shared, (Func<int>)(() => { probeCalls++; return simulatedReason; }));
        ResetInterval();
        Maintain(first); Maintain(second);
        Assert.Equal(1, probeCalls);
        Assert.Equal(OverlayRendererKind.NativeComposition, first.RendererKind);
        Assert.Equal(OverlayRendererKind.NativeComposition, second.RendererKind);

        // Inject a return value, never a driver reset or native device operation.
        simulatedReason = unchecked((int)0x887A0005); // DXGI_ERROR_DEVICE_REMOVED
        ResetInterval();
        Maintain(first);
        Assert.Equal(2, probeCalls);
        Assert.Equal(OverlayRendererKind.Legacy, first.RendererKind);
        Assert.Equal(OverlayRendererKind.NativeComposition, second.RendererKind);

        using var replacement = new ThumbnailOverlay(client, (_, _) => { }, OverlayRendererKind.NativeComposition);
        replacement.Show();
        var freshDevice = Device(Renderer(replacement));
        Assert.NotSame(shared, freshDevice);
        Renderer(replacement).CheckDeviceHealth(); // Exercise the actual healthy native status ABI.
        Maintain(second);
        Assert.Equal(2, probeCalls); // Cached removal reaches every old owner immediately.
        Assert.Equal(OverlayRendererKind.Legacy, second.RendererKind);

        using var subsequent = new ThumbnailOverlay(client, (_, _) => { }, OverlayRendererKind.NativeComposition);
        subsequent.Show();
        Assert.Same(freshDevice, Device(Renderer(subsequent))); // Old disposal cannot clear the replacement cache.
        Assert.Equal(client.Handle, Native.GetActiveWindow());
        Assert.Equal(foreground, Native.GetForegroundWindow());

        void ResetInterval() => shared.GetType().GetField("_lastHealthCheck", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(shared, -1L);
        static void Maintain(ThumbnailOverlay overlay) => typeof(ThumbnailOverlay)
            .GetMethod("MaintainGraphics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, null);
        static NativeCompositionOverlayRenderer Renderer(ThumbnailOverlay overlay) =>
            Assert.IsType<NativeCompositionOverlayRenderer>(typeof(ThumbnailOverlay)
                .GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay));
        static object Device(NativeCompositionOverlayRenderer renderer) => typeof(NativeCompositionOverlayRenderer)
            .GetField("_device", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
    }

    private static void CheckActiveHighlight(Form client, nint foreground)
    {
        var assembly = typeof(ThumbnailView).Assembly;
        var config = (IThumbnailConfiguration)Activator.CreateInstance(
            assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration")!);
        config.EnableActiveClientHighlight = true;
        config.ActiveClientHighlightThickness = 5;
        config.ActiveClientHighlightColor = Color.DeepSkyBlue;
        config.PerClientActiveClientHighlightColor["EVE - Second highlight"] = Color.OrangeRed;
        config.ShowThumbnailsAlwaysOnTop = true;
        config.EnableThumbnailSnap = false;
        config.MinimizeInactiveClients = false;
        using var logger = new LoggerConfiguration().CreateLogger();
        var keyboard = Stub.Create<IKeyboardMouseEvents>();
        var mediator = Stub.Create<IMediator>();
        int registrations = 0, unregistrations = 0, activationRequests = 0, imageUpdates = 0;
        var imageBounds = new Dictionary<nint, Rectangle>();
        Action<nint> inspectBeforeSourceFocus = null;
        nint observedForeground = 0;
        var windows = Stub.Create<IWindowManager>((method, args) =>
        {
            if (method.Name == "GetForegroundWindowHandle") return observedForeground;
            if (method.Name == "GetLiveThumbnail")
            {
                nint source = (nint)args[1];
                registrations++;
                return Stub.Create<IDwmThumbnail>((operation, values) =>
                {
                    if (operation.Name == "Move")
                        imageBounds[source] = Rectangle.FromLTRB((int)values[0], (int)values[1], (int)values[2], (int)values[3]);
                    if (operation.Name == "Unregister") unregistrations++;
                    if (operation.Name == "Update") imageUpdates++;
                    return operation.Name == "Update" ? true : Stub.Default(operation.ReturnType);
                });
            }
            if (method.Name == "ActivateWindow")
            {
                activationRequests++;
                inspectBeforeSourceFocus?.Invoke((nint)args[0]);
            }
            return Stub.Default(method.ReturnType);
        });
        var manager = (IThumbnailManager)Activator.CreateInstance(assembly.GetType("EveOPreview.Services.ThumbnailManager")!,
            mediator, config, Stub.Create<IProcessMonitor>(), windows, Stub.Create<IThumbnailViewFactory>(), keyboard,
            Stub.Create<IHookService>(), Stub.Create<IGlobalEvents>(), logger);
        using var first = CreateView(101, "EVE - First highlight");
        using var second = CreateView(102, "EVE - Second highlight");
        var known = (Dictionary<nint, IThumbnailView>)manager.GetType()
            .GetField("_thumbnailViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        known.Add(first.Id, first); known.Add(second.Id, second);
        try
        {
            first.Show(); second.Show();
            first.SetTopMost(true); second.SetTopMost(true);
            Native.SetActiveWindow(client.Handle);
            Assert.Equal(2, registrations);
            Select(first);
            var firstOverlay = Overlay(first);
            var firstGraphics = Graphics(firstOverlay);
            var secondGraphics = Graphics(Overlay(second));
            first.ShowAlert(new PreviewAlert(DurationSeconds: 0.15, Intensity: 1, ShakePixels: 24));
            firstGraphics.WaitForPendingCommit();
            long effectCommits = firstGraphics.CommitCount;
            long effectUploads = firstGraphics.SurfaceUploadCount;
            Assert.Equal(2, firstGraphics.AlertSurfacePixels);
            Assert.Equal(0, secondGraphics.AlertSurfacePixels);

            Select(second); // Border must change while the first preview's alert is still active.
            Assert.Equal(effectCommits + 1, firstGraphics.CommitCount); // Only the fixed clip changes; the animation continues.
            Assert.Equal(effectUploads, firstGraphics.SurfaceUploadCount);
            Thread.Sleep(200); Application.DoEvents(); // Let the finite native effect expire without an application frame loop.
            CheckSelection(second);
            Assert.Equal(effectCommits + 1, firstGraphics.CommitCount);
            long stableUploads = firstGraphics.SurfaceUploadCount + secondGraphics.SurfaceUploadCount;
            for (int i = 0; i < 200; i++) Select(i % 2 == 0 ? first : second);
            Assert.Equal(stableUploads, firstGraphics.SurfaceUploadCount + secondGraphics.SurfaceUploadCount);
            Assert.Equal("First highlight", ((OverlayScene)typeof(ThumbnailOverlay)
                .GetProperty("Scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(firstOverlay)!).Title);

            config.ActiveClientHighlightThickness = 2;
            config.ActiveClientHighlightColor = Color.Gold;
            Select(first);
            config.ActiveClientHighlightThickness = 1;
            Select(second);
            second.ShowAlert(new PreviewAlert(DurationSeconds: 0.05, Intensity: 1, ShakePixels: 24));
            var fixedClip = secondGraphics.AlertClipBounds;
            secondGraphics.WaitForPendingCommit();
            Thread.Sleep(75); Application.DoEvents();
            Assert.Equal(fixedClip, secondGraphics.AlertClipBounds);
            CheckSelection(second);
            Assert.Equal(204, activationRequests);
            // A click on the already-selected preview must not clear its own border
            // after the manager has finished activation.
            second.ThumbnailActivated = _ => Select(second);
            typeof(ThumbnailView).GetMethod("MouseDownEventHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(second, [new MouseEventArgs(MouseButtons.Left, 1, 40, 40, 0), Keys.None]);
            CheckSelection(second);
            Assert.Equal(2, registrations);
            Assert.Equal(0, unregistrations);
            int requestsBeforeNotification = activationRequests, updatesBeforeNotification = imageUpdates;
            var foregroundChanged = manager.GetType().GetMethod("ForegroundWindowChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            observedForeground = first.Id;
            foregroundChanged.Invoke(manager, [first.Id]);
            Application.DoEvents();
            CheckSelection(first); // External focus is reflected without discovery/refresh or reactivation.
            foregroundChanged.Invoke(manager, [second.Id]); // Queued stale notification cannot roll selection back.
            foregroundChanged.Invoke(manager, [(nint)999]); // Unrelated application retains last selected client.
            Application.DoEvents();
            CheckSelection(first);
            Assert.Equal(requestsBeforeNotification, activationRequests);
            Assert.Equal(updatesBeforeNotification, imageUpdates);
            manager.Stop();
            observedForeground = second.Id;
            foregroundChanged.Invoke(manager, [second.Id]);
            CheckSelection(first);
            Assert.Equal(client.Handle, Native.GetActiveWindow());
            Assert.Equal(foreground, Native.GetForegroundWindow());
        }
        finally { manager.Stop(); ((IDisposable)manager).Dispose(); }

        ThumbnailView CreateView(long id, string title)
        {
            var view = (ThumbnailView)Activator.CreateInstance(assembly.GetType("EveOPreview.View.LiveThumbnailView")!,
                windows, config, manager, mediator, keyboard, logger);
            view.Id = (nint)id; view.Title = title; view.ThumbnailSize = new Size(320, 180);
            view.IsOverlayEnabled = true;
            view.SetOverlayRenderer(OverlayRendererKind.NativeComposition);
            return view;
        }
        void Select(ThumbnailView view)
        {
            var a = Graphics(Overlay(first)); var b = Graphics(Overlay(second));
            long commits = a.CommitCount + b.CommitCount;
            int updates = imageUpdates, requests = activationRequests;
            inspectBeforeSourceFocus = selected =>
            {
                Assert.Equal(view.Id, selected);
                Assert.Equal(view.Id, manager.GetActiveClient().Id);
                Assert.Equal(commits, a.CommitCount + b.CommitCount); // No graphics work before focus.
                Assert.Equal(updates, imageUpdates);
            };
            manager.GetType().GetMethod("SetActive")!
                .Invoke(manager, [new KeyValuePair<nint, IThumbnailView>(view.Id, view)]);
            Assert.Equal(requests + 1, activationRequests);
            Assert.Equal(updates, imageUpdates); // No resize/capture on the complete switching path.
            CheckSelection(view); // No message pump, timer tick, or WM_PAINT needed to submit the frame.
        }
        void CheckSelection(ThumbnailView selected)
        {
            Assert.Equal(selected.Id, manager.GetActiveClient().Id);
            foreach (var view in new[] { first, second })
            {
                var bounds = imageBounds[view.Id];
                Assert.Equal(new Rectangle(Point.Empty, view.ClientSize), bounds);
                var graphics = Graphics(Overlay(view));
                if (view == selected)
                {
                    Assert.NotNull(graphics.ActiveBorder);
                    var inner = graphics.ActiveBorder.InnerBounds;
                    Assert.Equal(config.ActiveClientHighlightThickness, inner.Y);
                    Assert.Equal(view.ClientSize.Height - 2 * config.ActiveClientHighlightThickness, inner.Height);
                    Assert.Equal(config.ActiveClientHighlightThickness, inner.X);
                    Assert.Equal(view.ClientSize.Width - 2 * config.ActiveClientHighlightThickness, inner.Width);
                    Assert.Equal(unchecked((uint)(view == second ? Color.OrangeRed : config.ActiveClientHighlightColor).ToArgb()), graphics.ActiveBorder.Color);
                }
                else Assert.Null(graphics.ActiveBorder);
                var overlay = Overlay(view);
                var expectedClip = graphics.ActiveBorder?.InnerBounds ?? new PreviewRect(0, 0, view.ClientSize.Width, view.ClientSize.Height);
                Assert.Equal(expectedClip, graphics.AlertClipBounds);
                Assert.Equal(OverlayRendererKind.NativeComposition, overlay.RendererKind);
                Assert.True(overlay.GraphicsCapabilities.HasFlag(OverlayCapabilities.CompositorAnimations));
                Assert.True(IsAbove(overlay.Handle, view.Handle), "Title and alert overlay must remain above its DWM destination.");
            }
            Assert.Equal(2, registrations);
            Assert.Equal(0, unregistrations);
            Assert.Equal(client.Handle, Native.GetActiveWindow());
            Assert.Equal(foreground, Native.GetForegroundWindow());
        }
        static ThumbnailOverlay Overlay(ThumbnailView view) => (ThumbnailOverlay)typeof(ThumbnailView)
            .GetField("_overlay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        static NativeCompositionOverlayRenderer Graphics(ThumbnailOverlay overlay) =>
            Assert.IsType<NativeCompositionOverlayRenderer>(typeof(ThumbnailOverlay)
                .GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay));
        static bool IsAbove(nint upper, nint lower)
        {
            for (nint hwnd = Native.GetWindow(lower, 3); hwnd != 0; hwnd = Native.GetWindow(hwnd, 3))
                if (hwnd == upper) return true;
            return false;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hwnd, uint message, nint wparam, nint lparam);

    private sealed class CompositionHost : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08200080; return value; }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
    }

    private sealed class OccupiedCompositionOverlay(Form owner)
        : ThumbnailOverlay(owner, (_, _) => { }, OverlayRendererKind.NativeComposition)
    {
        private NativeCompositionOverlayRenderer _occupied;
        protected override void OnHandleCreated(EventArgs e)
        {
            if (RendererKind == OverlayRendererKind.NativeComposition)
                _occupied = new NativeCompositionOverlayRenderer(Handle);
            base.OnHandleCreated(e);
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            _occupied?.Dispose(); _occupied = null;
            base.OnHandleDestroyed(e);
        }
    }
}
