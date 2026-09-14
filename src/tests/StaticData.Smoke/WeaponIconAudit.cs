using System.Net;
using System.Text.Json;
using EveOPreview.Preview;
using EveOPreview.Services.Logs;
using EveOPreview.Services.StaticData;
using EveOPreview.UI;

// All live aliases, including named/meta/faction/officer variants that deliberately
// do not appear in the simulator. Uses both lookup routes and the real formatter.
internal static class WeaponIconAudit
{
    public static int Run(StaticDataService data)
    {
        var offline = EveLogCatalog.Load();
        var installed = EveLogCatalog.Load(data);
        var appearance = CombatAppearance.Preset("Classic") with { ShowWeaponIcon = true };
        var options = new LogOverlayOptions { Advanced = true, CustomAppearance = appearance };
        var coverage = new HashSet<WeaponPlatform>();
        int checkedEntries = 0, weaponOnly = 0;
        foreach (var alias in offline.Weapons)
        foreach (var catalog in new[] { offline, installed })
        foreach (string direction in new[] { "from", "to" })
        {
            string line = "[ 2026.09.14 01:00:00 ] (combat) <b>125</b> " + direction
                + " Other Pilot - " + WebUtility.HtmlEncode(alias.Key) + " - Hits";
            var entry = EveLogParser.Parse(line, new("Preview"), false, catalog, new HashSet<string>())
                ?? throw new InvalidOperationException("Named weapon entry was not parsed.");
            if (entry.Platform != alias.Value.Platform || entry.Weapon != alias.Key)
                throw new InvalidOperationException("Weapon alias failed lookup: " + alias.Key);
            checkedEntries++;
            if (entry.Platform == WeaponPlatform.Unknown) continue; // Keep genuinely conflicting aliases unresolved.
            coverage.Add(entry.Platform);
            var icons = CombatOverlayFormatter.Event(new(entry), options).Icons().Select(x => x.Symbol).ToArray();
            if (!icons.Contains(appearance.WeaponStyle(entry.Platform).Icon) || icons.Contains(OverlaySymbol.Unknown))
                throw new InvalidOperationException("Known weapon was presented as unknown: " + alias.Key);
            if (entry.EffectiveDamageTypes == DamageTypes.None)
            {
                weaponOnly++;
                if (icons.Length != 1) throw new InvalidOperationException("Module-only hit must show its known weapon alone.");
            }
        }
        if (!coverage.SetEquals(Enum.GetValues<WeaponPlatform>().Where(x => x != WeaponPlatform.Unknown)))
            throw new InvalidOperationException("Weapon icon audit did not cover every platform.");
        Console.WriteLine(JsonSerializer.Serialize(new { Passed = true, data.Build, Aliases = offline.Weapons.Count,
            CheckedEntries = checkedEntries, WeaponOnlyEntries = weaponOnly, Platforms = coverage.Count }));
        return 0;
    }
}
