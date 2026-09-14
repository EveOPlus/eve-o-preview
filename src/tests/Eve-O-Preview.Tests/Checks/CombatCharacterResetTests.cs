using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EveOPreview.Services.Logs;
using EveOPreview.UI;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CombatCharacterResetTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static StoredLogFile File(string source, string character) => new(source, new(source, 0, 10000, "utf-8", ""), new(character));
    private static PositionedLogEntry[] Entries(string character) => Enumerable.Range(0, 41).Select(i => new PositionedLogEntry(i * 100,
        new(Start.AddSeconds(i), character, "combat", "damage", DamageDirection.Outgoing, 100, "Sample NPC", CombatantKind.Npc)))
        .Concat(new PositionedLogEntry[] {
            new(5000, new(Start.AddSeconds(30), character, "combat", "repair", DamageDirection.Incoming, 300, "Sample Pilot", Effect: CombatEffect.ShieldRepair)),
            new(5100, new(Start, character, "location", "Jita", SolarSystem: "Jita", SolarSystemId: 30000142)),
            new(5200, new(Start.AddSeconds(20), character, "location", "Amarr", SolarSystem: "Amarr", SolarSystemId: 30002187)) }).ToArray();
    private static CharacterCombatSnapshot Read(CombatLogStore store, string character) => store.Snapshot(Start.AddMinutes(3), 10, "", "", new Dictionary<string, long?>()).Characters.Single(x => x.Name == character);

    [Theory]
    [InlineData(CombatResetScope.All)]
    [InlineData(CombatResetScope.Damage)]
    [InlineData(CombatResetScope.Repairs)]
    [InlineData(CombatResetScope.Jumps)]
    public void SelectedCharacterResetPersistsWithoutTouchingOthersOrReplayingOldData(CombatResetScope scope)
    {
        string path = Path.Combine(Path.GetTempPath(), "eve-character-reset-" + Guid.NewGuid().ToString("N") + ".db");
        string otherBefore;
        try
        {
            using (var store = new CombatLogStore(path, Start))
            {
                store.Commit(File("one", "Pilot One"), Entries("Pilot One"));
                store.Commit(File("two", "Pilot Two"), Entries("Pilot Two"));
                otherBefore = JsonSerializer.Serialize(Read(store, "Pilot Two"));
                store.Reset(Start.AddMinutes(1), scope, "pilot one");
                Assert.Equal(otherBefore, JsonSerializer.Serialize(Read(store, "Pilot Two")));
            }
            using (var store = new CombatLogStore(path, Start.AddDays(1)))
            {
                // An unseen file predating the selected reset must obey its cutoff.
                store.Commit(File("late", "Pilot One"), Entries("Pilot One"));
                var selected = Read(store, "Pilot One");
                bool damage = scope is CombatResetScope.All or CombatResetScope.Damage;
                bool repairs = scope is CombatResetScope.All or CombatResetScope.Repairs;
                bool jumps = scope is CombatResetScope.All or CombatResetScope.Jumps;
                Assert.Equal(damage ? 0 : 8200, selected.Categories.Sum(x => x.Total.Outgoing));
                Assert.Equal(repairs ? 0 : 600, selected.Activity!.Repairs.Sum(x => x.Total.Incoming));
                Assert.Equal(jumps ? 0 : 1, selected.Activity.SystemChanges);
                if (damage) Assert.All(selected.Activity.Combat, x => Assert.Equal(new DamageFigures(0, 0), x.DpsSampleSeconds));
                Assert.Equal(otherBefore, JsonSerializer.Serialize(Read(store, "Pilot Two")));
                using (var copy = store.CreateMemoryCopy())
                {
                    copy.Commit(File("simulation-old", "Pilot One"), Entries("Pilot One"));
                    if (damage) Assert.Equal(0, Read(copy, "Pilot One").Categories.Sum(x => x.Total.Outgoing));
                    copy.Reset(Start.AddMinutes(2), CombatResetScope.All, "Pilot Two");
                    Assert.Equal(otherBefore, JsonSerializer.Serialize(Read(store, "Pilot Two")));
                }
                store.Commit(File("new", "Pilot One"), [new(0, new(Start.AddSeconds(80), "Pilot One", "combat", "new", DamageDirection.Outgoing, 50)),
                    new(100, new(Start.AddSeconds(80), "Pilot One", "location", "Jita", SolarSystem: "Jita", SolarSystemId: 30000142))]);
                Assert.Equal(damage ? 50 : 8250, Read(store, "Pilot One").Categories.Sum(x => x.Total.Outgoing));
                Assert.Equal(jumps ? 1 : 2, Read(store, "Pilot One").Activity!.SystemChanges);
                store.Reset(Start.AddMinutes(2));
                Assert.All(store.Snapshot(Start.AddMinutes(3), 10, "", "", new Dictionary<string, long?>()).Characters,
                    x => Assert.Equal(0, x.Categories.Sum(c => c.Total.Incoming + c.Total.Outgoing)));
            }
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) System.IO.File.Delete(path + suffix); }
    }
}
