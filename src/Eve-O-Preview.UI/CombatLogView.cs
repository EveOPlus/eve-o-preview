using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using EveOPreview.Preview;

namespace EveOPreview.UI;

/// <summary>Retains unapplied folder/colour fields and navigation across refreshes.</summary>
public sealed class CombatLogDrafts : IWorkspaceModuleDrafts
{
    public string? Directory { get; set; }
    public string Selected { get; set; } = "All thumbnails (defaults)";
    public int Tab { get; set; }
    public string? DetailCharacter { get; set; }
    public Dictionary<string, string> Colors { get; } = new();
    public Dictionary<string, string> Fonts { get; } = new();
    public Dictionary<string, bool> Sections { get; } = new();
    public bool HasUnappliedEdits => Directory is not null || Colors.Count > 0 || Fonts.Count > 0;
    public void Discard() { Directory = null; Colors.Clear(); Fonts.Clear(); }
}

/// <summary>Portable telemetry page. Data updates do not rebuild settings or steal keyboard focus.</summary>
public sealed partial class CombatLogView : UserControl, IDisposable, IWorkspaceLiveModule
{
    private readonly IWorkspaceBackend _backend;
    private readonly IWorkspaceCombatLogs _logs;
    private readonly CombatLogDrafts _drafts;
    private readonly WorkspaceTheme _theme;
    private readonly StackPanel _stats = new() { Spacing = 10 };
    private readonly StackPanel _overlay = new() { Spacing = 8 };
    private readonly TextBlock _status, _message, _summary, _detectedDirectory;
    private readonly ComboBox _characterPicker;
    private readonly Expander _exceptions;
    private readonly TextBox _directory;
    private bool _disposed, _busy;
    private bool _updatingCharacters;
    private bool _lastSaveSucceeded;
    private int _queued;
    private string _selected = "All thumbnails (defaults)";

    public static WorkspaceModule CreateModule()
    {
        var drafts = new CombatLogDrafts();
        return new("Dps", "Augments", "Thumbnail damage, repairs and solar systems.",
            backend => new CombatLogView(backend, (IWorkspaceCombatLogs)backend, drafts), drafts)
        {
            SearchTargets = [
                new("thumbnail-augments", "Thumbnail augments", ["DPS", "reps", "Repairs", "repair rate", "repair per second", "damage per second",
                    "Incoming", "Outgoing", "Shield repairs", "Armour repairs", "Hull repairs", "armor", "Show DPS", "Show alpha",
                    "Show weapon icons", "weapon platform", "ammunition", "ammo", "damage type", "unknown", "Incoming damage indicator", "Simulation", "simulator", "thumbnail overlays", "flashing", "opacity", "settings"], () => drafts.Tab = 1),
                new("data-setup", "Data setup", ["log", "logs", "logging", "log setup", "Read EVE logs", "FC static data", "SDE", "download", "data sources",
                    "log folder", "Folder containing Gamelogs and Chatlogs", "configure", "settings"], () => drafts.Tab = 2),
                new("overview", "Overview", ["Reset statistics", "counters", "Jumps", "travel", "combat totals", "Average DPS", "Combined statistics", "repair totals", "repair cycles"], () => drafts.Tab = 0)
            ]
        };
    }

    public CombatLogView(IWorkspaceBackend backend, IWorkspaceCombatLogs logs, CombatLogDrafts? drafts = null)
    {
        _backend = backend; _logs = logs; _drafts = drafts ?? new(); _theme = WorkspaceTheme.Get(backend.Read().Theme);
        _localization = new(backend.Read().UiLanguage);
        _selected = _drafts.Selected;
        var settings = logs.ReadLogSettings();
        var root = new StackPanel { Spacing = 14 };
        Content = root;
        _status = Text("Starting log reader…", 14, true); _status.Name = "logs-status";
        _message = Text("", 12); _message.IsVisible = false;
        var setup = new StackPanel { Spacing = 8 };
        setup.Children.Add(Toggle("Read EVE logs", "logs-enabled", settings.Enabled,
            value => Save(_logs.ReadLogSettings() with { Enabled = value })));
        setup.Children.Add(Text("Enable game-message and Local chat logging in EVE.", 12));
        _detectedDirectory = Text("Detecting EVE logs folder…", 12); _detectedDirectory.Name = "logs-detected-directory";
        setup.Children.Add(_detectedDirectory);
        var manual = new StackPanel { Spacing = 8 };
        manual.Children.Add(Text("Folder containing Gamelogs and Chatlogs", 12));
        _directory = new TextBox { Name = "logs-directory", Text = _drafts.Directory ?? settings.Directory,
            Watermark = L("Automatic: Documents / EVE / logs"), MinWidth = 180 };
        _directory.TextChanged += (_, _) => _drafts.Directory = _directory.Text == _logs.ReadLogSettings().Directory ? null : _directory.Text ?? "";
        manual.Children.Add(_directory);
        var folderButtons = new WrapPanel { Orientation = Orientation.Horizontal };
        folderButtons.Children.Add(Button("Browse…", "logs-browse-directory", async () =>
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage?.CanPickFolder != true) { Report(CommandResult.Error("Folder browsing is unavailable here; enter the full folder path.")); return; }
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                { Title = L("Choose EVE logs folder (contains Gamelogs and Chatlogs)"), AllowMultiple = false });
            try
            {
                if (_disposed) return;
                if (folders.FirstOrDefault() is { } selectedFolder)
                {
                    string? path = selectedFolder.TryGetLocalPath();
                    if (path is not null) _directory.Text = path;
                    else Report(CommandResult.Error("Choose a local or Windows network folder."));
                }
            }
            finally { foreach (var folder in folders) folder.Dispose(); }
        }));
        folderButtons.Children.Add(Button("Apply folder", "logs-apply-directory", async () =>
        {
            var result = await Save(_logs.ReadLogSettings() with { Directory = _directory.Text ?? "" });
            if (result.Success) _drafts.Directory = null;
        }));
        var manualSection = new Expander { Header = L("Advanced: custom log folder"), Name = "logs-manual-folder", Content = manual,
            IsExpanded = !string.IsNullOrEmpty(_drafts.Directory ?? settings.Directory), HorizontalAlignment = HorizontalAlignment.Stretch };
        folderButtons.Children.Add(Button("Use automatic location", "logs-auto-directory", async () =>
        {
            var result = await Save(_logs.ReadLogSettings() with { Directory = "" });
            if (result.Success) { _directory.Text = ""; _drafts.Directory = null; manualSection.IsExpanded = false; }
        }));
        manual.Children.Add(folderButtons); setup.Children.Add(manualSection);
        setup.Children.Add(Number("DPS window (seconds)", "logs-window", settings.WindowSeconds, 1, 300,
            value => Save(_logs.ReadLogSettings() with { WindowSeconds = value })));
        setup.Children.Add(Number("Keep recent entries (days)", "logs-retention", settings.RetentionDays, 1, 30,
            value => Save(_logs.ReadLogSettings() with { RetentionDays = value })));
        var commands = new WrapPanel { Orientation = Orientation.Horizontal };
        commands.Children.Add(Button("Retry reading logs", "logs-rescan", async () => Report(await _logs.RescanLogsAsync())));
        commands.Children.Add(Button("Reset overall combat totals…", "logs-reset", () =>
        {
            var confirmation = new StackPanel { Spacing = 8 };
            confirmation.Children.Add(Text("Reset statistics and recent entries? Current systems and file positions will be kept.", 12));
            confirmation.Children.Add(Button("Reset now", "logs-reset-confirm", async () =>
            { Report(await _logs.ResetCombatAsync()); root.Children.Remove(confirmation); }));
            confirmation.Children.Add(Button("Cancel", "logs-reset-cancel", () => { root.Children.Remove(confirmation); return Task.CompletedTask; }));
            if (!root.Children.OfType<StackPanel>().Any(x => x.Name == "logs-reset-confirmation"))
            { confirmation.Name = "logs-reset-confirmation"; root.Children.Insert(1, confirmation); }
            return Task.CompletedTask;
        }));
        setup.Children.Add(commands); setup.Children.Add(BuildStaticData());
        _characterPicker = new ComboBox { Name = "logs-overlay-character", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<string>((item, _) => RawText(item == "All thumbnails (defaults)" ? L(item) : item ?? "", 12)) };
        ScrollViewer.SetVerticalScrollBarVisibility(_characterPicker, ScrollBarVisibility.Visible);
        ScrollViewer.SetAllowAutoHide(_characterPicker, false);
        UpdateCharacters();
        _characterPicker.SelectionChanged += (_, _) => { if (_updatingCharacters) return; _selected = _characterPicker.SelectedItem as string ?? "All thumbnails (defaults)"; _drafts.Selected = _selected; RenderOverlaySettings(); };
        _exceptions = new Expander { Name = "logs-overlay-exceptions", Header = L("Per-client settings"),
            IsExpanded = _selected.StartsWith("EVE - ", StringComparison.Ordinal), HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = _characterPicker };
        _exceptions.Collapsed += (_, _) => _characterPicker.SelectedItem = "All thumbnails (defaults)";
        RenderOverlaySettings();
        var simulator = BuildSimulator();
        _summary = Text("", 14, true); _summary.Name = "logs-overall";
        logs.LogsChanged += OnLogsChanged;
        RefreshData();
        ArrangeSections(root, setup, simulator);
    }

    private void ArrangeSections(StackPanel root, Control setup, Control simulator)
    {
        var overview = new StackPanel { Spacing = 12, Children = { _summary, BuildStatisticsReset(), _stats } };
        var configuration = new StackPanel { Spacing = 10, Children = { _exceptions, Card(_overlay), BuildDamageFlashSettings() } };
        var overlayGrid = new Grid { ColumnDefinitions = new("*"), RowDefinitions = new("Auto,Auto"), RowSpacing = 14, ColumnSpacing = 14 };
        overlayGrid.Children.Add(configuration); overlayGrid.Children.Add(simulator); Grid.SetRow(simulator, 1);
        overlayGrid.SizeChanged += (_, _) =>
        {
            bool wide = overlayGrid.Bounds.Width >= 760;
            if ((Grid.GetColumn(simulator) == 1) == wide) return;
            overlayGrid.ColumnDefinitions = new(wide ? "*,*" : "*");
            Grid.SetColumn(simulator, wide ? 1 : 0); Grid.SetRow(simulator, wide ? 0 : 1);
        };
        var tabs = new TabControl { Name = "logs-tabs", SelectedIndex = _drafts.Tab, ItemsSource = new[]
        {
            new TabItem { Header = L("Overview"), Content = overview },
            new TabItem { Header = L("Thumbnail augments"), Content = overlayGrid },
            new TabItem { Header = L("Data setup"), Content = Card(setup) }
        } };
        tabs.SelectionChanged += (_, args) => { if (args.Source == tabs) _drafts.Tab = tabs.SelectedIndex; };
        root.Children.Add(_status); root.Children.Add(_message); root.Children.Add(tabs);
    }

    private void UpdateCharacters()
    {
        var settings = _logs.ReadLogSettings();
        var titles = _backend.Read().Clients.Select(x => x.Title)
            .Concat(settings.Overlays.Keys).Concat(_logs.ReadLogs().Characters.Select(x => "EVE - " + x.Name))
            .Where(x => x.StartsWith("EVE - ", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Prepend("All thumbnails (defaults)").ToArray();
        if (_characterPicker.ItemsSource is IEnumerable<string> previous && previous.SequenceEqual(titles)) return;
        string next = titles.Contains(_selected) ? _selected : titles[0];
        _updatingCharacters = true;
        try { _characterPicker.ItemsSource = titles; _characterPicker.SelectedItem = next; }
        finally { _updatingCharacters = false; }
        bool changed = _selected != next; _selected = next; _drafts.Selected = next;
        if (changed && _exceptions is not null) RenderOverlaySettings();
    }

    private void RenderOverlaySettings()
    {
        foreach (var section in _overlay.GetLogicalDescendants().OfType<Expander>())
            if (section.Name is { } name) _drafts.Sections[name] = section.IsExpanded;
        _overlay.Children.Clear();
        bool defaults = _selected == "All thumbnails (defaults)";
        var settings = _logs.ReadLogSettings();
        var options = defaults ? settings.DefaultOverlay : settings.Overlays.GetValueOrDefault(_selected) ?? settings.DefaultOverlay;
        _overlay.Children.Add(RawText(defaults ? L("All thumbnails") : F($"{_selected[6..]} - per-client settings"), 16, true));
        if (defaults && settings.Overlays.Count > 0)
            _overlay.Children.Add(RawText(F($"Per-client settings: {settings.Overlays.Count}"), 12));
        if (!defaults) _overlay.Children.Add(Button("Edit all thumbnails", "logs-overlay-all", () =>
        { _characterPicker.SelectedItem = "All thumbnails (defaults)"; _exceptions.IsExpanded = false; return Task.CompletedTask; }));
        Task Change(Func<LogOverlayOptions, LogOverlayOptions> edit)
        {
            var current = _logs.ReadLogSettings();
            var next = edit(defaults ? current.DefaultOverlay : current.Overlays.GetValueOrDefault(_selected) ?? current.DefaultOverlay);
            if (defaults) return Save(current with { DefaultOverlay = next });
            var overrides = new Dictionary<string, LogOverlayOptions>(current.Overlays, StringComparer.Ordinal) { [_selected] = next };
            return Save(current with { Overlays = overrides });
        }
        _overlay.Children.Add(Choice("Configuration mode", "logs-overlay-mode", new[] { "Simple", "Advanced" }, options.Advanced ? "Advanced" : "Simple", async mode =>
        {
            await Change(x => x with { Advanced = mode == "Advanced", SystemColor = x.GetAppearance().SystemColor, CustomAppearance = x.CustomAppearance ?? x.GetAppearance() });
            if (!_disposed) RenderOverlaySettings();
        }));
        _overlay.Children.Add(Choice("Theme", "logs-overlay-preset", new[] { "Classic", "Damage colours", "Minimal" }, options.Preset, async preset =>
        {
            await Change(x => x with { Preset = preset, SystemColor = x.GetAppearance().SystemColor, CustomAppearance = null, Incoming = true, Outgoing = true, DamageEvents = preset != "Minimal" });
            if (!_disposed) RenderOverlaySettings();
        }));
        if (!defaults)
        {
            _overlay.Children.Add(Button("Use shared settings", "logs-overlay-inherit", async () =>
            {
                var current = _logs.ReadLogSettings(); var overrides = new Dictionary<string, LogOverlayOptions>(current.Overlays);
                overrides.Remove(_selected); await Save(current with { Overlays = overrides });
                if (!_disposed) RenderOverlaySettings();
            }));
        }
        if (options.Advanced)
        {
            string measures = options.DamageEvents ? options.Incoming || options.Outgoing ? "Alpha + DPS" : "Alpha only"
                : options.Incoming || options.Outgoing ? "DPS only" : "Hidden";
            _overlay.Children.Add(Choice("Damage display", "logs-overlay-measures", new[] { "Alpha + DPS", "Alpha only", "DPS only", "Hidden" }, measures, async value =>
            {
                await Change(x => x with { DamageEvents = value is "Alpha + DPS" or "Alpha only",
                    Incoming = value is "Alpha + DPS" or "DPS only", Outgoing = value is "Alpha + DPS" or "DPS only" });
                if (!_disposed) RenderOverlaySettings();
            }));
        }
        else
        {
            _overlay.Children.Add(Toggle("Show DPS", "logs-show-dps", options.Incoming || options.Outgoing,
                v => Change(x => x with { Incoming = v, Outgoing = v })));
            _overlay.Children.Add(Toggle("Show alpha", "logs-show-alpha", options.DamageEvents,
                v => Change(x => x with { DamageEvents = v })));
        }
        _overlay.Children.Add(Toggle("Show weapon icons", "logs-show-weapon-icon", options.GetAppearance().ShowWeaponIcon, async v =>
        {
            await Change(x => x with { CustomAppearance = (x.CustomAppearance ?? x.GetAppearance()) with { ShowWeaponIcon = v } });
            if (!_disposed && _lastSaveSucceeded) RenderOverlaySettings();
        }));
        _overlay.Children.Add(Toggle("Show incoming / outgoing repairs", "logs-overlay-repairs", options.Repairs, v => Change(x => x with { Repairs = v })));
        if (!options.Advanced) return;
        _overlay.Children.Add(Toggle("Incoming DPS", "logs-overlay-incoming", options.Incoming, async v =>
        { await Change(x => x with { Incoming = v }); if (!_disposed) RenderOverlaySettings(); }));
        _overlay.Children.Add(Toggle("Outgoing DPS", "logs-overlay-outgoing", options.Outgoing, async v =>
        { await Change(x => x with { Outgoing = v }); if (!_disposed) RenderOverlaySettings(); }));
        _overlay.Children.Add(Toggle("Show NPC / Player DPS", "logs-overlay-breakdown", options.Breakdown, v => Change(x => x with { Breakdown = v })));
        _overlay.Children.Add(Number("Event visibility (seconds)", "logs-overlay-duration", options.EventDurationSeconds, 1, 15, v => Change(x => x with { EventDurationSeconds = v })));
        int sectionStart = _overlay.Children.Count;
        _overlay.Children.Add(Choice("DPS position", "logs-overlay-position", Enum.GetValues<OverlayPosition>(), options.GetStatsStyle().EffectivePosition,
            v => Change(x => x with { Position = v })));
        _overlay.Children.Add(Choice("Row order", "logs-overlay-order", Enum.GetValues<CombatRowOrder>(), options.RowOrder,
            v => Change(x => x with { RowOrder = v })));
        _overlay.Children.Add(Number("Horizontal offset (pixels)", "logs-overlay-x", options.OffsetX, 0, 500, v => Change(x => x with { OffsetX = v })));
        _overlay.Children.Add(Number("Vertical offset (pixels)", "logs-overlay-y", options.OffsetY, 0, 500, v => Change(x => x with { OffsetY = v })));
        Group("Position & order", "logs-layout", sectionStart, true);
        sectionStart = _overlay.Children.Count;
        _overlay.Children.Add(Number("Font size (pixels)", "logs-overlay-font", options.FontSize, 8, 32, v => Change(x => x with { FontSize = v })));
        AppendCombatFont(options, Change);
        Group("DPS / alpha font", "logs-font-settings", sectionStart, false);
        AppendAdvancedAppearance(options, Change);
        foreach (var section in _overlay.GetLogicalDescendants().OfType<Expander>())
            if (section.Name is { } name && _drafts.Sections.TryGetValue(name, out bool expanded)) section.IsExpanded = expanded;

        void Group(string label, string name, int start, bool expanded)
        {
            var panel = new StackPanel { Spacing = 8 };
            foreach (var control in _overlay.Children.Skip(start).ToArray()) { _overlay.Children.Remove(control); panel.Children.Add(control); }
            _overlay.Children.Add(new Expander { Header = L(label), Name = name, Content = panel, IsExpanded = expanded, HorizontalAlignment = HorizontalAlignment.Stretch });
        }
    }

    public void RefreshWorkspace() { if (!_disposed) RefreshData(); }

    private void OnLogsChanged()
    {
        if (_disposed || Interlocked.Exchange(ref _queued, 1) != 0) return;
        Dispatcher.UIThread.Post(() => { Interlocked.Exchange(ref _queued, 0); if (!_disposed) RefreshData(); });
    }

    private void RefreshData()
    {
        var data = _logs.ReadLogs();
        _status.Text = L(data.Status);
        _detectedDirectory.Text = string.IsNullOrWhiteSpace(_logs.ReadLogSettings().Directory)
            ? F($"Automatic location: {data.Directory}") : F($"Custom location: {data.Directory}");
        _directory.FlowDirection = FlowDirection.LeftToRight;
        _detectedDirectory.FlowDirection = FlowDirection.LeftToRight;
        _status.ToolTipSet(data.Directory);
        UpdateCharacters();
        RefreshNameFlashes();
        _refreshAppearancePreview?.Invoke();
        double incoming = data.Characters.Sum(x => x.Categories.Sum(c => c.Total.Incoming));
        double outgoing = data.Characters.Sum(x => x.Categories.Sum(c => c.Total.Outgoing));
        _summary.Text = F($"Overall since {data.Since.ToLocalTime():g} · In {incoming:N0} · Out {outgoing:N0}");
        // Client-to-client damage appears from each observer's perspective; do not
        // add incoming and outgoing together and call it unique fleet damage.
        RefreshOverview(data);
        RefreshNameFlashes();
    }

    private void Cell(Grid grid, int column, int row, string value, bool bold = false)
    { var text = Text(value, 12, bold); text.Margin = new(0, 5, 8, 5); Grid.SetColumn(text, column); Grid.SetRow(text, row); grid.Children.Add(text); }
    private TextBlock Text(string value, int size, bool bold = false) => new()
    { Text = L(value), FontSize = size, Foreground = WorkspaceTheme.Brush(_theme.Text), TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal };
    private Border Card(Control content) => new() { Background = WorkspaceTheme.Brush(_theme.Surface), Padding = new(14),
        CornerRadius = new(_theme.Radius), BorderBrush = WorkspaceTheme.Brush(_theme.Border), BorderThickness = new(1), Child = content };
    private CheckBox Toggle(string label, string name, bool value, Func<bool, Task> change)
    {
        var control = new CheckBox { Name = name, Content = L(label), IsChecked = value };
        control.IsCheckedChanged += async (_, _) => { if (!_disposed && !_busy) await Run(() => change(control.IsChecked == true)); };
        return control;
    }
    private Control Number(string label, string name, int value, int min, int max, Func<int, Task> change)
    {
        var grid = new Grid { ColumnDefinitions = new("*,140") };
        grid.Children.Add(Text(label, 12));
        var input = new NumericUpDown { Name = name, Minimum = min, Maximum = max, Value = value, Increment = 1, FormatString = "0" };
        input.ValueChanged += async (_, _) => { if (!_disposed && !_busy && input.Value is decimal next) await Run(() => change((int)next)); };
        Grid.SetColumn(input, 1); grid.Children.Add(input); return grid;
    }
    private Button Button(string text, string name, Func<Task> action)
    {
        var button = new Button { Name = name, Content = L(text), Margin = new(0, 0, 8, 0), Padding = new(12, 7) };
        button.Click += async (_, _) => await Run(action); return button;
    }
    private async Task Run(Func<Task> action)
    {
        if (_disposed || _busy) return;
        _busy = true;
        try { await action(); }
        catch { if (!_disposed) Report(CommandResult.Error("The log operation failed. Retry after checking the source folder.")); }
        finally { _busy = false; }
    }
    private async Task<CommandResult> Save(CombatLogSettings settings)
    { var result = await _logs.SaveLogSettingsAsync(settings); _lastSaveSucceeded = result.Success; if (!_disposed) Report(result); return result; }
    private void Report(CommandResult result)
    { if (_disposed) return; _message.Text = result.Localize(L); _message.IsVisible = !string.IsNullOrWhiteSpace(result.Message);
        _message.Foreground = WorkspaceTheme.Brush(result.Success ? _theme.Positive : _theme.Danger); }
    public void Dispose() { _disposed = true; _logs.LogsChanged -= OnLogsChanged; _simulationPreview?.Dispose(); _nameFlashTimer?.Stop(); DisposeOverviewPortraits();
        if (_backend is IWorkspaceStaticData data && _staticDataChanged is not null) data.StaticDataChanged -= _staticDataChanged; }
}

internal static class CombatLogToolTip
{
    public static void ToolTipSet(this Control control, string text) => ToolTip.SetTip(control, text);
}
