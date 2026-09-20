using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EveOPreview.Preview;
using EveOPreview.View.Rendering;

namespace EveOPreview.View;

/// <summary>Temporary native top-level guides stay above DWM without copying game pixels.</summary>
internal sealed class ThumbnailSnapGuideWindow : Window, IDisposable
{
    private readonly WindowsPreviewWindowAdapter _native;
    private readonly GuideLines _lines = new();
    private bool _disposed;

    public ThumbnailSnapGuideWindow()
    {
        ShowActivated = false;
        ShowInTaskbar = false;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        Content = _lines;
        _native = new(this, clickThrough: true);
    }

    public void UpdateGuides(ThumbnailSnapGuide? vertical, ThumbnailSnapGuide? horizontal, Window owner)
    {
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        Include(vertical);
        Include(horizontal);
        if (left == int.MaxValue) return;
        left -= 2; top -= 2; right += 2; bottom += 2;
        _native.Location = new(left, top);
        _native.ClientSize = new(Math.Max(1, right - left), Math.Max(1, bottom - top));
        _lines.Set(vertical, horizontal, left, top);
        if (!IsVisible) Show(owner);
        _native.Restore(topmost: true, raise: true);

        void Include(ThumbnailSnapGuide? guide)
        {
            if (guide is not { } line) return;
            left = Math.Min(left, line.Vertical ? line.Coordinate : line.Start);
            top = Math.Min(top, line.Vertical ? line.Start : line.Coordinate);
            right = Math.Max(right, line.Vertical ? line.Coordinate : line.End);
            bottom = Math.Max(bottom, line.Vertical ? line.End : line.Coordinate);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _native.Dispose();
        Close();
    }

    private sealed class GuideLines : Control
    {
        private static readonly Pen Shadow = new(new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)), 3);
        private static readonly Pen Guide = new(new SolidColorBrush(Color.Parse("#66D9EF")), 1);
        private ThumbnailSnapGuide? _vertical, _horizontal;
        private int _left, _top;

        public void Set(ThumbnailSnapGuide? vertical, ThumbnailSnapGuide? horizontal, int left, int top)
        {
            (_vertical, _horizontal, _left, _top) = (vertical, horizontal, left, top);
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            Draw(_vertical);
            Draw(_horizontal);
            void Draw(ThumbnailSnapGuide? guide)
            {
                if (guide is not { } line) return;
                var start = line.Vertical ? new Point((line.Coordinate - _left) / scale, (line.Start - _top) / scale)
                    : new Point((line.Start - _left) / scale, (line.Coordinate - _top) / scale);
                var end = line.Vertical ? new Point((line.Coordinate - _left) / scale, (line.End - _top) / scale)
                    : new Point((line.End - _left) / scale, (line.Coordinate - _top) / scale);
                context.DrawLine(Shadow, start, end);
                context.DrawLine(Guide, start, end);
            }
        }
    }
}
