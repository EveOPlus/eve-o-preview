using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Preview;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.Services.Logs;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.UI;
using EveOPreview.View;
using EveOPreview.View.Rendering;
using EveOPreview.Input;
using MediatR;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CombatOverlayNativeTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryWeaponPlatformHasOneDistinctReadableEmbeddedIcon()
    {
        var platforms = CombatAppearance.Preset("Classic").Weapons.Where(x => x.Key != WeaponPlatform.Unknown).ToArray();
        Assert.Equal(platforms.Length, platforms.Select(x => x.Value.Icon).Distinct().Count());
        string directory = Path.Combine(AppContext.BaseDirectory, "platform-icons"); Directory.CreateDirectory(directory);
        foreach (int size in new[] { 12, 16, 24 })
        {
            var hashes = new HashSet<string>();
            foreach (var platform in platforms)
            {
                var symbol = platform.Value.Icon;
                Assert.NotEmpty(OverlaySymbols.Fills(symbol));
                Assert.Empty(OverlaySymbols.Strokes(symbol));
                foreach (var polygon in OverlaySymbols.Fills(symbol))
                {
                    Assert.True(polygon.Length >= 6 && polygon.Length % 2 == 0);
                    Assert.All(polygon, coordinate => Assert.InRange(coordinate, 0, 16));
                }
                var scene = new OverlayScene { ShowTitle = false,
                    StatsStyle = new(size, 8, 8, true), Stats = [new("", "250", Icon: symbol)] };
                using var image = OverlaySceneRasterizer.Render(scene, new(100, 48));
                using var stream = new MemoryStream(); image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                Assert.True(hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()))),
                    "Platform icons must render differently at small sizes: " + platform.Key);
                image.Save(Path.Combine(directory, platform.Key + "-" + size + ".png"));
            }
        }
    }

    [Theory]
    [InlineData(SubtitlePlacement.Below, 14)]
    [InlineData(SubtitlePlacement.Above, 14)]
    [InlineData(SubtitlePlacement.Left, 14)]
    [InlineData(SubtitlePlacement.Right, 14)]
    [InlineData(SubtitlePlacement.Below, 40)]
    [InlineData(SubtitlePlacement.Above, 40)]
    [InlineData(SubtitlePlacement.Left, 40)]
    [InlineData(SubtitlePlacement.Right, 40)]
    public void SystemPlacementAndSizeProduceSeparateUncroppedInk(SubtitlePlacement placement, float systemSize)
    {
        var scene = new OverlayScene { Title = "Pilot One", Subtitle = "Jita", SubtitleColor = 0xFF00FF00,
            Font = new("Arial", 26, Foreground: 0xFFFF0000, Outline: 0xFF000000, OffsetX: 10, OffsetY: 10),
            SubtitlePlacement = placement, SubtitleFontSize = systemSize, CycleSkipped = true, MarkerColor = 0xFF0000FF };
        using var image = OverlaySceneRasterizer.Render(scene, new(500, 180));
        var title = Ink(image, true); var system = Ink(image, false);
        Assert.False(title.IsEmpty); Assert.False(system.IsEmpty);
        switch (placement)
        {
            case SubtitlePlacement.Above: Assert.True(system.Bottom < title.Top); break;
            case SubtitlePlacement.Below: Assert.True(title.Bottom < system.Top); break;
            case SubtitlePlacement.Left: Assert.True(system.Right < title.Left); break;
            case SubtitlePlacement.Right: Assert.True(title.Right < system.Left); break;
        }
        // Native composition uploads a cropped asset. Its bounds must contain the same ink.
        using var asset = (IDisposable)typeof(OverlaySceneRasterizer).GetMethod("RenderAsset", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [scene, new PreviewSize(500, 180), true, true])!;
        var assetType = asset.GetType();
        var crop = (Bitmap)assetType.GetProperty("Bitmap")!.GetValue(asset)!;
        int x = (int)assetType.GetProperty("X")!.GetValue(asset)!, y = (int)assetType.GetProperty("Y")!.GetValue(asset)!;
        var cropped = Ink(crop, false); cropped.Offset(x, y);
        Assert.Equal(system, cropped);

        static Rectangle Ink(Bitmap bitmap, bool red)
        {
            var result = Rectangle.Empty;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.A < 150 || (red ? pixel.R < 150 || pixel.G > 50 : pixel.G < 150 || pixel.R > 50)) continue;
                    var point = new Rectangle(x, y, 1, 1);
                    result = result.IsEmpty ? point : Rectangle.Union(result, point);
                }
            return result;
        }
    }

    [Fact]
    public Task SimulationUsesActualThumbnailWithoutTouchingDwmFocusOrTotals() => PrivateDesktopRunner.RunAsync("combat-overlay", output);

    internal static void RunScenario()
    {
        string root = Path.Combine(Path.GetTempPath(), "eve-combat-overlay-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var logger = new LoggerConfiguration().CreateLogger();
        var preferences = new ApplicationPreferences(Path.Combine(root, "settings.json"), logger);
        preferences.SetCombatLogs(new() { FlashAnimation = DamageFlashAnimation.Blink, DefaultOverlay = new() { EventDurationSeconds = 1, Repairs = true } });
        var logs = new CombatLogService(preferences, null, logger, Path.Combine(root, "logs.db"), new(), () => DateTimeOffset.UtcNow);
        CommandResult Simulate(CombatSimulation request)
        { var result = logs.SimulateLogEventAsync(request).GetAwaiter().GetResult(); TestAvalonia.Pump(); return result; }
        try
        {
            var assembly = typeof(ThumbnailView).Assembly;
            var config = (IThumbnailConfiguration)Activator.CreateInstance(assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration")!);
            var mediator = Stub.Create<IMediator>(); var keyboard = Stub.Create<IGlobalPointerInput>();
            int registrations = 0, updates = 0;
            var windows = Stub.Create<IWindowManager>((method, args) =>
            {
                if (method.Name == "GetLiveThumbnail")
                {
                    registrations++;
                    return Stub.Create<IDwmThumbnail>((operation, _) =>
                    { if (operation.Name == "Update") updates++; return operation.Name == "Update" ? true : Stub.Default(operation.ReturnType); });
                }
                return Stub.Default(method.ReturnType);
            });
            var manager = (IThumbnailManager)Activator.CreateInstance(assembly.GetType("EveOPreview.Services.ThumbnailManager")!,
                mediator, config, Stub.Create<IProcessMonitor>(), windows, Stub.Create<IThumbnailViewFactory>(), Stub.Create<IHotkeyService>(),
                Stub.Create<IHookService>(), Stub.Create<IGlobalEvents>(), logger, logs, preferences);
            using var client = new Form { Text = "Simulated client", ClientSize = new(320, 180) }; client.Show(); client.Activate(); TestAvalonia.Pump();
            SetActiveWindow(client.Handle); nint foreground = GetForegroundWindow();
            using var view = (ThumbnailView)Activator.CreateInstance(assembly.GetType("EveOPreview.View.LiveThumbnailView")!, windows, config, manager, mediator, keyboard, logger);
            try
            {
                view.Id = 101; view.Title = "EVE - Simulation Pilot"; view.ThumbnailSize = new(384, 216); view.IsOverlayEnabled = true;
                view.SetOverlayRenderer(OverlayRendererKind.NativeComposition);
                var known = (Dictionary<nint, IThumbnailView>)manager.GetType().GetField("_thumbnailViews", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
                known.Add(view.Id, view); view.Show(); view.SetTopMost(true); TestAvalonia.Pump(); SetActiveWindow(client.Handle);
                var overlay = (ThumbnailOverlay)typeof(ThumbnailView).GetField("_overlay", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
                OverlayScene Scene() => (OverlayScene)typeof(ThumbnailOverlay).GetProperty("Scene", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
                var renderer = (NativeCompositionOverlayRenderer)typeof(ThumbnailOverlay).GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
                int initialRegistrations = registrations, initialUpdates = updates;
                foreach (var damage in Enum.GetValues<CombatDamageType>())
                {
                    var result = Simulate(new(view.Title, DamageDirection.Incoming, 1250, damage, WeaponPlatform.Rocket));
                    Assert.True(result.Success, result.Message);
                    // Service completion precedes its queued UI notification. Pump
                    // until the retained scene arrives, including under full-suite load.
                    var delivered = DateTimeOffset.UtcNow.AddSeconds(1);
                    while (Scene().Stats.Count < 2 && DateTimeOffset.UtcNow < delivered) { Thread.Sleep(5); TestAvalonia.Pump(); }
                    Assert.True(Scene().Stats.Count >= 2, "Incoming event did not reach the retained thumbnail scene.");
                    WaitForName(true);
                    var alpha = Scene().Stats[0].Suffix;
                    Assert.Equal("α 1,250", alpha.Text); Assert.Contains(alpha.Icons(), icon => icon.Symbol == OverlaySymbol.Rocket);
                    Assert.Equal(0xFFFF7666u, Scene().TitleColor);
                    Assert.Empty(Scene().Stats[0].Icons()); Assert.False(Scene().Stats[1].Visible);
                    Assert.Equal(0xFFFF9990u, alpha.Color);
                    Assert.Equal("125", Scene().Stats.Single(x => x.Label == "In DPS").Value);
                    Assert.Equal(client.Handle, GetActiveWindow()); Assert.Equal(foreground, GetForegroundWindow());
                }
                void WaitForName(bool highlighted)
                {
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        Thread.Sleep(10); TestAvalonia.Pump();
                        if ((Scene().TitleColor is not null) == highlighted) return;
                    }
                    Assert.Fail("The native name must blink without another damage event.");
                }
                WaitForName(false); WaitForName(true);
                preferences.SetCombatLogs(preferences.CombatLogs with { FlashTarget = DamageFlashTarget.Both });
                Assert.True(Simulate(new(view.Title, DamageDirection.Incoming, 1250, CombatDamageType.EM, WeaponPlatform.Rocket)).Success);
                Assert.NotNull(Scene().TitleColor); Assert.Equal(0x33FF7666u, Scene().DamageTint);
                WaitForName(false); Assert.Null(Scene().DamageTint);
                WaitForName(true); Assert.NotNull(Scene().DamageTint);
                var flashScene = Scene();
                long fadeUploads = renderer.SurfaceUploadCount;
                renderer.SetScene(flashScene with { DamageFlashIntensity = .25 });
                renderer.SetScene(flashScene with { DamageFlashIntensity = .75 });
                renderer.SetScene(flashScene);
                Assert.Equal(fadeUploads, renderer.SurfaceUploadCount); // Smooth fading uses retained opacity, not glyph uploads.
                preferences.SetCombatLogs(preferences.CombatLogs with { FlashTarget = DamageFlashTarget.Thumbnail });
                Assert.True(Simulate(new(view.Title, DamageDirection.Incoming, 1250, CombatDamageType.EM, WeaponPlatform.Rocket)).Success);
                Assert.Null(Scene().TitleColor); Assert.NotNull(Scene().DamageTint);
                Assert.Equal(1, renderer.DamageTintSurfacePixels);
                var tint = Scene().DamageTint; long tintUploads = renderer.SurfaceUploadCount;
                view.SetDamageFlash(null, null); view.SetDamageFlash(null, tint);
                Assert.Equal(tintUploads, renderer.SurfaceUploadCount);
                preferences.SetCombatLogs(preferences.CombatLogs with { FlashTarget = DamageFlashTarget.Title });
                Assert.True(Simulate(new(view.Title, DamageDirection.Incoming, 1250, CombatDamageType.EM, WeaponPlatform.Rocket)).Success);
                Assert.Null(Scene().DamageTint); Assert.NotNull(Scene().TitleColor);
                Assert.Equal(initialRegistrations, registrations); Assert.Equal(initialUpdates, updates);
                foreach (var effect in new[] { CombatEffect.ShieldRepair, CombatEffect.ArmorRepair, CombatEffect.HullRepair })
                {
                    Assert.True(Simulate(new(view.Title, DamageDirection.Incoming, 900, CombatDamageType.Unknown, WeaponPlatform.Unknown, effect)
                        { BothRepairDirections = true }).Success);
                    Assert.True(Scene().Stats[2].Visible && Scene().Stats[3].Visible);
                    Assert.Equal("IN", Scene().Stats[2].Text); Assert.Equal("OUT", Scene().Stats[3].Text);
                    Assert.Equal("90", Scene().Stats[2].Suffix.Text); Assert.Equal("90", Scene().Stats[3].Suffix.Text);
                    Assert.Equal(Scene().Stats[2].Suffix.Color, Scene().Stats[2].Suffix.IconColor);
                    Assert.Equal(2, Scene().Stats.Count(x => x.Visible));
                }
                long uploads = renderer.SurfaceUploadCount, commits = renderer.CommitCount;
                var same = Scene(); view.SetOverlayStats(same.Stats.ToArray(), same.StatsStyle);
                Assert.Equal(uploads, renderer.SurfaceUploadCount); Assert.Equal(commits, renderer.CommitCount);
                Thread.Sleep(1150); TestAvalonia.Pump();
                Assert.Empty(Scene().Stats);
                Assert.Null(Scene().TitleColor);
                Assert.Equal(initialRegistrations, registrations); Assert.Equal(initialUpdates, updates);
                Assert.Equal(client.Handle, GetActiveWindow()); Assert.Equal(foreground, GetForegroundWindow());
                Assert.Empty(logs.ReadLogs().RecentEntries);
                using var second = (ThumbnailView)Activator.CreateInstance(assembly.GetType("EveOPreview.View.LiveThumbnailView")!, windows, config, manager, mediator, keyboard, logger);
                second.Id = 102; second.Title = "EVE - Second Pilot"; second.ThumbnailSize = new(384, 216); second.IsOverlayEnabled = true;
                second.SetOverlayRenderer(OverlayRendererKind.NativeComposition); known.Add(second.Id, second); second.Show(); second.SetTopMost(true);
                TestAvalonia.Pump(); SetActiveWindow(client.Handle);
                var secondOverlay = (ThumbnailOverlay)typeof(ThumbnailView).GetField("_overlay", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(second)!;
                OverlayScene SecondScene() => (OverlayScene)typeof(ThumbnailOverlay).GetProperty("Scene", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(secondOverlay)!;
                logs.CurrentSystems.Observe("Simulation Pilot", "Jita", DateTimeOffset.UtcNow);
                logs.CurrentSystems.Observe("Second Pilot", "Amarr", DateTimeOffset.UtcNow);
                Assert.True(Simulate(new("", DamageDirection.Incoming, 1250, CombatDamageType.EM, WeaponPlatform.Rocket)
                    { AllVisibleThumbnails = true, Randomize = true, DurationSeconds = 10 }).Success);
                Assert.Contains(Scene().Stats, x => x.Visible); Assert.Contains(SecondScene().Stats, x => x.Visible);
                Assert.All(Scene().Stats, x => Assert.DoesNotContain("TEST", x.Label));
                Assert.Equal("Jita", Scene().Subtitle); Assert.Equal("Amarr", SecondScene().Subtitle);
                Assert.True(Scene().SubtitleY > Scene().Font.OffsetY);
                int firstEntryCount = logs.ReadLogs().RecentEntries.Count;
                for (int i = 0; i < 20; i++) { Thread.Sleep(100); TestAvalonia.Pump(); }
                Assert.True(logs.ReadLogs().RecentEntries.Count > firstEntryCount);
                Assert.NotEmpty(logs.ReadLogs().RecentEntries); // Overview consumes the same temporary stream.
                // Live and simulated notifications use identical production presentation.
                typeof(CombatLogService).GetMethod("NotifyCombat", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(logs,
                    [new CombatOverlayEvent(new(DateTimeOffset.UtcNow, "Simulation Pilot", "combat", "", DamageDirection.Incoming, 777))]);
                TestAvalonia.Pump(); Assert.Equal("α 777", Scene().Stats[0].Suffix.Text);
                // An outgoing notification must retain the independent incoming hit.
                typeof(CombatLogService).GetMethod("NotifyCombat", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(logs,
                    [new CombatOverlayEvent(new(DateTimeOffset.UtcNow, "Simulation Pilot", "combat", "", DamageDirection.Outgoing, 888))]);
                TestAvalonia.Pump(); Assert.Equal("α 777", Scene().Stats[0].Suffix.Text); Assert.Equal("α 888", Scene().Stats[1].Suffix.Text);
                Assert.True(Simulate(new("", DamageDirection.Incoming, 1, CombatDamageType.Unknown, WeaponPlatform.Unknown) { Stop = true }).Success);
                Assert.Empty(Scene().Stats); Assert.Empty(SecondScene().Stats);
                Assert.Empty(logs.ReadLogs().RecentEntries);
                Assert.Equal("Jita", Scene().Subtitle); Assert.Equal("Jita", logs.CurrentSystems.GetSystem("Simulation Pilot"));
                Assert.False(File.Exists(Path.Combine(root, "logs.db"))); // Simulation never starts storage while logs are disabled.
                Assert.Equal(client.Handle, GetActiveWindow()); Assert.Equal(foreground, GetForegroundWindow());
                preferences.SetTheme("Legacy"); TestAvalonia.Pump();
                Assert.Equal("", Scene().Subtitle);
                Assert.False(Simulate(new(view.Title, DamageDirection.Incoming, 1, CombatDamageType.EM, WeaponPlatform.Rocket)).Success);
                using var compatibility = new ThumbnailOverlay(null, OverlayRendererKind.Legacy)
                    { Location = new Point(25, 35), ClientSize = new Size(384, 216) };
                compatibility.SetOverlayLabel("Compatibility title");
                compatibility.Show(); TestAvalonia.Pump(); SetActiveWindow(client.Handle);
                compatibility.SetDamageFlash(0xFFFF2233, null, .5);
                compatibility.SetOverlayFont(new FontSettings { ForeColor = Color.White });
                var compatibilityRenderer = Assert.IsType<CompatibilityOverlayRenderer>(compatibility.Content);
                var titleImages = compatibilityRenderer.Children.OfType<Avalonia.Controls.Image>().ToArray();
                Assert.Equal(OverlayColors.Blend(0xFFFFFFFF, 0xFFFF2233, .5), compatibility.Scene.EffectiveTitleColor);
                Assert.Equal(1, titleImages[0].Opacity);
                Assert.NotNull(titleImages[0].Source);
                compatibility.SetDamageFlash(null, null);
                Assert.Equal(unchecked((uint)Color.White.ToArgb()), compatibility.Scene.EffectiveTitleColor);
                Assert.Equal(1, titleImages[0].Opacity);
                compatibility.SetDamageFlash(0xFFFF2233, 0x50FF2233); TestAvalonia.Pump();
                var wash = Assert.Single(compatibilityRenderer.Children.OfType<Avalonia.Controls.Border>());
                nint overlayHandle = compatibility.Handle;
                Assert.Equal(384 / compatibility.RenderScaling, wash.Width);
                Assert.Equal(216 / compatibility.RenderScaling, wash.Height);
                Assert.Equal((byte)0x50, ((Avalonia.Media.SolidColorBrush)wash.Background!).Color.A);
                Assert.Equal(client.Handle, GetActiveWindow()); Assert.Equal(foreground, GetForegroundWindow());
                compatibility.SetDamageFlash(null, null); TestAvalonia.Pump();
                Assert.Null(wash.Background);
                compatibility.Location = new Point(45, 55);
                compatibility.SetDamageFlash(null, 0x50FF2233); TestAvalonia.Pump();
                Assert.Equal(overlayHandle, compatibility.Handle);
                Assert.Same(wash, Assert.Single(compatibilityRenderer.Children.OfType<Avalonia.Controls.Border>()));
                compatibility.Hide(); TestAvalonia.Pump(); Assert.False(compatibilityRenderer.IsVisible);
                compatibility.Show(); TestAvalonia.Pump(); Assert.True(compatibilityRenderer.IsVisible);
                Assert.Equal((byte)0x50, ((Avalonia.Media.SolidColorBrush)wash.Background!).Color.A);
                Assert.Equal(client.Handle, GetActiveWindow()); Assert.Equal(foreground, GetForegroundWindow());
                compatibility.Dispose(); Assert.All(titleImages, image => Assert.Null(image.Source));
            }
            finally { ((IDisposable)manager).Dispose(); }
        }
        finally { logs.Dispose(); logs.Completion.GetAwaiter().GetResult(); Directory.Delete(root, true); }
    }
    [DllImport("user32.dll")] private static extern nint SetActiveWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetActiveWindow();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
}
