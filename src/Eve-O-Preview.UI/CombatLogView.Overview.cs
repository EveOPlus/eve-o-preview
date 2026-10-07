using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace EveOPreview.UI;

public sealed partial class CombatLogView
{
    private Control BuildStatisticsReset()
    {
        var popup = new Flyout();
        CombatResetScope scope = CombatResetScope.All;
        string? character = null;
        var content = new StackPanel { Name = "logs-reset-popup", Spacing = 10, Width = 310 };
        content.Children.Add(Text("Reset statistics", 16, true));
        var explanation = Text("", 12); explanation.Name = "logs-reset-target"; content.Children.Add(explanation);
        content.Children.Add(Choice("What to reset", "logs-reset-scope", Enum.GetValues<CombatResetScope>(), scope,
            value => { scope = value; return Task.CompletedTask; }));
        var buttons = new WrapPanel();
        buttons.Children.Add(Button("Reset now", "logs-overview-reset-confirm", async () =>
        {
            var result = await _logs.ResetCombatAsync(scope, character); Report(result);
            if (result.Success) { popup.Hide(); RefreshData(); }
        }));
        buttons.Children.Add(Button("Cancel", "logs-overview-reset-cancel", () => { popup.Hide(); return Task.CompletedTask; }));
        content.Children.Add(buttons); popup.Content = content;
        Button? trigger = null;
        trigger = Button("Reset statistics…", "logs-overview-reset", () =>
        {
            // Capture the scope when opening the popup so confirmation cannot
            // silently widen it if a background refresh changes the detail view.
            character = _drafts.DetailCharacter;
            explanation.Text = character is null ? L("Resets the selected counters for all characters.")
                : F($"Resets the selected counters for {character} only.");
            popup.ShowAt(trigger!); return Task.CompletedTask;
        });
        trigger.HorizontalAlignment = HorizontalAlignment.Left;
        Avalonia.Controls.Primitives.FlyoutBase.SetAttachedFlyout(trigger, popup);
        return trigger;
    }

    private sealed record OverviewRow(Button Button, TextBlock State);
    private readonly Dictionary<string, OverviewRow> _overviewRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> _detailValues = new();
    private readonly Dictionary<string, TextBlock> _combinedValues = new();
    private readonly Dictionary<string, (long Id, Bitmap Image)> _overviewPortraits = new(StringComparer.OrdinalIgnoreCase);
    private string? _openDetail;
    private string _overviewNames = "";
    private StackPanel? _detailHistory;
    private string _detailHistoryKey = "";

    private void RefreshOverview(CombatLogSnapshot data)
    {
        var selected = data.Characters.FirstOrDefault(x => x.Name.Equals(_drafts.DetailCharacter, StringComparison.OrdinalIgnoreCase));
        string names = string.Join('\n', data.Characters.Select(x => x.Name + ":" + x.CharacterId));
        if (_openDetail != selected?.Name || _overviewNames != names || _stats.Children.Count == 0)
        {
            _openDetail = selected?.Name; _drafts.DetailCharacter = _openDetail; _overviewNames = names;
            _stats.Children.Clear(); _damageNames.Clear(); _overviewRows.Clear(); _detailValues.Clear(); _combinedValues.Clear(); _detailHistoryKey = "";
            if (selected is not null) BuildCharacterDetail(selected);
            else
            {
                BuildCombinedStatistics();
                if (data.Characters.Count == 0) _stats.Children.Add(Card(Text("No character logs yet. Check Data setup and log in to EVE.", 14)));
                foreach (var character in data.Characters) AddOverviewRow(character);
            }
        }
        var online = _backend.Read().Clients.Select(x => x.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var character in data.Characters)
        {
            if (!_overviewRows.TryGetValue(character.Name, out var row)) continue;
            double incoming = character.Categories.Sum(x => x.Dps.Incoming), outgoing = character.Categories.Sum(x => x.Dps.Outgoing);
            string state = L(incoming > 0 || outgoing > 0 ? "In combat" : online.Contains("EVE - " + character.Name) ? "Online" : "Offline");
            row.State.Text = string.Join(" · ", new[] { character.SolarSystem, state,
                F($"In {incoming:N0} / Out {outgoing:N0} DPS"), F($"{character.Activity?.SystemChanges ?? 0:N0} jumps") }.Where(x => !string.IsNullOrEmpty(x)));
            row.Button.ToolTipSet(row.State.Text);
            AutomationProperties.SetName(row.Button, character.Name + " · " + row.State.Text);
        }
        if (selected is not null) RefreshCharacterDetail(selected, data);
        else RefreshCombinedStatistics(data);
    }

    private void BuildCombinedStatistics()
    {
        var content = new StackPanel { Spacing = 6, Name = "logs-combined-statistics" };
        content.Children.Add(Text("Combined statistics", 16, true));
        var grid = new Grid { ColumnDefinitions = new("*,*,*"), RowDefinitions = new("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"), ColumnSpacing = 8 };
        Cell(grid, 1, 0, "Incoming", true); Cell(grid, 2, 0, "Outgoing", true);
        string[] labels = ["Damage", "Average DPS", "Hits", "Largest hit", "Shield", "Armour", "Hull"];
        for (int row = 0; row < labels.Length; row++)
        {
            Cell(grid, 0, row + 1, labels[row]);
            for (int col = 1; col <= 2; col++)
            {
                var value = SingleLine("-", 13); value.Margin = new(0, 4); value.Name = $"logs-combined-{row}-{col}";
                if (row == 1) value.ToolTipSet(L("Average DPS is weighted by valid sample time across characters. Idle time and the first and last 10 seconds of combat are excluded."));
                Grid.SetColumn(value, col); Grid.SetRow(value, row + 1); grid.Children.Add(value);
                _combinedValues[$"{row}-{col}"] = value;
            }
        }
        content.Children.Add(grid);
        var jumps = Text("", 12); jumps.Name = "logs-combined-jumps"; _combinedValues["jumps"] = jumps; content.Children.Add(jumps);
        _stats.Children.Add(Card(content));
    }

    private void RefreshCombinedStatistics(CombatLogSnapshot data)
    {
        var categories = data.Characters.SelectMany(x => x.Categories).ToArray();
        var activities = data.Characters.Where(x => x.Activity is not null).Select(x => x.Activity!).ToArray();
        var combat = activities.SelectMany(x => x.Combat).ToArray();
        for (int col = 1; col <= 2; col++)
        {
            double Direction(DamageFigures value) => col == 1 ? value.Incoming : value.Outgoing;
            double seconds = combat.Sum(x => Direction(x.DpsSampleSeconds));
            _combinedValues[$"0-{col}"].Text = categories.Sum(x => Direction(x.Total)).ToString("N0");
            _combinedValues[$"1-{col}"].Text = seconds > 0 ? (combat.Sum(x => Direction(x.AverageDps) * Direction(x.DpsSampleSeconds)) / seconds).ToString("N1") : "-";
            _combinedValues[$"2-{col}"].Text = combat.Sum(x => Direction(x.Hits)).ToString("N0");
            _combinedValues[$"3-{col}"].Text = combat.Select(x => Direction(x.LargestHit)).DefaultIfEmpty().Max().ToString("N0");
            for (int row = 4; row <= 6; row++)
            {
                var repairs = activities.SelectMany(x => x.Repairs).Where(x => (int)x.Effect == row - 3).ToArray();
                _combinedValues[$"{row}-{col}"].Text = F($"{repairs.Sum(x => Direction(x.Total)):N0} HP ({repairs.Sum(x => Direction(x.Count)):N0} cycles)");
            }
        }
        _combinedValues["jumps"].Text = F($"System jumps: {activities.Sum(x => x.SystemChanges):N0}");
    }

    private TextBlock SingleLine(string text, int size, bool bold = false)
    {
        var control = RawText(text, size, bold); control.TextWrapping = TextWrapping.NoWrap;
        control.TextTrimming = TextTrimming.CharacterEllipsis; control.VerticalAlignment = VerticalAlignment.Center;
        return control;
    }

    private void AddOverviewRow(CharacterCombatSnapshot character)
    {
        var line = new Grid { ColumnDefinitions = new("32,2*,5*,18"), ColumnSpacing = 12 };
        line.Children.Add(OverviewPortrait(character, 32));
        var name = SingleLine(character.Name, 14, true); name.Name = "logs-character-name";
        Grid.SetColumn(name, 1); line.Children.Add(name); _damageNames.Add((character.Name, name));
        var state = SingleLine("", 12); state.Name = "logs-character-state"; state.Foreground = WorkspaceTheme.Brush(_theme.Muted);
        Grid.SetColumn(state, 2); line.Children.Add(state);
        var arrow = SingleLine("›", 20); Grid.SetColumn(arrow, 3); line.Children.Add(arrow);
        var button = new Button { Name = "logs-character-row", Content = line, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(10, 7), Background = WorkspaceTheme.Brush(_theme.Surface) };
        button.Click += (_, _) =>
        {
            _drafts.DetailCharacter = character.Name; RefreshData();
            _stats.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<Button>().FirstOrDefault()?.Focus();
        };
        _overviewRows[character.Name] = new(button, state); _stats.Children.Add(button);
    }

    private void BuildCharacterDetail(CharacterCombatSnapshot character)
    {
        var content = new StackPanel { Spacing = 14, Name = "logs-character-details" };
        content.Children.Add(Button("All characters", "logs-details-back", () =>
        {
            _drafts.DetailCharacter = null; RefreshData();
            if (_overviewRows.TryGetValue(character.Name, out var row)) row.Button.Focus();
            return Task.CompletedTask;
        }));
        var heading = new Grid { ColumnDefinitions = new("48,*"), ColumnSpacing = 12 };
        heading.Children.Add(OverviewPortrait(character, 48));
        var title = new StackPanel { Spacing = 3 };
        var name = SingleLine(character.Name, 19, true); name.Name = "logs-character-name";
        title.Children.Add(name); _damageNames.Add((character.Name, name));
        var system = SingleLine("", 13); title.Children.Add(system); _detailValues["system"] = system;
        Grid.SetColumn(title, 1); heading.Children.Add(title); content.Children.Add(heading);
        var since = Text("", 12); _detailValues["since"] = since; content.Children.Add(since);
        AddDetailTable(content, "Damage", "damage", ["Target", "In total", "Out total", "Avg. in DPS", "Avg. out DPS"], ["NPC", "Player"]);
        AddDetailTable(content, "Hits", "hits", ["Target", "Hits received", "Hits dealt", "Largest received", "Largest dealt"], ["NPC", "Player"]);
        AddDetailTable(content, "Distinct targets", "targets", ["", "Hit", "Attacked by"], ["NPC types", "Players"]);
        var travel = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 12 };
        foreach (var (key, label, index) in new[] { ("jumps", "System jumps", 0), ("systems", "Unique systems", 1) })
        {
            var block = new StackPanel { Spacing = 5 }; block.Children.Add(Text(label, 12));
            var value = RawText("", 20, true); value.Name = "logs-detail-" + key; _detailValues[key] = value; block.Children.Add(value);
            var card = Card(block); Grid.SetColumn(card, index); travel.Children.Add(card);
        }
        content.Children.Add(travel);
        AddDetailTable(content, "Repairs", "repairs", ["", "Incoming", "Outgoing"], ["Shield", "Armour", "Hull"]);
        _detailHistory = new StackPanel { Spacing = 8 };
        content.Children.Add(new Expander { Name = "logs-detail-history", Header = L("Log details"), Content = _detailHistory,
            HorizontalAlignment = HorizontalAlignment.Stretch });
        _stats.Children.Add(content);
    }

    private void AddDetailTable(StackPanel content, string heading, string key, string[] headers, string[] rows)
    {
        var block = new StackPanel { Spacing = 6 }; block.Children.Add(Text(heading, 15, true));
        var grid = new Grid { ColumnDefinitions = new(string.Join(',', headers.Select(_ => "*"))),
            RowDefinitions = new(string.Join(',', Enumerable.Repeat("Auto", rows.Length + 1))), ColumnSpacing = 8 };
        for (int column = 0; column < headers.Length; column++) Cell(grid, column, 0, headers[column], true);
        for (int row = 0; row < rows.Length; row++)
        {
            Cell(grid, 0, row + 1, rows[row]);
            for (int column = 1; column < headers.Length; column++)
            {
                var value = SingleLine("0", 13); value.Margin = new(0, 5); value.Name = $"logs-detail-{key}-{row}-{column}";
                Grid.SetColumn(value, column); Grid.SetRow(value, row + 1); grid.Children.Add(value);
                _detailValues[$"{key}-{row}-{column}"] = value;
            }
        }
        block.Children.Add(grid); content.Children.Add(Card(block));
    }

    private void RefreshCharacterDetail(CharacterCombatSnapshot character, CombatLogSnapshot data)
    {
        void Put(string key, double value, string format = "N0") => _detailValues[key].Text = value.ToString(format);
        _detailValues["system"].Text = character.SolarSystem ?? "-";
        var activity = character.Activity;
        _detailValues["since"].Text = F($"Activity since {(activity?.Since ?? data.Since).ToLocalTime():g}");
        string averageHelp = F($"Average DPS samples since {(activity?.AverageDpsSince ?? activity?.Since ?? data.Since).ToLocalTime():g}. Idle time and the first and last 10 seconds of combat are excluded.");
        for (int row = 0; row < 2; row++)
        {
            var kind = row == 0 ? CombatantKind.Npc : CombatantKind.Player;
            var damage = character.Categories.FirstOrDefault(x => x.Kind == kind);
            var combat = activity?.Combat.FirstOrDefault(x => x.Kind == kind);
            double[] totals = [damage?.Total.Incoming ?? 0, damage?.Total.Outgoing ?? 0, combat?.AverageDps.Incoming ?? 0, combat?.AverageDps.Outgoing ?? 0];
            double[] hits = [combat?.Hits.Incoming ?? 0, combat?.Hits.Outgoing ?? 0, combat?.LargestHit.Incoming ?? 0, combat?.LargestHit.Outgoing ?? 0];
            for (int col = 1; col <= 4; col++) { Put($"damage-{row}-{col}", totals[col - 1], col >= 3 ? "N1" : "N0"); Put($"hits-{row}-{col}", hits[col - 1]); }
            for (int col = 3; col <= 4; col++)
            {
                var value = _detailValues[$"damage-{row}-{col}"];
                if ((col == 3 ? combat?.DpsSampleSeconds.Incoming : combat?.DpsSampleSeconds.Outgoing) is not > 0) value.Text = "-";
                value.ToolTipSet(averageHelp);
            }
            Put($"targets-{row}-1", combat?.UniqueTargets ?? 0); Put($"targets-{row}-2", combat?.UniqueAttackers ?? 0);
        }
        Put("jumps", activity?.SystemChanges ?? 0); Put("systems", activity?.UniqueSystems ?? 0);
        for (int row = 0; row < 3; row++)
        {
            var repairs = activity?.Repairs.FirstOrDefault(x => x.Effect == (CombatEffect)(row + 1));
            _detailValues[$"repairs-{row}-1"].Text = F($"{repairs?.Total.Incoming ?? 0:N0} HP ({repairs?.Count.Incoming ?? 0:N0} cycles)");
            _detailValues[$"repairs-{row}-2"].Text = F($"{repairs?.Total.Outgoing ?? 0:N0} HP ({repairs?.Count.Outgoing ?? 0:N0} cycles)");
        }
        var entries = data.RecentEntries.Where(x => x.Character.Equals(character.Name, StringComparison.OrdinalIgnoreCase)).Take(40).ToArray();
        string key = System.Text.Json.JsonSerializer.Serialize(new { entries, character.LastLogAt, character.LocationObservedAt, character.GameFiles, character.LocalFiles });
        if (_detailHistory is null || key == _detailHistoryKey) return;
        _detailHistoryKey = key; _detailHistory.Children.Clear();
        _detailHistory.Children.Add(RawText(F($"{character.Name} · {character.GameFiles} game logs · {character.LocalFiles} Local logs\nLast entry: {character.LastLogAt?.ToLocalTime().ToString("g") ?? "-"} · System observed: {character.LocationObservedAt?.ToLocalTime().ToString("g") ?? "-"}"), 12));
        foreach (var entry in entries) _detailHistory.Children.Add(RawText($"{entry.Timestamp.ToLocalTime():HH:mm:ss} · {entry.Category}\n{entry.Text}", 12));
    }

    private Control OverviewPortrait(CharacterCombatSnapshot character, int size)
    {
        var fallback = SingleLine(string.Join("", character.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[..1])), 12, true);
        fallback.HorizontalAlignment = HorizontalAlignment.Center;
        var image = new Image { Stretch = Stretch.UniformToFill, IsVisible = false };
        var border = new Border { Name = "logs-character-portrait", Width = size, Height = size, CornerRadius = new(5), ClipToBounds = true,
            Background = WorkspaceTheme.Brush(_theme.AccentSurface), Child = new Grid { Children = { fallback, image } } };
        AutomationProperties.SetName(border, F($"Portrait of {character.Name}"));
        bool attached = false;
        border.DetachedFromVisualTree += (_, _) => attached = false;
        border.AttachedToVisualTree += async (_, _) =>
        {
            attached = true;
            try
            {
                long? id = character.CharacterId;
                if (id is null && _backend is IWorkspaceCharacterProvider identities) id = (await identities.GetCharacterAsync("EVE - " + character.Name))?.CharacterId;
                if (_disposed || !attached || id is null || _backend is not IWorkspacePortraitProvider portraits) return;
                if (!_overviewPortraits.TryGetValue(character.Name, out var cached) || cached.Id != id)
                {
                    var bytes = await portraits.GetCharacterPortraitAsync(id.Value);
                    if (_disposed || !attached || bytes is null) return;
                    // A module holds one current portrait per character, disposed with it.
                    var bitmap = new Bitmap(new MemoryStream(bytes, false));
                    cached.Image?.Dispose(); cached = (id.Value, bitmap); _overviewPortraits[character.Name] = cached;
                }
                image.Source = cached.Image; image.IsVisible = true; fallback.IsVisible = false;
            }
            catch { /* Initials remain when public identity or portrait lookup is unavailable. */ }
        };
        return border;
    }

    private void DisposeOverviewPortraits()
    { foreach (var portrait in _overviewPortraits.Values) portrait.Image.Dispose(); _overviewPortraits.Clear(); }
}
