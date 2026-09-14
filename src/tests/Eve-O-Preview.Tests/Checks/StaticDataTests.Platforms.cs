using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Services.StaticData;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using EveOPreview.Preview;
using Microsoft.Data.Sqlite;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed partial class StaticDataTests
{
    [Theory]
    [InlineData("Scourge Light Missile", WeaponPlatform.LightMissile)]
    [InlineData("Scourge Fury Light Missile", WeaponPlatform.LightMissile)]
    [InlineData("Rapid Light Missile Launcher II", WeaponPlatform.LightMissile)]
    [InlineData("Scourge Heavy Missile", WeaponPlatform.HeavyMissile)]
    [InlineData("Rapid Heavy Missile Launcher II", WeaponPlatform.HeavyMissile)]
    [InlineData("Scourge Heavy Assault Missile", WeaponPlatform.HeavyAssaultMissile)]
    [InlineData("Scourge Cruise Missile", WeaponPlatform.CruiseMissile)]
    [InlineData("Cruise Missile Launcher II", WeaponPlatform.CruiseMissile)]
    [InlineData("Scourge XL Cruise Missile", WeaponPlatform.XLCruiseMissile)]
    [InlineData("XL Cruise Missile Launcher II", WeaponPlatform.XLCruiseMissile)]
    [InlineData("Scourge XL Torpedo", WeaponPlatform.XLTorpedo)]
    [InlineData("XL Torpedo Launcher II", WeaponPlatform.XLTorpedo)]
    [InlineData("Rapid Torpedo Launcher II", WeaponPlatform.Torpedo)]
    [InlineData("Gatling Pulse Laser II", WeaponPlatform.PulseLaser)]
    [InlineData("Tachyon Beam Laser II", WeaponPlatform.BeamLaser)]
    [InlineData("Firbolg II", WeaponPlatform.Fighter)]
    [InlineData("Standup Firbolg II", WeaponPlatform.Fighter)]
    [InlineData("Hobgoblin II", WeaponPlatform.Drone)]
    [InlineData("Small Vorton Projector II", WeaponPlatform.Vorton)]
    [InlineData("GalvaSurge Condenser Pack S", WeaponPlatform.Vorton)]
    [InlineData("Light Entropic Disintegrator II", WeaponPlatform.Disintegrator)]
    [InlineData("Tetryon Exotic Plasma S", WeaponPlatform.Disintegrator)]
    [InlineData("Bomb Launcher II", WeaponPlatform.Bomb)]
    [InlineData("Scorch Bomb", WeaponPlatform.Bomb)]
    [InlineData("Standup Guided Bomb Launcher II", WeaponPlatform.GuidedBomb)]
    [InlineData("Standup Heavy Guided Bomb", WeaponPlatform.GuidedBomb)]
    [InlineData("Standup Multirole Missile Launcher II", WeaponPlatform.StructureMissile)]
    [InlineData("Standup XL Cruise Missile", WeaponPlatform.StructureMissile)]
    [InlineData("Standup Point Defense Battery II", WeaponPlatform.PointDefense)]
    [InlineData("Standup Flak Round I", WeaponPlatform.PointDefense)]
    [InlineData("Standup Arcing Vorton Projector I", WeaponPlatform.Doomsday)]
    [InlineData("'Judgment' Electromagnetic Doomsday", WeaponPlatform.Doomsday)]
    [InlineData("'Holy Destiny' Electromagnetic Lance", WeaponPlatform.Lance)]
    [InlineData("'Atgeir' Explosive Disruptive Lance", WeaponPlatform.Lance)]
    [InlineData("'Divine Harvest' Electromagnetic Reaper", WeaponPlatform.Reaper)]
    [InlineData("Bosonic Field Generator", WeaponPlatform.Bosonic)]
    [InlineData("Defender Missile I", WeaponPlatform.DefenderMissile)]
    [InlineData("Antimatter Charge S", WeaponPlatform.Hybrid)]
    [InlineData("EMP L", WeaponPlatform.Projectile)]
    [InlineData("Multifrequency S", WeaponPlatform.Laser)]
    [InlineData("Small Artillery Battery", WeaponPlatform.Artillery)]
    [InlineData("Gatling Modal Laser I", WeaponPlatform.PulseLaser)]
    [InlineData("Dual Modal Light Laser I", WeaponPlatform.BeamLaser)]
    [InlineData("75mm Prototype Gauss Gun", WeaponPlatform.Railgun)]
    [InlineData("Modal Light Electron Particle Accelerator I", WeaponPlatform.Blaster)]
    public void OfflineCatalogAndProductionParserCoverWeaponFamilies(string name, WeaponPlatform expected)
    {
        var catalog = EveLogCatalog.Load();
        var item = catalog.FindItem(name); Assert.NotNull(item); Assert.Equal(expected, item.Platform);
        var entry = EveLogParser.Parse("[ 2026.09.14 01:00:00 ] (combat) 123 from Another Pilot - " + name + " - Hits",
            new("Preview"), false, catalog, new HashSet<string>());
        Assert.Equal(expected, entry.Platform); Assert.Equal(CombatantKind.Player, entry.Kind); Assert.Equal(123, entry.Amount);
    }

    [Fact]
    public void BroadGroupsAndSharedAmmoDoNotInventWeaponOrDamageEvidence()
    {
        Assert.Equal(WeaponPlatform.Unknown, WeaponPlatformClassifier.Classify(588, 7, "Super Weapon", "Pulse Activated Nexus Invulnerability Core", 0, new HashSet<int> { 6719 }));
        Assert.Equal(WeaponPlatform.Unknown, WeaponPlatformClassifier.Classify(588, 7, "Super Weapon", "Gravitational Transportation Field Oscillator", 0, new HashSet<int> { 6474 }));
        Assert.Equal(WeaponPlatform.Unknown, WeaponPlatformClassifier.Classify(1396, 7, "Missile Guidance Computer", "Missile Guidance Computer II", 0, new HashSet<int>()));
        Assert.Equal(WeaponPlatform.Unknown, WeaponPlatformClassifier.Classify(482, 8, "Mining Crystal", "Mining Crystal", 0, new HashSet<int>()));
        var catalog = EveLogCatalog.Load();
        foreach (var name in new[] { "Small Vorton Projector II", "Light Entropic Disintegrator II", "Tachyon Beam Laser II" })
            Assert.Equal(DamageTypes.None, catalog.FindItem(name).Damage);
        Assert.Equal(DamageTypes.Thermal, catalog.FindItem("Firbolg II").Damage);
        Assert.Equal(DamageTypes.None, catalog.FindItem("Shadow").Damage);
    }

    [Fact]
    public async Task OldInstalledIndexUpgradesLocallyAndFailedUpgradeRollsBack()
    {
        string root = Path.Combine(Path.GetTempPath(), "eve-index-upgrade-" + Guid.NewGuid().ToString("N"));
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            using (var service = new StaticDataService(root, logger, new ExportHandler())) Assert.True((await service.UpdateStaticDataAsync()).Success);
            string path = Directory.GetFiles(root, "sde-*.sqlite").Single();
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()); db.Open();
            long Scalar(string sql) { using var q = db.CreateCommand(); q.CommandText = sql; return Convert.ToInt64(q.ExecuteScalar()); }
            void Sql(string sql) { using var q = db.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
            long records = Scalar("SELECT COUNT(*) FROM records"), blocks = Scalar("SELECT SUM(length(data)) FROM blocks");
            Sql("DROP TABLE combat_index; UPDATE combat SET platform=2 WHERE id=50; CREATE TRIGGER fail_upgrade BEFORE INSERT ON combat BEGIN SELECT RAISE(ABORT,'test rollback'); END");
            Assert.Throws<SqliteException>(() => StaticDataDatabase.UpgradeCombatIndex(path));
            Assert.Equal(2, Scalar("SELECT platform FROM combat WHERE id=50"));
            Assert.True(Scalar("SELECT COUNT(*) FROM names") > 0);
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='combat_index'"));
            Sql("DROP TRIGGER fail_upgrade");
            Assert.Throws<OperationCanceledException>(() => StaticDataDatabase.UpgradeCombatIndex(path, new CancellationToken(true)));
            using (var offline = new StaticDataService(root, logger, new ExportHandler { Broken = true }))
            {
                Assert.Equal(42, offline.Build); Assert.Equal(WeaponPlatform.Blaster, offline.FindItem("Sample Blaster II").Platform);
                Assert.Equal(WeaponPlatform.Blaster, (await offline.ReadSimulationCatalogAsync()).Weapons.Single(x => x.Id == 50).Attack.Platform);
                Assert.Equal("kept for future features", offline.ReadRecord("futureDataset", "test-key").RootElement.GetProperty("value").GetString());
            }
            Assert.False(StaticDataDatabase.UpgradeCombatIndex(path));
            Assert.Equal(records, Scalar("SELECT COUNT(*) FROM records")); Assert.Equal(blocks, Scalar("SELECT SUM(length(data)) FROM blocks"));
            Assert.Equal(StaticDataDatabase.CombatIndexVersion, Scalar("SELECT version FROM combat_index"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void PersistedEnumsAndOldCustomAppearanceRemainCompatible()
    {
        var oldPlatforms = new[] { WeaponPlatform.Unknown, WeaponPlatform.Rocket, WeaponPlatform.Missile, WeaponPlatform.Torpedo,
            WeaponPlatform.Blaster, WeaponPlatform.Railgun, WeaponPlatform.Laser, WeaponPlatform.Autocannon,
            WeaponPlatform.Artillery, WeaponPlatform.Drone, WeaponPlatform.Smartbomb, WeaponPlatform.Disintegrator };
        for (int i = 0; i < oldPlatforms.Length; i++) Assert.Equal(i, (int)oldPlatforms[i]);
        Assert.Equal(24, (int)OverlaySymbol.Torpedo); Assert.Equal(25, (int)OverlaySymbol.Smartbomb);
        var options = JsonSerializer.Deserialize<LogOverlayOptions>("""{"Advanced":true,"CustomAppearance":{"Weapons":{"Missile":{"Color":"#123456","Icon":3,"TextColor":"#ABCDEF"},"Torpedo":{"Color":"#654321","Icon":24}}}}""");
        var appearance = options.GetAppearance(); var old = appearance.Weapons[WeaponPlatform.Missile];
        Assert.Equal(old, appearance.WeaponStyle(WeaponPlatform.CruiseMissile));
        var xl = appearance.WeaponStyle(WeaponPlatform.XLTorpedo);
        Assert.Equal("#654321", xl.Color); Assert.Equal(OverlaySymbol.XLTorpedo, xl.Icon);
        var reopened = JsonSerializer.Deserialize<LogOverlayOptions>(JsonSerializer.Serialize(options));
        Assert.Equal(old, reopened.GetAppearance().Weapons[WeaponPlatform.Missile]);
        Assert.Equal(2, reopened.GetAppearance().Weapons.Count);
    }

    private sealed class FixedDamageRandom : Random { public override double NextDouble() => .5; }
    [Fact]
    public void StaticSimulationModelsSuperweaponBurstsCooldownsAndDisintegratorRamp()
    {
        var now = DateTimeOffset.Parse("2026-09-14T01:00:00Z");
        var request = new CombatSimulation("EVE - Preview", DamageDirection.Incoming, 100, CombatDamageType.Unknown, WeaponPlatform.Unknown)
            { DurationSeconds = 60, Kind = CombatantKind.Player };
        ParsedLogEntry[] Run(SimulationAttack attack)
        {
            var sequence = new CombatSimulationSequence(request, now, 3, new FixedDamageRandom(), [new("Pilot", CombatantKind.Player, [attack])]);
            var entries = new List<ParsedLogEntry>(sequence.DrainEvents());
            for (int i = 1; i < 60; i++) { sequence.Advance(now.AddSeconds(i)); entries.AddRange(sequence.DrainEvents()); }
            return entries.ToArray();
        }
        var lance = new SimulationAttack("Lance", WeaponPlatform.Lance, DamageTypes.EM, 40631, DamageEvidence.NamedItem, 50000, 300000, 3503375)
            { DelayMilliseconds = 15000, BurstDurationMilliseconds = 15000, BurstIntervalMilliseconds = 1000 };
        var burst = Run(lance); Assert.Equal(15, burst.Length); Assert.Equal(now.AddSeconds(15), burst.First().Timestamp);
        Assert.Equal(now.AddSeconds(29), burst.Last().Timestamp); Assert.All(burst, x => Assert.Equal(50000, x.Amount));
        var doomsday = Run(lance with { DelayMilliseconds = 9000, BurstDurationMilliseconds = 0, BurstIntervalMilliseconds = 0 });
        Assert.Single(doomsday); Assert.Equal(now.AddSeconds(9), doomsday.Single().Timestamp);
        var ramp = Run(lance with { Amount = 100, CycleMilliseconds = 1000, DelayMilliseconds = 0, BurstDurationMilliseconds = 0,
            BurstIntervalMilliseconds = 0, RampPerCycle = .1, RampMaximum = .3 });
        Assert.Equal(new double[] { 100, 110, 120, 130, 130 }, ramp.Take(5).Select(x => x.Amount));
        var fighter = Run(lance with { Amount = 124.5, CycleMilliseconds = 5000, DelayMilliseconds = 0, BurstDurationMilliseconds = 0, BurstIntervalMilliseconds = 0 });
        Assert.Equal(12, fighter.Length); Assert.All(fighter, x => Assert.Equal(124.5, x.Amount));
    }

    [Fact]
    public void ClassificationEnrichmentRetainsStoredCursorsTotalsAndBinaryKind()
    {
        var now = DateTimeOffset.Parse("2026-09-14T01:00:00Z");
        using var store = new CombatLogStore(":memory:", now.AddDays(-1));
        var file = new StoredLogFile("test", new("source", 3, 222, "utf-8", "anchor"), new("Preview"));
        var entry = new ParsedLogEntry(now, "Preview", "combat", "123 from Pilot - Scourge Cruise Missile - Hits", DamageDirection.Incoming,
            123, "Pilot", CombatantKind.Player, Weapon: "Scourge Cruise Missile", Platform: WeaponPlatform.Missile);
        store.Commit(file, [new(100, entry)]);
        var catalog = EveLogCatalog.Load();
        for (int i = 0; i < 2; i++) store.RefreshDamageMetadata(now.AddSeconds(1), 10,
            e => EveLogParser.Parse("[ 2026.09.14 01:00:00 ] (combat) " + e.Text, file.Header, false, catalog, new HashSet<string>()));
        store.Commit(file, [new(100, entry)]); // Original filesystem notification remains a duplicate.
        var snapshot = store.Snapshot(now.AddSeconds(1), 10, "", "", new Dictionary<string, long?>());
        Assert.Equal(file, store.ReadFile("source"));
        Assert.Equal(123, snapshot.Characters.Single().Categories.Single(x => x.Kind == CombatantKind.Player).Total.Incoming);
        var updated = Assert.Single(snapshot.RecentEntries); Assert.Equal(WeaponPlatform.CruiseMissile, updated.Platform);
        Assert.Equal(CombatantKind.Player, updated.Kind); Assert.Equal(123, updated.Amount);
    }
}
