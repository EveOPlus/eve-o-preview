#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EveOPreview.UI;
using EveOPreview.Services.StaticData;

namespace EveOPreview.Services.Logs;

public sealed class EveLogCatalog
{
    public sealed record WeaponInfo(WeaponPlatform Platform, CombatDamageType DamageType, DamageTypes Types = DamageTypes.None, long TypeId = 0);
    public int Build { get; init; }
    public HashSet<string> NpcNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, long> Systems { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, WeaponInfo> Weapons { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, WeaponInfo> NpcAttacks { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    [System.Text.Json.Serialization.JsonIgnore]
    public StaticDataService? StaticData { get; init; }
    public StaticCombatItem? FindItem(string name)
    {
        if (StaticData?.Build is not null) return StaticData.FindItem(name);
        bool npc = NpcNames.Contains(name);
        var weapon = (npc ? NpcAttacks : Weapons).GetValueOrDefault(name);
        if (weapon is null) return npc ? new(0, true, WeaponPlatform.Unknown, DamageTypes.None) : null;
        var types = weapon.Types != DamageTypes.None ? weapon.Types : new ParsedLogEntry(default, "", "", "", DamageType: weapon.DamageType).EffectiveDamageTypes;
        return new(weapon.TypeId, npc, weapon.Platform, types, Build: Build);
    }
    public static EveLogCatalog Load(StaticDataService? staticData = null)
    {
        using Stream stream = typeof(EveLogCatalog).Assembly.GetManifestResourceStream("EveOPreview.Resources.LogCatalog.json")
            ?? throw new InvalidOperationException("The log catalog is missing.");
        var catalog = JsonSerializer.Deserialize<EveLogCatalog>(stream)!;
        return new() { Build = catalog.Build, NpcNames = new(catalog.NpcNames, StringComparer.OrdinalIgnoreCase),
            Systems = new(catalog.Systems, StringComparer.OrdinalIgnoreCase), Weapons = new(catalog.Weapons, StringComparer.OrdinalIgnoreCase),
            NpcAttacks = new(catalog.NpcAttacks, StringComparer.OrdinalIgnoreCase), StaticData = staticData };
    }
}

public sealed record LogHeader(string? Listener = null, string? Channel = null,
    DateTimeOffset? Session = null, bool Ambiguous = false);

/// <summary>Only complete lines enter this parser. Unsupported text remains available as a game entry.</summary>
public static class EveLogParser
{
    private static readonly TimeSpan RegexLimit = TimeSpan.FromMilliseconds(50);
    private static Regex Pattern(string value) => new(value, RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexLimit);
    private static readonly Regex Timestamp = Pattern(@"^\[\s*(?<time>\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2})\s*\]\s*(?<body>.*)$");
    private static readonly Regex GameEntry = Pattern(@"^\((?<category>[^)]+)\)\s*(?<text>.*)$");
    private static readonly Regex Tags = Pattern(@"<[^>]*>");
    private static readonly Regex Damage = Pattern(@"^(?<amount>\d+(?:\.\d+)?)\s*(?<direction>from|to|von|nach|de|à|из|на|攻撃者:|対象:|来自|对)\s*(?<peer>.+?)(?:\s+-\s+(?<suffix>.*))?$");
    private static readonly Regex Repair = Pattern(@"^(?<amount>\d+(?:\.\d+)?)\s+remote (?<effect>armor repaired|shield boosted|hull repaired) (?<direction>to|by)\s+(?<peer>.+?)(?:\s+-\s+(?<suffix>.*))?$");
    private static readonly Regex PilotLabel = Pattern(@"^(?<name>.+?)\s*\[[^\[\]]+\]\s*\([^()]+\)$");
    private static readonly Regex Local = Pattern(@"^EVE System > Channel changed to Local\s*:\s*(?<system>[^<>\r\n]+)$");
    private static readonly string[] Listeners = ["Listener", "Empfänger", "Auditeur", "Слушатель", "傍聴者", "收听者"];
    private static readonly string[] Sessions = ["Session Started", "Sitzung gestartet", "Session commencée", "Сеанс начат", "セッション開始", "进程开始"];

    public static LogHeader ReadHeader(LogHeader header, string line)
    {
        int colon = line.IndexOf(':');
        if (colon < 0) return header;
        string key = line[..colon].Trim(), value = line[(colon + 1)..].Trim();
        if (Listeners.Contains(key, StringComparer.OrdinalIgnoreCase) && ValidName(value))
            return header with { Listener = header.Listener ?? value,
                Ambiguous = header.Ambiguous || (header.Listener is not null && !value.Equals(header.Listener, StringComparison.OrdinalIgnoreCase)) };
        if (key.Equals("Channel Name", StringComparison.OrdinalIgnoreCase)) return header with { Channel = value };
        if (Sessions.Contains(key, StringComparer.OrdinalIgnoreCase) && TryTime(value, out var time))
            return header with { Session = time };
        return header;
    }

    public static ParsedLogEntry? Parse(string line, LogHeader header, bool chat, EveLogCatalog catalog,
        IReadOnlySet<string> knownPlayers)
    {
        if (header.Ambiguous || header.Listener is null) return null;
        var match = Timestamp.Match(line.TrimStart('\uFEFF'));
        if (!match.Success || !TryTime(match.Groups["time"].Value, out var time)) return null;
        string body = match.Groups["body"].Value;
        if (chat)
        {
            // Never accept user chat mentioning a system, or messages from another channel.
            if (header.Channel != "Local") return null;
            var local = Local.Match(body);
            if (!local.Success) return null;
            string name = local.Groups["system"].Value.Trim();
            if (!catalog.Systems.TryGetValue(name, out long system)) return null;
            return new(time, header.Listener, "location", "Local: " + name, SolarSystem: name, SolarSystemId: system);
        }
        var entry = GameEntry.Match(body);
        if (!entry.Success) return null;
        string category = entry.Groups["category"].Value;
        string plain = WebUtility.HtmlDecode(Tags.Replace(entry.Groups["text"].Value, "")).Trim();
        if (plain.Length > 4096) plain = plain[..4096];
        var result = new ParsedLogEntry(time, header.Listener, category, plain);
        if (category != "combat") return result;
        var damage = Damage.Match(plain);
        bool repair = false;
        if (!damage.Success) { damage = Repair.Match(plain); repair = damage.Success; }
        if (!damage.Success || !double.TryParse(damage.Groups["amount"].Value, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out double amount) || !double.IsFinite(amount) || amount < 0 || amount > 1_000_000_000)
            return result;
        string peer = damage.Groups["peer"].Value.Trim();
        string suffix = damage.Groups["suffix"].Value;
        // NPC names can themselves contain the log's separator (for example
        // "Blood Phantom - Ectoplasm"). Keep the longest known full label before
        // parsing the weapon/quality suffix; a short NPC prefix is not the target.
        if (!repair && suffix.Contains(" - ", StringComparison.Ordinal))
        {
            string tail = peer + " - " + suffix;
            for (int end = tail.LastIndexOf(" - ", StringComparison.Ordinal); end > peer.Length;
                end = tail.LastIndexOf(" - ", end - 1, StringComparison.Ordinal))
            {
                string candidate = tail[..end].Trim();
                if (candidate.Length > 100 || !knownPlayers.Contains(candidate) && catalog.FindItem(candidate)?.Npc != true) continue;
                peer = candidate; suffix = tail[(end + 3)..]; break;
            }
        }
        // Binary product rule: exact NPC matches are NPC, remaining targets count
        // as Player. Custom overview names can obscure an NPC match; this fallback
        // is classification policy, not proof that a character identity was resolved.
        CombatantKind kind = CombatantKind.Player;
        var attacker = knownPlayers.Contains(peer) ? null : catalog.FindItem(peer);
        if (knownPlayers.Contains(peer)) kind = CombatantKind.Player;
        else if (attacker?.Npc == true) kind = CombatantKind.Npc;
        else if (PilotLabel.Match(peer) is { Success: true } pilot)
        { peer = pilot.Groups["name"].Value.Trim(); kind = CombatantKind.Player; }
        string? weapon = null;
        StaticCombatItem? weaponInfo = null;
        // Prefer the longest exact catalog prefix; names themselves can contain a
        // dash. A quality-only suffix is never invented into a weapon or damage type.
        for (int end = suffix.Length; end > 0;)
        {
            string candidate = suffix[..end].Trim();
            if ((weaponInfo = catalog.FindItem(candidate)) is not null) { weapon = candidate; break; }
            int separator = suffix.LastIndexOf(" - ", end - 1, StringComparison.Ordinal);
            if (separator < 0) break;
            end = separator;
        }
        // Named missiles/charges/smartbombs take priority over an NPC's gun damage.
        // Never treat its unused superweapon or missile as part of a normal gun hit.
        bool qualityOnly = suffix is "Hits" or "Penetrates" or "Smashes" or "Grazes" or "Glances Off" or "Wrecks";
        bool outgoing = damage.Groups["direction"].Value is "to" or "nach" or "à" or "на" or "対象:" or "对";
        var source = !repair && weaponInfo?.Damage != DamageTypes.None && weaponInfo is not null ? weaponInfo
            : !repair && !outgoing && qualityOnly && kind == CombatantKind.Npc ? attacker : null;
        var types = source?.Damage ?? DamageTypes.None;
        var type = types switch { DamageTypes.None => CombatDamageType.Unknown, DamageTypes.EM => CombatDamageType.EM,
            DamageTypes.Thermal => CombatDamageType.Thermal, DamageTypes.Kinetic => CombatDamageType.Kinetic,
            DamageTypes.Explosive => CombatDamageType.Explosive, _ => CombatDamageType.Mixed };
        return result with { Amount = amount, Counterparty = peer, Kind = kind,
            Effect = !repair ? CombatEffect.Damage : damage.Groups["effect"].Value switch
                { "armor repaired" => CombatEffect.ArmorRepair, "shield boosted" => CombatEffect.ShieldRepair, _ => CombatEffect.HullRepair },
            Weapon = weapon, Platform = weaponInfo?.Platform ?? source?.Platform ?? WeaponPlatform.Unknown, DamageType = type, DamageTypes = types,
            DamageEvidence = types == DamageTypes.None ? DamageEvidence.Unavailable : source == weaponInfo ? DamageEvidence.NamedItem : DamageEvidence.NpcAttack,
            DamageSourceTypeId = source?.Id > 0 ? source.Id : null, StaticDataBuild = types == DamageTypes.None ? null : source?.Build ?? catalog.Build,
            Direction = outgoing ? DamageDirection.Outgoing : DamageDirection.Incoming };
    }

    public static bool ValidName(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && !value.Any(char.IsControl);
    private static bool TryTime(string value, out DateTimeOffset timestamp) => DateTimeOffset.TryParseExact(value,
        "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp);
}
