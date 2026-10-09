using EveOPreview.View;
using System;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;

namespace EveOPreview.Services;

sealed partial class ThumbnailManager
{
    private Action _cancelRegionEdit;

    public void SetRegionEditCancel(Action cancel)
    {
        if (_cancelRegionEdit == cancel) return;
        _cancelRegionEdit = cancel;
        RegisterAllHotkeys();
    }

    public async Task<bool> DockThumbnail(IntPtr id, string regionId)
    {
        if (!_configuration.EnableThumbnailRegions || !_configuration.EnableRegionDragDocking
            || !_thumbnailViews.TryGetValue(id, out var view) || !view.IsActive
            || !_configuration.ThumbnailRegions.Any(region => region.Id == regionId)) return false;
        string title = view.Title;
        _configuration.ClientRegionAssignments.TryGetValue(title, out var previous);
        if (previous == regionId) return true;
        _configuration.ClientRegionAssignments[title] = regionId;
        try { await _mediator.Send(new SaveConfiguration()); }
        catch
        {
            if (previous == null) _configuration.ClientRegionAssignments.Remove(title);
            else _configuration.ClientRegionAssignments[title] = previous;
            throw;
        }
        ApplyRegionLayout();
        _configuration.NotifyRegionsChanged();
        return true;
    }

    public async Task UndockThumbnail(IntPtr id)
    {
        if (!_thumbnailViews.TryGetValue(id, out var view) || _configuration.GetThumbnailRegion(view.Title) is not { } region) return;
        var location = view.ThumbnailLocation;
        var size = view.ThumbnailSize;
        _configuration.ClientRegionAssignments.Remove(view.Title);
        var previousLocation = _configuration.GetThumbnailLocation(view.Title, _activeClient.Title, location);
        bool hadSize = _configuration.PerClientThumbnailSizes.TryGetValue(view.Title, out var previousSize);
        _configuration.SetThumbnailLocation(view.Title, _activeClient.Title, location);
        _configuration.PerClientThumbnailSizes[view.Title] = size;
        try { await _mediator.Send(new SaveConfiguration()); }
        catch
        {
            _configuration.SetThumbnailLocation(view.Title, _activeClient.Title, previousLocation);
            if (hadSize) _configuration.PerClientThumbnailSizes[view.Title] = previousSize;
            else _configuration.PerClientThumbnailSizes.Remove(view.Title);
            _configuration.ClientRegionAssignments[view.Title] = region.Id;
            throw;
        }
        ApplyRegionLayout();
        _configuration.NotifyRegionsChanged();
    }

    // Reuse the existing native windows and DWM relationships when docking changes.
    public void ApplyRegionLayout()
    {
        InvalidateThumbnailUndo();
        ClearThumbnailSelection();
        bool wasIgnoring = _ignoreViewEvents;
        _ignoreViewEvents = true;
        try
        {
            foreach (var view in _thumbnailViews.Values)
            {
                view.CancelInteraction();
                view.ZoomOut();
                view.SetFrames(_configuration.ShowThumbnailFrames && _configuration.GetThumbnailRegion(view.Title) == null);
                view.ThumbnailSize = _configuration.GetThumbnailSize(view.Title);
                view.ThumbnailLocation = _configuration.GetThumbnailLocation(view.Title, _activeClient.Title,
                    IsManageableThumbnail(view) ? view.ThumbnailLocation : _configuration.LoginThumbnailLocation);
                view.SetGroupInteraction(false);
                view.Refresh(false);
            }
        }
        finally { _ignoreViewEvents = wasIgnoring; }
    }
}
