using System.Security.Cryptography;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using EveOPreview.Preview;
using EveOPreview.UI;
using EveOPreview.UI.Previews;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string output = args.Length == 2 && args[0] == "--output" ? Path.GetFullPath(args[1])
                : args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "screenshots")
                : throw new ArgumentException("Usage: Eve-O-Preview.Preview.Smoke [--output <directory>]");
            Directory.CreateDirectory(output);
            AppBuilder.Configure<WorkspaceApp>().WithInterFont().UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            var window = new AvaloniaPreviewOverlayWindow();
            var renderer = window.Renderer;
            window.ResizePixels(new PreviewSize(384, 216));
            var scene = new OverlayScene
            {
                Title = "EVE - Overlay validation", CycleSkipped = true,
                Font = new OverlayFont("Consolas", 18, OverlayFontStyle.Bold, 0xFFFFFFFF, 0xFF000000, 2),
                Stats = [new("Test counter", "1,250", 0xFFFDCA61), new("Test label", "Synthetic", 0xFF71DAFC)]
            };
            renderer.SetScene(scene);
            window.Show();
            Flush();
            string title = Capture(window, Path.Combine(output, "title-stats.png"));
            long beforeTint = renderer.SceneUpdateCount;
            renderer.SetScene(scene with { DamageTint = 0x50FF2233 }); Flush();
            Require(Capture(window, Path.Combine(output, "incoming-thumbnail-flash.png")) != title, "The entire preview must receive the translucent damage wash.");
            Require(renderer.SceneUpdateCount == beforeTint, "Tint-only changes must retain title/stat geometry.");
            renderer.SetScene(scene with { DamageTint = 0x50FF2233, TitleColor = 0xFFFF2233 }); Flush();
            Capture(window, Path.Combine(output, "incoming-both-flash.png"));
            renderer.SetScene(scene); Flush();
            Require(Capture(window, null) == title, "Clearing damage tint must restore the original image exactly.");
            long updates = renderer.SceneUpdateCount;
            long renders = renderer.SceneRenderCount;
            renderer.SetScene(scene with { Stats = scene.Stats.ToArray() });
            for (int i = 0; i < 5; i++) Flush();
            Require(renderer.SceneUpdateCount == updates && renderer.SceneRenderCount == renders,
                "An unchanged scene must not rebuild geometry or schedule UI drawing.");

            renderer.SetScene(scene with { TitleColor = 0xFFFF7666 }); Flush();
            Require(Capture(window, Path.Combine(output, "incoming-title-flash.png")) != title, "A transient title colour must change the actual retained title.");
            renderer.SetScene(scene); Flush();
            Require(Capture(window, null) == title, "Clearing incoming damage must restore the original configured title colour.");
            var fading = scene with { TitleColor = 0xFFFF7666, DamageTint = 0x50FF7666, DamageFlashIntensity = 0 };
            renderer.SetScene(fading); Flush(); long fadeGeometry = renderer.SceneUpdateCount;
            var fadeImages = new HashSet<string>();
            foreach (double amount in new[] { 0d, .25, .5, .75, 1 })
            {
                renderer.SetScene(fading with { DamageFlashIntensity = amount }); Flush();
                fadeImages.Add(Capture(window, Path.Combine(output, "damage-fade-" + amount.ToString(CultureInfo.InvariantCulture) + ".png")));
            }
            Require(fadeImages.Count == 5, "Both title and thumbnail fade must render intermediate strengths.");
            Require(renderer.SceneUpdateCount == fadeGeometry, "Fade frames must retain text geometry.");
            foreach (var position in Enum.GetValues<OverlayPosition>())
            {
                renderer.SetScene(scene with { TitlePosition = position, Subtitle = "Jita", StatsStyle = scene.StatsStyle with { Position = position },
                    Stats = [new("In DPS", "125"), new("Out DPS", "430")] }); Flush();
                Capture(window, Path.Combine(output, "stacked-" + position + ".png"));
            }
            var alpha = CombatOverlayFormatter.Event(new(new(DateTimeOffset.UtcNow, "Pilot", "combat", "", DamageDirection.Incoming, 3200)
                { DamageTypes = DamageTypes.All }), new());
            var incomingTypes = new OverlayStat("In DPS", "320", 0xFFFF9990) { Suffix = alpha };
            var outgoing = new OverlayStat("Out DPS", "160", 0xFF8CE8AA) { Suffix = alpha with { Value = "α 1,600", Color = 0xFF8CE8AA } };
            renderer.SetScene(scene with { Stats = [incomingTypes, outgoing] }); Flush();
            string fourTypes = Capture(window, Path.Combine(output, "incoming-four-damage-types.png"));
            renderer.SetScene(scene with { Stats = [incomingTypes with { Suffix = alpha with { ThirdIcon = OverlaySymbol.None, FourthIcon = OverlaySymbol.None } }, outgoing] }); Flush();
            Require(Capture(window, null) != fourTypes, "All four damage icons must be rendered, including the third and fourth symbols.");
            foreach (bool top in new[] { false, true })
            {
                var meter = scene with { ShowTitle = false, CycleSkipped = false, Stats = [incomingTypes, outgoing], StatsStyle = new(16, 8, 8, top) };
                renderer.SetScene(meter); Flush(); var bothRows = ReadPixels(window);
                renderer.SetScene(meter with { Stats = [incomingTypes with { Visible = false }, outgoing] }); Flush(); var oneRow = ReadPixels(window);
                int firstY = meter.StatsStyle.StartY(216, 2);
                // Incoming glyph outlines can extend above their baseline. Every pixel
                // from the outgoing row downward must still match exactly.
                AssertOutsideEqual(bothRows, oneRow, new(0, 0, 384, firstY + meter.StatsStyle.LineHeight));
                Capture(window, Path.Combine(output, $"outgoing-fixed-row-{top}.png"));
                Require(!bothRows.Bytes.SequenceEqual(oneRow.Bytes), "Hiding incoming must remove its visible text and icons.");
            }

            var fontScene = scene with { ShowTitle = false, CycleSkipped = false, Stats = [incomingTypes, outgoing] };
            renderer.SetScene(fontScene); Flush(); string inheritedFont = Capture(window, null);
            renderer.SetScene(fontScene with { StatsStyle = fontScene.StatsStyle with { FontFamily = scene.Font.Family, FontStyle = scene.Font.Style } }); Flush();
            Require(Capture(window, null) == inheritedFont, "DPS must inherit the title family and style exactly.");
            renderer.SetScene(fontScene with { StatsStyle = fontScene.StatsStyle with { FontFamily = "Arial", FontStyle = OverlayFontStyle.Italic | OverlayFontStyle.Underline } }); Flush();
            Require(Capture(window, Path.Combine(output, "dps-font-override.png")) != inheritedFont, "The DPS font override must affect actual geometry.");
            renderer.SetScene(fontScene); Flush();
            Require(Capture(window, null) == inheritedFont, "Clearing the override must restore title-font inheritance.");

            renderer.SetScene(scene with { Subtitle = "Jita" }); Flush();
            string subtitle = Capture(window, Path.Combine(output, "system-subtitle.png"));
            Require(subtitle != title, "A plain system subtitle must change the retained image.");
            renderer.SetScene(scene with { Subtitle = "Amarr" }); Flush();
            Require(Capture(window, null) != subtitle, "A changed system name must invalidate the scene.");

            var markerHashes = new HashSet<string>();
            foreach (var marker in Enum.GetValues<CycleMarkerStyle>())
            {
                renderer.SetScene(scene with { ShowTitle = false, Stats = [], MarkerStyle = marker });
                Flush();
                markerHashes.Add(Capture(window, Path.Combine(output, "marker-" + marker + ".png")));
            }
            Require(markerHashes.Count == 3, "All three skip markers must be distinct with title hidden.");
            var fontHashes = new HashSet<string>();
            foreach (var style in new[] { OverlayFontStyle.Regular, OverlayFontStyle.Bold, OverlayFontStyle.Italic,
                OverlayFontStyle.Underline, OverlayFontStyle.Strikeout })
            {
                renderer.SetScene(scene with { Font = scene.Font with { Style = style } });
                Flush();
                fontHashes.Add(Capture(window, Path.Combine(output, "font-" + style + ".png")));
            }
            Require(fontHashes.Count == 5, "Existing title styles must produce distinct geometry.");

            renderer.SetScene(scene);
            Flush();
            string beforeAlert = Capture(window, null);
            renders = renderer.SceneRenderCount;
            renderer.ShowAlert(new PreviewAlert(DurationSeconds: 0.5, Intensity: 0.5, ShakePixels: 4));
            Thread.Sleep(80);
            Flush();
            string alert = Capture(window, Path.Combine(output, "alert-active.png"));
            Require(alert != beforeAlert, "A compositor alert must draw over the retained scene.");
            Require(renderer.SceneRenderCount == renders, "Compositor animation must not repaint the static scene on the UI thread.");
            Thread.Sleep(550);
            Flush();
            Require(Capture(window, Path.Combine(output, "alert-expired.png")) == beforeAlert,
                "A finite alert must return to the exact resting image.");
            for (int i = 0; i < 5; i++) Flush();
            Require(renderer.SceneRenderCount == renders, "Completed effects must leave static drawing idle.");

            renderer.ShowAlert(new PreviewAlert(DurationSeconds: 3, Intensity: 0.7, ReducedMotion: true));
            Thread.Sleep(50); Flush();
            Require(Capture(window, null) != beforeAlert, "Reduced motion must still provide a visible alert.");
            renderer.ClearAlerts(); Flush();
            Require(Capture(window, null) == beforeAlert, "Clear must immediately remove retained alert graphics.");
            renderer.ShowAlert(new PreviewAlert(DurationSeconds: 3));
            renderer.SetVisible(false); Flush();
            renderer.SetVisible(true); Flush();
            Require(Capture(window, null) == beforeAlert, "Hide/show must not replay a stale alert.");
            CheckProtectedFrame(window, scene, output);
            CheckPlatformIcons(window, output);
            window.Close();
            Require(renderer.SceneRenderCount > 0, "The production portable renderer must actually draw.");
            Console.WriteLine("PASS: portable outlined titles/styles, marker-only state, stats, unchanged-state idle, compositor pulse/shake, expiration, clear/hide recovery, fixed alert clipping and bounds-only updates.");
            Console.WriteLine("Headless Skia rendering; native window alpha, input, GPU cost and Linux capture are separate validation.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckPlatformIcons(AvaloniaPreviewOverlayWindow window, string output)
    {
        var renderer = window.Renderer;
        var platforms = CombatAppearance.Preset("Classic").Weapons.Where(x => x.Key != WeaponPlatform.Unknown).ToArray();
        string directory = Path.Combine(output, "platform-icons"); Directory.CreateDirectory(directory);
        window.ResizePixels(new(100, 48));
        foreach (int size in new[] { 12, 16, 24 })
        {
            var hashes = new HashSet<string>();
            foreach (var platform in platforms)
            {
                var entry = new ParsedLogEntry(DateTimeOffset.UnixEpoch, "Preview", "combat", "",
                    DamageDirection.Outgoing, 250, Platform: platform.Key);
                var stat = CombatOverlayFormatter.Event(new(entry), new LogOverlayOptions { Advanced = true,
                    CustomAppearance = CombatAppearance.Preset("Classic") with { ShowWeaponIcon = true } });
                Require(stat.Icons().Count() == 1 && stat.Icon == platform.Value.Icon,
                    "A known platform without ammunition metadata must show its weapon, not an unknown marker.");
                renderer.SetScene(new OverlayScene { ShowTitle = false, StatsStyle = new(size, 8, 8, true),
                    Stats = [stat] }); Flush();
                Require(hashes.Add(Capture(window, Path.Combine(directory, platform.Key + "-" + size + ".png"))),
                    "Each platform must have a distinct rendered icon at " + size + " px: " + platform.Key);
            }
        }
        Console.WriteLine($"PASS: {platforms.Length} fixed platform SVGs at 12, 16 and 24 px through the portable renderer.");
        foreach (int size in new[] { 12, 16, 24 })
        {
            var hashes = new HashSet<string>();
            foreach (var symbol in new[] { OverlaySymbol.Shield, OverlaySymbol.Armor, OverlaySymbol.Hull })
            {
                Require(OverlaySymbols.Fills(symbol).Count > 0 && OverlaySymbols.Strokes(symbol).Count == 0, "Repairs must use their shared SVG asset.");
                renderer.SetScene(new OverlayScene { ShowTitle = false, StatsStyle = new(size, 8, 8, true),
                    Stats = [new("", "250", Icon: symbol)] }); Flush();
                Require(hashes.Add(Capture(window, Path.Combine(directory, symbol + "-" + size + ".png"))),
                    "Repair silhouettes must remain distinct at " + size + " px: " + symbol);
            }
        }
        Console.WriteLine("PASS: three repair SVG silhouettes at 12, 16 and 24 px.");
    }

    private static void CheckProtectedFrame(AvaloniaPreviewOverlayWindow window, OverlayScene scene, string output)
    {
        var renderer = window.Renderer;
        window.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.LimeGreen);
        renderer.SetScene(scene with { AlertBounds = new PreviewRect(1, 1, 382, 214) });
        Flush();
        var baseline = ReadPixels(window);
        Capture(window, Path.Combine(output, "alert-protected-baseline.png"));
        long renders = renderer.SceneRenderCount, updates = renderer.SceneUpdateCount;
        var elapsed = Stopwatch.StartNew();
        renderer.ShowAlert(new PreviewAlert(DurationSeconds: 1, Intensity: 1, ShakePixels: 24));
        Thread.Sleep(75); Flush();
        var active = ReadPixels(window);
        Require(!baseline.Bytes.SequenceEqual(active.Bytes), "The maximum-intensity alert must actually be visible.");
        AssertOutsideEqual(baseline, active, new PreviewRect(1, 1, 382, 214));
        Capture(window, Path.Combine(output, "alert-protected-frame.png"));

        // Changing only the protected rectangle must preserve the ongoing finite animation.
        Thread.Sleep(250);
        renderer.SetScene(scene with { AlertBounds = new PreviewRect(12, 6, 360, 204) });
        Flush();
        active = ReadPixels(window);
        Require(!baseline.Bytes.SequenceEqual(active.Bytes), "A bounds-only update must not cancel the active alert.");
        AssertOutsideEqual(baseline, active, new PreviewRect(12, 6, 360, 204));
        Require(renderer.SceneRenderCount == renders && renderer.SceneUpdateCount == updates,
            "Changing alert bounds must not repaint or rebuild the title/stat scene.");
        Capture(window, Path.Combine(output, "alert-protected-frame-updated.png"));

        // Use a uniform frame to compare cleared output exactly: text antialiasing can
        // change rounding when the compositor changes intermediate layers beneath it.
        renderer.ClearAlerts();
        scene = scene with { ShowTitle = false, CycleSkipped = false, Stats = [] };
        renderer.SetScene(scene); Flush();
        baseline = ReadPixels(window);
        renderer.ShowAlert(new PreviewAlert(DurationSeconds: 1, Intensity: 1, ShakePixels: 24));
        Flush();
        elapsed.Restart();
        Thread.Sleep(250); Flush();
        foreach (var invalid in new[] { new PreviewRect(0, 0, 0, 216), new PreviewRect(8, 8, -4, 40),
            new PreviewRect(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue) })
        {
            renderer.SetScene(scene with { AlertBounds = invalid }); Flush();
            Capture(window, Path.Combine(output, "alert-invalid-" + invalid.Width + ".png"));
            var cleared = ReadPixels(window);
            Require(baseline.Bytes.SequenceEqual(cleared.Bytes), "Invalid or zero alert bounds must leave the frame clear: " + invalid);
        }
        renderer.SetScene(scene with { AlertBounds = new PreviewRect(-10, -10, 100, 100) }); Flush();
        active = ReadPixels(window);
        Require(!baseline.Bytes.SequenceEqual(active.Bytes), "Restoring nonempty bounds must retain the active alert.");
        AssertOutsideEqual(baseline, active, new PreviewRect(0, 0, 90, 90));
        // Finish after the original deadline, before a restarted one could finish.
        int remaining = Math.Max(0, 1100 - (int)elapsed.ElapsedMilliseconds);
        if (remaining > 0) Thread.Sleep(remaining);
        Flush();
        Require(baseline.Bytes.SequenceEqual(ReadPixels(window).Bytes), "Bounds changes must not extend a finite alert's deadline.");
    }

    private sealed record Pixels(byte[] Bytes, int Width, int Height, int Stride);

    private static Pixels ReadPixels(AvaloniaPreviewOverlayWindow window)
    {
        using var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No overlay frame rendered.");
        using var normalized = new Avalonia.Media.Imaging.WriteableBitmap(image.PixelSize, new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var buffer = normalized.Lock();
        image.CopyPixels(buffer, Avalonia.Platform.AlphaFormat.Premul);
        byte[] bytes = new byte[buffer.RowBytes * image.PixelSize.Height];
        Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);
        return new Pixels(bytes, image.PixelSize.Width, image.PixelSize.Height, buffer.RowBytes);
    }

    private static void AssertOutsideEqual(Pixels baseline, Pixels active, PreviewRect permitted)
    {
        for (int y = 0; y < baseline.Height; y++)
            for (int x = 0; x < baseline.Width; x++)
            {
                if (x >= permitted.X && x < permitted.X + permitted.Width && y >= permitted.Y && y < permitted.Y + permitted.Height) continue;
                int offset = y * baseline.Stride + x * 4;
                for (int channel = 0; channel < 4; channel++)
                    Require(baseline.Bytes[offset + channel] == active.Bytes[offset + channel], $"The alert obscured protected frame pixel ({x}, {y}).");
            }
    }

    private static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static string Capture(AvaloniaPreviewOverlayWindow window, string? path)
    {
        using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No overlay frame rendered.");
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        byte[] png = stream.ToArray();
        if (path is not null) File.WriteAllBytes(path, png);
        return Convert.ToHexString(SHA256.HashData(png));
    }
}
