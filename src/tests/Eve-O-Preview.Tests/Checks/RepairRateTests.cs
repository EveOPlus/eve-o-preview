using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Preview;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class RepairRateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static StoredLogFile Source(string id) => new(id, new(id, 0, 1000, "utf-8", ""), new("Pilot"));
    private static ParsedLogEntry Hit(double amount, CombatEffect effect, int second = 0, DamageDirection direction = DamageDirection.Incoming)
        => new(Now.AddSeconds(second), "Pilot", "combat", "", direction, amount, "Logistics Pilot", Effect: effect);
    private static CharacterCombatSnapshot Read(CombatLogStore store, int second = 0) => Assert.Single(store.Snapshot(
        Now.AddSeconds(second), 10, "", "", new Dictionary<string, long?>()).Characters);
    private static double Rate(CharacterCombatSnapshot character, CombatEffect effect) => character.RepairRates.Single(x => x.Effect == effect).PerSecond.Incoming;

    [Fact]
    public void RepairRatesUseEverySourceAndExactWindowWithoutDamageOrReplayInflation()
    {
        using var store = new CombatLogStore(":memory:", Now.AddSeconds(-20));
        var entries = Enumerable.Range(0, 151).Select(i => new PositionedLogEntry(i, Hit(10, CombatEffect.ArmorRepair, -1)
            with { Counterparty = "Logistics Pilot " + i })).ToList();
        entries.Add(new(151, Hit(900, CombatEffect.ArmorRepair, -10))); // Open lower boundary.
        entries.Add(new(152, Hit(200, CombatEffect.ShieldRepair)));
        entries.Add(new(153, Hit(100, CombatEffect.ArmorRepair, direction: DamageDirection.Outgoing)));
        entries.Add(new(154, Hit(40, CombatEffect.HullRepair, 1))); // Future entries wait for their time.
        entries.Add(new(155, Hit(300, CombatEffect.Damage)));
        store.Commit(Source("first"), entries); store.Commit(Source("first"), entries);
        var character = Read(store);
        Assert.Equal(151, Rate(character, CombatEffect.ArmorRepair));
        Assert.Equal(20, Rate(character, CombatEffect.ShieldRepair)); Assert.Equal(0, Rate(character, CombatEffect.HullRepair));
        Assert.Equal(10, character.RepairRates.Single(x => x.Effect == CombatEffect.ArmorRepair).PerSecond.Outgoing);
        Assert.Equal(300, character.Categories.Sum(x => x.Total.Incoming));
        Assert.Equal(100, store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>()).RecentEntries.Count);
        Assert.Equal(4, Rate(Read(store, 1), CombatEffect.HullRepair));
        Assert.Equal(0, Rate(Read(store, 9), CombatEffect.ArmorRepair));
        Assert.All(Read(store, 11).RepairRates, x => Assert.Equal(new DamageFigures(0, 0), x.PerSecond));
    }

    [Fact]
    public void MultipleSimulatedSourcesCombineIntoCompactTypeColouredRates()
    {
        var sequence = new CombatSimulationSequence(new("EVE - Pilot", DamageDirection.Incoming, 1000, CombatDamageType.Unknown,
            WeaponPlatform.Unknown, CombatEffect.ShieldRepair) { RepairSourceCount = 6, MixedRepairTypes = true, BothRepairDirections = true }, Now, 3);
        var entries = sequence.DrainEvents(); Assert.Equal(12, entries.Count); Assert.Equal(6, entries.Select(x => x.Counterparty).Distinct().Count());
        var rows = CombatOverlayFormatter.SimulationEvents(entries.Select(x => new CombatOverlayEvent(x, true)).ToArray(), new() { Repairs = true }, 10);
        Assert.Equal(new[] { "IN", "OUT" }, rows.Where(x => x.Visible).Select(x => x.Label));
        foreach (var row in rows.Where(x => x.Visible))
        {
            var parts = row.Suffix!.Segments().ToArray();
            Assert.Equal(new[] { OverlaySymbol.Shield, OverlaySymbol.Armor, OverlaySymbol.Hull }, parts.Select(x => x.Icon));
            Assert.All(parts, part => { Assert.Equal("200", part.Text); Assert.Equal("", part.Label); Assert.Equal(part.IconColor, part.Color); Assert.True(part.PrefixIcons); });
        }
        var scene = new OverlayScene { ShowTitle = false, Stats = rows, StatsStyle = new(16, 8, 8, true) };
        using var image = EveOPreview.View.Rendering.OverlaySceneRasterizer.Render(scene, new(384, 216));
        string output = Path.Combine(AppContext.BaseDirectory, "repair-rates.png"); image.Save(output);
        Assert.True(new FileInfo(output).Length > 1000);
    }

    [Fact]
    public void AppearanceSampleShowsBothDamageDirectionsAndEveryRepairTypeRegardlessOfSelectedEvent()
    {
        var request = new CombatSimulation("EVE - Pilot", DamageDirection.Incoming, 1200, CombatDamageType.Unknown,
            WeaponPlatform.Unknown, CombatEffect.HullRepair) { Kind = CombatantKind.Player, WeaponTypeId = 1, RepairSourceCount = 1 };
        var catalog = new CombatSimulationCatalog(42, [], [], [new(1, "Sample rocket", new("Sample rocket", WeaponPlatform.Rocket,
            DamageTypes.Kinetic, 1, DamageEvidence.NamedItem, 300, 2500, 42), 1, [])], []);
        var options = new LogOverlayOptions { Repairs = true };
        var rows = CombatOverlayFormatter.AppearanceSample(request, catalog, options, 10);
        Assert.Equal(new[] { "In DPS", "Out DPS", "IN", "OUT" }, rows.Select(x => x.Label));
        Assert.All(rows.Take(2), row => { Assert.True(row.Visible); Assert.Equal("30", row.Value); Assert.NotNull(row.Suffix); });
        Assert.All(rows.Skip(2), row => Assert.Equal(new[] { OverlaySymbol.Shield, OverlaySymbol.Armor, OverlaySymbol.Hull },
            row.Suffix!.Segments().Select(x => x.Icon)));
        var hidden = CombatOverlayFormatter.AppearanceSample(request, catalog,
            options with { Incoming = false, DamageEvents = false, Repairs = false }, 10);
        Assert.Single(hidden, x => x.Visible); Assert.Equal("Out DPS", hidden.Single(x => x.Visible).Label);
        var basic = CombatOverlayFormatter.AppearanceSample(request, CombatSimulationCatalog.Unavailable(""), options, 10);
        Assert.Equal(4, basic.Count(x => x.Visible));
        // Rendering the appearance example must not modify the pending simulation.
        Assert.Equal(CombatEffect.HullRepair, request.Effect); Assert.Equal(1, request.RepairSourceCount);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(10, false)]
    [InlineData(17, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(17, true)]
    public void TimedRepairsCycleThroughSingleAndCombinedRatesAfterPreviousSamplesExpire(int window, bool mixedCombat)
    {
        var request = new CombatSimulation("EVE - Pilot", DamageDirection.Incoming, 1000, CombatDamageType.Unknown,
            WeaponPlatform.Unknown, mixedCombat ? CombatEffect.Damage : CombatEffect.ShieldRepair)
            { DurationSeconds = 60, Randomize = mixedCombat, RepairSourceCount = 6, MixedRepairTypes = !mixedCombat, BothRepairDirections = true };
        var sequence = new CombatSimulationSequence(request, Now, 3, new Random(42), windowSeconds: window);
        using var store = new CombatLogStore(":memory:", Now);
        for (int phase = 0; phase < 4; phase++)
        {
            var at = Now.AddSeconds((mixedCombat ? 1 : 0) + phase * Math.Max(3, window));
            sequence.Advance(at);
            // Use the production parser and rate window, including whole-second log timestamps.
            var entries = sequence.DrainEvents().Where(x => x.Effect != CombatEffect.Damage).Select(x => EveLogParser.Parse(
                FormattableString.Invariant($"[ {x.Timestamp.UtcDateTime:yyyy.MM.dd HH:mm:ss} ] (combat) {x.Text}"),
                new("Pilot"), false, EveLogCatalog.Load(), new HashSet<string> { "Orion Voss" })!).ToArray();
            Assert.Equal(phase % 2 == 0 ? 2 : 12, entries.Length);
            Assert.Equal(phase % 2 == 0 ? 1 : 6, entries.Select(x => x.Counterparty).Distinct().Count());
            store.Commit(Source("phase-" + phase), entries.Select((x, i) => new PositionedLogEntry(i, x)).ToArray());
            var character = Assert.Single(store.Snapshot(at, window, "", "", new Dictionary<string, long?>()).Characters);
            var rows = CombatOverlayFormatter.Meter(character, new() { Repairs = true }).Where(x => x.Visible).ToArray();
            Assert.Equal(new[] { "IN", "OUT" }, rows.Select(x => x.Label));
            Assert.All(rows, row => Assert.Equal(phase % 2 == 0 ? 1 : 3, row.Suffix!.Segments().Count()));
            // Each snapshot includes only this sample, not previous combined rates.
            Assert.Equal(entries.Where(x => x.Direction == DamageDirection.Incoming).Sum(x => x.Amount) / window,
                character.RepairRates.Sum(x => x.PerSecond.Incoming), 8);
            if (phase % 2 == 0) Assert.All(rows, row => Assert.Equal(phase == 0 ? OverlaySymbol.Shield : OverlaySymbol.Armor, row.Suffix!.Icon));
        }
        sequence.Advance(Now.AddSeconds(60)); Assert.Empty(sequence.DrainEvents());
    }

    [Theory]
    [InlineData(CombatResetScope.All)]
    [InlineData(CombatResetScope.Damage)]
    [InlineData(CombatResetScope.Repairs)]
    [InlineData(CombatResetScope.Jumps)]
    public void ScopedResetSurvivesRestartAndPreservesOtherCountersAndCursors(CombatResetScope scope)
    {
        string path = Path.Combine(Path.GetTempPath(), "eve-reset-" + Guid.NewGuid().ToString("N") + ".db");
        bool damage = scope is CombatResetScope.All or CombatResetScope.Damage;
        bool repairs = scope is CombatResetScope.All or CombatResetScope.Repairs;
        bool jumps = scope is CombatResetScope.All or CombatResetScope.Jumps;
        try
        {
            using (var store = new CombatLogStore(path, Now))
            {
                store.Commit(Source("first"), [new(0, new(Now, "Pilot", "location", "Jita", SolarSystem: "Jita", SolarSystemId: 1)),
                    new(1, Hit(400, CombatEffect.Damage, 1)), new(2, Hit(100, CombatEffect.ShieldRepair, 1)),
                    new(3, new(Now.AddSeconds(2), "Pilot", "location", "Amarr", SolarSystem: "Amarr", SolarSystemId: 2))]);
                Assert.Equal(1, Read(store, 4).Activity!.SystemChanges);
                store.Reset(Now.AddSeconds(5), scope);
            }
            using var reopened = new CombatLogStore(path, Now.AddSeconds(6));
            var current = Read(reopened, 6);
            Assert.Equal(damage ? 0 : 400, current.Categories.Sum(x => x.Total.Incoming));
            Assert.Equal(repairs ? 0 : 10, Rate(current, CombatEffect.ShieldRepair));
            Assert.Equal(jumps ? 0 : 1, current.Activity!.SystemChanges);
            Assert.Equal("Amarr", current.SolarSystem); Assert.Equal(1000, reopened.ReadFile("first")!.Cursor.Offset);
            // Newly discovered older files must not repopulate a reset counter.
            reopened.Commit(Source("late"), [new(0, Hit(80, CombatEffect.Damage, 3)), new(1, Hit(20, CombatEffect.ShieldRepair, 3))]);
            current = Read(reopened, 6);
            Assert.Equal(damage ? 0 : 480, current.Categories.Sum(x => x.Total.Incoming));
            Assert.Equal(repairs ? 0 : 12, Rate(current, CombatEffect.ShieldRepair));
            reopened.Commit(Source("new"), [new(0, Hit(50, CombatEffect.Damage, 6)), new(1, Hit(30, CombatEffect.ShieldRepair, 6))]);
            current = Read(reopened, 7);
            Assert.Equal(damage ? 50 : 530, current.Categories.Sum(x => x.Total.Incoming));
            Assert.Equal(repairs ? 3 : 15, Rate(current, CombatEffect.ShieldRepair));
        }
        finally { foreach (string suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix); }
    }

    [Fact]
    public void FailedScopedResetRollsBackEntriesCountersAndCutoffs()
    {
        string path = Path.Combine(Path.GetTempPath(), "eve-reset-rollback-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using var store = new CombatLogStore(path, Now);
            store.Commit(Source("first"), [new(0, Hit(100, CombatEffect.ShieldRepair, 1))]);
            using var connection = new SqliteConnection($"Data Source={path};Pooling=False"); connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER fail_reset BEFORE DELETE ON activity_totals BEGIN SELECT RAISE(ABORT,'test'); END;";
            command.ExecuteNonQuery();
            Assert.Throws<SqliteException>(() => store.Reset(Now.AddSeconds(5), CombatResetScope.Repairs));
            Assert.Equal(10, Rate(Read(store, 6), CombatEffect.ShieldRepair));
            store.Commit(Source("late"), [new(0, Hit(20, CombatEffect.ShieldRepair, 3))]);
            Assert.Equal(12, Rate(Read(store, 6), CombatEffect.ShieldRepair));
        }
        finally { foreach (string suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix); }
    }
}
