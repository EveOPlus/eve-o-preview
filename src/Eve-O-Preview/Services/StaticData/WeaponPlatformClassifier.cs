#nullable enable
using System;
using System.Collections.Generic;
using EveOPreview.UI;

namespace EveOPreview.Services.StaticData;

/// <summary>SDE group/effect evidence, shared by import, local index upgrades and simulation.
/// Names only split families inside a known weapon group; never search arbitrary item names.</summary>
public static class WeaponPlatformClassifier
{
    public static WeaponPlatform Classify(int group, int category, string groupName, string name,
        DamageTypes damage, IReadOnlySet<int> effects)
    {
        var fixedGroup = group switch
        {
            387 or 648 or 507 => WeaponPlatform.Rocket,
            384 or 394 or 653 or 509 or 511 => WeaponPlatform.LightMissile,
            385 or 395 or 655 or 510 or 1245 => WeaponPlatform.HeavyMissile,
            772 or 654 or 771 => WeaponPlatform.HeavyAssaultMissile,
            386 or 396 or 656 or 506 => WeaponPlatform.CruiseMissile,
            1019 or 1678 or 1674 => WeaponPlatform.XLCruiseMissile,
            89 or 657 or 508 or 1673 => WeaponPlatform.Torpedo,
            476 or 1010 or 1677 or 524 => WeaponPlatform.XLTorpedo,
            88 or 1158 or 512 => WeaponPlatform.DefenderMissile,
            90 or 862 or 863 or 864 => WeaponPlatform.Bomb,
            1548 or 1328 => WeaponPlatform.GuidedBomb,
            1546 or 1547 or 1327 or 1562 => WeaponPlatform.StructureMissile,
            1330 or 4186 => WeaponPlatform.PointDefense,
            1986 or 1987 or 1989 => WeaponPlatform.Disintegrator,
            4060 or 4061 or 4062 => WeaponPlatform.Vorton,
            100 => WeaponPlatform.Drone,
            549 or 1023 or 1537 or 1652 or 1653 or 4777 or 4778 or 4779 => WeaponPlatform.Fighter,
            72 => WeaponPlatform.Smartbomb,
            372 => WeaponPlatform.Autocannon, 376 => WeaponPlatform.Artillery,
            373 => WeaponPlatform.Railgun, 377 => WeaponPlatform.Blaster,
            374 => WeaponPlatform.BeamLaser, 375 => WeaponPlatform.PulseLaser,
            83 => WeaponPlatform.Projectile, 85 => WeaponPlatform.Hybrid, 86 => WeaponPlatform.Laser,
            _ => WeaponPlatform.Unknown
        };
        if (fixedGroup != WeaponPlatform.Unknown) return fixedGroup;
        if (group is 588 or 1333)
        {
            // PANIC and transportation share Super Weapon. A group/name is not proof of damage.
            if (damage == DamageTypes.None) return WeaponPlatform.Unknown;
            if (effects.Contains(6472) || effects.Contains(11691)) return WeaponPlatform.Lance;
            if (effects.Contains(6201)) return WeaponPlatform.Reaper;
            if (effects.Contains(6473)) return WeaponPlatform.Bosonic;
            return WeaponPlatform.Doomsday;
        }
        name = name.ToLowerInvariant();
        // Include old starbase batteries, keeping the same platform irrespective of size/faction.
        if (group is 53 or 430 || groupName == "Energy Weapon")
            return name.Contains("pulse") ? WeaponPlatform.PulseLaser
                : name.Contains("beam") || name.Contains("tachyon") ? WeaponPlatform.BeamLaser : WeaponPlatform.Laser;
        if (group is 74 or 449 || groupName == "Hybrid Weapon")
            return name.Contains("blaster") ? WeaponPlatform.Blaster : name.Contains("rail") ? WeaponPlatform.Railgun : WeaponPlatform.Hybrid;
        if (group is 55 or 426 || groupName == "Projectile Weapon")
            return name.Contains("artillery") ? WeaponPlatform.Artillery : name.Contains("cannon") ? WeaponPlatform.Autocannon : WeaponPlatform.Projectile;
        if (group == 417)
            return name.Contains("cruise") ? WeaponPlatform.CruiseMissile : name.Contains("torpedo") ? WeaponPlatform.Torpedo : WeaponPlatform.Missile;
        // Narrow generic fallbacks also support older exports; never classify guidance/rig groups.
        return groupName switch
        {
            "Missile" or "Missile Launcher" => WeaponPlatform.Missile,
            "Smart Bomb" => WeaponPlatform.Smartbomb,
            _ => WeaponPlatform.Unknown
        };
    }

    public static DamageTypes Mask(IReadOnlyDictionary<int, double> attributes, params int[] ids)
    {
        DamageTypes result = 0;
        for (int i = 0; i < ids.Length; i++) if (attributes.GetValueOrDefault(ids[i]) > 0) result |= (DamageTypes)(1 << i);
        return result;
    }

    // A fighter name alone cannot distinguish its abilities. Keep composition only
    // when all damaging abilities agree, rather than unioning different attacks.
    public static DamageTypes FighterDamage(IReadOnlyDictionary<int, double> attributes)
    {
        DamageTypes result = 0;
        foreach (var ids in new[] { new[] { 2227, 2228, 2229, 2230 }, new[] { 2131, 2132, 2133, 2134 }, new[] { 2325, 2326, 2327, 2328 } })
        {
            var mask = Mask(attributes, ids);
            if (mask == DamageTypes.None) continue;
            if (result != DamageTypes.None && result != mask) return DamageTypes.None;
            result = mask;
        }
        return result;
    }
}
