using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Automation;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using EveOPreview.Preview;
using EveOPreview.UI.Previews;

namespace EveOPreview.UI;

public sealed partial class CombatLogView
{
    private Action? _refreshSimulationCatalog;
    private Action? _refreshAppearancePreview;

    private Control BuildSimulator()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Text("Simulation", 16, true));
        var preview = _simulationPreview = new AvaloniaPreviewOverlay();
        var previewHost = new Border { Height = 180, MinWidth = 220, Background = WorkspaceTheme.Brush("#161D2A"),
            CornerRadius = new(6), ClipToBounds = true, Child = preview };
        previewHost.LayoutUpdated += (_, _) =>
        {
            if (_disposed) return;
            double scale = TopLevel.GetTopLevel(previewHost)?.RenderScaling ?? 1;
            preview.Resize(new PreviewSize((int)Math.Round(previewHost.Bounds.Width * scale), (int)Math.Round(previewHost.Bounds.Height * scale)));
        };
        panel.Children.Add(previewHost);
        var platformSummary = Text("", 12); platformSummary.Name = "logs-sim-platforms"; platformSummary.IsVisible = false;
        panel.Children.Add(platformSummary);
        bool mixed = true, loading = false, loaded = false, npcSelectionValid = true;
        int duration = 20, damagePercent = 100;
        int? loadedBuild = null;
        var kind = CombatantKind.Player;
        var direction = DamageDirection.Incoming;
        var effect = CombatEffect.Damage;
        bool bothRepairs = false, mixedRepairTypes = false;
        int repairSources = 3;
        Control? directionControl = null;
        Control? repairSourcesControl = null;
        CheckBox? bothRepairsControl = null;
        CombatSimulationCatalog catalog = CombatSimulationCatalog.Unavailable("Loading simulation choices…");
        SimulationFaction? faction = null;
        SimulationNpc? npc = null;
        SimulationWeapon? weapon = null;
        SimulationAmmo? ammo = null;
        var sources = new StackPanel { Spacing = 8 };
        var ammoPanel = new StackPanel { Spacing = 8 };
        var availability = Text("Loading simulation choices…", 12); availability.Name = "logs-sim-status";
        var result = Text("", 12); result.IsVisible = false;
        Button? simulate = null;
        async Task EnableDisplay(Func<LogOverlayOptions, LogOverlayOptions> change)
        {
            var current = _logs.ReadLogSettings();
            var perClient = new Dictionary<string, LogOverlayOptions>(current.Overlays);
            bool all = !_selected.StartsWith("EVE - ", StringComparison.Ordinal);
            if (all)
            {
                foreach (string title in _backend.Read().Clients.Select(x => x.Title).Where(perClient.ContainsKey))
                    perClient[title] = change(perClient[title]);
            }
            else perClient[_selected] = change(perClient.GetValueOrDefault(_selected) ?? current.DefaultOverlay);
            await Save(current with { DefaultOverlay = all ? change(current.DefaultOverlay) : current.DefaultOverlay, Overlays = perClient });
            if (!_disposed) { RenderOverlaySettings(); _refreshAppearancePreview?.Invoke(); }
        }
        var enableRepairs = Button("Show repairs on simulated thumbnails", "logs-sim-enable-repairs", () => EnableDisplay(x => x with { Repairs = true }));
        enableRepairs.IsVisible = false;
        var enableWeapons = Button("Show alpha + weapon icons", "logs-sim-enable-weapons", () => EnableDisplay(x => x with
            { DamageEvents = true, CustomAppearance = (x.CustomAppearance ?? x.GetAppearance()) with { ShowWeaponIcon = true } }));
        enableWeapons.IsVisible = false;

        CombatSimulation Request() => new(_selected, direction, 1250, CombatDamageType.Unknown, WeaponPlatform.Unknown, effect)
        {
            AllVisibleThumbnails = !_selected.StartsWith("EVE - ", StringComparison.Ordinal), DurationSeconds = duration,
            Randomize = mixed, BothRepairDirections = bothRepairs && (mixed || effect != CombatEffect.Damage),
            RepairSourceCount = repairSources, MixedRepairTypes = mixedRepairTypes,
            Kind = kind, UseStaticData = true, DamageScale = damagePercent / 100d,
            NpcFactionId = faction?.Id, NpcTypeId = npc?.Id, WeaponTypeId = weapon?.Id, AmmoTypeId = ammo?.Id
        };
        void RefreshPreview()
        {
            var settings = _logs.ReadLogSettings(); var options = settings.Overlays.GetValueOrDefault(_selected) ?? settings.DefaultOverlay;
            bool all = !_selected.StartsWith("EVE - ", StringComparison.Ordinal);
            if (bothRepairsControl is not null) bothRepairsControl.IsVisible = mixed || effect != CombatEffect.Damage;
            if (repairSourcesControl is not null) repairSourcesControl.IsVisible = mixed || effect != CombatEffect.Damage;
            if (directionControl is not null) directionControl.IsEnabled = effect == CombatEffect.Damage || !bothRepairs;
            enableRepairs.IsVisible = (mixed || effect != CombatEffect.Damage) && (!options.Repairs || all
                && _backend.Read().Clients.Any(x => settings.Overlays.TryGetValue(x.Title, out var perClient) && !perClient.Repairs));
            sources.IsVisible = mixed || effect == CombatEffect.Damage;
            bool HiddenWeapons(LogOverlayOptions value) => !value.DamageEvents || !value.GetAppearance().ShowWeaponIcon;
            enableWeapons.IsVisible = sources.IsVisible && (HiddenWeapons(options) || all
                && _backend.Read().Clients.Any(x => settings.Overlays.TryGetValue(x.Title, out var perClient) && HiddenWeapons(perClient)));
            platformSummary.IsVisible = false;
            bool valid = false;
            if (!loading && catalog.Build.HasValue && (kind != CombatantKind.Npc || npcSelectionValid || !mixed && effect != CombatEffect.Damage))
            {
                try
                {
                    var request = Request(); var choices = catalog.Resolve(request);
                    if (sources.IsVisible)
                    {
                        string Platforms(IEnumerable<SimulationAttack> attacks) => string.Join(" / ", attacks.Select(x =>
                            L(x.Platform == WeaponPlatform.Unknown ? "Unidentified weapon" : x.Platform.ToString())).Distinct());
                        string description = kind == CombatantKind.Npc && choices.Count > 1 ? L("Varies by NPC ship")
                            : Platforms(choices.SelectMany(x => direction == DamageDirection.Outgoing && !mixed && x.Kind == CombatantKind.Npc ? x.OutgoingAttacks : x.Attacks));
                        if (kind == CombatantKind.Npc && mixed) description += " · " + L("Outgoing") + ": " + Platforms(choices.SelectMany(x => x.OutgoingAttacks));
                        platformSummary.Text = F($"Weapon platform: {description}"); platformSummary.IsVisible = true;
                    }
                    valid = true;
                }
                catch (ArgumentException) { }
            }
            if (simulate is not null) simulate.IsEnabled = valid;
            var stats = CombatOverlayFormatter.AppearanceSample(Request(), catalog, options, settings.WindowSeconds);
            previewHost.Height = Math.Max(180, stats.Count * options.GetStatsStyle().LineHeight + 64);
            var title = _backend.Read().Settings;
            Enum.TryParse<OverlayFontStyle>(title.GetValueOrDefault("TitleFontStyle", "Regular"), out var style);
            float.TryParse(title.GetValueOrDefault("TitleFontSize", "14"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var titleSize);
            int.TryParse(title.GetValueOrDefault("TitleFontOffsetLeft", "8"), out var titleX);
            int.TryParse(title.GetValueOrDefault("TitleFontOffsetTop", "8"), out var titleY);
            preview.SetScene(new OverlayScene { Title = L("Appearance preview"),
                TitlePosition = options.TitlePosition, Subtitle = options.SolarSystem ? "Jita" : "",
                SubtitlePlacement = options.SystemPlacement, SubtitleFontSize = options.SystemFontSize,
                SubtitleColor = CombatOverlayFormatter.Color(options.GetAppearance().SystemColor),
                Font = new(title.GetValueOrDefault("TitleFontName", "Consolas"), Size: Math.Clamp(titleSize, 1, 200), Style: style,
                    Foreground: CombatOverlayFormatter.Color(title.GetValueOrDefault("TitleFontForeColor", "#FFFFFF")), Outline: 0xFF000000,
                    OffsetX: titleX, OffsetY: titleY),
                Stats = stats, StatsStyle = options.GetStatsStyle() });
        }
        Control Search<T>(string label, string name, IReadOnlyList<T> choices, T? current, Action<T?> changed) where T : class
        {
            var group = new StackPanel { Spacing = 4 };
            group.Children.Add(Text(label, 12));
            var picker = new AutoCompleteBox { Name = name, ItemsSource = choices, MinimumPrefixLength = 0,
                FilterMode = AutoCompleteFilterMode.Contains, ValueMemberBinding = new Binding("Name"),
                Watermark = L("Search…"), HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32,
                MaxDropDownHeight = 260, IsTextCompletionEnabled = false, SelectedItem = current, Text = current?.ToString() ?? "" };
            picker.SelectionChanged += (_, _) => { if (!_disposed) { changed(picker.SelectedItem as T); RefreshPreview(); } };
            picker.TextChanged += (_, _) =>
            {
                if (picker.SelectedItem is T selected && picker.Text != selected.ToString()) picker.SelectedItem = null;
            };
            group.Children.Add(WorkspacePickers.Search(picker, choices, F($"Browse {L(label)}"), ChoiceText)); return group;
        }
        Control Dropdown<T>(string label, string name, IReadOnlyList<T> choices, T? current, Action<T?> changed,
            Func<T, WeaponPlatform>? platform = null) where T : class
        {
            var group = new StackPanel { Spacing = 4 };
            group.Children.Add(Text(label, 12));
            var items = new List<object>();
            if (platform is null) items.AddRange(choices);
            else foreach (var category in choices.GroupBy(platform).OrderBy(x => ChoiceText(x.Key), StringComparer.CurrentCulture))
            {
                items.Add(new ComboBoxItem { Name = name + "-header-" + category.Key,
                    Content = Text(ChoiceText(category.Key), 11, true), IsEnabled = false, Focusable = false,
                    Padding = new(10, 5, 10, 2), MinHeight = 22 });
                items.AddRange(category.OrderBy(ChoiceText, StringComparer.CurrentCulture));
            }
            var picker = new ComboBox { Name = name, ItemsSource = items, SelectedItem = current,
                HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 260,
                ItemTemplate = new FuncDataTemplate<T>((item, _) => RawText(ChoiceText(item), 12)) };
            ScrollViewer.SetVerticalScrollBarVisibility(picker, ScrollBarVisibility.Visible);
            ScrollViewer.SetAllowAutoHide(picker, false);
            T? selectedValue = current;
            picker.SelectionChanged += (_, _) =>
            {
                if (_disposed) return;
                if (picker.SelectedItem is ComboBoxItem) { picker.SelectedItem = selectedValue; return; }
                selectedValue = picker.SelectedItem as T; changed(selectedValue); RefreshPreview();
            };
            group.Children.Add(picker); return group;
        }
        void RenderAmmo()
        {
            ammoPanel.Children.Clear();
            if (weapon?.ModelNote is { } note) ammoPanel.Children.Add(Text(note, 12));
            if (weapon is null || weapon.AmmoIds.Count == 0) { ammo = null; RefreshPreview(); return; }
            var compatible = catalog.Ammo.Where(x => weapon.AmmoIds.Contains(x.Id)).ToArray();
            ammo = compatible.FirstOrDefault(x => x.Id == ammo?.Id) ?? compatible.FirstOrDefault();
            ammoPanel.Children.Add(Dropdown("Ammunition", "logs-sim-ammo", compatible, ammo, selected => ammo = selected));
            RefreshPreview();
        }
        void RenderSources()
        {
            sources.Children.Clear();
            if (!catalog.Build.HasValue) { RefreshPreview(); return; }
            if (kind == CombatantKind.Npc)
            {
                var all = new SimulationFaction(0, "All factions");
                sources.Children.Add(Choice("Faction", "logs-sim-faction", new[] { all }.Concat(catalog.Factions).ToArray(), faction ?? all, value =>
                {
                    faction = value.Id == 0 ? null : value;
                    if (npc is not null && faction is not null && npc.FactionId != faction.Id) npc = null;
                    npcSelectionValid = true;
                    RenderSources(); return Task.CompletedTask;
                }));
                var ships = catalog.Npcs.Where(x => faction is null || x.FactionId == faction.Id).ToArray();
                var any = new SimulationNpc(0, "Any ship", null, []);
                sources.Children.Add(Search("NPC ship", "logs-sim-npc", new[] { any }.Concat(ships).ToArray(), npc ?? any,
                    selected => { npc = selected?.Id == 0 ? null : selected; npcSelectionValid = selected is not null; }));
            }
            if (kind == CombatantKind.Player || mixed || direction == DamageDirection.Outgoing)
            {
                sources.Children.Add(Dropdown(kind == CombatantKind.Npc ? "Your weapon" : "Weapon", "logs-sim-weapon", catalog.Weapons, weapon,
                    selected => { weapon = selected; RenderAmmo(); }, selected => selected.Attack.Platform));
                sources.Children.Add(ammoPanel); RenderAmmo();
            }
            RefreshPreview();
        }
        // Empty search text is not the deliberate "Any ship" choice.
        var selectedEvent = new StackPanel { Name = "logs-sim-selected-event", Spacing = 8, IsVisible = false };
        panel.Children.Add(Choice("Scenario", "logs-sim-scenario", new[] { "Mixed combat", "Selected event" }, "Mixed combat", x =>
        { mixed = x == "Mixed combat"; selectedEvent.IsVisible = !mixed; RenderSources(); return Task.CompletedTask; }));
        panel.Children.Add(Choice("Combatant", "logs-sim-kind", new[] { "Player", "NPC" }, "Player", x =>
        { kind = x == "NPC" ? CombatantKind.Npc : CombatantKind.Player; npcSelectionValid = true; RenderSources(); return Task.CompletedTask; }));
        panel.Children.Add(sources); panel.Children.Add(availability);
        selectedEvent.Children.Add(directionControl = Choice("Direction", "logs-sim-direction", Enum.GetValues<DamageDirection>(), direction,
            x => { direction = x; RenderSources(); return Task.CompletedTask; }));
        selectedEvent.Children.Add(Choice("Event", "logs-sim-effect", new[] { "Damage", "Shield repairs", "Armour repairs", "Hull repairs", "Mixed repairs" }, "Damage", x =>
        { mixedRepairTypes = x == "Mixed repairs";
            effect = x switch { "Shield repairs" or "Mixed repairs" => CombatEffect.ShieldRepair, "Armour repairs" => CombatEffect.ArmorRepair, "Hull repairs" => CombatEffect.HullRepair, _ => CombatEffect.Damage };
            RefreshPreview(); return Task.CompletedTask; }));
        panel.Children.Add(selectedEvent);
        panel.Children.Add(bothRepairsControl = Toggle("Incoming and outgoing repairs together", "logs-sim-both-repairs", bothRepairs,
            x => { bothRepairs = x; RefreshPreview(); return Task.CompletedTask; }));
        panel.Children.Add(repairSourcesControl = Number("Repair sources", "logs-sim-repair-sources", repairSources, 1, 20,
            x => { repairSources = x; RefreshPreview(); return Task.CompletedTask; }));
        panel.Children.Add(Number("Damage scale (%)", "logs-sim-scale", damagePercent, 1, 10000, x => { damagePercent = x; RefreshPreview(); return Task.CompletedTask; }));
        panel.Children.Add(Number("Duration (seconds)", "logs-sim-duration", duration, 1, 60, x => { duration = x; return Task.CompletedTask; }));
        void ShowResult(CommandResult command)
        {
            if (_disposed) return;
            result.Text = command.Localize(L); result.IsVisible = !string.IsNullOrWhiteSpace(command.Message);
            result.Foreground = WorkspaceTheme.Brush(command.Success ? _theme.Positive : _theme.Danger);
        }
        var buttons = new WrapPanel();
        simulate = Button("Simulate", "logs-simulate", async () =>
        {
            if (kind == CombatantKind.Npc && !npcSelectionValid && (mixed || effect == CombatEffect.Damage)) { ShowResult(CommandResult.Error("Choose an NPC ship or Any ship.")); return; }
            ShowResult(await _logs.SimulateLogEventAsync(Request())); RefreshPreview();
        });
        simulate.IsEnabled = false; buttons.Children.Add(simulate);
        buttons.Children.Add(Button("Stop simulation", "logs-sim-stop", async () => ShowResult(await _logs.SimulateLogEventAsync(Request() with { Stop = true }))));
        panel.Children.Add(enableWeapons); panel.Children.Add(enableRepairs); panel.Children.Add(buttons); panel.Children.Add(result);
        async Task LoadCatalog()
        {
            if (_disposed || loading || _backend is not IWorkspaceStaticData data) return;
            int? build = data.ReadStaticData().Build;
            if (loaded && loadedBuild == build) return;
            loading = true; availability.Text = L("Loading simulation choices…"); availability.IsVisible = true; RefreshPreview();
            try { catalog = await data.ReadSimulationCatalogAsync(); }
            catch { catalog = CombatSimulationCatalog.Unavailable("Could not load simulation choices. Check Data setup."); }
            if (_disposed) return;
            loading = false; loaded = true; loadedBuild = build;
            if (build != data.ReadStaticData().Build) { await LoadCatalog(); return; }
            weapon = catalog.Weapons.FirstOrDefault(x => x.Id == weapon?.Id)
                ?? catalog.Weapons.FirstOrDefault(x => x.Attack.Platform == WeaponPlatform.Rocket) ?? catalog.Weapons.FirstOrDefault();
            faction = catalog.Factions.FirstOrDefault(x => x.Id == faction?.Id);
            npc = catalog.Npcs.FirstOrDefault(x => x.Id == npc?.Id); npcSelectionValid = true;
            availability.Text = L(catalog.Error ?? ""); availability.IsVisible = !string.IsNullOrEmpty(catalog.Error);
            RenderSources();
        }
        _refreshAppearancePreview = RefreshPreview;
        _refreshSimulationCatalog = () => _ = LoadCatalog();
        _refreshSimulationCatalog(); RefreshPreview();
        return Card(panel);
    }
}
