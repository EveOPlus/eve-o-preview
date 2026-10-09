using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace EveOPreview.UI.Smoke;

internal static partial class Program
{
    private static int CheckRegions(string output)
    {
        int renders = 0;
        foreach (string theme in new[] { "Dark", "Light" })
        {
            var backend = new RegionBackend();
            backend.ExecuteAsync(new("theme", Value: theme)).GetAwaiter().GetResult();
            using var view = new WorkspaceView(backend, [RegionsView.CreateModule()]);
            var window = new Window { Width = 1100, Height = 800, Content = view };
            window.Show(); view.Navigate("Regions"); Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-regions-empty.png")); renders++;
            Click(view, "region-add"); Flush();
            Click(view, "region-add"); Flush();
            Require(backend.Items.Count == 2, "The region page must add multiple regions.");
            var portrait = FindControl<Border>(view, "region-monitor-0");
            var landscape = FindControl<Border>(view, "region-monitor-1");
            var flipped = FindControl<Border>(view, "region-monitor-2");
            Require(Math.Abs(portrait.Width / portrait.Height - 1080d / 1920) < .001
                && Math.Abs(landscape.Width / landscape.Height - 2560d / 1440) < .001,
                "Monitor map must preserve landscape and portrait aspect ratios.");
            Require(Math.Abs(Canvas.GetLeft(portrait) + portrait.Width - Canvas.GetLeft(landscape)) < .01
                && Canvas.GetTop(portrait) < Canvas.GetTop(landscape) && Canvas.GetLeft(flipped) > Canvas.GetLeft(landscape),
                "Monitor map must preserve negative desktop origins and display offsets.");
            Require(Canvas.GetLeft(FindControl<Border>(view, "region-monitor-top-0")) > Canvas.GetLeft(portrait) + portrait.Width / 2
                && Canvas.GetLeft(FindControl<Border>(view, "region-monitor-top-2")) < Canvas.GetLeft(flipped) + flipped.Width / 2,
                "Monitor orientation markers must distinguish the two portrait rotations.");
            backend.Assignments["EVE - Historical"] = backend.Items[0].Id;
            backend.Notify(); Flush();
            var picker = FindControl<ComboBox>(view, "region-picker");
            picker.SelectedIndex = 0; Flush();
            Require(!view.GetVisualDescendants().Any(control => control.Name is "region-x" or "region-y" or "region-width" or "region-height"),
                "Region geometry must be edited on screen, without numeric controls.");
            Require(!view.GetVisualDescendants().Any(control => control.Name is "region-client-EVE - Historical" or "region-client-notepad (99)"),
                "Saved titles and stale assignments must not enter the available client list.");
            var name = FindControl<TextBox>(view, "region-name");
            name.Text = "Fleet thumbnails"; Flush();
            Click(view, "region-map-" + backend.Items[1].Id); Flush();
            Require(((RegionItem)picker.SelectedItem!).Id == backend.Items[1].Id, "Map clicks must select the region for editing.");
            Click(view, "region-map-" + backend.Items[0].Id); Flush();
            Require(FindControl<TextBox>(view, "region-name").Text == "Fleet thumbnails", "Map selection must retain name drafts.");
            backend.Items[0] = backend.Items[0] with { X = -600, Width = 440 };
            backend.Notify(); Flush();
            Require(FindControl<TextBox>(view, "region-name").Text == "Fleet thumbnails" && view.HasUnappliedEdits,
                "Background updates must retain region drafts.");
            portrait = FindControl<Border>(view, "region-monitor-0");
            var regionButton = FindControl<Button>(view, "region-map-" + backend.Items[0].Id);
            double scale = portrait.Width / 1080;
            Require(Math.Abs(regionButton.Width - 440 * scale) < .01
                && Math.Abs(Canvas.GetLeft(regionButton) - Canvas.GetLeft(portrait) - 480 * scale) < .01,
                "Regions must use the same physical-pixel transform as the monitors, including negative origins.");
            view.Navigate("Clients"); view.Navigate("Regions"); Flush();
            Require(FindControl<TextBox>(view, "region-name").Text == "Fleet thumbnails", "Navigation must retain region drafts.");
            Click(view, "region-save"); Flush();
            Require(backend.Items[0].Name == "Fleet thumbnails" && !view.HasUnappliedEdits, "Saving must commit and clear only the selected region draft.");
            Require(backend.Items[0].X == -600 && backend.Items[0].Width == 440, "Saving a name must retain the latest overlay geometry.");
            var client = backend.Read().Clients[0].Title;
            FindControl<CheckBox>(view, "region-client-" + client).IsChecked = true; Flush();
            Require(backend.Assignments[client] == backend.Items[0].Id, "Client assignment must reach the backend.");
            FindControl<CheckBox>(view, "region-client-EVE - Offline group member").IsChecked = true; Flush();
            Require(backend.Assignments["EVE - Offline group member"] == backend.Items[0].Id,
                "Offline members of any cycle group must remain assignable.");
            FindControl<CheckBox>(view, "region-client-notepad (123)").IsChecked = true; Flush();
            Require(backend.Assignments["notepad (123)"] == backend.Items[0].Id, "A currently available other process can be assigned.");
            backend.OtherProcessAvailable = false; backend.Notify(); Flush();
            Require(!view.GetVisualDescendants().Any(control => control.Name == "region-client-notepad (123)")
                && backend.Assignments.ContainsKey("notepad (123)"), "Closed clients must leave the list without losing their assignments.");
            backend.OtherProcessAvailable = true; backend.Notify(); Flush();
            Require(FindControl<CheckBox>(view, "region-client-notepad (123)").IsChecked == true,
                "Returning clients must retain their saved assignment.");
            FindControl<CheckBox>(view, "region-edit").IsChecked = true; Flush();
            Require(backend.Editing, "Edit mode must reach the backend.");
            var regionsView = view.GetVisualDescendants().OfType<RegionsView>().Single();
            regionsView.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Flush();
            Require(!backend.Editing && FindControl<CheckBox>(view, "region-edit").IsChecked == false, "Escape must exit region editing.");
            FindControl<CheckBox>(view, "region-edit").IsChecked = true; Flush();
            FindControl<CheckBox>(view, "region-enabled").IsChecked = false; Flush();
            Require(!backend.Enabled && !backend.Editing && !FindControl<CheckBox>(view, "region-edit").IsEnabled,
                "Disabling regions must close editors and disable edit mode.");
            Require(backend.Items.Count == 2 && backend.Assignments.Count == 4, "Disabling regions must preserve templates and assignments.");
            FindControl<CheckBox>(view, "region-enabled").IsChecked = true; Flush();
            FindControl<CheckBox>(view, "region-drag-docking").IsChecked = false; Flush();
            Require(!backend.DragDocking && backend.Enabled, "Drag docking must be independently configurable.");
            FindControl<CheckBox>(view, "region-drag-docking").IsChecked = true; Flush();
            FindControl<CheckBox>(view, "region-edit").IsChecked = true; Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-regions.png")); renders++;
            view.Navigate("Clients"); Flush();
            Require(!backend.Editing, "Leaving Regions must close the on-screen editor.");
            view.Navigate("Regions"); Flush();
            FindControl<TextBox>(view, "search-settings").Text = "docking"; Flush();
            Click(view, "search-module-Regions-regions"); Flush();
            Require(FindControl<Button>(view, "region-add").IsVisible, "Docking search must open Regions.");
            window.Width = 940; window.Height = 700; Flush();
            Capture(window, Path.Combine(output, theme.ToLowerInvariant() + "-regions-compact.png")); renders++;
            Click(view, "region-delete"); Flush();
            Require(backend.Items.Count == 1 && backend.Assignments.Count == 0, "Removing a region must clear its assignments.");
            backend.ExecuteAsync(new("theme", Value: "Legacy")).GetAwaiter().GetResult(); Flush(); view.Navigate("Regions"); Flush();
            Require(!view.GetVisualDescendants().Any(control => control.Name == "region-add"), "Legacy must not expose Regions.");
            window.Close();
        }
        return renders;
    }

    private sealed class RegionBackend : IWorkspaceBackend, IWorkspaceRegions
    {
        private readonly SmokeBackend _inner = new();
        public List<RegionItem> Items { get; } = [];
        public Dictionary<string, string> Assignments { get; } = new();
        public bool OtherProcessAvailable { get; set; } = true;
        public bool Editing { get; private set; }
        public bool Enabled { get; private set; } = true;
        public bool DragDocking { get; private set; } = true;
        public event Action? Changed;
        public RegionBackend() => _inner.Changed += Notify;
        public void Notify() => Changed?.Invoke();
        public WorkspaceSnapshot Read() => _inner.Read() with
        {
            Clients = OtherProcessAvailable ? [.. _inner.Read().Clients, new("notepad (123)", true)] : _inner.Read().Clients,
            SavedClientTitles = ["EVE - Historical", "notepad (99)"],
            CycleGroups = [.. _inner.Read().CycleGroups, new(99, "Offline fleet", ["EVE - Offline group member"], [], [])]
        };
        public RegionSnapshot ReadRegions() => new(Items.ToArray(), new Dictionary<string, string>(Assignments), Editing, "", 192, 108, 960, 540,
            Enabled, DragDocking, [new("Portrait", -1080, -200, 1080, 1920, 90, false), new("Main", 0, 0, 2560, 1440, 0, true),
                new("Flipped portrait", 2560, 200, 1440, 2560, 270, false)]);
        public void StopRegionEditing() { Editing = false; }
        public Task<CommandResult> ExecuteAsync(WorkspaceCommand command)
        {
            switch (command.Action)
            {
                case "region-add": Items.Add(new(Guid.NewGuid().ToString("N"), "Region " + (Items.Count + 1), 40 + 450 * Items.Count, 40, 384, 216)); break;
                case "region-rename":
                    int index = Items.FindIndex(region => region.Id == command.Target);
                    Items[index] = Items[index] with { Name = command.Value };
                    break;
                case "region-assign": if (command.Value == "") Assignments.Remove(command.Target); else Assignments[command.Target] = command.Value; break;
                case "region-delete":
                    Items.RemoveAll(region => region.Id == command.Target);
                    foreach (string title in Assignments.Keys.Where(title => Assignments[title] == command.Target).ToArray()) Assignments.Remove(title);
                    break;
                case "region-edit": Editing = bool.Parse(command.Value); break;
                case "region-enabled": Enabled = bool.Parse(command.Value); if (!Enabled) Editing = false; break;
                case "region-drag-docking": DragDocking = bool.Parse(command.Value); break;
                default: return _inner.ExecuteAsync(command);
            }
            Notify(); return Task.FromResult(CommandResult.Ok());
        }
    }
}
