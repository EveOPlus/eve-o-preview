using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Input;
using EveOPreview.Preview;
using EveOPreview.UI;
using EveOPreview.View.CustomControl;

namespace EveOPreview.View;

public abstract partial class ThumbnailView
{
    private readonly Dictionary<string, ThumbnailRegionWindow> _regionDragHighlights = new();
    private readonly List<RegionDockTarget> _regionDockTargets = new();
    private List<ThumbnailRegion> _regionDragProfile;
    private string _regionDragTitle, _regionDockCandidate;
    private bool _regionDragWasDocked, _regionDragAllowUndock, _regionDragBypassed, _regionDockPending, _nativeMoving;
    private Point _nativeMoveLocation;
    private sealed record RegionDrop(string Target, bool Undock);

    private void BeginRegionDrag(bool allowUndock = false)
    {
        if (NativeMenuTheme.CurrentTheme == "Legacy" || IsSelected || _groupInteraction
            || !_config.EnableThumbnailRegions || !_config.EnableRegionDragDocking
            || _thumbnailManager == null || _config.ThumbnailRegions.Count == 0) return;
        _regionDragProfile = _config.ThumbnailRegions;
        _regionDragTitle = Title;
        _regionDragWasDocked = _config.GetThumbnailRegion(Title) != null;
        _regionDragAllowUndock = allowUndock;
        _regionDragBypassed = false;
        var limits = new RegionSnapshot([], new Dictionary<string, string>(), false, "",
            _config.ThumbnailMinimumSize.Width, _config.ThumbnailMinimumSize.Height,
            _config.ThumbnailMaximumSize.Width, _config.ThumbnailMaximumSize.Height);
        foreach (var region in _regionDragProfile)
        {
            var item = new RegionItem(region.Id, region.Name, region.X, region.Y, region.Width, region.Height);
            var highlight = new ThumbnailRegionWindow(item, limits, null, interactive: false);
            _regionDragHighlights.Add(region.Id, highlight);
            _regionDockTargets.Add(new(region.Id, new(region.X, region.Y, region.Width, region.Height)));
            highlight.ShowEditor(this);
        }
    }

    private bool RegionDragIsCurrent => _regionDragProfile != null
        && ReferenceEquals(_regionDragProfile, _config.ThumbnailRegions) && _regionDragTitle == Title
        && NativeMenuTheme.CurrentTheme != "Legacy" && _config.EnableThumbnailRegions && _config.EnableRegionDragDocking;

    private void UpdateRegionDrag(Point pointer, ShortcutKeys modifiers)
    {
        if (_regionDragProfile == null) return;
        if (!RegionDragIsCurrent) { EndRegionDrag(false); return; }
        _regionDragBypassed = (modifiers & ShortcutKeys.Shift) != 0;
        string candidate = _regionDragBypassed ? null
            : RegionDockTarget.FindClosest(_regionDockTargets, pointer.X, pointer.Y, (int)Math.Round(12 * RenderScaling));
        if (_regionDockCandidate == candidate) return;
        _regionDockCandidate = candidate;
        foreach (var entry in _regionDragHighlights) entry.Value.SetDockTarget(entry.Key == candidate);
    }

    // Only a completed pointer release can change an assignment. Cancellation,
    // profile changes and disposal merely close the temporary, click-through targets.
    private RegionDrop EndRegionDrag(bool commit)
    {
        bool current = RegionDragIsCurrent;
        string candidate = commit && current ? _regionDockCandidate : null;
        bool undock = commit && current && !_regionDragBypassed && candidate == null && _regionDragWasDocked && _regionDragAllowUndock;
        bool restoreDock = current && _regionDragWasDocked && !undock;
        _regionDragProfile = null; _regionDragTitle = _regionDockCandidate = null;
        _regionDockTargets.Clear();
        foreach (var window in _regionDragHighlights.Values) window.Dispose();
        _regionDragHighlights.Clear();
        if (restoreDock && !_disposed && _config.GetThumbnailRegion(Title) is { } region)
        {
            ThumbnailLocation = new(region.X, region.Y);
            ThumbnailSize = new(region.Width, region.Height);
            RefreshAppearance();
        }
        return candidate != null || undock ? new(candidate, undock) : null;
    }

    private async Task CommitRegionDockAsync(RegionDrop drop)
    {
        if (drop == null || _disposed) return;
        _regionDockPending = true;
        try
        {
            if (drop.Undock) await _thumbnailManager.UndockThumbnail(Id);
            else await _thumbnailManager.DockThumbnail(Id, drop.Target);
        }
        catch (Exception ex)
        {
            if (!_disposed && _config.GetThumbnailRegion(Title) is { } region)
            {
                ThumbnailLocation = new(region.X, region.Y);
                ThumbnailSize = new(region.Width, region.Height);
                RefreshAppearance();
            }
            Serilog.Log.Error(ex, "Could not change thumbnail region {Region}", drop.Target);
        }
        finally
        {
            _regionDockPending = false;
            if (!_disposed) { SaveWindowSizeAndLocation(); ReleaseHoverOutside(); }
        }
    }
}
