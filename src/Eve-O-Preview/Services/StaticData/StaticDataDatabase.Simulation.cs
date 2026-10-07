#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using EveOPreview.UI;

namespace EveOPreview.Services.StaticData;

public sealed partial class StaticDataDatabase
{
    private sealed record SimType(long Id, string Name, int Group, int Category, long? Faction, bool Published, double Capacity, double Volume, int MetaGroup, string PlatformName);
    private sealed record SimDogma(Dictionary<int, double> Attributes, HashSet<int> Effects)
    {
        public double Get(int id, double fallback = 0) => Attributes.GetValueOrDefault(id, fallback);
        public double Amount => Get(114) + Get(118) + Get(117) + Get(116);
        public DamageTypes Damage => (Get(114) > 0 ? DamageTypes.EM : 0) | (Get(118) > 0 ? DamageTypes.Thermal : 0)
            | (Get(117) > 0 ? DamageTypes.Kinetic : 0) | (Get(116) > 0 ? DamageTypes.Explosive : 0);
    }

    /// <summary>Read once, on a separate background connection. Existing schema-2
    /// installs retain these datasets; the constructor upgrades only the local derived index.</summary>
    public CombatSimulationCatalog ReadSimulationCatalog(CancellationToken cancellation = default)
    {
        var groups = new Dictionary<int, (int Category, string Name)>();
        foreach (var row in ReadDataset(_db, "groups")) using (row)
            groups[row.RootElement.GetProperty("_key").GetInt32()] = ((int)Number(row.RootElement, "categoryID"), EnglishName(row.RootElement));
        var factions = new List<SimulationFaction>();
        foreach (var row in ReadDataset(_db, "factions")) using (row)
            factions.Add(new((long)Number(row.RootElement, "_key"), EnglishName(row.RootElement)));
        var corporationFactions = new Dictionary<long, long>();
        foreach (var row in ReadDataset(_db, "npcCorporations")) using (row)
            if (Number(row.RootElement, "factionID") is > 0 and var faction)
                corporationFactions[(long)Number(row.RootElement, "_key")] = (long)faction;
        var types = new Dictionary<long, SimType>();
        foreach (var row in ReadDataset(_db, "types")) using (row)
        {
            cancellation.ThrowIfCancellationRequested(); var root = row.RootElement;
            int group = (int)Number(root, "groupID");
            if (!groups.TryGetValue(group, out var g) || g.Category is not (7 or 8 or 11 or 18 or 23 or 66 or 87)) continue;
            long id = (long)Number(root, "_key"); double faction = Number(root, "factionID");
            types[id] = new(id, EnglishName(root), group, g.Category, faction > 0 ? (long)faction : null,
                root.TryGetProperty("published", out var published) && published.GetBoolean(), Number(root, "capacity"), Number(root, "volume"), (int)Number(root, "metaGroupID"), PlatformName(_db, root));
        }
        var dogma = new Dictionary<long, SimDogma>();
        foreach (var row in ReadDataset(_db, "typeDogma")) using (row)
        {
            cancellation.ThrowIfCancellationRequested(); var root = row.RootElement; long id = (long)Number(root, "_key");
            if (!types.ContainsKey(id)) continue;
            var attributes = new Dictionary<int, double>(); var effects = new HashSet<int>();
            if (root.TryGetProperty("dogmaAttributes", out var attrs))
                foreach (var a in attrs.EnumerateArray()) attributes[(int)Number(a, "attributeID")] = Number(a, "value");
            if (root.TryGetProperty("dogmaEffects", out var fx))
                foreach (var e in fx.EnumerateArray()) effects.Add((int)Number(e, "effectID"));
            dogma[id] = new(attributes, effects);
        }
        SimDogma Stats(long id) => dogma.GetValueOrDefault(id) ?? new(new(), new());
        int Cycle(double value) => (int)Math.Clamp(value > 0 ? value : 3000, 1, int.MaxValue);
        WeaponPlatform TypePlatform(SimType t) => WeaponPlatformClassifier.Classify(t.Group, t.Category, groups[t.Group].Name, t.PlatformName,
            t.Category == 87 ? WeaponPlatformClassifier.FighterDamage(Stats(t.Id).Attributes) : Stats(t.Id).Damage, Stats(t.Id).Effects);
        var observed = new Dictionary<string, StaticCombatItem?>(StringComparer.OrdinalIgnoreCase);
        StaticCombatItem? Observed(string name)
        {
            if (!observed.TryGetValue(name, out var item)) observed[name] = item = FindItem(name);
            return item;
        }
        // Named/compact and polarized variants can share a Tech I/II meta group.
        // Their dogma meta level distinguishes them from standard level 0/5 items.
        bool StandardTech(SimType t)
        {
            bool techTwo = t.MetaGroup is 2 or 53;
            return (techTwo || t.MetaGroup is 0 or 1 or 54) && Stats(t.Id).Get(633, techTwo ? 5 : 0) == (techTwo ? 5 : 0);
        }
        var ammo = types.Values.Where(t => t.Category == 8 && t.Published && StandardTech(t)
                && t.Group is not (88 or 1158 or 863 or 864) && Stats(t.Id).Amount > 0)
            .Select(t => new SimulationAmmo(t.Id, t.Name, Observed(t.Name)?.Damage ?? DamageTypes.None, Stats(t.Id).Amount)
                { LoggedPlatform = Observed(t.Name)?.Platform ?? WeaponPlatform.Unknown, LoggedSourceTypeId = Observed(t.Name)?.Id ?? 0 }).OrderBy(x => x.Name).ToArray();
        var weapons = new List<SimulationWeapon>(); var npcs = new List<SimulationNpc>();
        var factionIds = factions.Select(x => x.Id).ToHashSet();
        // Older NPC types omit factionID. Their SDE group names explicitly identify
        // the faction (e.g. Asteroid Guristas Frigate). Match only unique faction
        // stems from SDE names, never a ship name or a broad racial association.
        string Stem(string name) => Regex.Split(name.ToLowerInvariant().Replace("'s", ""), "[^a-z]+")
            .FirstOrDefault(x => x.Length >= 4 && x != "the")?.TrimEnd('s') ?? "";
        var stems = factions.GroupBy(x => Stem(x.Name)).Where(x => x.Key.Length >= 4 && x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single().Id);
        long? Faction(SimType type, SimDogma stats)
        {
            long id = type.Faction ?? (long)stats.Get(1341);
            if (factionIds.Contains(id)) return id;
            if (corporationFactions.TryGetValue(id, out var owner) && factionIds.Contains(owner)) return owner;
            var matches = Regex.Split(groups[type.Group].Name.ToLowerInvariant().Replace("'s", ""), "[^a-z]+")
                .Select(x => x.TrimEnd('s')).Where(stems.ContainsKey).Select(x => stems[x]).Distinct().ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        foreach (var type in types.Values)
        {
            cancellation.ThrowIfCancellationRequested(); var d = Stats(type.Id); var platform = TypePlatform(type);
            if (type.Category == 11)
            {
                // Names can alias several SDE types. The simulator must use the
                // same conservative name lookup as the logs, not privileged IDs.
                // Exclude labels that the live parser cannot classify as NPC.
                var npcItem = Observed(type.Name);
                if (npcItem?.Npc != true) continue;
                var attacks = new List<SimulationAttack>();
                if (d.Amount > 0 && (d.Effects.Contains(10) || !d.Effects.Contains(569)))
                {
                    attacks.Add(new(null, npcItem.Platform, npcItem.Damage, npcItem.Id,
                        npcItem.Damage == DamageTypes.None ? DamageEvidence.Unavailable : DamageEvidence.NpcAttack,
                        d.Amount * Math.Max(d.Get(64, 1), .01), Cycle(d.Get(51)), Build));
                }
                if (d.Effects.Contains(569) && types.TryGetValue((long)d.Get(507), out var missile) && Stats(missile.Id).Amount > 0)
                {
                    var m = Stats(missile.Id);
                    var item = Observed(missile.Name);
                    attacks.Add(new(missile.Name, item?.Platform ?? WeaponPlatform.Unknown, item?.Damage ?? DamageTypes.None, item?.Id ?? 0,
                        item?.Damage is not null and not DamageTypes.None ? DamageEvidence.NamedItem : DamageEvidence.Unavailable,
                        m.Amount * Math.Max(d.Get(212, 1), .01), Cycle(d.Get(506)), Build));
                }
                if (attacks.Count > 0) npcs.Add(new(type.Id, type.Name, Faction(type, d), attacks));
            }
            else if (type.Published && type.Category is 7 or 18 or 23 or 66 or 87
                && platform is not (WeaponPlatform.Unknown or WeaponPlatform.DefenderMissile) && StandardTech(type))
            {
                // Fighters use ability attributes, not drone damage/speed. Simulate
                // one fighter's primary repeatable attack; no invented squad or cooldown abilities.
                bool fighter = type.Category == 87;
                double amount = fighter ? new[] { 2227, 2228, 2229, 2230 }.Sum(id => d.Get(id)) : d.Amount;
                double multiplier = fighter ? d.Get(2226, 1) : d.Get(64, d.Get(212, 1));
                double interval = fighter ? d.Get(2233) : d.Get(51, d.Get(73, d.Get(506)));
                if (interval <= 0 || !double.IsFinite(interval) || multiplier <= 0) continue;
                var chargeGroups = new[] { 604, 605, 606, 609, 610 }.Select(x => (int)d.Get(x)).Where(x => x > 0).ToHashSet();
                var compatible = ammo.Where(a => chargeGroups.Contains(types[a.Id].Group)
                    && (d.Get(128) <= 0 || Stats(a.Id).Get(128) == d.Get(128))
                    && (type.Capacity <= 0 || types[a.Id].Volume <= type.Capacity)).Select(a => a.Id).ToArray();
                if (chargeGroups.Count > 0 ? compatible.Length == 0 : amount <= 0) continue;
                var item = Observed(type.Name);
                var attack = new SimulationAttack(type.Name, platform, item?.Damage ?? DamageTypes.None, item?.Id ?? 0,
                    item?.Damage is not null and not DamageTypes.None ? DamageEvidence.NamedItem : DamageEvidence.Unavailable,
                    amount * multiplier, Cycle(interval + d.Get(669)), Build)
                    {
                        LoggedPlatform = item?.Platform ?? WeaponPlatform.Unknown,
                        DelayMilliseconds = CycleOrZero(d.Get(2262, d.Get(1839))),
                        BurstDurationMilliseconds = CycleOrZero(d.Get(2264)),
                        BurstIntervalMilliseconds = CycleOrZero(d.Get(2265)),
                        RampPerCycle = d.Get(2733), RampMaximum = d.Get(2734)
                    };
                weapons.Add(new(type.Id, type.Name, attack, multiplier, compatible)
                    { ModelNote = fighter ? "Primary attack (one fighter)" : null });
            }
        }
        var usedFactions = npcs.Where(x => x.FactionId.HasValue).Select(x => x.FactionId!.Value).ToHashSet();
        return new(Build, factions.Where(x => usedFactions.Contains(x.Id)).OrderBy(x => x.Name).ToArray(),
            npcs.OrderBy(x => x.Name).ToArray(), weapons.OrderBy(x => x.Name).ToArray(), ammo);
    }
    private static double Number(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.TryGetDouble(out var number) ? number : 0;
    private static int CycleOrZero(double value) => (int)Math.Clamp(value, 0, int.MaxValue);
}
