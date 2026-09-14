namespace EveOPreview.UI;

public sealed record SimulationFaction(long Id, string Name)
{ public override string ToString() => Name; }
public sealed record SimulationAttack(string? Weapon, WeaponPlatform Platform, DamageTypes DamageTypes,
    long SourceTypeId, DamageEvidence Evidence, double Amount, int CycleMilliseconds, int Build)
{
    public WeaponPlatform? LoggedPlatform { get; init; }
    public int DelayMilliseconds { get; init; }
    public int BurstDurationMilliseconds { get; init; }
    public int BurstIntervalMilliseconds { get; init; }
    public double RampPerCycle { get; init; }
    public double RampMaximum { get; init; }
}
public sealed record SimulationNpc(long Id, string Name, long? FactionId, IReadOnlyList<SimulationAttack> Attacks)
{ public override string ToString() => Name; }
public sealed record SimulationAmmo(long Id, string Name, DamageTypes DamageTypes, double Amount)
{
    public WeaponPlatform? LoggedPlatform { get; init; }
    public long? LoggedSourceTypeId { get; init; }
    public override string ToString() => Name;
}
public sealed record SimulationWeapon(long Id, string Name, SimulationAttack Attack,
    double DamageMultiplier, IReadOnlyList<long> AmmoIds)
{
    public string? ModelNote { get; init; }
    public override string ToString() => Name;
}
public sealed record SimulationSource(string Name, CombatantKind Kind, IReadOnlyList<SimulationAttack> Attacks)
{
    public IReadOnlyList<SimulationAttack> OutgoingAttacks { get; init; } = [];
}

/// <summary>Small, immutable projection of the installed SDE. No database or filesystem reaches the UI.</summary>
public sealed record CombatSimulationCatalog(int? Build, IReadOnlyList<SimulationFaction> Factions,
    IReadOnlyList<SimulationNpc> Npcs, IReadOnlyList<SimulationWeapon> Weapons, IReadOnlyList<SimulationAmmo> Ammo,
    string? Error = null)
{
    public static CombatSimulationCatalog Unavailable(string message) => new(null, [], [], [], [], message);

    // Resolve IDs again on the host. A stale UI selection must never silently use a
    // different ship, incompatible ammunition, or an invented damage composition.
    public IReadOnlyList<SimulationSource> Resolve(CombatSimulation request)
    {
        if (Build is null) throw new ArgumentException(Error ?? "Download FC static data in Data setup.");
        if (!request.Randomize && request.Effect != CombatEffect.Damage)
            return []; // Selected repairs do not depend on a damage weapon, ammunition or NPC.
        if (request.Kind == CombatantKind.Npc)
        {
            var npcs = Npcs.Where(x => (!request.NpcFactionId.HasValue || x.FactionId == request.NpcFactionId)
                && (!request.NpcTypeId.HasValue || x.Id == request.NpcTypeId)).ToArray();
            if (npcs.Length == 0) throw new ArgumentException("Choose an NPC ship from the selected faction.");
            var outgoing = request.Randomize || request.Direction == DamageDirection.Outgoing ? new[] { PlayerAttack(request) } : [];
            return npcs.Select(x => new SimulationSource(x.Name, CombatantKind.Npc, x.Attacks) { OutgoingAttacks = outgoing }).ToArray();
        }
        if (request.Kind != CombatantKind.Player) throw new ArgumentException("Choose NPC or Player.");
        return [new("Orion Voss", CombatantKind.Player, [PlayerAttack(request)])];
    }

    private SimulationAttack PlayerAttack(CombatSimulation request)
    {
        var weapon = Weapons.FirstOrDefault(x => x.Id == request.WeaponTypeId)
            ?? throw new ArgumentException("Choose a weapon.");
        var attack = weapon.Attack;
        if (weapon.AmmoIds.Count > 0)
        {
            var ammo = Ammo.FirstOrDefault(x => x.Id == request.AmmoTypeId && weapon.AmmoIds.Contains(x.Id))
                ?? throw new ArgumentException("Choose compatible ammunition.");
            // EVE logs launched ammunition by name, but turrets by module name.
            // Selected turret ammo determines the amount; the log cannot reveal
            // that loaded charge's composition to the damage display.
            bool launched = LogsAmmunition(attack.Platform);
            var types = launched ? ammo.DamageTypes : attack.DamageTypes;
            attack = attack with { Weapon = launched ? ammo.Name : weapon.Name, DamageTypes = types,
                SourceTypeId = types == DamageTypes.None ? 0 : launched ? ammo.LoggedSourceTypeId ?? ammo.Id : attack.SourceTypeId,
                LoggedPlatform = launched ? ammo.LoggedPlatform ?? attack.LoggedPlatform : attack.LoggedPlatform,
                Evidence = types == DamageTypes.None ? DamageEvidence.Unavailable : DamageEvidence.NamedItem,
                Amount = ammo.Amount * weapon.DamageMultiplier };
        }
        else if (request.AmmoTypeId.HasValue) throw new ArgumentException("This weapon does not use ammunition.");
        return attack with { Platform = attack.LoggedPlatform ?? attack.Platform };
    }

    public static bool LogsAmmunition(WeaponPlatform platform) => platform is WeaponPlatform.Rocket or WeaponPlatform.Missile or WeaponPlatform.Torpedo
        or WeaponPlatform.LightMissile or WeaponPlatform.HeavyMissile or WeaponPlatform.HeavyAssaultMissile
        or WeaponPlatform.CruiseMissile or WeaponPlatform.XLCruiseMissile or WeaponPlatform.XLTorpedo
        or WeaponPlatform.Bomb or WeaponPlatform.GuidedBomb or WeaponPlatform.StructureMissile or WeaponPlatform.DefenderMissile;
}
