using System;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Preview;
using EveOPreview.Services;

namespace EveOPreview.View.Rendering;

/// <summary>Compatibility capture owns its bitmap and native image control inside the Windows adapter.</summary>
public sealed class WindowsStaticPreviewBackend(
    IWindowManager windows, Control destination, Func<PreviewClientId, IntPtr> source) : IPreviewBackend
{
    public PreviewCapabilities Capabilities => PreviewCapabilities.CapturedImage;
    public IPreviewSession CreateSession(PreviewClientId client) => new Session(windows, destination, () => source(client));

    private sealed class Session : IPreviewSession
    {
        private readonly IWindowManager _windows;
        private readonly Func<IntPtr> _source;
        private readonly StaticThumbnailImage _image;
        private bool _disposed;
        public PreviewCapabilities Capabilities => PreviewCapabilities.CapturedImage;

        public Session(IWindowManager windows, Control destination, Func<IntPtr> source)
        {
            _windows = windows;
            _source = source;
            _image = new StaticThumbnailImage { TabStop = false, SizeMode = PictureBoxSizeMode.StretchImage };
            destination.Controls.Add(_image);
        }

        public void SetBounds(PreviewRect bounds)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var rectangle = new Rectangle(bounds.X, bounds.Y, Math.Max(0, bounds.Width), Math.Max(0, bounds.Height));
            if (_image.Bounds != rectangle) _image.Bounds = rectangle;
        }

        public void Refresh(bool maintenance)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!maintenance) return;
            var replacement = _windows.GetStaticThumbnail(_source());
            if (replacement == null) return; // Unsupported/minimized capture retains the last valid frame.
            var previous = _image.Image;
            _image.Image = replacement;
            previous?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _image.Image?.Dispose();
            _image.Image = null;
            _image.Dispose();
        }
    }
}
