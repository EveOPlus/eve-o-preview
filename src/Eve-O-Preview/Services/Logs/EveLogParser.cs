#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    public long? FindTypeId(string name) => FindItem(name) is { Id: > 0 } item ? item.Id : null;
    public long? FindSystem(string name) => StaticData?.Build is not null ? StaticData.FindSystem(name)
        : Systems.TryGetValue(name, out long id) ? id : null;
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
    DateTimeOffset? Session = null, bool Ambiguous = false, LogLanguage Language = LogLanguage.Automatic);

/// <summary>Only complete lines enter this parser. Unsupported text remains available as a game entry.</summary>
public static class EveLogParser
{
    private static readonly TimeSpan RegexLimit = TimeSpan.FromMilliseconds(50);
    private static Regex Pattern(string value) => new(value, RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexLimit);
    private static readonly Regex Timestamp = Pattern(@"^\[\s*(?<time>\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2})\s*\]\s*(?<body>.*)$");
    private static readonly Regex GameEntry = Pattern(@"^\((?<category>[^)]+)\)\s*(?<text>.*)$");
    private static readonly Regex PilotLabel = Pattern(@"^(?<name>.+?)\s*\[[^\[\]]+\]\s*\([^()]+\)$");
    private static readonly Regex ChatLine = Pattern(@"^(?<sender>[^<>]+?)\s*>\s*(?<text>.*)$");

    public static LogHeader ReadHeader(LogHeader header, string line)
    {
        if (line.Length > 4096) return header;
        if (line.AsSpan().TrimStart().TrimStart('\uFEFF').StartsWith("[", StringComparison.Ordinal)) return header;
        line = EveLogText.PlainText(line);
        int colon = line.IndexOfAny([':', '：']);
        if (colon < 0) return header;
        string key = line[..colon].Trim(), value = line[(colon + 1)..].Trim();
        var listener = EveLogLanguages.All.FirstOrDefault(x => key.Equals(x.Listener, StringComparison.OrdinalIgnoreCase));
        if (listener is not null && ValidName(value))
            return header with { Listener = header.Listener ?? value,
                Language = listener.Language,
                Ambiguous = header.Ambiguous || (header.Listener is not null && !value.Equals(header.Listener, StringComparison.OrdinalIgnoreCase)) };
        if (EveLogLanguages.All.Any(x => x.ChannelKeys.Contains(key, StringComparer.OrdinalIgnoreCase)))
            return header with { Channel = value };
        var session = EveLogLanguages.All.FirstOrDefault(x => key.Equals(x.Session, StringComparison.OrdinalIgnoreCase));
        if (session is not null && TryTime(value, out var time))
            return header with { Session = time, Language = header.Language == LogLanguage.Automatic ? session.Language : header.Language };
        return header;
    }

    public static ParsedLogEntry? Parse(string line, LogHeader header, bool chat, EveLogCatalog catalog,
        IReadOnlySet<string> knownPlayers, LogLanguage language = LogLanguage.Automatic)
    {
        if (header.Ambiguous || header.Listener is null) return null;
        if (line.Length > CompleteLogReader.MaximumLineBytes || !Enum.IsDefined(language)) return null;
        var match = Timestamp.Match(line.TrimStart('\uFEFF'));
        if (!match.Success || !TryTime(match.Groups["time"].Value, out var time)) return null;
        string body = match.Groups["body"].Value;
        if (chat)
        {
            // Never accept user chat mentioning a system, or messages from another channel.
            if (!IsLocalChannel(header.Channel)) return null;
            // Inspect the original sender before stripping message markup. A player
            // quoting a system message, even with tags, is never location evidence.
            var chatLine = ChatLine.Match(body);
            if (!chatLine.Success) return null;
            string sender = chatLine.Groups["sender"].Value.Trim();
            string text = EveLogText.PlainText(chatLine.Groups["text"].Value);
            if (text.Length > 4096) return null;
            foreach (var definition in EveLogLanguages.Candidates(language, header.Language))
            {
                if (sender != definition.SystemSender) continue;
                var local = definition.LocalMessage.Match(text);
                if (!local.Success) continue;
                string name = local.Groups["system"].Value.Trim();
                // Local can emit the localized-name marker without a wrapper.
                // Remove it only when the remaining name is an exact SDE system.
                if (name.EndsWith('*') && catalog.FindSystem(name) is null
                    && catalog.FindSystem(name[..^1]) is not null) name = name[..^1];
                if (!ValidName(name) || catalog.FindSystem(name) is not { } system) return null;
                return new(time, header.Listener, "location", text, SolarSystem: name, SolarSystemId: system)
                    { EventKind = LogEventKind.SystemChange, Language = definition.Language };
            }
            return null;
        }
        var entry = GameEntry.Match(body);
        if (!entry.Success) return null;
        string category = entry.Groups["category"].Value;
        string original = entry.Groups["text"].Value;
        string plain = EveLogText.PlainText(original);
        var result = new ParsedLogEntry(time, header.Listener, category, plain.Length > 4096 ? plain[..4096] : plain)
            { Language = language == LogLanguage.Automatic ? header.Language : language };
        // Retain a bounded diagnostic, but never parse the prefix of a truncated line.
        if (plain.Length > 4096) return result;
        foreach (var definition in EveLogLanguages.Candidates(language, header.Language))
        foreach (var rule in definition.ByCategory.GetValueOrDefault(category) ?? [])
        {
            var parsed = rule.Text.Match(plain);
            if (!parsed.Success) continue;
            // Japanese's current source/target fragments have identical wording.
            // Only the leading damage number's client color resolves that ambiguity;
            // colors inside an overview name cannot change the direction.
            if (rule.RequiresDamageColor && EveLogText.DamageDirection(original) != rule.Direction) continue;
            var recognized = result with { Language = EveLogLanguages.Resolve(language, header.Language, definition.Language, rule),
                EventKind = rule.Kind, MessageId = rule.MessageId };
            if (rule.Kind is LogEventKind.Damage or LogEventKind.Repair)
                return Combat(recognized, parsed, rule, definition, catalog, knownPlayers);
            if (rule.Kind == LogEventKind.SystemChange)
            {
                string name = parsed.Groups["system"].Value.Trim();
                if (ValidName(name) && catalog.FindSystem(name) is { } system)
                    return recognized with { SolarSystem = name, SolarSystemId = system };
                return result;
            }
            if (rule.Kind is LogEventKind.Mining or LogEventKind.MiningResidue or LogEventKind.Bounty or LogEventKind.Capacitor)
            {
                if (!TryQuantity(parsed.Groups["quantity"].Value, out var quantity)) return result;
                decimal? residue = null;
                if (parsed.Groups["residue"].Success)
                {
                    if (!TryQuantity(parsed.Groups["residue"].Value, out var wasted)) return result;
                    residue = wasted;
                }
                string? item = parsed.Groups["item"].Success ? parsed.Groups["item"].Value.Trim() : null;
                return recognized with { Quantity = quantity, ResidueQuantity = residue,
                    Unit = rule.Kind == LogEventKind.Bounty ? "ISK" : rule.Kind == LogEventKind.Capacitor ? "GJ" : "units",
                    ItemName = item, ItemTypeId = item is null ? null : catalog.FindTypeId(item) };
            }
            return recognized;
        }
        return result;
    }

    public static bool IsLocalChannel(string? name) => EveLogLanguages.IsLocalChannel(name);
    public static bool IsLocalFile(string name) => EveLogLanguages.IsLocalFile(name);

    internal static ParsedLogEntry Reparse(ParsedLogEntry entry, EveLogCatalog catalog, IReadOnlySet<string> players)
    {
        string text = System.Net.WebUtility.HtmlEncode(entry.Text);
        // Stored history is plain text. Preserve already established direction
        // evidence while refreshing SDE metadata for an ambiguous Japanese hit.
        if (entry.Language == LogLanguage.Japanese && entry.EventKind == LogEventKind.Damage && entry.Direction is { } direction)
            text = (direction == DamageDirection.Incoming ? "<color=0xffcc0000>" : "<color=0xff00ffff>") + text;
        return Parse($"[ {entry.Timestamp.UtcDateTime:yyyy.MM.dd HH:mm:ss} ] ({entry.Category}) {text}",
            new(entry.Character, Language: entry.Language), false, catalog, players) ?? entry;
    }
    private static bool TryQuantity(string text, out decimal quantity) => decimal.TryParse(text,
        NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out quantity)
        && quantity is >= 0 and <= 1_000_000_000_000_000m;

    private static ParsedLogEntry Combat(ParsedLogEntry result, Match damage, LogMessagePattern rule,
        LogLanguageDefinition definition, EveLogCatalog catalog, IReadOnlySet<string> knownPlayers)
    {
        bool repair = rule.Kind == LogEventKind.Repair;
        if (!double.TryParse(damage.Groups["amount"].Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out double amount) || !double.IsFinite(amount) || amount < 0 || amount > 1_000_000_000)
            return result with { EventKind = LogEventKind.Unknown };
        string peer = damage.Groups["peer"].Value.Trim();
        string suffix = damage.Groups["suffix"].Value;
        // Custom overview labels may contain the same " - " delimiter as the
        // weapon suffix. Recover that boundary from an exact SDE item, not from
        // tag balance or an assumed pilot/corporation/ship layout.
        if (suffix.Contains(" - ", StringComparison.Ordinal))
        {
            string combined = peer + " - " + suffix;
            int itemEnd = combined.Length;
            int qualityStart = combined.LastIndexOf(" - ", StringComparison.Ordinal);
            string quality = combined[(qualityStart + 3)..];
            if (!repair && (definition.HitQualities.Contains(quality, StringComparer.Ordinal)
                || EveLogLanguages.All[0].HitQualities.Contains(quality, StringComparer.Ordinal))) itemEnd = qualityStart;
            for (int split = combined.IndexOf(" - ", StringComparison.Ordinal); split >= 0 && split + 3 < itemEnd;
                split = combined.IndexOf(" - ", split + 3, StringComparison.Ordinal))
            {
                if (catalog.FindItem(combined[(split + 3)..itemEnd].Trim()) is null) continue;
                peer = combined[..split].Trim(); suffix = combined[(split + 3)..]; break;
            }
        }
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
        bool qualityOnly = definition.HitQualities.Contains(suffix, StringComparer.Ordinal)
            || EveLogLanguages.All[0].HitQualities.Contains(suffix, StringComparer.Ordinal);
        bool outgoing = rule.Direction == DamageDirection.Outgoing;
        var source = !repair && weaponInfo?.Damage != DamageTypes.None && weaponInfo is not null ? weaponInfo
            : !repair && !outgoing && qualityOnly && kind == CombatantKind.Npc ? attacker : null;
        var types = source?.Damage ?? DamageTypes.None;
        var type = types switch { DamageTypes.None => CombatDamageType.Unknown, DamageTypes.EM => CombatDamageType.EM,
            DamageTypes.Thermal => CombatDamageType.Thermal, DamageTypes.Kinetic => CombatDamageType.Kinetic,
            DamageTypes.Explosive => CombatDamageType.Explosive, _ => CombatDamageType.Mixed };
        return result with { Amount = amount, Counterparty = peer, Kind = kind,
            Effect = rule.Effect,
            Weapon = weapon, Platform = weaponInfo?.Platform ?? source?.Platform ?? WeaponPlatform.Unknown, DamageType = type, DamageTypes = types,
            DamageEvidence = types == DamageTypes.None ? DamageEvidence.Unavailable : source == weaponInfo ? DamageEvidence.NamedItem : DamageEvidence.NpcAttack,
            DamageSourceTypeId = source?.Id > 0 ? source.Id : null, StaticDataBuild = types == DamageTypes.None ? null : source?.Build ?? catalog.Build,
            Direction = outgoing ? DamageDirection.Outgoing : DamageDirection.Incoming };
    }

    public static bool ValidName(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && !value.Any(char.IsControl);
    private static bool TryTime(string value, out DateTimeOffset timestamp) => DateTimeOffset.TryParseExact(value,
        "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp);
}
