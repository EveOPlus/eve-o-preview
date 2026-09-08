using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private bool _showGlobalShortcuts;

    private void AddGlobalHotkeys()
    {
        var shortcuts = new StackPanel { Spacing = 12, Children = { Text("GLOBAL SHORTCUTS", 10, _theme.Accent, true), Text("Use Record, then press a key combination. Escape cancels capture. Clear removes the binding.", 11, _theme.Muted), HotkeyRow("Show / hide all previews", "ToggleHideAllActiveHotkey", _snapshot.Settings.GetValueOrDefault("ToggleHideAllActiveHotkey", "")), HotkeyRow("Minimize all clients", "MinimizeAllClientsHotkey", _snapshot.Settings.GetValueOrDefault("MinimizeAllClientsHotkey", "")) } };
        _page.Children.Add(Card(shortcuts));
    }

    private Control HotkeyRow(string label, string target, string binding)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 12, 0), Children = { Text(label, 12, _theme.Text, true), RawText(string.IsNullOrEmpty(binding) || binding == "None" ? L("Not assigned") : binding, 12, _theme.Accent) } });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var record = CommandButton("Record", new("hotkey-capture", target), "record-" + target, true);
        var clear = CommandButton("Clear", new("hotkey-clear", target), "clear-" + target);
        clear.IsEnabled = !string.IsNullOrWhiteSpace(binding) && binding != "None";
        AutomationProperties.SetName(record, F($"Record shortcut for {L(label)}"));
        AutomationProperties.SetName(clear, F($"Clear shortcut for {L(label)}"));
        buttons.Children.Add(record); buttons.Children.Add(clear);
        Grid.SetColumn(buttons, 1); row.Children.Add(buttons);
        return row;
    }

    private void RenderSwitching()
    {
        _orderNarrow = Bounds.Width < 1120;
        Heading(_theme.Legacy ? "Cycle Groups" : "Switching & hotkeys", "Build a character order, then move through it with a shortcut.");
        if (!_theme.Legacy)
        {
            var sections = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sections.Children.Add(ActionButton("Cycle groups", () => { _showGlobalShortcuts = false; RenderPage(false); }, "switching-groups", !_showGlobalShortcuts));
            sections.Children.Add(ActionButton("Global shortcuts", () => { _showGlobalShortcuts = true; RenderPage(false); }, "switching-global", _showGlobalShortcuts));
            _page.Children.Add(sections);
            if (_showGlobalShortcuts) { AddGlobalHotkeys(); return; }
        }
        var groupBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        groupBar.Children.Add(Text("CYCLE GROUPS", 10, _theme.Accent, true));
        var add = ActionButton("+ New group", async () =>
        {
            var oldCount = _snapshot.CycleGroups.Count;
            var result = await Run(new("group-add"));
            if (result.Success && _snapshot.CycleGroups.Count > oldCount) { _selectedGroup = _snapshot.CycleGroups.Last().Id; RenderPage(false); }
        }, "add-cycle-group", true);
        Grid.SetColumn(add, 1); groupBar.Children.Add(add);
        _page.Children.Add(groupBar);
        if (_snapshot.CycleGroups.Count == 0)
        {
            _page.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Text("Your next character, one shortcut away", 20, _theme.Text, true), Text("Create a group, add characters in the order you fly them, and record a forward or backward shortcut. A group with one character becomes a direct character hotkey.", 13, _theme.Muted) } }));
            return;
        }
        if (!_snapshot.CycleGroups.Any(g => g.Id == _selectedGroup)) _selectedGroup = _snapshot.CycleGroups[0].Id;
        var picker = new ComboBox { Name = "cycle-group-picker", ItemsSource = _snapshot.CycleGroups.Select(g => new ComboBoxItem { Content = F($"{g.Name}   ·   Characters: {g.Clients.Count}"), Tag = g.Id }).ToArray(), HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 39 };
        picker.SelectedIndex = _snapshot.CycleGroups.ToList().FindIndex(g => g.Id == _selectedGroup);
        AutomationProperties.SetName(picker, L("Cycle group to edit"));
        picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is ComboBoxItem item && item.Tag is int id) { _selectedGroup = id; RenderPage(true); } };
        _page.Children.Add(picker);
        var group = _snapshot.CycleGroups.First(g => g.Id == _selectedGroup);
        var idText = group.Id.ToString();
        var groupDraftKey = "group-name-" + group.Name;
        var offlineDraftKey = "offline-client-" + group.Name;
        var settings = new StackPanel { Spacing = 9 };
        var nameRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        var name = FormInput(groupDraftKey, "Cycle group name", group.Name);
        nameRow.Children.Add(name);
        var rename = ActionButton("Rename", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { _message = "Enter a cycle group name."; _messageError = true; UpdateFooter(); return; }
            var result = await Run(new("group-rename", idText, name.Text));
            if (result.Success) { _formDrafts.Remove(groupDraftKey); UpdateFooter(); }
        }, "rename-cycle-group");
        rename.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(rename, 1); nameRow.Children.Add(rename);
        var delete = ActionButton("Delete", () => Confirm(F($"Delete {group.Name}?"), "This removes the group, its order and its shortcuts. It does not close any EVE clients.", "Delete group", async () => { var result = await Run(new("group-delete", idText)); if (result.Success) { _formDrafts.Remove(groupDraftKey); _formDrafts.Remove(offlineDraftKey); } _selectedGroup = null; RenderPage(false); }), "delete-cycle-group");
        delete.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(delete, 2); nameRow.Children.Add(delete);
        _page.Children.Add(nameRow);
        settings.Children.Add(OrderHeader(group));
        settings.Children.Add(OrderList(group, false));
        settings.Children.Add(new Border { Height = 1, Background = B(_theme.Border) });
        settings.Children.Add(Text("ADD A CHARACTER", 10, _theme.Muted, true));
        var available = _snapshot.Clients.Where(c => !group.Clients.Contains(c.Title)).ToArray();
        if (available.Length > 0)
        {
            var runningRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var running = new ComboBox { Name = "add-running-client-picker", ItemsSource = available.Select(c => c.Title).ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 36, FontSize = 12 };
            AutomationProperties.SetName(running, L("Running client to add"));
            runningRow.Children.Add(running);
            var addRunning = ActionButton("Add running", async () => { if (running.SelectedItem is string title) await Run(new("group-client-add", idText, title)); }, "add-running-client", true);
            addRunning.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(addRunning, 1); runningRow.Children.Add(addRunning);
            settings.Children.Add(runningRow);
        }
        var offlineRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var offline = FormInput(offlineDraftKey, "Character name or full EVE - Name title");
        offlineRow.Children.Add(offline);
        var addOffline = ActionButton("Add character", async () =>
        {
            if (string.IsNullOrWhiteSpace(offline.Text)) { _message = "Enter a character name first."; _messageError = true; UpdateFooter(); return; }
            var result = await Run(new("group-client-add", idText, offline.Text));
            if (result.Success) { _formDrafts.Remove(offlineDraftKey); RenderPage(true); }
        }, "add-offline-client", true);
        addOffline.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(addOffline, 1); offlineRow.Children.Add(addOffline);
        settings.Children.Add(offlineRow);
        settings.Children.Add(Text("Offline characters stay in the order and are available when their client returns.", 11, _theme.Muted));
        var shortcuts = new StackPanel { Spacing = 10, Children = { Text("GROUP SHORTCUTS", 10, _theme.Accent, true) } };
        for (var i = 0; i < 2; i++) shortcuts.Children.Add(HotkeyRow("Cycle forward · shortcut " + (i + 1), $"group:{group.Id}:forward:{i}", group.ForwardHotkeys.ElementAtOrDefault(i) ?? ""));
        for (var i = 0; i < 2; i++) shortcuts.Children.Add(HotkeyRow("Cycle backward · shortcut " + (i + 1), $"group:{group.Id}:backward:{i}", group.BackwardHotkeys.ElementAtOrDefault(i) ?? ""));
        if (group.ForwardHotkeys.Count > 2 || group.BackwardHotkeys.Count > 2)
            shortcuts.Children.Add(Text(F($"Additional shortcuts from your existing profile are preserved: {string.Join(", ", group.ForwardHotkeys.Skip(2).Concat(group.BackwardHotkeys.Skip(2)).Where(s => !string.IsNullOrWhiteSpace(s)))}"), 11, _theme.Muted));
        if (_orderNarrow)
        {
            _page.Children.Add(Card(settings));
            _page.Children.Add(Card(shortcuts));
            return;
        }
        var groupEditor = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var membersCard = Card(settings); membersCard.Margin = new Thickness(0, 0, 6, 0);
        var shortcutsCard = Card(shortcuts); shortcutsCard.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(shortcutsCard, 1);
        groupEditor.Children.Add(membersCard); groupEditor.Children.Add(shortcutsCard);
        _page.Children.Add(groupEditor);
    }

    private void RenderProfiles()
    {
        Heading("Profiles", "Keep separate setups for the way you fly. The active profile owns your settings, layouts and shortcuts.");
        var current = _snapshot.Profiles.FirstOrDefault(p => p.Name == _snapshot.ProfileName);
        var identity = new StackPanel { Spacing = 8, Children = { Text(current?.IsDefault == true ? "Duplicate Default to create a profile you can rename or delete." : "Manage the profile selected above. Changes you apply are saved to it.", 11, _theme.Muted) } };
        _page.Children.Add(Card(identity));
        var manage = new StackPanel { Spacing = 9 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(CommandButton("Duplicate", new("profile-clone"), "clone-profile", true));
        var nameRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var name = FormInput("profile-name", "Profile name", _snapshot.ProfileName);
        name.IsEnabled = current?.IsDefault != true;
        nameRow.Children.Add(name);
        var rename = ActionButton("Rename", async () =>
        {
            var result = await Run(new("profile-rename", Value: name.Text ?? ""));
            if (result.Success) { _formDrafts.Remove("profile-name"); UpdateFooter(); }
        }, "rename-profile");
        rename.IsEnabled = current?.IsDefault != true;
        rename.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(rename, 1); nameRow.Children.Add(rename);
        manage.Children.Add(nameRow);
        var delete = ActionButton("Delete current profile…", () => Confirm(F($"Delete {_snapshot.ProfileName}?"), "This permanently deletes this profile's saved settings and layouts. The Default profile will become active. Any unapplied edits will be discarded.", "Delete profile", async () => { var result = await Run(new("profile-delete")); if (result.Success) { _drafts.Clear(); _formDrafts.Clear(); } }), "delete-profile");
        delete.IsEnabled = current?.IsDefault != true;
        actions.Children.Add(delete);
        manage.Children.Add(actions);
        identity.Children.Add(manage);
        _page.Children.Add(Text("PROFILE IDENTITY", 10, _theme.Accent, true));
        var swatches = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var pair in new[] { ("#7C9CFF", "Blue"), ("#B793EF", "Violet"), ("#64C5AA", "Green"), ("#E7B96F", "Amber"), ("#E58EA0", "Rose"), ("#74C5DA", "Cyan") })
        {
            var color = pair.Item1;
            var swatch = CommandButton(pair.Item2, new("setting", "ProfileAccentColor", color), "accent-" + pair.Item2);
            swatch.BorderBrush = B(color); swatch.BorderThickness = new Thickness(0, 0, 0, 4); swatch.Margin = new Thickness(0, 0, 6, 6);
            swatches.Children.Add(swatch);
        }
        swatches.Children.Add(CommandButton("Theme default", new("setting", "ProfileAccentColor", ""), "accent-default"));
        _page.Children.Add(swatches);
        AddSettings("Profiles");
        _page.Children.Add(Text("AVAILABLE PROFILES", 10, _theme.Accent, true));
        var availableProfiles = new StackPanel { Spacing = 5 };
        foreach (var profile in _snapshot.Profiles)
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            line.Children.Add(RawText(profile.Name + (profile.IsDefault ? L(" · protected") : ""), 13, _theme.Text, true));
            var select = ActionButton(profile.Name == _snapshot.ProfileName ? "Loaded" : "Load", async () =>
            {
                if (HasUnappliedEdits) Confirm("Switch profile?", "Your unapplied edits will be discarded. Applied settings are already saved.", "Discard edits & switch", async () => { _drafts.Clear(); _formDrafts.Clear(); await Run(new("profile-switch", profile.Id)); });
                else await Run(new("profile-switch", profile.Id));
            }, "load-profile-" + profile.Name);
            select.IsEnabled = profile.Name != _snapshot.ProfileName;
            Grid.SetColumn(select, 1); line.Children.Add(select);
            availableProfiles.Children.Add(Card(line, new Thickness(10, 7)));
        }
        _page.Children.Add(new ScrollViewer { Content = availableProfiles, MaxHeight = 220, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
    }
}
