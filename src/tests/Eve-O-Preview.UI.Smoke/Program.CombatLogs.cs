using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Threading;
using EveOPreview.Preview;

namespace EveOPreview.UI.Smoke;

internal static partial class Program
{
    private static void CapturePopup(Control content, string path)
    {
        var root = (TopLevel)content.GetVisualRoot()!;
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(
            (int)Math.Ceiling(root.Bounds.Width), (int)Math.Ceiling(root.Bounds.Height)));
        bitmap.Render(root); bitmap.Save(path);
    }

    private static int CheckInstalledWeaponChoices(string output, string path)
    {
        var catalog = System.Text.Json.JsonSerializer.Deserialize<CombatSimulationCatalog>(File.ReadAllText(path))!;
        int renders = 0;
        foreach (string theme in new[] { "Dark", "Light" })
        {
            var backend = new CombatBackend { SimulationCatalog = catalog };
            backend.ExecuteAsync(new("theme", Value: theme)).GetAwaiter().GetResult();
            using var view = new WorkspaceView(backend, [CombatLogView.CreateModule()]);
            var window = new Window { Width = 940, Height = 700, Content = view }; window.Show(); view.Navigate("Dps"); Flush();
            FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 1; Flush();
            FindControl<ComboBox>(view, "logs-sim-scenario").SelectedItem = "Selected event"; Flush();
            var installedWeapons = FindControl<ComboBox>(view, "logs-sim-weapon");
            var headers = installedWeapons.ItemsSource!.OfType<ComboBoxItem>().ToArray();
            Require(headers.Length == catalog.Weapons.Select(x => x.Attack.Platform).Distinct().Count()
                && headers.All(x => !x.IsEnabled && !x.Focusable), "Every platform must have a disabled heading.");
            object? selected = installedWeapons.SelectedItem;
            installedWeapons.SelectedItem = headers[0]; Flush();
            Require(ReferenceEquals(selected, installedWeapons.SelectedItem), "A platform heading must never become the selection.");
            var groupedItems = installedWeapons.ItemsSource!.Cast<object>().ToArray();
            int nextHeader = Array.IndexOf(groupedItems, headers[1]);
            installedWeapons.SelectedItem = groupedItems[nextHeader - 1];
            installedWeapons.BringIntoView(); Flush(); installedWeapons.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); Flush();
            Require(ReferenceEquals(installedWeapons.SelectedItem, groupedItems[nextHeader + 1]),
                "Keyboard navigation must skip disabled platform headings.");
            installedWeapons.SelectedItem = selected; Flush();
            installedWeapons.BringIntoView(); Flush(); installedWeapons.IsDropDownOpen = true; Flush();
            var visibleHeading = installedWeapons.GetRealizedContainers().OfType<ComboBoxItem>().First(x => x.GetVisualRoot() is not null);
            CapturePopup(visibleHeading, Path.Combine(output, theme.ToLowerInvariant() + "-sde-weapon-dropdown.png")); renders++;
            installedWeapons.IsDropDownOpen = false; Flush();
            foreach (var family in catalog.Weapons.GroupBy(x => x.Attack.Platform))
            {
                var weapon = family.First();
                FindControl<ComboBox>(view, "logs-sim-weapon").SelectedItem = weapon; Flush();
                Require(FindControl<Button>(view, "logs-simulate").IsEnabled, "Installed SDE weapon must be selectable: " + weapon.Name);
                var summary = FindControl<TextBlock>(view, "logs-sim-platforms");
                Require(summary.IsVisible && !string.IsNullOrWhiteSpace(summary.Text), "Installed platform summary must be visible.");
                var preview = view.GetVisualDescendants().OfType<EveOPreview.UI.Previews.AvaloniaPreviewOverlay>().Single();
                preview.BringIntoView(); Flush();
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-sde-" + family.Key + ".png")); renders++;
                Click(view, "logs-simulate");
                var request = backend.Simulations.Last();
                Require(request.UseStaticData && request.WeaponTypeId == weapon.Id, "The selected SDE ID must reach the normal host request.");
                Require(catalog.Resolve(request).Single().Attacks.Single().Platform == family.Key,
                    "Simulator must preserve the platform evidenced by its generated log.");
            }
            window.Close();
        }
        Console.WriteLine($"PASS: {renders} installed-SDE simulation selection renders.");
        return renders;
    }

    private static int CheckCombatLogs(string output)
    {
        var backend = new CombatBackend();
        using var view = new WorkspaceView(backend, [CombatLogView.CreateModule()]);
        var window = new Window { Width = 940, Height = 700, Content = view };
        window.Show(); view.Navigate("Dps"); Flush();
        int renders = 0;
        foreach (string theme in new[] { "Dark", "Light" })
        {
            backend.ExecuteAsync(new("theme", Value: theme)).GetAwaiter().GetResult(); Flush(); view.Navigate("Dps"); Flush();
            Require(view.GetVisualDescendants().Any(x => x.Name == "nav-Dps"), "Ready combat module must be available in modern navigation.");
            foreach (var (terms, route, tab) in new[] {
                (new[] { "dps", "reps", "incoming reps", "outgoing repairs", "alpha", "weapon icons", "weapon platform", "ammo", "ammunition", "damage type", "unknown", "thumbnail augments", "repair rate", "DPS settings" }, "thumbnail-augments", 1),
                (new[] { "log", "logs", "logging", "log folder", "configure logs", "data setup", "FC static data", "SDE" }, "data-setup", 2),
                (new[] { "reset statistics", "jumps", "average DPS", "combined statistics", "repair totals", "repair cycles" }, "overview", 0) })
            foreach (string query in terms)
            {
                FindControl<TextBox>(view, "search-settings").Text = query; Flush();
                var resultButton = FindControl<Button>(view, "search-module-Dps-" + route);
                Require(resultButton.IsEffectivelyVisible, "Search must locate the relevant section: " + query);
                Require(!view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "No matching settings" || x.Text?.StartsWith("0 settings matching") == true),
                    "A module search result must be counted and must not show an empty-search message.");
                if (query is "dps" or "logs") { Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-search-" + query + ".png")); renders++; }
                Click(view, "search-module-Dps-" + route); Flush();
                Require(FindControl<TabControl>(view, "logs-tabs").SelectedIndex == tab, "Search must open its exact section: " + query);
                Require(FindControl<TextBox>(view, "search-settings").Text == "", "Section navigation must clear the query.");
            }

            FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 0; Flush();
            Require(FindControl<TabControl>(view, "logs-tabs").Items.OfType<TabItem>().Last().Header as string == "Data setup",
                "Logs and static data share the Data setup tab.");
            Require(!view.GetVisualDescendants().Any(x => x.Name == "logs-flash-settings"), "Incoming indicator settings belong in Thumbnail augments, not Overview.");
            FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 1; Flush();
            FindControl<Expander>(view, "logs-flash-settings").IsExpanded = true; Flush();
            FindControl<CheckBox>(view, "logs-flash-enabled").IsChecked = false; Flush();
            Require(FindControl<Expander>(view, "logs-flash-settings").Header as string == "Incoming damage indicator (off)", "A disabled indicator must be visible in its collapsed heading.");
            FindControl<CheckBox>(view, "logs-flash-enabled").IsChecked = true; Flush();
            var flashTarget = FindControl<ComboBox>(view, "logs-flash-target");
            foreach (var target in Enum.GetValues<DamageFlashTarget>())
            {
                flashTarget.SelectedItem = target; Flush();
                Require(backend.Settings.FlashTarget == target, "Flash target must save through the normal backend.");
            }
            flashTarget.BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-damage-flash-target.png")); renders++;
            flashTarget.SelectedItem = DamageFlashTarget.Title; Flush();
            var animation = FindControl<ComboBox>(view, "logs-flash-animation");
            animation.SelectedItem = DamageFlashAnimation.Fade; Flush();
            Require(backend.Settings.FlashAnimation == DamageFlashAnimation.Fade, "Fade selection must reach persisted settings.");
            FindControl<NumericUpDown>(view, "logs-flash-opacity").Value = 10; Flush();
            Require(backend.Settings.FlashOpacityPercent == 10, "Thumbnail opacity must save through the normal settings backend.");
            FindControl<NumericUpDown>(view, "logs-flash-opacity").BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-damage-opacity-settings.png")); renders++;
            animation.BringIntoView(); Flush(); Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-damage-fade-settings.png")); renders++;
            animation.SelectedItem = DamageFlashAnimation.Blink; Flush();
            FindControl<NumericUpDown>(view, "logs-flash-interval").Value = 750; Flush();
            Require(backend.Settings.FlashIntervalMilliseconds == 750, "Flash interval must save through the workspace backend.");
            FindControl<NumericUpDown>(view, "logs-flash-interval").Value = 500; Flush();
            FindControl<TextBox>(view, "logs-flash-color").Text = "#AA44FF"; Click(view, "logs-flash-color-apply");
            var flashSources = FindControl<ComboBox>(view, "logs-flash-sources");
            flashSources.SelectedItem = "NPC + Player"; Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-indicator-settings.png")); renders++;
            FindControl<Expander>(view, "logs-flash-settings").IsExpanded = false; Flush();
            FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 0; Flush();
            backend.LastIncoming = DateTimeOffset.UtcNow; backend.LastPlayer = null; backend.Emit(); Flush();
            var compactRow = FindControl<Button>(view, "logs-character-row");
            Require(compactRow.Bounds.Height <= 52 && FindControl<TextBlock>(view, "logs-character-state").Text!.Contains("In 105 / Out 400 DPS"),
                "Overview must be one compact row with combined state beside the name.");
            Require(FindControl<Border>(view, "logs-character-portrait").GetVisualDescendants().OfType<Image>().Any(x => x.Source is not null),
                "The overview must display the cached character portrait.");
            Require(!view.GetVisualDescendants().Any(x => x.Name == "logs-character-details"), "Details must stay behind the character row.");
            Require(((ISolidColorBrush)FindControl<TextBlock>(view, "logs-character-name").Foreground!).Color == Color.Parse("#AA44FF"), "NPC incoming damage must flash with NPC + Player enabled.");
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-name-flash.png")); renders++;
            void WaitForName(bool highlighted)
            {
                var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    using (var tick = new CancellationTokenSource(TimeSpan.FromMilliseconds(20))) Dispatcher.UIThread.MainLoop(tick.Token);
                    Flush();
                    bool visible = ((ISolidColorBrush)FindControl<TextBlock>(view, "logs-character-name").Foreground!).Color == Color.Parse("#AA44FF");
                    if (visible == highlighted) return;
                }
                throw new InvalidOperationException("The overview name must blink without new log updates.");
            }
            WaitForName(false);
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-name-blink-off.png")); renders++;
            WaitForName(true);
            flashTarget.SelectedItem = DamageFlashTarget.Thumbnail; Flush();
            Require(((ISolidColorBrush)FindControl<TextBlock>(view, "logs-character-name").Foreground!).Color != Color.Parse("#AA44FF"),
                "Thumbnail-only flashing must leave the name colour alone.");
            flashTarget.SelectedItem = DamageFlashTarget.Both; Flush();
            WaitForName(true);
            flashTarget.SelectedItem = DamageFlashTarget.Title; Flush();
            flashSources.SelectedItem = "Player only"; Flush();
            Require(((ISolidColorBrush)FindControl<TextBlock>(view, "logs-character-name").Foreground!).Color != Color.Parse("#AA44FF"), "Player-only mode must ignore NPC incoming damage.");
            backend.LastPlayer = DateTimeOffset.UtcNow; backend.Emit(); Flush();
            Require(((ISolidColorBrush)FindControl<TextBlock>(view, "logs-character-name").Foreground!).Color == Color.Parse("#AA44FF"), "Player damage must flash the character name.");
            backend.LastPlayer = DateTimeOffset.UtcNow.AddSeconds(-3); backend.LastIncoming = backend.LastPlayer; backend.Emit(); Flush();
            Require(((ISolidColorBrush)FindControl<TextBlock>(view, "logs-character-name").Foreground!).Color != Color.Parse("#AA44FF"), "Expired damage must restore the name colour.");
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-overview.png")); renders++;
            int resetsBeforePopup = backend.ResetScopes.Count;
            var resetTrigger = FindControl<Button>(view, "logs-overview-reset");
            var resetFlyout = (Flyout)FlyoutBase.GetAttachedFlyout(resetTrigger)!;
            var resetContent = (Control)resetFlyout.Content!;
            Click(view, "logs-overview-reset"); Flush();
            Require(resetFlyout.IsOpen && backend.ResetScopes.Count == resetsBeforePopup, "Opening reset must leave all counters intact.");
            CapturePopup(resetContent, Path.Combine(output, theme.ToLowerInvariant() + "-reset-statistics.png")); renders++;
            Click(resetContent, "logs-overview-reset-cancel"); Flush();
            Require(!resetFlyout.IsOpen && backend.ResetScopes.Count == resetsBeforePopup, "Cancel must leave counters intact.");
            foreach (var scope in Enum.GetValues<CombatResetScope>())
            {
                Click(view, "logs-overview-reset"); Flush();
                FindControl<ComboBox>(resetContent, "logs-reset-scope").SelectedItem = scope; Flush();
                Click(resetContent, "logs-overview-reset-confirm"); Flush();
                Require(backend.ResetScopes.Last() == scope && backend.ResetCharacters.Last() is null && !resetFlyout.IsOpen,
                    "Full overview reset must send the chosen counters and all-character scope to the host.");
            }
            var sameRow = FindControl<Button>(view, "logs-character-row"); sameRow.Focus(); backend.Emit(); Flush();
            Require(ReferenceEquals(sameRow, FindControl<Button>(view, "logs-character-row")) && sameRow.IsFocused,
                "Ordinary log refreshes must preserve the clickable row and keyboard focus.");
            Click(view, "logs-character-row"); Flush();
            Click(view, "logs-overview-reset"); Flush();
            Require(FindControl<TextBlock>(resetContent, "logs-reset-target").Text == "Resets the selected counters for Aura Asuna only.",
                "Character detail reset must name the selected character in its confirmation.");
            CapturePopup(resetContent, Path.Combine(output, theme.ToLowerInvariant() + "-reset-character-statistics.png")); renders++;
            Click(resetContent, "logs-overview-reset-confirm"); Flush();
            Require(backend.ResetCharacters.Last() == "Aura Asuna", "Detail reset must send only its selected character to the host.");
            Require(FindControl<TextBlock>(view, "logs-detail-repairs-0-1").Text == "2,200 HP (8 cycles)" &&
                FindControl<TextBlock>(view, "logs-detail-repairs-0-2").Text == "1,700 HP (6 cycles)", "Repair amounts and cycles must be clearly labelled in two direction columns.");
            Require(FindControl<TextBlock>(view, "logs-detail-damage-0-4").Text == "200.0" &&
                FindControl<TextBlock>(view, "logs-detail-damage-1-3").Text == "-", "Details must show recorded averages and distinguish missing samples from zero DPS.");
            Require(FindControl<TextBlock>(view, "logs-detail-jumps").Text == "12", "Clicking a character must open their travel counters.");
            Require(FindControl<TextBlock>(view, "logs-detail-targets-0-1").Text == "7", "NPC types hit must be shown in character details.");
            var detailHistory = FindControl<Expander>(view, "logs-detail-history"); detailHistory.IsExpanded = true; backend.Emit(); Flush();
            Require(ReferenceEquals(detailHistory, FindControl<Expander>(view, "logs-detail-history")) && detailHistory.IsExpanded,
                "Live stats must not collapse or rebuild an open detail view.");
            detailHistory.IsExpanded = false;
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-character-details.png")); renders++;
            FindControl<TextBlock>(view, "logs-detail-repairs-2-2").BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-repair-details.png")); renders++;
            Click(view, "logs-details-back"); Flush();
            Require(FindControl<Button>(view, "logs-character-row").IsFocused, "Back must return keyboard focus to the character row.");
            backend.ExtraCharacters.Add(new("Orion Voss", 12345679, "Amarr", 30002187, null, null,
                [new(CombatantKind.Player, new(50, 90), new(0, 0))], 1) { Activity = new(DateTimeOffset.UtcNow, 3, 2, [], []) });
            backend.Emit(); Flush();
            var rows = view.GetVisualDescendants().OfType<Button>().Where(x => x.Name == "logs-character-row").ToArray();
            Require(FindControl<TextBlock>(view, "logs-combined-0-1").Text == "17,650" &&
                FindControl<TextBlock>(view, "logs-combined-0-2").Text == "83,640", "Combined damage must sum characters while keeping direction separate.");
            Require(FindControl<TextBlock>(view, "logs-combined-1-2").Text == "150.0" &&
                FindControl<TextBlock>(view, "logs-combined-jumps").Text == "System jumps: 15", "Combined averages must be weighted by valid time, with jumps summed across characters.");
            Require(FindControl<TextBlock>(view, "logs-combined-4-1").Text == "2,200 HP (8 cycles)", "Combined repairs must retain HP and cycle units.");
            Require(rows.Length == 2 && rows.All(x => x.Bounds.Height <= 52), "Every character must get one compact row.");
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-fleet-overview.png")); renders++;
            rows[1].Focus(); window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None); Flush();
            Require(FindControl<TextBlock>(view, "logs-character-name").Text == "Orion Voss" && FindControl<TextBlock>(view, "logs-detail-jumps").Text == "3",
                "Keyboard activation must open only the selected character's statistics.");
            Click(view, "logs-details-back"); backend.ExtraCharacters.Clear(); backend.Emit(); Flush();
            FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 1; Flush();
            FindControl<Expander>(view, "logs-overlay-exceptions").IsExpanded = true; Flush();
            FindControl<ComboBox>(view, "logs-overlay-character").SelectedItem = "All thumbnails (defaults)"; Flush();
            FindControl<Expander>(view, "logs-overlay-exceptions").IsExpanded = false; Flush();
            var mode = FindControl<ComboBox>(view, "logs-overlay-mode"); mode.SelectedItem = "Simple"; Flush();
            Require(!view.GetVisualDescendants().Any(x => x.Name == "logs-incoming-color"), "Simple mode must hide advanced colour settings.");
            Require(backend.Settings.DefaultOverlay.SolarSystem, "Simple defaults must show the system name.");
            Click(view, "logs-simulate");
            Require(backend.Simulations.Last() is { AllVisibleThumbnails: true, Randomize: true, DurationSeconds: 20 },
                "Default simulation must reach the live host for all visible thumbnails with a timed sequence.");
            Click(view, "logs-sim-stop");
            Require(backend.Simulations.Last().Stop, "Stop must reach the live host.");
            FindControl<CheckBox>(view, "logs-show-dps").IsChecked = false; Flush();
            Require(backend.Settings.DefaultOverlay.DamageEvents && !backend.Settings.DefaultOverlay.Incoming && !backend.Settings.DefaultOverlay.Outgoing, "Alpha-only preset must hide rolling DPS.");
            FindControl<CheckBox>(view, "logs-show-alpha").IsChecked = false; Flush();
            FindControl<CheckBox>(view, "logs-show-dps").IsChecked = true; Flush();
            Require(!backend.Settings.DefaultOverlay.DamageEvents && backend.Settings.DefaultOverlay.Incoming && backend.Settings.DefaultOverlay.Outgoing, "DPS-only preset must hide alpha events.");
            FindControl<CheckBox>(view, "logs-show-weapon-icon").IsChecked = false; Flush();
            Require(!backend.Settings.DefaultOverlay.Advanced && !backend.Settings.DefaultOverlay.GetAppearance().ShowWeaponIcon,
                "Simple weapon visibility must save without selecting Advanced mode.");
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Advanced"; Flush();
            Require(FindControl<CheckBox>(view, "logs-show-weapon-icon").IsChecked == false,
                "Weapon visibility must follow the saved setting between configuration modes.");
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Simple"; Flush();
            Click(view, "logs-sim-enable-weapons"); Flush();
            Require(!backend.Settings.DefaultOverlay.Advanced && backend.Settings.DefaultOverlay.DamageEvents
                && backend.Settings.DefaultOverlay.GetAppearance().ShowWeaponIcon,
                "The simulator must enable alpha and weapon icons without leaving Simple mode.");
            FindControl<CheckBox>(view, "logs-show-alpha").IsChecked = false; Flush();
            Require(backend.Settings.DefaultOverlay.GetAppearance().ShowWeaponIcon, "Hiding alpha must retain the weapon icon preference.");
            FindControl<CheckBox>(view, "logs-show-alpha").IsChecked = true; Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-simple.png")); renders++;
            string title = backend.Read().Clients[0].Title;
            FindControl<Expander>(view, "logs-overlay-exceptions").IsExpanded = true; Flush();
            FindControl<ComboBox>(view, "logs-overlay-character").SelectedItem = title; Flush();
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Simple"; Flush();
            FindControl<CheckBox>(view, "logs-show-alpha").IsChecked = false; Flush();
            FindControl<CheckBox>(view, "logs-show-weapon-icon").IsChecked = false; Flush();
            Require(!backend.Settings.Overlays[title].Advanced && !backend.Settings.Overlays[title].DamageEvents
                && !backend.Settings.Overlays[title].GetAppearance().ShowWeaponIcon
                && backend.Settings.DefaultOverlay.DamageEvents && backend.Settings.DefaultOverlay.GetAppearance().ShowWeaponIcon,
                "Simple per-client switches must leave shared defaults unchanged.");
            FindControl<CheckBox>(view, "logs-show-alpha").IsChecked = true; Flush();
            FindControl<CheckBox>(view, "logs-show-weapon-icon").IsChecked = true; Flush();
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Advanced"; Flush();
            Require(backend.Settings.Overlays[title].Advanced, "Advanced mode must save a character override.");
            FindControl<ComboBox>(view, "logs-overlay-position").SelectedItem = OverlayPosition.TopRight; Flush();
            FindControl<ComboBox>(view, "logs-overlay-order").SelectedItem = CombatRowOrder.RepairsOutgoingIncoming; Flush();
            Require(backend.Settings.Overlays[title].Position == OverlayPosition.TopRight && backend.Settings.Overlays[title].RowOrder == CombatRowOrder.RepairsOutgoingIncoming,
                "Position and row order must save for the selected client.");
            FindControl<Expander>(view, "logs-layout").BringIntoView(); Flush(); Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-augment-layout.png")); renders++;
            FindControl<ComboBox>(view, "logs-overlay-position").SelectedItem = OverlayPosition.BottomLeft; Flush();
            FindControl<ComboBox>(view, "logs-overlay-order").SelectedItem = CombatRowOrder.IncomingOutgoingRepairs; Flush();
            FindControl<Expander>(view, "logs-font-settings").IsExpanded = true; Flush();
            var font = FindControl<ComboBox>(view, "logs-font-family");
            var fonts = FindControl<ComboBox>(view, "logs-font-family");
            fonts.BringIntoView(); fonts.IsDropDownOpen = true; Flush();
            Require(fonts.IsDropDownOpen && ScrollViewer.GetVerticalScrollBarVisibility(fonts) == ScrollBarVisibility.Visible,
                "Font family must use a standard dropdown with a visible scrollbar.");
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-font-dropdown.png")); renders++;
            fonts.SelectedItem = "Arial"; fonts.IsDropDownOpen = false; Flush(); Click(view, "logs-font-apply");
            Require(backend.Settings.Overlays[title].FontFamily == "Arial", "DPS font override must save for the selected character.");
            FindControl<ComboBox>(view, "logs-font-style").SelectedItem = "Bold, Italic"; Flush();
            Require(backend.Settings.Overlays[title].FontStyle == (OverlayFontStyle.Bold | OverlayFontStyle.Italic), "DPS style must save independently.");
            FindControl<Button>(view, "logs-font-reset").BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-font.png")); renders++;
            font = FindControl<ComboBox>(view, "logs-font-family"); font.SelectedItem = "Segoe UI";
            backend.Emit(); Flush(); Require(view.HasUnappliedEdits, "Font drafts must survive background data updates.");
            Click(view, "logs-font-reset");
            Require(backend.Settings.Overlays[title].FontFamily is null && backend.Settings.Overlays[title].FontStyle is null,
                "Use title font must remove both overrides.");
            Require(!view.HasUnappliedEdits, "Resetting the font must remove its draft.");
            var incoming = FindControl<TextBox>(view, "logs-incoming-color");
            var colorPicker = FindControl<ColorPicker>(view, "logs-incoming-color-picker");
            colorPicker.Color = Color.Parse("#66AAFF"); Flush();
            Require(incoming.Text == "#66AAFF" && view.HasUnappliedEdits, "The popup colour picker must update the existing colour draft.");
            Require(colorPicker.IsComponentSliderVisible && colorPicker.IsComponentTextInputVisible && colorPicker.ColorModel == ColorModel.Rgba,
                "Colour pickers must expose RGB scales and numeric values.");
            Require(colorPicker.PaletteColors!.Count() == 24 && colorPicker.PaletteColors!.Contains(Color.Parse("#66AAFF")),
                "The palette must contain curated text, damage and repair colours.");
            colorPicker.BringIntoView(); Flush();
            var pickerPoint = colorPicker.TranslatePoint(new Point(colorPicker.Bounds.Width / 2, colorPicker.Bounds.Height / 2), window)!.Value;
            window.MouseDown(pickerPoint, MouseButton.Left); window.MouseUp(pickerPoint, MouseButton.Left); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-colour-picker.png")); renders++;
            colorPicker.SelectedIndex = 1; Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-colour-palette.png")); renders++;
            colorPicker.SelectedIndex = 2; Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-colour-rgb.png")); renders++;
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None); Flush();
            incoming.Text = "#EE3344";
            Click(view, "logs-incoming-color-apply");
            Require(backend.Settings.Overlays[title].GetAppearance().IncomingColor == "#EE3344", "Incoming text colour must save independently.");
            Require(FindControl<ComboBox>(view, "logs-overlay-character").SelectedItem as string == title, "Background settings refresh must retain the selected thumbnail.");
            FindControl<ComboBox>(view, "logs-sim-scenario").SelectedItem = "Selected event";
            Flush();
            var weaponPicker = FindControl<ComboBox>(view, "logs-sim-weapon");
            var previousWeapon = weaponPicker.SelectedItem;
            weaponPicker.BringIntoView(); Flush(); weaponPicker.IsDropDownOpen = true; Flush();
            Require(weaponPicker.IsDropDownOpen && weaponPicker.SelectedItem == previousWeapon
                && weaponPicker.ItemsSource!.OfType<SimulationWeapon>().Count() == backend.SimulationCatalog.Weapons.Count
                && ScrollViewer.GetVerticalScrollBarVisibility(weaponPicker) == ScrollBarVisibility.Visible,
                "The weapon dropdown must show every weapon without clearing the current selection.");
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-weapon-dropdown.png")); renders++;
            weaponPicker.SelectedItem = backend.SimulationCatalog.Weapons.Single(x => x.Id == 3178);
            weaponPicker.IsDropDownOpen = false; Flush();
            var ammoPicker = FindControl<ComboBox>(view, "logs-sim-ammo");
            Require(ammoPicker.ItemsSource!.Cast<SimulationAmmo>().All(x => x.Id == 230), "Ammo choices must follow the selected weapon.");
            ammoPicker.BringIntoView(); Flush(); ammoPicker.IsDropDownOpen = true; Flush();
            Require(ammoPicker.IsDropDownOpen, "Ammunition must open a standard dropdown.");
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-ammo-dropdown.png")); renders++;
            ammoPicker.IsDropDownOpen = false; Flush();
            Require(ammoPicker.SelectedItem is SimulationAmmo { Id: 230 }, "Closing the list must retain compatible ammunition.");
            Require(FindControl<TextBlock>(view, "logs-sim-platforms").Text!.Contains("Blaster"), "The simulator must identify the selected weapon platform.");
            FindControl<ComboBox>(view, "logs-text-mode").SelectedItem = CombatTextColorMode.Direction; Flush();
            FindControl<ComboBox>(view, "logs-overlay-measures").SelectedItem = "DPS only"; Flush();
            var weaponStyles = FindControl<Expander>(view, "logs-weapon-styles"); weaponStyles.IsExpanded = true; Flush();
            Require(weaponStyles.Header as string == "Weapon platform colours & icons (alpha hidden)"
                && !FindControl<TextBox>(view, "logs-weapon-Blaster-icon-color").IsEffectivelyEnabled,
                "Weapon styling must make the hidden-alpha dependency visible.");
            Click(view, "logs-sim-enable-weapons"); Flush();
            Require(backend.Settings.Overlays[title].DamageEvents && backend.Settings.Overlays[title].GetAppearance().ShowWeaponIcon,
                "The simulator action must save normal alpha and weapon visibility on the selected client.");
            FindControl<Expander>(view, "logs-weapon-styles").IsExpanded = true; Flush();
            Require(!FindControl<TextBox>(view, "logs-weapon-Blaster-text-color").IsEffectivelyEnabled,
                "Platform text colours are inactive while alpha follows direction colours.");
            FindControl<ComboBox>(view, "logs-text-mode").SelectedItem = CombatTextColorMode.WeaponPlatform; Flush();
            FindControl<Expander>(view, "logs-weapon-styles").IsExpanded = true; Flush();
            FindControl<TextBox>(view, "logs-weapon-Blaster-text-color").Text = "#AA44FF"; Click(view, "logs-weapon-Blaster-text-color-apply");
            var iconSample = FindControl<EveOPreview.UI.Previews.OverlaySymbolPreview>(view, "logs-weapon-Blaster-sample");
            FindControl<TextBox>(view, "logs-weapon-Blaster-icon-color").Text = "#FFD166"; Flush();
            Require(iconSample.Color == Color.Parse("#FFD166"), "The weapon sample must preview draft colours before Apply.");
            Click(view, "logs-weapon-Blaster-icon-color-apply");
            FindControl<ComboBox>(view, "logs-weapon-Blaster-icon").SelectedItem = OverlaySymbol.Railgun; Flush();
            Require(iconSample.Symbol == OverlaySymbol.Railgun, "The weapon sample must follow the chosen icon.");
            iconSample.BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-weapon-icon-sample.png")); renders++;
            foreach (var platform in Enum.GetValues<WeaponPlatform>().Where(x => (int)x >= 12))
            {
                var sample = FindControl<EveOPreview.UI.Previews.OverlaySymbolPreview>(view, "logs-weapon-" + platform + "-sample");
                Require(sample.Symbol == CombatAppearance.Preset("Classic").Weapons[platform].Icon, "Each added platform must have its production settings sample.");
                sample.BringIntoView(); Flush();
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-platform-" + platform + ".png")); renders++;
            }
            foreach (var (section, sampleName, symbol) in new[] { ("logs-damage-styles", "logs-damage-Kinetic-sample", OverlaySymbol.EveKinetic),
                ("logs-repair-styles", "logs-repair-ShieldRepair-sample", OverlaySymbol.Shield) })
            {
                var expander = FindControl<Expander>(view, section); expander.IsExpanded = true; Flush();
                var sample = FindControl<EveOPreview.UI.Previews.OverlaySymbolPreview>(view, sampleName);
                Require(sample.Symbol == symbol, "Damage and repair settings must show their actual icon.");
                sample.BringIntoView(); Flush();
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-" + sampleName + ".png")); renders++;
                expander.IsExpanded = false; Flush();
            }
            var platformStyle = backend.Settings.Overlays[title].GetAppearance().Weapons[WeaponPlatform.Blaster];
            Require(platformStyle.Color == "#FFD166" && platformStyle.TextColor == "#AA44FF" && platformStyle.Icon == OverlaySymbol.Railgun,
                "Weapon icon and alpha colour controls must save independent appearance values.");
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Simple"; Flush();
            FindControl<CheckBox>(view, "logs-show-weapon-icon").IsChecked = false; Flush();
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Advanced"; Flush();
            Require(!backend.Settings.Overlays[title].GetAppearance().ShowWeaponIcon
                && backend.Settings.Overlays[title].GetAppearance().Weapons[WeaponPlatform.Blaster] == platformStyle,
                "Editing Simple visibility must preserve saved Advanced colours and symbols.");
            FindControl<CheckBox>(view, "logs-show-weapon-icon").IsChecked = true; Flush();
            var platformPreview = view.GetVisualDescendants().OfType<EveOPreview.UI.Previews.AvaloniaPreviewOverlay>().Single();
            platformPreview.BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-weapon-platform.png")); renders++;
            using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)platformPreview.Bounds.Width, (int)platformPreview.Bounds.Height)))
            { bitmap.Render(platformPreview); bitmap.Save(Path.Combine(output, theme.ToLowerInvariant() + "-weapon-platform-alpha.png")); }
            FindControl<ComboBox>(view, "logs-overlay-measures").SelectedItem = "DPS only"; Flush();
            FindControl<ComboBox>(view, "logs-overlay-character").SelectedItem = "All thumbnails (defaults)"; Flush();
            FindControl<CheckBox>(view, "logs-show-alpha").IsChecked = false; Flush();
            Click(view, "logs-sim-enable-weapons"); Flush();
            Require(backend.Settings.DefaultOverlay.DamageEvents && backend.Settings.DefaultOverlay.GetAppearance().ShowWeaponIcon
                && backend.Settings.Overlays[title].DamageEvents && backend.Settings.Overlays[title].GetAppearance().ShowWeaponIcon,
                "All-thumbnail simulation must also enable the existing per-client display override.");
            Require(backend.Settings.Overlays[title].GetAppearance().Weapons[WeaponPlatform.Blaster] == platformStyle,
                "Enabling display must preserve customised weapon colours and symbols.");
            FindControl<ComboBox>(view, "logs-overlay-character").SelectedItem = title; Flush();
            var totals = backend.ReadLogs().Characters.Sum(x => x.Categories.Sum(c => c.Total.Incoming));
            Click(view, "logs-simulate");
            Require(backend.Simulations.Last() is { Kind: CombatantKind.Player, WeaponTypeId: 3178, AmmoTypeId: 230, UseStaticData: true }
                && backend.Simulations.Last().FullTitle == title, "Simulation must target the selected thumbnail, weapon and ammo.");
            Require(!backend.Simulations.Last().Randomize && !backend.Simulations.Last().AllVisibleThumbnails, "Selected event must retain the chosen target and event type.");
            Require(backend.ReadLogs().Characters.Sum(x => x.Categories.Sum(c => c.Total.Incoming)) == totals, "Simulation must not alter overall combat totals.");
            ammoPicker.BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-player-simulation.png")); renders++;
            FindControl<ComboBox>(view, "logs-sim-kind").SelectedItem = "NPC"; Flush();
            FindControl<ComboBox>(view, "logs-sim-faction").SelectedItem = backend.SimulationCatalog.Factions.Single(); Flush();
            var npcPicker = FindControl<AutoCompleteBox>(view, "logs-sim-npc");
            Require(npcPicker.ItemsSource!.Cast<SimulationNpc>().All(x => x.Id is 0 or 2385), "Faction selection must filter NPC ships.");
            npcPicker.SelectedItem = backend.SimulationCatalog.Npcs.Single(x => x.Id == 2385); Flush();
            Click(view, "logs-simulate");
            Require(backend.Simulations.Last() is { Kind: CombatantKind.Npc, NpcFactionId: 500010, NpcTypeId: 2385 }, "NPC selections must reach the host.");
            npcPicker.Text = "no matching ship"; Flush();
            Require(!FindControl<Button>(view, "logs-simulate").IsEnabled, "Incomplete NPC search must not silently simulate Any ship.");
            npcPicker.SelectedItem = npcPicker.ItemsSource!.Cast<SimulationNpc>().First(); Flush();
            FindControl<ComboBox>(view, "logs-sim-direction").SelectedItem = DamageDirection.Outgoing; Flush();
            Require(FindControl<ComboBox>(view, "logs-sim-weapon").SelectedItem is SimulationWeapon,
                "Outgoing NPC simulation must expose our own weapon instead of reversing NPC attacks.");
            FindControl<ComboBox>(view, "logs-sim-direction").SelectedItem = DamageDirection.Incoming; Flush();
            Click(view, "logs-simulate");
            Require(backend.Simulations.Last() is { Kind: CombatantKind.Npc, NpcFactionId: 500010, NpcTypeId: null }, "Faction-only simulation must retain Any ship.");
            npcPicker.BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-npc-simulation.png")); renders++;
            view.GetVisualDescendants().OfType<EveOPreview.UI.Previews.AvaloniaPreviewOverlay>().Single().BringIntoView(); Flush();
            var appearancePreview = view.GetVisualDescendants().OfType<EveOPreview.UI.Previews.AvaloniaPreviewOverlay>().Single();
            using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)appearancePreview.Bounds.Width, (int)appearancePreview.Bounds.Height)))
            { bitmap.Render(appearancePreview); bitmap.Save(Path.Combine(output, theme.ToLowerInvariant() + "-combat-icons.png")); }
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-simulation.png")); renders++;
            FindControl<CheckBox>(view, "logs-overlay-repairs").IsChecked = false; Flush();
            var completeOptions = backend.Settings.Overlays[title] with { Incoming = true, Outgoing = true, DamageEvents = true };
            backend.SaveLogSettingsAsync(backend.Settings with { Overlays = new Dictionary<string, LogOverlayOptions>(backend.Settings.Overlays) { [title] = completeOptions } }).GetAwaiter().GetResult(); Flush();
            FindControl<ComboBox>(view, "logs-sim-effect").SelectedItem = "Mixed repairs"; Flush();
            Click(view, "logs-sim-enable-repairs"); Flush();
            FindControl<NumericUpDown>(view, "logs-sim-repair-sources").Value = 6; Flush();
            FindControl<CheckBox>(view, "logs-sim-both-repairs").IsChecked = true; Flush();
            Click(view, "logs-simulate"); Flush();
            Require(backend.Simulations.Last() is { RepairSourceCount: 6, MixedRepairTypes: true, BothRepairDirections: true },
                "Multi-source mixed repairs must reach the host.");
            var repairPreview = view.GetVisualDescendants().OfType<EveOPreview.UI.Previews.AvaloniaPreviewOverlay>().Single();
            var canvas = typeof(EveOPreview.UI.Previews.AvaloniaPreviewOverlay).GetField("_sceneCanvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(repairPreview)!;
            OverlayScene PreviewScene() => (OverlayScene)canvas.GetType().GetField("_scene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(canvas)!;
            Require(PreviewScene().Stats.Select(x => x.Label).SequenceEqual(new[] { "In DPS", "Out DPS", "IN", "OUT" })
                && PreviewScene().Stats.All(x => x.Visible), "The appearance preview must show all enabled rows even for a selected repair event.");
            Require(PreviewScene().Stats.Skip(2).All(x => x.Suffix!.Segments().Count() == 3), "Every repair type must appear in the appearance preview.");
            repairPreview.BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-mixed-repair-rates.png")); renders++;
            FindControl<CheckBox>(view, "logs-sim-both-repairs").IsChecked = false;
            FindControl<CheckBox>(view, "logs-overlay-repairs").IsChecked = false; Flush();
            foreach (var pair in new[] { ("Shield repairs", CombatEffect.ShieldRepair), ("Armour repairs", CombatEffect.ArmorRepair), ("Hull repairs", CombatEffect.HullRepair) })
            {
                FindControl<ComboBox>(view, "logs-sim-effect").SelectedItem = pair.Item1; Flush();
                if (!backend.Settings.Overlays[title].Repairs)
                {
                    Require(FindControl<Button>(view, "logs-sim-enable-repairs").IsVisible, "Hidden repairs need an actionable enable control.");
                    Click(view, "logs-sim-enable-repairs"); Flush();
                }
                Click(view, "logs-simulate");
                Require(backend.Simulations.Last().Effect == pair.Item2 && backend.Settings.Overlays[title].Repairs,
                    "The selected repair effect and visible repair setting must reach the host.");
                appearancePreview.BringIntoView(); Flush();
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-" + pair.Item2 + ".png")); renders++;
                var simultaneous = FindControl<CheckBox>(view, "logs-sim-both-repairs");
                Require(simultaneous.IsVisible, "Selected repairs must offer both directions together.");
                simultaneous.IsChecked = true; Flush(); Click(view, "logs-simulate");
                Require(backend.Simulations.Last().BothRepairDirections && !FindControl<ComboBox>(view, "logs-sim-direction").IsEffectivelyEnabled,
                    "Both repair directions must reach the host and replace the single-direction selection.");
                appearancePreview.BringIntoView(); Flush();
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-both-" + pair.Item2 + ".png")); renders++;
                simultaneous.IsChecked = false; Flush();
            }
            FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 2; Flush();
            Require(FindControl<TextBlock>(view, "logs-static-status").Text!.Contains("102 datasets"), "The complete installed SDE must be visible in Data setup.");
            int downloads = backend.Downloads; Click(view, "logs-static-download");
            Require(backend.Downloads == downloads + 1, "Download/update must reach the static data service.");
            backend.BeginStaticDownload(); Flush();
            Require(!FindControl<Button>(view, "logs-static-download").IsEnabled && FindControl<Button>(view, "logs-static-cancel").IsVisible,
                "Downloading must disable duplicate starts and leave cancellation available.");
            Click(view, "logs-static-cancel");
            Require(!backend.ReadStaticData().Busy && FindControl<TextBlock>(view, "logs-static-status").Text!.Contains("cancelled"), "Cancellation must restore the offline data state.");
            FindControl<TextBlock>(view, "logs-static-status").BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-static-data.png")); renders++;
            var manualFolder = FindControl<Expander>(view, "logs-manual-folder");
            if (theme == "Dark") Require(!manualFolder.IsExpanded, "Manual folder override must be advanced and collapsed by default.");
            manualFolder.IsExpanded = true; Flush();
            FindControl<TextBox>(view, "logs-directory").Text = "C:\\Custom\\EVE\\logs";
            Click(view, "logs-apply-directory");
            Require(backend.Settings.Directory == "C:\\Custom\\EVE\\logs", "Manual folder override must save.");
            Click(view, "logs-auto-directory");
            Require(backend.Settings.Directory == "" && !manualFolder.IsExpanded, "Returning to automatic detection must clear the override.");
            manualFolder.IsExpanded = true; Flush();
            FindControl<TextBox>(view, "logs-directory").Text = "C:\\unapplied-folder";
            backend.Emit(); Flush(); Require(view.HasUnappliedEdits, "Log folder drafts must participate in the workspace close guard.");
            view.Navigate("Clients"); view.Navigate("Dps"); Flush();
            Require(FindControl<TextBox>(view, "logs-directory").Text == "C:\\unapplied-folder", "Navigation must preserve unapplied module fields.");
            var nextProfile = backend.Read().Profiles.First(x => x.Name != backend.Read().ProfileName);
            backend.ExecuteAsync(new("profile-switch", nextProfile.Id)).GetAwaiter().GetResult(); Flush();
            Require(!view.HasUnappliedEdits, "A completed profile switch must discard module drafts consistently with the workspace.");
            FindControl<Expander>(view, "logs-manual-folder").IsExpanded = true; Flush();
            Require(FindControl<TextBox>(view, "logs-directory").Text == backend.Settings.Directory, "Discarded draft text must not remain visible after a profile switch.");
            window.Width = 784; window.Height = 581; Flush();
            FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 1; Flush();
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Advanced"; Flush();
            var iconSection = view.GetVisualDescendants().OfType<Expander>().First(x => x.Header as string == "Damage type colours & icons");
            iconSection.IsExpanded = true; iconSection.BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-combat-advanced-minimum.png")); renders++;
            foreach (var control in view.GetVisualDescendants().OfType<Control>().Where(x => x.Name?.StartsWith("logs-") == true && x.Bounds.Width > 0))
            {
                var point = control.TranslatePoint(default, window);
                if (point.HasValue) Require(point.Value.X >= -1 && point.Value.X + control.Bounds.Width <= window.ClientSize.Width + 1,
                    "Combat controls must fit horizontally: " + control.Name);
            }
            window.Width = 940; window.Height = 700;
            view.NavigatePreviewTab("Titles"); Flush();
            foreach (var titlePosition in new[] { OverlayPosition.MiddleCenter, OverlayPosition.BottomRight, OverlayPosition.TopLeft })
            {
                FindControl<ComboBox>(view, "setting-TitlePosition").SelectedItem = titlePosition.ToString(); Flush();
                Click(view, "apply-preview-settings"); Flush();
                Require(backend.Settings.DefaultOverlay.TitlePosition == titlePosition && backend.Settings.Overlays.Values.All(x => x.TitlePosition == titlePosition),
                    "Title placement must follow the preview Apply route for every thumbnail.");
                FindControl<ComboBox>(view, "setting-TitlePosition").BringIntoView(); Flush();
                if (titlePosition != OverlayPosition.TopLeft)
                    Require(FindControl<ScrollViewer>(view, "title-preview-pan").Offset.Y > 0,
                        "The compact sample must pan to keep a middle or bottom title visible while editing.");
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-title-position-" + titlePosition + ".png")); renders++;
            }
            var systemColor = FindControl<TextBox>(view, "setting-SolarSystemColor");
            var savedColor = backend.Settings.DefaultOverlay.GetAppearance().SystemColor;
            string nextColor = theme == "Dark" ? "#55BBDD" : "#AABB55";
            systemColor.Text = nextColor; Flush();
            Require(backend.Settings.DefaultOverlay.GetAppearance().SystemColor == savedColor && view.HasUnappliedEdits,
                "Solar system colour must use the title editor's draft workflow.");
            view.Navigate("Clients"); view.NavigatePreviewTab("Titles"); Flush();
            Require(FindControl<TextBox>(view, "setting-SolarSystemColor").Text == nextColor, "Solar system drafts must survive navigation.");
            FindControl<CheckBox>(view, "setting-ShowCurrentSolarSystem").IsChecked = false; Flush();
            Click(view, "apply-preview-settings"); Flush();
            Require(!backend.Settings.DefaultOverlay.SolarSystem && backend.Settings.DefaultOverlay.GetAppearance().SystemColor == nextColor
                && backend.Settings.Overlays.Values.All(x => !x.SolarSystem && x.GetAppearance().SystemColor == nextColor),
                "Layout settings must apply solar system appearance to all thumbnails.");
            FindControl<CheckBox>(view, "setting-ShowCurrentSolarSystem").IsChecked = true; Flush();
            FindControl<CheckBox>(view, "solar-system-use-title-size").IsChecked = false; Flush();
            FindControl<NumericUpDown>(view, "setting-SolarSystemFontSize").Value = 20; Flush();
            Click(view, "apply-preview-settings"); Flush();
            Require(backend.Settings.DefaultOverlay.SystemFontSize == 20, "System size must save independently of title size.");
            foreach (var placement in Enum.GetValues<SubtitlePlacement>())
            {
                FindControl<ComboBox>(view, "setting-SolarSystemPlacement").SelectedItem = placement.ToString(); Flush();
                if (FindControl<Button>(view, "apply-preview-settings").IsEnabled) Click(view, "apply-preview-settings");
                Flush();
                Require(backend.Settings.DefaultOverlay.SystemPlacement == placement, "System placement must save.");
                FindControl<Control>(view, "preview-solar-system").BringIntoView(); Flush();
                Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-system-" + placement + ".png")); renders++;
            }
            FindControl<CheckBox>(view, "solar-system-use-title-size").IsChecked = true; Flush();
            Click(view, "apply-preview-settings"); Flush();
            Require(backend.Settings.DefaultOverlay.SystemFontSize is null, "Use title size must restore inheritance.");
            FindControl<Control>(view, "preview-solar-system").BringIntoView(); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-solar-system-settings.png")); renders++;
            view.Navigate("Dps"); Flush();
        }
        foreach (var language in WorkspaceLocalization.Languages)
        {
            backend.ExecuteAsync(new("language", Value: language.Code)).GetAwaiter().GetResult(); Flush();
            view.Navigate("Dps"); Flush();
            var localized = new WorkspaceLocalization(language.Code);
            foreach (var (term, section, tabIndex) in new[] { (localized.Get("Repairs"), "thumbnail-augments", 1), (localized.Get("Read EVE logs"), "data-setup", 2) })
            {
                FindControl<TextBox>(view, "search-settings").Text = term; Flush();
                Click(view, "search-module-Dps-" + section); Flush();
                Require(FindControl<TabControl>(view, "logs-tabs").SelectedIndex == tabIndex, "Localized search must open the right section: " + language.Code);
            }

            var tabs = FindControl<TabControl>(view, "logs-tabs"); tabs.SelectedIndex = 1; Flush();
            Require(tabs.Items.Cast<TabItem>().ElementAt(2).Header as string == localized.Get("Data setup"), "The renamed data tab must be localized.");
            Require(tabs.Items.Cast<TabItem>().ElementAt(1).Header as string == localized.Get("Thumbnail augments"),
                "Augments tabs must follow the selected language: " + language.Code);
            Require(FindControl<Button>(view, "logs-simulate").Content as string == localized.Get("Simulate"),
                "Simulation controls must follow the selected language.");
            Require(FindControl<ComboBox>(view, "logs-sim-scenario").Items.Cast<string>().Contains("Mixed combat"),
                "Translation must preserve selection identities.");
            FindControl<ComboBox>(view, "logs-overlay-mode").SelectedItem = "Advanced"; Flush();
            FindControl<ComboBox>(view, "logs-font-family").SelectedItem = "Arial";
            Capture(window, Path.Combine(output, "combat-language-" + language.Code + ".png")); renders++;
            tabs.SelectedIndex = 2; Flush();
            Require(FindControl<TextBlock>(view, "logs-status").Text == localized.Get(backend.Status),
                "Live log status must follow the selected language.");
            Capture(window, Path.Combine(output, "combat-setup-language-" + language.Code + ".png")); renders++;
        }
        backend.ExecuteAsync(new("language", Value: "en")).GetAwaiter().GetResult(); Flush();
        FindControl<TabControl>(view, "logs-tabs").SelectedIndex = 1; Flush();
        Require(FindControl<ComboBox>(view, "logs-font-family").SelectedItem as string == "Arial", "Changing language must retain unapplied font choices.");
        backend.SetStaticAvailable(false); Flush();
        Require(!FindControl<Button>(view, "logs-simulate").IsEnabled
            && FindControl<TextBlock>(view, "logs-sim-status").Text!.Contains("Download"), "Missing SDE must explain how to enable simulation.");
        backend.SetStaticAvailable(true); Flush();
        Require(FindControl<Button>(view, "logs-simulate").IsEnabled, "Installed SDE must enable simulation without reopening the page.");
        backend.Status = "Log storage unavailable. Check the folder and available disk space, then Retry."; backend.Emit(); Flush();
        Require(FindControl<TextBlock>(view, "logs-status").Text!.Contains("unavailable"), "Service errors must be visible.");
        backend.ExecuteAsync(new("theme", Value: "Legacy")).GetAwaiter().GetResult(); Flush(); view.Navigate("Dps"); Flush();
        Require(!view.GetVisualDescendants().Any(x => x.Name == "nav-Dps" || x.Name == "logs-simulate"), "Legacy must not expose the module.");
        view.Navigate("LegacySearch"); Flush(); FindControl<TextBox>(view, "search-settings").Text = "logs"; Flush();
        Require(!view.GetVisualDescendants().Any(x => x.Name?.StartsWith("search-module-Dps-") == true), "Legacy search must not expose modern Augments routes.");
        window.Close();
        foreach (bool accept in new[] { false, true })
        {
            var missing = new CombatBackend(); missing.SetStaticAvailable(false);
            using var setupView = new WorkspaceView(missing, [CombatLogView.CreateModule()]);
            var setupWindow = new Window { Width = 940, Height = 700, Content = setupView }; setupWindow.Show(); Flush();
            Require(missing.Downloads == 0, "Missing data must never download before consent.");
            Capture(setupWindow, Path.Combine(output, "static-data-confirmation-" + accept + ".png")); renders++;
            Click(setupView, accept ? "accept-confirmation" : "cancel-confirmation"); Flush();
            Require(missing.Downloads == (accept ? 1 : 0), "Only acceptance may start the download.");
            setupView.Navigate("Dps"); Flush(); setupView.RefreshFromBackend(); Flush();
            Require(!setupView.GetVisualDescendants().OfType<Button>().Any(x => x.Name == "accept-confirmation"), "The same session must not repeatedly prompt.");
            FindControl<TabControl>(setupView, "logs-tabs").SelectedIndex = 1; Flush();
            Require(!FindControl<Button>(setupView, "logs-simulate").IsEnabled, "No SDE must disable SDE-dependent simulation.");
            Require(FindControl<CheckBox>(setupView, "logs-show-alpha").IsEnabled
                && FindControl<CheckBox>(setupView, "logs-show-weapon-icon").IsEnabled, "Basic augments remain configurable without SDE.");
            setupWindow.Close();
        }
        return renders;
    }

    private sealed class CombatBackend : IWorkspaceBackend, IWorkspaceCombatLogs, IWorkspaceStaticData, IWorkspacePortraitProvider
    {
        private readonly SmokeBackend _inner = new();
        public CombatLogSettings Settings { get; private set; } = new() { Enabled = true };
        public List<CombatSimulation> Simulations { get; } = new();
        public string Status = "Watching EVE logs";
        private readonly DateTimeOffset _since = DateTimeOffset.UtcNow.AddMinutes(-20);
        public DateTimeOffset? LastIncoming, LastPlayer;
        public CombatBackend() { _inner.Changed += () => Changed?.Invoke(); _inner.CompletePortrait(); }
        public Task<byte[]?> GetCharacterPortraitAsync(long id) => _inner.GetCharacterPortraitAsync(id);
        public event Action? Changed;
        public event Action? LogsChanged;
        public event Action? StaticDataChanged;
        private StaticDataStatus _staticData = new("Static data ready offline.", 3503375, StoredBytes: 158171136, Datasets: 102, Records: 676271);
        public int Downloads;
        public StaticDataStatus ReadStaticData() => _staticData;
        public CombatSimulationCatalog SimulationCatalog { get; init; } = new(3503375,
            [new(500010, "Guristas Pirates")],
            [new(2385, "Guristas Despoiler", 500010, [new(null, WeaponPlatform.Railgun, DamageTypes.Kinetic | DamageTypes.Thermal, 2385, DamageEvidence.NpcAttack, 16, 2750, 3503375)]),
             new(56331, "Hypnosian Warden", 500024, [new(null, WeaponPlatform.Laser, DamageTypes.EM | DamageTypes.Thermal, 56331, DamageEvidence.NpcAttack, 468, 5000, 3503375)])],
            [new(10631, "Rocket Launcher II", new("Rocket Launcher II", WeaponPlatform.Rocket, 0, 10631, DamageEvidence.NamedItem, 0, 4000, 3503375), 1, [266]),
             new(3178, "Light Neutron Blaster II", new("Light Neutron Blaster II", WeaponPlatform.Blaster, 0, 3178, DamageEvidence.NamedItem, 0, 3500, 3503375), 4.41, [230])],
            [new(266, "Scourge Rocket", DamageTypes.Kinetic, 33), new(230, "Antimatter Charge S", DamageTypes.Kinetic | DamageTypes.Thermal, 13.8)]);
        public Task<CombatSimulationCatalog> ReadSimulationCatalogAsync() => Task.FromResult(_staticData.Build.HasValue ? SimulationCatalog
            : CombatSimulationCatalog.Unavailable("Download FC static data in Data setup."));
        public void SetStaticAvailable(bool value) { _staticData = _staticData with { Build = value ? 3503375 : null }; StaticDataChanged?.Invoke(); }
        public Task<CommandResult> UpdateStaticDataAsync() { Downloads++; return Task.FromResult(CommandResult.Ok("Static data is up to date.")); }
        public void BeginStaticDownload() { _staticData = _staticData with { Busy = true, Progress = .5 }; StaticDataChanged?.Invoke(); }
        public void CancelStaticDataUpdate() { _staticData = _staticData with { Busy = false, Progress = null, Message = "Update cancelled; previous static data is still available." }; StaticDataChanged?.Invoke(); }
        public WorkspaceSnapshot Read()
        {
            var snapshot = _inner.Read();
            return snapshot with { Settings = new Dictionary<string, string>(snapshot.Settings)
            { ["ShowCurrentSolarSystem"] = Settings.DefaultOverlay.SolarSystem.ToString(), ["SolarSystemColor"] = Settings.DefaultOverlay.GetAppearance().SystemColor,
                ["TitlePosition"] = Settings.DefaultOverlay.TitlePosition.ToString(), ["SolarSystemPlacement"] = Settings.DefaultOverlay.SystemPlacement.ToString(), ["SolarSystemFontSize"] = (Settings.DefaultOverlay.SystemFontSize ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture) } };
        }
        public Task<CommandResult> ExecuteAsync(WorkspaceCommand command)
        {
            if (command is { Action: "setting", Target: "ShowCurrentSolarSystem" or "SolarSystemColor" or "SolarSystemPlacement" or "SolarSystemFontSize" or "TitlePosition" })
            {
                LogOverlayOptions Edit(LogOverlayOptions options) => command.Target switch
                {
                    "ShowCurrentSolarSystem" => options with { SolarSystem = bool.Parse(command.Value) },
                    "TitlePosition" => options with { TitlePosition = Enum.Parse<OverlayPosition>(command.Value) },
                    "SolarSystemPlacement" => options with { SystemPlacement = Enum.Parse<SubtitlePlacement>(command.Value) },
                    "SolarSystemFontSize" => options with { SystemFontSize = command.Value == "0" ? null : float.Parse(command.Value, System.Globalization.CultureInfo.InvariantCulture) },
                    _ => options with { SystemColor = command.Value }
                };
                return SaveLogSettingsAsync(Settings with { DefaultOverlay = Edit(Settings.DefaultOverlay),
                    Overlays = Settings.Overlays.ToDictionary(x => x.Key, x => Edit(x.Value)) });
            }
            return _inner.ExecuteAsync(command);
        }
        public CombatLogSettings ReadLogSettings() => Settings;
        public List<CharacterCombatSnapshot> ExtraCharacters { get; } = new();
        public CombatLogSnapshot ReadLogs() { var data = ReadBase(); return data with { Characters = data.Characters.Concat(ExtraCharacters).ToArray() }; }
        private CombatLogSnapshot ReadBase() => new(Status, "C:\\Sample\\EVE\\logs", _since, Settings.WindowSeconds,
            [new(Read().Clients[0].Title[6..], 12345678, "Jita", 30000142, _since.AddMinutes(15), _since.AddMinutes(19),
                [new(CombatantKind.Npc, new(15600, 74200), new(85, 320)) { IncomingDamageTypes = DamageTypes.All },
                 new(CombatantKind.Player, new(2000, 9350), new(20, 80)) { IncomingDamageTypes = DamageTypes.EM, HasUnresolvedIncomingDamage = true }], 2, 1, 1)
                { LastIncomingDamageAt = LastIncoming, LastPlayerDamageAt = LastPlayer,
                  Activity = new(_since, 12, 8, [new(CombatantKind.Npc, new(40, 210), new(2100, 3400), 7, 3)
                      { AverageDps = new(70, 200), DpsSampleSeconds = new(30, 10) },
                      new(CombatantKind.Player, new(3, 12), new(800, 2800), 2, 1)
                      { AverageDps = new(0, 100), DpsSampleSeconds = new(0, 10) }],
                      [new(CombatEffect.ShieldRepair, new(2200, 1700), new(8, 6)), new(CombatEffect.ArmorRepair, new(350, 0), new(2, 0))]) }], []);
        public Task<CommandResult> SaveLogSettingsAsync(CombatLogSettings settings)
        { Settings = settings; Changed?.Invoke(); LogsChanged?.Invoke(); return Task.FromResult(CommandResult.Ok()); }
        public Task<CommandResult> ResetCombatAsync() => Task.FromResult(CommandResult.Ok());
        public List<CombatResetScope> ResetScopes { get; } = new();
        public List<string?> ResetCharacters { get; } = new();
        public Task<CommandResult> ResetCombatAsync(CombatResetScope scope, string? character = null)
        { ResetScopes.Add(scope); ResetCharacters.Add(character); return Task.FromResult(CommandResult.Ok()); }
        public Task<CommandResult> RescanLogsAsync() => Task.FromResult(CommandResult.Ok());
        public Task<CommandResult> SimulateLogEventAsync(CombatSimulation simulation)
        { Simulations.Add(simulation); return Task.FromResult(CommandResult.Ok("Simulation uses the live display; temporary statistics are discarded when it ends.")); }
        public void Emit() => LogsChanged?.Invoke();
    }
}
