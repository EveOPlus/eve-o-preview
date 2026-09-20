using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Preview;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using EveOPreview.UI.Previews;
using EveOPreview.View;
using EveOPreview.Input;
using MediatR;
using Serilog;

namespace EveOPreview.RenderingSmoke;

internal static partial class Program
{
    // Opt-in: real DWM sources, production service -> manager -> renderer, isolated
    // settings/database. Never writes EVE logs or the running application's history.
    private static int ValidateCombatSimulation(List<ThumbnailView> views, CountingWindowManager windows,
        IThumbnailConfiguration config, ILogger logger, Options options)
    {
        if (!options.Live || options.Renderer != "native" || views.Select(x => x.Id).Distinct().Count() != views.Count)
            throw new ArgumentException("--combat-simulation requires --live --renderer native and no duplicate source thumbnails.");
        string root = Path.Combine(options.Output, "isolated-state"); Directory.CreateDirectory(root);
        var preferences = new ApplicationPreferences(Path.Combine(root, "settings.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, FlashTarget = DamageFlashTarget.Both, FlashAnimation = DamageFlashAnimation.Blink,
            DefaultOverlay = new() { Repairs = true, Preset = "Minimal" } });
        var logs = new CombatLogService(preferences, null, logger, Path.Combine(root, "combat.sqlite"), EveLogCatalog.Load(), () => DateTimeOffset.UtcNow);
        var assembly = typeof(ThumbnailView).Assembly;
        var manager = (IThumbnailManager)Activator.CreateInstance(assembly.GetType("EveOPreview.Services.ThumbnailManager")!,
            NoOp.Create<IMediator>(), config, NoOp.Create<IProcessMonitor>(), windows, NoOp.Create<IThumbnailViewFactory>(),
            NoOp.Create<IHotkeyService>(), NoOp.Create<IHookService>(), NoOp.Create<IGlobalEvents>(), logger, logs, preferences)!;
        var known = (Dictionary<nint, IThumbnailView>)manager.GetType().GetField("_thumbnailViews", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
        OverlayScene Scene(ThumbnailView view)
        {
            var overlay = typeof(ThumbnailView).GetField("_overlay", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
            return (OverlayScene)overlay.GetType().GetProperty("Scene", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
        }
        static double Total(CombatLogSnapshot snapshot) => snapshot.Characters.Sum(x => x.Categories.Sum(c => c.Total.Incoming + c.Total.Outgoing));
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        var captured = new Dictionary<ThumbnailView, AvaloniaPreviewOverlayWindow>();
        var frames = new List<object>();
        var simulated = new System.Collections.Concurrent.ConcurrentQueue<ParsedLogEntry>();
        logs.CombatEvent += notification => { if (notification.Simulated) simulated.Enqueue(notification.Entry); };
        try
        {
            foreach (var view in views) known.Add(view.Id, view);
            var startup = Stopwatch.StartNew(); long lastReads = -1; int settled = 0;
            while (startup.Elapsed.TotalSeconds < 20 && settled < 4)
            {
                Pump(TimeSpan.FromMilliseconds(250));
                long reads = logs.Diagnostics.Reads;
                settled = reads == lastReads && logs.ReadLogs().Status.Contains("Watching") ? settled + 1 : 0;
                lastReads = reads;
            }
            var before = logs.ReadLogs();
            double peakTotal = Total(before);
            int registrations = windows.Registrations;
            long foreground = Native.GetForegroundWindow().ToInt64();
            int duration = Math.Clamp(options.Seconds, 5, 60);
            var command = logs.SimulateLogEventAsync(new("", DamageDirection.Incoming, 1250, CombatDamageType.EM, WeaponPlatform.Rocket)
                { AllVisibleThumbnails = true, Randomize = true, DurationSeconds = duration }).GetAwaiter().GetResult();
            Require(command.Success, command.Message);
            var timer = Stopwatch.StartNew(); int phase = 0;
            while (timer.Elapsed.TotalSeconds < duration)
            {
                Pump(TimeSpan.FromMilliseconds(250));
                if (timer.Elapsed.TotalSeconds < phase * 3 || timer.Elapsed.TotalSeconds > duration - 1) continue;
                foreach (var (view, index) in views.Select((view, index) => (view, index)))
                {
                    var scene = Scene(view);
                    Require(scene.Stats.Count == 4 && scene.Stats.Any(x => x.Visible) && scene.Stats.All(x => !x.Label.Contains("TEST")), "Simulation did not use normal production formatting.");
                    string? system = logs.CurrentSystems.GetSystem(view.Title[6..]);
                    Require(scene.Subtitle == (system ?? ""), "System text is not the current known system name alone.");
                    var overview = logs.ReadLogs(); peakTotal = Math.Max(peakTotal, Total(overview));
                    frames.Add(new { Phase = phase, Client = index + 1, scene.Subtitle, scene.SubtitleY, scene.Stats,
                        OverviewTotal = Total(overview), IncomingNames = overview.Characters.Count(x => x.LastIncomingDamageAt is not null) });
                    if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"combat-{phase}-{index + 1}.png"));
                }
                Require(!OwnsForeground(views, captured), "Simulation captured foreground focus.");
                phase++;
            }
            Pump(TimeSpan.FromMilliseconds(350));
            Require(!logs.IsSimulating, "Timed simulation did not expire.");
            Require(peakTotal > Total(before) && simulated.Count > 3, "Normal overview statistics did not receive the simulated stream.");
            // A deterministic all-types incoming event verifies both native title
            // colour and the full alpha icon strip, rather than relying on random hits.
            Require(logs.SimulateLogEventAsync(new("", DamageDirection.Incoming, 1600, CombatDamageType.Mixed, WeaponPlatform.Unknown)
                { AllVisibleThumbnails = true, DurationSeconds = 10 }).Result.Success, "Incoming damage validation failed.");
            Pump(TimeSpan.FromMilliseconds(100));
            foreach (var (view, index) in views.Select((view, index) => (view, index)))
            {
                var scene = Scene(view);
                Require(scene.TitleColor == 0xFFFF7666u, "Incoming damage did not highlight the actual thumbnail character name.");
                Require(scene.DamageTint == 0x33FF7666u, "Incoming damage did not tint the entire live thumbnail.");
                var row = scene.Stats.Single(x => x.Label == "In DPS");
                Require(!row.Icons().Any(), "DPS must not repeat alpha's damage icons.");
                var icons = row.Suffix!.Icons().Select(x => x.Symbol).ToArray();
                Require(new[] { OverlaySymbol.EveEM, OverlaySymbol.EveThermal, OverlaySymbol.EveKinetic, OverlaySymbol.EveExplosive }.All(icons.Contains),
                    "Incoming alpha did not show all four damage type icons.");
                if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"incoming-flash-types-{index + 1}.png"));
            }
            void WaitForNames(bool highlighted)
            {
                var deadline = Stopwatch.StartNew();
                while (deadline.ElapsedMilliseconds < 1000)
                {
                    Pump(TimeSpan.FromMilliseconds(10));
                    if (views.All(view => (Scene(view).TitleColor is not null) == highlighted && Scene(view).DamageTint.HasValue == highlighted)) return;
                }
                Require(false, "Character names did not alternate between flash and normal colours.");
            }
            WaitForNames(false);
            foreach (var (view, index) in views.Select((view, index) => (view, index)))
                if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"incoming-blink-off-{index + 1}.png"));
            WaitForNames(true);
            foreach (var (view, index) in views.Select((view, index) => (view, index)))
                if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"incoming-blink-on-{index + 1}.png"));
            preferences.SetCombatLogs(preferences.CombatLogs with { FlashAnimation = DamageFlashAnimation.Fade, FlashOpacityPercent = 10 });
            Require(logs.SimulateLogEventAsync(new("", DamageDirection.Incoming, 1600, CombatDamageType.Kinetic, WeaponPlatform.Railgun)
                { AllVisibleThumbnails = true, DurationSeconds = 10 }).Result.Success, "Fade simulation failed.");
            foreach (var (label, low, high) in new[] { ("low", .02, .15), ("half", .4, .6), ("peak", .94, 1.0) })
            {
                var deadline = Stopwatch.StartNew();
                while (deadline.ElapsedMilliseconds < 1500)
                {
                    Pump(TimeSpan.FromMilliseconds(10));
                    if (views.All(view => Scene(view).DamageFlashIntensity >= low && Scene(view).DamageFlashIntensity <= high)) break;
                }
                Require(views.All(view => Scene(view).DamageFlashIntensity >= low && Scene(view).DamageFlashIntensity <= high), "Fade did not reach " + label);
                Require(views.All(view => Scene(view).DamageTint >> 24 == 26), "Configured 10% opacity must reach every native thumbnail.");
                foreach (var (view, index) in views.Select((view, index) => (view, index)))
                    if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"incoming-fade-{label}-{index + 1}.png"));
            }
            foreach (var position in new[] { OverlayPosition.TopLeft, OverlayPosition.MiddleCenter, OverlayPosition.BottomRight })
            {
                preferences.SetCombatLogs(preferences.CombatLogs with { DefaultOverlay = preferences.CombatLogs.DefaultOverlay with
                    { TitlePosition = position, Position = position, RowOrder = CombatRowOrder.RepairsOutgoingIncoming } });
                Pump(TimeSpan.FromMilliseconds(90));
                foreach (var (view, index) in views.Select((view, index) => (view, index)))
                {
                    Require(Scene(view).TitlePosition == position && Scene(view).StatsStyle.Position == position, "Shared placement did not reach the live view.");
                    if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"stacked-{position}-{index + 1}.png"));
                }
            }
            preferences.SetCombatLogs(preferences.CombatLogs with { FlashAnimation = DamageFlashAnimation.Blink,
                DefaultOverlay = preferences.CombatLogs.DefaultOverlay with { TitlePosition = OverlayPosition.TopLeft, Position = null, RowOrder = CombatRowOrder.IncomingOutgoingRepairs } });
            preferences.SetCombatLogs(preferences.CombatLogs with { DefaultOverlay = preferences.CombatLogs.DefaultOverlay with
                { FontFamily = "Arial", FontStyle = OverlayFontStyle.Bold | OverlayFontStyle.Italic } });
            Pump(TimeSpan.FromMilliseconds(100));
            foreach (var (view, index) in views.Select((view, index) => (view, index)))
            {
                Require(Scene(view).StatsStyle.FontFamily == "Arial" && Scene(view).StatsStyle.FontStyle == (OverlayFontStyle.Bold | OverlayFontStyle.Italic),
                    "Shared DPS font override did not reach every live thumbnail.");
                if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"dps-font-override-{index + 1}.png"));
            }
            preferences.SetCombatLogs(preferences.CombatLogs with { DefaultOverlay = preferences.CombatLogs.DefaultOverlay with { FontFamily = null, FontStyle = null } });
            Pump(TimeSpan.FromMilliseconds(100));
            Require(views.All(view => Scene(view).StatsStyle.FontFamily is null && Scene(view).StatsStyle.FontStyle is null), "Font reset did not restore inheritance.");
            Require(simulated.Any(x => x.Effect != CombatEffect.Damage && x.Direction == DamageDirection.Incoming)
                && simulated.Any(x => x.Effect != CombatEffect.Damage && x.Direction == DamageDirection.Outgoing),
                "Mixed combat did not include both repair directions.");
            foreach (var effect in new[] { CombatEffect.ShieldRepair, CombatEffect.ArmorRepair, CombatEffect.HullRepair })
            foreach (var direction in new[] { DamageDirection.Incoming, DamageDirection.Outgoing })
            {
                Require(logs.SimulateLogEventAsync(new("", direction, 800, CombatDamageType.Unknown, WeaponPlatform.Unknown, effect)
                    { DurationSeconds = 10, AllVisibleThumbnails = true }).Result.Success, "Selected repair simulation failed.");
                Pump(TimeSpan.FromMilliseconds(100));
                var symbol = effect == CombatEffect.ShieldRepair ? OverlaySymbol.Shield : effect == CombatEffect.ArmorRepair ? OverlaySymbol.Armor : OverlaySymbol.Hull;
                foreach (var (view, index) in views.Select((view, index) => (view, index)))
                {
                    Require(Scene(view).Stats[direction == DamageDirection.Incoming ? 2 : 3] is { Visible: true } row && row.Suffix?.Icon == symbol,
                        "A repair did not reach the live thumbnail: " + effect + " " + direction);
                    if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"{effect}-{direction}-{index + 1}.png"));
                }
            }
            foreach (var effect in new[] { CombatEffect.ShieldRepair, CombatEffect.ArmorRepair, CombatEffect.HullRepair })
            {
                Require(logs.SimulateLogEventAsync(new("", DamageDirection.Incoming, 800, CombatDamageType.Unknown, WeaponPlatform.Unknown, effect)
                    { BothRepairDirections = true, DurationSeconds = 10, AllVisibleThumbnails = true }).Result.Success, "Simultaneous repair simulation failed.");
                Pump(TimeSpan.FromMilliseconds(150));
                foreach (var (view, index) in views.Select((view, index) => (view, index)))
                {
                    var repairs = Scene(view).Stats.Skip(2).Where(x => x.Visible).ToArray();
                    Require(repairs.Length == 2 && repairs.Select(x => x.Label).SequenceEqual(new[] { "IN", "OUT" }) && repairs.All(x => x.Suffix!.Color == x.Suffix.IconColor),
                        "Both repairs must remain visible on the same native thumbnail.");
                    if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"{effect}-Both-{index + 1}.png"));
                }
            }
            Require(logs.SimulateLogEventAsync(new("", DamageDirection.Incoming, 800, CombatDamageType.Unknown, WeaponPlatform.Unknown, CombatEffect.ShieldRepair)
                { BothRepairDirections = true, MixedRepairTypes = true, RepairSourceCount = 6, DurationSeconds = 0, AllVisibleThumbnails = true }).Result.Success,
                "Multi-source repair simulation failed.");
            Pump(TimeSpan.FromMilliseconds(200));
            foreach (var (view, index) in views.Select((view, index) => (view, index)))
            {
                var repairs = Scene(view).Stats.Skip(2).ToArray();
                Require(repairs.Length == 2 && repairs.Select(x => x.Label).SequenceEqual(new[] { "IN", "OUT" }) && repairs.All(x => x.Suffix!.Segments().Count() == 3
                    && x.Suffix.Segments().All(segment => segment.Label == "" && segment.Color == segment.IconColor && segment.Text == "160")),
                    "Six sources must combine into three coloured 160 HP/s icon/amount pairs in each direction.");
                if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"mixed-repair-rates-{index + 1}.png"));
            }
            preferences.SetCombatLogs(preferences.CombatLogs with { WindowSeconds = 3 });
            Require(logs.SimulateLogEventAsync(new("", DamageDirection.Incoming, 800, CombatDamageType.Unknown, WeaponPlatform.Unknown, CombatEffect.ShieldRepair)
                { BothRepairDirections = true, MixedRepairTypes = true, RepairSourceCount = 6, DurationSeconds = 10, AllVisibleThumbnails = true }).Result.Success,
                "Cycling repair simulation failed.");
            foreach (var (phaseName, count) in new[] { ("single", 1), ("combined", 3), ("single-again", 1) })
            {
                var deadline = Stopwatch.StartNew();
                bool Matches(ThumbnailView view) => Scene(view).Stats.Skip(2).Count() == 2
                    && Scene(view).Stats.Skip(2).Select(x => x.Label).SequenceEqual(new[] { "IN", "OUT" })
                    && Scene(view).Stats.Skip(2).All(x => x.Visible && x.Suffix!.Segments().Count() == count);
                do { Pump(TimeSpan.FromMilliseconds(100)); }
                while (!views.All(Matches) && deadline.Elapsed.TotalSeconds < 5);
                Require(views.All(Matches), "Repair rows failed to cycle to " + phaseName);
                foreach (var (view, index) in views.Select((view, index) => (view, index)))
                    if (options.Capture) CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"repair-cycle-{phaseName}-{index + 1}.png"));
            }
            var weaponAppearance = CombatAppearance.Preset("Classic") with { ShowWeaponIcon = true };
            preferences.SetCombatLogs(preferences.CombatLogs with { DefaultOverlay = preferences.CombatLogs.DefaultOverlay with
                { Advanced = true, CustomAppearance = weaponAppearance } });
            int weaponIconCases = 0;
            foreach (var platform in Enum.GetValues<WeaponPlatform>())
            foreach (var damage in new[] { CombatDamageType.Unknown, CombatDamageType.Kinetic })
            {
                Require(logs.SimulateLogEventAsync(new("", DamageDirection.Outgoing, 139, damage, platform)
                    { DurationSeconds = 0, AllVisibleThumbnails = true }).Result.Success, "Weapon icon simulation failed.");
                Pump(TimeSpan.FromMilliseconds(100));
                var expectedIcons = new List<OverlaySymbol>();
                if (damage == CombatDamageType.Kinetic) expectedIcons.Add(OverlaySymbol.EveKinetic);
                if (platform != WeaponPlatform.Unknown) expectedIcons.Add(weaponAppearance.WeaponStyle(platform).Icon);
                foreach (var (view, index) in views.Select((view, index) => (view, index)))
                {
                    var row = Scene(view).Stats.Single(x => x.Label == "Out DPS");
                    Require(row.Suffix!.Icons().Select(x => x.Symbol).SequenceEqual(expectedIcons),
                        "Native alpha must show only known weapon/damage icons: " + platform + " " + damage);
                    if (options.Capture && platform is WeaponPlatform.Autocannon or WeaponPlatform.Unknown)
                        CaptureOwnedPreview(view, captured, Path.Combine(options.Output, $"known-icons-{platform}-{damage}-{index + 1}.png"));
                }
                weaponIconCases++;
            }
            Require(logs.SimulateLogEventAsync(new("", DamageDirection.Incoming, 1, CombatDamageType.Unknown, WeaponPlatform.Unknown) { Stop = true }).Result.Success, "Stop failed.");
            Pump(TimeSpan.FromMilliseconds(100));
            Require(!logs.IsSimulating, "Stop did not restore live data.");
            Require(views.All(view => Scene(view).TitleColor is null), "Temporary incoming name colour survived the simulation.");
            var after = logs.ReadLogs();
            Require(!after.RecentEntries.Intersect(simulated).Any(), "A simulated event remained after Stop.");
            int persistedSimulation;
            using (var db = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                { DataSource = Path.Combine(root, "combat.sqlite"), Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            {
                db.Open(); using var check = db.CreateCommand(); check.CommandText = "SELECT COUNT(*) FROM entries WHERE source LIKE 'simulation-%'";
                persistedSimulation = Convert.ToInt32(check.ExecuteScalar());
            }
            Require(persistedSimulation == 0, "A simulation event was persisted.");
            Require(windows.Registrations == registrations, "Simulation replaced a live DWM relationship.");
            Require(!OwnsForeground(views, captured), "Simulation captured focus.");
            var result = new { Passed = true, Clients = views.Count, Duration = duration, Frames = frames,
                BeforeTotal = Total(before), AfterTotal = Total(after), TotalsUnchanged = Total(before) == Total(after),
                SimulationHistoryEntries = persistedSimulation, PeakOverviewTotal = peakTotal, SimulatedEventsReceived = simulated.Count,
                DwmRegistrationsBefore = registrations, DwmRegistrationsAfter = windows.Registrations,
                ForegroundBefore = foreground, ForegroundAfter = Native.GetForegroundWindow().ToInt64(),
                KnownSystems = views.Count(x => logs.CurrentSystems.GetSystem(x.Title[6..]) is not null),
                after.Status, StopRestoredLiveData = true, NaturalExpiry = true, NameBlinkVerified = true, FontOverrideAndResetVerified = true,
                RepairDirectionsVerified = true, SimultaneousRepairsVerified = true, CombinedRepairRatesVerified = true, RepairRateCycleVerified = true, ShieldArmorHullRepairsVerified = true,
                KnownIconCombinationsVerified = weaponIconCases,
                Limitations = "Isolated validation instance using real EVE DWM windows and shared read-only logs. Running app settings/history are untouched. Real incoming events can legitimately change totals during the test." };
            string json = JsonSerializer.Serialize(result, JsonOptions);
            File.WriteAllText(Path.Combine(options.Output, "combat-result.json"), json);
            Console.WriteLine(JsonSerializer.Serialize(new { result.Passed, result.Clients, result.TotalsUnchanged, result.KnownSystems,
                result.SimulationHistoryEntries, result.SimulatedEventsReceived, result.PeakOverviewTotal,
                result.DwmRegistrationsAfter, result.ForegroundBefore, result.ForegroundAfter, result.Status }, JsonOptions));
            return 0;
        }
        finally
        {
            // Outer harness owns and closes the actual thumbnail windows.
            known.Clear(); ((IDisposable)manager).Dispose();
            logs.Dispose(); logs.Completion.GetAwaiter().GetResult();
        }
    }
}
