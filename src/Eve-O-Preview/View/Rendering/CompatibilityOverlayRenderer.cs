using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EveOPreview.Preview;

namespace EveOPreview.View.Rendering;

/// <summary>
/// Compatibility presentation uses the same Windows glyph rasterizer as native
/// composition. Changed, cropped glyph assets are retained; tint intensity only
/// changes brush opacity. Title flashes retain the former blended glyph colour
/// without changing its alpha. Avalonia owns the transparent surface and recovery.
/// </summary>
internal sealed class CompatibilityOverlayRenderer : Canvas, IOverlayRenderer
{
    private readonly Border _tint = new() { IsHitTestVisible = false };
    private readonly Image _title = new() { IsHitTestVisible = false, Stretch = Stretch.Fill };
    private readonly Image _stats = new() { IsHitTestVisible = false, Stretch = Stretch.Fill };
    private OverlayScene _scene = new();
    private PreviewSize _size;
    private bool _disposed, _initialized;
    private double _scale = 1;
    private TopLevel _topLevel;
    private OverlaySceneRasterizer.Asset _titleAsset, _statsAsset;

    public CompatibilityOverlayRenderer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Children.Add(_tint); Children.Add(_title); Children.Add(_stats);
        AttachedToVisualTree += (_, _) =>
        {
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null) _topLevel.ScalingChanged += ScalingChanged;
            UpdateScale();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_topLevel != null) _topLevel.ScalingChanged -= ScalingChanged;
            _topLevel = null;
        };
    }
    public OverlayCapabilities Capabilities => OverlayCapabilities.Title | OverlayCapabilities.CycleMarker | OverlayCapabilities.Stats;
    public void Resize(PreviewSize size)
    {
        if (_size == size) return;
        _size = size;
        UpdateAssets(true, true);
        UpdateScale();
    }
    public void SetScene(OverlayScene scene)
    {
        bool titleChanged = !_initialized || !TitleEquals(_scene, scene)
            || _scene.EffectiveTitleColor != scene.EffectiveTitleColor;
        bool statsChanged = !_initialized || _scene.Font != scene.Font || _scene.StatsStyle != scene.StatsStyle || !_scene.Stats.SequenceEqual(scene.Stats)
            || _scene.Title != scene.Title || _scene.Subtitle != scene.Subtitle || _scene.TitlePosition != scene.TitlePosition
            || _scene.SubtitlePlacement != scene.SubtitlePlacement || _scene.SubtitleFontSize != scene.SubtitleFontSize
            || _scene.ShowTitle != scene.ShowTitle || _scene.CycleSkipped != scene.CycleSkipped;
        _scene = scene;
        _initialized = true;
        UpdateAssets(titleChanged, statsChanged);
        _title.Opacity = 1;
        _tint.Background = scene.DamageTint is { } color ? new SolidColorBrush(Color.FromUInt32(color)) : null;
        _tint.Opacity = scene.DamageFlashIntensity;
    }
    public void SetOpacity(double opacity) => Opacity = opacity;
    public void SetVisible(bool visible) => IsVisible = visible;
    public void ShowAlert(PreviewAlert alert) { } // Compatibility graphics has no synthetic compositor alerts.
    public void ClearAlerts() { }
    private void ScalingChanged(object sender, EventArgs args) => UpdateScale();
    private void UpdateScale()
    {
        _scale = _topLevel?.RenderScaling ?? 1;
        Width = Math.Max(0, _size.Width) / _scale;
        Height = Math.Max(0, _size.Height) / _scale;
        // Compatibility selection uses an inset DWM image with its frame on the
        // underlying host. Tint only that image rectangle, preserving every edge.
        var bounds = _scene.AlertBounds ?? new PreviewRect(0, 0, _size.Width, _size.Height);
        int left = Math.Clamp(bounds.X, 0, Math.Max(0, _size.Width));
        int top = Math.Clamp(bounds.Y, 0, Math.Max(0, _size.Height));
        int right = (int)Math.Clamp((long)bounds.X + Math.Max(0, bounds.Width), left, Math.Max(left, _size.Width));
        int bottom = (int)Math.Clamp((long)bounds.Y + Math.Max(0, bounds.Height), top, Math.Max(top, _size.Height));
        SetLeft(_tint, left / _scale); SetTop(_tint, top / _scale);
        _tint.Width = (right - left) / _scale; _tint.Height = (bottom - top) / _scale;
        Position(_title, _titleAsset); Position(_stats, _statsAsset);
    }
    private void Position(Image image, OverlaySceneRasterizer.Asset asset)
    {
        if (asset == null) return;
        SetLeft(image, asset.X / _scale); SetTop(image, asset.Y / _scale);
        image.Width = asset.Bitmap.Width / _scale; image.Height = asset.Bitmap.Height / _scale;
    }
    private void UpdateAssets(bool titleChanged, bool statsChanged)
    {
        if (_disposed || _size.Width <= 0 || _size.Height <= 0) return;
        if (titleChanged)
        {
            Replace(_title, ref _titleAsset, OverlaySceneRasterizer.RenderAsset(_scene, _size, drawStats: false));
        }
        if (statsChanged) Replace(_stats, ref _statsAsset, OverlaySceneRasterizer.RenderAsset(_scene, _size, drawTitle: false));
        UpdateScale();
    }
    private void Replace(Image image, ref OverlaySceneRasterizer.Asset asset, OverlaySceneRasterizer.Asset replacement)
    {
        var bitmap = replacement == null ? null : WindowsBitmap.Copy(replacement.Bitmap);
        var previous = image.Source as Bitmap;
        image.Source = bitmap;
        previous?.Dispose(); asset?.Dispose(); asset = replacement;
        Position(image, asset);
    }
    private static bool TitleEquals(OverlayScene a, OverlayScene b) => a.Font == b.Font && a.Title == b.Title && a.ShowTitle == b.ShowTitle
        && a.CycleSkipped == b.CycleSkipped && a.MarkerStyle == b.MarkerStyle && a.MarkerColor == b.MarkerColor
        && a.Subtitle == b.Subtitle && a.SubtitleColor == b.SubtitleColor && a.SubtitlePlacement == b.SubtitlePlacement
        && a.SubtitleFontSize == b.SubtitleFontSize && a.TitleColor == b.TitleColor && a.TitlePosition == b.TitlePosition
        && a.StatsStyle == b.StatsStyle && a.Stats.SequenceEqual(b.Stats);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_topLevel != null) _topLevel.ScalingChanged -= ScalingChanged;
        foreach (var image in new[] { _title, _stats }) { (image.Source as Bitmap)?.Dispose(); image.Source = null; }
        _titleAsset?.Dispose(); _statsAsset?.Dispose();
    }
}
