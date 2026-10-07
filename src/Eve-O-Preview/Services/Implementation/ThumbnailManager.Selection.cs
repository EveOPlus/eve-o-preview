using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EveOPreview.View;

namespace EveOPreview.Services;

sealed partial class ThumbnailManager
{
    // Live view identity prevents a closed/reused source HWND from joining a gesture.
    private readonly Dictionary<IThumbnailView, (Point Location, Size Size)> _selectionOrigins = new();
    private IThumbnailView _selectionAnchor;
    private bool _selectionResize;
    private bool _selectionMoved;

    public void ToggleThumbnailSelection(IntPtr id)
    {
        if (!_thumbnailViews.TryGetValue(id, out var view) || !view.IsActive) return;
        if (_selectionAnchor != null) return;
        view.ZoomOut();
        view.SetSelected(!view.IsSelected);
    }

    public void ClearThumbnailSelection()
    {
        CancelSelectionTransform();
        foreach (var view in _thumbnailViews.Values) view.SetSelected(false);
    }

    public void RemoveThumbnailSelection(IntPtr id)
    {
        if (!_thumbnailViews.TryGetValue(id, out var view)) return;
        if (_selectionOrigins.ContainsKey(view)) CancelSelectionTransform();
        view.SetSelected(false);
    }

    private void CancelSelectionTransform()
    {
        var anchor = _selectionAnchor;
        // Clear state before cancelling the owner's pointer subscription (which calls End).
        _selectionAnchor = null;
        foreach (var view in _selectionOrigins.Keys) view.SetGroupInteraction(false);
        _selectionOrigins.Clear();
        anchor?.CancelInteraction();
    }

    public bool BeginSelectionTransform(IntPtr id, bool resize)
    {
        CancelSelectionTransform();
        if (!_thumbnailViews.TryGetValue(id, out var anchor) || !anchor.IsSelected) return false;
        foreach (var view in _thumbnailViews.Values.Where(v => v.IsSelected && v.IsActive
                     && !_configuration.IsThumbnailDisabled(v.Title)))
        {
            view.ZoomOut();
            _selectionOrigins.Add(view, (view.ThumbnailLocation, view.ThumbnailSize));
            view.SetGroupInteraction(true);
        }
        if (!_selectionOrigins.ContainsKey(anchor))
        {
            CancelSelectionTransform();
            return false;
        }
        _selectionAnchor = anchor;
        _selectionResize = resize;
        _selectionMoved = false;
        return true;
    }

    public void EndSelectionTransform(IntPtr id)
    {
        if (_selectionAnchor?.Id != id) return;
        var anchor = _selectionAnchor;
        _selectionAnchor = null;
        foreach (var view in _selectionOrigins.Keys) view.SetGroupInteraction(false);
        _selectionOrigins.Clear();
        // All members' locations are already in memory. Queue just one normal save,
        // rather than sending a save for each member on every pointer update.
        if (_selectionMoved) EnqueueLocationChange(anchor);
    }

    private bool ApplySelectionMove(IThumbnailView anchor)
    {
        if (_selectionAnchor != anchor || _selectionResize) return false;
        var origin = _selectionOrigins[anchor].Location;
        var delta = new Size(anchor.ThumbnailLocation.X - origin.X, anchor.ThumbnailLocation.Y - origin.Y);
        foreach (var entry in _selectionOrigins)
        {
            var view = entry.Key;
            view.ThumbnailLocation = entry.Value.Location + delta;
            _configuration.SetThumbnailLocation(view.Title, _activeClient.Title, view.ThumbnailLocation);
            view.Refresh(false);
        }
        _selectionMoved = true;
        return true;
    }

    private bool ApplySelectionResize(IThumbnailView anchor)
    {
        if (_selectionAnchor != anchor || !_selectionResize) return false;
        double scale = (double)anchor.ThumbnailSize.Width / _selectionOrigins[anchor].Size.Width;
        double lower = 0, upper = double.MaxValue;
        foreach (var origin in _selectionOrigins.Values)
        {
            lower = Math.Max(lower, Math.Max((double)_configuration.ThumbnailMinimumSize.Width / origin.Size.Width,
                (double)_configuration.ThumbnailMinimumSize.Height / origin.Size.Height));
            upper = Math.Min(upper, Math.Min((double)_configuration.ThumbnailMaximumSize.Width / origin.Size.Width,
                (double)_configuration.ThumbnailMaximumSize.Height / origin.Size.Height));
        }
        scale = lower <= upper ? Math.Clamp(scale, lower, upper) : 1;
        bool wasIgnoring = _ignoreViewEvents;
        _ignoreViewEvents = true;
        try
        {
            foreach (var entry in _selectionOrigins)
            {
                var view = entry.Key;
                view.ThumbnailSize = ScaleSize(entry.Value.Size, scale);
                _configuration.PerClientThumbnailSizes[view.Title] = view.ThumbnailSize;
                view.Refresh(false);
            }
        }
        finally { _ignoreViewEvents = wasIgnoring; }
        return true;
    }
}
