using EveOPreview.Preview;

namespace EveOPreview.UI;

// Preserve persisted values 1/2. Historical unclassified value 0 is read as Player.
public enum CombatantKind { Npc = 1, Player = 2 }
public enum DamageDirection { Incoming, Outgoing }
public enum CombatDamageType { Unknown, EM, Thermal, Kinetic, Explosive, Mixed }
[Flags]
public enum DamageTypes { None = 0, EM = 1, Thermal = 2, Kinetic = 4, Explosive = 8, All = 15 }
public enum DamageEvidence { Unavailable, NamedItem, NpcAttack }
// Persisted in settings and history. Append only; generic Missile/Laser remain fallbacks.
public enum WeaponPlatform
{
    Unknown = 0, Rocket = 1, Missile = 2, Torpedo = 3, Blaster = 4, Railgun = 5, Laser = 6,
    Autocannon = 7, Artillery = 8, Drone = 9, Smartbomb = 10, Disintegrator = 11,
    LightMissile = 12, HeavyMissile = 13, HeavyAssaultMissile = 14, CruiseMissile = 15,
    XLCruiseMissile = 16, XLTorpedo = 17, PulseLaser = 18, BeamLaser = 19, Fighter = 20,
    Vorton = 21, Bomb = 22, GuidedBomb = 23, StructureMissile = 24, PointDefense = 25,
    Doomsday = 26, Lance = 27, Reaper = 28, Bosonic = 29, DefenderMissile = 30,
    Hybrid = 31, Projectile = 32
}
public enum CombatEffect { Damage, ShieldRepair, ArmorRepair, HullRepair }
public enum CombatTextColorMode { Direction, DamageType, WeaponPlatform }
public sealed record CombatVisualStyle(string Color, OverlaySymbol Icon, string? TextColor = null);
public sealed record CombatAppearance
{
    public string IncomingColor { get; init; } = "#FF9990";
    public string OutgoingColor { get; init; } = "#8FE5B9";
    public string SystemColor { get; init; } = "#D4E8FF";
    public CombatTextColorMode TextColorMode { get; init; }
    public bool ShowDamageIcon { get; init; } = true;
    public bool ShowWeaponIcon { get; init; } = true;
    public IReadOnlyDictionary<CombatDamageType, CombatVisualStyle> Damage { get; init; } = new Dictionary<CombatDamageType, CombatVisualStyle>();
    public IReadOnlyDictionary<WeaponPlatform, CombatVisualStyle> Weapons { get; init; } = new Dictionary<WeaponPlatform, CombatVisualStyle>();
    public IReadOnlyDictionary<CombatEffect, CombatVisualStyle> Repairs { get; init; } = new Dictionary<CombatEffect, CombatVisualStyle>();

    public CombatVisualStyle WeaponStyle(WeaponPlatform platform)
    {
        if (Weapons.TryGetValue(platform, out var saved)) return saved;
        var defaults = Preset("Classic").Weapons;
        var style = defaults.GetValueOrDefault(platform) ?? defaults[WeaponPlatform.Unknown];
        var previous = platform switch
        {
            WeaponPlatform.LightMissile or WeaponPlatform.HeavyMissile or WeaponPlatform.HeavyAssaultMissile
                or WeaponPlatform.CruiseMissile or WeaponPlatform.XLCruiseMissile or WeaponPlatform.StructureMissile
                or WeaponPlatform.GuidedBomb or WeaponPlatform.Bomb or WeaponPlatform.DefenderMissile => WeaponPlatform.Missile,
            WeaponPlatform.XLTorpedo => WeaponPlatform.Torpedo,
            WeaponPlatform.PulseLaser or WeaponPlatform.BeamLaser => WeaponPlatform.Laser,
            WeaponPlatform.Fighter => WeaponPlatform.Drone,
            _ => WeaponPlatform.Unknown
        };
        // Older custom maps inherit their previous family colours. Preserve an
        // intentionally selected symbol; default symbols gain the new platform glyph.
        return previous != WeaponPlatform.Unknown && Weapons.TryGetValue(previous, out var inherited)
            ? style with { Color = inherited.Color, TextColor = inherited.TextColor,
                Icon = inherited.Icon != defaults[previous].Icon ? inherited.Icon : style.Icon } : style;
    }

    public static CombatAppearance Preset(string name)
    {
        var damage = new Dictionary<CombatDamageType, CombatVisualStyle>
        {
            [CombatDamageType.Unknown] = new("#D1D6E0", OverlaySymbol.Unknown),
            [CombatDamageType.EM] = new("#70BFFF", OverlaySymbol.EveEM),
            [CombatDamageType.Thermal] = new("#FF7666", OverlaySymbol.EveThermal),
            [CombatDamageType.Kinetic] = new("#C3CEDD", OverlaySymbol.EveKinetic),
            [CombatDamageType.Explosive] = new("#FFC85B", OverlaySymbol.EveExplosive),
            [CombatDamageType.Mixed] = new("#D6A6FF", OverlaySymbol.Mixed)
        };
        var weapons = Enum.GetValues<WeaponPlatform>().ToDictionary(x => x, x => new CombatVisualStyle("#F2F4FA", x switch
        {
            WeaponPlatform.Rocket => OverlaySymbol.Rocket, WeaponPlatform.Missile => OverlaySymbol.Missile, WeaponPlatform.Torpedo => OverlaySymbol.Torpedo,
            WeaponPlatform.Blaster => OverlaySymbol.Blaster, WeaponPlatform.Railgun => OverlaySymbol.Railgun,
            WeaponPlatform.Laser => OverlaySymbol.Laser, WeaponPlatform.Autocannon => OverlaySymbol.Autocannon, WeaponPlatform.Artillery => OverlaySymbol.Artillery,
            WeaponPlatform.Drone => OverlaySymbol.Drone, WeaponPlatform.Smartbomb => OverlaySymbol.Smartbomb,
            WeaponPlatform.Disintegrator => OverlaySymbol.Disintegrator,
            WeaponPlatform.LightMissile => OverlaySymbol.LightMissile,
            WeaponPlatform.HeavyMissile => OverlaySymbol.HeavyMissile,
            WeaponPlatform.HeavyAssaultMissile => OverlaySymbol.HeavyAssaultMissile,
            WeaponPlatform.CruiseMissile => OverlaySymbol.CruiseMissile,
            WeaponPlatform.XLCruiseMissile => OverlaySymbol.XLCruiseMissile,
            WeaponPlatform.XLTorpedo => OverlaySymbol.XLTorpedo,
            WeaponPlatform.PulseLaser => OverlaySymbol.PulseLaser,
            WeaponPlatform.BeamLaser => OverlaySymbol.BeamLaser,
            WeaponPlatform.Fighter => OverlaySymbol.Fighter,
            WeaponPlatform.Vorton => OverlaySymbol.Vorton,
            WeaponPlatform.Bomb => OverlaySymbol.Bomb,
            WeaponPlatform.GuidedBomb => OverlaySymbol.GuidedBomb,
            WeaponPlatform.StructureMissile => OverlaySymbol.StructureMissile,
            WeaponPlatform.PointDefense => OverlaySymbol.PointDefense,
            WeaponPlatform.Doomsday => OverlaySymbol.Doomsday,
            WeaponPlatform.Lance => OverlaySymbol.Lance,
            WeaponPlatform.Reaper => OverlaySymbol.Reaper,
            WeaponPlatform.Bosonic => OverlaySymbol.Bosonic,
            WeaponPlatform.DefenderMissile => OverlaySymbol.DefenderMissile,
            WeaponPlatform.Hybrid => OverlaySymbol.Hybrid,
            WeaponPlatform.Projectile => OverlaySymbol.Projectile,
            _ => OverlaySymbol.Unknown
        }));
        return new()
        {
            TextColorMode = name == "Damage colours" ? CombatTextColorMode.DamageType : CombatTextColorMode.Direction,
            ShowDamageIcon = true, ShowWeaponIcon = name != "Minimal", Damage = damage, Weapons = weapons,
            Repairs = new Dictionary<CombatEffect, CombatVisualStyle>
            {
                [CombatEffect.ShieldRepair] = new("#6EBDFF", OverlaySymbol.Shield),
                [CombatEffect.ArmorRepair] = new("#D7A16B", OverlaySymbol.Armor),
                [CombatEffect.HullRepair] = new("#DFDFDC", OverlaySymbol.Hull)
            }
        };
    }
}

public sealed record LogOverlayOptions(bool Incoming = true, bool Outgoing = true,
    bool SolarSystem = true, bool Breakdown = false, bool Top = false, int FontSize = 14,
    int OffsetX = 8, int OffsetY = 8)
{
    public OverlayPosition TitlePosition { get; init; }
    public OverlayPosition? Position { get; init; }
    public CombatRowOrder RowOrder { get; init; }
    public bool DamageEvents { get; init; } = true;
    public bool Repairs { get; init; }
    public int EventDurationSeconds { get; init; } = 3;
    public string Preset { get; init; } = "Classic";
    public bool Advanced { get; init; }
    public CombatAppearance? CustomAppearance { get; init; }
    // Separate from combat themes; null keeps older saved appearance colours.
    public string? SystemColor { get; init; }
    public SubtitlePlacement SystemPlacement { get; init; }
    public float? SystemFontSize { get; init; }
    public string? FontFamily { get; init; }
    public OverlayFontStyle? FontStyle { get; init; }
    public OverlayStatsStyle GetStatsStyle() => new(FontSize, OffsetX, OffsetY, Top) { FontFamily = FontFamily, FontStyle = FontStyle, Position = Position };
    public CombatAppearance GetAppearance()
    {
        var appearance = Advanced && CustomAppearance is not null ? CustomAppearance : CombatAppearance.Preset(Preset);
        // Simple mode uses preset styling, but weapon visibility is shared between modes.
        if (!Advanced && CustomAppearance is not null)
            appearance = appearance with { ShowWeaponIcon = CustomAppearance.ShowWeaponIcon };
        return SystemColor is null ? appearance : appearance with { SystemColor = SystemColor };
    }
}

public enum DamageFlashTarget { Title, Thumbnail, Both }
public enum CombatRowOrder { IncomingOutgoingRepairs, OutgoingIncomingRepairs, IncomingRepairsOutgoing, OutgoingRepairsIncoming, RepairsIncomingOutgoing, RepairsOutgoingIncoming }
public enum DamageFlashAnimation { Blink, Fade }

/// <summary>Application-wide preferences; title keys retain the full EVE window title.</summary>
public sealed record CombatLogSettings
{
    public bool Enabled { get; init; }
    public string Directory { get; init; } = "";
    public int WindowSeconds { get; init; } = 10;
    public int RetentionDays { get; init; } = 7;
    public bool FlashIncomingDamage { get; init; } = true;
    public DamageFlashTarget FlashTarget { get; init; } = DamageFlashTarget.Title;
    public DamageFlashAnimation FlashAnimation { get; init; } = DamageFlashAnimation.Fade;
    public int FlashOpacityPercent { get; init; } = 20;
    public bool FlashPlayerOnly { get; init; }
    public string FlashColor { get; init; } = "#FF7666";
    public int FlashSeconds { get; init; } = 2;
    public int FlashIntervalMilliseconds { get; init; } = 500;
    public LogOverlayOptions DefaultOverlay { get; init; } = new();
    public IReadOnlyDictionary<string, LogOverlayOptions> Overlays { get; init; }
        = new Dictionary<string, LogOverlayOptions>(StringComparer.Ordinal);
}

public sealed record DamageFigures(double Incoming, double Outgoing);
public sealed record CombatCategory(CombatantKind Kind, DamageFigures Total, DamageFigures Dps)
{
    public DamageTypes IncomingDamageTypes { get; init; }
    public bool HasUnresolvedIncomingDamage { get; init; }
}
public sealed record CombatActivityCategory(CombatantKind Kind, DamageFigures Hits, DamageFigures LargestHit,
    long UniqueTargets, long UniqueAttackers)
{
    public DamageFigures AverageDps { get; init; } = new(0, 0);
    public DamageFigures DpsSampleSeconds { get; init; } = new(0, 0);
}
public sealed record RepairActivity(CombatEffect Effect, DamageFigures Total, DamageFigures Count);
public sealed record RepairRate(CombatEffect Effect, DamageFigures PerSecond);
public enum CombatResetScope { All, Damage, Repairs, Jumps }
/// <summary>Durable counters since the available activity history began. NPC uniqueness means log labels, not individual spawns.</summary>
public sealed record CharacterActivityStats(DateTimeOffset Since, long SystemChanges, long UniqueSystems,
    IReadOnlyList<CombatActivityCategory> Combat, IReadOnlyList<RepairActivity> Repairs)
{
    public DateTimeOffset? AverageDpsSince { get; init; }
}
public sealed record CharacterCombatSnapshot(string Name, long? CharacterId, string? SolarSystem,
    long? SolarSystemId, DateTimeOffset? LocationObservedAt, DateTimeOffset? LastLogAt,
    IReadOnlyList<CombatCategory> Categories, int SourceFiles, int GameFiles = 0, int LocalFiles = 0)
{
    public CharacterActivityStats? Activity { get; init; }
    public IReadOnlyList<RepairRate> RepairRates { get; init; } = [];
    public DateTimeOffset? LastIncomingDamageAt { get; init; }
    public DateTimeOffset? LastPlayerDamageAt { get; init; }
    public DateTimeOffset? IncomingDamageStartedAt { get; init; }
    public DateTimeOffset? PlayerDamageStartedAt { get; init; }
    public DamageTypes IncomingDamageTypes { get; init; }
    public bool HasUnresolvedIncomingDamage { get; init; }
}
public sealed record ParsedLogEntry(DateTimeOffset Timestamp, string Character, string Category,
    string Text, DamageDirection? Direction = null, double Amount = 0,
    string? Counterparty = null, CombatantKind Kind = CombatantKind.Player,
    string? SolarSystem = null, long? SolarSystemId = null, CombatEffect Effect = CombatEffect.Damage,
    string? Weapon = null, WeaponPlatform Platform = WeaponPlatform.Unknown, CombatDamageType DamageType = CombatDamageType.Unknown)
{
    public DamageTypes DamageTypes { get; init; }
    public DamageEvidence DamageEvidence { get; init; }
    public long? DamageSourceTypeId { get; init; }
    public int? StaticDataBuild { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public DamageTypes EffectiveDamageTypes => DamageTypes != DamageTypes.None ? DamageTypes : DamageType switch
    { CombatDamageType.EM => DamageTypes.EM, CombatDamageType.Thermal => DamageTypes.Thermal,
      CombatDamageType.Kinetic => DamageTypes.Kinetic, CombatDamageType.Explosive => DamageTypes.Explosive, _ => DamageTypes.None };
}
public sealed record CombatSimulation(string FullTitle, DamageDirection Direction, double Amount,
    CombatDamageType DamageType, WeaponPlatform Platform, CombatEffect Effect = CombatEffect.Damage)
{
    public bool AllVisibleThumbnails { get; init; }
    /// <summary>Zero preserves the single-event preview; timed runs are bounded to 60 seconds.</summary>
    public int DurationSeconds { get; init; }
    public bool Randomize { get; init; }
    public bool BothRepairDirections { get; init; }
    public int RepairSourceCount { get; init; } = 1;
    public bool MixedRepairTypes { get; init; }
    public bool Stop { get; init; }
    public CombatantKind? Kind { get; init; }
    public bool UseStaticData { get; init; }
    public long? NpcFactionId { get; init; }
    public long? NpcTypeId { get; init; }
    public long? WeaponTypeId { get; init; }
    public long? AmmoTypeId { get; init; }
    public double DamageScale { get; init; } = 1;
}
public sealed record CombatLogSnapshot(string Status, string Directory, DateTimeOffset Since,
    int WindowSeconds, IReadOnlyList<CharacterCombatSnapshot> Characters,
    IReadOnlyList<ParsedLogEntry> RecentEntries, long UnrecognizedCombatEntries = 0);

/// <summary>Snapshots contain no handles, credentials or database connections.</summary>
public interface IWorkspaceCombatLogs
{
    CombatLogSettings ReadLogSettings();
    CombatLogSnapshot ReadLogs();
    event Action? LogsChanged;
    Task<CommandResult> SaveLogSettingsAsync(CombatLogSettings settings);
    Task<CommandResult> ResetCombatAsync();
    Task<CommandResult> ResetCombatAsync(CombatResetScope scope, string? character = null);
    Task<CommandResult> RescanLogsAsync();
    Task<CommandResult> SimulateLogEventAsync(CombatSimulation simulation);
}
