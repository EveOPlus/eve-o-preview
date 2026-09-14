using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CombatDpsAverageTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static StoredLogFile File(string source) => new(source, new(source, 0, 10000, "utf-8", ""), new("Pilot"));
    private static PositionedLogEntry[] Run(int start, int length, double amount = 100, int step = 1,
        DamageDirection direction = DamageDirection.Outgoing, CombatantKind kind = CombatantKind.Npc)
        => Enumerable.Range(0, length / step + 1).Select(i => new PositionedLogEntry((start + i * step) * 100,
            new(Start.AddSeconds(start + i * step), "Pilot", "combat", "hit", direction, amount, "Target", kind,
                Platform: WeaponPlatform.Artillery))).ToArray();
    private static CharacterCombatSnapshot Read(CombatLogStore store) => Assert.Single(store.Snapshot(Start.AddDays(1), 10, "", "", new Dictionary<string, long?>()).Characters);
    private static CombatActivityCategory Npc(CombatLogStore store) => Read(store).Activity!.Combat.Single(x => x.Kind == CombatantKind.Npc);

    [Fact]
    public void OnlyConfirmedMiddleIntervalsCountAndIdleDoesNotDiluteLifetimeAverage()
    {
        using var store = new CombatLogStore(":memory:", Start);
        var hits = Run(0, 40).Select(x => x with { Entry = x.Entry with
            { Amount = x.Entry.Timestamp <= Start.AddSeconds(10) || x.Entry.Timestamp > Start.AddSeconds(30) ? 9000 : 100 } }).ToArray();
        store.Commit(File("a"), hits[..21]);
        Assert.Equal(0, Npc(store).DpsSampleSeconds.Outgoing);
        store.Commit(File("a"), hits[21..]);
        Assert.Equal(100, Npc(store).AverageDps.Outgoing);
        Assert.Equal(20, Npc(store).DpsSampleSeconds.Outgoing);
        Assert.Equal(9000, Assert.Single(store.Snapshot(Start.AddSeconds(40), 10, "", "", new Dictionary<string, long?>()).Characters)
            .Categories.Single(x => x.Kind == CombatantKind.Npc).Dps.Outgoing);
        Assert.Equal(0, Read(store).Categories.Single(x => x.Kind == CombatantKind.Npc).Dps.Outgoing);
        store.Commit(File("b"), Run(200, 60, 200));
        Assert.Equal(60, Npc(store).DpsSampleSeconds.Outgoing);
        Assert.Equal(10000d / 60, Npc(store).AverageDps.Outgoing, 8);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    public void SlowVolleysUseCompleteIntervalsWithoutTreatingNormalFiringGapsAsIdle(int cycle)
    {
        using var store = new CombatLogStore(":memory:", Start);
        store.Commit(File("artillery"), Run(0, cycle * 5, cycle * 100, cycle));
        Assert.Equal(100, Npc(store).AverageDps.Outgoing);
        Assert.Equal(cycle * 3, Npc(store).DpsSampleSeconds.Outgoing);
    }

    [Fact]
    public void ShortBurstsZeroHitsAndRepairsDoNotCreateGoodDamageSamples()
    {
        using var store = new CombatLogStore(":memory:", Start);
        store.Commit(File("a"), Run(0, 20));
        store.Commit(File("zero"), Run(21, 200, 0));
        store.Commit(File("repairs"), Run(21, 200).Select(x => x with { Entry = x.Entry with { Effect = CombatEffect.ShieldRepair } }).ToArray());
        store.Commit(File("b"), Run(201, 20));
        Assert.Equal(0, Npc(store).DpsSampleSeconds.Outgoing);
        Assert.Equal(0, Npc(store).AverageDps.Outgoing);
    }

    [Fact]
    public void DirectionsAndTargetsAccumulateIndependently()
    {
        using var store = new CombatLogStore(":memory:", Start);
        store.Commit(File("out"), Run(0, 40, 100));
        store.Commit(File("in"), Run(0, 50, 200, direction: DamageDirection.Incoming));
        store.Commit(File("player"), Run(0, 60, 300, kind: CombatantKind.Player));
        Assert.Equal(new DamageFigures(200, 100), Npc(store).AverageDps);
        Assert.Equal(new DamageFigures(30, 20), Npc(store).DpsSampleSeconds);
        var player = Read(store).Activity!.Combat.Single(x => x.Kind == CombatantKind.Player);
        Assert.Equal(new DamageFigures(0, 300), player.AverageDps);
        Assert.Equal(new DamageFigures(0, 40), player.DpsSampleSeconds);
    }

    [Fact]
    public void LateFilesRebuildChronologyAndDuplicateDeliveryDoesNotInflateIt()
    {
        using var store = new CombatLogStore(":memory:", Start);
        store.Commit(File("late"), Run(25, 15));
        store.Commit(File("early"), Run(0, 24));
        Assert.Equal(100, Npc(store).AverageDps.Outgoing);
        Assert.Equal(20, Npc(store).DpsSampleSeconds.Outgoing);
        store.Commit(File("early"), Run(0, 24));
        Assert.Equal(100, Npc(store).AverageDps.Outgoing);
        store.Commit(File("same-time"), Run(30, 0, 200));
        Assert.Equal(110, Npc(store).AverageDps.Outgoing);
    }

    [Fact]
    public void FailedCommitRollsBackSamplesStateAndCursorTogether()
    {
        using var store = new CombatLogStore(":memory:", Start);
        store.Commit(File("a"), Run(0, 40));
        Assert.ThrowsAny<Exception>(() => store.Commit(File("bad") with { Path = null! }, Run(200, 60, 900)));
        Assert.Equal(100, Npc(store).AverageDps.Outgoing);
        Assert.Equal(20, Npc(store).DpsSampleSeconds.Outgoing);
        store.Commit(File("bad"), Run(200, 60, 200));
        Assert.Equal(10000d / 60, Npc(store).AverageDps.Outgoing, 8);
    }

    [Theory]
    [InlineData(CombatResetScope.All, true)]
    [InlineData(CombatResetScope.Damage, true)]
    [InlineData(CombatResetScope.Repairs, false)]
    [InlineData(CombatResetScope.Jumps, false)]
    public void OnlyDamageOrAllResetClearsRecordedAverages(CombatResetScope scope, bool clears)
    {
        using var store = new CombatLogStore(":memory:", Start);
        store.Commit(File("a"), Run(0, 40));
        store.Reset(Start.AddSeconds(100), scope);
        store.Commit(File(clears ? "old-replay" : "a"), Run(0, 40, 900));
        if (clears) Assert.All(Read(store).Activity!.Combat, x => Assert.Equal(new DamageFigures(0, 0), x.DpsSampleSeconds));
        store.Commit(File("new"), Run(200, 40, 200));
        Assert.Equal(clears ? 200 : 150, Npc(store).AverageDps.Outgoing);
    }

    [Fact]
    public void RetentionRestartSimulationAndSourceRetractionPreserveAccurateMeans()
    {
        string path = Path.Combine(Path.GetTempPath(), "eve-dps-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var store = new CombatLogStore(path, Start))
            {
                store.Commit(File("a"), Run(0, 40)); store.Commit(File("b"), Run(200, 40, 200));
                store.Prune(Start.AddDays(20), 1);
            }
            using (var store = new CombatLogStore(path, Start.AddDays(20)))
            {
                Assert.Equal(150, Npc(store).AverageDps.Outgoing);
                store.Commit(File("b"), Run(241, 9, 200)); // Pending tail survived pruning/restart.
                Assert.Equal(8000d / 50, Npc(store).AverageDps.Outgoing, 8);
                using (var copy = store.CreateMemoryCopy())
                {
                    copy.Commit(File("simulation"), Run(400, 40, 900));
                    Assert.NotEqual(Npc(copy).AverageDps, Npc(store).AverageDps);
                }
                store.Commit(File("a") with { Header = new("Pilot", Ambiguous: true) }, []);
                Assert.Equal(200, Npc(store).AverageDps.Outgoing);
                Assert.Equal(30, Npc(store).DpsSampleSeconds.Outgoing);
            }
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) System.IO.File.Delete(path + suffix); }
    }

    [Fact]
    public void VersionTwoMigrationUsesAvailableHistoryOnceWithoutChangingOldTotals()
    {
        string path = Path.Combine(Path.GetTempPath(), "eve-dps-upgrade-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var store = new CombatLogStore(path, Start.AddDays(-20)))
            {
                store.Commit(File("old"), [new(0, Run(0, 0)[0].Entry with { Timestamp = Start.AddDays(-10), Amount = 5000 })]);
                store.Commit(File("new"), Run(0, 40)); store.Prune(Start, 7);
            }
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                connection.Open(); using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE dps_streams; DROP TABLE dps_damage; DELETE FROM metadata WHERE key='dps_since'; PRAGMA user_version=2;";
                command.ExecuteNonQuery();
            }
            for (int i = 0; i < 2; i++)
            using (var store = new CombatLogStore(path, Start.AddDays(1)))
            {
                Assert.Equal(100, Npc(store).AverageDps.Outgoing);
                Assert.Equal(20, Npc(store).DpsSampleSeconds.Outgoing);
                Assert.Equal(Start, Read(store).Activity!.AverageDpsSince);
                Assert.Equal(9100, Read(store).Categories.Sum(x => x.Total.Outgoing));
            }
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) System.IO.File.Delete(path + suffix); }
    }
}
