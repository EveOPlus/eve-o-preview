using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Mediator.Messages;
using EveOPreview.UI;
using EveOPreview.Preview;

namespace EveOPreview.View;

public sealed partial class WindowsWorkspaceBackend : IWorkspaceRegions
{
    private readonly Dictionary<string, ThumbnailRegionWindow> _regionWindows = new();
    private bool _editingRegions;
    private string _regionError = "";
    private int _regionEditSession;
    private string _regionProfile;

    public RegionSnapshot ReadRegions()
    {
        if (_editingRegions && (!_configuration.EnableThumbnailRegions || _regionProfile != _storage.CurrentProfile?.FullPath || _preferences.Theme == "Legacy")) StopRegionEditing();
        return new(_configuration.ThumbnailRegions.Select(ToItem).ToArray(),
        new Dictionary<string, string>(_configuration.ClientRegionAssignments), _editingRegions, _regionError,
        _configuration.ThumbnailMinimumSize.Width, _configuration.ThumbnailMinimumSize.Height,
        _configuration.ThumbnailMaximumSize.Width, _configuration.ThumbnailMaximumSize.Height,
        _configuration.EnableThumbnailRegions, _configuration.EnableRegionDragDocking, ReadRegionMonitors());
    }

    private RegionMonitor[] ReadRegionMonitors() => _view is Window owner
        ? owner.Screens.All.Select(screen => new RegionMonitor(screen.DisplayName ?? "Display",
            screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height,
            screen.CurrentOrientation switch
            {
                ScreenOrientation.Portrait => 90, ScreenOrientation.LandscapeFlipped => 180,
                ScreenOrientation.PortraitFlipped => 270, _ => 0
            }, screen.IsPrimary)).ToArray() : [];

    private void RegionScreensChanged(object sender, EventArgs args) => NotifyChanged();

    private static RegionItem ToItem(ThumbnailRegion region) => new(region.Id, region.Name, region.X, region.Y, region.Width, region.Height);

    public void StopRegionEditing()
    {
        bool wasEditing = _editingRegions;
        _editingRegions = false;
        if (wasEditing) _thumbnails?.SetRegionEditCancel(null);
        _regionEditSession++;
        foreach (var window in _regionWindows.Values) window.Dispose();
        _regionWindows.Clear();
        if (wasEditing) NotifyChanged();
    }

    private void RefreshRegionWindows()
    {
        if (!_editingRegions) return;
        foreach (var id in _regionWindows.Keys.Where(id => !_configuration.ThumbnailRegions.Any(region => region.Id == id)).ToArray())
        {
            _regionWindows[id].Dispose();
            _regionWindows.Remove(id);
        }
        foreach (var region in _configuration.ThumbnailRegions)
        {
            if (_regionWindows.TryGetValue(region.Id, out var window)) window.UpdateRegion(ToItem(region));
            else
            {
                int session = _regionEditSession;
                window = new ThumbnailRegionWindow(ToItem(region), ReadRegions(), async bounds =>
                {
                    if (session != _regionEditSession) return;
                    var result = await ExecuteAsync(new WorkspaceCommand("region-update", bounds.Id, Settings: new Dictionary<string, string>
                    {
                        ["X"] = bounds.X.ToString(CultureInfo.InvariantCulture),
                        ["Y"] = bounds.Y.ToString(CultureInfo.InvariantCulture), ["Width"] = bounds.Width.ToString(CultureInfo.InvariantCulture),
                        ["Height"] = bounds.Height.ToString(CultureInfo.InvariantCulture)
                    }));
                    if (session != _regionEditSession) return;
                    _regionError = result.Success ? "" : result.Message;
                    RefreshRegionWindows();
                    NotifyChanged();
                }, () => _configuration.EnableThumbnailSnap, targets => CollectRegionSnapTargets(region.Id, targets));
                _regionWindows.Add(region.Id, window);
                window.ShowEditor(_view as Window);
            }
        }
    }

    private void CollectRegionSnapTargets(string id, List<ThumbnailSnapTarget> targets)
    {
        DesktopSnapTargets.Collect(targets, _regionWindows[id], _configuration, _thumbnails, excludedRegion: id);
        for (int i = 0; i < _configuration.ThumbnailRegions.Count; i++)
        {
            var region = _configuration.ThumbnailRegions[i];
            if (region.Id != id)
                targets.Add(new(long.MinValue / 2 + i, new(region.X, region.Y, region.Width, region.Height), FixedBounds: true));
        }
    }

    private async Task<CommandResult> EditRegionAsync(WorkspaceCommand command)
    {
        if (_preferences.Theme == "Legacy") return CommandResult.Error("Regions are available in Light and Dark themes.");
        if (command.Action == "region-edit")
        {
            if (!bool.TryParse(command.Value, out bool editing)) return CommandResult.Error("Choose an edit state.");
            if (editing && !_configuration.EnableThumbnailRegions) return CommandResult.Error("Enable regions before editing on screen.");
            StopRegionEditing();
            _regionError = "";
            _editingRegions = editing;
            _regionProfile = _storage.CurrentProfile?.FullPath;
            if (editing) _thumbnails?.SetRegionEditCancel(StopRegionEditing);
            RefreshRegionWindows();
            return CommandResult.Ok();
        }
        if (command.Action is "region-enabled" or "region-drag-docking")
        {
            if (!bool.TryParse(command.Value, out bool enabled)) return CommandResult.Error("Choose an enabled state.");
            bool beforeEnabled = _configuration.EnableThumbnailRegions;
            bool beforeDragging = _configuration.EnableRegionDragDocking;
            if (command.Action == "region-enabled") _configuration.EnableThumbnailRegions = enabled;
            else _configuration.EnableRegionDragDocking = enabled;
            try { await _mediator.Send(new SaveConfiguration()); }
            catch
            {
                _configuration.EnableThumbnailRegions = beforeEnabled;
                _configuration.EnableRegionDragDocking = beforeDragging;
                throw;
            }
            if (!_configuration.EnableThumbnailRegions) StopRegionEditing();
            _thumbnails?.ApplyRegionLayout();
            return CommandResult.Ok("Regions saved");
        }
        var beforeRegions = _configuration.ThumbnailRegions;
        var beforeAssignments = _configuration.ClientRegionAssignments;
        var regions = beforeRegions.Select(region => region with { }).ToList();
        var assignments = new Dictionary<string, string>(beforeAssignments);
        var selected = regions.FirstOrDefault(region => region.Id == command.Target);
        switch (command.Action)
        {
            case "region-add":
                var owner = _view as Window;
                var area = owner?.Screens.ScreenFromWindow(owner)?.WorkingArea;
                int number = 1;
                while (regions.Any(region => region.Name == $"Region {number}")) number++;
                regions.Add(new ThumbnailRegion { Id = Guid.NewGuid().ToString("N"), Name = $"Region {number}",
                    X = (area?.X ?? 0) + 30, Y = (area?.Y ?? 0) + 30,
                    Width = _configuration.ThumbnailSize.Width, Height = _configuration.ThumbnailSize.Height });
                break;
            case "region-update":
                if (selected == null) return CommandResult.Error("Choose a region.");
                if (!ReadNumber("X", -1000000, 1000000, out int x) || !ReadNumber("Y", -1000000, 1000000, out int y)
                    || !ReadNumber("Width", _configuration.ThumbnailMinimumSize.Width, _configuration.ThumbnailMaximumSize.Width, out int width)
                    || !ReadNumber("Height", _configuration.ThumbnailMinimumSize.Height, _configuration.ThumbnailMaximumSize.Height, out int height))
                    return CommandResult.Error("Enter whole pixel coordinates and a size within the preview limits.");
                selected.X = x; selected.Y = y; selected.Width = width; selected.Height = height;
                break;
            case "region-rename":
                if (selected == null) return CommandResult.Error("Choose a region.");
                if (string.IsNullOrWhiteSpace(command.Value) || command.Value.Trim().Length > 80)
                    return CommandResult.Error("Enter a region name (1-80 characters).");
                selected.Name = command.Value.Trim();
                break;
            case "region-delete":
                if (selected == null) return CommandResult.Error("Choose a region.");
                regions.Remove(selected);
                foreach (var title in assignments.Keys.Where(title => assignments[title] == selected.Id).ToArray()) assignments.Remove(title);
                break;
            case "region-assign":
                if (string.IsNullOrWhiteSpace(command.Target) || (!_clients.ContainsKey(command.Target)
                    && !(_view.CycleGroups ?? []).Any(group => group.ClientsOrder.Values.Contains(command.Target, StringComparer.Ordinal))))
                    return CommandResult.Error("Choose an available client or a client in a cycle group.");
                if (command.Value.Length > 0 && !regions.Any(region => region.Id == command.Value)) return CommandResult.Error("Choose a region.");
                if (command.Value.Length == 0) assignments.Remove(command.Target);
                else assignments[command.Target] = command.Value;
                break;
            default: return CommandResult.Error("Unknown region action.");
        }
        _configuration.ThumbnailRegions = regions;
        _configuration.ClientRegionAssignments = assignments;
        try { await _mediator.Send(new SaveConfiguration()); }
        catch
        {
            _configuration.ThumbnailRegions = beforeRegions;
            _configuration.ClientRegionAssignments = beforeAssignments;
            throw;
        }
        _regionError = "";
        _thumbnails?.ApplyRegionLayout();
        RefreshRegionWindows();
        return CommandResult.Ok("Regions saved");

        bool ReadNumber(string key, int min, int max, out int value)
        {
            value = 0;
            return command.Settings != null && command.Settings.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= min && value <= max;
        }
    }
}
