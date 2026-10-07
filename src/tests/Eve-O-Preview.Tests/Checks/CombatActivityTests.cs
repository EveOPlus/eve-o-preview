using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CombatActivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static StoredLogFile File(string source, long offset = 1000) => new(source, new(source, 0, offset, "utf-8", ""), new("Pilot"));
    private static ParsedLogEntry Hit(string peer, double amount, DamageDirection direction = DamageDirection.Outgoing, CombatantKind kind = CombatantKind.Npc)
        => new(Now, "Pilot", "combat", "hit", direction, amount, peer, kind);
    private static CharacterCombatSnapshot Read(CombatLogStore store) => Assert.Single(store.Snapshot(Now.AddSeconds(50), 10, "", "", new Dictionary<string, long?>()).Characters);
    private static ParsedLogEntry Visit(int second, string system, long id) => new(Now.AddSeconds(second), "Pilot", "location", system, SolarSystem: system, SolarSystemId: id);

    [Fact]
    public void ActivitySurvivesRetentionRestartAndReplayAndRetractsAmbiguousSources()
    {
        string path = Path.Combine(Path.GetTempPath(), "eve-activity-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            PositionedLogEntry[] hits = [new(0, Hit("Guristas Despoiler", 120)), new(100, Hit("guristas despoiler", 240)),
                new(200, Hit("Guristas Despoiler", 30, DamageDirection.Incoming)), new(300, Hit("Pilot Two", 80, kind: CombatantKind.Player)),
                new(400, Hit("Guardian", 500, DamageDirection.Incoming, CombatantKind.Player) with { Effect = CombatEffect.ArmorRepair }),
                new(500, Hit("Guardian", 150, kind: CombatantKind.Player) with { Effect = CombatEffect.ShieldRepair }),
                new(600, Hit("Missed target", 0))];
            using (var store = new CombatLogStore(path, Now))
            {
                store.Commit(File("first"), hits);
                store.Commit(File("second"), [new(0, Hit("Guristas Despoiler", 70))]);
                var stats = Read(store).Activity!; var npc = stats.Combat.Single(x => x.Kind == CombatantKind.Npc);
                Assert.Equal(new DamageFigures(1, 3), npc.Hits); Assert.Equal(1, npc.UniqueTargets); Assert.Equal(1, npc.UniqueAttackers);
                Assert.Equal(new DamageFigures(30, 240), npc.LargestHit);
                Assert.Equal(1, stats.Combat.Single(x => x.Kind == CombatantKind.Player).UniqueTargets);
                Assert.Equal(500, stats.Repairs.Single(x => x.Effect == CombatEffect.ArmorRepair).Total.Incoming);
                Assert.Equal(1, stats.Repairs.Single(x => x.Effect == CombatEffect.ShieldRepair).Count.Outgoing);
                store.Prune(Now.AddDays(20), 7); store.Commit(File("first"), hits);
                Assert.Empty(store.Snapshot(Now.AddDays(20), 10, "", "", new Dictionary<string, long?>()).RecentEntries);
                Assert.Equal(3, Read(store).Activity!.Combat.Single(x => x.Kind == CombatantKind.Npc).Hits.Outgoing);
            }
            using (var store = new CombatLogStore(path, Now.AddDays(1)))
            {
                Assert.Equal(3, Read(store).Activity!.Combat.Single(x => x.Kind == CombatantKind.Npc).Hits.Outgoing);
                using (var simulation = store.CreateMemoryCopy())
                {
                    simulation.Commit(File("simulation"), [new(0, Hit("Different NPC", 999))]);
                    Assert.Equal(2, Read(simulation).Activity!.Combat.Single(x => x.Kind == CombatantKind.Npc).UniqueTargets);
                    Assert.Equal(1, Read(store).Activity!.Combat.Single(x => x.Kind == CombatantKind.Npc).UniqueTargets);
                }
                store.Commit(File("first") with { Header = new("Pilot", Ambiguous: true) }, []);
                var npc = Read(store).Activity!.Combat.Single(x => x.Kind == CombatantKind.Npc);
                Assert.Equal(1, npc.Hits.Outgoing); Assert.Equal(70, npc.LargestHit.Outgoing); Assert.Equal(1, npc.UniqueTargets);
                Assert.All(Read(store).Activity!.Repairs, x => Assert.Equal(new DamageFigures(0, 0), x.Total));
            }
        }
        finally { foreach (string suffix in new[] { "", "-wal", "-shm" }) System.IO.File.Delete(path + suffix); }
    }

    [Fact]
    public void TravelUsesTimestampOrderAcrossFilesAndDoesNotCountReconnectsAsJumps()
    {
        using var store = new CombatLogStore(":memory:", Now);
        store.Commit(File("late"), [new(0, Visit(30, "Jita", 1)), new(100, Visit(40, "Jita", 1))]);
        Assert.Equal(0, Read(store).Activity!.SystemChanges);
        store.Commit(File("early"), [new(0, Visit(-5, "Jita", 1)), new(100, Visit(10, "Amarr", 2)), new(200, Visit(20, "Dodixie", 3))]);
        store.Commit(File("duplicate"), [new(0, Visit(10, "Amarr", 2))]);
        Assert.Equal(3, Read(store).Activity!.SystemChanges); Assert.Equal(3, Read(store).Activity!.UniqueSystems);
        Assert.Equal("Jita", Read(store).SolarSystem);
        store.Prune(Now.AddDays(20), 7);
        Assert.Equal(3, Read(store).Activity!.SystemChanges);
        store.Commit(File("early") with { Header = new("Pilot", Ambiguous: true) }, []);
        Assert.Equal(1, Read(store).Activity!.SystemChanges); Assert.Equal(2, Read(store).Activity!.UniqueSystems);
        store.Reset(Now.AddSeconds(50));
        Assert.Equal(0, Read(store).Activity!.SystemChanges); Assert.Equal(1, Read(store).Activity!.UniqueSystems);
        Assert.Equal("Jita", Read(store).SolarSystem);
        store.Commit(File("next"), [new(0, Visit(60, "Amarr", 2))]);
        Assert.Equal(1, Read(store).Activity!.SystemChanges);
    }

    [Fact]
    public void FailedCommitCannotLeaveActivityOrHighWaterMarkBehind()
    {
        using var store = new CombatLogStore(":memory:", Now);
        Assert.ThrowsAny<Exception>(() => store.Commit(File("first") with { Path = null! }, [new(0, Hit("NPC", 100))]));
        Assert.Empty(store.Snapshot(Now, 10, "", "", new Dictionary<string, long?>()).Characters);
        store.Commit(File("first"), [new(0, Hit("NPC", 100))]);
        Assert.Equal(1, Read(store).Activity!.Combat.Single(x => x.Kind == CombatantKind.Npc).Hits.Outgoing);
    }

    [Fact]
    public void VersionOneMigrationBackfillsRetainedHistoryOnceAndPreservesOlderTotals()
    {
        string path = Path.Combine(Path.GetTempPath(), "eve-activity-migration-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var store = new CombatLogStore(path, Now.AddDays(-20)))
            {
                store.Commit(File("old"), [new(0, Hit("Old NPC", 200) with { Timestamp = Now.AddDays(-10) })]);
                store.Commit(File("new"), [new(0, Hit("Retained NPC", 100))]); store.Prune(Now, 7);
            }
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
            {
                connection.Open(); using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE activity_totals; DROP TABLE encounters; DROP TABLE system_visits; DROP TABLE activity_offsets; DELETE FROM metadata WHERE key='activity_since'; PRAGMA user_version=1;";
                command.ExecuteNonQuery();
            }
            for (int open = 0; open < 2; open++)
            {
                using var store = new CombatLogStore(path, Now.AddDays(1)); var character = Read(store);
                Assert.Equal(300, character.Categories.Sum(x => x.Total.Outgoing)); Assert.Equal(Now, character.Activity!.Since);
                Assert.Equal(1, character.Activity.Combat.Single(x => x.Kind == CombatantKind.Npc).Hits.Outgoing);
            }
        }
        finally { foreach (string suffix in new[] { "", "-wal", "-shm" }) System.IO.File.Delete(path + suffix); }
    }
}
