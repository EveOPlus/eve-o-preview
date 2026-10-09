using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EveOPreview.UI;

public sealed class RegionDrafts : IWorkspaceModuleDrafts
{
    public string Selected { get; set; } = "";
    public Dictionary<string, Dictionary<string, string>> Edits { get; } = new();
    public bool HasUnappliedEdits => Edits.Count > 0;
    public void Discard() => Edits.Clear();
}

public sealed class RegionsView : UserControl, IWorkspaceLiveModule, IDisposable
{
    private readonly IWorkspaceBackend _backend;
    private readonly IWorkspaceRegions _regions;
    private readonly RegionDrafts _drafts;
    private readonly WorkspaceLocalization _localization;
    private readonly WorkspaceTheme _theme;
    private readonly ComboBox _picker;
    private readonly CheckBox _editing, _enabled, _dragDocking;
    private readonly RegionMonitorMap _monitorMap;
    private readonly StackPanel _details = new() { Spacing = 14 };
    private StackPanel _clients = new() { Spacing = 8 };
    private readonly TextBlock _message;
    private readonly Dictionary<string, TextBox> _fields = new();
    private string _clientSignature = "";
    private bool _updating, _busy, _disposed;

    public static WorkspaceModule CreateModule()
    {
        var drafts = new RegionDrafts();
        return new("Regions", "Regions", "Arrange and dock thumbnails in screen regions.", backend => new RegionsView(backend, drafts), drafts)
        {
            SearchTargets = [new("regions", "Regions", ["region", "regions", "dock", "undock", "docking", "auto dock", "enable regions", "disable regions", "monitor layout", "displays", "template", "arrange", "position", "resize", "assign clients", "cycle groups", "yellow boxes", "snap regions", "screen edges", "drag", "drop", "move thumbnail"], () => { })]
        };
    }

    public RegionsView(IWorkspaceBackend backend, RegionDrafts? drafts = null)
    {
        _backend = backend; _regions = (IWorkspaceRegions)backend; _drafts = drafts ?? new();
        var snapshot = backend.Read();
        _localization = new(snapshot.UiLanguage); _theme = WorkspaceTheme.Get(snapshot.Theme);
        var root = new StackPanel { Spacing = 16 };
        Content = root;
        _monitorMap = new RegionMonitorMap(_theme, L);
        _monitorMap.RegionSelected += id => { _drafts.Selected = id; RefreshWorkspace(); };
        root.Children.Add(_monitorMap);
        var options = new WrapPanel { Orientation = Orientation.Horizontal };
        _enabled = Toggle("Enable regions", "region-enabled");
        _dragDocking = Toggle("Dock and undock with right-click drag", "region-drag-docking");
        options.Children.Add(_enabled); options.Children.Add(_dragDocking);
        root.Children.Add(options);
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(Button("Add region", "region-add", async () =>
        {
            var previous = _regions.ReadRegions().Regions.Select(region => region.Id).ToHashSet();
            if (await Send(new("region-add"))) _drafts.Selected = _regions.ReadRegions().Regions.First(region => !previous.Contains(region.Id)).Id;
            RefreshWorkspace();
        }));
        _editing = new CheckBox { Content = L("Edit regions on screen"), Name = "region-edit", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };
        _editing.IsCheckedChanged += async (_, _) =>
        {
            if (!_updating) await Send(new("region-edit", Value: (_editing.IsChecked == true).ToString()));
        };
        toolbar.Children.Add(_editing);
        root.Children.Add(toolbar);
        _message = Text(""); _message.Name = "region-message"; _message.IsVisible = false;
        root.Children.Add(_message);
        _picker = new ComboBox { Name = "region-picker", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<RegionItem>((region, _) => Text(region?.Name ?? "", raw: true)) };
        _picker.SelectionChanged += (_, _) =>
        {
            if (_updating) return;
            _drafts.Selected = (_picker.SelectedItem as RegionItem)?.Id ?? "";
            _clientSignature = "";
            _monitorMap.Update(_regions.ReadRegions(), _drafts.Selected);
            RenderDetails();
        };
        root.Children.Add(_picker);
        root.Children.Add(_details);
        KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape || !_regions.ReadRegions().Editing) return;
            _regions.StopRegionEditing(); RefreshWorkspace(); args.Handled = true;
        };
        RefreshWorkspace();
    }

    private CheckBox Toggle(string label, string action)
    {
        var toggle = new CheckBox { Name = action, Content = L(label), Margin = new Thickness(0, 0, 20, 0) };
        toggle.IsCheckedChanged += async (_, _) =>
        {
            if (!_updating) await Send(new(action, Value: (toggle.IsChecked == true).ToString()));
        };
        return toggle;
    }

    public void RefreshWorkspace()
    {
        if (_disposed) return;
        var state = _regions.ReadRegions();
        _updating = true;
        try
        {
            _editing.IsChecked = state.Editing;
            _enabled.IsChecked = state.Enabled;
            _dragDocking.IsChecked = state.DragDocking;
            _dragDocking.IsEnabled = _editing.IsEnabled = state.Enabled;
            if (!state.Regions.Any(region => region.Id == _drafts.Selected)) _drafts.Selected = state.Regions.FirstOrDefault()?.Id ?? "";
            _monitorMap.Update(state, _drafts.Selected);
            string previous = (_picker.SelectedItem as RegionItem)?.Id ?? "";
            _picker.ItemsSource = state.Regions;
            _picker.SelectedItem = state.Regions.FirstOrDefault(region => region.Id == _drafts.Selected);
            _picker.IsVisible = state.Regions.Count > 0;
            if (previous != _drafts.Selected || _fields.Count == 0) RenderDetails();
            else
            {
                if (_picker.SelectedItem is RegionItem region) SetFields(region);
                RefreshClients();
            }
            if (state.Error.Length > 0) Report(state.Error);
        }
        finally { _updating = false; }
    }

    private void RenderDetails()
    {
        _details.Children.Clear(); _fields.Clear(); _clientSignature = "";
        _clients = new StackPanel { Spacing = 8 };
        var region = _regions.ReadRegions().Regions.FirstOrDefault(item => item.Id == _drafts.Selected);
        if (region == null)
        {
            _details.Children.Add(Text("Add a region to start arranging thumbnails."));
            return;
        }
        var fields = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (string key in new[] { "Name" })
        {
            var field = new TextBox { Name = "region-name", Width = 300 };
            _fields.Add(key, field);
            var column = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 12, 8) };
            column.Children.Add(Text(key)); column.Children.Add(field); fields.Children.Add(column);
            field.TextChanged += (_, _) =>
            {
                if (_updating) return;
                var current = _regions.ReadRegions().Regions.FirstOrDefault(item => item.Id == _drafts.Selected);
                var values = _fields.ToDictionary(pair => pair.Key, pair => pair.Value.Text ?? "");
                if (current == null) return;
                var saved = Values(current);
                var edits = values.Where(pair => saved[pair.Key] != pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                if (edits.Count == 0) _drafts.Edits.Remove(_drafts.Selected);
                else _drafts.Edits[_drafts.Selected] = edits;
            };
        }
        bool updating = _updating;
        _updating = true; SetFields(region); _updating = updating;
        _details.Children.Add(fields);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Button("Save name", "region-save", async () =>
        {
            string id = _drafts.Selected;
            if (await Send(new("region-rename", id, _fields["Name"].Text ?? ""))) _drafts.Edits.Remove(id);
            RefreshWorkspace();
        }));
        actions.Children.Add(Button("Discard changes", "region-discard", () =>
        {
            _drafts.Edits.Remove(_drafts.Selected); RefreshWorkspace(); return Task.CompletedTask;
        }));
        actions.Children.Add(Button("Remove region", "region-delete", async () =>
        {
            string id = _drafts.Selected;
            if (await Send(new("region-delete", id))) _drafts.Edits.Remove(id);
            RefreshWorkspace();
        }));
        _details.Children.Add(actions);
        _details.Children.Add(Text("Assigned clients"));
        _details.Children.Add(new ScrollViewer { Content = _clients, MaxHeight = 330, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        RefreshClients();
    }

    private void SetFields(RegionItem region)
    {
        var values = Values(region);
        if (_drafts.Edits.TryGetValue(region.Id, out var edits))
            foreach (var pair in edits) values[pair.Key] = pair.Value;
        foreach (var pair in _fields) pair.Value.Text = values[pair.Key];
    }

    private static Dictionary<string, string> Values(RegionItem region) => new()
    {
        ["Name"] = region.Name
    };

    private void RefreshClients()
    {
        var snapshot = _backend.Read(); var state = _regions.ReadRegions();
        var titles = snapshot.Clients.Select(client => client.Title)
            .Concat(snapshot.CycleGroups.SelectMany(group => group.Clients))
            .Distinct(StringComparer.Ordinal).OrderBy(title => title, StringComparer.OrdinalIgnoreCase).ToArray();
        string signature = _drafts.Selected + "|" + string.Join("|", titles.Select(title => title + ":" + state.Assignments.GetValueOrDefault(title)))
            + "|" + string.Join("|", state.Regions.Select(region => region.Name));
        if (signature == _clientSignature) return;
        _clientSignature = signature; _clients.Children.Clear();
        if (titles.Length == 0) { _clients.Children.Add(Text("No available clients or clients in cycle groups.")); return; }
        foreach (string title in titles)
        {
            string assigned = state.Assignments.GetValueOrDefault(title) ?? "";
            string other = state.Regions.FirstOrDefault(region => region.Id == assigned && assigned != _drafts.Selected)?.Name ?? "";
            var row = new CheckBox { Name = "region-client-" + title, Content = title + (other.Length > 0 ? " (" + other + ")" : ""),
                IsChecked = assigned == _drafts.Selected, Foreground = WorkspaceTheme.Brush(_theme.Text) };
            row.IsCheckedChanged += async (_, _) =>
            {
                if (_updating) return;
                await Send(new("region-assign", title, row.IsChecked == true ? _drafts.Selected : ""));
                _clientSignature = ""; RefreshClients();
            };
            _clients.Children.Add(row);
        }
    }

    private async Task<bool> Send(WorkspaceCommand command)
    {
        var result = await _backend.ExecuteAsync(command);
        Report(result.Success ? "" : result.Localize(L));
        RefreshWorkspace();
        return result.Success;
    }

    private Button Button(string title, string name, Func<Task> action)
    {
        var button = new Button { Name = name, Content = L(title), Margin = new Thickness(0, 0, 8, 0) };
        button.Click += async (_, _) =>
        {
            if (_busy) return;
            _busy = true;
            try { await action(); }
            finally { _busy = false; }
        };
        return button;
    }
    private string L(string value) => _localization.Get(value);
    private TextBlock Text(string value, bool raw = false) => new() { Text = raw ? value : L(value), FontSize = 14,
        Foreground = WorkspaceTheme.Brush(_theme.Text), TextWrapping = TextWrapping.Wrap };
    private void Report(string message) { _message.Text = L(message); _message.IsVisible = message.Length > 0; }
    public void Dispose() { if (_disposed) return; _disposed = true; _regions.StopRegionEditing(); }
}
