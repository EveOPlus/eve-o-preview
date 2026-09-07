using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private static readonly string[] PreviewSizeLimitKeys =
        ["ThumbnailMinimumWidth", "ThumbnailMinimumHeight", "ThumbnailMaximumWidth", "ThumbnailMaximumHeight"];
    private string? _clientSettingsTitle;

    private string? ValidatePreviewSizeLimits()
    {
        if (!PreviewSizeLimitKeys.Any(_drafts.ContainsKey)) return null;
        var values = PreviewSizeLimitKeys.Select(key => double.TryParse(DraftValue(key), CultureInfo.InvariantCulture, out var value) ? value : double.NaN).ToArray();
        return values[0] > values[2] || values[1] > values[3] ? "Minimum dimensions cannot exceed maximum dimensions." : null;
    }

    private void BuildAdvancedPreviewEditor(Panel parent)
    {
        parent.Children.Add(Card(new StackPanel { Spacing = 10, Children = {
            Text("Arranging & hiding previews", 14, _theme.Text, true),
            PreviewToggle("EnableThumbnailSnap", "Snap previews together"),
            NumberField("HideDelaySeconds", "Delay before hiding outside EVE (seconds)", .1m),
            Text("Used when Hide previews outside EVE is enabled. Rounds up to the next client check; review this delay after changing the check interval.", 11, _theme.Muted)
        } }, new Thickness(14)));
        parent.Children.Add(Card(new StackPanel { Spacing = 10, Children = {
            Text("Resize limits", 14, _theme.Text, true),
            Text("Apply these four limits together. The current preview size adjusts to fit them.", 11, _theme.Muted),
            FieldGrid("*,*", NumberField("ThumbnailMinimumWidth", "Minimum width"), NumberField("ThumbnailMinimumHeight", "Minimum height")),
            FieldGrid("*,*", NumberField("ThumbnailMaximumWidth", "Maximum width"), NumberField("ThumbnailMaximumHeight", "Maximum height"))
        } }, new Thickness(14)));
        parent.Children.Add(Card(new StackPanel { Spacing = 10, Children = {
            Text("Capture & client detection", 14, _theme.Text, true),
            NumberField("ThumbnailRefreshPeriod", "Client check interval (ms)"),
            Text("This controls discovery and property updates, not game FPS or live preview frame rate.", 11, _theme.Muted),
            PreviewToggle("EnableCompatibilityMode", "Use compatibility capture"),
            Text("Uses still-image previews when live previews do not work. Updates are slower; changing this recreates preview windows.", 11, _theme.Muted)
        } }, new Thickness(14)));
        parent.Children.Add(Card(new StackPanel { Spacing = 10, Children = {
            Text("Login preview position", 14, _theme.Text, true),
            Text("Starting position for login-screen previews. Negative coordinates support displays above or left of the main display.", 11, _theme.Muted),
            FieldGrid("*,*", NumberField("LoginThumbnailLeft", "X (px)"), NumberField("LoginThumbnailTop", "Y (px)"))
        } }, new Thickness(14)));
    }

    private void RenderLegacyAdvancedPreview()
    {
        _page.Children.Clear();
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(8) };
        var header = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(ActionButton("Back to Thumbnail", () => Navigate("Thumbnail"), "advanced-preview-back"));
        header.Children.Add(Text("Advanced previews", 18, _theme.Text, true));
        _previewApplyButton = ActionButton("Apply changes", async () => await ApplyPreviewDrafts(), "apply-preview-settings", true);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(_previewApplyButton);
        actions.Children.Add(ActionButton("Discard edits", () =>
        {
            foreach (var key in _drafts.Keys.Where(key => SettingCatalog.Find(key)?.Page == "AdvancedPreview").ToArray()) _drafts.Remove(key);
            RenderPage(true);
        }, "reset-advanced-settings"));
        header.Children.Add(actions); layout.Children.Add(header);
        var editor = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 8, 0) };
        BuildAdvancedPreviewEditor(editor);
        _previewEditorScroll = new ScrollViewer { Name = "preview-editor-scroll", Content = editor,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(_previewEditorScroll, 1); layout.Children.Add(_previewEditorScroll);
        _scroll.Content = layout;
        _scroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        UpdatePreviewEditorStatus();
    }

    private void RenderClientSettings()
    {
        _page.Children.Clear();
        _page.Margin = _theme.Legacy ? new Thickness(8) : new Thickness(22, 16, 22, 20);
        _page.Spacing = 10;
        _page.Children.Add(ActionButton("Back to clients", () => Navigate(_theme.Legacy ? "ActiveClients" : "Clients"), "client-settings-back"));
        Heading("Character settings", "Border colors and minimization exceptions for this profile. Offline characters work too.");
        var entries = _snapshot.ClientPreferences ?? [];
        var titles = entries.Select(entry => entry.Title).Concat(_snapshot.Clients.Select(client => client.Title))
            .Concat(_snapshot.SavedClientTitles ?? []).Concat(_snapshot.CycleGroups.SelectMany(group => group.Clients))
            .Distinct(StringComparer.Ordinal).OrderBy(title => title, StringComparer.OrdinalIgnoreCase).ToArray();
        _clientSettingsTitle ??= titles.FirstOrDefault();
        var picker = new AutoCompleteBox { Name = "client-settings-character", ItemsSource = titles, Text = _clientSettingsTitle ?? "",
            MinimumPrefixLength = 0, FilterMode = AutoCompleteFilterMode.Contains, MinHeight = 32, Watermark = "Choose or type a character name" };
        AutomationProperties.SetName(picker, "Online or offline character name");
        var pickerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 7 };
        pickerRow.Children.Add(picker);
        var edit = ActionButton("Edit", () =>
        {
            var title = picker.Text ?? "";
            if (!titles.Contains(title)) title = title.Trim();
            if (title.Length == 0 || title == "EVE -")
            {
                _message = "Choose a character or enter an offline character name.";
                _messageError = true; UpdateFooter(); return;
            }
            _clientSettingsTitle = titles.Contains(title) || title == "EVE" || title.StartsWith("EVE - ", StringComparison.Ordinal) ? title : "EVE - " + title;
            RenderPage(true);
        }, "edit-client-settings");
        Grid.SetColumn(edit, 1); pickerRow.Children.Add(edit); _page.Children.Add(pickerRow);
        if (_clientSettingsTitle is not { } selected) return;
        var saved = entries.FirstOrDefault(entry => entry.Title == selected) ?? new(selected, false);
        string colorKey = "client-color:" + selected, priorityKey = "client-priority:" + selected;
        var colorValue = _formDrafts.GetValueOrDefault(colorKey, saved.BorderColor);
        var priorityValue = _formDrafts.GetValueOrDefault(priorityKey, saved.Priority.ToString());
        var content = new StackPanel { Spacing = 12, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(Text(selected, 15, _theme.Text, true));
        var priority = new CheckBox { Name = "client-settings-priority", Content = "Keep open when switching characters", IsChecked = bool.TryParse(priorityValue, out var enabled) && enabled };
        AutomationProperties.SetName(priority, "Exempt this character from automatic minimization");
        content.Children.Add(priority);
        content.Children.Add(Text("This character is exempt from Minimize inactive clients. It can still be minimized manually.", 11, _theme.Muted));
        var inherit = new CheckBox { Name = "client-settings-inherit-color", Content = "Use the profile's border color", IsChecked = colorValue.Length == 0 };
        content.Children.Add(inherit);
        var color = new TextBox { Name = "client-settings-color", Text = colorValue.Length > 0 ? colorValue : _snapshot.Settings.GetValueOrDefault("ActiveClientHighlightColor", "#ADFF2F"), MinHeight = 32 };
        AutomationProperties.SetName(color, "Character active border color");
        var swatch = ActionButton("", () => { }, "client-settings-color-picker");
        swatch.Width = 30; swatch.Height = 30; swatch.Padding = default;
        AutomationProperties.SetName(swatch, "Choose a custom character border color");
        ToolTip.SetTip(swatch, "Choose a custom color");
        swatch.Click += (_, _) => { inherit.IsChecked = false; ShowColorPicker(swatch, color, "Use Save character settings to apply this color."); };
        var colorRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        colorRow.Children.Add(swatch); Grid.SetColumn(color, 1); colorRow.Children.Add(color); content.Children.Add(colorRow);
        var colors = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (string value in new[] { "#FF6B6B", "#FFCC66", "#ADFF2F", "#72C9FF", "#AC9CFF", "#FFFFFF" })
        {
            var button = ActionButton(new Border { Width = 22, Height = 22, Background = B(value), CornerRadius = new CornerRadius(4) }, () => { color.Text = value; inherit.IsChecked = false; }, "client-color-" + value[1..]);
            button.Padding = new Thickness(3); button.Margin = new Thickness(0, 0, 5, 0);
            AutomationProperties.SetName(button, "Use border color " + value); colors.Children.Add(button);
        }
        content.Children.Add(colors);
        content.Children.Add(Text("Used while this character is active and highlighting is enabled. Turn on Use the profile's border color to remove an override.", 11, _theme.Muted));
        var error = Text("", 11, _theme.Danger);
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);
        content.Children.Add(error);
        var save = ActionButton("Save character settings", async () =>
        {
            var border = inherit.IsChecked == true ? "" : color.Text ?? "";
            var keep = (priority.IsChecked == true).ToString();
            var result = await Run(new("client-preferences", selected, border, Settings: new Dictionary<string, string> { ["Priority"] = keep }));
            if (result.Success)
            {
                _formDrafts.Remove(colorKey); _formDrafts.Remove(priorityKey);
                RefreshFromBackend();
            }
        }, "save-client-settings", true);
        void Changed()
        {
            var border = inherit.IsChecked == true ? "" : color.Text ?? "";
            var keep = (priority.IsChecked == true).ToString();
            color.IsEnabled = inherit.IsChecked != true;
            error.Text = border.Length == 0 ? "" : SettingCatalog.Validate(SettingCatalog.Find("ActiveClientHighlightColor")!, border) ?? "";
            save.IsEnabled = error.Text.Length == 0;
            if (Color.TryParse(color.Text, out var paint)) swatch.Background = new SolidColorBrush(paint);
            if (border == saved.BorderColor) _formDrafts.Remove(colorKey); else _formDrafts[colorKey] = border;
            if (keep == saved.Priority.ToString()) _formDrafts.Remove(priorityKey); else _formDrafts[priorityKey] = keep;
            UpdateFooter();
        }
        priority.IsCheckedChanged += (_, _) => Changed();
        inherit.IsCheckedChanged += (_, _) => Changed();
        color.TextChanged += (_, _) => Changed();
        Changed();
        content.Children.Add(save);
        content.Children.Add(ActionButton("Discard character edits", () =>
        {
            _formDrafts.Remove(colorKey); _formDrafts.Remove(priorityKey); RenderPage(true);
        }, "discard-client-settings"));
        _page.Children.Add(Card(content, new Thickness(_theme.Legacy ? 8 : 16)));
    }
}
