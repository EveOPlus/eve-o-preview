using System.Globalization;
using System.Xml.Linq;

namespace EveOPreview.Preview;

public enum OverlaySymbol { None, Unknown, Lightning, Heat, Kinetic, Explosion, Mixed, Rocket, Missile, Blaster, Railgun, Laser, Projectile, Drone, Shield, Armor, Hull, EveEM, EveThermal, EveKinetic, EveExplosive, Artillery, Autocannon, Disintegrator, Torpedo, Smartbomb, LightMissile, HeavyMissile, HeavyAssaultMissile, CruiseMissile, XLCruiseMissile, XLTorpedo, PulseLaser, BeamLaser, Fighter, Vorton, Bomb, GuidedBomb, StructureMissile, PointDefense, Doomsday, Lance, Reaper, Bosonic, DefenderMissile, Hybrid }

/// <summary>16x16 stroke glyphs and embedded damage/platform/repair SVG polygons shared by both renderers.</summary>
public static class OverlaySymbols
{
    private static readonly IReadOnlyDictionary<OverlaySymbol, IReadOnlyList<float[]>> SvgPolygons =
        new Dictionary<OverlaySymbol, IReadOnlyList<float[]>>
        {
            [OverlaySymbol.EveEM] = LoadSvg("em"), [OverlaySymbol.EveThermal] = LoadSvg("thermal"),
            [OverlaySymbol.EveKinetic] = LoadSvg("kinetic"), [OverlaySymbol.EveExplosive] = LoadSvg("explosive"),
            [OverlaySymbol.Shield] = LoadSvg("shield", "Repairs"),
            [OverlaySymbol.Armor] = LoadSvg("armour", "Repairs"),
            [OverlaySymbol.Hull] = LoadSvg("hull", "Repairs"),
            // Fixed platform symbols, not icons selected from a hit's ammunition.
            [OverlaySymbol.Rocket] = LoadSvg("rocket", "Weapons"), [OverlaySymbol.Missile] = LoadSvg("missile", "Weapons"),
            [OverlaySymbol.Torpedo] = LoadSvg("torpedo", "Weapons"), [OverlaySymbol.Blaster] = LoadSvg("blaster", "Weapons"),
            [OverlaySymbol.Railgun] = LoadSvg("railgun", "Weapons"), [OverlaySymbol.Laser] = LoadSvg("laser", "Weapons"),
            [OverlaySymbol.Autocannon] = LoadSvg("autocannon", "Weapons"), [OverlaySymbol.Artillery] = LoadSvg("artillery", "Weapons"),
            [OverlaySymbol.Disintegrator] = LoadSvg("disintegrator", "Weapons"), [OverlaySymbol.Drone] = LoadSvg("drone", "Weapons"),
            [OverlaySymbol.Smartbomb] = LoadSvg("smartbomb", "Weapons"),
            [OverlaySymbol.LightMissile] = LoadSvg("light-missile", "Weapons"),
            [OverlaySymbol.HeavyMissile] = LoadSvg("heavy-missile", "Weapons"),
            [OverlaySymbol.HeavyAssaultMissile] = LoadSvg("heavy-assault-missile", "Weapons"),
            [OverlaySymbol.CruiseMissile] = LoadSvg("cruise-missile", "Weapons"),
            [OverlaySymbol.XLCruiseMissile] = LoadSvg("xl-cruise-missile", "Weapons"),
            [OverlaySymbol.XLTorpedo] = LoadSvg("xl-torpedo", "Weapons"),
            [OverlaySymbol.PulseLaser] = LoadSvg("pulse-laser", "Weapons"),
            [OverlaySymbol.BeamLaser] = LoadSvg("beam-laser", "Weapons"),
            [OverlaySymbol.Fighter] = LoadSvg("fighter", "Weapons"),
            [OverlaySymbol.Vorton] = LoadSvg("vorton", "Weapons"),
            [OverlaySymbol.Bomb] = LoadSvg("bomb", "Weapons"),
            [OverlaySymbol.GuidedBomb] = LoadSvg("guided-bomb", "Weapons"),
            [OverlaySymbol.StructureMissile] = LoadSvg("structure-missile", "Weapons"),
            [OverlaySymbol.PointDefense] = LoadSvg("point-defense", "Weapons"),
            [OverlaySymbol.Doomsday] = LoadSvg("doomsday", "Weapons"),
            [OverlaySymbol.Lance] = LoadSvg("lance", "Weapons"),
            [OverlaySymbol.Reaper] = LoadSvg("reaper", "Weapons"),
            [OverlaySymbol.Bosonic] = LoadSvg("bosonic", "Weapons"),
            [OverlaySymbol.DefenderMissile] = LoadSvg("defender-missile", "Weapons"),
            [OverlaySymbol.Hybrid] = LoadSvg("hybrid", "Weapons"),
            [OverlaySymbol.Projectile] = LoadSvg("projectile", "Weapons")
        };

    public static IReadOnlyList<float[]> Fills(OverlaySymbol symbol) => SvgPolygons.GetValueOrDefault(symbol) ?? [];

    private static IReadOnlyList<float[]> LoadSvg(string name, string folder = "Damage")
    {
        using var resource = typeof(OverlaySymbols).Assembly.GetManifestResourceStream($"EveOPreview.Preview.Assets.{folder}.{name}.svg")
            ?? throw new InvalidOperationException("Missing embedded overlay symbol: " + folder + "/" + name);
        XNamespace svg = "http://www.w3.org/2000/svg";
        return XDocument.Load(resource).Descendants(svg + "polygon").Select(p => p.Attribute("points")!.Value
            .Split([' ', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToArray();
    }

    public static IReadOnlyList<float[]> Strokes(OverlaySymbol symbol) => symbol switch
    {
        OverlaySymbol.Lightning => [[9, 1, 3, 9, 8, 9, 6, 15, 13, 6, 8, 6, 9, 1]],
        OverlaySymbol.Heat => [[8, 1, 6, 6, 3, 4, 2, 10, 4, 14, 11, 14, 14, 10, 11, 5, 10, 9, 8, 1]],
        OverlaySymbol.Kinetic => [[2, 8, 7, 3, 14, 3, 14, 13, 7, 13, 2, 8], [6, 5, 6, 11]],
        OverlaySymbol.Explosion => [[8, 1, 10, 5, 15, 3, 12, 8, 15, 13, 10, 11, 8, 15, 6, 11, 1, 13, 4, 8, 1, 3, 6, 5, 8, 1]],
        OverlaySymbol.Mixed => [[8, 1, 15, 8, 8, 15, 1, 8, 8, 1], [8, 3, 8, 13], [3, 8, 13, 8]],
        OverlaySymbol.Unknown => [[4, 4, 5, 2, 11, 2, 12, 4, 11, 6, 8, 8, 8, 10], [8, 13, 8, 14]],
        _ => []
    };
}
