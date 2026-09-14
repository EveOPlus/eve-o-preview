using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Services.Logs;
using EveOPreview.Services.StaticData;
using EveOPreview.UI;
using EveOPreview.Configuration.Implementation;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed partial class StaticDataTests
{
    [Fact]
    public async Task FullExportIsIndexedOfflineAndFailedOrCancelledUpdatesKeepThePreviousGeneration()
    {
        string root = Path.Combine(Path.GetTempPath(), "eve-sde-" + Guid.NewGuid().ToString("N"));
        using var logger = new LoggerConfiguration().CreateLogger();
        var http = new ExportHandler();
        try
        {
            using (var service = new StaticDataService(root, logger, http))
            {
                Assert.True((await service.UpdateStaticDataAsync()).Success);
                Assert.Equal(42, service.Build); Assert.Equal(7, service.ReadStaticData().Datasets);
                var simulationCatalog = await service.ReadSimulationCatalogAsync();
                Assert.Same(simulationCatalog, await service.ReadSimulationCatalogAsync());
                using var future = service.ReadRecord("futureDataset", "test-key");
                Assert.Equal("kept for future features", future.RootElement.GetProperty("value").GetString());
                Assert.Equal(DamageTypes.EM | DamageTypes.Thermal, service.FindItem("Sample Warden").Damage);
                Assert.Equal(DamageTypes.Kinetic | DamageTypes.Explosive, service.FindItem("Sample Missile").Damage);
                Assert.Equal(2, service.CachedItems);
                var catalog = new EveLogCatalog { StaticData = service };
                ParsedLogEntry Parse(string peer, string suffix) => EveLogParser.Parse(
                    $"[ 2026.09.13 12:00:00 ] (combat) <b>125</b> from <b>{peer}</b> - {suffix}", new("Pilot"), false, catalog, new System.Collections.Generic.HashSet<string>());
                var gun = Parse("Sample Warden", "Hits");
                Assert.Equal(CombatantKind.Npc, gun.Kind); Assert.Equal(DamageEvidence.NpcAttack, gun.DamageEvidence);
                Assert.Equal(DamageTypes.EM | DamageTypes.Thermal, gun.DamageTypes);
                Assert.Equal(42, gun.StaticDataBuild);
                var missile = Parse("Sample Warden", "Sample Missile - Hits");
                Assert.Equal(DamageTypes.Kinetic | DamageTypes.Explosive, missile.DamageTypes); Assert.Equal(DamageEvidence.NamedItem, missile.DamageEvidence);
                Assert.Equal(DamageTypes.None, Parse("Other Pilot[TEST](Ship)", "Sample Laser - Hits").DamageTypes);
                Assert.Equal(DamageTypes.None, Parse("Sample Warden", "Unlisted Missile - Hits").DamageTypes);
                Assert.Equal(DamageTypes.Thermal, Parse("Other Pilot", "Sample Smartbomb - Hits").DamageTypes);
                http.Build = 43; http.Broken = true;
                Assert.False((await service.UpdateStaticDataAsync()).Success);
                Assert.Equal(42, service.Build); Assert.NotNull(service.ReadRecord("futureDataset", "test-key"));
                http.Broken = false; http.Hold = true;
                var update = service.UpdateStaticDataAsync(); await http.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                service.CancelStaticDataUpdate(); Assert.False((await update).Success); Assert.Equal(42, service.Build);
                http.Hold = false;
                Assert.True((await service.UpdateStaticDataAsync()).Success); Assert.Equal(43, service.Build);
                Assert.NotSame(simulationCatalog, await service.ReadSimulationCatalogAsync());
                Assert.Equal(43, (await service.ReadSimulationCatalogAsync()).Build);
                Assert.Equal(0, service.CachedItems); Assert.Equal(DamageTypes.EM, service.FindItem("Sample Warden").Damage);
            }
            using var offline = new StaticDataService(root, logger, new ExportHandler { Broken = true });
            Assert.Equal(43, offline.Build); Assert.Equal(DamageTypes.EM, offline.FindItem("sample warden").Damage);
            Assert.Single(Directory.GetFiles(root, "sde-*.sqlite")); Assert.Empty(Directory.GetFiles(root, "*.part"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task StaticSimulationFiltersAmmoAndFactionsAndUsesNormalTemporaryStatsAndIndicators()
    {
        string root = Path.Combine(Path.GetTempPath(), "eve-simulation-" + Guid.NewGuid().ToString("N"));
        using var logger = new LoggerConfiguration().CreateLogger();
        try
        {
            using var data = new StaticDataService(root, logger, new ExportHandler());
            Assert.True((await data.UpdateStaticDataAsync()).Success);
            var catalog = await data.ReadSimulationCatalogAsync();
            Assert.Equal(500024, catalog.Npcs.Single(x => x.Id == 10).FactionId);
            Assert.Equal(500010, catalog.Npcs.Single(x => x.Id == 11).FactionId);
            Assert.Null(catalog.Npcs.Single(x => x.Id == 12).FactionId); // Never infer faction from a ship's own name.
            Assert.Equal(new long[] { 51, 53 }, catalog.Weapons.Single(x => x.Id == 50).AmmoIds.Order().ToArray());
            Assert.Equal(new long[] { 50, 54 }, catalog.Weapons.Select(x => x.Id).Order().ToArray());
            Assert.DoesNotContain(catalog.Ammo, x => x.Id is 58 or 59);

            var now = DateTimeOffset.UtcNow;
            var request = new CombatSimulation("EVE - Pilot", DamageDirection.Incoming, 1250, CombatDamageType.Unknown, WeaponPlatform.Unknown)
                { Kind = CombatantKind.Npc, UseStaticData = true, NpcTypeId = 10, NpcFactionId = 500024 };
            var mixedRequest = request with { DurationSeconds = 60, Randomize = true, WeaponTypeId = 50, AmmoTypeId = 51 };
            var sequence = new CombatSimulationSequence(mixedRequest, now, 3, new Random(42), catalog.Resolve(mixedRequest));
            var entries = new List<ParsedLogEntry>();
            for (int i = 0; i < 60; i++) { sequence.Advance(now.AddSeconds(i)); entries.AddRange(sequence.DrainEvents()); }
            Assert.All(entries.Where(x => x.Effect == CombatEffect.Damage), x => Assert.Equal(CombatantKind.Npc, x.Kind));
            Assert.All(entries.Where(x => x.Effect != CombatEffect.Damage), x => Assert.Equal(CombatantKind.Player, x.Kind));
            Assert.Contains(entries, x => x.Effect == CombatEffect.ShieldRepair && x.Direction == DamageDirection.Incoming);
            Assert.Contains(entries, x => x.Effect == CombatEffect.ArmorRepair && x.Direction == DamageDirection.Outgoing);
            Assert.Contains(entries, x => x.DamageEvidence == DamageEvidence.NpcAttack && x.DamageTypes == (DamageTypes.EM | DamageTypes.Thermal));
            Assert.Contains(entries, x => x.DamageEvidence == DamageEvidence.NamedItem && x.DamageTypes == (DamageTypes.Kinetic | DamageTypes.Explosive));
            Assert.DoesNotContain(entries, x => x.DamageTypes == DamageTypes.All);
            Assert.All(entries.Where(x => x.Effect == CombatEffect.Damage && x.Direction == DamageDirection.Outgoing), x =>
            { Assert.Equal("Sample Blaster II", x.Weapon); Assert.InRange(x.Amount, 13.1, 34.9); Assert.Equal(DamageTypes.None, x.DamageTypes); });
            var preferences = new ApplicationPreferences(Path.Combine(root, "settings.json"), logger);
            var logs = new CombatLogService(preferences, null, logger, Path.Combine(root, "combat.db"), new EveLogCatalog { StaticData = data }, () => now);
            try
            {
                Assert.True((await logs.RunSimulationAsync(request, [request.FullTitle])).Success);
                var character = Assert.Single(logs.ReadLogs().Characters);
                Assert.Equal(now, character.LastIncomingDamageAt); Assert.Null(character.LastPlayerDamageAt);
                Assert.Equal(60, character.Categories.Single(x => x.Kind == CombatantKind.Npc).Total.Incoming);
                Assert.True((await logs.RunSimulationAsync(request with { Stop = true }, [])).Success);
                Assert.Empty(logs.ReadLogs().RecentEntries);
                var player = request with { Kind = CombatantKind.Player, WeaponTypeId = 50, AmmoTypeId = 51 };
                Assert.True((await logs.RunSimulationAsync(player, [request.FullTitle])).Success);
                character = Assert.Single(logs.ReadLogs().Characters);
                Assert.Equal(now, character.LastPlayerDamageAt);
                var entry = Assert.Single(logs.ReadLogs().RecentEntries, x => x.Direction.HasValue);
                Assert.Equal(24, entry.Amount); Assert.Null(entry.DamageSourceTypeId);
                Assert.Equal("Sample Blaster II", entry.Weapon); Assert.Equal(WeaponPlatform.Blaster, entry.Platform);
                Assert.Equal(DamageTypes.None, entry.DamageTypes); // A real module-only log does not reveal loaded ammo.
                Assert.False((await logs.RunSimulationAsync(player with { AmmoTypeId = 52 }, [request.FullTitle])).Success);
                Assert.True(logs.IsSimulating); // Invalid replacement leaves the previous run intact.
                Assert.True((await logs.RunSimulationAsync(player with { Stop = true }, [])).Success);
                Assert.Empty(logs.ReadLogs().RecentEntries);
                Assert.False(logs.IsSimulating);
            }
            finally { logs.Dispose(); await logs.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class ExportHandler : HttpMessageHandler
    {
        public int Build = 42;
        public bool Broken, Hold;
        public TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri.AbsolutePath.EndsWith("latest.jsonl", StringComparison.Ordinal))
                return new(HttpStatusCode.OK) { Content = new StringContent($"{{\"_key\":\"sde\",\"buildNumber\":{Build}}}\n") };
            if (Hold) { Started.TrySetResult(true); await Task.Delay(Timeout.Infinite, token); }
            if (Broken) return new(HttpStatusCode.OK) { Content = new ByteArrayContent([0, 1, 2]) };
            using var bytes = new MemoryStream();
            using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            {
                void Add(string name, string data) { using var writer = new StreamWriter(zip.CreateEntry(name + ".jsonl").Open(), new UTF8Encoding(false)); writer.Write(data); }
                Add("_sde", $"{{\"_key\":\"sde\",\"buildNumber\":{Build}}}");
                Add("futureDataset", """{"_key":"test-key","value":"kept for future features"}""");
                Add("factions", """
                    {"_key":500010,"name":{"en":"Guristas Pirates"}}
                    {"_key":500024,"name":{"en":"Drifters"}}
                    """);
                Add("groups", """
                    {"_key":1,"categoryID":11,"name":{"en":"Entity"}}
                    {"_key":2,"categoryID":8,"name":{"en":"Missile"}}
                    {"_key":3,"categoryID":7,"name":{"en":"Energy Weapon"}}
                    {"_key":4,"categoryID":7,"name":{"en":"Smart Bomb"}}
                    {"_key":5,"categoryID":7,"name":{"en":"Hybrid Weapon"}}
                    {"_key":6,"categoryID":8,"name":{"en":"Hybrid Charge"}}
                    {"_key":7,"categoryID":8,"name":{"en":"Advanced Blaster Charge"}}
                    {"_key":8,"categoryID":11,"name":{"en":"Asteroid Guristas Frigate"}}
                    """);
                Add("types", """
                    {"_key":10,"groupID":1,"factionID":500024,"name":{"en":"Sample Warden"}}
                    {"_key":11,"groupID":8,"name":{"en":"Scout"}}
                    {"_key":12,"groupID":1,"name":{"en":"Guristas Lookalike"}}
                    {"_key":20,"groupID":2,"name":{"en":"Sample Missile"}}
                    {"_key":30,"groupID":3,"name":{"en":"Sample Laser"}}
                    {"_key":40,"groupID":4,"name":{"en":"Sample Smartbomb"}}
                    {"_key":50,"groupID":5,"published":true,"metaGroupID":2,"capacity":0.2,"name":{"en":"Sample Blaster II"}}
                    {"_key":54,"groupID":5,"published":true,"metaGroupID":1,"capacity":0.2,"name":{"en":"Sample Blaster I"}}
                    {"_key":55,"groupID":5,"published":true,"metaGroupID":1,"capacity":0.2,"name":{"en":"Named Blaster"}}
                    {"_key":56,"groupID":5,"published":true,"metaGroupID":2,"capacity":0.2,"name":{"en":"Polarized Blaster"}}
                    {"_key":57,"groupID":5,"published":true,"metaGroupID":4,"capacity":0.2,"name":{"en":"Faction Blaster"}}
                    {"_key":58,"groupID":6,"published":true,"metaGroupID":4,"volume":0.0025,"name":{"en":"Nonstandard Charge 58"}}
                    {"_key":59,"groupID":6,"published":true,"metaGroupID":1,"volume":0.0025,"name":{"en":"Nonstandard Charge 59"}}
                    {"_key":51,"groupID":6,"published":true,"volume":0.0025,"name":{"en":"Sample Charge S"}}
                    {"_key":52,"groupID":6,"published":true,"volume":0.02,"name":{"en":"Sample Charge L"}}
                    {"_key":53,"groupID":7,"published":true,"volume":0.0025,"name":{"en":"Advanced Charge S"}}
                    """);
                Add("dogmaAttributes", """{"_key":114,"name":"emDamage"}""");
                Add("typeDogma", $$"""
                    {"_key":10,"dogmaAttributes":[{"attributeID":114,"value":20},{"attributeID":118,"value":{{(Build == 42 ? 20 : 0)}}},{"attributeID":507,"value":20},{"attributeID":2011,"value":99999}],"dogmaEffects":[{"effectID":10},{"effectID":569}]}
                    {"_key":20,"dogmaAttributes":[{"attributeID":117,"value":10},{"attributeID":116,"value":10}]}
                    {"_key":40,"dogmaAttributes":[{"attributeID":118,"value":300}]}
                    {"_key":11,"dogmaAttributes":[{"attributeID":117,"value":5}],"dogmaEffects":[{"effectID":10}]}
                    {"_key":12,"dogmaAttributes":[{"attributeID":117,"value":5}],"dogmaEffects":[{"effectID":10}]}
                    {"_key":50,"dogmaAttributes":[{"attributeID":604,"value":6},{"attributeID":605,"value":7},{"attributeID":128,"value":1},{"attributeID":64,"value":2},{"attributeID":51,"value":3500}]}
                    {"_key":54,"dogmaAttributes":[{"attributeID":633,"value":0},{"attributeID":604,"value":6},{"attributeID":128,"value":1},{"attributeID":64,"value":2},{"attributeID":51,"value":3500}]}
                    {"_key":55,"dogmaAttributes":[{"attributeID":633,"value":4},{"attributeID":604,"value":6},{"attributeID":128,"value":1},{"attributeID":64,"value":2},{"attributeID":51,"value":3500}]}
                    {"_key":56,"dogmaAttributes":[{"attributeID":633,"value":9},{"attributeID":604,"value":6},{"attributeID":128,"value":1},{"attributeID":64,"value":2},{"attributeID":51,"value":3500}]}
                    {"_key":57,"dogmaAttributes":[{"attributeID":633,"value":5},{"attributeID":604,"value":6},{"attributeID":128,"value":1},{"attributeID":64,"value":2},{"attributeID":51,"value":3500}]}
                    {"_key":58,"dogmaAttributes":[{"attributeID":633,"value":2},{"attributeID":128,"value":1},{"attributeID":117,"value":7}]}
                    {"_key":59,"dogmaAttributes":[{"attributeID":633,"value":2},{"attributeID":128,"value":1},{"attributeID":117,"value":7}]}
                    {"_key":51,"dogmaAttributes":[{"attributeID":128,"value":1},{"attributeID":117,"value":7},{"attributeID":118,"value":5}]}
                    {"_key":52,"dogmaAttributes":[{"attributeID":128,"value":3},{"attributeID":117,"value":70}]}
                    {"_key":53,"dogmaAttributes":[{"attributeID":128,"value":1},{"attributeID":117,"value":8}]}
                    """);
            }
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes.ToArray()) };
        }
    }
}
