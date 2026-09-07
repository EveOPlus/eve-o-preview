using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace EveOPreview.UI;

/// <summary>The portable settings workspace. Every platform operation crosses IWorkspaceBackend.</summary>
public sealed partial class WorkspaceView : UserControl, IDisposable
{
    private readonly IWorkspaceBackend _backend;
    private readonly IReadOnlyList<WorkspaceModule> _modules;
    private WorkspaceSnapshot _snapshot;
    private WorkspaceTheme _theme;
    private readonly Dictionary<string, string> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _formDrafts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedSettings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkspaceCommand> _retryCommands = new(StringComparer.Ordinal);
    private WorkspaceCommand? _retryCommand => _retryCommands.Values.FirstOrDefault();
    private Button _retryButton = null!;
    private bool _preserveDraftsOnRefresh;
    private readonly Dictionary<string, Button> _navigation = new(StringComparer.Ordinal);
    private Grid _root = null!;
    private StackPanel _page = null!;
    private ScrollViewer _scroll = null!;
    private TextBox _search = null!;
    private TextBlock _status = null!;
    private TextBlock _draftStatus = null!;
    private TextBlock _clientStatus = null!;
    private TextBlock _profileName = null!;
    private TextBlock _versionLabel = null!;
    private Border _profileCard = null!;
    private ComboBox _profilePicker = null!;
    private Button _hideAll = null!;
    private Border? _confirmation;
    private Control? _confirmationPreviousFocus;
    private string _pageId = "Overview";
    private string _query = "";
    private string _message = "Ready";
    private bool _messageError;
    private bool _disposed;
    private bool _busy;
    private bool _refreshQueued;
    private bool _settingProfile;
    private bool _deferPageRefresh;
    private bool _previewReflowQueued;
    private Control? _moduleContent;
    private int? _selectedGroup;

    public WorkspaceView(IWorkspaceBackend backend, IEnumerable<WorkspaceModule>? modules = null)
    {
        _backend = backend;
        _modules = modules?.ToArray() ?? Array.Empty<WorkspaceModule>();
        _snapshot = backend.Read();
        UpdatePreviewCharacter();
        _theme = WorkspaceTheme.Get(_snapshot.Theme);
        _pageId = _theme.Legacy ? "General" : "Overview";
        FontFamily = new FontFamily("Inter, Segoe UI, sans-serif");
        FontSize = 13;
        _backend.Changed += OnBackendChanged;
        SizeChanged += (_, _) =>
        {
            if (!_theme.Legacy && _pageId == "ThumbnailMenu" && !_busy && _cancelMenuDrag is null && Bounds.Width > 0 && (Bounds.Width < 1000) != _menuNarrow)
                Dispatcher.UIThread.Post(() =>
                {
                    if (!_disposed && !_theme.Legacy && _pageId == "ThumbnailMenu" && !_busy && _cancelMenuDrag is null && (Bounds.Width < 1000) != _menuNarrow)
                        RenderPage(true);
                });
            if (!_theme.Legacy && _pageId == "Switching" && !_busy && _cancelOrderDrag is null && _expandedOrderGroup is null && Bounds.Width > 0 && (Bounds.Width < 1120) != _orderNarrow)
                Dispatcher.UIThread.Post(() =>
                {
                    if (!_disposed && !_theme.Legacy && _pageId == "Switching" && !_busy && _cancelOrderDrag is null && _expandedOrderGroup is null && (Bounds.Width < 1120) != _orderNarrow)
                        RenderPage(true);
                });
            if (_theme.Legacy || _pageId != "Previews" || _query.Length > 0 || _busy || Bounds.Width <= 0 || (Bounds.Width < 1080) == _previewNarrow || _previewReflowQueued) return;
            _previewReflowQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _previewReflowQueued = false;
                // A DPI transition can briefly resize the host before the island's
                // scale catches up. Recheck the final width to preserve editor focus.
                if (!_disposed && !_theme.Legacy && _pageId == "Previews" && !_busy && _query.Length == 0 && (Bounds.Width < 1080) != _previewNarrow)
                    RenderPage(true);
            });
        };
        BuildShell();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cancelOrderDrag?.Invoke();
        _cancelMenuDrag?.Invoke();
        _disposed = true;
        _backend.Changed -= OnBackendChanged;
        KeyDown -= HandleShellKey;
        if (_moduleContent is IDisposable disposableModule) disposableModule.Dispose();
        _moduleContent = null;
        ReleaseTitlePreviews();
        _supportPortrait?.Dispose();
        _supportPortrait = null;
        _drafts.Clear();
        _formDrafts.Clear();
        _lastPreviewImage = null;
        _previewStill = null;
        _previewStillBitmap?.Dispose();
        _previewStillBitmap = null;
    }

    public bool HasUnappliedEdits => _drafts.Count > 0 || _formDrafts.Count > 0;

    public void RequestDiscardDrafts(Action confirmed) => Confirm("Discard unapplied edits?", "Applied settings are already saved. Your unapplied field edits will be discarded when this window closes.", "Discard edits & close", () =>
    {
        _drafts.Clear(); _formDrafts.Clear(); confirmed(); return Task.CompletedTask;
    });

    public void Navigate(string pageId)
    {
        _cancelMenuDrag?.Invoke();
        // Reserved feature routes stay hidden until a real modern module is registered.
        if (WorkspaceModules.ReservedIds.Contains(pageId) && (_theme.Legacy || !_modules.Any(module => module.Id == pageId)))
            pageId = _theme.Legacy ? "General" : "Overview";
        if (!_theme.Legacy && pageId == "Overlay") { _previewTab = "Titles"; pageId = "Previews"; }
        if (!_theme.Legacy && pageId is "Thumbnail" or "Zoom") { _previewTab = "Layout"; pageId = "Previews"; }
        _pageId = pageId;
        _query = "";
        if (_search is not null) _search.Text = "";
        RenderPage(false);
    }

    public void RefreshFromBackend()
    {
        if (_disposed) return;
        var next = _backend.Read();
        if (_cancelMenuDrag is not null)
        {
            if (next.Theme == _snapshot.Theme && next.ThumbnailMenuTheme == _snapshot.ThumbnailMenuTheme
                && ThumbnailMenuActions.Normalize(next.ThumbnailMenuOrder).SequenceEqual(ThumbnailMenuActions.Normalize(_snapshot.ThumbnailMenuOrder))) return;
            _cancelMenuDrag();
        }
        if (_cancelOrderDrag is not null && next.ProfileName == _snapshot.ProfileName && next.Theme == _snapshot.Theme) return;
        if (next.ProfileName != _snapshot.ProfileName && !_preserveDraftsOnRefresh)
        {
            _cancelOrderDrag?.Invoke();
            DismissConfirmation();
            _orderScroll = null;
            _drafts.Clear();
            _formDrafts.Clear();
            _failedSettings.Clear();
            _retryCommands.Clear();
            _lastPreviewImage = null;
            _previewRenderError = null;
        }
        _snapshot = next;
        UpdatePreviewCharacter();
        if (_expandedOrderGroup is not null && _theme.Name == WorkspaceTheme.Get(next.Theme).Name)
        {
            RenderExpandedOrder();
            return;
        }
        if (_theme.Name != WorkspaceTheme.Get(next.Theme).Name)
        {
            DismissConfirmation();
            if (_pageId == "Overlay") _previewTab = "Titles";
            if (_pageId == "AdvancedPreview") _previewTab = "Advanced";
            _theme = WorkspaceTheme.Get(next.Theme);
            _pageId = _theme.Legacy
                ? _pageId switch { "Overview" => "General", "Previews" when _previewTab == "Advanced" => "AdvancedPreview", "Previews" => "Thumbnail", "Clients" => "ActiveClients", "Switching" => "CycleGroups", _ => _pageId }
                : _pageId switch { "General" or "Thumbnail" or "Zoom" or "Overlay" or "AdvancedPreview" => "Previews", "ActiveClients" => "Clients", "CycleGroups" => "Switching", _ => _pageId };
            BuildShell();
        }
        else
        {
            UpdateHeader();
            if (!_deferPageRefresh) RenderPage(true);
        }
    }

    private void OnBackendChanged()
    {
        if (_disposed || _refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            if (!_disposed && !_busy) RefreshFromBackend();
        });
    }

    private void BuildShell()
    {
        if (_theme.Legacy) { BuildLegacyShell(); return; }
        FontFamily = new FontFamily("Inter, Segoe UI, sans-serif");
        FontSize = 13;
        if (Application.Current is { } app)
            app.RequestedThemeVariant = _theme.Name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Foreground = B(_theme.Text);
        Background = B(_theme.Background);
        _navigation.Clear();
        _root = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*") };
        Content = _root;
        _root.Children.Add(BuildSidebar());
        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetColumn(main, 1);
        _root.Children.Add(main);
        main.Children.Add(BuildHeader());
        _page = new StackPanel { Spacing = 12, Margin = new Thickness(22, 18, 22, 22), MaxWidth = 1120, HorizontalAlignment = HorizontalAlignment.Stretch };
        _scroll = new ScrollViewer { Content = _page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(_scroll, 1);
        main.Children.Add(_scroll);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(26, 12) };
        _status = Text(_message, 12, _messageError ? _theme.Danger : _theme.Muted);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        footer.Children.Add(_status);
        _draftStatus = Text("", 12, _theme.Accent);
        Grid.SetColumn(_draftStatus, 2);
        footer.Children.Add(_draftStatus);
        _retryButton = ActionButton("Retry update", async () => { if (_retryCommand is { } command) await Run(command); }, "retry-update", true);
        _retryButton.Margin = new Thickness(10, 0, 12, 0);
        Grid.SetColumn(_retryButton, 1); footer.Children.Add(_retryButton);
        var footBorder = new Border { BorderBrush = B(_theme.Border), BorderThickness = new Thickness(0, 1, 0, 0), Child = footer };
        Grid.SetRow(footBorder, 2);
        main.Children.Add(footBorder);
        UpdateHeader();
        RenderPage(false);
    }

    private Control BuildSidebar()
    {
        var dock = new DockPanel { Margin = new Thickness(12, 20, 12, 12), LastChildFill = true };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(10, 0, 0, 18) };
        brand.Children.Add(BrandLogo());
        var brandText = new StackPanel { Spacing = 2 };
        brandText.Children.Add(Text("EVE-O", 16, _theme.Text, true));
        brandText.Children.Add(Text("P R E V I E W", 8, _theme.Muted, true));
        brand.Children.Add(brandText);
        DockPanel.SetDock(brand, Dock.Top);
        dock.Children.Add(brand);
        var bottom = new StackPanel { Spacing = 9, Margin = new Thickness(8, 18, 8, 0) };
        bottom.Children.Add(SupportButton());
        var utilities = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var about = ActionButton("Help & about", () => Navigate("About"), "about-link");
        about.Padding = new Thickness(8); about.HorizontalAlignment = HorizontalAlignment.Stretch;
        utilities.Children.Add(about);
        var exit = CommandButton("Exit", new("exit"), "exit-application");
        exit.Padding = new Thickness(8); exit.Margin = new Thickness(6, 0, 0, 0);
        ToolTip.SetTip(exit, "Quit EVE-O Preview and stop all previews");
        Grid.SetColumn(exit, 1); utilities.Children.Add(exit);
        bottom.Children.Add(utilities);
        _versionLabel = Text("", 9, _theme.Muted);
        _versionLabel.Name = "application-version";
        bottom.Children.Add(_versionLabel);
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        var nav = new StackPanel { Spacing = 4 };
        if (_theme.Legacy)
        {
            AddNav(nav, "General", "General", "sliders");
            AddNav(nav, "Thumbnail", "Thumbnail", "preview");
            AddNav(nav, "Zoom", "Zoom", "search");
            AddNav(nav, "Overlay", "Overlay", "overlay");
            AddNav(nav, "ActiveClients", "Active Clients", "clients");
            AddNav(nav, "CycleGroups", "Cycle Groups", "cycle");
            AddNav(nav, "FpsAudio", "FPS / Audio", "performance");
            AddNav(nav, "Profiles", "Profiles", "profiles");
            AddNav(nav, "About", "About", "help");
            nav.Children.Add(Text("APPEARANCE", 9, _theme.Muted, true, margin: new Thickness(12, 22, 0, 8)));
            AddNav(nav, "Appearance", "Themes", "theme");
        }
        else
        {
            nav.Children.Add(Text("WORKSPACE", 9, _theme.Muted, true, margin: new Thickness(12, 0, 0, 10)));
            AddNav(nav, "Overview", "Overview", "overview");
            AddNav(nav, "Clients", "Clients", "clients");
            AddNav(nav, "Previews", "Previews & layout", "preview");
            AddNav(nav, "Switching", "Switching & hotkeys", "cycle");
            AddNav(nav, "FpsAudio", "Performance & audio", "performance");
            nav.Children.Add(Text("PERSONALIZE", 9, _theme.Muted, true, margin: new Thickness(12, 23, 0, 8)));
            AddNav(nav, "Appearance", "Appearance", "theme");
        }
        foreach (var module in _modules.Where(m => !_navigation.ContainsKey(m.Id))) AddNav(nav, module.Id, module.Title, "overview");
        dock.Children.Add(new ScrollViewer { Content = nav, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        return new Border { Background = B(_theme.Sidebar), BorderBrush = B(_theme.Border), BorderThickness = new Thickness(0, 0, 1, 0), Child = dock };
    }

    private void AddNav(Panel parent, string id, string title, string icon, bool planned = false)
    {
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*,Auto"), VerticalAlignment = VerticalAlignment.Center };
        line.Children.Add(Icon(icon));
        var label = Text(title, 12, _theme.Text);
        Grid.SetColumn(label, 1); line.Children.Add(label);
        if (planned)
        {
            var dot = Text("·", 15, _theme.Muted);
            Grid.SetColumn(dot, 2); line.Children.Add(dot);
        }
        var button = ActionButton(line, () => Navigate(id), "nav-" + id);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Padding = new Thickness(10, 9);
        button.BorderThickness = new Thickness(0);
        AutomationProperties.SetName(button, title + (planned ? ", planned feature" : ""));
        _navigation[id] = button;
        parent.Children.Add(button);
    }

    private Control BuildHeader()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(22, 12, 22, 10) };
        var profile = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center };
        profile.Children.Add(Text("Profile", 12, _theme.Muted));
        _profilePicker = new ComboBox { Name = "profile-picker", MinWidth = 160, MinHeight = 33, MaxDropDownHeight = 360, FontSize = 12,
            Foreground = B(_theme.Text), HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(9, 0, 0, 0),
            SelectionBoxItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((name, _) => new TextBlock
            {
                Text = name, Foreground = B(_theme.Text), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center
            }) };
        AutomationProperties.SetName(_profilePicker, "Active profile");
        _profilePicker.SelectionChanged += async (_, _) =>
        {
            if (_settingProfile || _profilePicker.SelectedItem is not ComboBoxItem item) return;
            if (item.Name == "manage-profiles")
            {
                _profilePicker.IsDropDownOpen = false;
                UpdateHeader(); // Keep the actual profile selected when choosing this navigation action.
                Navigate("Profiles");
                return;
            }
            if (item.Tag is not ProfileItem selected || selected.Name == _snapshot.ProfileName) return;
            if (HasUnappliedEdits)
            {
                UpdateHeader();
                Confirm("Switch profile?", "Your unapplied edits will be discarded. Applied settings are already saved in " + _snapshot.ProfileName + ".", "Discard edits & switch", async () => { _drafts.Clear(); _formDrafts.Clear(); await Run(new("profile-switch", selected.Id)); });
            }
            else await Run(new("profile-switch", selected.Id));
        };
        Grid.SetColumn(_profilePicker, 1); profile.Children.Add(_profilePicker);
        _profileCard = new Border { Name = "active-profile-card", Background = B(_theme.Inset),
            CornerRadius = new CornerRadius(_theme.Radius), BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(9, 5, 7, 5), Margin = new Thickness(0, 0, 14, 0), MaxWidth = 440,
            HorizontalAlignment = HorizontalAlignment.Left, Child = profile };
        header.Children.Add(_profileCard);
        _search = new TextBox { Name = "search-settings", Watermark = "Search settings…  Ctrl+K", Width = 240, HorizontalAlignment = HorizontalAlignment.Right, MinHeight = 34, Text = _query, FontSize = 12 };
        AutomationProperties.SetName(_search, "Search all settings and features");
        _search.TextChanged += (_, _) => { _query = _search.Text?.Trim() ?? ""; RenderPage(false); };
        Grid.SetColumn(_search, 1); header.Children.Add(_search);
        _clientStatus = Text("", 11, _theme.Muted, margin: new Thickness(0, 8, 0, 0));
        Grid.SetRow(_clientStatus, 1); header.Children.Add(_clientStatus);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 7, 0, 0) };
        _hideAll = CommandButton("Hide all previews", new("toggle-all"), "toggle-all-previews");
        actions.Children.Add(_hideAll);
        actions.Children.Add(CommandButton("Minimize all clients", new("minimize-all"), "minimize-all-clients"));
        Grid.SetRow(actions, 1); Grid.SetColumn(actions, 1); header.Children.Add(actions);
        KeyDown -= HandleShellKey;
        KeyDown += HandleShellKey;
        return new Border { Background = B(_theme.Sidebar), BorderBrush = B(_theme.Border), BorderThickness = new Thickness(0, 0, 0, 1), Child = header };
    }

    private void HandleShellKey(object? sender, KeyEventArgs e)
    {
        if (_busy) return; // A recording command must be able to capture even the shell's own shortcuts.
        if (e.Key == Key.Escape && _cancelMenuDrag is not null) { _cancelMenuDrag(); RefreshFromBackend(); e.Handled = true; return; }
        if (e.Key == Key.Escape && _cancelOrderDrag is not null) { _cancelOrderDrag(); RefreshFromBackend(); e.Handled = true; return; }
        if (_expandedOrderGroup is not null)
        {
            if (e.Key == Key.Escape) { DismissConfirmation(); e.Handled = true; }
            return;
        }
        if (_theme.Legacy && e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Navigate("LegacySearch"); _search.Focus(); _search.SelectAll(); e.Handled = true; return; }
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _search.Focus(); _search.SelectAll(); e.Handled = true; }
        if (e.Key == Key.Escape && _confirmation is not null) { DismissConfirmation(); e.Handled = true; }
    }

    private void UpdateHeader()
    {
        if (_theme.Legacy) { UpdateLegacyHeader(); return; }
        _versionLabel.Text = string.IsNullOrWhiteSpace(_snapshot.Version) ? "EVE-O Preview" : "EVE-O Preview  \u00b7  " + _snapshot.Version;
        _profileCard.BorderBrush = ProfileAccent();
        ToolTip.SetTip(_profilePicker, "Active profile: " + _snapshot.ProfileName);
        _settingProfile = true;
        var profileItems = _snapshot.Profiles.Select(p =>
        {
            var item = new ComboBoxItem { Content = p.Name, Tag = p, Foreground = B(_theme.Text) };
            ToolTip.SetTip(item, p.Name);
            return item;
        }).ToList();
        profileItems.Add(new ComboBoxItem { Name = "profile-menu-divider", Content = new Separator(),
            IsEnabled = false, Focusable = false, MinHeight = 9, Padding = new Thickness(0) });
        var manageProfiles = new ComboBoxItem { Name = "manage-profiles", Content = "Manage profiles…", Foreground = B(_theme.Text) };
        AutomationProperties.SetName(manageProfiles, "Manage profiles");
        profileItems.Add(manageProfiles);
        _profilePicker.ItemsSource = profileItems;
        _profilePicker.SelectedIndex = _snapshot.Profiles.ToList().FindIndex(p => p.Name == _snapshot.ProfileName);
        _settingProfile = false;
        var count = _snapshot.Clients.Count;
        _clientStatus.Text = count == 0 ? "No EVE clients detected  ·  Launch EVE to begin" : $"{count} {(count == 1 ? "client" : "clients")} detected  ·  {_snapshot.Clients.Count(c => c.PreviewVisible)} previews enabled";
        _hideAll.Content = _snapshot.AllPreviewsHidden ? "Show all previews" : "Hide all previews";
        _hideAll.Background = B(_snapshot.AllPreviewsHidden ? _theme.AccentSurface : _theme.Surface);
        UpdateFooter();
    }

    private void UpdateFooter()
    {
        if (_theme.Legacy) { UpdateLegacyFooter(); return; }
        _status.Text = _message;
        _status.Foreground = B(_messageError ? _theme.Danger : _theme.Muted);
        var edits = _drafts.Count + _formDrafts.Count;
        _draftStatus.Text = edits == 0 ? (_retryCommand is null ? "Applied settings save automatically" : "Update incomplete") : $"{edits} unapplied {(edits == 1 ? "edit" : "edits")}";
        _retryButton.IsVisible = _retryCommand is not null;
        _retryButton.Content = _retryCommands.Count > 1 ? $"Retry update ({_retryCommands.Count})" : "Retry update";
    }

    private async Task<CommandResult> Run(WorkspaceCommand command)
    {
        if (_busy) return CommandResult.Error("Wait for the current action to finish.");
        _busy = true;
        _messageError = false;
        _message = command.Action == "hotkey-capture" ? "Listening for a shortcut… Press the keys now. Escape cancels; capture expires after 10 seconds." : "Applying…";
        UpdateFooter();
        CommandResult result;
        try { result = await _backend.ExecuteAsync(command); }
        catch (Exception ex) { result = CommandResult.Error(ex.Message); }
        finally { _busy = false; }
        if (_disposed) return result;
        _message = result.Message;
        _messageError = !result.Success;
        if (command.Action == "setting")
        {
            if (!result.Success) { _retryCommands[command.Target] = command; _failedSettings.Add(command.Target); }
            else
            {
                _failedSettings.Remove(command.Target);
                if (_drafts.GetValueOrDefault(command.Target) == command.Value) _drafts.Remove(command.Target);
                _retryCommands.Remove(command.Target);
            }
        }
        if (result.Success && !result.WasCancelled && command.Action is "font-picker" or "color-picker")
        {
            var appliedKeys = command.Action == "font-picker" ? new[] { "TitleFontName", "TitleFontSize", "TitleFontStyle" } : new[] { command.Target };
            foreach (var key in appliedKeys) { _drafts.Remove(key); _failedSettings.Remove(key); _retryCommands.Remove(key); }
        }
        _preserveDraftsOnRefresh = command.Action == "profile-rename";
        RefreshFromBackend();
        _preserveDraftsOnRefresh = false;
        return result;
    }

    private void RenderPage(bool preservePosition)
    {
        if (_theme.Legacy) { RenderLegacyPage(preservePosition); return; }
        if (_page is null) return;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var focusOwner = focused?.Name?.StartsWith("PART_", StringComparison.Ordinal) == true
            ? focused.GetVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Name?.StartsWith("setting-", StringComparison.Ordinal) == true)
            : focused;
        var focusName = focusOwner?.Name ?? focused?.Name;
        var caret = (focused as TextBox)?.CaretIndex;
        var offset = _scroll.Offset;
        if (_moduleContent is IDisposable disposableModule) disposableModule.Dispose();
        _moduleContent = null;
        ReleaseTitlePreviews();
        _scroll.Content = _page;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _page.Children.Clear();
        _page.Margin = new Thickness(22, 18, 22, 22);
        _page.Spacing = 12;
        foreach (var pair in _navigation)
        {
            var active = pair.Key == (_pageId == "ClientSettings" ? "Clients" : _pageId) && _query.Length == 0;
            pair.Value.Background = B(active ? _theme.AccentSurface : _theme.Sidebar);
            pair.Value.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
        }
        if (_query.Length > 0) RenderSearch();
        else if (_modules.FirstOrDefault(m => m.Id == _pageId) is { } registeredModule)
        {
            Heading(registeredModule.Title, registeredModule.Description);
            _moduleContent = registeredModule.CreateView(_backend);
            _page.Children.Add(_moduleContent);
        }
        else switch (_pageId)
        {
            case "Overview": RenderOverview(); break;
            case "Clients": case "ActiveClients": RenderClients(); break;
            case "ClientSettings": RenderClientSettings(); break;
            case "Previews": RenderPreviewSettings(); break;
            case "Switching": case "CycleGroups": RenderSwitching(); break;
            case "General": Heading("General", "Window behavior, saved layouts and processor settings."); AddSettings("General"); AddSettings("Thumbnail", s => s.Key == "ShowThumbnailsAlwaysOnTop"); AddSettings("FpsAudio", s => s.Key == "EnableAutomaticCpuAffinity"); break;
            case "Thumbnail": Heading("Thumbnail", "Size, transparency and window position."); AddSettings("Thumbnail"); break;
            case "Zoom": Heading("Zoom", "A closer look, exactly where you want it."); AddSettings("Zoom"); break;
            case "Overlay": Heading("Overlay", "Character names and active-client highlighting."); AddFontPreview(); AddSettings("Overlay"); break;
            case "FpsAudio": RenderPerformance(); break;
            case "Profiles": RenderProfiles(); break;
            case "Appearance": RenderAppearance(); break;
            case "ThumbnailMenu": RenderThumbnailMenu(); break;
            case "About": RenderAbout(); break;
            default:
                var module = _modules.FirstOrDefault(m => m.Id == _pageId);
                if (module is not null) { Heading(module.Title, module.Description); _page.Children.Add(module.CreateView(_backend)); }
                else { Heading("Page unavailable", "Choose a page from the workspace navigation."); }
                break;
        }
        _scroll.Offset = preservePosition ? offset : default;
        UpdateFooter();
        if (preservePosition && focusName is not null && focused != _search)
        {
            var replacement = _scroll.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == focusName);
            // Unattached controls are not visual descendants until layout. Resolve after attachment.
            Dispatcher.UIThread.Post(() =>
            {
                replacement ??= _scroll.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == focusName);
                if (replacement is null || _disposed) return;
                var input = replacement is NumericUpDown or AutoCompleteBox
                    ? replacement.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() ?? replacement : replacement;
                input.Focus();
                if (input is TextBox box && caret is { } index) box.CaretIndex = Math.Min(index, box.Text?.Length ?? 0);
            }, DispatcherPriority.Loaded);
        }
    }

    private void Heading(string title, string description, string? eyebrow = null)
    {
        var block = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 0, 4) };
        if (eyebrow is not null) block.Children.Add(Text(eyebrow.ToUpperInvariant(), 10, _theme.Accent, true));
        block.Children.Add(Text(title, 24, _theme.Text, true));
        block.Children.Add(Text(description, 13, _theme.Muted));
        _page.Children.Add(block);
    }

    private Border Card(Control child, Thickness? padding = null) => new() { Child = child, Background = B(_theme.Surface), BorderBrush = B(_theme.Border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(_theme.Radius), Padding = padding ?? new Thickness(15) };
    private static IBrush B(string color) => WorkspaceTheme.Brush(color);
    private IBrush ProfileAccent() => Color.TryParse(_snapshot.Settings.GetValueOrDefault("ProfileAccentColor", ""), out var color) ? new SolidColorBrush(color) : B(_theme.Accent);
    private TextBlock Text(string value, double size = 13, string? color = null, bool bold = false, HorizontalAlignment align = HorizontalAlignment.Stretch, Thickness? margin = null) => new() { Text = value, FontSize = size, Foreground = B(color ?? _theme.Text), FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = align, Margin = margin ?? default };
    private Button ActionButton(object content, Action action, string? name = null, bool primary = false)
    {
        var button = new Button { Name = name, Content = content, Padding = new Thickness(12, 8), FontSize = 12, CornerRadius = new CornerRadius(_theme.Legacy ? 2 : 7), Background = B(primary ? _theme.AccentSurface : _theme.Surface), Foreground = B(primary ? _theme.Accent : _theme.Text), BorderBrush = B(_theme.Border), BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => action();
        return button;
    }
    private Button CommandButton(string label, WorkspaceCommand command, string? name = null, bool primary = false) => ActionButton(label, async () => await Run(command), name, primary);
    private TextBox FormInput(string id, string watermark, string initial = "")
    {
        var box = new TextBox { Name = id, Text = _formDrafts.GetValueOrDefault(id, initial), Watermark = watermark, MinHeight = 36, FontSize = 12 };
        AutomationProperties.SetName(box, watermark);
        box.TextChanged += (_, _) => { if (box.Text == initial) _formDrafts.Remove(id); else _formDrafts[id] = box.Text ?? ""; UpdateFooter(); };
        return box;
    }

    private Control Icon(string key)
    {
        var data = key switch
        {
            "overview" => "M2 2h6v6H2z M12 2h6v6h-6z M2 12h6v6H2z M12 12h6v6h-6z",
            "clients" => "M2 3h13v10H2z M5 17h13V7 M5 8h7",
            "preview" => "M2 3h16v13H2z M2 7h16 M6 10h4v3H6z",
            "cycle" => "M3 7a7 7 0 0112-3l2 2 M17 1v5h-5 M17 13a7 7 0 01-12 3l-2-2 M3 19v-5h5",
            "performance" => "M2 16h3l3-10 4 13 3-9h3 M2 3h16",
            "profiles" => "M3 5h5l2 2h8v10H3z M3 5V3h7l2 2h6v2",
            "theme" => "M10 2a8 8 0 100 16h1a2 2 0 001.4-3.4l-.2-.2a1.5 1.5 0 011.1-2.4H15a3 3 0 003-3c0-4-3.6-7-8-7z M6 7a.8.8 0 110 1.6a.8.8 0 010-1.6 M9 4.5a.8.8 0 110 1.6a.8.8 0 010-1.6 M13 5a.8.8 0 110 1.6a.8.8 0 010-1.6 M5.5 11a.8.8 0 110 1.6a.8.8 0 010-1.6",
            "character" => "M10 2a3 3 0 100 6a3 3 0 100-6 M3 18v-3a7 6 0 0114 0v3z",
            "chart" => "M2 2v16h16 M6 14V9 M10 14V5 M14 14V7",
            "search" => "M9 2a6 6 0 100 12a6 6 0 100-12 M13 13l5 5",
            "overlay" => "M2 3h16v14H2z M6 13V7h8v6 M10 7v6",
            "sliders" => "M2 5h16 M2 10h16 M2 15h16 M6 3v4 M14 8v4 M8 13v4",
            _ => "M10 2a8 8 0 100 16a8 8 0 100-16 M8 7a2 2 0 114 0c0 2-2 2-2 4 M10 14v1",
        };
        return new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Stroke = B(_theme.Muted), StrokeThickness = 1.4, StrokeLineCap = PenLineCap.Round, Width = 18, Height = 18, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    }

    private void Confirm(string title, string description, string accept, Func<Task> action)
    {
        DismissConfirmation();
        _confirmationPreviousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var body = new StackPanel { Spacing = 17 };
        body.Children.Add(Text(title, 22, _theme.Text, true));
        body.Children.Add(Text(description, 13, _theme.Muted));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = ActionButton("Cancel", DismissConfirmation, "cancel-confirmation");
        buttons.Children.Add(cancel);
        buttons.Children.Add(ActionButton(accept, async () => { DismissConfirmation(); await action(); }, "accept-confirmation", true));
        body.Children.Add(buttons);
        _confirmation = new Border { Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), Child = new Border { Background = B(_theme.Surface), BorderBrush = B(_theme.Border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(_theme.Radius), Padding = new Thickness(28), MaxWidth = 480, Margin = new Thickness(28), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = body } };
        Grid.SetColumnSpan(_confirmation, 2);
        _root.Children.Add(_confirmation);
        // The rest of the workspace remains visible but cannot receive mouse or keyboard actions.
        foreach (var child in _root.Children.Where(c => c != _confirmation)) child.IsEnabled = false;
        cancel.Focus();
    }

    private void DismissConfirmation()
    {
        if (_confirmation is null) return;
        bool expanded = _expandedOrderGroup is not null;
        _cancelOrderDrag?.Invoke();
        _expandedOrderGroup = null;
        _root.Children.Remove(_confirmation);
        _confirmation = null;
        foreach (var child in _root.Children) child.IsEnabled = true;
        if (expanded)
        {
            RenderPage(true);
            _confirmationPreviousFocus = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "expand-cycle-order");
        }
        // A command or background refresh can replace the page behind a dialog.
        // Restore focus to the current control with the same stable identity.
        if (_confirmationPreviousFocus?.Name is { } focusName)
            _confirmationPreviousFocus = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == focusName) ?? _confirmationPreviousFocus;
        _confirmationPreviousFocus?.Focus();
        _confirmationPreviousFocus = null;
    }
}
