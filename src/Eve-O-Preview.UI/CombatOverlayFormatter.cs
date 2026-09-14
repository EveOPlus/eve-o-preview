using System.Globalization;
using EveOPreview.Preview;

namespace EveOPreview.UI;

public sealed record CombatOverlayEvent(ParsedLogEntry Entry, bool Simulated = false);

/// <summary>Pure presentation, shared by the settings preview and real thumbnail.</summary>
public static class CombatOverlayFormatter
{
    /// <summary>Show every enabled appearance item together, independently of the selected simulation scenario.</summary>
    public static IReadOnlyList<OverlayStat> AppearanceSample(CombatSimulation request, CombatSimulationCatalog catalog,
        LogOverlayOptions options, int windowSeconds)
    {
        var sample = request with { FullTitle = "EVE - Preview", DurationSeconds = 0, Randomize = false, Effect = CombatEffect.Damage };
        var entries = new List<CombatOverlayEvent>();
        foreach (var direction in Enum.GetValues<DamageDirection>())
        {
            var damage = sample with { Direction = direction };
            IReadOnlyList<SimulationSource> sources = [];
            try { sources = catalog.Resolve(damage); }
            catch (ArgumentException) { } // Basic appearance remains available without a valid data selection.
            var sequence = new CombatSimulationSequence(damage, DateTimeOffset.UnixEpoch, 3, new Random(0), sources);
            entries.AddRange(sequence.DrainEvents().Select(x => new CombatOverlayEvent(x, true)));
        }
        var repairs = new CombatSimulationSequence(sample with { Effect = CombatEffect.ShieldRepair,
            RepairSourceCount = 3, MixedRepairTypes = true, BothRepairDirections = true }, DateTimeOffset.UnixEpoch, 3);
        entries.AddRange(repairs.DrainEvents().Select(x => new CombatOverlayEvent(x, true)));
        return SimulationEvents(entries, options, windowSeconds);
    }

    public static IReadOnlyList<OverlayStat> Simulation(CombatOverlayEvent notification, LogOverlayOptions options, int windowSeconds)
        => SimulationEvents([notification], options, windowSeconds);

    public static IReadOnlyList<OverlayStat> SimulationEvents(IReadOnlyList<CombatOverlayEvent> events, LogOverlayOptions options, int windowSeconds)
    {
        var categories = events.Where(x => x.Entry.Effect == CombatEffect.Damage).GroupBy(x => x.Entry.Kind).Select(group =>
        {
            double Dps(DamageDirection direction) => group.Where(x => x.Entry.Direction == direction).Sum(x => x.Entry.Amount) / Math.Clamp(windowSeconds, 1, 300);
            return new CombatCategory(group.Key, new(0, 0), new(Dps(DamageDirection.Incoming), Dps(DamageDirection.Outgoing)));
        }).ToArray();
        var repairRates = events.Where(x => x.Entry.Effect != CombatEffect.Damage).GroupBy(x => x.Entry.Effect).Select(group =>
        {
            double Rate(DamageDirection direction) => group.Where(x => x.Entry.Direction == direction).Sum(x => x.Entry.Amount) / Math.Clamp(windowSeconds, 1, 300);
            return new RepairRate(group.Key, new(Rate(DamageDirection.Incoming), Rate(DamageDirection.Outgoing)));
        }).ToArray();
        var snapshot = new CharacterCombatSnapshot("", null, null, null, null, null, categories, 0) { RepairRates = repairRates };
        CombatOverlayEvent? Latest(DamageDirection direction, bool repair) => events.LastOrDefault(x =>
            x.Entry.Direction == direction && (x.Entry.Effect != CombatEffect.Damage) == repair);
        return Meter(snapshot, options, Latest(DamageDirection.Incoming, false), Latest(DamageDirection.Outgoing, false));
    }

    /// <summary>Incoming and outgoing keep their slots even when empty. Alpha belongs to its direction's row.</summary>
    public static IReadOnlyList<OverlayStat> Meter(CharacterCombatSnapshot? character, LogOverlayOptions options,
        CombatOverlayEvent? incoming = null, CombatOverlayEvent? outgoing = null)
    {
        if (!options.Incoming && !options.Outgoing && !options.DamageEvents && !options.Repairs) return [];
        var appearance = options.GetAppearance();
        OverlayStat Row(bool isIncoming, CombatOverlayEvent? hit)
        {
            double Value(CombatCategory category) => isIncoming ? category.Dps.Incoming : category.Dps.Outgoing;
            double total = character?.Categories.Sum(Value) ?? 0;
            bool showDps = (isIncoming ? options.Incoming : options.Outgoing) && total > 0;
            bool showAlpha = options.DamageEvents && hit?.Entry.Amount > 0;
            if (!showDps && !showAlpha) return new("", "") { Visible = false };
            string direction = isIncoming ? "In" : "Out";
            string value = "";
            if (showDps)
            {
                double Kind(CombatantKind kind) => character?.Categories.Where(c => c.Kind == kind).Sum(Value) ?? 0;
                value = options.Breakdown
                    ? $"NPC {Kind(CombatantKind.Npc).ToString("N0", CultureInfo.InvariantCulture)} / Player {Kind(CombatantKind.Player).ToString("N0", CultureInfo.InvariantCulture)}"
                    : total < .01 ? "<0.01" : total.ToString(total < 1 ? "0.##" : "N0", CultureInfo.InvariantCulture);
            }
            return new(showDps ? direction + " DPS" : direction, value,
                Color(isIncoming ? appearance.IncomingColor : appearance.OutgoingColor))
                { Suffix = showAlpha ? Event(hit!, options) : null };
        }
        int[] order = options.RowOrder switch
        {
            CombatRowOrder.OutgoingIncomingRepairs => [1, 0, 2], CombatRowOrder.IncomingRepairsOutgoing => [0, 2, 1],
            CombatRowOrder.OutgoingRepairsIncoming => [1, 2, 0], CombatRowOrder.RepairsIncomingOutgoing => [2, 0, 1],
            CombatRowOrder.RepairsOutgoingIncoming => [2, 1, 0], _ => [0, 1, 2]
        };
        OverlayStat Repairs(bool isIncoming)
        {
            OverlayStat? row = null;
            // Build backwards so the visible pairs remain shield, armour, hull.
            foreach (var effect in new[] { CombatEffect.HullRepair, CombatEffect.ArmorRepair, CombatEffect.ShieldRepair })
            {
                double value = character?.RepairRates.Where(x => x.Effect == effect)
                    .Sum(x => isIncoming ? x.PerSecond.Incoming : x.PerSecond.Outgoing) ?? 0;
                if (value <= 0) continue;
                var style = appearance.Repairs.GetValueOrDefault(effect) ?? CombatAppearance.Preset("Classic").Repairs[effect];
                string amount = value < .01 ? "<0.01" : value.ToString(value < 1 ? "0.##" : "N0", CultureInfo.InvariantCulture);
                row = new("", amount, Color(style.Color), appearance.ShowDamageIcon ? style.Icon : OverlaySymbol.None, Color(style.Color))
                    { Suffix = row, PrefixIcons = true };
            }
            return row is null ? new("", "") { Visible = false }
                : new(isIncoming ? "IN" : "OUT", "", Color(isIncoming ? appearance.IncomingColor : appearance.OutgoingColor)) { Suffix = row };
        }
        // Rates have fixed direction slots, independently of the alpha event lifetime.
        var rows = new[] { Row(true, incoming), Row(false, outgoing), Repairs(true), Repairs(false) };
        return order.SelectMany(i => i == 2 ? options.Repairs ? new[] { rows[2], rows[3] } : [] : new[] { rows[i] }).ToArray();
    }

    public static OverlayStat Event(CombatOverlayEvent notification, LogOverlayOptions options)
    {
        var entry = notification.Entry;
        var appearance = options.GetAppearance();
        var fallback = CombatAppearance.Preset("Classic");
        bool incoming = entry.Direction == DamageDirection.Incoming;
        bool repair = entry.Effect != CombatEffect.Damage;
        var damage = appearance.Damage.GetValueOrDefault(entry.DamageType) ?? fallback.Damage[entry.DamageType];
        var weapon = appearance.WeaponStyle(entry.Platform);
        var rep = appearance.Repairs.GetValueOrDefault(entry.Effect)
            ?? new CombatVisualStyle("#DFDFDC", OverlaySymbol.Hull);
        string textColor = repair ? rep.TextColor ?? rep.Color : appearance.TextColorMode switch
        {
            CombatTextColorMode.DamageType => damage.TextColor ?? damage.Color,
            CombatTextColorMode.WeaponPlatform when entry.Platform != WeaponPlatform.Unknown => weapon.TextColor ?? weapon.Color,
            _ => incoming ? appearance.IncomingColor : appearance.OutgoingColor
        };
        string label = repair ? incoming ? "IN" : "OUT" : "";
        string detail = repair ? entry.Effect switch
            { CombatEffect.ShieldRepair => "shield rep", CombatEffect.ArmorRepair => "armor rep", _ => "hull rep" }
            : "";
        var row = new OverlayStat(label, (repair ? "" : "α ") + entry.Amount.ToString("N0", CultureInfo.InvariantCulture) + (repair ? " " + detail : ""), Color(textColor),
            appearance.ShowDamageIcon ? repair ? rep.Icon : damage.Icon : OverlaySymbol.None, Color(repair ? rep.Color : damage.Color),
            !repair && appearance.ShowWeaponIcon && entry.Platform != WeaponPlatform.Unknown ? weapon.Icon : OverlaySymbol.None, Color(weapon.Color));
        if (repair) return row;
        // A module-only turret hit identifies its platform even though the log
        // omits ammunition. Show only established facts: the weapon, damage
        // composition, both, or no icons. Missing metadata never gets a question mark.
        bool showWeapon = appearance.ShowWeaponIcon && entry.Platform != WeaponPlatform.Unknown && weapon.Icon != OverlaySymbol.None;
        return DamageIcons(row, entry.EffectiveDamageTypes, appearance, showWeapon ? weapon : null);
    }

    private static OverlayStat DamageIcons(OverlayStat row, DamageTypes types, CombatAppearance appearance, CombatVisualStyle? weapon = null)
    {
        var icons = new List<(OverlaySymbol Icon, uint Color)>();
        var fallback = CombatAppearance.Preset("Classic");
        if (appearance.ShowDamageIcon)
        {
            foreach (var pair in new[] { (DamageTypes.EM, CombatDamageType.EM), (DamageTypes.Thermal, CombatDamageType.Thermal),
                (DamageTypes.Kinetic, CombatDamageType.Kinetic), (DamageTypes.Explosive, CombatDamageType.Explosive) })
                if ((types & pair.Item1) != 0)
                {
                    var style = appearance.Damage.GetValueOrDefault(pair.Item2) ?? fallback.Damage[pair.Item2];
                    icons.Add((style.Icon, Color(style.Color)));
                }
        }
        if (weapon is not null) icons.Add((weapon.Icon, Color(weapon.Color)));
        while (icons.Count < 5) icons.Add((OverlaySymbol.None, 0xFFFFFFFF));
        return row with { Icon = icons[0].Icon, IconColor = icons[0].Color, SecondaryIcon = icons[1].Icon, SecondaryIconColor = icons[1].Color,
            ThirdIcon = icons[2].Icon, ThirdIconColor = icons[2].Color, FourthIcon = icons[3].Icon, FourthIconColor = icons[3].Color,
            FifthIcon = icons[4].Icon, FifthIconColor = icons[4].Color };
    }

    public static uint Color(string text) => text is { Length: 7 } && text[0] == '#'
        && uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint color) ? 0xFF000000 | color : 0xFFFFFFFF;
}
