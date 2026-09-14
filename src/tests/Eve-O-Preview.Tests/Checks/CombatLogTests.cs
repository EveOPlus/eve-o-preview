using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Preview;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CombatLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
    private static EveLogCatalog Catalog() => new()
    {
        NpcNames = new(StringComparer.OrdinalIgnoreCase) { "Guristas Despoiler" }, Systems = new() { ["Jita"] = 30000142, ["Amarr"] = 30002187 },
        Weapons = new() { ["Mjolnir Rocket"] = new(WeaponPlatform.Rocket, CombatDamageType.EM),
            ["Light Neutron Blaster II"] = new(WeaponPlatform.Blaster, CombatDamageType.Unknown) }
    };
    private static string Header(string name = "Pilot One") => $"---\r\nGamelog\r\nListener: {name}\r\nSession Started: 2026.09.13 12:00:00\r\n---\r\n";
    private static string Hit(string peer = "Guristas Despoiler", int amount = 100) =>
        $"[ 2026.09.13 12:00:00 ] (combat) <color=0xffcc0000><b>{amount}</b> <font size=10>from</font> <b>{peer}</b> - Mjolnir Rocket - Hits\r\n";

    [Fact]
    public void KnownWeaponIconsDoNotLookUnknownWhenAmmunitionIsAbsent()
    {
        var appearance = CombatAppearance.Preset("Classic") with { ShowWeaponIcon = true };
        var options = new LogOverlayOptions { Advanced = true, CustomAppearance = appearance };
        foreach (var platform in Enum.GetValues<WeaponPlatform>().Where(x => x != WeaponPlatform.Unknown))
        foreach (var direction in Enum.GetValues<DamageDirection>())
        {
            var hit = new CombatOverlayEvent(new(Now, "Pilot One", "combat", "", direction, 125,
                Platform: platform, DamageType: CombatDamageType.Unknown));
            var row = CombatOverlayFormatter.Event(hit, options);
            Assert.Equal(appearance.WeaponStyle(platform).Icon, Assert.Single(row.Icons()).Symbol);
            Assert.Equal(DamageTypes.None, hit.Entry.EffectiveDamageTypes);
            var damageOnly = CombatOverlayFormatter.Event(hit, options with
                { CustomAppearance = appearance with { ShowWeaponIcon = false } });
            Assert.Empty(damageOnly.Icons());
            var knownDamage = CombatOverlayFormatter.Event(new(hit.Entry with
                { DamageTypes = DamageTypes.Thermal, DamageType = CombatDamageType.Thermal }), options);
            Assert.Equal(new[] { OverlaySymbol.EveThermal, appearance.WeaponStyle(platform).Icon }, knownDamage.Icons().Select(x => x.Symbol));
        }
        var unknown = new CombatOverlayEvent(new(Now, "Pilot One", "combat", "", DamageDirection.Incoming, 125));
        Assert.Empty(CombatOverlayFormatter.Event(unknown, options).Icons());
        var ammoOnly = new CombatOverlayEvent(unknown.Entry with { DamageTypes = DamageTypes.EM | DamageTypes.Explosive,
            DamageType = CombatDamageType.Mixed });
        Assert.Equal(new[] { OverlaySymbol.EveEM, OverlaySymbol.EveExplosive },
            CombatOverlayFormatter.Event(ammoOnly, options).Icons().Select(x => x.Symbol));
        Assert.Empty(CombatOverlayFormatter.Event(unknown, options with
            { CustomAppearance = appearance with { ShowDamageIcon = false, ShowWeaponIcon = false } }).Icons());
        var custom = options with { CustomAppearance = appearance with
            { Weapons = new Dictionary<WeaponPlatform, CombatVisualStyle> { [WeaponPlatform.Autocannon] = new("#12AB34", OverlaySymbol.Heat) } } };
        var customIcon = Assert.Single(CombatOverlayFormatter.Event(new(unknown.Entry with { Platform = WeaponPlatform.Autocannon }), custom).Icons());
        Assert.Equal(OverlaySymbol.Heat, customIcon.Symbol); Assert.Equal(0xFF12AB34u, customIcon.Color);
    }

    [Fact]
    public void MarkedUpScoutAutocannonAndNpcGunRocketHitsKeepTheirKnownPlatforms()
    {
        var catalog = EveLogCatalog.Load();
        var options = new LogOverlayOptions { Advanced = true,
            CustomAppearance = CombatAppearance.Preset("Classic") with { ShowWeaponIcon = true } };
        foreach (var (direction, peer, suffix, expected, damage) in new[]
        {
            ("to", "Guristas Saboteur", "200mm Light 'Scout' Autocannon I - Grazes", WeaponPlatform.Autocannon, DamageTypes.None),
            ("from", "Guristas Saboteur", "Scourge Rocket - Hits", WeaponPlatform.Rocket, DamageTypes.Kinetic),
            ("from", "Guristas Saboteur", "Grazes", WeaponPlatform.Railgun, DamageTypes.Thermal | DamageTypes.Kinetic),
            ("from", "Guristas Arrogator", "Grazes", WeaponPlatform.Blaster, DamageTypes.Thermal | DamageTypes.Kinetic)
        })
        {
            var entry = EveLogParser.Parse("[ 2026.09.14 01:00:00 ] (combat) <color=0xff00ffff><b>42</b> <color=0x77ffffff><font size=10>"
                + direction + "</font> <b><color=0xffffffff>" + peer + "</b><font size=10><color=0x77ffffff> - " + suffix,
                new("Pilot One"), false, catalog, new HashSet<string>());
            Assert.NotNull(entry); Assert.Equal(expected, entry.Platform); Assert.Equal(damage, entry.EffectiveDamageTypes);
            Assert.Equal(CombatantKind.Npc, entry.Kind); Assert.Equal(42, entry.Amount);
            var icons = CombatOverlayFormatter.Event(new(entry), options).Icons().Select(x => x.Symbol).ToArray();
            Assert.Contains(options.GetAppearance().WeaponStyle(expected).Icon, icons);
            Assert.DoesNotContain(OverlaySymbol.Unknown, icons);
        }
    }

    [Fact]
    public void NpcNamesWithSeparatorsRemainWholeAndTargetsDoNotSupplyOutgoingDamageTypes()
    {
        var catalog = Catalog(); string name = "Blood Phantom - Ectoplasm";
        catalog.NpcNames.Add(name); catalog.NpcNames.Add("Blood Phantom");
        catalog.NpcAttacks[name] = new(WeaponPlatform.Laser, CombatDamageType.Mixed, DamageTypes.EM | DamageTypes.Thermal);
        ParsedLogEntry Parse(string body) => EveLogParser.Parse("[ 2026.09.13 12:00:00 ] (combat) " + body, new("Pilot One"), false, catalog, new HashSet<string>())!;
        var incoming = Parse("16 from Blood Phantom - Ectoplasm - Glances Off");
        Assert.Equal(name, incoming.Counterparty); Assert.Equal(CombatantKind.Npc, incoming.Kind);
        Assert.Equal(WeaponPlatform.Laser, incoming.Platform); Assert.Equal(DamageTypes.EM | DamageTypes.Thermal, incoming.DamageTypes);
        var outgoing = Parse("32 to Blood Phantom - Ectoplasm - Light Neutron Blaster II - Hits");
        Assert.Equal(name, outgoing.Counterparty); Assert.Equal(WeaponPlatform.Blaster, outgoing.Platform);
        Assert.Equal(DamageTypes.None, outgoing.DamageTypes);
        Assert.Equal(DamageTypes.None, Parse("32 to Blood Phantom - Ectoplasm - Hits").DamageTypes);
    }

    [Fact]
    public void SimulatedLogTextUsesRealDecimalFormatAndWeaponStylingOnlyColoursAlpha()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("de-DE");
            var request = new CombatSimulation("EVE - Pilot One", DamageDirection.Incoming, 680.5, CombatDamageType.Unknown, WeaponPlatform.Unknown);
            var source = new SimulationSource("Orion Voss", CombatantKind.Player,
                [new("Light Neutron Blaster II", WeaponPlatform.Blaster, DamageTypes.None, 0, DamageEvidence.Unavailable, 95.5, 4000, 42)]);
            var generated = new CombatSimulationSequence(request, Now, 3, sources: [source]).DrainEvents().Single();
            ParsedLogEntry Parse(ParsedLogEntry entry) => EveLogParser.Parse("[ 2026.09.13 12:00:00 ] (combat) " + entry.Text,
                new("Pilot One"), false, Catalog(), new HashSet<string> { "Orion Voss" })!;
            var parsed = Parse(generated); Assert.Equal(95.5, parsed.Amount); Assert.Equal(WeaponPlatform.Blaster, parsed.Platform);
            Assert.Equal(DamageTypes.None, parsed.DamageTypes);
            var appearance = CombatAppearance.Preset("Classic") with { TextColorMode = CombatTextColorMode.WeaponPlatform,
                Weapons = new Dictionary<WeaponPlatform, CombatVisualStyle> { [WeaponPlatform.Blaster] = new("#FFD166", OverlaySymbol.Railgun, "#AA44FF") } };
            var options = new LogOverlayOptions { Advanced = true, CustomAppearance = appearance };
            var row = CombatOverlayFormatter.Simulation(new(parsed), options, 10).First();
            Assert.Equal(CombatOverlayFormatter.Color(appearance.IncomingColor), row.Color); Assert.Empty(row.Icons());
            Assert.Equal(0xFFAA44FFu, row.Suffix!.Color);
            Assert.Contains(row.Suffix.Icons(), x => x.Symbol == OverlaySymbol.Railgun && x.Color == 0xFFFFD166u);
            var unresolved = CombatOverlayFormatter.Event(new(parsed with { Platform = WeaponPlatform.Unknown }), options);
            Assert.Equal(CombatOverlayFormatter.Color(appearance.IncomingColor), unresolved.Color);
            var repair = new CombatSimulationSequence(request with { Effect = CombatEffect.ShieldRepair }, Now, 3).DrainEvents().Single();
            Assert.Equal(680.5, Parse(repair).Amount); Assert.Equal(CombatEffect.ShieldRepair, Parse(repair).Effect);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void RealNpcGunMissileAndOmniHitsKeepIndividualDamageIconsInAlpha()
    {
        var catalog = EveLogCatalog.Load();
        ParsedLogEntry Parse(string peer, string suffix) => EveLogParser.Parse(
            $"[ 2026.09.13 12:00:00 ] (combat) <b>320</b> from <b>{peer}</b> - {suffix}", new("Pilot One"), false, catalog, new HashSet<string>());
        var gun = Parse("Hypnosian Warden", "Hits");
        var missile = Parse("Hypnosian Warden", "Praedormitan Missile - Hits");
        var omni = Parse("Vexing Phase-I Swarmer", "Penetrates");
        Assert.Equal(DamageTypes.EM | DamageTypes.Thermal, gun.DamageTypes);
        Assert.Equal(DamageTypes.Kinetic | DamageTypes.Explosive, missile.DamageTypes);
        Assert.Equal(DamageTypes.All, omni.DamageTypes);
        Assert.Equal(DamageTypes.Thermal, Parse("Other Pilot", "Federation Navy Large Plasma Smartbomb - Hits").DamageTypes);
        Assert.Equal(DamageTypes.None, Parse("Other Pilot[CORP](Vargur)", "800mm Repeating Cannon II - Grazes").DamageTypes);
        using var store = new CombatLogStore(":memory:", Now);
        store.Commit(new("Gamelogs/sample.txt", new("sample", 0, 3, "utf-8", ""), new("Pilot One")), [new(1, gun), new(2, missile), new(3, omni)]);
        var character = Assert.Single(store.Snapshot(Now.AddSeconds(1), 10, "", "", new Dictionary<string, long?>()).Characters);
        Assert.Equal(DamageTypes.All, character.IncomingDamageTypes); Assert.False(character.HasUnresolvedIncomingDamage);
        var row = CombatOverlayFormatter.Meter(character, new(), new(omni))[0];
        Assert.Empty(row.Icons());
        Assert.Equal(new[] { OverlaySymbol.EveEM, OverlaySymbol.EveThermal, OverlaySymbol.EveKinetic, OverlaySymbol.EveExplosive }, row.Suffix.Icons().Select(x => x.Symbol));
        Assert.Equal(0xFF70BFFFu, CombatOverlayFormatter.Event(new(gun with { DamageTypes = DamageTypes.EM, DamageType = CombatDamageType.EM }),
            new() { Preset = "Damage colours" }).Color);
        Assert.Equal(DamageTypes.All, character.Categories.Single(x => x.Kind == CombatantKind.Npc).IncomingDamageTypes);
        var unknownPlayer = Parse("Other Pilot", "800mm Repeating Cannon II - Hits");
        store.Commit(new("Gamelogs/sample.txt", new("sample", 0, 4, "utf-8", ""), new("Pilot One")), [new(4, unknownPlayer)]);
        var before = Assert.Single(store.Snapshot(Now.AddSeconds(1), 10, "", "", new Dictionary<string, long?>()).Characters);
        Assert.False(before.Categories.Single(x => x.Kind == CombatantKind.Npc).HasUnresolvedIncomingDamage);
        Assert.True(before.Categories.Single(x => x.Kind == CombatantKind.Player).HasUnresolvedIncomingDamage);
        store.RefreshDamageMetadata(Now.AddSeconds(1), 10, entry => entry.Kind == CombatantKind.Player
            ? entry with { DamageTypes = DamageTypes.EM, DamageType = CombatDamageType.EM, StaticDataBuild = 42 } : entry);
        var refreshed = Assert.Single(store.Snapshot(Now.AddSeconds(1), 10, "", "", new Dictionary<string, long?>()).Characters);
        Assert.Equal(before.Categories.Sum(x => x.Total.Incoming), refreshed.Categories.Sum(x => x.Total.Incoming));
        Assert.Equal(DamageTypes.EM, refreshed.Categories.Single(x => x.Kind == CombatantKind.Player).IncomingDamageTypes);
        Assert.Equal(DamageTypes.None, Assert.Single(store.Snapshot(Now.AddSeconds(11), 10, "", "", new Dictionary<string, long?>()).Characters).IncomingDamageTypes);
    }

    [Fact]
    public async Task DelayedIncomingLogStartsTheFullIndicatorAtReceiptAndRepairsCannotRetriggerIt()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string games = Path.Combine(folder.Path, "Gamelogs"); Directory.CreateDirectory(games);
        string path = Path.Combine(games, "live.txt"); File.WriteAllText(path, Header());
        var preferences = new ApplicationPreferences(Path.Combine(folder.Path, "prefs.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, Directory = folder.Path });
        var now = Now.AddSeconds(-10);
        var service = new CombatLogService(preferences, null, logger, Path.Combine(folder.Path, "logs.db"), Catalog(), () => now);
        try
        {
            await WaitFor(service, x => x.Characters.Any(c => c.GameFiles > 0));
            now = Now;
            File.AppendAllText(path, Hit().Replace("12:00:00", "11:59:55"));
            await WaitFor(service, x => x.Characters.Single().LastIncomingDamageAt == Now);
            Assert.Equal(Now, service.ReadLogs().Characters.Single().IncomingDamageStartedAt);
            Assert.Null(service.ReadLogs().Characters.Single().LastPlayerDamageAt);
            Assert.Equal(100, service.ReadLogs().Characters.Single().Categories.Sum(x => x.Total.Incoming));
            now = Now.AddSeconds(1);
            File.AppendAllText(path, "[ 2026.09.13 12:00:01 ] (combat) 500 remote shield boosted by Other Pilot\r\n");
            await WaitFor(service, x => x.RecentEntries.Any(e => e.Effect == CombatEffect.ShieldRepair));
            Assert.Equal(Now, service.ReadLogs().Characters.Single().LastIncomingDamageAt);
            Assert.Null(service.ReadLogs().Characters.Single().LastPlayerDamageAt);
            now = Now.AddMilliseconds(1300);
            File.AppendAllText(path, Hit("Other Pilot", 150).Replace("12:00:00", "12:00:01"));
            await WaitFor(service, x => x.Characters.Single().LastPlayerDamageAt == now);
            var repeated = service.ReadLogs().Characters.Single();
            Assert.Equal(Now, repeated.IncomingDamageStartedAt);
            Assert.Equal(now, repeated.PlayerDamageStartedAt);
            Assert.False(CombatDamageFlash.Evaluate(repeated, preferences.CombatLogs with { FlashAnimation = DamageFlashAnimation.Blink }, now).Highlighted);
            Assert.True(CombatDamageFlash.Evaluate(repeated, preferences.CombatLogs with { FlashAnimation = DamageFlashAnimation.Blink, FlashPlayerOnly = true }, now).Highlighted);
            now = Now.AddSeconds(4);
            File.AppendAllText(path, Hit().Replace("12:00:00", "12:00:04"));
            await WaitFor(service, x => x.Characters.Single().LastIncomingDamageAt == now);
            Assert.Equal(now, service.ReadLogs().Characters.Single().IncomingDamageStartedAt);
        }
        finally { service.Dispose(); await service.Completion; }
    }

    [Fact]
    public async Task RepairOnlyRatesExpireOnTheServiceTimerAndScopedResetReachesTheStore()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string games = Path.Combine(folder.Path, "Gamelogs"); Directory.CreateDirectory(games);
        string path = Path.Combine(games, "live.txt"); File.WriteAllText(path, Header());
        var preferences = new ApplicationPreferences(Path.Combine(folder.Path, "prefs.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, Directory = folder.Path });
        var now = Now.AddSeconds(-10);
        var service = new CombatLogService(preferences, null, logger, Path.Combine(folder.Path, "logs.db"), Catalog(), () => now);
        static double Rate(CombatLogSnapshot value) => value.Characters.Sum(c => c.RepairRates.Sum(x => x.PerSecond.Incoming));
        try
        {
            await WaitFor(service, x => x.Characters.Any(c => c.GameFiles > 0));
            now = Now;
            File.AppendAllText(path, "[ 2026.09.13 12:00:00 ] (combat) 500 remote shield boosted by Other Pilot\r\n");
            await WaitFor(service, x => Rate(x) == 50);
            now = Now.AddSeconds(11);
            await WaitFor(service, x => Rate(x) == 0); // No new event/command to provoke refresh.
            File.AppendAllText(path, Hit().Replace("12:00:00", "12:00:11")
                + "[ 2026.09.13 12:00:11 ] (combat) 500 remote shield boosted by Other Pilot\r\n");
            await WaitFor(service, x => Rate(x) == 50 && Total(x) > 0);
            double damage = Total(service.ReadLogs());
            Assert.True((await service.ResetCombatAsync(CombatResetScope.Repairs)).Success);
            await WaitFor(service, x => Rate(x) == 0);
            Assert.Equal(damage, Total(service.ReadLogs()));
        }
        finally { service.Dispose(); await service.Completion; }
    }

    [Fact]
    public void NameBlinkUsesConfiguredPeriodAndExpiresWithoutLosingPhaseOnRepeatedDamage()
    {
        var settings = new CombatLogSettings { FlashAnimation = DamageFlashAnimation.Blink };
        var character = new CharacterCombatSnapshot("Pilot", null, null, null, null, null, [], 0)
            { IncomingDamageStartedAt = Now, LastIncomingDamageAt = Now };
        Assert.True(CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(249)).Highlighted);
        var off = CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(250));
        Assert.False(off.Highlighted); Assert.Equal(Now.AddMilliseconds(500), off.NextChangeAt);
        Assert.True(CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(500)).Highlighted);
        Assert.False(CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(1750)).Highlighted);
        Assert.Null(CombatDamageFlash.Evaluate(character, settings, Now.AddSeconds(2)).NextChangeAt);
        Assert.True(CombatDamageFlash.Evaluate(character, settings with { FlashIntervalMilliseconds = 1000 }, Now.AddMilliseconds(400)).Highlighted);
        Assert.False(CombatDamageFlash.Evaluate(character, settings with { FlashIntervalMilliseconds = 1000 }, Now.AddMilliseconds(500)).Highlighted);
        character = character with { LastIncomingDamageAt = Now.AddMilliseconds(300) };
        Assert.False(CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(300)).Highlighted);
        Assert.True(CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(2100)).Highlighted);
        Assert.Null(CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(2300)).NextChangeAt);
        Assert.Null(CombatDamageFlash.Evaluate(character, settings with { FlashIncomingDamage = false }, Now.AddMilliseconds(500)).NextChangeAt);
        Assert.Null(CombatDamageFlash.Evaluate(character, settings with { FlashPlayerOnly = true }, Now.AddMilliseconds(500)).NextChangeAt);
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(settings with { FlashIntervalMilliseconds = 99 }));
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(settings with { FlashIntervalMilliseconds = 2001 }));
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string path = Path.Combine(folder.Path, "prefs.json");
        var preferences = new ApplicationPreferences(path, logger);
        Assert.Equal(500, preferences.CombatLogs.FlashIntervalMilliseconds);
        Assert.Equal(DamageFlashTarget.Title, preferences.CombatLogs.FlashTarget);
        preferences.SetCombatLogs(settings with { FlashIntervalMilliseconds = 750, FlashTarget = DamageFlashTarget.Both });
        var restored = new ApplicationPreferences(path, logger).CombatLogs;
        Assert.Equal(750, restored.FlashIntervalMilliseconds);
        Assert.Equal(DamageFlashTarget.Both, restored.FlashTarget);
    }

    [Theory]
    [InlineData(DamageFlashTarget.Title, true, false)]
    [InlineData(DamageFlashTarget.Thumbnail, false, true)]
    [InlineData(DamageFlashTarget.Both, true, true)]
    public void DamageFlashTargetsShareOnePhaseAndRestoreAtExpiry(DamageFlashTarget target, bool title, bool thumbnail)
    {
        var settings = new CombatLogSettings { FlashTarget = target, FlashColor = "#FF2233", FlashAnimation = DamageFlashAnimation.Blink };
        var character = new CharacterCombatSnapshot("Pilot", null, null, null, null, null, [], 0)
            { IncomingDamageStartedAt = Now, LastIncomingDamageAt = Now };
        var on = CombatDamageFlash.Evaluate(character, settings, Now);
        Assert.Equal(title, on.TitleHighlighted);
        Assert.Equal(thumbnail ? 0x33FF2233u : (uint?)null, on.ThumbnailTint);
        Assert.Equal(Now.AddMilliseconds(250), on.NextChangeAt);
        var off = CombatDamageFlash.Evaluate(character, settings, Now.AddMilliseconds(250));
        Assert.False(off.TitleHighlighted); Assert.Null(off.ThumbnailTint);
        Assert.Equal(Now.AddMilliseconds(500), off.NextChangeAt);
        var expired = CombatDamageFlash.Evaluate(character, settings, Now.AddSeconds(2));
        Assert.False(expired.TitleHighlighted); Assert.Null(expired.ThumbnailTint); Assert.Null(expired.NextChangeAt);
        Assert.Null(CombatDamageFlash.Evaluate(character, settings with { FlashPlayerOnly = true }, Now).ThumbnailTint);
        Assert.Null(CombatDamageFlash.Evaluate(character, settings with { FlashIncomingDamage = false }, Now).ThumbnailTint);
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(settings with { FlashTarget = (DamageFlashTarget)999 }));
    }

    [Fact]
    public void SimulationProducesNormalVariedEventsAndExpires()
    {
        var request = new CombatSimulation("EVE - Pilot One", DamageDirection.Incoming, 1250, CombatDamageType.EM, WeaponPlatform.Rocket)
            { DurationSeconds = 60, Randomize = true };
        var sequence = new CombatSimulationSequence(request, Now, 3, new Random(42));
        var entries = new List<ParsedLogEntry>();
        for (int second = 0; second < 60; second++)
        {
            var time = Now.AddSeconds(second); sequence.Advance(time);
            entries.AddRange(sequence.DrainEvents());
        }
        Assert.All(entries, entry => Assert.Equal("combat", entry.Category));
        Assert.Contains(entries, entry => entry.Direction == DamageDirection.Incoming);
        Assert.Contains(entries, entry => entry.Direction == DamageDirection.Outgoing);
        Assert.Contains(entries, entry => entry.Effect == CombatEffect.ShieldRepair);
        Assert.Contains(entries, entry => entry.Effect == CombatEffect.ArmorRepair);
        foreach (var direction in new[] { DamageDirection.Incoming, DamageDirection.Outgoing })
            foreach (var effect in new[] { CombatEffect.ShieldRepair, CombatEffect.ArmorRepair })
                Assert.Contains(entries, entry => entry.Effect == effect && entry.Direction == direction && entry.Timestamp < Now.AddSeconds(20));
        Assert.True(entries.Select(x => x.Platform).Distinct().Count() > 4);
        Assert.True(entries.Select(x => x.Amount).Distinct().Count() > 4);
        sequence.Advance(Now.AddMinutes(2)); Assert.Empty(sequence.DrainEvents());
        foreach (var entry in entries)
            Assert.Equal(CombatOverlayFormatter.Event(new(entry), new()), CombatOverlayFormatter.Event(new(entry, true), new()));
        var selected = new CombatSimulationSequence(request with { Randomize = false }, Now, 3, new Random(42));
        selected.Advance(Now.AddSeconds(2));
        Assert.All(selected.DrainEvents(), entry => { Assert.Equal(CombatDamageType.EM, entry.DamageType); Assert.Equal(WeaponPlatform.Rocket, entry.Platform); });
    }

    [Theory]
    [InlineData(CombatEffect.ShieldRepair)]
    [InlineData(CombatEffect.ArmorRepair)]
    [InlineData(CombatEffect.HullRepair)]
    public void SimultaneousRepairsParseAndKeepIndependentVisibleRows(CombatEffect effect)
    {
        var request = new CombatSimulation("EVE - Pilot One", DamageDirection.Outgoing, 800, CombatDamageType.Unknown, WeaponPlatform.Unknown, effect)
            { BothRepairDirections = true, DurationSeconds = 10 };
        var sequence = new CombatSimulationSequence(request, Now, 3, new Random(42));
        var entries = sequence.DrainEvents();
        Assert.Equal(2, entries.Count);
        Assert.Equal(new DamageDirection?[] { DamageDirection.Incoming, DamageDirection.Outgoing }, entries.Select(x => x.Direction));
        var parsed = entries.Select(entry => EveLogParser.Parse("[ 2026.09.13 12:00:00 ] (combat) " + entry.Text,
            new("Pilot One"), false, Catalog(), new HashSet<string> { "Orion Voss" })!).ToArray();
        Assert.All(parsed, entry => { Assert.Equal(effect, entry.Effect); Assert.Equal(CombatantKind.Player, entry.Kind); });
        Assert.Equal(entries.Select(x => (x.Direction, x.Amount)), parsed.Select(x => (x.Direction, x.Amount)));
        var incoming = new CombatOverlayEvent(parsed[0], true); var outgoing = new CombatOverlayEvent(parsed[1], true);
        foreach (var order in Enum.GetValues<CombatRowOrder>())
        {
            var options = new LogOverlayOptions { Repairs = true, RowOrder = order };
            var rows = CombatOverlayFormatter.SimulationEvents([incoming, outgoing], options, 10);
            Assert.Equal(2, rows.Count(x => x.Visible));
            Assert.Equal(new[] { "IN", "OUT" }, rows.Where(x => x.Visible).Select(x => x.Label));
            Assert.All(rows.Where(x => x.Visible), x => { Assert.Equal("", x.Value); Assert.Equal(x.Suffix.Color, x.Suffix.IconColor); });
            var remaining = CombatOverlayFormatter.SimulationEvents([outgoing], options, 10);
            Assert.Equal(rows.ToList().FindLastIndex(x => x.Visible), remaining.ToList().FindIndex(x => x.Visible));
            Assert.Single(remaining, x => x.Visible);
            Assert.All(CombatOverlayFormatter.SimulationEvents([incoming, outgoing], options with { Repairs = false }, 10), x => Assert.False(x.Visible));
        }
        sequence.Advance(Now.AddSeconds(3));
        Assert.Equal(2, sequence.DrainEvents().Count);
        sequence.Advance(Now.AddSeconds(10)); Assert.Empty(sequence.DrainEvents());
        var mixed = new CombatSimulationSequence(request with { Randomize = true, Effect = CombatEffect.Damage }, Now, 3, new Random(42));
        mixed.DrainEvents(); mixed.Advance(Now.AddSeconds(1));
        Assert.Equal(new DamageDirection?[] { DamageDirection.Incoming, DamageDirection.Outgoing }, mixed.DrainEvents()
            .Where(x => x.Effect != CombatEffect.Damage).Select(x => x.Direction));
    }

    [Fact]
    public void CurrentSystemUsesNewestKnowledgeAndKeepsCharactersSeparate()
    {
        var systems = new CharacterSystemCache();
        systems.Observe("Pilot One", "Jita", Now);
        systems.Observe("Pilot Two", "Amarr", Now);
        systems.Observe("Pilot One", "Dodixie", Now.AddSeconds(1));
        systems.Observe("Pilot One", "Jita", Now); // Replay from an older file.
        Assert.Equal("Dodixie", systems.GetSystem("PILOT ONE"));
        Assert.Equal("Amarr", systems.GetSystem("Pilot Two"));
        Assert.Null(systems.GetSystem("New Pilot"));
        Assert.True(new LogOverlayOptions().SolarSystem);
    }

    [Fact]
    public void AutomaticFolderUsesRedirectedDocumentsThenConventionalFallback()
    {
        using var folder = new TempFolder();
        string redirected = Path.Combine(folder.Path, "redirected"), profile = Path.Combine(folder.Path, "profile");
        string preferred = Path.Combine(redirected, "EVE", "logs"), fallback = Path.Combine(profile, "Documents", "EVE", "logs");
        Assert.Equal(preferred, EveLogDirectory.Detect(redirected, profile));
        Directory.CreateDirectory(Path.Combine(fallback, "Gamelogs"));
        Assert.Equal(fallback, EveLogDirectory.Detect(redirected, profile));
        Directory.CreateDirectory(Path.Combine(preferred, "Chatlogs"));
        Assert.Equal(preferred, EveLogDirectory.Detect(redirected, profile));
        Assert.Equal(Path.Combine(folder.Path, "custom"), EveLogDirectory.Resolve(Path.Combine(folder.Path, "custom")));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8bom")]
    [InlineData("utf16")]
    [InlineData("utf16be")]
    public void EmitsOnlyWholeUnicodeLinesAcrossEveryByteBoundary(string encodingName)
    {
        using var folder = new TempFolder();
        Encoding encoding = encodingName switch { "utf8bom" => new UTF8Encoding(true), "utf16" => new UnicodeEncoding(false, true),
            "utf16be" => new UnicodeEncoding(true, true), _ => new UTF8Encoding(false) };
        string path = Path.Combine(folder.Path, "log.txt");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes("Piłot 🚀\r\nsecond\nunfinished")).ToArray();
        var lines = new List<string>(); LogCursor cursor = null;
        foreach (byte value in bytes)
        {
            writer.WriteByte(value); writer.Flush();
            using var reader = CompleteLogReader.OpenShared(path);
            var batch = CompleteLogReader.Read(reader, CompleteLogReader.Identity(reader), cursor);
            cursor = batch.Cursor; lines.AddRange(batch.Lines.Select(x => x.Text));
            Assert.DoesNotContain(lines, x => x != "Piłot 🚀" && x != "second");
        }
        Assert.Equal(new[] { "Piłot 🚀", "second" }, lines);
        writer.Write(encoding.GetBytes("\n")); writer.Flush();
        using var lastReader = CompleteLogReader.OpenShared(path);
        var last = CompleteLogReader.Read(lastReader, CompleteLogReader.Identity(lastReader), cursor);
        Assert.Equal("unfinished", Assert.Single(last.Lines).Text);
    }

    [Fact]
    public void SharedReaderPermitsWriterAndRenameAndDetectsTruncation()
    {
        using var folder = new TempFolder(); string path = System.IO.Path.Combine(folder.Path, "log.txt"), moved = path + ".moved";
        File.WriteAllText(path, "first longer line\n", new UTF8Encoding(false));
        LogCursor cursor;
        using (var read = CompleteLogReader.OpenShared(path))
        {
            cursor = CompleteLogReader.Read(read, CompleteLogReader.Identity(read), null).Cursor;
            using (var write = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                write.Write(Encoding.UTF8.GetBytes("second\n"));
            File.Move(path, moved);
        }
        using (var read = CompleteLogReader.OpenShared(moved))
        {
            Assert.Equal(cursor.Identity, CompleteLogReader.Identity(read));
            var batch = CompleteLogReader.Read(read, cursor.Identity, cursor);
            Assert.Equal("second", Assert.Single(batch.Lines).Text); cursor = batch.Cursor;
        }
        File.WriteAllText(moved, "new\n", new UTF8Encoding(false));
        using var replacement = CompleteLogReader.OpenShared(moved);
        var restarted = CompleteLogReader.Read(replacement, CompleteLogReader.Identity(replacement), cursor);
        Assert.True(restarted.Cursor.Generation > cursor.Generation);
        Assert.Equal("new", Assert.Single(restarted.Lines).Text);
    }

    [Fact]
    public void OversizedUnterminatedLinesNeverEmitTheirSuffixAsAnEntry()
    {
        using var folder = new TempFolder(); string path = System.IO.Path.Combine(folder.Path, "log.txt");
        File.WriteAllText(path, new string('x', CompleteLogReader.MaximumLineBytes + 1));
        LogCursor cursor;
        using (var reader = CompleteLogReader.OpenShared(path))
        { var batch = CompleteLogReader.Read(reader, CompleteLogReader.Identity(reader), null); Assert.Empty(batch.Lines); Assert.Equal(1, batch.OversizedLines); cursor = batch.Cursor; }
        File.AppendAllText(path, "suffix\nvalid\n");
        using var next = CompleteLogReader.OpenShared(path);
        Assert.Equal("valid", Assert.Single(CompleteLogReader.Read(next, CompleteLogReader.Identity(next), cursor).Lines).Text);
    }

    [Theory]
    [InlineData("Guristas Despoiler", CombatantKind.Npc)]
    [InlineData("Pilot Two[CORP](Rifter)", CombatantKind.Player)]
    [InlineData("Known Pilot", CombatantKind.Player)]
    [InlineData("Custom ship label", CombatantKind.Player)]
    public void ParserUsesBinaryNpcPlayerRuleAndRecognizesWeapons(string peer, CombatantKind expected)
    {
        var entry = EveLogParser.Parse(Hit(peer).TrimEnd(), new("Pilot One"), false, Catalog(), new HashSet<string> { "Known Pilot" });
        Assert.NotNull(entry); Assert.Equal(100, entry.Amount); Assert.Equal(DamageDirection.Incoming, entry.Direction);
        Assert.Equal(expected, entry.Kind); Assert.Equal(WeaponPlatform.Rocket, entry.Platform); Assert.Equal(CombatDamageType.EM, entry.DamageType);
        var blaster = EveLogParser.Parse(Hit(peer).Replace("Mjolnir Rocket", "Light Neutron Blaster II").TrimEnd(), new("Pilot One"), false, Catalog(), new HashSet<string>());
        Assert.Equal(WeaponPlatform.Blaster, blaster.Platform); Assert.Equal(CombatDamageType.Unknown, blaster.DamageType);
    }

    [Theory]
    [InlineData("shield boosted", CombatEffect.ShieldRepair)]
    [InlineData("armor repaired", CombatEffect.ArmorRepair)]
    [InlineData("hull repaired", CombatEffect.HullRepair)]
    public void RepairsAreTypedAndNotAddedToDamageTotals(string wording, CombatEffect effect)
    {
        var entry = EveLogParser.Parse($"[ 2026.09.13 12:00:00 ] (combat) <b>900</b> remote {wording} by <b>Pilot Two</b>", new("Pilot One"), false, Catalog(), new HashSet<string>());
        Assert.NotNull(entry); Assert.Equal(effect, entry.Effect); Assert.Equal(DamageDirection.Incoming, entry.Direction);
        using var folder = new TempFolder(); using var store = new CombatLogStore(System.IO.Path.Combine(folder.Path, "combat.db"), Now);
        store.Commit(new("path", new("source", 0, 100, "utf-8", ""), new("Pilot One")), [new(0, entry)]);
        var snapshot = store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>());
        Assert.Equal(0, snapshot.Characters.Single().Categories.Sum(x => x.Total.Incoming));
        Assert.Equal(effect, snapshot.RecentEntries.Single().Effect);
    }

    [Fact]
    public void LocalMessagesRejectSpoofedChatAndOutOfOrderLocationsDoNotRegress()
    {
        var header = new LogHeader("Pilot One", "Local");
        string line = "[ 2026.09.13 12:00:00 ] EVE System > Channel changed to Local : Jita";
        Assert.Null(EveLogParser.Parse(line.Replace("EVE System >", "Some Pilot > EVE System >"), header, true, Catalog(), new HashSet<string>()));
        Assert.Null(EveLogParser.Parse(line, header with { Channel = "Corp" }, true, Catalog(), new HashSet<string>()));
        Assert.Null(EveLogParser.Parse(line, header with { Ambiguous = true }, true, Catalog(), new HashSet<string>()));
        var entry = EveLogParser.Parse(line, header, true, Catalog(), new HashSet<string>());
        using var folder = new TempFolder(); using var store = new CombatLogStore(System.IO.Path.Combine(folder.Path, "combat.db"), Now);
        store.Commit(new("path", new("source", 0, 100, "utf-8", ""), header), [new(0, entry)]);
        store.Commit(new("path", new("source", 0, 200, "utf-8", ""), header), [new(100, entry with { Timestamp = Now.AddMinutes(-1), SolarSystem = "Amarr", SolarSystemId = 30002187 })]);
        Assert.Equal("Jita", store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>()).Characters.Single().SolarSystem);
    }

    [Fact]
    public void CheckpointsDeduplicateReplayButPreserveIdenticalLegitimateHitsAndSurviveRestart()
    {
        using var folder = new TempFolder(); string path = System.IO.Path.Combine(folder.Path, "combat.db");
        var file = new StoredLogFile("log", new("file", 0, 300, "utf-8", ""), new("Pilot One"));
        var hit = new ParsedLogEntry(Now, "Pilot One", "combat", "hit", DamageDirection.Incoming, 100, Kind: CombatantKind.Npc);
        using (var store = new CombatLogStore(path, Now))
        {
            store.Commit(file, [new(0, hit), new(100, hit)]); store.Commit(file, [new(0, hit), new(100, hit)]);
            Assert.Equal(200, Total(store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>())));
        }
        using (var store = new CombatLogStore(path, Now.AddSeconds(2)))
        {
            Assert.Equal(300, store.ReadFile("file").Cursor.Offset);
            Assert.Equal(200, Total(store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>())));
            Assert.Equal(0, store.Snapshot(Now.AddSeconds(10), 10, "", "", new Dictionary<string, long?>()).Characters.Single().Categories.Sum(x => x.Dps.Incoming));
            store.Prune(Now.AddDays(8), 7);
            Assert.Empty(store.Snapshot(Now.AddDays(8), 10, "", "", new Dictionary<string, long?>()).RecentEntries);
            Assert.Equal(200, Total(store.Snapshot(Now.AddDays(8), 10, "", "", new Dictionary<string, long?>())));
            store.Commit(file with { Header = new("Pilot One", Ambiguous: true) }, []);
            Assert.Equal(0, Total(store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>())));
        }
    }

    [Fact]
    public void TransactionFailureRollsBackBothEntriesAndFilePosition()
    {
        using var folder = new TempFolder(); using var store = new CombatLogStore(System.IO.Path.Combine(folder.Path, "combat.db"), Now);
        var file = new StoredLogFile("log", new("file", 0, 0, "utf-8", ""), new("Pilot One")); store.Commit(file, []);
        var entry = new ParsedLogEntry(Now, "Pilot One", "combat", "hit", DamageDirection.Outgoing, 100);
        // A null cursor violates the source row after entries/totals have been inserted.
        // A null character entry is ignored by SQLite's OR IGNORE; force a source failure instead.
        Assert.ThrowsAny<Exception>(() => store.Commit(file with { Path = null, Cursor = file.Cursor with { Offset = 200 } }, [new(0, entry)]));
        Assert.Equal(0, store.ReadFile("file").Cursor.Offset);
        Assert.Empty(store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>()).RecentEntries);
    }

    [Fact]
    public void AppearancePersistsIndependentlyOfThemeAndRejectsInvalidColours()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string path = System.IO.Path.Combine(folder.Path, "settings.json");
        var preferences = new ApplicationPreferences(path, logger);
        var appearance = CombatAppearance.Preset("Classic") with { IncomingColor = "#FF0000" };
        var options = new LogOverlayOptions(true, true, true) { Advanced = true, CustomAppearance = appearance, Repairs = true, DamageEvents = true,
            SystemColor = "#55BBDD", SystemPlacement = SubtitlePlacement.Right, SystemFontSize = 19 };
        preferences.SetCombatLogs(new() { Enabled = true, DefaultOverlay = options, Overlays = new Dictionary<string, LogOverlayOptions> { ["EVE - Pilot One"] = options with { FontSize = 20 } } });
        preferences.SetTheme("Light");
        var loaded = new ApplicationPreferences(path, logger);
        Assert.True(loaded.CombatLogs.Enabled); Assert.Equal(20, loaded.CombatLogs.Overlays["EVE - Pilot One"].FontSize);
        Assert.Equal("#FF0000", loaded.CombatLogs.DefaultOverlay.GetAppearance().IncomingColor);
        Assert.Equal("#55BBDD", loaded.CombatLogs.DefaultOverlay.GetAppearance().SystemColor);
        Assert.Equal(SubtitlePlacement.Right, loaded.CombatLogs.DefaultOverlay.SystemPlacement);
        Assert.Equal(19f, loaded.CombatLogs.DefaultOverlay.SystemFontSize);
        Assert.Throws<ArgumentException>(() => loaded.SetCombatLogs(loaded.CombatLogs with { DefaultOverlay = options with { SystemFontSize = float.NaN } }));
        Assert.Equal("#55BBDD", (loaded.CombatLogs.DefaultOverlay with { Advanced = false, Preset = "Minimal" }).GetAppearance().SystemColor);
        Assert.Throws<ArgumentException>(() => loaded.SetCombatLogs(loaded.CombatLogs with { DefaultOverlay = options with { SystemColor = "invalid" } }));
        var invalid = loaded.CombatLogs with { DefaultOverlay = options with { CustomAppearance = appearance with { IncomingColor = "invalid" } } };
        Assert.Throws<ArgumentException>(() => loaded.SetCombatLogs(invalid));
        var notification = new CombatOverlayEvent(new(Now, "Pilot One", "combat", "", DamageDirection.Incoming, 1250,
            Platform: WeaponPlatform.Rocket, DamageType: CombatDamageType.EM), true);
        var formatted = CombatOverlayFormatter.Event(notification, options);
        Assert.Equal(OverlaySymbol.EveEM, CombatOverlayFormatter.Event(notification, new() { Preset = "Minimal" }).Icon);
        Assert.Empty(CombatOverlayFormatter.Event(notification, options with { CustomAppearance = appearance with { ShowDamageIcon = false, ShowWeaponIcon = false } }).Icons());
        Assert.Equal(0xFFFF0000u, formatted.Color); Assert.Equal(OverlaySymbol.EveEM, formatted.Icon); Assert.Equal(OverlaySymbol.Rocket, formatted.SecondaryIcon);
        Assert.NotEqual(formatted.Color, formatted.IconColor); Assert.Equal("α 1,250", formatted.Text);
        var rep = CombatOverlayFormatter.Event(notification with { Entry = notification.Entry with { Effect = CombatEffect.ArmorRepair } }, options);
        Assert.Equal(OverlaySymbol.Armor, rep.Icon); Assert.Equal(OverlaySymbol.None, rep.SecondaryIcon); Assert.Equal(0xFFD7A16Bu, rep.Color);
        Assert.Single(CombatOverlayFormatter.Simulation(notification, options with { DamageEvents = false }, 10), x => x.Label.Contains("DPS"));
        Assert.Single(CombatOverlayFormatter.Simulation(notification, options with { Incoming = false, Outgoing = false }, 10), x => x.Suffix?.Text == "α 1,250");
        Assert.Single(CombatOverlayFormatter.Simulation(notification, options, 10), x => x.Visible);
        Assert.Empty(CombatOverlayFormatter.Simulation(notification, options with { DamageEvents = false, Incoming = false, Outgoing = false, Repairs = false }, 10));
    }

    [Fact]
    public void SimpleVisibilityPersistsWithoutReplacingAdvancedStyles()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string path = System.IO.Path.Combine(folder.Path, "settings.json");
        var appearance = CombatAppearance.Preset("Classic") with { IncomingColor = "#123456", ShowWeaponIcon = false,
            Weapons = new Dictionary<WeaponPlatform, CombatVisualStyle> { [WeaponPlatform.Rocket] = new("#ABCDEF", OverlaySymbol.Railgun) } };
        var options = new LogOverlayOptions { DamageEvents = false, CustomAppearance = appearance };
        new ApplicationPreferences(path, logger).SetCombatLogs(new() { DefaultOverlay = options,
            Overlays = new Dictionary<string, LogOverlayOptions> { ["EVE - Pilot One"] = options with { DamageEvents = true } } });
        var loaded = new ApplicationPreferences(path, logger).CombatLogs;
        Assert.False(loaded.DefaultOverlay.Advanced);
        Assert.False(loaded.DefaultOverlay.DamageEvents);
        Assert.True(loaded.DefaultOverlay.Incoming && loaded.DefaultOverlay.Outgoing);
        Assert.False(loaded.DefaultOverlay.GetAppearance().ShowWeaponIcon);
        Assert.Equal(CombatAppearance.Preset("Classic").IncomingColor, loaded.DefaultOverlay.GetAppearance().IncomingColor);
        var client = loaded.Overlays["EVE - Pilot One"];
        Assert.True(client.DamageEvents);
        var hit = new CombatOverlayEvent(new(Now, "Pilot One", "combat", "", DamageDirection.Incoming, 1250, Platform: WeaponPlatform.Rocket));
        Assert.Equal(OverlaySymbol.None, CombatOverlayFormatter.Event(hit, client).SecondaryIcon);
        var advanced = (client with { Advanced = true }).GetAppearance();
        Assert.False(advanced.ShowWeaponIcon);
        Assert.Equal("#123456", advanced.IncomingColor);
        Assert.Equal(appearance.Weapons[WeaponPlatform.Rocket], advanced.WeaponStyle(WeaponPlatform.Rocket));
        Assert.False(new LogOverlayOptions { Preset = "Minimal" }.GetAppearance().ShowWeaponIcon);
    }

    [Fact]
    public void MeterKeepsDirectionSlotsAndAppendsIndependentAlphaWithoutDpsIcons()
    {
        var options = new LogOverlayOptions { Repairs = true, Advanced = true,
            CustomAppearance = CombatAppearance.Preset("Classic") with { TextColorMode = CombatTextColorMode.DamageType } };
        var incoming = new CombatOverlayEvent(new(Now, "Pilot One", "combat", "", DamageDirection.Incoming, 1250,
            Platform: WeaponPlatform.Rocket, DamageType: CombatDamageType.EM));
        var outgoing = new CombatOverlayEvent(incoming.Entry with { Direction = DamageDirection.Outgoing, Amount = 987, DamageType = CombatDamageType.Thermal });
        var snapshot = new CharacterCombatSnapshot("Pilot One", null, null, null, null, Now,
            [new(CombatantKind.Npc, new(0, 0), new(125, 98.7))], 1, 1);
        var both = CombatOverlayFormatter.Meter(snapshot, options, incoming, outgoing);
        Assert.Equal(4, both.Count);
        Assert.Equal("In DPS: 125", both[0].Text); Assert.Equal("α 1,250", both[0].Suffix.Text);
        Assert.Equal("Out DPS: 99", both[1].Text); Assert.Equal("α 987", both[1].Suffix.Text);
        Assert.All(both, row => Assert.Empty(row.Icons()));
        Assert.Equal(OverlaySymbol.EveEM, both[0].Suffix.Icon); Assert.Equal(OverlaySymbol.EveThermal, both[1].Suffix.Icon);
        Assert.Equal(CombatOverlayFormatter.Color(options.GetAppearance().IncomingColor), both[0].Color);
        Assert.NotEqual(both[0].Color, both[0].Suffix.Color); Assert.False(both[2].Visible);

        var outOnly = CombatOverlayFormatter.Meter(snapshot with { Categories = [new(CombatantKind.Npc, new(0, 0), new(0, 98.7))] }, options, outgoing: outgoing);
        Assert.False(outOnly[0].Visible); Assert.Equal(both[1], outOnly[1]);
        var alphaOnly = CombatOverlayFormatter.Meter(snapshot, options with { Incoming = false, Outgoing = false }, incoming, outgoing);
        Assert.Equal("In", alphaOnly[0].Text); Assert.Equal("Out", alphaOnly[1].Text);
        Assert.Equal(both[0].Suffix, alphaOnly[0].Suffix); Assert.Equal(both[1].Suffix, alphaOnly[1].Suffix);
        Assert.All(CombatOverlayFormatter.Meter(null, options), row => Assert.False(row.Visible));
        var expiredAlpha = CombatOverlayFormatter.Meter(snapshot, options);
        Assert.Equal("In DPS: 125", expiredAlpha[0].Text); Assert.Null(expiredAlpha[0].Suffix);

        foreach (bool top in new[] { false, true })
        {
            var scene = new OverlayScene { ShowTitle = false, Stats = both, StatsStyle = new(18, 8, 8, top) };
            using var all = EveOPreview.View.Rendering.OverlaySceneRasterizer.Render(scene, new(384, 216));
            using var reduced = EveOPreview.View.Rendering.OverlaySceneRasterizer.Render(scene with { Stats = outOnly }, new(384, 216));
            int y = scene.StatsStyle.StartY(216, both.Count) + scene.StatsStyle.LineHeight;
            int ink = 0;
            for (int row = y; row < y + scene.StatsStyle.LineHeight; row++) for (int x = 0; x < 384; x++)
            {
                Assert.Equal(all.GetPixel(x, row), reduced.GetPixel(x, row));
                if (reduced.GetPixel(x, row).A > 0) ink++;
            }
            Assert.True(ink > 50, "Outgoing text must stay in exactly the same pixel position with incoming hidden.");
            string output = Path.Combine(AppContext.BaseDirectory, "artifacts"); Directory.CreateDirectory(output);
            all.Save(Path.Combine(output, $"inline-alpha-{top}.png"));
            reduced.Save(Path.Combine(output, $"outgoing-only-{top}.png"));
        }
    }

    [Fact]
    public void DpsInheritsTitleFontAndCanOverrideAndRestoreIt()
    {
        var title = new OverlayScene { Title = "Same text", Font = new("Consolas", 18, OverlayFontStyle.Bold | OverlayFontStyle.Italic,
            0xFFFFFFFF, 0xFF000000, 2, 8, 8) };
        var options = new LogOverlayOptions { FontSize = 18, Top = true };
        var meter = title with { ShowTitle = false, Stats = [new("", title.Title)], StatsStyle = options.GetStatsStyle() };
        byte[] Pixels(OverlayScene scene)
        {
            using var bitmap = EveOPreview.View.Rendering.OverlaySceneRasterizer.Render(scene, new(384, 216));
            using var stream = new MemoryStream(); bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); return stream.ToArray();
        }
        Assert.Equal(Pixels(title), Pixels(meter));
        var custom = options with { FontFamily = "Arial", FontStyle = OverlayFontStyle.Regular };
        Assert.False(Pixels(meter).SequenceEqual(Pixels(meter with { StatsStyle = custom.GetStatsStyle() })));
        Assert.Equal(Pixels(meter), Pixels(meter with { StatsStyle = (custom with { FontFamily = null, FontStyle = null }).GetStatsStyle() }));
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string path = Path.Combine(folder.Path, "prefs.json");
        var preferences = new ApplicationPreferences(path, logger);
        preferences.SetCombatLogs(new() { DefaultOverlay = custom });
        Assert.Equal(custom, new ApplicationPreferences(path, logger).CombatLogs.DefaultOverlay);
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(new() { DefaultOverlay = options with { FontStyle = (OverlayFontStyle)32 } }));
    }

    [Fact]
    public void EmbeddedDamageSvgsRenderAtThumbnailSizeAndAcceptCustomColours()
    {
        var appearance = CombatAppearance.Preset("Classic");
        var rows = new List<OverlayStat>();
        foreach (var type in new[] { CombatDamageType.EM, CombatDamageType.Thermal, CombatDamageType.Kinetic, CombatDamageType.Explosive })
        {
            var style = appearance.Damage[type];
            using var bitmap = EveOPreview.View.Rendering.OverlaySceneRasterizer.Render(new OverlayScene
            {
                ShowTitle = false, StatsStyle = new(14, 3, 3, true),
                Stats = [new("", "", 0xFFFFFFFF, style.Icon, 0xFF00FF00)]
            }, new PreviewSize(32, 24));
            int green = 0;
            for (int y = 0; y < 20; y++) for (int x = 0; x < 20; x++)
            { var pixel = bitmap.GetPixel(x, y); if (pixel.G > 150 && pixel.R < 40 && pixel.B < 40) green++; }
            Assert.True(green > 5, "SVG must render with its configured colour at 14px: " + type);
            rows.Add(new(type.ToString(), "EVE-style SVG", 0xFFFFFFFF, style.Icon, CombatOverlayFormatter.Color(style.Color)));
        }
        string output = Path.Combine(AppContext.BaseDirectory, "artifacts"); Directory.CreateDirectory(output);
        using var gallery = EveOPreview.View.Rendering.OverlaySceneRasterizer.Render(new OverlayScene
            { ShowTitle = false, Stats = rows, StatsStyle = new(24, 8, 8, true) }, new PreviewSize(460, 130));
        gallery.Save(Path.Combine(output, "damage-svg-icons.png"), System.Drawing.Imaging.ImageFormat.Png);
        using var narrow = EveOPreview.View.Rendering.OverlaySceneRasterizer.Render(new OverlayScene
            { ShowTitle = false, Stats = [new("Alpha", new string('W', 100))], StatsStyle = new(14, 3, 3, true) }, new PreviewSize(80, 100));
        for (int y = 25; y < narrow.Height; y++) for (int x = 0; x < narrow.Width; x++)
            Assert.Equal(0, narrow.GetPixel(x, y).A); // A long augment must not wrap over later rows.
    }

    [Fact]
    public async Task OpenLocalWriterPublishesSystemChangesWithoutCloseOrNotification()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string logs = Path.Combine(folder.Path, "logs");
        Directory.CreateDirectory(Path.Combine(logs, "Gamelogs"));
        Directory.CreateDirectory(Path.Combine(logs, "Chatlogs"));
        string local = Path.Combine(logs, "Chatlogs", "Local_session.txt");
        File.WriteAllText(local, "Channel Name: Local\r\nListener: Pilot One\r\n[ 2026.09.13 12:00:00 ] EVE System > Channel changed to Local : Jita\r\n", Encoding.Unicode);
        var preferences = new ApplicationPreferences(Path.Combine(folder.Path, "settings.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, Directory = logs });
        var service = new CombatLogService(preferences, null, logger, Path.Combine(folder.Path, "combat.db"), Catalog(), () => Now.AddSeconds(10));
        try
        {
            await WaitFor(service, x => x.Characters.Any(c => c.SolarSystem == "Jita"));
            // Windows may delay size/write notifications until an open writer closes.
            // Suppress notifications explicitly so the regression is deterministic.
            var watchers = (List<FileSystemWatcher>)typeof(CombatLogService).GetField("_watchers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(service)!;
            foreach (var watcher in watchers) watcher.EnableRaisingEvents = false;
            using var writer = new FileStream(local, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            writer.Write(Encoding.Unicode.GetBytes("\uFEFF[ 2026.09.13 12:00:01 ] EVE System > Channel changed to Local : Amarr"));
            writer.Flush();
            await Task.Delay(700, TestContext.Current.CancellationToken);
            Assert.Equal("Jita", service.CurrentSystems.GetSystem("Pilot One"));
            long partialReads = service.Diagnostics.Reads;
            await Task.Delay(700, TestContext.Current.CancellationToken);
            Assert.Equal(partialReads, service.Diagnostics.Reads); // Unchanged partial bytes are not reread.
            writer.Write(Encoding.Unicode.GetBytes("\r\n")); writer.Flush();
            await WaitFor(service, x => x.Characters.Any(c => c.SolarSystem == "Amarr"));
            Assert.Equal("Amarr", service.CurrentSystems.GetSystem("Pilot One"));
            writer.Write(Encoding.Unicode.GetBytes("\uFEFF[ 2026.09.13 12:00:02 ] EVE System > Channel changed to Local : Jita\r\n")); writer.Flush();
            await WaitFor(service, x => x.Characters.Any(c => c.SolarSystem == "Jita"));
            Assert.Equal("Jita", service.CurrentSystems.GetSystem("Pilot One"));
            Assert.Equal(1, service.Diagnostics.Reconciliations);
            service.Stop();
            await WaitFor(service, x => x.Status == "Stopped");
            long reads = service.Diagnostics.Reads;
            writer.Write(Encoding.Unicode.GetBytes("\uFEFF[ 2026.09.13 12:00:03 ] EVE System > Channel changed to Local : Amarr\r\n")); writer.Flush();
            await Task.Delay(700, TestContext.Current.CancellationToken);
            Assert.Equal(reads, service.Diagnostics.Reads);
            Assert.Equal("Jita", service.CurrentSystems.GetSystem("Pilot One"));
            service.Start();
            await WaitFor(service, x => x.Characters.Any(c => c.SolarSystem == "Amarr"));
        }
        finally { service.Dispose(); await service.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
    }

    [Fact]
    public async Task WatcherFollowsConcurrentFilesPartialWritesRotationAndRestartWithoutPolling()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string logs = System.IO.Path.Combine(folder.Path, "logs"); Directory.CreateDirectory(System.IO.Path.Combine(logs, "Gamelogs")); Directory.CreateDirectory(System.IO.Path.Combine(logs, "Chatlogs"));
        var preferences = new ApplicationPreferences(System.IO.Path.Combine(folder.Path, "settings.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, Directory = logs });
        string database = System.IO.Path.Combine(folder.Path, "combat.db");
        long time = Now.ToUnixTimeSeconds();
        DateTimeOffset Clock() => DateTimeOffset.FromUnixTimeSeconds(Interlocked.Read(ref time));
        var service = new CombatLogService(preferences, null, logger, database, Catalog(), Clock);
        try
        {
            await WaitFor(service, x => x.Status.Contains("Watching"));
            string path = System.IO.Path.Combine(logs, "Gamelogs", "20260913_120000_1.txt");
            using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            writer.Write(Encoding.UTF8.GetBytes(Header() + Hit().TrimEnd())); writer.Flush(flushToDisk: true);
            await WaitFor(service, x => x.Characters.Any(c => c.Name == "Pilot One"));
            Assert.Equal(0, Total(service.ReadLogs()));
            writer.Write(Encoding.UTF8.GetBytes("\r\n")); writer.Flush(flushToDisk: true);
            await WaitFor(service, x => Total(x) == 100);
            string second = System.IO.Path.Combine(logs, "Gamelogs", "20260913_120001_1.txt");
            await File.WriteAllTextAsync(second, Header() + Hit(amount: 200));
            await WaitFor(service, x => Total(x) == 300);
            // Old file still receives a late entry after the replacement file exists.
            writer.Write(Encoding.UTF8.GetBytes(Hit(amount: 50))); writer.Flush(flushToDisk: true);
            await WaitFor(service, x => Total(x) == 350);
            string renamed = second.Replace("120001", "120002"); File.Move(second, renamed);
            await service.RescanLogsAsync();
            await Task.Delay(150); Assert.Equal(350, Total(service.ReadLogs()));
            string local = System.IO.Path.Combine(logs, "Chatlogs", "Local_20260913_120000.txt");
            await File.WriteAllTextAsync(local, "Channel Name: Local\r\nListener: Pilot One\r\n[ 2026.09.13 12:00:00 ] EVE System > Channel changed to Local : Jita\r\n", Encoding.Unicode);
            await WaitFor(service, x => x.Characters.Any(c => c.SolarSystem == "Jita"));
            Assert.Equal("Jita", service.CurrentSystems.GetSystem("Pilot One"));
            service.SimulationRequested += _ => Task.FromResult(CommandResult.Ok());
            Assert.True((await service.SimulateLogEventAsync(new("EVE - Pilot One", DamageDirection.Incoming, 9000, CombatDamageType.EM, WeaponPlatform.Rocket))).Success);
            Assert.Equal(350, Total(service.ReadLogs()));
            Interlocked.Add(ref time, 11);
            await WaitFor(service, x => x.Characters.All(c => c.Categories.All(k => k.Dps.Incoming == 0 && k.Dps.Outgoing == 0)));
            long reads = service.Diagnostics.Reads;
            await Task.Delay(400, TestContext.Current.CancellationToken);
            Assert.Equal(reads, service.Diagnostics.Reads); // Display decay never polls EVE files.
        }
        finally { service.Dispose(); await service.Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
        var restarted = new CombatLogService(preferences, null, logger, database, Catalog(), Clock);
        try
        { await WaitFor(restarted, x => Total(x) == 350 && x.Characters.Any(c => c.SolarSystem == "Jita")); await Task.Delay(100); Assert.Equal(350, Total(restarted.ReadLogs())); }
        finally { restarted.Dispose(); await restarted.Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task MissingFoldersAppearByNotificationAndExclusiveWritersAreLeftAlone()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string logs = System.IO.Path.Combine(folder.Path, "EVE", "logs");
        var preferences = new ApplicationPreferences(System.IO.Path.Combine(folder.Path, "settings.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, Directory = logs });
        var service = new CombatLogService(preferences, null, logger, System.IO.Path.Combine(folder.Path, "combat.db"), Catalog(), () => Now);
        try
        {
            await WaitFor(service, x => x.Status.Contains("missing"));
            Directory.CreateDirectory(System.IO.Path.Combine(logs, "Gamelogs")); Directory.CreateDirectory(System.IO.Path.Combine(logs, "Chatlogs"));
            await WaitFor(service, x => x.Status.Contains("Watching"));
            string path = System.IO.Path.Combine(logs, "Gamelogs", "locked.txt");
            using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                writer.Write(Encoding.UTF8.GetBytes(Header() + Hit())); writer.Flush(true);
                await Task.Delay(180, TestContext.Current.CancellationToken);
                Assert.Equal(0, Total(service.ReadLogs()));
                Assert.True(service.Diagnostics.Reads > 0);
                // The log consumer did not take ownership of or interfere with this writer.
                writer.Write(Encoding.UTF8.GetBytes(Hit(amount: 50))); writer.Flush(true);
            }
            await WaitFor(service, x => Total(x) == 150);
            long reconciliations = service.Diagnostics.Reconciliations;
            var watchers = (List<FileSystemWatcher>)typeof(CombatLogService).GetField("_watchers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(service)!;
            typeof(FileSystemWatcher).GetMethod("OnError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(watchers.First(x => x.Path.EndsWith("Gamelogs")), [new ErrorEventArgs(new InternalBufferOverflowException())]);
            await WaitFor(service, x => service.Diagnostics.Reconciliations > reconciliations);
            Assert.Equal(150, Total(service.ReadLogs()));
        }
        finally { service.Dispose(); await service.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
    }

    private static double Total(CombatLogSnapshot snapshot) => snapshot.Characters.Sum(x => x.Categories.Sum(c => c.Total.Incoming + c.Total.Outgoing));

    [Fact]
    public async Task SimulationUsesRealAggregationAndEventsButOnlyRealConcurrentDamageSurvives()
    {
        using var folder = new TempFolder(); using var logger = new LoggerConfiguration().CreateLogger();
        string root = Path.Combine(folder.Path, "logs"), database = Path.Combine(folder.Path, "combat.db");
        Directory.CreateDirectory(Path.Combine(root, "Gamelogs")); Directory.CreateDirectory(Path.Combine(root, "Chatlogs"));
        var preferences = new ApplicationPreferences(Path.Combine(folder.Path, "settings.json"), logger);
        preferences.SetCombatLogs(new() { Enabled = true, Directory = root });
        int elapsed = 0;
        var service = new CombatLogService(preferences, null, logger, database, Catalog(), () => Now.AddSeconds(Volatile.Read(ref elapsed)));
        var received = new System.Collections.Concurrent.ConcurrentQueue<CombatOverlayEvent>(); service.CombatEvent += received.Enqueue;
        try
        {
            await WaitFor(service, x => x.Status.Contains("Watching"));
            string log = Path.Combine(root, "Gamelogs", "session.txt");
            await File.WriteAllTextAsync(log, Header() + Hit("Actual Pilot", 100), TestContext.Current.CancellationToken);
            await WaitFor(service, x => Total(x) == 100);
            var request = new CombatSimulation("EVE - Pilot One", DamageDirection.Incoming, 1000, CombatDamageType.EM, WeaponPlatform.Rocket);
            Assert.True((await service.RunSimulationAsync(request, [request.FullTitle])).Success);
            Assert.Equal(1100, Total(service.ReadLogs()));
            Assert.Contains(received, x => x.Simulated && x.Entry.Amount == 1000);
            Assert.Equal(Now, service.ReadLogs().Characters.Single().LastPlayerDamageAt);
            await File.AppendAllTextAsync(log, Hit(amount: 200), TestContext.Current.CancellationToken);
            await WaitFor(service, x => Total(x) == 1300);
            // The on-disk database must already exclude simulation while it is active.
            using (var db = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                { DataSource = database, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            {
                db.Open(); using var command = db.CreateCommand(); command.CommandText = "SELECT SUM(amount) FROM totals";
                Assert.Equal(300d, Convert.ToDouble(command.ExecuteScalar()));
            }
            Assert.True((await service.RunSimulationAsync(request with { Stop = true }, [])).Success);
            Assert.False(service.IsSimulating); Assert.Equal(300, Total(service.ReadLogs()));
            Assert.DoesNotContain(service.ReadLogs().RecentEntries, x => x.Amount == 1000);
            Assert.True((await service.RunSimulationAsync(request with { Effect = CombatEffect.ShieldRepair, DurationSeconds = 1, BothRepairDirections = true }, [request.FullTitle])).Success);
            Assert.Equal(300, Total(service.ReadLogs()));
            Assert.Contains(received, x => x.Simulated && x.Entry.Effect == CombatEffect.ShieldRepair);
            var repairActivity = service.ReadLogs().Characters.Single().Activity.Repairs.Single(x => x.Effect == CombatEffect.ShieldRepair);
            Assert.True(repairActivity.Total.Incoming > 0 && repairActivity.Total.Outgoing > 0);
            Volatile.Write(ref elapsed, 2);
            await WaitFor(service, x => !service.IsSimulating && Total(x) == 300);
            Assert.DoesNotContain(service.ReadLogs().RecentEntries, x => x.Effect == CombatEffect.ShieldRepair);
        }
        finally { service.Dispose(); await service.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
        using var stored = new CombatLogStore(database, Now.AddSeconds(2));
        Assert.Equal(300, Total(stored.Snapshot(Now.AddSeconds(2), 10, "", "", new Dictionary<string, long?>())));
    }

    [Fact]
    public void BinaryHistoryCompatibilityAndIncomingIndicatorExcludeOutgoingAndRepairs()
    {
        using var folder = new TempFolder(); using var store = new CombatLogStore(Path.Combine(folder.Path, "combat.db"), Now.AddSeconds(-5));
        var first = new ParsedLogEntry(Now.AddSeconds(-3), "Pilot", "combat", "hit", DamageDirection.Incoming, 100, Kind: (CombatantKind)0);
        store.Commit(new("Gamelogs/log.txt", new("source", 0, 4, "utf-8", ""), new("Pilot")),
            [new(0, first), new(1, first with { Kind = CombatantKind.Player, Amount = 50 }),
             new(2, first with { Kind = CombatantKind.Npc, Timestamp = Now.AddSeconds(-2) }),
             new(3, first with { Direction = DamageDirection.Outgoing, Timestamp = Now }),
             new(4, first with { Effect = CombatEffect.ArmorRepair, Timestamp = Now })]);
        var character = store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>()).Characters.Single();
        Assert.Equal(2, character.Categories.Count);
        Assert.Equal(150, character.Categories.Single(x => x.Kind == CombatantKind.Player).Total.Incoming);
        Assert.Equal(Now.AddSeconds(-2), character.LastIncomingDamageAt);
        Assert.Equal(Now.AddSeconds(-3), character.LastPlayerDamageAt);
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(new() { FlashSeconds = 11 }));
        Assert.Throws<ArgumentException>(() => ApplicationPreferences.NormalizeCombatLogs(new() { FlashColor = "invalid" }));
    }
    private static async Task WaitFor(CombatLogService service, Func<CombatLogSnapshot, bool> condition)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check() { if (condition(service.ReadLogs())) completed.TrySetResult(); }
        service.LogsChanged += Check;
        try { Check(); await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { throw new TimeoutException(service.Diagnostics + " " + System.Text.Json.JsonSerializer.Serialize(service.ReadLogs())); }
        finally { service.LogsChanged -= Check; }
    }
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eve-combat-tests-" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
