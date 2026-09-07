using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private void AddSettings(string page, Func<SettingDefinition, bool>? predicate = null)
    {
        var settings = SettingCatalog.All.Where(s => s.Page == page && (predicate?.Invoke(s) ?? true)).ToArray();
        if (settings.Length == 0) return;
        var rows = new StackPanel();
        foreach (var definition in settings)
        {
            if (rows.Children.Count > 0) rows.Children.Add(new Border { Height = 1, Background = B(_theme.Border) });
            rows.Children.Add(SettingRow(definition));
        }
        _page.Children.Add(Card(rows, new Thickness(16, 0)));
    }

    private SettingDefinition EffectiveDefinition(SettingDefinition definition)
    {
        if (definition.Key is not ("ThumbnailWidth" or "ThumbnailHeight")) return definition;
        var axis = definition.Key == "ThumbnailWidth" ? "Width" : "Height";
        var minimum = _drafts.GetValueOrDefault("ThumbnailMinimum" + axis, _snapshot.Settings.GetValueOrDefault("ThumbnailMinimum" + axis, ""));
        var maximum = _drafts.GetValueOrDefault("ThumbnailMaximum" + axis, _snapshot.Settings.GetValueOrDefault("ThumbnailMaximum" + axis, ""));
        return definition with
        {
            Minimum = double.TryParse(minimum, CultureInfo.InvariantCulture, out var min) ? min : definition.Minimum,
            Maximum = double.TryParse(maximum, CultureInfo.InvariantCulture, out var max) ? max : definition.Maximum,
        };
    }

    private Control SettingRow(SettingDefinition original)
    {
        var definition = EffectiveDefinition(original);
        var value = _snapshot.Settings.GetValueOrDefault(definition.Key, "");
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(_theme.Legacy ? "*" : "*,228"),
            RowDefinitions = new RowDefinitions(_theme.Legacy ? "Auto,Auto" : "Auto"), Margin = new Thickness(0, 10) };
        var label = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = Text(definition.Label, 13, _theme.Text, true);
        label.Children.Add(title);
        label.Children.Add(Text(definition.Description, 11, _theme.Muted));
        row.Children.Add(label);
        Control editor;
        if (definition.Kind == SettingKind.Toggle)
        {
            var toggle = new ToggleSwitch { Name = "setting-" + definition.Key, IsChecked = bool.TryParse(value, out var active) && active, OnContent = "On", OffContent = "Off", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(toggle, definition.Label);
            toggle.IsCheckedChanged += async (_, _) => await Run(new("setting", definition.Key, (toggle.IsChecked == true).ToString()));
            editor = toggle;
        }
        else if (definition.Key == "ThumbnailZoomAnchor") editor = AnchorEditor(value);
        else
        {
            var stack = new StackPanel { Spacing = 7 };
            var fieldValue = _drafts.GetValueOrDefault(definition.Key, value);
            var error = Text("", 10, _theme.Danger);
            AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);
            var controls = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            var apply = ActionButton("Apply", async () => await ApplySetting(definition), "apply-" + definition.Key, true);
            apply.Margin = new Thickness(6, 0, 0, 0);
            var reset = ActionButton("↶", () => { _drafts.Remove(definition.Key); RenderPage(true); }, "reset-" + definition.Key);
            reset.Margin = new Thickness(4, 0, 0, 0);
            AutomationProperties.SetName(reset, "Discard unapplied edit to " + definition.Label);
            ToolTip.SetTip(reset, "Discard edit");
            void Changed(string updated)
            {
                if (updated == value && !_failedSettings.Contains(definition.Key)) _drafts.Remove(definition.Key); else _drafts[definition.Key] = updated;
                error.Text = SettingCatalog.Validate(definition, updated) ?? (_drafts.ContainsKey(definition.Key) ? "Unapplied edit" : "");
                error.Foreground = B(SettingCatalog.Validate(definition, updated) is null ? _theme.Muted : _theme.Danger);
                apply.IsEnabled = _drafts.ContainsKey(definition.Key) && SettingCatalog.Validate(definition, updated) is null;
                reset.IsVisible = _drafts.ContainsKey(definition.Key);
                UpdateFooter();
                if (definition.Page == "Overlay") UpdateFontPreview();
            }
            Control input;
            if (definition.Kind == SettingKind.Choice)
            {
                var choices = new ComboBox { Name = "setting-" + definition.Key, ItemsSource = definition.Options, MinHeight = 34, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 11 };
                if (definition.Key == "TitleFontStyle" && int.TryParse(fieldValue, out var style) && style is >= 0 and < 16) fieldValue = definition.Options![style];
                choices.SelectedItem = fieldValue;
                choices.SelectionChanged += (_, _) => Changed(choices.SelectedItem?.ToString() ?? "");
                input = choices;
            }
            else
            {
                var box = new TextBox { Name = "setting-" + definition.Key, Text = fieldValue, MinHeight = 34, FontSize = 12, Watermark = definition.Kind == SettingKind.Color ? "#RRGGBB" : "", HorizontalContentAlignment = HorizontalAlignment.Left };
                box.TextChanged += (_, _) => Changed(box.Text ?? "");
                box.KeyDown += async (_, e) =>
                {
                    if (e.Key == Key.Enter && apply.IsEnabled) { e.Handled = true; await ApplySetting(definition); }
                    if (e.Key == Key.Escape) { _drafts.Remove(definition.Key); RenderPage(true); e.Handled = true; }
                };
                if (definition.Kind == SettingKind.AudioIds)
                {
                    box.AcceptsReturn = false;
                    box.TextWrapping = TextWrapping.Wrap;
                    box.MinHeight = 70;
                }
                input = box;
            }
            AutomationProperties.SetName(input, definition.Label);
            AutomationProperties.SetHelpText(input, definition.Description);
            if (definition.Kind is SettingKind.AudioIds or SettingKind.Choice or SettingKind.Text)
            {
                stack.Children.Add(input);
                var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right };
                buttonRow.Children.Add(reset); buttonRow.Children.Add(apply);
                stack.Children.Add(buttonRow);
            }
            else
            {
                controls.Children.Add(input);
                Grid.SetColumn(apply, 1); controls.Children.Add(apply);
                Grid.SetColumn(reset, 2); controls.Children.Add(reset);
                stack.Children.Add(controls);
            }
            stack.Children.Add(error);
            Changed(fieldValue);
            editor = stack;
        }
        if (_theme.Legacy) Grid.SetRow(editor, 1); else Grid.SetColumn(editor, 1);
        row.Children.Add(editor);
        return row;
    }

    private async Task ApplySetting(SettingDefinition definition)
    {
        if (!_drafts.TryGetValue(definition.Key, out var value)) return;
        if (SettingCatalog.Validate(definition, value) is { } error) { _message = error; _messageError = true; UpdateFooter(); return; }
        var result = await Run(new("setting", definition.Key, value));
        if (result.Success) { if (_drafts.GetValueOrDefault(definition.Key) == value) _drafts.Remove(definition.Key); RefreshFromBackend(); }
    }

    private Control AnchorEditor(string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("42,42,42"), RowDefinitions = new RowDefinitions("38,38,38"), HorizontalAlignment = HorizontalAlignment.Right };
        var keys = new[] { "NW", "N", "NE", "W", "C", "E", "SW", "S", "SE" };
        var symbols = new[] { "↖", "↑", "↗", "←", "·", "→", "↙", "↓", "↘" };
        var labels = new[] { "Top left", "Top center", "Top right", "Middle left", "Center", "Middle right", "Bottom left", "Bottom center", "Bottom right" };
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            var button = CommandButton(symbols[index], new("setting", "ThumbnailZoomAnchor", key), "anchor-" + key);
            button.Margin = new Thickness(2); button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center;
            if (value == key) { button.Background = B(_theme.AccentSurface); button.BorderBrush = B(_theme.Accent); }
            ToolTip.SetTip(button, labels[index]);
            AutomationProperties.SetName(button, "Zoom anchor " + labels[index] + (value == key ? ", selected" : ""));
            Grid.SetColumn(button, index % 3); Grid.SetRow(button, index / 3); grid.Children.Add(button);
        }
        return grid;
    }

    private void RenderSearch()
    {
        var matches = SettingCatalog.All.Where(s => s.Matches(_query)).ToArray();
        Heading("Search results", $"{matches.Length} settings matching “{_query}”. Edit them here; your navigation stays available.");
        foreach (var group in matches.GroupBy(s => s.Page))
        {
            _page.Children.Add(Text(PageLabel(group.Key), 12, _theme.Accent, true));
            if (group.Key == "Overlay") _page.Children.Add(ActionButton("Open title & highlight editor", () => NavigatePreviewTab("Titles"), "search-open-title-editor", true));
            if (group.Key is "Thumbnail" or "Zoom") _page.Children.Add(ActionButton("Open size & zoom editor", () => NavigatePreviewTab("Layout"), "search-open-layout-editor", true));
            if (group.Key == "AdvancedPreview") _page.Children.Add(ActionButton("Open advanced preview settings", () => NavigatePreviewTab("Advanced"), "search-open-advanced-editor", true));
            var rows = new StackPanel();
            foreach (var definition in group) rows.Children.Add(SettingRow(definition));
            _page.Children.Add(Card(rows, new Thickness(20, 0)));
        }
        var routes = new[] { ("Switching", "Cycle groups, hotkeys and keyboard shortcuts"), ("Clients", "Active clients and preview visibility"), ("ClientSettings", "Character colors & minimization"), ("Profiles", "Profiles, clone, rename, delete and profile accent color"), ("Appearance", "Appearance, themes, Light, Dark and Legacy") };
        var showGlobalShortcuts = "hide all show all minimize all minimise all global hotkey keyboard shortcuts ToggleHideAllActiveHotkey MinimizeAllClientsHotkey".Contains(_query, StringComparison.OrdinalIgnoreCase);
        if (showGlobalShortcuts) AddGlobalHotkeys();
        var featureMatches = routes.Where(r => r.Item2.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || r.Item1 == "ClientSettings" && "PriorityClients PerClientActiveClientHighlightColor priority border minimization exceptions offline".Contains(_query, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var route in featureMatches) _page.Children.Add(ActionButton("Open " + route.Item2 + "  →", () => Navigate(route.Item1)));
        var moduleMatches = _modules.Where(m => !_theme.Legacy && $"{m.Title} {m.Description}".Contains(_query, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var module in moduleMatches) _page.Children.Add(ActionButton("Open " + module.Title + "  →", () => Navigate(module.Id)));
        if (matches.Length == 0 && featureMatches.Length == 0 && moduleMatches.Length == 0 && !showGlobalShortcuts)
            _page.Children.Add(Card(new StackPanel { Spacing = 9, Children = { Text("No matching settings", 18, _theme.Text, true), Text("Try a shorter term such as “opacity”, “FPS”, “hotkey” or “font”.", 13, _theme.Muted), ActionButton("Clear search", () => { _search.Text = ""; }) } }));
    }

    private static string PageLabel(string key) => key switch { "General" => "Window behavior & layouts", "Thumbnail" => "Preview windows", "Zoom" => "Hover zoom", "Overlay" => "Titles & highlighting", "AdvancedPreview" => "Advanced preview settings", "FpsAudio" => "Performance & audio", _ => key };

}
