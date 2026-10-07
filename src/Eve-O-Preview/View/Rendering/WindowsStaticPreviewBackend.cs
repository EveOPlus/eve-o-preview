using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EveOPreview.Preview;
using EveOPreview.Services;

namespace EveOPreview.View.Rendering;

/// <summary>Compatibility capture retains one bitmap in an input-transparent Avalonia image.</summary>
public sealed class WindowsStaticPreviewBackend(
    IWindowManager windows, Canvas destination, Func<PreviewClientId, IntPtr> source) : IPreviewBackend
{
    public PreviewCapabilities Capabilities => PreviewCapabilities.CapturedImage;
    public IPreviewSession CreateSession(PreviewClientId client) => new Session(windows, destination, () => source(client));
    private sealed class Session : IPreviewSession
    {
        private readonly IWindowManager _windows;
        private readonly Canvas _destination;
        private readonly Func<IntPtr> _source;
        private readonly Image _image = new() { IsHitTestVisible = false, Stretch = Stretch.Fill };
        private PreviewRect _bounds;
        private TopLevel _topLevel;
        private bool _disposed;
        public PreviewCapabilities Capabilities => PreviewCapabilities.CapturedImage;
        public Session(IWindowManager windows, Canvas destination, Func<IntPtr> source)
        {
            _windows = windows; _destination = destination; _source = source;
            destination.Children.Add(_image);
            _image.AttachedToVisualTree += Attached;
            _image.DetachedFromVisualTree += Detached;
            AttachRoot();
        }
        private void Attached(object sender, Avalonia.VisualTreeAttachmentEventArgs args) => AttachRoot();
        private void Detached(object sender, Avalonia.VisualTreeAttachmentEventArgs args)
        {
            if (_topLevel != null) _topLevel.ScalingChanged -= ScalingChanged;
            _topLevel = null;
        }
        private void AttachRoot()
        {
            var root = TopLevel.GetTopLevel(_destination);
            if (_topLevel == root) return;
            if (_topLevel != null) _topLevel.ScalingChanged -= ScalingChanged;
            _topLevel = root;
            if (_topLevel != null) _topLevel.ScalingChanged += ScalingChanged;
            ApplyBounds();
        }
        private void ScalingChanged(object sender, EventArgs args) => ApplyBounds();
        public void SetBounds(PreviewRect bounds)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _bounds = bounds; ApplyBounds();
        }
        private void ApplyBounds()
        {
            double scale = _topLevel?.RenderScaling ?? 1;
            Canvas.SetLeft(_image, _bounds.X / scale); Canvas.SetTop(_image, _bounds.Y / scale);
            _image.Width = Math.Max(0, _bounds.Width) / scale; _image.Height = Math.Max(0, _bounds.Height) / scale;
        }
        public void Refresh(bool maintenance)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!maintenance) return;
            using var captured = _windows.GetStaticThumbnail(_source());
            if (captured == null) return;
            // This is the explicitly selected compatibility backend, never the DWM path.
            using var converted = captured is System.Drawing.Bitmap ? null : new System.Drawing.Bitmap(captured);
            var replacement = WindowsBitmap.Copy(captured as System.Drawing.Bitmap ?? converted);
            var previous = _image.Source as Bitmap;
            _image.Source = replacement; previous?.Dispose();
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_topLevel != null) _topLevel.ScalingChanged -= ScalingChanged;
            _image.AttachedToVisualTree -= Attached; _image.DetachedFromVisualTree -= Detached;
            (_image.Source as Bitmap)?.Dispose(); _image.Source = null;
            _destination.Children.Remove(_image);
        }
    }
}
