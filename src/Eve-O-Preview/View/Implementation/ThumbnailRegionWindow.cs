using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EveOPreview.UI;
using EveOPreview.Preview;
using EveOPreview.View.Rendering;

namespace EveOPreview.View;

/// <summary>Nonactivating region editor or click-through drop highlight, with desktop-pixel geometry.</summary>
internal sealed class ThumbnailRegionWindow : Window, IDisposable
{
    private readonly WindowsPreviewWindowAdapter _native;
    private readonly TextBlock _label = new() { Foreground = Brushes.Black, Margin = new Thickness(12), FontWeight = FontWeight.Bold };
    private readonly Action<RegionItem> _commit;
    private readonly bool _interactive;
    private readonly Border _frame;
    private readonly Func<bool> _snapEnabled;
    private readonly Action<List<ThumbnailSnapTarget>> _collectTargets;
    private readonly ThumbnailSnapSession _snap = new();
    private readonly List<ThumbnailSnapTarget> _snapTargets = new();
    private ThumbnailSnapGuideWindow _guides;
    private RegionItem _region, _origin;
    private PixelPoint _pointerOrigin;
    private IPointer _pointer;
    private int _edges;
    private bool _disposed;
    private readonly int _minWidth, _minHeight, _maxWidth, _maxHeight;

    public ThumbnailRegionWindow(RegionItem region, RegionSnapshot limits, Action<RegionItem> commit,
        Func<bool> snapEnabled = null, Action<List<ThumbnailSnapTarget>> collectTargets = null, bool interactive = true)
    {
        _commit = commit;
        _interactive = interactive;
        _snapEnabled = snapEnabled ?? (() => true);
        _collectTargets = collectTargets;
        (_minWidth, _minHeight, _maxWidth, _maxHeight) = (limits.MinimumWidth, limits.MinimumHeight, limits.MaximumWidth, limits.MaximumHeight);
        ShowActivated = false;
        ShowInTaskbar = false;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        Content = _frame = new Border { Background = new SolidColorBrush(Color.Parse(interactive ? "#66FFDA00" : "#33FFDA00")), BorderBrush = Brushes.Yellow,
            BorderThickness = new Thickness(2), Child = new Border { Background = Brushes.Gold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Child = _label } };
        _native = new(this, clickThrough: !interactive);
        UpdateRegion(region);
        if (interactive)
        {
            PointerPressed += Pressed;
            PointerMoved += Moved;
            PointerReleased += Released;
            PointerCaptureLost += (_, _) => CancelDrag();
        }
        ScalingChanged += (_, _) => ApplyBounds();
        Closed += (_, _) => StopObservingPreviews();
    }

    public void UpdateRegion(RegionItem region)
    {
        if (_pointer != null) return;
        _region = region;
        Title = region.Name;
        _label.Text = region.Name;
        ApplyBounds();
    }

    private void ApplyBounds()
    {
        if (_disposed || _region == null) return;
        _native.Location = new(_region.X, _region.Y);
        _native.ClientSize = new(_region.Width, _region.Height);
    }

    internal void ShowEditor(Window owner)
    {
        ThumbnailView.ZOrderChanged -= RaiseAbovePreviews;
        ThumbnailView.ZOrderChanged += RaiseAbovePreviews;
        if (owner != null) Show(owner); else Show();
        ApplyBounds();
        RaiseAbovePreviews();
    }

    private void RaiseAbovePreviews()
    {
        if (!_disposed && IsVisible) _native.Restore(topmost: true, raise: true);
    }

    private void StopObservingPreviews() => ThumbnailView.ZOrderChanged -= RaiseAbovePreviews;

    internal void SetDockTarget(bool selected)
    {
        if (_interactive) return;
        _frame.BorderThickness = new Thickness(selected ? 4 : 2);
        _frame.Background = new SolidColorBrush(Color.Parse(selected ? "#99FFDA00" : "#33FFDA00"));
        _label.Text = selected ? _region.Name + " - Release to dock" : _region.Name;
    }

    private int Edges(Point point)
    {
        const int grip = 10;
        return (point.X < grip ? 1 : point.X > Bounds.Width - grip ? 2 : 0)
            | (point.Y < grip ? 4 : point.Y > Bounds.Height - grip ? 8 : 0);
    }

    private void Pressed(object sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _edges = Edges(e.GetPosition(this));
        _origin = _region;
        _snap.Reset();
        _pointerOrigin = this.PointToScreen(e.GetPosition(this));
        _pointer = e.Pointer;
        _pointer.Capture(this);
        e.Handled = true;
    }

    private void Moved(object sender, PointerEventArgs e)
    {
        if (_pointer == null)
        {
            int edges = Edges(e.GetPosition(this));
            Cursor = new Cursor(edges switch { 1 or 2 => StandardCursorType.SizeWestEast, 4 or 8 => StandardCursorType.SizeNorthSouth,
                5 or 10 => StandardCursorType.TopLeftCorner, 6 or 9 => StandardCursorType.TopRightCorner, _ => StandardCursorType.SizeAll });
            return;
        }
        var point = this.PointToScreen(e.GetPosition(this));
        int dx = point.X - _pointerOrigin.X, dy = point.Y - _pointerOrigin.Y;
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool bypass = shift || !_snapEnabled();
        _snapTargets.Clear();
        if (!bypass)
        {
            if (_collectTargets != null) _collectTargets(_snapTargets);
            else DesktopSnapTargets.Collect(_snapTargets, this, null, null);
        }
        int acquire = (int)Math.Round(8 * RenderScaling), release = (int)Math.Round(16 * RenderScaling);
        ThumbnailSnapResult result;
        if (_edges == 0)
            result = _snap.Move(new(_origin.X + dx, _origin.Y + dy, _origin.Width, _origin.Height), _snapTargets, acquire, release, bypass);
        else
        {
            bool left = (_edges & 1) != 0, right = (_edges & 2) != 0, top = (_edges & 4) != 0, bottom = (_edges & 8) != 0;
            var raw = FitSize(_origin.Width + (left ? -dx : right ? dx : 0),
                _origin.Height + (top ? -dy : bottom ? dy : 0), shift);
            result = _snap.Resize(raw, _snapTargets, acquire, release, left, right, top, bottom, bypass);
            result = result.WithConstrainedBounds(FitSize(result.Bounds.Width, result.Bounds.Height, shift), left, top);
        }
        _region = _origin with { X = result.Bounds.X, Y = result.Bounds.Y, Width = result.Bounds.Width, Height = result.Bounds.Height };
        ApplyBounds();
        if (result.VerticalGuide != null || result.HorizontalGuide != null)
        {
            _guides ??= new ThumbnailSnapGuideWindow();
            _guides.UpdateGuides(result.VerticalGuide, result.HorizontalGuide, this);
        }
        else ClearGuides();
        e.Handled = true;
    }

    private PreviewRect FitSize(int width, int height, bool keepRatio)
    {
        if (keepRatio)
        {
            bool heightDriven = (_edges & 3) == 0 || (_edges & 12) != 0
                && Math.Abs((double)height / _origin.Height - 1) > Math.Abs((double)width / _origin.Width - 1);
            double scale = heightDriven ? (double)height / _origin.Height : (double)width / _origin.Width;
            scale = Math.Clamp(scale, Math.Max((double)_minWidth / _origin.Width, (double)_minHeight / _origin.Height),
                Math.Min((double)_maxWidth / _origin.Width, (double)_maxHeight / _origin.Height));
            width = (int)Math.Round(_origin.Width * scale);
            height = (int)Math.Round(_origin.Height * scale);
        }
        width = Math.Clamp(width, _minWidth, _maxWidth);
        height = Math.Clamp(height, _minHeight, _maxHeight);
        return new(_origin.X + ((_edges & 1) != 0 ? _origin.Width - width : 0),
            _origin.Y + ((_edges & 4) != 0 ? _origin.Height - height : 0), width, height);
    }

    private void ClearGuides() { _guides?.Dispose(); _guides = null; }

    private void Released(object sender, PointerReleasedEventArgs e)
    {
        if (_pointer == null || e.InitialPressMouseButton != MouseButton.Left) return;
        var pointer = _pointer;
        _pointer = null;
        pointer.Capture(null);
        _snap.Reset();
        ClearGuides();
        if (_region != _origin) _commit(_region);
        e.Handled = true;
    }

    private void CancelDrag()
    {
        _snap.Reset();
        ClearGuides();
        if (_pointer == null) return;
        var pointer = _pointer;
        _pointer = null;
        _region = _origin;
        pointer.Capture(null);
        ApplyBounds();
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopObservingPreviews();
        CancelDrag();
        _disposed = true;
        _native.Dispose();
        Close();
    }
}
