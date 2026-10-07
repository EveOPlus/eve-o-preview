using System;
using EveOPreview.Preview;
using EveOPreview.Services;

namespace EveOPreview.View.Rendering;

/// <summary>The Windows adapter resolves opaque runtime IDs and owns all native window handles.</summary>
public sealed class WindowsDwmPreviewBackend(
    IWindowManager windows, Func<IntPtr> destination, Func<PreviewClientId, IntPtr> source) : IPreviewBackend
{
    public PreviewCapabilities Capabilities => PreviewCapabilities.NativeLiveImage;

    public IPreviewSession CreateSession(PreviewClientId client) =>
        new Session(() => windows.GetLiveThumbnail(destination(), source(client)));

    private sealed class Session(Func<IDwmThumbnail> register) : IPreviewSession
    {
        private IDwmThumbnail _thumbnail;
        private PreviewRect _bounds;
        private bool _disposed;
        public PreviewCapabilities Capabilities => PreviewCapabilities.NativeLiveImage;

        public void SetBounds(PreviewRect bounds)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_bounds == bounds) return;
            _bounds = bounds;
            if (_thumbnail == null) return;
            Move(_thumbnail);
            _thumbnail.Update();
        }

        public void Refresh(bool maintenance)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thumbnail != null && (!maintenance || _thumbnail.Update())) return;
            // Populate the replacement before releasing the old compositor relationship.
            var replacement = register();
            try { Move(replacement); replacement.Update(); }
            catch { replacement.Unregister(); throw; }
            var obsolete = _thumbnail;
            _thumbnail = replacement;
            obsolete?.Unregister();
        }

        private void Move(IDwmThumbnail thumbnail) => thumbnail.Move(
            _bounds.X, _bounds.Y, _bounds.X + _bounds.Width, _bounds.Y + _bounds.Height);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _thumbnail?.Unregister();
            _thumbnail = null;
        }
    }
}
