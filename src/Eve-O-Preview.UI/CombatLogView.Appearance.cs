using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Layout;
using EveOPreview.Preview;
using EveOPreview.UI.Previews;

namespace EveOPreview.UI;

public sealed partial class CombatLogView
{
    private AvaloniaPreviewOverlay? _simulationPreview;
    private IReadOnlyList<string>? _combatFontFamilies;

    private void AppendCombatFont(LogOverlayOptions options, Func<Func<LogOverlayOptions, LogOverlayOptions>, Task> change)
    {
        _combatFontFamilies ??= _backend is IWorkspacePreviewRenderer renderer ? renderer.FontFamilies
            : Avalonia.Media.FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
        string key = _selected;
        var input = WorkspacePickers.FontFamily(_combatFontFamilies, _drafts.Fonts.GetValueOrDefault(key) ?? options.FontFamily ?? "",
            "logs-font-family", L("Font family"), L("Use title font"));
        input.SelectionChanged += (_, _) =>
        {
            string family = input.SelectedItem as string ?? "";
            if (family == (options.FontFamily ?? "")) _drafts.Fonts.Remove(key);
            else _drafts.Fonts[key] = family;
        };
        var row = new Grid { ColumnDefinitions = new("*,Auto") };
        row.Children.Add(input);
        var apply = Button("Apply", "logs-font-apply", async () =>
        {
            string text = input.SelectedItem as string ?? "";
            string? family = _combatFontFamilies.FirstOrDefault(x => x.Equals(text, StringComparison.OrdinalIgnoreCase));
            if (text.Length > 0 && family is null) { Report(CommandResult.Error("Choose an installed font.")); return; }
            await change(x => x with { FontFamily = family });
            if (_lastSaveSucceeded) { _drafts.Fonts.Remove(key); RenderOverlaySettings(); }
        });
        Grid.SetColumn(apply, 1); row.Children.Add(apply);
        _overlay.Children.Add(Text("DPS / alpha font", 12)); _overlay.Children.Add(row);
        var styles = SettingCatalog.Find("TitleFontStyle")!.Options!.Prepend("Use title font").ToArray();
        _overlay.Children.Add(Choice("Font style", "logs-font-style", styles, options.FontStyle?.ToString() ?? styles[0],
            style => change(x => x with { FontStyle = style == styles[0] ? null : Enum.Parse<OverlayFontStyle>(style) })));
        _overlay.Children.Add(Button("Use title font", "logs-font-reset", async () =>
        {
            await change(x => x with { FontFamily = null, FontStyle = null });
            if (_lastSaveSucceeded) { _drafts.Fonts.Remove(key); RenderOverlaySettings(); }
        }));
    }
    private void AppendAdvancedAppearance(LogOverlayOptions options, Func<Func<LogOverlayOptions, LogOverlayOptions>, Task> change)
    {
        var appearance = options.GetAppearance();
        Task Edit(Func<CombatAppearance, CombatAppearance> edit) => change(x => x with { Advanced = true, CustomAppearance = edit(x.GetAppearance()) });
        async Task Display(Func<CombatAppearance, CombatAppearance> edit)
        { await Edit(edit); if (!_disposed && _lastSaveSucceeded) RenderOverlaySettings(); }
        _overlay.Children.Add(Text("TEXT AND ICONS", 12, true));
        _overlay.Children.Add(Choice("Alpha text colour", "logs-text-mode", Enum.GetValues<CombatTextColorMode>(), appearance.TextColorMode,
            value => Display(x => x with { TextColorMode = value })));
        _overlay.Children.Add(ColorField("Incoming text", "logs-incoming-color", appearance.IncomingColor, color => Edit(x => x with { IncomingColor = color })));
        _overlay.Children.Add(ColorField("Outgoing text", "logs-outgoing-color", appearance.OutgoingColor, color => Edit(x => x with { OutgoingColor = color })));
        _overlay.Children.Add(Toggle("Show damage / repair icon", "logs-show-damage-icon", appearance.ShowDamageIcon, v => Display(x => x with { ShowDamageIcon = v })));
        var damage = new StackPanel { Spacing = 8 };
        foreach (var kind in Enum.GetValues<CombatDamageType>())
        {
            var style = appearance.Damage.GetValueOrDefault(kind) ?? CombatAppearance.Preset("Classic").Damage[kind];
            damage.Children.Add(StyleEditor(kind.ToString(), "damage-" + kind, style, edit => Edit(x =>
            { var map = new Dictionary<CombatDamageType, CombatVisualStyle>(x.Damage) { [kind] = edit(x.Damage.GetValueOrDefault(kind) ?? style) }; return x with { Damage = map }; }),
                options.DamageEvents && appearance.ShowDamageIcon, options.DamageEvents && appearance.TextColorMode == CombatTextColorMode.DamageType));
        }
        _overlay.Children.Add(new Expander { Name = "logs-damage-styles", Header = L("Damage type colours & icons"), Content = damage, HorizontalAlignment = HorizontalAlignment.Stretch });
        var weapons = new StackPanel { Spacing = 8 };
        foreach (var platform in Enum.GetValues<WeaponPlatform>().Where(x => x != WeaponPlatform.Unknown))
        {
            var style = appearance.WeaponStyle(platform);
            weapons.Children.Add(StyleEditor(platform.ToString(), "weapon-" + platform, style, edit => Edit(x =>
            { var map = new Dictionary<WeaponPlatform, CombatVisualStyle>(x.Weapons) { [platform] = edit(x.Weapons.GetValueOrDefault(platform) ?? style) }; return x with { Weapons = map }; }),
                options.DamageEvents && appearance.ShowWeaponIcon, options.DamageEvents && appearance.TextColorMode == CombatTextColorMode.WeaponPlatform));
        }
        _overlay.Children.Add(new Expander { Name = "logs-weapon-styles", Header = L(options.DamageEvents ? "Weapon platform colours & icons" : "Weapon platform colours & icons (alpha hidden)"),
            Content = weapons, HorizontalAlignment = HorizontalAlignment.Stretch });
        var repairs = new StackPanel { Spacing = 8 };
        foreach (var effect in new[] { CombatEffect.ShieldRepair, CombatEffect.ArmorRepair, CombatEffect.HullRepair })
        {
            var style = appearance.Repairs.GetValueOrDefault(effect) ?? CombatAppearance.Preset("Classic").Repairs[effect];
            repairs.Children.Add(StyleEditor(ChoiceText(effect), "repair-" + effect, style, edit => Edit(x =>
            { var map = new Dictionary<CombatEffect, CombatVisualStyle>(x.Repairs) { [effect] = edit(x.Repairs.GetValueOrDefault(effect) ?? style) }; return x with { Repairs = map }; }), sharedColor: true));
        }
        _overlay.Children.Add(new Expander { Name = "logs-repair-styles", Header = L("Repair colours & icons"), Content = repairs, HorizontalAlignment = HorizontalAlignment.Stretch });
    }

    private Control StyleEditor(string label, string name, CombatVisualStyle style, Func<Func<CombatVisualStyle, CombatVisualStyle>, Task> change,
        bool iconEnabled = true, bool textEnabled = true, bool sharedColor = false)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8) };
        var sample = new OverlaySymbolPreview(style.Icon, Color.Parse(style.Color)) { Name = "logs-" + name + "-sample" };
        AutomationProperties.SetName(sample, L(label) + ": " + ChoiceText(style.Icon));
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        heading.Children.Add(sample);
        var title = Text(label, 13, true); title.VerticalAlignment = VerticalAlignment.Center;
        heading.Children.Add(title); panel.Children.Add(heading);
        var iconColor = ColorField("Icon colour", "logs-" + name + "-icon-color", style.Color, color => change(x => x with { Color = color }),
            preview: color => sample.Update(sample.Symbol, color));
        var textColor = ColorField("Text colour", "logs-" + name + "-text-color", style.TextColor ?? style.Color, color => change(x => x with { TextColor = color }));
        var icon = Choice("Icon", "logs-" + name + "-icon", Enum.GetValues<OverlaySymbol>(), style.Icon, async value =>
        {
            await change(x => x with { Icon = value });
            if (!_lastSaveSucceeded) return;
            sample.Update(value, sample.Color);
            AutomationProperties.SetName(sample, L(label) + ": " + ChoiceText(value));
        });
        iconColor.IsEnabled = icon.IsEnabled = iconEnabled; textColor.IsEnabled = textEnabled;
        panel.Children.Add(iconColor); if (!sharedColor) panel.Children.Add(textColor); panel.Children.Add(icon);
        return panel;
    }

    private Control Choice<T>(string label, string name, IReadOnlyList<T> choices, T value, Func<T, Task> change)
    {
        var panel = new Grid { ColumnDefinitions = new("*,180") };
        panel.Children.Add(Text(label, 12));
        var select = new ComboBox { Name = name, ItemsSource = choices, SelectedItem = value, HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 260, ItemTemplate = new FuncDataTemplate<T>((item, _) => RawText(ChoiceText(item), 12)) };
        ScrollViewer.SetVerticalScrollBarVisibility(select, ScrollBarVisibility.Visible);
        ScrollViewer.SetAllowAutoHide(select, false);
        select.SelectionChanged += async (_, _) => { if (!_disposed && !_busy && select.SelectedItem is T next) await Run(() => change(next)); };
        Grid.SetColumn(select, 1); panel.Children.Add(select); return panel;
    }

    private Control ColorField(string label, string name, string current, Func<string, Task> change, bool global = false, Action<Color>? preview = null)
    {
        string draftKey = (global ? "global" : _selected) + ":" + name;
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Text(label, 12));
        var row = new Grid { ColumnDefinitions = new("64,*,Auto"), ColumnSpacing = 6 };
        var input = new TextBox { Name = name, Text = _drafts.Colors.GetValueOrDefault(draftKey) ?? current, Watermark = "#RRGGBB", MaxLength = 7,
            FlowDirection = FlowDirection.LeftToRight };
        var picker = WorkspacePickers.Color(Color.TryParse(input.Text, out var initial) ? initial : Colors.White,
            name + "-picker", F($"Choose {L(label)}"));
        preview?.Invoke(picker.Color);
        picker.ColorChanged += (_, e) => input.Text = $"#{e.NewColor.R:X2}{e.NewColor.G:X2}{e.NewColor.B:X2}";
        input.TextChanged += (_, _) =>
        {
            if (input.Text == current) _drafts.Colors.Remove(draftKey);
            else _drafts.Colors[draftKey] = input.Text ?? "";
            if (Color.TryParse(input.Text, out var selected))
            {
                if (selected != picker.Color) picker.Color = selected;
                preview?.Invoke(selected);
            }
        };
        row.Children.Add(picker); Grid.SetColumn(input, 1); row.Children.Add(input);
        var apply = Button("Apply", name + "-apply", async () =>
        {
            string text = input.Text ?? "";
            if (text.Length != 7 || text[0] != '#' || !uint.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            { Report(CommandResult.Error("Enter a colour such as #66AAFF.")); return; }
            await change(text);
            // The host rejects invalid settings; a valid field remains editable if
            // saving failed, and its explicit error stays visible below setup.
            if (_lastSaveSucceeded) { _drafts.Colors.Remove(draftKey); current = text; }
        });
        apply.Margin = default; Grid.SetColumn(apply, 2); row.Children.Add(apply); panel.Children.Add(row); return panel;
    }

}
