using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private string _previewTab = "Layout";
    private ScrollViewer? _previewEditorScroll;
    private readonly Dictionary<string, TextBlock> _previewValidation = new(StringComparer.Ordinal);
    private Button? _previewApplyButton, _previewResetButton;
    private Button? _previewActualButton, _previewFitButton;
    private TextBlock? _previewApplyStatus;
    private IReadOnlyList<string>? _fontNames;
    private bool _previewNarrow;
    private bool _applyingPreviewDrafts;

    public void NavigatePreviewTab(string tab)
    {
        _previewTab = tab is "Titles" or "Behavior" or "Advanced" ? tab : "Layout";
        Navigate(_theme.Legacy && tab == "Advanced" ? "AdvancedPreview" : "Previews");
    }

    private string DraftValue(string key, string fallback = "") => _drafts.GetValueOrDefault(key, _snapshot.Settings.GetValueOrDefault(key, fallback));

    private void StagePreviewSetting(string key, string value)
    {
        if (value == _snapshot.Settings.GetValueOrDefault(key, "") && !_failedSettings.Contains(key)) _drafts.Remove(key);
        else _drafts[key] = value;
        UpdateFooter();
        UpdatePreviewEditorStatus();
        SchedulePreview();
    }

    private static bool IsPreviewSetting(SettingDefinition definition) => definition.Page is "Thumbnail" or "Zoom" or "Overlay" or "AdvancedPreview";

    private void UpdatePreviewEditorStatus()
    {
        var pending = _drafts.Where(pair => SettingCatalog.Find(pair.Key) is { } definition && IsPreviewSetting(definition)).ToArray();
        bool invalid = false;
        foreach (var entry in _previewValidation)
        {
            var definition = EffectiveDefinition(SettingCatalog.Find(entry.Key)!);
            var error = SettingCatalog.Validate(definition, DraftValue(entry.Key));
            entry.Value.Text = error ?? "";
            entry.Value.IsVisible = error is not null;
        }
        foreach (var entry in pending)
            invalid |= SettingCatalog.Validate(EffectiveDefinition(SettingCatalog.Find(entry.Key)!), entry.Value) is not null;
        var limitsError = ValidatePreviewSizeLimits();
        invalid |= limitsError is not null;
        if (_previewValidation.TryGetValue("ThumbnailMaximumHeight", out var limitsMessage) && limitsError is not null)
        {
            limitsMessage.Text = limitsError;
            limitsMessage.IsVisible = true;
        }
        if (_previewApplyButton is not null)
        {
            _previewApplyButton.Content = pending.Length == 0 ? "Apply changes" : $"Apply changes ({pending.Length})";
            _previewApplyButton.IsEnabled = pending.Length > 0 && !invalid && !_busy && !_applyingPreviewDrafts;
        }
        if (_previewResetButton is not null) _previewResetButton.IsEnabled = pending.Length > 0 && !_busy && !_applyingPreviewDrafts;
        if (_previewApplyStatus is not null)
            _previewApplyStatus.Text = _previewNarrow ? invalid ? "Fix invalid values before applying." : pending.Length == 0 ? "All changes applied." : "Preview only until Apply."
                : invalid ? "Fix the highlighted value before applying." : pending.Length == 0 ? "All preview settings are applied." : "Previewing edits. Apply to update your real previews.";
        if (_previewStatusText is not null)
        {
            _previewStatusText.Foreground = B(_previewRenderError is null ? _theme.Muted : _theme.Danger);
            _previewStatusText.Text = _previewRenderError is not null ? "Last valid preview shown. " + _previewRenderError
                : _backend is not IWorkspacePreviewRenderer ? "Illustrative preview. This host has no native title renderer."
                : _previewNarrow ? pending.Length > 0 ? "Previewing unapplied edits" : "Applied appearance"
                : (pending.Length > 0 ? "Sample includes unapplied edits. " : "") + PreviewBackgroundDescription;
            ToolTip.SetTip(_previewStatusText, _previewStatusText.Text);
        }
    }

    private async Task ApplyPreviewDrafts()
    {
        if (_busy || _applyingPreviewDrafts) return;
        var pending = _drafts.Where(pair => SettingCatalog.Find(pair.Key) is { } definition && IsPreviewSetting(definition)).ToArray();
        if (pending.Any(pair => SettingCatalog.Validate(EffectiveDefinition(SettingCatalog.Find(pair.Key)!), pair.Value) is not null)) return;
        if (ValidatePreviewSizeLimits() is not null) return;
        _deferPageRefresh = true;
        _applyingPreviewDrafts = true;
        UpdatePreviewEditorStatus();
        try
        {
            if (pending.Any(pair => PreviewSizeLimitKeys.Contains(pair.Key)))
            {
                var limits = PreviewSizeLimitKeys.ToDictionary(key => key, key => DraftValue(key));
                var result = await Run(new("preview-size-limits", Settings: limits));
                if (!result.Success) return;
                foreach (var key in PreviewSizeLimitKeys) _drafts.Remove(key);
            }
            foreach (var pair in pending.Where(pair => !PreviewSizeLimitKeys.Contains(pair.Key)).OrderBy(pair => pair.Key == "ThumbnailRefreshPeriod" ? 0 : 1))
            {
                var result = await Run(new("setting", pair.Key, pair.Value));
                if (!result.Success) break;
            }
        }
        finally { _applyingPreviewDrafts = false; _deferPageRefresh = false; RefreshFromBackend(); }
    }

    private void BuildPreviewWorkspace()
    {
        var previousOffset = _previewEditorScroll?.Offset ?? default;
        _previewNarrow = Bounds.Width > 0 && Bounds.Width < 1080;
        var workspace = new Grid { RowDefinitions = new RowDefinitions(_previewNarrow ? "Auto,*" : "Auto,Auto,*"), Margin = new Thickness(22, _previewNarrow ? 10 : 16, 22, 14) };
        var heading = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 12), Children = { Text("Previews & layout", 24, _theme.Text, true), Text("Size and placement, character titles, and a clear active-client border.", 12, _theme.Muted) } };
        if (!_previewNarrow) workspace.Children.Add(heading);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(0, 0, 0, 10) };
        foreach (var tab in new[] { ("Layout", "Size & zoom"), ("Titles", "Title & highlight"), ("Behavior", "Window behavior"), ("Advanced", "Advanced") })
        {
            var id = tab.Item1;
            var button = ActionButton(tab.Item2, () => { _previewTab = id; _previewEditorScroll = null; RenderPage(false); }, "preview-tab-" + id);
            button.Background = B(id == _previewTab ? _theme.AccentSurface : _theme.Surface);
            button.BorderBrush = B(id == _previewTab ? _theme.Accent : _theme.Border);
            if (_previewNarrow) { button.Padding = new Thickness(8, 6); button.FontSize = 11; }
            tabs.Children.Add(button);
        }
        if (_previewNarrow)
        {
            var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            toolbar.Children.Add(Text("Previews", 18, _theme.Text, true, margin: new Thickness(0, 0, 0, 10)));
            Grid.SetColumn(tabs, 1); tabs.HorizontalAlignment = HorizontalAlignment.Right; toolbar.Children.Add(tabs);
            workspace.Children.Add(toolbar);
        }
        else { Grid.SetRow(tabs, 1); workspace.Children.Add(tabs); }
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions(_previewNarrow ? "*" : "*,416"), RowDefinitions = new RowDefinitions(_previewNarrow ? "Auto,*" : "*") };
        var editor = new StackPanel { Spacing = 10 };
        _previewEditorScroll = new ScrollViewer { Name = "preview-editor-scroll", Content = editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        if (_previewNarrow) Grid.SetRow(_previewEditorScroll, 1);
        else _previewEditorScroll.Margin = new Thickness(0, 0, 14, 0);
        body.Children.Add(_previewEditorScroll);
        if (_previewTab == "Titles") BuildTitleEditor(editor);
        else if (_previewTab == "Behavior")
        {
            foreach (var setting in SettingCatalog.All.Where(s => s.Page == "General")) editor.Children.Add(Card(SettingRow(setting), new Thickness(14, 0)));
        }
        else if (_previewTab == "Advanced") BuildAdvancedPreviewEditor(editor);
        else BuildLayoutEditor(editor);
        var preview = BuildPinnedPreview();
        if (!_previewNarrow) Grid.SetColumn(preview, 1);
        else preview.Margin = new Thickness(0, 0, 10, 12);
        body.Children.Add(preview);
        Grid.SetRow(body, _previewNarrow ? 1 : 2); workspace.Children.Add(body);
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.Content = workspace;
        _previewEditorScroll.Offset = previousOffset;
        UpdatePreviewEditorStatus();
    }

    private Control BuildPinnedPreview()
    {
        var pane = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), VerticalAlignment = VerticalAlignment.Top };
        var content = new StackPanel { Spacing = _previewNarrow ? 4 : 7 };
        var scaleButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var actualSize = ActionButton("Actual size", () => SetPreviewScale(true), "preview-scale-actual", _previewActualSize);
        var fit = ActionButton("Fit width", () => SetPreviewScale(false), "preview-scale-fit", !_previewActualSize);
        _previewActualButton = actualSize; _previewFitButton = fit;
        actualSize.Padding = fit.Padding = new Thickness(8, 4);
        scaleButtons.Children.Add(actualSize); scaleButtons.Children.Add(fit);
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        toolbar.Children.Add(Text("Title & border sample", 11, _theme.Text, true));
        Grid.SetColumn(scaleButtons, 1); toolbar.Children.Add(scaleButtons);
        content.Children.Add(toolbar);
        var sample = BuildTitlePreview(0, _previewNarrow ? 40 : 96);
        content.Children.Add(sample);
        var states = new WrapPanel { Name = "preview-sample-states", Orientation = Orientation.Horizontal };
        var mode = new CheckBox { Name = "preview-active-client", Content = "Show as active character", IsChecked = _previewActive, FontSize = 11, MinHeight = 24, Margin = new Thickness(0, 0, 12, 0) };
        mode.IsCheckedChanged += (_, _) => { _previewActive = mode.IsChecked == true; UpdateFontPreview(); };
        states.Children.Add(mode);
        var skipped = SkipPreviewToggle(); skipped.MinHeight = 24;
        states.Children.Add(skipped);
        content.Children.Add(states);
        _previewScaleText = Text("", 10, _theme.Muted);
        _previewStatusText = Text("", 11, _theme.Muted);
        if (!_previewNarrow)
        {
            content.Children.Add(_previewScaleText);
            var sampleHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            sampleHeading.Children.Add(Text("Sample character", 10, _theme.Muted));
            if (_backend is IWorkspacePreviewCapture)
            {
                var refresh = ActionButton("Refresh image", () => CapturePreviewStill(true), "preview-refresh-image");
                refresh.FontSize = 10; refresh.Padding = new Thickness(6, 2); refresh.MinHeight = 22;
                ToolTip.SetTip(refresh, "Take another still from an open EVE client. The sample is not live.");
                Grid.SetColumn(refresh, 1); sampleHeading.Children.Add(refresh);
            }
            content.Children.Add(sampleHeading);
            var sampleTitle = new TextBox { Name = "preview-character-title", Text = _previewCharacter, Watermark = "Preview character name", MinHeight = 30, FontSize = 11 };
            AutomationProperties.SetName(sampleTitle, "Sample character name for preview only");
            sampleTitle.TextChanged += (_, _) =>
            {
                if (sampleTitle.Text == _previewCharacter) return;
                _previewCharacter = sampleTitle.Text ?? "";
                _previewCharacterEdited = true;
                SchedulePreview();
            };
            content.Children.Add(sampleTitle);
            content.Children.Add(_previewStatusText);
        }
        var card = Card(content, new Thickness(_previewNarrow ? 10 : 13));
        pane.Children.Add(card);
        var apply = new StackPanel { Spacing = 7, Margin = new Thickness(0, 10, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _previewApplyButton = ActionButton("Apply changes", async () => await ApplyPreviewDrafts(), "apply-preview-settings", true);
        _previewResetButton = ActionButton("Reset edits", () =>
        {
            foreach (var key in _drafts.Keys.Where(key => SettingCatalog.Find(key) is { } definition && IsPreviewSetting(definition)).ToArray()) _drafts.Remove(key);
            RenderPage(true);
        }, "reset-preview-settings");
        actions.Children.Add(_previewApplyButton); actions.Children.Add(_previewResetButton);
        _previewApplyStatus = Text("", 11, _theme.Muted);
        if (_previewNarrow)
        {
            var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            footer.Children.Add(actions);
            _previewApplyStatus.MaxLines = 1;
            _previewApplyStatus.TextTrimming = TextTrimming.CharacterEllipsis;
            _previewApplyStatus.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(_previewApplyStatus, 1); footer.Children.Add(_previewApplyStatus);
            content.Children.Add(footer);
        }
        else
        {
            apply.Children.Add(actions); apply.Children.Add(_previewApplyStatus);
            Grid.SetRow(apply, 1); pane.Children.Add(apply);
        }
        return pane;
    }

    private void SetPreviewScale(bool actualSize)
    {
        _previewActualSize = actualSize;
        if (_previewActualButton is not null) _previewActualButton.Background = B(actualSize ? _theme.AccentSurface : _theme.Surface);
        if (_previewFitButton is not null) _previewFitButton.Background = B(actualSize ? _theme.Surface : _theme.AccentSurface);
        foreach (var surface in _previewSurfaces) surface.ActualSize = actualSize;
        UpdatePreviewScale();
    }

    private void BuildTitleEditor(Panel parent)
    {
        var title = new StackPanel { Spacing = 10 };
        title.Children.Add(PreviewToggle("ShowThumbnailOverlays", "Show character title"));
        title.Children.Add(FieldGrid("*,100", FontFamilyField(), NumberField("TitleFontSize", "Size", 0.25m)));
        title.Children.Add(FontStyleField());
        title.Children.Add(ColorField("TitleFontForeColor", "Text color"));
        title.Children.Add(FieldGrid("*,100", ColorField("TitleFontOutlineColor", "Outline color"), NumberField("TitleFontOutlineWidth", "Width", 0.1m)));
        title.Children.Add(FieldGrid("*,*", NumberField("TitleFontOffsetLeft", "Left offset (px)"), NumberField("TitleFontOffsetTop", "Top offset (px)")));
        parent.Children.Add(Card(title, new Thickness(14)));
        var skipped = new StackPanel { Spacing = 9 };
        skipped.Children.Add(Text("Skipped characters", 13, _theme.Text, true));
        var skipStyle = new ComboBox { Name = "setting-CycleSkipIndicatorStyle", ItemsSource = SettingCatalog.Find("CycleSkipIndicatorStyle")!.Options,
            SelectedItem = DraftValue("CycleSkipIndicatorStyle", "Circle with slash"), HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32 };
        AutomationProperties.SetName(skipStyle, "Skipped character marker symbol");
        skipStyle.SelectionChanged += (_, _) => { if (skipStyle.SelectedItem is string style) StagePreviewSetting("CycleSkipIndicatorStyle", style); };
        skipped.Children.Add(LabeledPreviewField("CycleSkipIndicatorStyle", "Marker symbol", skipStyle));
        skipped.Children.Add(ColorField("CycleSkipIndicatorColor", "Marker color"));
        skipped.Children.Add(Text("Shown beside the title when a character is skipped. The marker stays visible if titles are hidden.", 11, _theme.Muted));
        parent.Children.Add(Card(skipped, new Thickness(14)));
        var highlight = new StackPanel { Spacing = 10 };
        highlight.Children.Add(PreviewToggle("EnableActiveClientHighlight", "Highlight active character"));
        highlight.Children.Add(FieldGrid("*,100", ColorField("ActiveClientHighlightColor", "Border color"), NumberField("ActiveClientHighlightThickness", "Thickness")));
        highlight.Children.Add(ActionButton("Per-character border colors", () => Navigate("ClientSettings"), "open-character-colors"));
        parent.Children.Add(Card(highlight, new Thickness(14)));
        parent.Children.Add(PreviewToggle("ShowThumbnailFrames", "Show standard window frames"));
        parent.Children.Add(Text("Offsets are measured from the preview's top-left corner. Negative offsets can crop the title. Window frames appear on real preview windows.", 11, _theme.Muted));
    }

    private void BuildLayoutEditor(Panel parent)
    {
        var size = new StackPanel { Spacing = 11, Children = { Text("Preview windows", 14, _theme.Text, true), FieldGrid("*,*", NumberField("ThumbnailWidth", "Width (px)"), NumberField("ThumbnailHeight", "Height (px)")), NumberField("ThumbnailOpacity", "Opacity (%)"), PreviewToggle("ShowThumbnailsAlwaysOnTop", "Keep previews on top") } };
        parent.Children.Add(Card(size, new Thickness(14)));
        var zoom = new StackPanel { Spacing = 10, Children = { PreviewToggle("EnableThumbnailZoom", "Zoom previews on hover"), NumberField("ThumbnailZoomFactor", "Magnification"), Text("Keep this point fixed when the preview grows", 11, _theme.Muted), DraftAnchorEditor() } };
        parent.Children.Add(Card(zoom, new Thickness(14)));
    }

    private CheckBox SkipPreviewToggle()
    {
        var check = new CheckBox { Name = "preview-skipped-client", Content = "Show skipped marker in sample", IsChecked = _previewSkipped, FontSize = 11 };
        check.IsCheckedChanged += (_, _) => { _previewSkipped = check.IsChecked == true; UpdateFontPreview(); };
        return check;
    }

    private Control FieldGrid(string columns, params Control[] fields)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 12 };
        for (var index = 0; index < fields.Length; index++) { Grid.SetColumn(fields[index], index); grid.Children.Add(fields[index]); }
        return grid;
    }

    private Control NumberField(string key, string label, decimal increment = 1)
    {
        var definition = EffectiveDefinition(SettingCatalog.Find(key)!);
        var value = DraftValue(key);
        var number = new NumericUpDown { Name = "setting-" + key, Minimum = (decimal)(definition.Minimum ?? -10000), Maximum = (decimal)(definition.Maximum ?? 10000), Increment = increment, MinHeight = 32, FontSize = 12, ClipValueToMinMax = false, ParsingNumberStyle = NumberStyles.Float, NumberFormat = CultureInfo.InvariantCulture.NumberFormat, FormatString = increment < 1 ? "0.######" : "0" };
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var initial)) number.Value = initial;
        number.Text = value;
        AutomationProperties.SetName(number, definition.Label);
        AutomationProperties.SetHelpText(number, definition.Description);
        number.PropertyChanged += (_, e) => { if (e.Property == NumericUpDown.TextProperty) StagePreviewSetting(key, number.Text ?? ""); };
        return LabeledPreviewField(key, label, number);
    }

    private Control LabeledPreviewField(string key, string label, Control editor)
    {
        var error = Text("", 10, _theme.Danger); error.IsVisible = false;
        _previewValidation[key] = error;
        return new StackPanel { Spacing = 4, Children = { Text(label, 11, _theme.Muted), editor, error } };
    }

    private Control PreviewToggle(string key, string label)
    {
        var toggle = new CheckBox { Name = "setting-" + key, Content = label, IsChecked = bool.TryParse(DraftValue(key), out var enabled) && enabled, FontSize = 12 };
        AutomationProperties.SetName(toggle, SettingCatalog.Find(key)?.Label ?? label);
        toggle.IsCheckedChanged += (_, _) => StagePreviewSetting(key, (toggle.IsChecked == true).ToString());
        return toggle;
    }

    private Control FontFamilyField()
    {
        _fontNames ??= _backend is IWorkspacePreviewRenderer renderer ? renderer.FontFamilies : FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var font = new AutoCompleteBox { Name = "setting-TitleFontName", Text = DraftValue("TitleFontName", "Arial"), ItemsSource = _fontNames, MinimumPrefixLength = 0, FilterMode = AutoCompleteFilterMode.Contains, MinHeight = 32, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(font, "Title font family. Type or choose an installed font");
        font.TextChanged += (_, _) => StagePreviewSetting("TitleFontName", font.Text ?? "");
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,30") };
        row.Children.Add(font);
        var show = ActionButton("⌄", () => { }, "show-font-list");
        show.Padding = new Thickness(6, 2);
        AutomationProperties.SetName(show, "Show installed font families");
        show.Click += (_, _) =>
        {
            var list = new ListBox { ItemsSource = _fontNames, SelectedItem = font.Text, Width = 280, MaxHeight = 280, FontSize = 12 };
            AutomationProperties.SetName(list, "Installed font families");
            var flyout = new Flyout { Content = list };
            list.SelectionChanged += (_, _) => { if (list.SelectedItem is string selected) { font.Text = selected; flyout.Hide(); } };
            flyout.ShowAt(show);
            if (list.SelectedItem is not null) list.ScrollIntoView(list.SelectedItem);
        };
        Grid.SetColumn(show, 1); row.Children.Add(show);
        return LabeledPreviewField("TitleFontName", "Font family", row);
    }

    private Control FontStyleField()
    {
        var style = DraftValue("TitleFontStyle", "Regular");
        var bits = int.TryParse(style, out var numeric) ? numeric : (style.Contains("Bold") ? 1 : 0) | (style.Contains("Italic") ? 2 : 0) | (style.Contains("Underline") ? 4 : 0) | (style.Contains("Strikeout") ? 8 : 0);
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var option in new[] { (1, "Bold"), (2, "Italic"), (4, "Underline"), (8, "Strikeout") })
        {
            var flag = option.Item1;
            var button = new ToggleButton { Name = "font-style-" + option.Item2, Content = option.Item2, IsChecked = (bits & flag) != 0, Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 5, 0), FontSize = 11 };
            AutomationProperties.SetName(button, "Title font " + option.Item2);
            button.IsCheckedChanged += (_, _) => { bits = button.IsChecked == true ? bits | flag : bits & ~flag; StagePreviewSetting("TitleFontStyle", SettingCatalog.Find("TitleFontStyle")!.Options![bits]); };
            row.Children.Add(button);
        }
        return row;
    }

    private Control ColorField(string key, string label)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,88"), ColumnSpacing = 7 };
        var value = DraftValue(key, "#FFFFFF");
        var hex = new TextBox { Name = "setting-" + key, Text = value, Watermark = "#RRGGBB", MinHeight = 32, FontSize = 11 };
        AutomationProperties.SetName(hex, SettingCatalog.Find(key)?.Label ?? label);
        var picker = ActionButton("", () => { }, "pick-" + key);
        picker.Width = 30; picker.Height = 30; picker.Padding = default;
        picker.Background = Color.TryParse(value, out var initial) ? new SolidColorBrush(initial) : Brushes.Transparent;
        AutomationProperties.SetName(picker, "Open color picker for " + label);
        picker.Click += (_, _) => ShowColorPicker(picker, hex);
        hex.TextChanged += (_, _) => { if (Color.TryParse(hex.Text, out var color)) picker.Background = new SolidColorBrush(color); StagePreviewSetting(key, hex.Text ?? ""); };
        row.Children.Add(picker);
        var palette = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var color in new[] { "#FFFFFF", "#000000", "#FFCC66", "#7DCFB6", "#7EA5FF", "#E995AE" })
        {
            var swatch = ActionButton("", () => hex.Text = color, "swatch-" + key + "-" + color[1..]);
            swatch.Width = 18; swatch.Height = 18; swatch.Margin = new Thickness(0, 0, 3, 3); swatch.Padding = default;
            swatch.Background = B(color); swatch.BorderBrush = B(_theme.Muted);
            AutomationProperties.SetName(swatch, label + " " + color);
            palette.Children.Add(swatch);
        }
        Grid.SetColumn(palette, 1); row.Children.Add(palette);
        Grid.SetColumn(hex, 2); row.Children.Add(hex);
        return LabeledPreviewField(key, label, row);
    }

    private void ShowColorPicker(Button anchor, TextBox hex, string saveHint = "Preview changes immediately. Use Apply changes to save.")
    {
        var color = Color.TryParse(hex.Text, out var parsed) ? parsed : Colors.White;
        var body = new StackPanel { Spacing = 10, Width = 255, Margin = new Thickness(8) };
        body.Children.Add(Text("Choose a color", 14, _theme.Text, true));
        var sample = new Border { Height = 28, Background = new SolidColorBrush(color), BorderBrush = B(_theme.Border), BorderThickness = new Thickness(1) };
        body.Children.Add(sample);
        var sliders = new List<Slider>();
        foreach (var channel in new[] { ("Red", color.R), ("Green", color.G), ("Blue", color.B) })
        {
            var slider = new Slider { Minimum = 0, Maximum = 255, Value = channel.Item2, TickFrequency = 1, IsSnapToTickEnabled = true };
            AutomationProperties.SetName(slider, channel.Item1 + " color channel");
            sliders.Add(slider);
            body.Children.Add(new StackPanel { Spacing = 2, Children = { Text(channel.Item1, 11, _theme.Muted), slider } });
        }
        foreach (var slider in sliders) slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty) return;
            var selected = Color.FromRgb((byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value);
            hex.Text = $"#{selected.R:X2}{selected.G:X2}{selected.B:X2}";
            sample.Background = new SolidColorBrush(selected);
        };
        body.Children.Add(Text(saveHint, 11, _theme.Muted));
        new Flyout { Content = body }.ShowAt(anchor);
    }

    private Control DraftAnchorEditor()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("38,38,38"), RowDefinitions = new RowDefinitions("32,32,32"), HorizontalAlignment = HorizontalAlignment.Left };
        var options = SettingCatalog.Find("ThumbnailZoomAnchor")!.Options!;
        var buttons = new Dictionary<string, Button>();
        foreach (var key in options)
        {
            var index = options.ToList().IndexOf(key);
            var button = ActionButton(key, () =>
            {
                StagePreviewSetting("ThumbnailZoomAnchor", key);
                foreach (var pair in buttons) pair.Value.Background = B(pair.Key == key ? _theme.AccentSurface : _theme.Surface);
            }, "anchor-" + key);
            button.Background = B(DraftValue("ThumbnailZoomAnchor") == key ? _theme.AccentSurface : _theme.Surface);
            button.FontSize = 10; button.Margin = new Thickness(2); button.Padding = default; button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center;
            AutomationProperties.SetName(button, "Zoom anchor " + key);
            buttons[key] = button;
            Grid.SetColumn(button, index % 3); Grid.SetRow(button, index / 3); grid.Children.Add(button);
        }
        return grid;
    }
}
