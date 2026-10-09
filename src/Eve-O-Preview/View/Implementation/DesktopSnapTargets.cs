using System;
using System.Collections.Generic;
using Avalonia.Controls;
using EveOPreview.Configuration;
using EveOPreview.Preview;
using EveOPreview.Services;

namespace EveOPreview.View;

/// <summary>Shared physical-pixel targets for thumbnail and region gestures.</summary>
internal static class DesktopSnapTargets
{
    public static void Collect(List<ThumbnailSnapTarget> targets, Window window, IThumbnailConfiguration config,
        IThumbnailManager thumbnails, IntPtr excludedThumbnail = default, bool excludeSelected = false, string excludedRegion = null)
    {
        targets.Clear();
        // Full monitor bounds include negative origins and shared monitor seams.
        // Screen IDs cannot collide with HWND identities or region editor targets.
        int index = 0;
        foreach (var screen in window.Screens.All)
        {
            var bounds = screen.Bounds;
            targets.Add(new(long.MinValue + index++, new(bounds.X, bounds.Y, bounds.Width, bounds.Height), FixedBounds: true));
        }
        if (thumbnails == null) return;
        foreach (var view in thumbnails.GetAllKnownClients().Values)
        {
            if (view.Id == excludedThumbnail || !view.IsActive || config.IsThumbnailDisabled(view.Title)
                || excludeSelected && view.IsSelected) continue;
            // Assigned previews stay at the old region rectangle until the edit is committed.
            // They must never pull their own editor back to that rectangle.
            if (excludedRegion != null && config.GetThumbnailRegion(view.Title)?.Id == excludedRegion) continue;
            var location = view.ThumbnailLocation;
            var size = view is ThumbnailView native ? native.Size : view.ThumbnailSize;
            targets.Add(new(view.Id.ToInt64(), new(location.X, location.Y, size.Width, size.Height)));
        }
    }
}
