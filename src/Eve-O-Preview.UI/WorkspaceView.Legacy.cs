using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    // Coordinates are logical pixels from the original MainForm's 460 x 417 client area.
    // Keep this layout independent of the modern cards/header while sharing commands and drafts.
    private Canvas? _legacyCanvas;
    private ComboBox? _legacyThemePicker;
    private bool _legacyRendering;
    private string? _legacySelectedClient;
    private static readonly FontFamily LegacyFont = new("Segoe UI, sans-serif");
    private const string LegacyThemeNotice = "Legacy may lack features.";
    private const string LegacyThemeDescription = "Maintained for existing features. Use Light or Dark for new features.";
    private static readonly string[] LegacyPageIds = { "General", "Thumbnail", "Zoom", "Overlay", "ActiveClients", "CycleGroups", "FpsAudio", "Profiles", "About" };
    private static readonly string[] LegacyPageTitles = { "General", "Thumbnail", "Zoom", "Overlay", "Active Clients", "Cycle Groups", "FPS / Audio", "Profiles", "About" };

    private void BuildLegacyShell()
    {
        if (Application.Current is { } app) app.RequestedThemeVariant = ThemeVariant.Light;
        FontFamily = LegacyFont;
        FontSize = 12;
        Foreground = Brushes.Black;
        Background = B("#F0F0F0");
        _navigation.Clear();
        _root = new Grid { ColumnDefinitions = new ColumnDefinitions("124,*"), Background = B("#F0F0F0") };
        Content = _root;
        var rail = new Canvas { Width = 124, Background = B("#F0F0F0") };
        _root.Children.Add(rail);
        for (var i = 0; i < LegacyPageIds.Length; i++)
        {
            string id = LegacyPageIds[i];
            var button = LegacyButton(LegacyPageTitles[i], () => Navigate(id), "nav-" + id);
            button.FontFamily = new FontFamily("Arial, sans-serif");
            button.FontSize = 13.5;
            if (id == "About") button.FontSize = 12;
            button.FontWeight = FontWeight.Bold;
            button.BorderBrush = Brushes.White;
            button.CornerRadius = new CornerRadius(0);
            _navigation[id] = button;
            Put(rail, button, 3, 4 + i * 35, id == "About" ? 76 : 120, 35);
            if (id == "About")
                Put(rail, LegacyButton("Exit", async () => await Run(new("exit")), "exit-application"), 81, 4 + i * 35, 42, 35);
        }

        _profileName = LegacyText(_snapshot.ProfileName, 10);
        _profileName.TextTrimming = TextTrimming.CharacterEllipsis;
        _profileCard = new Border { Name = "active-profile-card", BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(4, 0, 0, 0), Child = _profileName };
        ToolTip.SetTip(_profileCard, "Active profile. Change its identity color on the Profiles page.");
        Put(rail, _profileCard, 8, 326, 108, 17);
        _status = LegacyText("", 10);
        _status.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        Put(rail, _status, 8, 346, 108, 28);
        _draftStatus = LegacyText("", 10);
        _retryButton = LegacyButton("Retry update", async () => { if (_retryCommand is { } command) await Run(command); }, "retry-update");
        Put(rail, _retryButton, 8, 345, 108, 23);
        _legacyThemePicker = LegacyCombo(new[] { "Light", "Dark", "Legacy" }, "legacy-theme-picker");
        _legacyThemePicker.SelectedItem = "Legacy";
        _legacyThemePicker.SelectionChanged += async (_, _) =>
        {
            if (!_legacyRendering && _legacyThemePicker.SelectedItem is string theme && theme != _snapshot.Theme)
                await Run(new("theme", Value: theme));
        };
        AutomationProperties.SetName(_legacyThemePicker, "Application theme");
        ToolTip.SetTip(_legacyThemePicker, "Application theme (all profiles)");
        Put(rail, _legacyThemePicker, 8, 379, 108, 23);

        _search = LegacyTextBox("legacy-search", "");
        _page = new StackPanel { Spacing = 0, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch };
        _scroll = new ScrollViewer { Content = _page, Margin = new Thickness(0, 4, 4, 4), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(_scroll, 1);
        _root.Children.Add(_scroll);
        KeyDown -= HandleShellKey;
        KeyDown += HandleShellKey;
        UpdateLegacyHeader();
        RenderLegacyPage(false);
    }

    private void UpdateLegacyHeader()
    {
        if (_profileName is null) return;
        _profileName.Text = _snapshot.ProfileName;
        _profileCard.BorderBrush = ProfileAccent();
        UpdateLegacyFooter();
    }

    private void UpdateLegacyFooter()
    {
        if (_status is null) return;
        _status.Text = _messageError ? _message : HasUnappliedEdits
            ? _pageId is "AdvancedPreview" or "ClientSettings" ? "Apply or save your edits" : "Enter or leave field to save" : LegacyThemeNotice;
        _status.Foreground = _messageError ? B("#B00020") : B("#555555");
        ToolTip.SetTip(_status, _status.Text == LegacyThemeNotice ? LegacyThemeDescription : _message);
        _retryButton.IsVisible = _retryCommand is not null;
        _status.IsVisible = _retryCommand is null;
    }

    private void RenderLegacyPage(bool preservePosition)
    {
        if (_page is null) return;
        if (WorkspaceModules.ReservedIds.Contains(_pageId) || _modules.Any(module => module.Id == _pageId)) _pageId = "General";
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var focusName = focused?.Name;
        var caret = (focused as TextBox)?.CaretIndex;
        var offset = _scroll.Offset;
        _legacyRendering = true;
        try
        {
            if (_moduleContent is IDisposable disposable) disposable.Dispose();
            _moduleContent = null;
            ReleaseTitlePreviews();
            _scroll.Content = _page;
            _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _page.Children.Clear();
            _page.Margin = new Thickness(0);
            _page.Spacing = 0;
            _legacyCanvas = new Canvas { Width = 332, Height = 409, ClipToBounds = true, Background = B(_pageId is "Profiles" or "FpsAudio" ? "#FAFAFA" : "#F0F0F0") };
            _page.Children.Add(_legacyCanvas);
            foreach (var pair in _navigation)
            {
                bool selected = pair.Key == (_pageId switch { "ClientSettings" => "ActiveClients", "AdvancedPreview" => "Thumbnail", _ => _pageId });
                pair.Value.Background = B(selected ? "#F0F0F0" : "#A0A0A0");
                pair.Value.BorderBrush = selected ? B("#A0A0A0") : Brushes.White;
                pair.Value.Content = pair.Key == "ActiveClients" && _snapshot.AllPreviewsHidden ? "ALL HIDDEN" : LegacyPageTitles[Array.IndexOf(LegacyPageIds, pair.Key)];
            }
            if (_modules.FirstOrDefault(m => m.Id == _pageId) is { } module)
            {
                _moduleContent = module.CreateView(_backend);
                _page.Children.Clear();
                _page.Children.Add(_moduleContent);
            }
            else switch (_pageId)
            {
                case "General": LegacyGeneral(); break;
                case "Thumbnail": case "Previews": LegacyThumbnail(); break;
                case "Zoom": LegacyZoom(); break;
                case "Overlay": LegacyOverlay(); break;
                case "Clients": case "ActiveClients": LegacyClients(); break;
                case "Switching": case "CycleGroups": LegacyGroups(); break;
                case "FpsAudio": LegacyFpsAudio(); break;
                case "Profiles": LegacyProfiles(); break;
                case "Appearance": LegacyAppearance(); break;
                case "ThumbnailMenu": RenderThumbnailMenu(); break;
                case "AdvancedPreview": RenderLegacyAdvancedPreview(); break;
                case "ClientSettings": RenderClientSettings(); break;
                case "LegacySearch": LegacySearch(); break;
                default: LegacyAbout(); break;
            }
            _scroll.Offset = preservePosition ? offset : default;
            UpdateLegacyFooter();
        }
        finally { _legacyRendering = false; }
        if (preservePosition && focusName is not null && focused != _search)
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed || !_theme.Legacy) return;
                var replacement = _scroll.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == focusName);
                replacement?.Focus();
                if (replacement is TextBox box && caret is { } index) box.CaretIndex = Math.Min(index, box.Text?.Length ?? 0);
            }, DispatcherPriority.Loaded);
    }

    private void LegacyGeneral()
    {
        Put(_legacyCanvas!, new Border { BorderBrush = B("#646464"), BorderThickness = new Thickness(1) }, 4, 3, 324, 403);
        var rows = new[] { ("MinimizeToTray", "Minimize to System Tray", 8), ("EnableClientLayoutTracking", "Track client locations", 36), ("HideActiveClientThumbnail", "Hide preview of active EVE client", 63), ("MinimizeInactiveClients", "Minimize inactive EVE clients", 91), ("ShowThumbnailsAlwaysOnTop", "Previews always on top", 119), ("HideThumbnailsOnLostFocus", "Hide previews when EVE client is not active", 147), ("EnablePerClientThumbnailLayouts", "Unique layout for each EVE client", 174), ("EnableAutomaticCpuAffinity", "Dynamic CPU Affinity Strategy", 199) };
        foreach (var (key, label, y) in rows) Put(_legacyCanvas!, LegacySettingCheck(key, label), 14, y + 4, 300, 19);
        Put(_legacyCanvas!, LegacyButton("Advanced preview settings", () => NavigatePreviewTab("Advanced"), "legacy-advanced-preview"), 14, 236, 190, 24);
    }

    private void LegacyThumbnail()
    {
        Put(_legacyCanvas!, new Border { BorderBrush = B("#646464"), BorderThickness = new Thickness(1) }, 4, 3, 324, 403);
        Label("Opacity", 14, 14);
        var opacity = new Slider { Name = "setting-ThumbnailOpacity", Minimum = 20, Maximum = 100, Value = LegacyNumber("ThumbnailOpacity", 50), TickFrequency = 10, IsSnapToTickEnabled = false, Height = 25, MinHeight = 0 };
        ToolTip.SetTip(opacity, "Preview opacity. Use the arrow keys to adjust.");
        opacity.ValueChanged += (_, _) => { if (!_legacyRendering) _drafts["ThumbnailOpacity"] = Math.Round(opacity.Value).ToString(CultureInfo.InvariantCulture); };
        opacity.PointerReleased += async (_, _) => await LegacyApply("ThumbnailOpacity");
        opacity.KeyUp += async (_, _) => await LegacyApply("ThumbnailOpacity");
        Put(_legacyCanvas!, opacity, 76, 11, 223, 25);
        Label("Thumbnail Width", 14, 42); Numeric("ThumbnailWidth", 127, 40, 56);
        Label("Thumbnail Height", 14, 70); Numeric("ThumbnailHeight", 127, 67, 56);
        Put(_legacyCanvas!, LegacyButton("Advanced preview settings", () => NavigatePreviewTab("Advanced"), "legacy-advanced-preview"), 14, 106, 190, 24);
    }

    private void LegacyZoom()
    {
        Frame();
        Put(_legacyCanvas!, LegacySettingCheck("EnableThumbnailZoom", "Zoom on hover"), 10, 9, 260, 19);
        Label("Zoom Factor", 10, 39); Numeric("ThumbnailZoomFactor", 95, 37, 44);
        Label("Anchor", 10, 67);
        Put(_legacyCanvas!, new Border { BorderBrush = B("#646464"), BorderThickness = new Thickness(1) }, 95, 63, 90, 84);
        var keys = new[] { "NW", "N", "NE", "W", "C", "E", "SW", "S", "SE" };
        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            var anchor = LegacyCheck("", Value("ThumbnailZoomAnchor") == key, "anchor-" + key, true);
            anchor.IsEnabled = Value("EnableThumbnailZoom").Equals("true", StringComparison.OrdinalIgnoreCase);
            ToolTip.SetTip(anchor, key);
            AutomationProperties.SetName(anchor, "Zoom anchor " + key);
            anchor.IsCheckedChanged += async (_, _) => { if (!_legacyRendering && anchor.IsChecked == true) await Run(new("setting", "ThumbnailZoomAnchor", key)); };
            Put(_legacyCanvas!, anchor, 100 + (i % 3) * 32, 67 + (i / 3) * 30, 16, 16);
        }
    }

    private void LegacyOverlay()
    {
        Frame();
        Put(_legacyCanvas!, LegacySettingCheck("ShowThumbnailOverlays", "Show overlay"), 10, 9, 220, 19);
        Put(_legacyCanvas!, LegacySettingCheck("ShowThumbnailFrames", "Show frames"), 10, 37, 220, 19);
        Put(_legacyCanvas!, LegacySettingCheck("EnableActiveClientHighlight", "Highlight active client"), 10, 64, 260, 19);
        Label("Color", 7, 91);
        var highlight = LegacyButton("", async () => await Run(new("color-picker", "ActiveClientHighlightColor")), "setting-ActiveClientHighlightColor");
        highlight.Background = Color.TryParse(Value("ActiveClientHighlightColor"), out var color) ? new SolidColorBrush(color) : Brushes.GreenYellow;
        ToolTip.SetTip(highlight, "Choose active-client highlight color");
        Put(_legacyCanvas!, highlight, 50, 90, 108, 19);
        var extra = LegacyButton("…", () => ShowLegacySettingDialog("Active border width", new[] { "ActiveClientHighlightThickness" }), "legacy-border-width");
        ToolTip.SetTip(extra, "Active border width"); Put(_legacyCanvas!, extra, 163, 90, 25, 19);
        var font = Group("Title Font", 10, 130, 286, 152);
        Put(font, LegacyButton("Set Font", async () => await Run(new("font-picker")), "legacy-font-picker"), 7, 22, 65, 27);
        Put(font, LegacyButton("Forecolor", async () => await Run(new("color-picker", "TitleFontForeColor")), "legacy-font-color"), 98, 22, 69, 27);
        Put(font, LegacyButton("Outline Color", async () => await Run(new("color-picker", "TitleFontOutlineColor")), "legacy-outline-color"), 190, 22, 89, 27);
        Put(font, LegacyText("Offset Left"), 13, 59, 72, 16); Numeric("TitleFontOffsetLeft", 85, 55, 30, font, false);
        Put(font, LegacyText("Top"), 122, 59, 30, 16); Numeric("TitleFontOffsetTop", 160, 55, 30, font, false);
        Put(font, LegacyText("Outline"), 197, 59, 48, 16); Numeric("TitleFontOutlineWidth", 251, 55, 30, font, false);
        Put(font, BuildTitlePreview(272, 56), 7, 88, 272, 56);
        Put(_legacyCanvas!, LegacyButton("Skipped character marker…", () => ShowLegacySettingDialog("Skipped character marker", new[] { "CycleSkipIndicatorStyle", "CycleSkipIndicatorColor" }), "legacy-skip-marker"), 10, 294, 220, 27);
        Put(_legacyCanvas!, SkipPreviewToggle(), 10, 330, 280, 24);
    }

    private void LegacyClients()
    {
        var group = Group("All Clients", 5, 4, 292, 103);
        var hide = LegacyButton(_snapshot.AllPreviewsHidden ? "Show All" : "Hide All", async () => await Run(new("toggle-all")), "toggle-all-previews");
        if (_snapshot.AllPreviewsHidden) hide.Background = B("#BC8F8F");
        Put(group, hide, 7, 22, 112, 30);
        Put(group, LegacyButton("Minimize", async () => await Run(new("minimize-all")), "minimize-all-clients"), 7, 59, 112, 30);
        Put(group, LegacyText("Hotkey"), 124, 30, 45, 15); Put(group, LegacyHotkey("ToggleHideAllActiveHotkey", Value("ToggleHideAllActiveHotkey")), 170, 25, 109, 23);
        Put(group, LegacyText("Hotkey"), 124, 67, 45, 15); Put(group, LegacyHotkey("MinimizeAllClientsHotkey", Value("MinimizeAllClientsHotkey")), 170, 62, 109, 23);
        Label("Thumbnails (check to force hide)", 3, 113, 310);
        Put(_legacyCanvas!, LegacyButton("Colors / priority", () => Navigate("ClientSettings"), "open-client-settings"), 205, 110, 125, 24);
        var list = new StackPanel { Spacing = 0, Background = Brushes.White };
        foreach (var client in _snapshot.Clients)
        {
            var check = LegacyCheck(client.Title, !client.PreviewVisible, "client-" + client.Title);
            check.Height = 17;
            check.IsCheckedChanged += async (_, _) => { if (!_legacyRendering) await Run(new("client-visible", client.Title, (check.IsChecked != true).ToString())); };
            list.Children.Add(check);
        }
        Put(_legacyCanvas!, new Border { BorderBrush = B("#A0A0A0"), BorderThickness = new Thickness(1), Background = Brushes.White, Child = new ScrollViewer { Content = list } }, 2, 141, 330, 268);
    }

    private void LegacyGroups()
    {
        Label("Select Cycle Group:", 15, 12, 180);
        var groups = _snapshot.CycleGroups;
        if (!groups.Any(g => g.Id == _selectedGroup)) _selectedGroup = groups.FirstOrDefault()?.Id;
        var group = groups.FirstOrDefault(g => g.Id == _selectedGroup);
        if (group is not null)
        {
            var expand = LegacyButton("⛶", () => ExpandOrder(group.Id), "expand-cycle-order");
            ToolTip.SetTip(expand, "Expand character order to drag, skip or resume characters");
            AutomationProperties.SetName(expand, "Expand character order");
            Put(_legacyCanvas!, expand, 303, 151, 28, 28);
        }
        var picker = LegacyCombo(groups.Select(g => new ComboBoxItem { Content = g.Name, Tag = g.Id }).ToArray(), "cycle-group-picker");
        picker.SelectedIndex = groups.ToList().FindIndex(g => g.Id == _selectedGroup);
        picker.SelectionChanged += (_, _) => { if (!_legacyRendering && picker.SelectedItem is ComboBoxItem selected && selected.Tag is int id) { _selectedGroup = id; _legacySelectedClient = null; RenderPage(false); } };
        Put(_legacyCanvas!, picker, 19, 30, 223, 23);
        Put(_legacyCanvas!, LegacyButton("-", () => { if (group is not null) Confirm("Remove cycle group?", "Remove " + group.Name + " and its shortcuts?", "Remove", async () => await Run(new("group-delete", group.Id.ToString()))); }, "remove-cycle-group"), 245, 29, 28, 27);
        Put(_legacyCanvas!, LegacyButton("+", async () => { var result = await Run(new("group-add")); if (result.Success) { _selectedGroup = _snapshot.CycleGroups.LastOrDefault()?.Id; RenderPage(false); } }, "add-cycle-group"), 272, 29, 28, 27);
        Label("Description:", 15, 66, 90);
        var description = LegacyFormField("legacy-group-name-" + (group?.Id.ToString() ?? "none"), group?.Name ?? "", async value => { if (group is not null) await Run(new("group-rename", group.Id.ToString(), value)); });
        description.IsEnabled = group is not null; Put(_legacyCanvas!, description, 107, 62, 190, 23);
        Label("Forward Key:", 15, 96, 90); Label("Backward Key:", 15, 126, 90);
        for (int i = 0; i < 2; i++)
        {
            string id = group?.Id.ToString() ?? "-1";
            var forward = LegacyHotkey($"group:{id}:forward:{i}", group?.ForwardHotkeys.ElementAtOrDefault(i) ?? ""); forward.IsEnabled = group is not null;
            var backward = LegacyHotkey($"group:{id}:backward:{i}", group?.BackwardHotkeys.ElementAtOrDefault(i) ?? ""); backward.IsEnabled = group is not null;
            Put(_legacyCanvas!, forward, 107 + i * 97, 92, 93, 23); Put(_legacyCanvas!, backward, 107 + i * 97, 122, 93, 23);
        }
        Label("Clients and Order:", 15, 158, 125);
        Put(_legacyCanvas!, LegacyButton("Up", async () => { if (group is not null && _legacySelectedClient is not null) await Run(new("group-client-up", group.Id.ToString(), _legacySelectedClient)); }, "legacy-client-up"), 204, 152, 38, 27);
        Put(_legacyCanvas!, LegacyButton("-", async () => { if (group is not null && _legacySelectedClient is not null) await Run(new("group-client-remove", group.Id.ToString(), _legacySelectedClient)); }, "legacy-client-remove"), 245, 152, 26, 27);
        Put(_legacyCanvas!, LegacyButton("+", () => { if (group is not null) ShowLegacyClientPicker(group); }, "legacy-client-add"), 272, 152, 26, 27);
        var list = LegacyList(group?.Clients ?? Array.Empty<string>(), "legacy-cycle-clients");
        list.SelectedItem = group?.Clients.Contains(_legacySelectedClient ?? "") == true ? _legacySelectedClient : group?.Clients.FirstOrDefault();
        _legacySelectedClient = list.SelectedItem as string;
        var skip = LegacyButton("Skip", async () =>
        {
            if (_legacySelectedClient is string title) await Run(new("client-cycle-skip", title, (group?.SkippedClients?.Contains(title) != true).ToString()));
        }, "legacy-client-skip");
        void UpdateSkip() { skip.Content = group?.SkippedClients?.Contains(_legacySelectedClient ?? "") == true ? "Resume" : "Skip"; skip.IsEnabled = _legacySelectedClient is not null; }
        UpdateSkip();
        ToolTip.SetTip(skip, "Skip or resume the selected character in every cycle group in this profile");
        Put(_legacyCanvas!, skip, 143, 152, 58, 27);
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((title, _) => new TextBlock
        {
            Text = (group?.SkippedClients?.Contains(title) == true ? "Skipped · " : "") + title,
            Foreground = B(group?.SkippedClients?.Contains(title) == true ? _theme.Danger : _theme.Text),
            TextWrapping = TextWrapping.NoWrap
        });
        list.SelectionChanged += (_, _) => { _legacySelectedClient = list.SelectedItem as string; UpdateSkip(); };
        Put(_legacyCanvas!, list, 0, 190, 332, 219);
    }

    private void LegacyFpsAudio()
    {
        var intro = LegacyText("Limit client FPS and choose sounds to mute.\nAudio settings apply to all EVE clients."); intro.TextWrapping = TextWrapping.Wrap;
        Put(_legacyCanvas!, intro, 4, 3, 324, 52);
        Put(_legacyCanvas!, LegacySettingCheck("FpsEnabled", "Enable DirectX FPS Limits"), 16, 71, 300, 19);
        var fps = Group("FPS Limits", 16, 95, 300, 112);
        Put(fps, LegacyText("Active Client"), 13, 24, 110, 15); Numeric("FpsFocused", 126, 22, 56, fps);
        Put(fps, LegacyText("Inactive Clients"), 13, 52, 110, 15); Numeric("FpsBackground", 126, 50, 56, fps);
        Put(fps, LegacyText("Predicted Client"), 13, 81, 110, 15); Numeric("FpsPredictingFocus", 126, 79, 56, fps);
        Put(fps, LegacyButton("Go", async () => { foreach (var key in new[] { "FpsFocused", "FpsBackground", "FpsPredictingFocus" }) await LegacyApply(key); }, "apply-FpsFocused"), 249, 77, 35, 27);
        var audio = Group("Audio muting", 16, 215, 300, 180);
        Put(audio, LegacySettingCheck("AudioMuteJumpGateTunnel", "Mute Jump Gate Tunnel"), 16, 24, 268, 19);
        Put(audio, LegacySettingCheck("AudioMuteLocationBanner", "Mute Asteroid Belt Warp In"), 16, 51, 268, 19);
        Put(audio, LegacyText("Custom event IDs (comma-separated)"), 16, 76, 268, 16);
        var ids = LegacySettingText("AudioCustomMutedEventIds"); ids.TextWrapping = TextWrapping.Wrap;
        Put(audio, ids, 16, 96, 268, 42);
        var hint = LegacyText("Saves on Enter or leaving this field.\nClear the list to use only the presets.", 11); hint.Foreground = B("#666666");
        Put(audio, hint, 16, 143, 268, 32);
    }

    private void LegacyProfiles()
    {
        var hint = LegacyText("Profiles keep separate settings and layouts.\nKeep a backup before deleting a profile."); hint.TextWrapping = TextWrapping.Wrap;
        Put(_legacyCanvas!, hint, 6, 3, 306, 30);
        Put(_legacyCanvas!, LegacyButton("Clone Current Profile", async () => await Run(new("profile-clone")), "clone-profile"), 4, 39, 145, 33);
        var current = _snapshot.Profiles.FirstOrDefault(p => p.Name == _snapshot.ProfileName);
        var delete = LegacyButton("Delete Current Profile", () => Confirm("Delete profile?", "Permanently delete " + _snapshot.ProfileName + "? Default will become active.", "Delete", async () => await Run(new("profile-delete"))), "delete-profile");
        delete.IsEnabled = current?.IsDefault != true; Put(_legacyCanvas!, delete, 156, 39, 145, 33);
        Label("Current Profile", 10, 81, 94);
        var name = LegacyFormField("profile-name", _snapshot.ProfileName, async value => await Run(new("profile-rename", Value: value)));
        name.IsEnabled = current?.IsDefault != true; Put(_legacyCanvas!, name, 103, 77, 198, 23);
        var list = LegacyList(_snapshot.Profiles.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).Select(p => p.Name).ToArray(), "profiles-list");
        list.SelectedItem = _snapshot.ProfileName;
        list.SelectionChanged += async (_, _) =>
        {
            if (_legacyRendering || list.SelectedItem is not string selected || selected == _snapshot.ProfileName) return;
            var profile = _snapshot.Profiles.First(p => p.Name == selected);
            if (HasUnappliedEdits) Confirm("Switch profile?", "Your unapplied edits will be discarded.", "Discard and switch", async () => { _drafts.Clear(); _formDrafts.Clear(); await Run(new("profile-switch", profile.Id)); });
            else await Run(new("profile-switch", profile.Id));
        };
        Put(_legacyCanvas!, list, 2, 110, 330, 289);
        var accent = LegacyButton("", async () => await Run(new("color-picker", "ProfileAccentColor")), "legacy-profile-accent");
        accent.Background = ProfileAccent(); ToolTip.SetTip(accent, "Profile identity color"); Put(_legacyCanvas!, accent, 308, 80, 18, 18);
    }

    private void LegacyAbout()
    {
        Frame();
        var name = LegacyText("EVE-O Preview", 16); name.FontWeight = FontWeight.Bold; Put(_legacyCanvas!, name, 6, 11, 150, 24);
        var version = LegacyText(_snapshot.Version, 16); version.FontWeight = FontWeight.Bold; Put(_legacyCanvas!, version, 156, 11, 167, 24);
        Put(_legacyCanvas!, SupportPortrait(48), 16, 43, 48, 48);
        var invitation = LegacyText("Developed by Aura Asuna\nEvery feature is free.");
        Put(_legacyCanvas!, invitation, 74, 43, 234, 36);
        var donate = LegacyButton("Say thanks with an ISK gift", ShowSupport, "support-open");
        donate.Background = B(SupportSurface); donate.Foreground = B(SupportInk);
        Put(_legacyCanvas!, donate, 74, 83, 232, 26);
        var notice = LegacyText("Your kindness makes a difference. Thank you.\n\nABSOLUTELY NO WARRANTY. Licensed under GPLv3 (https://www.gnu.org/licenses/)."); notice.TextWrapping = TextWrapping.Wrap;
        Put(_legacyCanvas!, notice, 16, 116, 286, 70);
        Put(_legacyCanvas!, PartnerBadge(162), 16, 185, 184, 68);
        var description = LegacyText("No input broadcasting or modified EVE UI.\nPreviews, window switching, FPS limits\nand selective audio muting.");
        description.TextWrapping = TextWrapping.Wrap;
        Put(_legacyCanvas!, description, 16, 261, 294, 52);
        Label("Credit to previous maintainer: Phrynohyas Tig-Rah", 16, 318, 308, 11);
        Label("Get help, share ideas, or pop in and say hi:", 16, 340, 300);
        var link = LegacyButton("Documentation", async () => await Run(new("documentation")), "open-documentation"); link.Foreground = B("#0000EE");
        Put(_legacyCanvas!, link, 15, 363, 164, 23);
        Put(_legacyCanvas!, LegacyButton("Discord", async () => await Run(new("discord")), "open-discord"), 185, 363, 114, 23);
        Put(_legacyCanvas!, LegacyButton("More…", () => Navigate("Appearance"), "legacy-more"), 256, 390, 66, 19);
    }

    private void LegacyAppearance()
    {
        Label("Application theme", 12, 12, 280, 14);
        for (var i = 0; i < 3; i++)
        {
            string name = new[] { "Light", "Dark", "Legacy" }[i];
            var button = LegacyButton(name, async () => await Run(new("theme", Value: name)), "theme-" + name);
            button.IsEnabled = name != _snapshot.Theme; Put(_legacyCanvas!, button, 12 + i * 99, 39, 90, 27);
        }
        Label("The theme applies to all profiles.", 12, 79, 302);
        Put(_legacyCanvas!, LegacyButton("Profile identity color…", async () => await Run(new("color-picker", "ProfileAccentColor")), "legacy-accent-dialog"), 12, 110, 210, 27);
        Put(_legacyCanvas!, LegacyButton("Use theme's profile color", async () => await Run(new("setting", "ProfileAccentColor", "")), "accent-default"), 12, 146, 210, 27);
        Put(_legacyCanvas!, LegacyButton("Search settings…", () => Navigate("LegacySearch"), "legacy-search-button"), 12, 191, 210, 27);
        Put(_legacyCanvas!, LegacyButton("Title font settings…", () => ShowLegacySettingDialog("Title Font", new[] { "TitleFontName", "TitleFontSize", "TitleFontStyle" }), "legacy-font-settings"), 12, 227, 210, 27);
        Put(_legacyCanvas!, LegacyButton("Thumbnail right-click menu…", () => Navigate("ThumbnailMenu"), "thumbnail-menu-settings"), 12, 263, 245, 27);
    }

    private void LegacyPlanned()
    {
        Label(_pageId == "Dps" ? "DPS overviews (planned)" : "Character ESI connections (planned)", 12, 12, 304, 15);
        var copy = LegacyText(_pageId == "Dps" ? "A future home for configurable damage overviews.\n\nCombat logs are not read and DPS is not calculated yet." : "A future home for signing characters into EVE ESI.\n\nAuthentication and token storage are not implemented yet."); copy.TextWrapping = TextWrapping.Wrap;
        Put(_legacyCanvas!, copy, 12, 52, 304, 190);
        Put(_legacyCanvas!, LegacyButton("Back", () => Navigate("Appearance")), 12, 270, 70, 27);
    }

    private void LegacySearch()
    {
        Label("Search settings", 10, 10, 300);
        _search = LegacyTextBox("search-settings", _query);
        Put(_legacyCanvas!, _search, 10, 32, 308, 23);
        var results = new StackPanel { Spacing = 2 };
        void Populate()
        {
            results.Children.Clear();
            foreach (var item in SettingCatalog.All.Where(d => d.Matches(_search.Text?.Trim() ?? "")))
                results.Children.Add(LegacyButton(item.Label, () => ShowLegacySettingDialog(item.Label, new[] { item.Key })));
        }
        _search.TextChanged += (_, _) => Populate();
        Populate();
        Put(_legacyCanvas!, new ScrollViewer { Content = results }, 10, 66, 308, 329);
    }

    private async Task LegacyApply(string key)
    {
        if (_legacyRendering || _busy || !_drafts.TryGetValue(key, out var value)) return;
        var definition = SettingCatalog.Find(key);
        if (definition is null) return;
        definition = EffectiveDefinition(definition);
        if (SettingCatalog.Validate(definition, value) is { } error) { _message = error; _messageError = true; UpdateLegacyFooter(); return; }
        var result = await Run(new("setting", key, value));
        if (result.Success && _drafts.GetValueOrDefault(key) == value) { _drafts.Remove(key); UpdateLegacyFooter(); }
    }

    private TextBox LegacySettingText(string key)
    {
        var initial = Value(key);
        var box = LegacyTextBox("setting-" + key, _drafts.GetValueOrDefault(key, initial));
        var definition = SettingCatalog.Find(key);
        if (definition is not null) { AutomationProperties.SetName(box, definition.Label); ToolTip.SetTip(box, definition.Description + " Save with Enter or by leaving this field."); }
        box.TextChanged += (_, _) =>
        {
            if (_legacyRendering) return;
            if (box.Text == initial && !_failedSettings.Contains(key)) _drafts.Remove(key); else _drafts[key] = box.Text ?? "";
            UpdateLegacyFooter();
            if (definition?.Page == "Overlay") SchedulePreview();
        };
        box.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await LegacyApply(key); } else if (e.Key == Key.Escape) { _drafts.Remove(key); RenderPage(true); e.Handled = true; } };
        box.LostFocus += (_, _) => { if (!_legacyRendering) Dispatcher.UIThread.Post(async () => await LegacyApply(key), DispatcherPriority.Background); };
        return box;
    }

    private TextBox LegacyFormField(string key, string initial, Func<string, Task> save)
    {
        var box = LegacyTextBox(key, _formDrafts.GetValueOrDefault(key, initial));
        box.TextChanged += (_, _) => { if (!_legacyRendering) { if (box.Text == initial) _formDrafts.Remove(key); else _formDrafts[key] = box.Text ?? ""; UpdateLegacyFooter(); } };
        async Task Save()
        {
            if (_legacyRendering || _busy || !_formDrafts.TryGetValue(key, out var draft)) return;
            await save(draft);
            if (!_messageError && _formDrafts.GetValueOrDefault(key) == draft) _formDrafts.Remove(key);
            UpdateLegacyFooter();
        }
        box.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await Save(); } };
        box.LostFocus += (_, _) => { if (!_legacyRendering) Dispatcher.UIThread.Post(async () => await Save(), DispatcherPriority.Background); };
        return box;
    }

    private Control LegacyHotkey(string target, string value)
    {
        var button = LegacyButton(value == "None" ? "" : value, async () => await Run(new("hotkey-capture", target)), "record-" + target);
        button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Padding = new Thickness(3, 0);
        button.Background = B("#F0F0F0"); button.BorderBrush = B("#A0A0A0");
        ToolTip.SetTip(button, "Click to record a shortcut. Escape cancels. Right-click or press Delete to clear.");
        var clear = new MenuItem { Header = "Clear shortcut" };
        clear.Click += async (_, _) => await Run(new("hotkey-clear", target));
        button.ContextMenu = new ContextMenu { ItemsSource = new[] { clear } };
        button.KeyDown += async (_, e) => { if (e.Key == Key.Delete) { e.Handled = true; await Run(new("hotkey-clear", target)); } };
        return button;
    }

    private void Numeric(string key, double x, double y, double width, Canvas? parent = null, bool spinner = true)
    {
        var host = new Grid { ColumnDefinitions = new ColumnDefinitions(spinner ? "*,14" : "*") };
        var box = LegacySettingText(key); box.BorderThickness = new Thickness(0); host.Children.Add(box);
        if (spinner)
        {
            var spin = new Grid { RowDefinitions = new RowDefinitions("*,*") };
            for (var i = 0; i < 2; i++)
            {
                var delta = i == 0 ? 1 : -1;
                var button = LegacyButton(i == 0 ? "▴" : "▾", async () =>
                {
                    var rule = EffectiveDefinition(SettingCatalog.Find(key)!);
                    var number = double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : LegacyNumber(key, 0);
                    var next = Math.Clamp(number + delta, rule.Minimum ?? double.MinValue, rule.Maximum ?? double.MaxValue).ToString(CultureInfo.InvariantCulture);
                    box.Text = next; _drafts[key] = next; await LegacyApply(key);
                }, (i == 0 ? "increase-" : "decrease-") + key);
                button.FontSize = 9; button.Height = double.NaN; button.Padding = new Thickness(0); Grid.SetRow(button, i); spin.Children.Add(button);
            }
            Grid.SetColumn(spin, 1); host.Children.Add(spin);
        }
        Put(parent ?? _legacyCanvas!, new Border { BorderBrush = B("#7A7A7A"), BorderThickness = new Thickness(1), Background = Brushes.White, Child = host }, x, y, width, 23);
    }

    private CheckBox LegacySettingCheck(string key, string label)
    {
        var check = LegacyCheck(label, Value(key).Equals("true", StringComparison.OrdinalIgnoreCase), "setting-" + key);
        if (SettingCatalog.Find(key) is { } definition) ToolTip.SetTip(check, definition.Description);
        check.IsCheckedChanged += async (_, _) => { if (!_legacyRendering) await Run(new("setting", key, (check.IsChecked == true).ToString())); };
        return check;
    }

    private static CheckBox LegacyCheck(string text, bool isChecked, string name, bool radio = false)
    {
        var check = new CheckBox { Name = name, Content = text, IsChecked = isChecked, FontFamily = LegacyFont, FontSize = 12, Padding = new Thickness(0), MinHeight = 0, MinWidth = 0, VerticalContentAlignment = VerticalAlignment.Center };
        check.Template = new FuncControlTemplate<CheckBox>((owner, _) =>
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Opacity = owner.IsEnabled ? 1 : 0.45 };
            var mark = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M2 6L5 9L10 2"), Stroke = Brushes.White, StrokeThickness = 1.5, Width = 12, Height = 12, IsVisible = owner.IsChecked == true };
            var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = B("#0067C0"), IsVisible = owner.IsChecked == true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var square = new Border { Width = 13, Height = 13, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radio ? 7 : 2), Child = radio ? dot : mark };
            void Paint() { square.Background = owner.IsChecked == true && !radio ? B("#0067C0") : Brushes.White; square.BorderBrush = B(owner.IsChecked == true ? "#0067C0" : "#767676"); mark.IsVisible = dot.IsVisible = owner.IsChecked == true; }
            owner.PropertyChanged += (_, e) => { if (e.Property == ToggleButton.IsCheckedProperty) Paint(); if (e.Property == IsEnabledProperty) line.Opacity = owner.IsEnabled ? 1 : 0.45; };
            Paint(); line.Children.Add(square);
            if (text.Length > 0) line.Children.Add(new TextBlock { Text = text, FontFamily = LegacyFont, FontSize = 12, Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Center });
            return line;
        });
        AutomationProperties.SetName(check, text.Length == 0 ? name : text);
        return check;
    }

    private static Button LegacyButton(string text, Action click, string? name = null)
    {
        var button = new Button { Name = name, Content = text, FontFamily = LegacyFont, FontSize = 12, Padding = new Thickness(3, 0), MinHeight = 0, MinWidth = 0, Height = 25, Background = B("#FAFAFA"), BorderBrush = B("#B8B8B8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Foreground = Brushes.Black };
        button.Template = new FuncControlTemplate<Button>((owner, _) =>
        {
            var presenter = new ContentPresenter { Content = owner.Content, HorizontalContentAlignment = owner.HorizontalContentAlignment, VerticalContentAlignment = VerticalAlignment.Center, Margin = owner.Padding, Opacity = owner.IsEnabled ? 1 : 0.45 };
            var border = new Border { Background = owner.Background, BorderBrush = owner.BorderBrush, BorderThickness = owner.BorderThickness, CornerRadius = owner.CornerRadius, Child = presenter };
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == ContentControl.ContentProperty) presenter.Content = owner.Content;
                border.Background = owner.Background; border.BorderBrush = owner.BorderBrush; border.CornerRadius = owner.CornerRadius;
                presenter.HorizontalContentAlignment = owner.HorizontalContentAlignment; presenter.Margin = owner.Padding;
                presenter.Opacity = owner.IsEnabled ? 1 : 0.45;
            };
            return border;
        });
        button.Click += (_, _) => click();
        return button;
    }

    private static TextBox LegacyTextBox(string name, string value) => new() { Name = name, Text = value, FontFamily = LegacyFont, FontSize = 12, MinHeight = 0, MinWidth = 0, Padding = new Thickness(3, 0), Background = Brushes.White, Foreground = Brushes.Black, BorderBrush = B("#A0A0A0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(0), VerticalContentAlignment = VerticalAlignment.Center };
    private static ComboBox LegacyCombo(System.Collections.IEnumerable source, string name) => new() { Name = name, ItemsSource = source, FontFamily = LegacyFont, FontSize = 12, MinHeight = 0, MinWidth = 0, Padding = new Thickness(3, 0), Background = Brushes.White, Foreground = Brushes.Black, BorderBrush = B("#A0A0A0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(0), VerticalContentAlignment = VerticalAlignment.Center };
    private static TextBlock LegacyText(string text, double size = 12) => new() { Text = text, FontFamily = LegacyFont, FontSize = size, LineHeight = size * 1.25, Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Top };
    private string Value(string key) => _snapshot.Settings.GetValueOrDefault(key, "");
    private double LegacyNumber(string key, double fallback) => double.TryParse(Value(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private void Label(string text, double x, double y, double width = 200, double fontSize = 12) => Put(_legacyCanvas!, LegacyText(text, fontSize), x, y, width, 18);
    private void Frame() => Put(_legacyCanvas!, new Border { BorderBrush = B("#646464"), BorderThickness = new Thickness(1) }, 0, 0, 332, 409);
    private static void Put(Canvas parent, Control control, double x, double y, double width, double height) { control.Width = width; control.Height = height; Canvas.SetLeft(control, x); Canvas.SetTop(control, y); parent.Children.Add(control); }

    private Canvas Group(string title, double x, double y, double width, double height)
    {
        var group = new Canvas();
        Put(group, new Border { BorderBrush = B("#D5D5D5"), BorderThickness = new Thickness(1) }, 0, 8, width, height - 8);
        var label = LegacyText(title); label.Background = B(_pageId == "FpsAudio" ? "#FAFAFA" : "#F0F0F0");
        Put(group, label, 7, 0, title.Length * 6.2 + 7, 18);
        Put(_legacyCanvas!, group, x, y, width, height); return group;
    }

    private static ListBox LegacyList(IEnumerable<string> items, string name)
    {
        var list = new ListBox { Name = name, ItemsSource = items, FontFamily = LegacyFont, FontSize = 12, Padding = new Thickness(1), Background = Brushes.White, Foreground = Brushes.Black, BorderBrush = B("#A0A0A0"), BorderThickness = new Thickness(1) };
        list.Styles.Add(new Style(s => s.OfType<ListBoxItem>()) { Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(1, 0)), new Setter(Layoutable.MinHeightProperty, 0d), new Setter(Layoutable.HeightProperty, 16d) } });
        list.Styles.Add(new Style(s => s.OfType<ListBoxItem>().Class(":selected")) { Setters = { new Setter(TemplatedControl.BackgroundProperty, B("#0078D7")), new Setter(TemplatedControl.ForegroundProperty, Brushes.White) } });
        return list;
    }

    private void ShowLegacyClientPicker(CycleGroupItem group)
    {
        var body = new Canvas { Width = 223, Height = 297, Background = B("#F0F0F0") };
        var names = LegacyList(_snapshot.Clients.Select(c => c.Title), "legacy-client-picker-list"); Put(body, names, 3, 3, 217, 225);
        string draftKey = "legacy-offline-client-" + group.Id;
        var input = LegacyTextBox("legacy-client-picker-name", _formDrafts.GetValueOrDefault(draftKey, "")); Put(body, input, 9, 236, 198, 23);
        input.TextChanged += (_, _) => { if (string.IsNullOrEmpty(input.Text)) _formDrafts.Remove(draftKey); else _formDrafts[draftKey] = input.Text; UpdateLegacyFooter(); };
        names.SelectionChanged += (_, _) => { if (names.SelectedItem is string title) input.Text = title; };
        async Task Select()
        {
            var result = await Run(new("group-client-add", group.Id.ToString(), input.Text ?? ""));
            if (result.Success) { _formDrafts.Remove(draftKey); DismissConfirmation(); RenderPage(true); }
        }
        Put(body, LegacyButton("Select", async () => await Select(), "legacy-client-picker-select"), 9, 267, 198, 23);
        names.DoubleTapped += async (_, _) => await Select();
        ShowLegacyDialog("Client Name", body); input.Focus();
    }

    private void ShowLegacySettingDialog(string title, IReadOnlyList<string> keys)
    {
        var body = new Canvas { Width = 306, Height = 55 + keys.Count * 52, Background = B("#F0F0F0") };
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i]; var definition = SettingCatalog.Find(key); if (definition is null) continue;
            Put(body, LegacyText(definition.Label), 9, 8 + i * 52, 286, 17);
            Control input;
            if (definition.Kind == SettingKind.Choice)
            {
                var combo = LegacyCombo(definition.Options!, "setting-" + key); combo.SelectedItem = Value(key);
                combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string selected) _drafts[key] = selected; }; input = combo;
            }
            else if (definition.Kind == SettingKind.Toggle) input = LegacySettingCheck(key, definition.Label);
            else input = LegacySettingText(key);
            Put(body, input, 9, 27 + i * 52, 204, 23);
            Put(body, LegacyButton("Apply", async () => await LegacyApply(key), "apply-" + key), 222, 27 + i * 52, 75, 23);
        }
        Put(body, LegacyButton("Close", DismissConfirmation), 222, body.Height - 31, 75, 23);
        ShowLegacyDialog(title, body);
    }

    private void ShowLegacyDialog(string title, Control body)
    {
        DismissConfirmation();
        _confirmationPreviousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var panel = new StackPanel { Background = B("#F0F0F0") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,25"), Background = B("#D7E4F2"), Height = 25 };
        var label = LegacyText(title); label.Margin = new Thickness(7, 5, 0, 0); header.Children.Add(label);
        var close = LegacyButton("×", DismissConfirmation); Grid.SetColumn(close, 1); header.Children.Add(close);
        panel.Children.Add(header); panel.Children.Add(body);
        _confirmation = new Border { Background = B("#33000000"), Child = new Border { BorderBrush = B("#646464"), BorderThickness = new Thickness(1), Child = panel, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        Grid.SetColumnSpan(_confirmation, 2); _root.Children.Add(_confirmation);
        foreach (var child in _root.Children.Where(c => c != _confirmation)) child.IsEnabled = false;
        close.Focus();
    }
}
