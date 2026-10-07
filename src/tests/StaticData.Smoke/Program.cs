using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using EveOPreview.Services.Logs;
using EveOPreview.Services.StaticData;
using EveOPreview.UI;
using Serilog;

// Explicit opt-in network/corpus check, never part of the ordinary unit suite.
// Output is aggregate-only; character names and raw logs are not copied to artifacts.
if (args.Length >= 2 && args[0] == "--log-audit") return LogAudit.Run(args[1], args.Length > 2 ? args[2] : null);
if (args.Length < 2 || args[0] is not ("--download" or "--audit" or "--simulation-catalog" or "--record" or "--weapon-icons"))
    throw new ArgumentException("--download <static-data-directory> OR --audit <static-data-directory> <Gamelogs-directory> OR --simulation-catalog <static-data-directory>");
using var logger = new LoggerConfiguration().CreateLogger();
using var data = new StaticDataService(args[1], logger);
var elapsed = Stopwatch.StartNew();
if (args[0] == "--download")
{
    string last = "";
    data.StaticDataChanged += () =>
    {
        var state = data.ReadStaticData(); string phase = state.Message;
        if (phase != last) { Console.WriteLine(phase); last = phase; }
    };
    var result = await data.UpdateStaticDataAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { result.Success, result.Message, State = data.ReadStaticData(), elapsed.Elapsed }));
    return result.Success ? 0 : 1;
}
if (data.Build is null) throw new InvalidOperationException("Download static data first.");
if (args[0] == "--weapon-icons") return WeaponIconAudit.Run(data);
if (args[0] == "--record")
{
    foreach (var key in args.Skip(3)) { using var record = data.ReadRecord(args[2], key); Console.WriteLine(record?.RootElement.GetRawText()); }
    return 0;
}
if (args[0] == "--simulation-catalog")
{
    var choices = await data.ReadSimulationCatalogAsync();
    void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    Require(choices.Build.HasValue, choices.Error ?? "Missing catalog");
    Require(ReferenceEquals(choices, await data.ReadSimulationCatalogAsync()), "Catalog must be cached.");
    var guristas = choices.Factions.Single(x => x.Name == "Guristas Pirates");
    Require(choices.Npcs.Single(x => x.Name == "Guristas Despoiler").FactionId == guristas.Id, "Classic NPC faction must resolve through SDE group names.");
    var warden = choices.Npcs.Single(x => x.Name == "Hypnosian Warden");
    Require(warden.Attacks.Count == 2 && warden.Attacks[0].DamageTypes == (DamageTypes.EM | DamageTypes.Thermal)
        && warden.Attacks[1].DamageTypes == (DamageTypes.Kinetic | DamageTypes.Explosive), "NPC guns and missiles must retain separate compositions.");
    var blaster = choices.Weapons.Single(x => x.Name == "Light Neutron Blaster II");
    var charge = choices.Ammo.Single(x => x.Name == "Antimatter Charge S");
    Require(blaster.AmmoIds.Contains(charge.Id), "Blaster must accept matching small hybrid ammunition.");
    Require(!blaster.AmmoIds.Contains(choices.Ammo.Single(x => x.Name == "Antimatter Charge L").Id), "Blaster must reject large charges.");
    Require(!blaster.AmmoIds.Contains(choices.Ammo.Single(x => x.Name == "Spike S").Id), "Blaster must reject railgun-only ammunition.");
    var launcher = choices.Weapons.Single(x => x.Name == "Rocket Launcher II");
    Require(launcher.AmmoIds.Contains(choices.Ammo.Single(x => x.Name == "Scourge Rage Rocket").Id), "T2 launcher must accept T2 ammunition.");
    foreach (long id in choices.Weapons.Select(x => x.Id).Concat(choices.Ammo.Select(x => x.Id)))
    {
        using var type = data.ReadRecord("types", id.ToString()); var record = type!.RootElement;
        int meta = record.TryGetProperty("metaGroupID", out var m) ? m.GetInt32() : 0;
        using var dogma = data.ReadRecord("typeDogma", id.ToString());
        double level = dogma!.RootElement.GetProperty("dogmaAttributes").EnumerateArray()
            .Where(x => x.GetProperty("attributeID").GetInt32() == 633)
            .Select(x => x.GetProperty("value").GetDouble()).DefaultIfEmpty(meta is 2 or 53 ? 5 : 0).Single();
        Require(meta is 2 or 53 ? level == 5 : meta is 0 or 1 or 54 && level == 0,
            "Only standard T1/T2 modules and ammunition belong in the simulator: " + id);
    }
    Require(choices.Weapons.Any(x => x.Name == "Rocket Launcher I") && choices.Weapons.Any(x => x.Name == "Rocket Launcher II"),
        "Both standard technology levels must be selectable.");
    foreach (var platform in new[] { WeaponPlatform.LightMissile, WeaponPlatform.HeavyMissile, WeaponPlatform.HeavyAssaultMissile,
        WeaponPlatform.CruiseMissile, WeaponPlatform.XLCruiseMissile, WeaponPlatform.XLTorpedo, WeaponPlatform.PulseLaser,
        WeaponPlatform.BeamLaser, WeaponPlatform.Fighter, WeaponPlatform.Vorton, WeaponPlatform.Disintegrator,
        WeaponPlatform.Bomb, WeaponPlatform.GuidedBomb, WeaponPlatform.StructureMissile, WeaponPlatform.PointDefense,
        WeaponPlatform.Doomsday, WeaponPlatform.Lance, WeaponPlatform.Reaper, WeaponPlatform.Bosonic })
        Require(choices.Weapons.Any(x => x.Attack.Platform == platform), "Missing simulation platform: " + platform);
    Require(choices.Weapons.Single(x => x.Id == 40558).Attack is { Amount: 124.5, CycleMilliseconds: 5000 }, "Fighter primary ability amount/cadence.");
    Require(choices.Weapons.Single(x => x.Id == 24550).Attack.CycleMilliseconds == 300000, "Doomsday cadence must not be clamped to one minute.");
    Require(!choices.Weapons.Any(x => x.Id is 42522 or 40634), "Utility super-group members must not be simulated as damage.");
    var request = new CombatSimulation("EVE - Preview", DamageDirection.Incoming, 1250, CombatDamageType.Unknown, WeaponPlatform.Unknown)
        { UseStaticData = true, Kind = CombatantKind.Player, WeaponTypeId = blaster.Id, AmmoTypeId = charge.Id };
    var attack = choices.Resolve(request).Single().Attacks.Single();
    Require(attack.Weapon == blaster.Name && attack.DamageTypes == DamageTypes.None && attack.Amount > 0,
        "Turret ammo affects damage amount but its composition is not exposed by a module-only log entry.");
    var factionShips = choices.Resolve(request with { Kind = CombatantKind.Npc, NpcFactionId = guristas.Id });
    Require(factionShips.Count > 1 && factionShips.All(x => x.Kind == CombatantKind.Npc), "Faction-only selection must offer NPC ships.");
    var logCatalog = EveLogCatalog.Load(data);
    var peers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orion Voss" };
    int roundTrips = 0;
    var differences = new Dictionary<string, int>();
    var differenceExamples = new Dictionary<string, List<object>>();
    void Check(CombatSimulation simulation)
    {
        var sequence = new CombatSimulationSequence(simulation, DateTimeOffset.UtcNow, 3, sources: choices.Resolve(simulation));
        foreach (var entry in sequence.DrainEvents())
        {
            var parsed = EveLogParser.Parse(FormattableString.Invariant($"[ {entry.Timestamp.UtcDateTime:yyyy.MM.dd HH:mm:ss} ] (combat) {entry.Text}"),
                new(entry.Character), false, logCatalog, peers);
            roundTrips++;
            string Difference() => parsed is null ? "Missing entry" : parsed.Direction != entry.Direction ? "Direction" : parsed.Kind != entry.Kind ? "Kind"
                : parsed.EffectiveDamageTypes != entry.EffectiveDamageTypes ? "Damage types" : parsed.Platform != entry.Platform ? "Platform"
                : parsed.Amount != entry.Amount ? "Amount" : parsed.Effect != entry.Effect ? "Effect"
                : parsed.Weapon != entry.Weapon ? "Weapon" : parsed.DamageEvidence != entry.DamageEvidence ? "Evidence"
                : parsed.DamageSourceTypeId != entry.DamageSourceTypeId ? "Source ID" : "";
            string difference = Difference();
            if (difference.Length > 0)
            {
                differences[difference] = differences.GetValueOrDefault(difference) + 1;
                if (!differenceExamples.TryGetValue(difference, out var examples)) differenceExamples[difference] = examples = [];
                if (examples.Count < 6) examples.Add(new { entry.Counterparty, entry.Weapon, entry.Direction, ExpectedPlatform = entry.Platform,
                    ActualPlatform = parsed?.Platform, ExpectedDamage = entry.DamageTypes, ActualDamage = parsed?.DamageTypes });
            }
        }
    }
    foreach (var weapon in choices.Weapons)
        foreach (long? ammoId in weapon.AmmoIds.Count > 0 ? weapon.AmmoIds.Select(x => (long?)x) : new long?[] { null })
            foreach (var direction in Enum.GetValues<DamageDirection>())
                Check(request with { WeaponTypeId = weapon.Id, AmmoTypeId = ammoId, Direction = direction });
    foreach (var npc in choices.Npcs)
    {
        Check(request with { Kind = CombatantKind.Npc, NpcTypeId = npc.Id });
        Check(request with { Kind = CombatantKind.Npc, NpcTypeId = npc.Id, Direction = DamageDirection.Outgoing });
    }
    Console.WriteLine(JsonSerializer.Serialize(new { roundTrips, differences, differenceExamples }));
    Require(differences.Count == 0, "Simulation must retain the evidence available to the production parser.");
    Console.WriteLine(JsonSerializer.Serialize(new { choices.Build, Factions = choices.Factions.Count, Npcs = choices.Npcs.Count,
        Weapons = choices.Weapons.Count, Ammo = choices.Ammo.Count,
        Platforms = choices.Weapons.GroupBy(x => x.Attack.Platform).ToDictionary(x => x.Key.ToString(), x => x.Count()),
        elapsed.Elapsed, CatalogMemory = GC.GetTotalMemory(true) }));
    if (args.Length > 2) File.WriteAllText(args[2], JsonSerializer.Serialize(choices));
    return 0;
}
var catalog = EveLogCatalog.Load(data);
var auditNow = DateTimeOffset.UtcNow;
var auditSince = auditNow.AddYears(-1);
using var store = new CombatLogStore(":memory:", auditSince);
var identities = new Dictionary<string, long?>();
double expectedIncoming = 0, expectedOutgoing = 0;
long files = 0, entries = 0, incoming = 0, outgoing = 0, resolved = 0, multi = 0, positiveIncoming = 0, positiveResolved = 0;
var combinations = new Dictionary<string, long>();
var evidence = new Dictionary<string, long>();
var excludedDamage = new Dictionary<string, long>();
var platforms = new Dictionary<string, long>();
var repairs = new Dictionary<string, long>();
foreach (string path in Directory.EnumerateFiles(args[2], "*.txt").Order(StringComparer.Ordinal))
{
    if (string.CompareOrdinal(Path.GetFileName(path)[..8], auditSince.ToString("yyyyMMdd")) < 0) continue;
    files++; LogCursor? cursor = null; LogHeader header = new(); bool more;
    do
    {
        LogReadBatch batch;
        using (var stream = CompleteLogReader.OpenShared(path)) batch = CompleteLogReader.Read(stream, CompleteLogReader.Identity(stream), cursor);
        cursor = batch.Cursor; more = batch.More;
        var stored = new List<PositionedLogEntry>();
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        foreach (var line in batch.Lines)
        {
            var entry = EveLogParser.Parse(line.Text, header, false, catalog, new HashSet<string>());
            if (entry?.Direction is null && Regex.IsMatch(Regex.Replace(line.Text, "<[^>]*>", ""), @"\(combat\)\s*\d+(?:\.\d+)?\s+(?:from|to)\s+"))
            {
                string reason = header.Ambiguous ? "Conflicting listeners" : header.Listener is null ? "Missing listener" : entry is null ? "Invalid timestamp or entry header" : "Unsupported damage format";
                excludedDamage[reason] = excludedDamage.GetValueOrDefault(reason) + 1;
            }
            if (entry is null || entry.Timestamp < auditSince || entry.Timestamp > auditNow) continue;
            stored.Add(new(line.Offset, entry));
            entries++;
            if (entry.Direction.HasValue && entry.Effect != CombatEffect.Damage)
            { string rep = $"{entry.Direction} {entry.Effect}"; repairs[rep] = repairs.GetValueOrDefault(rep) + 1; }
            if (entry.Effect != CombatEffect.Damage || !entry.Direction.HasValue) continue;
            string platform = $"{entry.Direction} {entry.Kind} {entry.Platform}";
            platforms[platform] = platforms.GetValueOrDefault(platform) + 1;
            if (entry.Direction == DamageDirection.Outgoing) { outgoing++; expectedOutgoing += entry.Amount; continue; }
            expectedIncoming += entry.Amount;
            incoming++;
            if (entry.Amount > 0) { positiveIncoming++; if (entry.EffectiveDamageTypes != DamageTypes.None) positiveResolved++; }
            if (entry.EffectiveDamageTypes != DamageTypes.None) resolved++;
            if (entry.DamageType == CombatDamageType.Mixed) multi++;
            string type = entry.EffectiveDamageTypes.ToString(); combinations[type] = combinations.GetValueOrDefault(type) + 1;
            string source = entry.DamageEvidence.ToString(); evidence[source] = evidence.GetValueOrDefault(source) + 1;
        }
        var storedFile = new StoredLogFile(path, cursor, header);
        store.Commit(storedFile, stored);
        store.Commit(storedFile, stored); // Duplicate filesystem notifications must not double-count.
    } while (more);
}
long localFiles = 0, systemEvents = 0;
string chatFolder = Path.Combine(Path.GetDirectoryName(args[2])!, "Chatlogs");
if (Directory.Exists(chatFolder)) foreach (string path in Directory.EnumerateFiles(chatFolder, "Local_*.txt"))
{
    if (File.GetLastWriteTimeUtc(path) < auditSince.UtcDateTime) continue;
    localFiles++; LogCursor? cursor = null; LogHeader header = new(); bool more;
    do
    {
        LogReadBatch batch;
        using (var stream = CompleteLogReader.OpenShared(path)) batch = CompleteLogReader.Read(stream, CompleteLogReader.Identity(stream), cursor);
        cursor = batch.Cursor; more = batch.More;
        foreach (var line in batch.Lines) header = EveLogParser.ReadHeader(header, line.Text);
        var stored = new List<PositionedLogEntry>();
        foreach (var line in batch.Lines)
        {
            var entry = EveLogParser.Parse(line.Text, header, true, catalog, new HashSet<string>());
            if (entry?.SolarSystem is null || entry.Timestamp > auditNow) continue;
            systemEvents++; stored.Add(new(line.Offset, entry));
        }
        store.Commit(new(path, cursor, header), stored);
    } while (more);
}
var snapshot = store.Snapshot(auditNow, 10, "Audit", "", identities);
double Total(CombatLogSnapshot state, bool incoming) => state.Characters.Sum(c => c.Categories.Sum(x => incoming ? x.Total.Incoming : x.Total.Outgoing));
if (Math.Abs(Total(snapshot, true) - expectedIncoming) > .1 || Math.Abs(Total(snapshot, false) - expectedOutgoing) > .1)
    throw new InvalidOperationException("Corpus storage totals differ from parsed hits, including duplicate delivery.");
foreach (var character in snapshot.Characters)
foreach (var category in character.Activity!.Combat)
{
    var totals = character.Categories.FirstOrDefault(x => x.Kind == category.Kind)?.Total ?? new DamageFigures(0, 0);
    foreach (var (average, seconds, total) in new[] {
        (category.AverageDps.Incoming, category.DpsSampleSeconds.Incoming, totals.Incoming),
        (category.AverageDps.Outgoing, category.DpsSampleSeconds.Outgoing, totals.Outgoing) })
        if (!double.IsFinite(average) || seconds < 0 || average < 0 || average * seconds > total + .1)
            throw new InvalidOperationException("Recorded DPS samples exceed their character/direction damage evidence.");
}
foreach (var character in snapshot.Characters)
foreach (var order in Enum.GetValues<CombatRowOrder>())
{
    var options = new LogOverlayOptions { RowOrder = order, Repairs = true };
    if (CombatOverlayFormatter.Meter(character, options).Count != 4) throw new InvalidOperationException("Fixed rows were lost.");
}
using (var simulationCopy = store.CreateMemoryCopy())
{
    simulationCopy.Reset(auditNow);
    if (Total(simulationCopy.Snapshot(auditNow, 10, "", "", identities), true) != 0 || Total(store.Snapshot(auditNow, 10, "", "", identities), true) != expectedIncoming)
        throw new InvalidOperationException("Transient reset changed real corpus totals.");
}
var report = new { data.Build, Since = auditSince, files, entries, localFiles, systemEvents, Characters = snapshot.Characters.Count,
    AcceptedDpsSeconds = snapshot.Characters.Sum(x => x.Activity!.Combat.Sum(c => c.DpsSampleSeconds.Incoming + c.DpsSampleSeconds.Outgoing)),
    RecordedDpsEvidencePassed = true,
    SystemsKnown = snapshot.Characters.Count(x => x.SolarSystem is not null), Jumps = snapshot.Characters.Sum(x => x.Activity?.SystemChanges ?? 0),
    incoming, outgoing, resolved, multi, positiveIncoming, positiveResolved, combinations, evidence, platforms, repairs, excludedDamage,
    StoredIncoming = Total(snapshot, true), StoredOutgoing = Total(snapshot, false), DuplicateDeliveryPassed = true, TemporaryIsolationPassed = true, data.CachedItems, elapsed.Elapsed };
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return 0;
