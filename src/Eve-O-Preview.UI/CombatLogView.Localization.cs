using Avalonia.Controls;
using Avalonia.Media;
using EveOPreview.Preview;

namespace EveOPreview.UI;

public sealed partial class CombatLogView
{
    private WorkspaceLocalization _localization = new("en");
    private string L(string value) => _localization.Get(value);
    private string F(FormattableString value) => _localization.Format(value);
    private TextBlock RawText(string value, int size, bool bold = false) => new()
    {
        Text = value, FontSize = size, Foreground = WorkspaceTheme.Brush(_theme.Text), TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, FlowDirection = FlowDirection.LeftToRight
    };
    private string ChoiceText(object? value) => value switch
    {
        SimulationFaction faction => faction.Id == 0 ? L("All factions") : faction.Name,
        SimulationNpc npc => npc.Id == 0 ? L("Any ship") : npc.Name,
        SimulationWeapon weapon => weapon.Name,
        SimulationAmmo ammo => ammo.Name,
        CombatResetScope.All => L("All statistics"),
        CombatResetScope.Damage => L("Damage / DPS"),
        CombatResetScope.Repairs => L("Repairs"),
        CombatResetScope.Jumps => L("Jumps"),
        CombatRowOrder order => AugmentLabels.Order(order, L),
        OverlayPosition position => AugmentLabels.Position(position, L),
        CombatEffect.ShieldRepair => L("Shield repairs"),
        CombatEffect.ArmorRepair => L("Armour repairs"),
        CombatEffect.HullRepair => L("Hull repairs"),
        CombatTextColorMode.DamageType => L("Damage type"),
        CombatTextColorMode.WeaponPlatform => L("Weapon platform"),
        OverlaySymbol.EveEM => L("EVE EM"), OverlaySymbol.EveThermal => L("EVE thermal"),
        OverlaySymbol.EveKinetic => L("EVE kinetic"), OverlaySymbol.EveExplosive => L("EVE explosive"),
        _ => L(value?.ToString() ?? "")
    };
}
