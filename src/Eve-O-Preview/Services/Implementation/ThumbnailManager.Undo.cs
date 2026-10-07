using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.View;

namespace EveOPreview.Services;

sealed partial class ThumbnailManager
{
    private sealed record ThumbnailEditState(string Title, Point Location, Size Size, Size? SizeOverride, bool Skipped);
    private sealed record ThumbnailEditSnapshot(IntPtr Owner, ThumbnailEditKind Kind, string ActiveClient,
        bool PerClientLayouts, Dictionary<IThumbnailView, ThumbnailEditState> Views,
        Size DefaultSize, Dictionary<string, Size> AllSizes);
    private ThumbnailEditSnapshot _pendingThumbnailEdit;
    private (ThumbnailEditSnapshot Before, ThumbnailEditSnapshot After)? _thumbnailUndo;

    public bool CanUndoThumbnailEdit => _thumbnailUndo != null && _pendingThumbnailEdit == null
        && !_thumbnailViews.Values.Any(view => view.IsInteracting);

    public void BeginThumbnailEdit(IntPtr id, ThumbnailEditKind kind, bool selected = false)
    {
        if (_pendingThumbnailEdit != null) CompleteThumbnailEdit(_pendingThumbnailEdit.Owner);
        if (!_thumbnailViews.TryGetValue(id, out var anchor)) return;
        var targets = kind == ThumbnailEditKind.ResizeAll ? _thumbnailViews.Values.ToArray()
            : selected ? _thumbnailViews.Values.Where(view => view.IsSelected && view.IsActive
                && !_configuration.IsThumbnailDisabled(view.Title)).ToArray() : [anchor];
        if (targets.Length == 0) return;
        if (kind != ThumbnailEditKind.CycleSkip)
        if (kind != ThumbnailEditKind.CycleSkip)
            foreach (var view in targets) view.ZoomOut();
        _pendingThumbnailEdit = CaptureThumbnailEdit(id, kind, targets, _activeClient.Title, _configuration.EnablePerClientThumbnailLayouts);
    }

    private ThumbnailEditSnapshot CaptureThumbnailEdit(IntPtr owner, ThumbnailEditKind kind, IEnumerable<IThumbnailView> views,
        string activeClient, bool perClientLayouts) => new(owner, kind, activeClient, perClientLayouts,
        views.ToDictionary(view => view, view => new ThumbnailEditState(view.Title, view.ThumbnailLocation, view.ThumbnailSize,
            _configuration.PerClientThumbnailSizes.TryGetValue(view.Title, out var size) ? size : null,
            _configuration.IsClientCycleSkipped(view.Title))), _configuration.ThumbnailSize,
        kind == ThumbnailEditKind.ResizeAll ? new(_configuration.PerClientThumbnailSizes) : null);

    public void CompleteThumbnailEdit(IntPtr id)
    {
        var before = _pendingThumbnailEdit;
        if (before == null || before.Owner != id) return;
        _pendingThumbnailEdit = null;
        var after = CaptureThumbnailEdit(id, before.Kind, before.Views.Keys, before.ActiveClient, before.PerClientLayouts);
        bool changed = before.Views.Any(entry => before.Kind == ThumbnailEditKind.CycleSkip
            ? entry.Value.Skipped != after.Views[entry.Key].Skipped
            : entry.Value.Location != after.Views[entry.Key].Location || entry.Value.Size != after.Views[entry.Key].Size
                || entry.Value.SizeOverride != after.Views[entry.Key].SizeOverride);
        if (before.Kind == ThumbnailEditKind.ResizeAll)
            changed |= before.DefaultSize != after.DefaultSize || before.AllSizes.Count != after.AllSizes.Count
                || before.AllSizes.Any(entry => !after.AllSizes.TryGetValue(entry.Key, out var size) || entry.Value != size);
        // A click without movement must not consume the last useful undo.
        if (changed) _thumbnailUndo = (before, after);
    }

    private void InvalidateThumbnailUndo()
    {
        _pendingThumbnailEdit = null;
        _thumbnailUndo = null;
    }

    private void ForgetThumbnailUndo(IThumbnailView view)
    {
        if (_pendingThumbnailEdit?.Views.ContainsKey(view) == true || _thumbnailUndo?.Before.Views.ContainsKey(view) == true)
            InvalidateThumbnailUndo();
    }

    public async Task UndoThumbnailEdit()
    {
        if (!CanUndoThumbnailEdit) return;
        var (before, after) = _thumbnailUndo.Value;
        _thumbnailUndo = null; // One step only; Undo itself does not become another edit.
        if (before.Views.Any(entry => !_thumbnailViews.TryGetValue(entry.Key.Id, out var current)
                || !ReferenceEquals(current, entry.Key) || entry.Value.Title != current.Title)) return;

        bool wasIgnoring = _ignoreViewEvents;
        _ignoreViewEvents = true;
        try
        {
            if (before.Kind == ThumbnailEditKind.ResizeAll)
            {
                _configuration.ThumbnailSize = before.DefaultSize;
                _configuration.PerClientThumbnailSizes.Clear();
                foreach (var entry in before.AllSizes) _configuration.PerClientThumbnailSizes.Add(entry.Key, entry.Value);
                ApplyThumbnailSizes();
            }
            foreach (var entry in before.Views)
            {
                var view = entry.Key;
                var original = entry.Value;
                var edited = after.Views[view];
                if (before.Kind == ThumbnailEditKind.CycleSkip)
                {
                    await _mediator.Send(new SetClientCycleSkipped(original.Title, original.Skipped));
                    continue;
                }
                view.ZoomOut();
                if (original.Location != edited.Location)
                {
                    // Restore the layout in which the edit was made, not whichever
                    // client happens to be active when Undo is clicked.
                    _configuration.SetThumbnailLocation(original.Title, before.ActiveClient, original.Location);
                    if (!before.PerClientLayouts || before.ActiveClient == _activeClient.Title)
                        view.ThumbnailLocation = original.Location;
                }
                if (before.Kind != ThumbnailEditKind.ResizeAll
                    && (original.Size != edited.Size || original.SizeOverride != edited.SizeOverride))
                {
                    if (original.SizeOverride is { } size) _configuration.PerClientThumbnailSizes[original.Title] = size;
                    else _configuration.PerClientThumbnailSizes.Remove(original.Title);
                    view.ThumbnailSize = original.Size;
                }
                view.SetGroupInteraction(false); // Refresh the hover baseline after restoring position too.
                view.Refresh(false);
            }
        }
        finally { _ignoreViewEvents = wasIgnoring; }
        if (before.Kind == ThumbnailEditKind.ResizeAll)
            await _mediator.Publish(new ThumbnailActiveSizeUpdated(_configuration.ThumbnailSize));
        if (before.Kind != ThumbnailEditKind.CycleSkip)
            await _mediator.Send(new SaveConfiguration());
    }
}
