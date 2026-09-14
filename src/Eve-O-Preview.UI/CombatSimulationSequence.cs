namespace EveOPreview.UI;

/// <summary>A finite presentation-only sequence. It has no ingestion, history or persistence dependency.</summary>
public sealed class CombatSimulationSequence
{
    private readonly CombatSimulation _request;
    private readonly Random _random;
    private readonly List<ParsedLogEntry> _pending = new();
    private DateTimeOffset _next;
    private DateTimeOffset _nextRepair;
    private int _repairIndex;
    private readonly int _repairSampleSeconds;
    private bool CycleRepairs => _request.DurationSeconds > 0 && _request.RepairSourceCount > 1;
    private readonly SimulationSource? _source;
    private readonly (SimulationAttack Attack, DamageDirection? Direction)[] _attacks;
    private readonly DateTimeOffset[] _attackTimes;
    private readonly DateTimeOffset[] _cycleStarts;
    private readonly int[] _burstHits;
    private readonly int[] _attackCounts;
    public DateTimeOffset Until { get; }
    public string FullTitle => _request.FullTitle;

    public CombatSimulationSequence(CombatSimulation request, DateTimeOffset now, int eventDurationSeconds, Random? random = null,
        IReadOnlyList<SimulationSource>? sources = null, int windowSeconds = 10)
    {
        _request = request; _random = random ?? new Random(); _next = now; _nextRepair = now.AddMilliseconds(750);
        // Let the previous sample leave the real rate window before switching
        // between single and combined repairs. Damage keeps its own cadence.
        _repairSampleSeconds = Math.Max(3, Math.Clamp(windowSeconds, 1, 300));
        _source = sources is { Count: > 0 } ? sources[_random.Next(sources.Count)] : null;
        if (_source?.Kind == CombatantKind.Npc)
        {
            // NPC weapons hit us; our selected weapon hits the NPC. Each keeps
            // its own cadence rather than reversing an NPC's gun into our attack.
            _attacks = (request.Randomize || request.Direction == DamageDirection.Incoming
                    ? _source.Attacks.Select(x => (x, (DamageDirection?)DamageDirection.Incoming)) : [])
                .Concat(request.Randomize || request.Direction == DamageDirection.Outgoing
                    ? _source.OutgoingAttacks.Select(x => (x, (DamageDirection?)DamageDirection.Outgoing)) : []).ToArray();
        }
        else _attacks = _source?.Attacks.Select(x => (x, (DamageDirection?)null)).ToArray() ?? [];
        _cycleStarts = _attacks.Select(_ => now).ToArray();
        _attackTimes = _attacks.Select(x => request.DurationSeconds > 0 ? now.AddMilliseconds(x.Attack.DelayMilliseconds) : now).ToArray();
        _burstHits = new int[_attacks.Length]; _attackCounts = new int[_attacks.Length];
        Until = now.AddSeconds(request.DurationSeconds == 0 ? eventDurationSeconds : Math.Clamp(request.DurationSeconds, 1, 60));
        Advance(now);
    }

    public void Advance(DateTimeOffset now)
    {
        if (now >= Until) return;
        bool mixed = _request.DurationSeconds > 0 && _request.Randomize;
        if (mixed && now >= _nextRepair)
        {
            // Logistics has its own cadence: a slow weapon must not suppress
            // repairs, and NPC damage must not turn friendly repairs into NPC hits.
            var repairEffect = _repairIndex / (_request.BothRepairDirections ? 1 : 2) % 2 == 0
                ? CombatEffect.ShieldRepair : CombatEffect.ArmorRepair;
            // Across successive pairs, both directions get a single and a combined sample.
            int directionIndex = CycleRepairs ? _repairIndex + _repairIndex / 2 : _repairIndex;
            EmitRepairs(now, directionIndex % 2 == 0 ? DamageDirection.Incoming : DamageDirection.Outgoing, repairEffect);
            _nextRepair = NextRepair(now);
        }
        if (!mixed && _request.Effect != CombatEffect.Damage)
        {
            if (now >= _next)
            {
                EmitRepairs(now, _request.Direction, _request.Effect);
                _next = _request.DurationSeconds > 0 ? NextRepair(now) : Until;
            }
            return;
        }
        if (_source is not null) { AdvanceStatic(now); return; }
        if (now < _next) return;
        bool stream = _request.DurationSeconds > 0;
        var direction = _request.Direction; var effect = _request.Effect;
        var platform = _request.Platform; var damage = _request.DamageType;
        double scale = 1;
        if (stream && _request.Randomize)
        {
            direction = _random.Next(2) == 0 ? DamageDirection.Incoming : DamageDirection.Outgoing;
            effect = CombatEffect.Damage;
            platform = (WeaponPlatform)_random.Next(1, Enum.GetValues<WeaponPlatform>().Length);
            damage = platform is WeaponPlatform.Blaster or WeaponPlatform.Railgun or WeaponPlatform.Laser
                or WeaponPlatform.Autocannon or WeaponPlatform.Artillery or WeaponPlatform.Disintegrator
                ? CombatDamageType.Mixed : (CombatDamageType)_random.Next(1, 5);
            scale = platform switch { WeaponPlatform.Rocket or WeaponPlatform.Drone => .35,
                WeaponPlatform.Artillery or WeaponPlatform.Torpedo => 2.2, _ => 1 };
        }
        if (effect != CombatEffect.Damage) { platform = WeaponPlatform.Unknown; damage = CombatDamageType.Unknown; }
        // Amount/cadence variation is illustrative, not an invented reconstruction of an EVE volley.
        double amount = stream ? Math.Round(_request.Amount * scale * (.55 + _random.NextDouble() * .9)) : _request.Amount;
        var kind = _request.Kind ?? (_request.Randomize && _random.Next(2) == 0 ? CombatantKind.Npc : CombatantKind.Player);
        if (effect != CombatEffect.Damage) kind = CombatantKind.Player;
        string peer = kind == CombatantKind.Npc ? "Guristas Despoiler" : "Orion Voss";
        string text = effect == CombatEffect.Damage ? $"{amount:0} {(direction == DamageDirection.Incoming ? "from" : "to")} {peer} - {platform} - Hits"
            : $"{amount:0} remote {(effect == CombatEffect.ShieldRepair ? "shield boosted" : effect == CombatEffect.ArmorRepair ? "armor repaired" : "hull repaired")} {(direction == DamageDirection.Incoming ? "by" : "to")} {peer}";
        var types = damage != CombatDamageType.Mixed ? DamageTypes.None : platform switch
        {
            WeaponPlatform.Blaster or WeaponPlatform.Railgun => DamageTypes.Thermal | DamageTypes.Kinetic,
            WeaponPlatform.Laser => DamageTypes.EM | DamageTypes.Thermal,
            WeaponPlatform.Disintegrator => DamageTypes.Thermal | DamageTypes.Explosive,
            WeaponPlatform.Autocannon or WeaponPlatform.Artillery => DamageTypes.Kinetic | DamageTypes.Explosive,
            _ => DamageTypes.All
        };
        _pending.Add(new(now, FullTitle.StartsWith("EVE - ", StringComparison.Ordinal) ? FullTitle[6..] : FullTitle,
            "combat", text, direction, amount, peer, kind, Effect: effect, Platform: platform, DamageType: damage) { DamageTypes = types });
        _next = stream ? now.AddMilliseconds(550 + _random.Next(950)) : Until;
    }

    private void AdvanceStatic(DateTimeOffset now)
    {
        var source = _source!;
        bool stream = _request.DurationSeconds > 0;
        for (int i = 0; i < _attacks.Length; i++)
        {
            if (now < _attackTimes[i]) continue;
            var attack = _attacks[i].Attack; var effect = _request.Effect; var direction = _attacks[i].Direction ?? _request.Direction;
            if (stream && _request.Randomize)
            {
                direction = _attacks[i].Direction ?? (_random.Next(2) == 0 ? DamageDirection.Incoming : DamageDirection.Outgoing);
                effect = CombatEffect.Damage;
            }
            bool damage = effect == CombatEffect.Damage;
            double ramp = 1 + Math.Min(attack.RampMaximum, attack.RampPerCycle * _attackCounts[i]);
            double amount = Math.Round((damage ? attack.Amount * ramp : _request.Amount) * _request.DamageScale
                * (stream ? .55 + _random.NextDouble() * .9 : 1), 1);
            var types = damage ? attack.DamageTypes : DamageTypes.None;
            var platform = damage ? attack.Platform : WeaponPlatform.Unknown;
            string? weapon = damage ? attack.Weapon : null;
            string text = damage ? FormattableString.Invariant($"{amount:0.0} {(direction == DamageDirection.Incoming ? "from" : "to")} {source.Name}{(weapon is null ? "" : " - " + weapon)} - Hits")
                : FormattableString.Invariant($"{amount:0.0} remote {(effect == CombatEffect.ShieldRepair ? "shield boosted" : effect == CombatEffect.ArmorRepair ? "armor repaired" : "hull repaired")} {(direction == DamageDirection.Incoming ? "by" : "to")} {source.Name}");
            var type = types switch { DamageTypes.EM => CombatDamageType.EM, DamageTypes.Thermal => CombatDamageType.Thermal,
                DamageTypes.Kinetic => CombatDamageType.Kinetic, DamageTypes.Explosive => CombatDamageType.Explosive,
                DamageTypes.None => CombatDamageType.Unknown, _ => CombatDamageType.Mixed };
            _pending.Add(new(now, FullTitle.StartsWith("EVE - ", StringComparison.Ordinal) ? FullTitle[6..] : FullTitle,
                "combat", text, direction, amount, source.Name, source.Kind, Effect: effect, Weapon: weapon, Platform: platform, DamageType: type)
            { DamageTypes = types, DamageEvidence = damage ? attack.Evidence : DamageEvidence.Unavailable,
                DamageSourceTypeId = damage && types != DamageTypes.None && attack.SourceTypeId > 0 ? attack.SourceTypeId : null,
                StaticDataBuild = types == DamageTypes.None ? null : attack.Build });
            // Each NPC weapon has its own cadence. Guns and missiles must retain
            // their own damage types instead of becoming one impossible mixed hit.
            _attackCounts[i]++;
            if (!stream) _attackTimes[i] = Until;
            else if (attack.BurstDurationMilliseconds > 0 && attack.BurstIntervalMilliseconds > 0
                && ++_burstHits[i] * attack.BurstIntervalMilliseconds < attack.BurstDurationMilliseconds)
                _attackTimes[i] = _cycleStarts[i].AddMilliseconds(attack.DelayMilliseconds + _burstHits[i] * attack.BurstIntervalMilliseconds);
            else
            {
                _burstHits[i] = 0;
                _cycleStarts[i] = _cycleStarts[i].AddMilliseconds(attack.CycleMilliseconds);
                _attackTimes[i] = _cycleStarts[i].AddMilliseconds(attack.DelayMilliseconds);
            }
        }
    }

    private DateTimeOffset NextRepair(DateTimeOffset now) => CycleRepairs ? now.AddSeconds(_repairSampleSeconds)
        : now.AddMilliseconds(2500 + _random.Next(501));

    private void EmitRepairs(DateTimeOffset now, DamageDirection direction, CombatEffect effect)
    {
        bool single = CycleRepairs && _repairIndex % 2 == 0;
        bool mixedTypes = _request.MixedRepairTypes || CycleRepairs && _request.Randomize;
        int count = single ? 1 : Math.Clamp(_request.RepairSourceCount, 1, 20);
        for (int source = 0; source < count; source++)
        {
            string peer = source == 0 ? "Orion Voss" : "Logistics Pilot " + (source + 1);
            var type = mixedTypes ? (CombatEffect)(1 + (single ? _repairIndex / 2 : source) % 3) : effect;
            if (_request.BothRepairDirections)
            {
                EmitRepair(now, DamageDirection.Incoming, type, peer);
                EmitRepair(now, DamageDirection.Outgoing, type, peer);
            }
            else EmitRepair(now, direction, type, peer);
        }
        _repairIndex++;
    }

    private void EmitRepair(DateTimeOffset now, DamageDirection direction, CombatEffect effect, string peer)
    {
        double amount = Math.Round(_request.Amount * _request.DamageScale
            * (_request.DurationSeconds > 0 ? .75 + _random.NextDouble() * .5 : 1), 1);
        string wording = effect == CombatEffect.ShieldRepair ? "shield boosted" : effect == CombatEffect.ArmorRepair ? "armor repaired" : "hull repaired";
        string text = FormattableString.Invariant($"{amount:0.0} remote {wording} {(direction == DamageDirection.Incoming ? "by" : "to")} {peer}");
        _pending.Add(new(now, FullTitle.StartsWith("EVE - ", StringComparison.Ordinal) ? FullTitle[6..] : FullTitle,
            "combat", text, direction, amount, peer, CombatantKind.Player, Effect: effect));
    }

    public IReadOnlyList<ParsedLogEntry> DrainEvents()
    {
        var entries = _pending.ToArray(); _pending.Clear(); return entries;
    }
}
