using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private void RenderOverview()
    {
        Heading("Workspace", _snapshot.Clients.Count == 0 ? "Launch EVE to begin. You can configure previews and shortcuts now." : "Your previews, shortcuts and client performance at a glance.");
        _page.Children.Add(SupportAboutCard());
        var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        AddStat(stats, 0, "PREVIEWS", _snapshot.AllPreviewsHidden ? "Hidden" : _snapshot.Clients.Count(c => c.PreviewVisible).ToString(), _snapshot.AllPreviewsHidden ? "Show all from the header" : "Enabled for this profile");
        AddStat(stats, 1, "CYCLE GROUPS", _snapshot.CycleGroups.Count.ToString(), "Your character sequences");
        AddStat(stats, 2, "FRAME LIMITER", _snapshot.Settings.GetValueOrDefault("FpsEnabled", "False").Equals("True", StringComparison.OrdinalIgnoreCase) ? "On" : "Off", "Separate limits by client role");
        _page.Children.Add(stats);
        _page.Children.Add(FeatureLink("clients", "Clients", "Show or hide each character's preview.", "Clients"));
        _page.Children.Add(FeatureLink("preview", "Previews, titles & highlights", "Adjust appearance while keeping the preview in view.", "Previews"));
        _page.Children.Add(FeatureLink("cycle", "Cycle groups & shortcuts", "Edit character order and record switching hotkeys.", "Switching"));
        _page.Children.Add(FeatureLink("performance", "Performance & audio", "Control frame rates, processor affinity and selective muting.", "FpsAudio"));
    }

    private void AddStat(Grid grid, int column, string title, string value, string detail)
    {
        var card = Card(new StackPanel { Spacing = 5, Children = { Text(title, 9, _theme.Muted, true), Text(value, 22, _theme.Text, true), Text(detail, 10, _theme.Muted) } }, new Thickness(12));
        card.Margin = new Thickness(column == 0 ? 0 : 6, 0, column == 2 ? 0 : 6, 0);
        Grid.SetColumn(card, column); grid.Children.Add(card);
    }

    private Control FeatureLink(string icon, string title, string detail, string destination)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*,Auto") };
        grid.Children.Add(Icon(icon));
        var description = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 12, 0), Children = { Text(title, 13, _theme.Text, true), Text(detail, 11, _theme.Muted) } };
        Grid.SetColumn(description, 1); grid.Children.Add(description);
        var open = ActionButton("Open →", () => Navigate(destination));
        Grid.SetColumn(open, 2); grid.Children.Add(open);
        return Card(grid, new Thickness(13));
    }

    private void RenderPreviewSettings()
    {
        BuildPreviewWorkspace();
    }

    private void RenderPerformance()
    {
        Heading(_theme.Legacy ? "FPS / Audio" : "Performance & audio", "Keep your active character responsive and background clients quiet.");
        _page.Children.Add(Text("FRAME RATES & PROCESSOR", 10, _theme.Accent, true));
        AddSettings("FpsAudio", s => !s.Key.StartsWith("Audio", StringComparison.Ordinal));
        _page.Children.Add(Text("SELECTIVE AUDIO MUTING", 10, _theme.Accent, true));
        AddSettings("FpsAudio", s => s.Key.StartsWith("Audio", StringComparison.Ordinal));
    }

    private void RenderClients()
    {
        Heading(_theme.Legacy ? "Active Clients" : "Clients", "Choose which characters have a preview. Visibility is saved in this profile.");
        _page.Children.Add(ActionButton("Character colors & minimization", () => Navigate("ClientSettings"), "open-client-settings"));
        if (_theme.Legacy) AddGlobalHotkeys();
        if (_snapshot.AllPreviewsHidden)
            _page.Children.Add(Card(new StackPanel { Spacing = 8, Children = { Text("All previews are temporarily hidden", 15, _theme.Text, true), Text("Individual switches below remain saved. Use Show all previews in the header to reveal enabled previews.", 12, _theme.Muted) } }));
        if (_snapshot.Clients.Count == 0)
        {
            _page.Children.Add(Card(new StackPanel { Spacing = 12, Margin = new Thickness(12, 28), Children = { Icon("clients"), Text("Your characters will appear here", 22, _theme.Text, true), Text("Open an EVE client and sign in to a character. EVE-O detects running clients automatically; no manual connection is needed.", 13, _theme.Muted), ActionButton("Configure previews while you wait", () => Navigate(_theme.Legacy ? "Thumbnail" : "Previews"), primary: true) } }));
            return;
        }
        var rows = new StackPanel { Spacing = 12 };
        foreach (var client in _snapshot.Clients)
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("42,*,Auto") };
            line.Children.Add(CharacterPortrait(client.Title, 30));
            var details = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 12, 0), Children = { RawText(client.Title, 14, _theme.Text, true), Text(_snapshot.AllPreviewsHidden ? "Detected · globally hidden" : client.PreviewVisible ? "Detected · preview enabled" : "Detected · preview hidden", 11, _theme.Muted) } };
            Grid.SetColumn(details, 1); line.Children.Add(details);
            var toggle = new ToggleSwitch { IsChecked = client.PreviewVisible, OnContent = L("Visible"), OffContent = L("Hidden"), Name = "client-" + client.Title };
            Avalonia.Automation.AutomationProperties.SetName(toggle, F($"Preview visible for {client.Title}"));
            toggle.IsCheckedChanged += async (_, _) => await Run(new("client-visible", client.Title, (toggle.IsChecked == true).ToString()));
            Grid.SetColumn(toggle, 2); line.Children.Add(toggle);
            rows.Children.Add(Card(line));
        }
        _page.Children.Add(rows);
    }

    private static string ClientInitial(string title)
    {
        var display = title.StartsWith("EVE - ", StringComparison.Ordinal) ? title[6..] : title;
        return display.Length > 0 ? display[..1].ToUpperInvariant() : "E";
    }

    private void RenderAppearance()
    {
        Heading("Make yourself at home.", "Choose the look that works for you. Your theme applies to every profile.", "APPEARANCE");
        AddLanguageSettings();
        _page.Children.Add(ActionButton("Thumbnail right-click menu…", () => Navigate("ThumbnailMenu"), "thumbnail-menu-settings"));
        foreach (var name in new[] { "Light", "Dark", "Legacy" })
        {
            var palette = WorkspaceTheme.Get(name);
            var selected = _theme.Name == name;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("128,*,Auto") };
            var miniature = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*"), Width = 108, Height = 76, Background = B(palette.Background), ClipToBounds = true };
            miniature.Children.Add(new Border { Background = B(palette.Sidebar), BorderBrush = B(palette.Border), BorderThickness = new Thickness(0, 0, 1, 0), Child = new StackPanel { Margin = new Thickness(4, 8), Spacing = 5, Children = { new Border { Height = 5, Background = B(palette.Accent), CornerRadius = new CornerRadius(2) }, new Border { Height = 5, Background = B(palette.Border) }, new Border { Height = 5, Background = B(palette.Border) } } } });
            var miniContent = new StackPanel { Margin = new Thickness(7, 9), Spacing = 7, Children = { new Border { Height = 5, Width = 37, Background = B(palette.Text), HorizontalAlignment = HorizontalAlignment.Left }, new Border { Height = 21, Background = B(palette.Surface), BorderBrush = B(palette.Border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(palette.Radius / 2) }, new Border { Height = 12, Background = B(palette.Surface), BorderBrush = B(palette.Border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(palette.Radius / 2) } } };
            Grid.SetColumn(miniContent, 1); miniature.Children.Add(miniContent);
            grid.Children.Add(new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = miniature, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center });
            var explanation = name switch { "Light" => "Bright surfaces and crisp contrast for daylight sessions.", "Dark" => "Quiet, deep surfaces that are comfortable alongside EVE.", _ => L("Classic layout.") + " " + L(LegacyThemeDescription) };
            var text = new StackPanel { Spacing = 7, Margin = new Thickness(0, 0, 15, 0), Children = { Text(name, 18, _theme.Text, true), Text(explanation, 12, _theme.Muted) } };
            Grid.SetColumn(text, 1); grid.Children.Add(text);
            var select = CommandButton(selected ? L("Selected") : F($"Use {L(name)}"), new("theme", Value: name), "theme-" + name, true);
            select.IsEnabled = !selected;
            Grid.SetColumn(select, 2); grid.Children.Add(select);
            var card = Card(grid);
            if (selected) card.BorderBrush = B(_theme.Accent);
            _page.Children.Add(card);
        }
        _page.Children.Add(Card(new StackPanel { Spacing = 8, Children = { Text("Recognize your active profile", 15, _theme.Text, true), Text("Give each profile its own accent color so you can tell at a glance which one is loaded. Your chosen Light, Dark or Legacy theme still applies to every profile.", 12, _theme.Muted), ActionButton("Customize this profile’s color →", () => Navigate("Profiles"), "appearance-profile-accent") } }));
    }

    private void RenderPlanned(bool dps)
    {
        Heading(dps ? "DPS overviews" : "Your characters, connected.", dps ? "A future home for understanding combat across your characters." : "A future home for character identity and EVE ESI connections.", "PLANNED MODULE");
        _page.Children.Add(Card(new StackPanel { Spacing = 17, Children = { Icon(dps ? "chart" : "character"), Text(dps ? "Combat context at a glance" : "One place for every pilot", 23, _theme.Text, true), Text(dps ? "This workspace reserves room for configurable damage overviews. No combat logs are being read and no DPS values are calculated yet." : "This workspace reserves room for signing individual characters into EVE ESI. Account connection and token storage are not implemented yet.", 13, _theme.Muted), new Border { Height = 1, Background = B(_theme.Border) }, Text("DESIGNED TO GROW", 10, _theme.Accent, true), Text(dps ? "• Configure the characters and encounters in an overview\n• Choose useful metrics, time windows and presentation\n• Reuse your profile and appearance preferences" : "• Review each character's connection status\n• Make requested ESI permissions clear before authorization\n• Manage connected characters and revoke access", 13, _theme.Muted), Text("These are design directions for future work, not available controls or a release commitment.", 11, _theme.Muted), ActionButton(dps ? "Configure current overlays →" : "View running clients →", () => Navigate(dps ? (_theme.Legacy ? "Overlay" : "Previews") : "Clients")) } }));
    }

    private void RenderAbout()
    {
        Heading("EVE-O Preview", "A thoughtful workspace for flying more than one character.", "ABOUT");
        var heading = _page.Children[^1];
        _page.Children.Remove(heading);
        var aboutHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Margin = new Thickness(0, 0, 16, 0);
        aboutHeader.Children.Add(heading);
        var partner = PartnerBadge(190);
        Grid.SetColumn(partner, 1);
        aboutHeader.Children.Add(partner);
        _page.Children.Add(aboutHeader);
        _page.Children.Add(SupportAboutCard());
        var links = new WrapPanel { Orientation = Orientation.Horizontal };
        var documentation = CommandButton("Documentation \u2197", new("documentation"), "open-documentation", true);
        documentation.Margin = new Thickness(0, 0, 8, 4);
        links.Children.Add(documentation);
        links.Children.Add(CommandButton("Discord \u2197", new("discord"), "open-discord"));
        _page.Children.Add(Card(new StackPanel { Spacing = 13, Children = { Text(F($"Version {_snapshot.Version}"), 21, _theme.Text, true), Text("Live previews, flexible layouts, cycle groups, frame-rate controls and selective audio muting, brought together in one workspace.", 13, _theme.Muted), Text("Get help, share an idea, or just pop in and say hi to fellow pilots.", 12, _theme.Muted), links, Text("Available on Windows.", 12, _theme.Muted), Text("EVE-O Preview is distributed under the GNU General Public License, version 3 or later. EVE Online belongs to Fenris Creations (FC). EVE-O Preview is an independent project.", 11, _theme.Muted) } }));
        _page.Children.Add(Card(new StackPanel { Spacing = 10, Children = { Text("Project credits & license", 16, _theme.Text, true), Text("With thanks to original maintainer Phrynohyas Tig-Rah and the EVE-O Preview contributors. This software is provided without warranty; see the distributed GNU GPL license for the full terms.", 12, _theme.Muted), Text("Close this window to follow your profile's system-tray preference. Exit stops EVE-O Preview completely.", 12, _theme.Muted) } }));
    }
}
