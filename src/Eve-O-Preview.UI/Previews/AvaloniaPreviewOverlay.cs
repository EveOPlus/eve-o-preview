using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using EveOPreview.Preview;

namespace EveOPreview.UI.Previews;

/// <summary>
/// Portable overlay candidate. Scene drawing is retained; finite animations run in the
/// Avalonia compositor without a UI timer. The platform host owns capture, windows and input.
/// </summary>
public sealed class AvaloniaPreviewOverlay : Panel, IOverlayRenderer
{
    private readonly SceneCanvas _sceneCanvas = new();
    private readonly AlertCanvas _alertCanvas = new();
    private readonly Border _damageTint = new() { IsVisible = false, IsHitTestVisible = false };
    private uint? _damageColor;
    private PreviewSize _pixelSize;
    private TopLevel? _root;
    private bool _disposed;
    private double _renderScale = 1;

    public AvaloniaPreviewOverlay()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        Children.Add(_damageTint);
        Children.Add(_alertCanvas);
        Children.Add(_sceneCanvas);
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    public OverlayCapabilities Capabilities => OverlayCapabilities.Title | OverlayCapabilities.CycleMarker
        | OverlayCapabilities.Stats | OverlayCapabilities.Pulse | OverlayCapabilities.Shake
        | OverlayCapabilities.CompositorAnimations;

    // Counts UI drawing operations, not GPU presentations or compositor animation frames.
    public long SceneRenderCount => _sceneCanvas.RenderCount;
    public long SceneUpdateCount => _sceneCanvas.UpdateCount;

    public void Resize(PreviewSize size)
    {
        ThrowIfDisposed();
        size = new PreviewSize(Math.Max(0, size.Width), Math.Max(0, size.Height));
        if (_pixelSize == size) return;
        _pixelSize = size;
        UpdateScale();
    }

    public void SetScene(OverlayScene scene)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scene);
        if (_damageColor != scene.DamageTint)
        {
            _damageColor = scene.DamageTint;
            _damageTint.Background = scene.DamageTint is { } tint ? Brush(tint) : null;
            _damageTint.IsVisible = scene.DamageTint.HasValue;
        }
        _alertCanvas.SetAlertBounds(scene.AlertBounds);
        _damageTint.Opacity = Math.Clamp(scene.DamageFlashIntensity, 0, 1);
        _sceneCanvas.SetScene(scene);
    }

    public void SetOpacity(double opacity)
    {
        ThrowIfDisposed();
        Opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;
    }

    public void SetVisible(bool visible)
    {
        ThrowIfDisposed();
        if (!visible) ClearAlerts();
        IsVisible = visible;
    }

    public void ShowAlert(PreviewAlert alert)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(alert);
        if (!IsEffectivelyVisible || _root is null) return;
        var pulseVisual = _alertCanvas.Visual;
        if (pulseVisual is null) return;

        alert = alert.Normalize();
        ClearAlerts();
        pulseVisual.Visible = true;
        _alertCanvas.SetColor(alert.Color);
        var pulse = pulseVisual.Compositor.CreateScalarKeyFrameAnimation();
        pulse.Duration = TimeSpan.FromSeconds(alert.DurationSeconds);
        pulse.IterationCount = 1;
        pulse.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        float peak = (float)alert.Intensity;
        if (alert.ReducedMotion)
        {
            pulse.InsertKeyFrame(0, peak);
            pulse.InsertKeyFrame(0.999f, peak);
        }
        else
        {
            pulse.InsertKeyFrame(0, peak);
        }
        pulse.InsertKeyFrame(1, 0);
        pulseVisual.StartAnimation(nameof(CompositionVisual.Opacity), pulse);

        if (alert.ShakePixels <= 0) return;
        var shake = pulseVisual.Compositor.CreateVector3DKeyFrameAnimation();
        shake.Duration = pulse.Duration;
        shake.IterationCount = 1;
        shake.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        var origin = new Vector3D(0, 0, 0);
        shake.InsertKeyFrame(0, origin);
        // Only alert graphics move. Titles, stats, capture and desktop geometry stay fixed.
        int shakeSteps = Math.Max(12, (int)Math.Ceiling(alert.DurationSeconds * 36));
        for (int step = 1; step < shakeSteps; step++)
        {
            double time = alert.DurationSeconds * step / shakeSteps;
            double amount = alert.ShakePixels / _renderScale * Math.Sin(time * Math.PI * 18);
            shake.InsertKeyFrame(step / (float)shakeSteps, origin + new Vector3D(amount, 0, 0));
        }
        shake.InsertKeyFrame(1, origin);
        pulseVisual.StartAnimation(nameof(CompositionVisual.Offset), shake);
    }

    public void ClearAlerts()
    {
        if (_alertCanvas.Visual is { } pulse)
        {
            pulse.Visible = false;
            pulse.StopAnimation(nameof(CompositionVisual.Opacity));
            pulse.Opacity = 0;
            pulse.StopAnimation(nameof(CompositionVisual.Offset));
            pulse.Offset = new Vector3D(0, 0, 0);
        }
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _root = TopLevel.GetTopLevel(this);
        if (_root is not null) _root.ScalingChanged += OnScalingChanged;
        UpdateScale();
        ClearAlerts();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        ClearAlerts();
        if (_root is not null) _root.ScalingChanged -= OnScalingChanged;
        _root = null;
    }

    private void OnScalingChanged(object? sender, EventArgs args) { ClearAlerts(); UpdateScale(); }

    private void UpdateScale()
    {
        _renderScale = _root?.RenderScaling ?? 1;
        Width = _pixelSize.Width / _renderScale;
        Height = _pixelSize.Height / _renderScale;
        _sceneCanvas.SetPixelSize(_pixelSize, _renderScale);
        _alertCanvas.SetPixelSize(_pixelSize, _renderScale);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        ClearAlerts();
        if (_root is not null) _root.ScalingChanged -= OnScalingChanged;
        _root = null;
        AttachedToVisualTree -= OnAttached;
        DetachedFromVisualTree -= OnDetached;
        Children.Clear();
        _sceneCanvas.Release();
        _disposed = true;
    }

    private static ImmutableSolidColorBrush Brush(uint argb) => new(Color.FromUInt32(argb));

    private sealed class AlertCanvas : Control
    {
        private uint _color = 0xFFFF4038;
        private PreviewSize _size;
        private PreviewRect? _alertBounds;
        private double _scale = 1;
        private CompositionContainerVisual? _clip;
        public CompositionCustomVisual? Visual { get; private set; }

        public AlertCanvas()
        {
            AttachedToVisualTree += (_, _) =>
            {
                var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
                if (compositor is null) return;
                Visual = compositor.CreateCustomVisual(new AlertDrawing());
                Visual.Opacity = 0;
                Visual.Visible = false;
                _clip = compositor.CreateContainerVisual();
                _clip.ClipToBounds = true;
                _clip.Children.Add(Visual);
                ElementComposition.SetElementChildVisual(this, _clip);
                UpdateBounds();
                UpdateAppearance();
            };
            DetachedFromVisualTree += (_, _) =>
            {
                if (Visual is not null)
                {
                    Visual.StopAnimation(nameof(CompositionVisual.Opacity));
                    Visual.StopAnimation(nameof(CompositionVisual.Offset));
                    Visual.Opacity = 0;
                }
                ElementComposition.SetElementChildVisual(this, null);
                _clip = null;
                Visual = null;
            };
        }

        public void SetColor(uint argb)
        {
            if (_color == argb) return;
            _color = argb;
            UpdateAppearance();
        }

        public void SetAlertBounds(PreviewRect? bounds)
        {
            if (_alertBounds == bounds) return;
            _alertBounds = bounds;
            UpdateBounds();
        }

        public void SetPixelSize(PreviewSize size, double scale)
        {
            if (_size == size && _scale == scale) return;
            bool scaleChanged = _scale != scale;
            _size = size; _scale = scale;
            UpdateBounds();
            if (scaleChanged) UpdateAppearance();
        }

        private void UpdateBounds()
        {
            if (Visual is null || _clip is null) return;
            var requested = _alertBounds ?? new PreviewRect(0, 0, _size.Width, _size.Height);
            // Long arithmetic keeps extreme producer bounds from overflowing before clipping.
            long left = Math.Clamp((long)requested.X, 0, _size.Width);
            long top = Math.Clamp((long)requested.Y, 0, _size.Height);
            long right = Math.Clamp((long)requested.X + Math.Max(0L, requested.Width), 0, _size.Width);
            long bottom = Math.Clamp((long)requested.Y + Math.Max(0L, requested.Height), 0, _size.Height);
            _clip.Offset = new Vector3D(left / _scale, top / _scale, 0);
            _clip.Size = new Vector(Math.Max(0, right - left) / _scale, Math.Max(0, bottom - top) / _scale);
            _clip.Visible = right > left && bottom > top;
            Visual.Size = _clip.Size;
        }

        private void UpdateAppearance() => Visual?.SendHandlerMessage(new AlertAppearance(_color, _scale));

        private sealed record AlertAppearance(uint Color, double Scale);

        private sealed class AlertDrawing : CompositionCustomVisualHandler
        {
            private ImmutableSolidColorBrush _color = Brush(0xFFFF4038);
            private ImmutableSolidColorBrush _tint = Brush(0x55FF4038);
            private ImmutablePen _border = new(Brush(0xFFFF4038), 3);
            private double _halfWidth = 1.5;

            public override void OnMessage(object message)
            {
                if (message is not AlertAppearance appearance) return;
                _color = Brush(appearance.Color);
                _tint = Brush((appearance.Color & 0x00FFFFFF) | ((appearance.Color >> 24) / 3 << 24));
                _border = new ImmutablePen(_color, 3 / appearance.Scale);
                _halfWidth = 1.5 / appearance.Scale;
                Invalidate();
            }

            public override void OnRender(ImmediateDrawingContext context)
            {
                if (EffectiveSize.X <= 0 || EffectiveSize.Y <= 0) return;
                context.FillRectangle(_tint, new Rect(0, 0, EffectiveSize.X, EffectiveSize.Y));
                context.DrawRectangle(_border, new Rect(_halfWidth, _halfWidth,
                    Math.Max(0, EffectiveSize.X - 2 * _halfWidth), Math.Max(0, EffectiveSize.Y - 2 * _halfWidth)));
            }
        }
    }

    private sealed class SceneCanvas : Control
    {
        private OverlayScene _scene = new();
        private Geometry? _title;
        private readonly List<(Geometry Geometry, ImmutableSolidColorBrush Brush)> _stats = [];
        private ImmutableSolidColorBrush _foreground = Brush(0xFFA9A9A9);
        private ImmutablePen? _outline;
        private ImmutablePen _marker = new(Brush(0xFFFF0000), 2);
        private static readonly ImmutablePen MarkerContrast = new(Brush(0xFF000000), 4);
        private static readonly ImmutablePen StatOutline = new(Brush(0xFF000000), 2, lineJoin: PenLineJoin.Round);
        private OverlayTitleLayout _titleLayout;
        private PreviewSize _size;
        private double _scale = 1;
        public long RenderCount { get; private set; }
        public long UpdateCount { get; private set; }

        public void SetScene(OverlayScene scene)
        {
            if (_scene == scene || (_scene with { Stats = scene.Stats, AlertBounds = scene.AlertBounds, DamageTint = scene.DamageTint } == scene
                && _scene.Stats.SequenceEqual(scene.Stats))) return;
            if (_scene with { DamageFlashIntensity = scene.DamageFlashIntensity, DamageTint = scene.DamageTint, Stats = scene.Stats } == scene
                && _scene.Stats.SequenceEqual(scene.Stats))
            {
                _scene = scene;
                _foreground = Brush(scene.EffectiveTitleColor);
                InvalidateVisual();
                return;
            }
            // Keep a bounded private snapshot; a producer cannot mutate retained drawing state.
            _scene = scene with { Stats = scene.Stats.Take(8).ToArray() };
            Rebuild();
            UpdateCount++;
            InvalidateVisual();
        }

        public void SetPixelSize(PreviewSize size, double scale)
        {
            if (_size == size && _scale == scale) return;
            bool resized = _size != size;
            _size = size; _scale = scale;
            if (resized) Rebuild();
            InvalidateVisual();
        }

        private void Rebuild()
        {
            var font = _scene.Font;
            double size = float.IsFinite(font.Size) ? Math.Clamp(font.Size, 1, 512) : 8.25;
            var face = new Typeface(new FontFamily(string.IsNullOrWhiteSpace(font.Family) ? "Consolas" : font.Family),
                font.Style.HasFlag(OverlayFontStyle.Italic) ? FontStyle.Italic : FontStyle.Normal,
                font.Style.HasFlag(OverlayFontStyle.Bold) ? FontWeight.Bold : FontWeight.Normal);
            _foreground = Brush(_scene.EffectiveTitleColor);
            _outline = float.IsFinite(font.OutlineWidth) && font.OutlineWidth > 0.1f
                ? new ImmutablePen(Brush(font.Outline), Math.Clamp(font.OutlineWidth, 0, 64), lineJoin: PenLineJoin.Round) : null;
            _marker = new ImmutablePen(Brush(_scene.MarkerColor), 2);
            var titleText = new FormattedText(_scene.Title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, _foreground);
            var subtitleBrush = Brush(_scene.SubtitleColor);
            var subtitleText = new FormattedText(_scene.Subtitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(font.Family), _scene.EffectiveSubtitleSize, subtitleBrush);
            var statsStyle = _scene.StatsStyle.FontStyle ?? font.Style;
            var statsFace = new Typeface(new FontFamily(_scene.StatsStyle.FontFamily ?? font.Family),
                statsStyle.HasFlag(OverlayFontStyle.Italic) ? FontStyle.Italic : FontStyle.Normal,
                statsStyle.HasFlag(OverlayFontStyle.Bold) ? FontWeight.Bold : FontWeight.Normal);
            float statSize = Math.Clamp(_scene.StatsStyle.FontSize, 8, 32);
            float Advance(string text) => (float)new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                statsFace, statSize, _foreground).WidthIncludingTrailingWhitespace;
            float Width(OverlayStat row)
            {
                float width = 0; bool first = true;
                foreach (var part in row.Segments())
                {
                    if (!first) width += statSize * (part.PrefixIcons ? .6f : 1);
                    width += part.Icons().Count() * _scene.StatsStyle.LineHeight + Advance(part.Text);
                    first = false;
                }
                return width;
            }
            var arranged = OverlayLayout.Arrange(_scene, _size, (float)titleText.WidthIncludingTrailingWhitespace,
                (float)subtitleText.WidthIncludingTrailingWhitespace, _scene.Stats.Where(x => x.Visible).Select(Width).DefaultIfEmpty(0).Max());
            _titleLayout = arranged.TitleLayout((float)titleText.WidthIncludingTrailingWhitespace, (float)subtitleText.WidthIncludingTrailingWhitespace);
            _title = null;
            if (_scene.ShowTitle && _scene.Title.Length != 0)
            {
                var origin = new Point(_titleLayout.TitleX, _titleLayout.TitleY);
                _title = TextGeometry(titleText, origin, font.Style, size);
            }
            _stats.Clear();
            _icons.Clear();
            if (_scene.Subtitle.Length > 0)
            {
                if (subtitleText.BuildGeometry(new Point(_titleLayout.SubtitleX, _titleLayout.SubtitleY)) is { } subtitle)
                    _stats.Add((subtitle, subtitleBrush));
            }
            for (int index = 0; index < _scene.Stats.Count; index++)
            {
                var stat = _scene.Stats[index];
                if (!stat.Visible) continue;
                double statX = arranged.StatsStyle.OffsetX;
                double statY = arranged.StatsStyle.StartY(_size.Height, _scene.Stats.Count) + _scene.StatsStyle.LineHeight * index;
                void Icon(OverlaySymbol symbol, uint color)
                {
                    if (symbol == OverlaySymbol.None) return;
                    double size = Math.Clamp(_scene.StatsStyle.FontSize, 8, 32);
                    foreach (float[] polygon in OverlaySymbols.Fills(symbol))
                    {
                        var geometry = new StreamGeometry();
                        using (var builder = geometry.Open())
                        {
                            builder.BeginFigure(new Point(statX + polygon[0] * size / 16, statY + polygon[1] * size / 16), true);
                            for (int point = 2; point < polygon.Length; point += 2)
                                builder.LineTo(new Point(statX + polygon[point] * size / 16, statY + polygon[point + 1] * size / 16));
                            builder.EndFigure(true);
                        }
                        _icons.Add((geometry, null, Brush(color)));
                    }
                    foreach (float[] stroke in OverlaySymbols.Strokes(symbol))
                    {
                        var geometry = new StreamGeometry();
                        using (var builder = geometry.Open())
                        {
                            builder.BeginFigure(new Point(statX + stroke[0] * size / 16, statY + stroke[1] * size / 16), false);
                            for (int point = 2; point < stroke.Length; point += 2)
                                builder.LineTo(new Point(statX + stroke[point] * size / 16, statY + stroke[point + 1] * size / 16));
                            builder.EndFigure(false);
                        }
                        _icons.Add((geometry, new ImmutablePen(Brush(color), 1.5, lineJoin: PenLineJoin.Round), null));
                    }
                    statX += _scene.StatsStyle.LineHeight;
                }
                bool first = true;
                foreach (var part in stat.Segments())
                {
                    double fontSize = Math.Clamp(_scene.StatsStyle.FontSize, 8, 32);
                    if (!first) statX += fontSize * .6;
                    bool prefix = first || part.PrefixIcons;
                    if (prefix) foreach (var icon in part.Icons()) Icon(icon.Symbol, icon.Color);
                    var brush = Brush(part.Color);
                    var text = new FormattedText(part.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, statsFace, fontSize, brush);
                    if (TextGeometry(text, new Point(statX, statY), statsStyle, fontSize) is { } geometry) _stats.Add((geometry, brush));
                    statX += text.WidthIncludingTrailingWhitespace;
                    if (!prefix) { statX += fontSize * .4; foreach (var icon in part.Icons()) Icon(icon.Symbol, icon.Color); }
                    first = false;
                }
            }
        }

        private static Geometry? TextGeometry(FormattedText text, Point origin, OverlayFontStyle style, double size)
        {
            var geometry = text.BuildGeometry(origin);
            // BuildGeometry omits decorations; title and DPS share the same vector strokes.
            if (!style.HasFlag(OverlayFontStyle.Underline) && !style.HasFlag(OverlayFontStyle.Strikeout)) return geometry;
            var decorated = new GeometryGroup();
            if (geometry is not null) decorated.Children.Add(geometry);
            double thickness = Math.Max(1, size / 14);
            if (style.HasFlag(OverlayFontStyle.Underline))
                decorated.Children.Add(new RectangleGeometry(new Rect(origin.X, origin.Y + text.Baseline + size * .08, text.Width, thickness)));
            if (style.HasFlag(OverlayFontStyle.Strikeout))
                decorated.Children.Add(new RectangleGeometry(new Rect(origin.X, origin.Y + text.Baseline - size * .3, text.Width, thickness)));
            return decorated;
        }

        private double MarkerSize => Math.Clamp(Math.Ceiling(float.IsFinite(_scene.Font.Size) ? _scene.Font.Size : 8.25), 12, 22);
        private readonly List<(Geometry Geometry, ImmutablePen? Pen, IBrush? Fill)> _icons = new();

        public override void Render(DrawingContext context)
        {
            RenderCount++;
            using var transform = context.PushTransform(Matrix.CreateScale(1 / _scale, 1 / _scale));
            if (_scene.CycleSkipped)
            {
                var box = new Rect(_titleLayout.MarkerX + 2, _titleLayout.MarkerY + 3, MarkerSize - 3, MarkerSize - 3);
                DrawMarker(context, MarkerContrast, box);
                DrawMarker(context, _marker, box);
            }
            if (_title is not null)
            {
                if (_outline is not null) context.DrawGeometry(null, _outline, _title);
                context.DrawGeometry(_foreground, null, _title);
            }
            foreach (var stat in _stats)
            {
                context.DrawGeometry(null, StatOutline, stat.Geometry);
                context.DrawGeometry(stat.Brush, null, stat.Geometry);
            }
            foreach (var icon in _icons)
            {
                context.DrawGeometry(null, StatOutline, icon.Geometry);
                context.DrawGeometry(icon.Fill, icon.Pen, icon.Geometry);
            }
        }

        private void DrawMarker(DrawingContext context, ImmutablePen pen, Rect box)
        {
            switch (_scene.MarkerStyle)
            {
                case CycleMarkerStyle.Pause:
                    context.DrawLine(pen, box.TopLeft + new Vector(2, 0), box.BottomLeft + new Vector(2, 0));
                    context.DrawLine(pen, box.TopRight - new Vector(2, 0), box.BottomRight - new Vector(2, 0));
                    break;
                case CycleMarkerStyle.Cross:
                    context.DrawLine(pen, box.TopLeft, box.BottomRight);
                    context.DrawLine(pen, box.TopRight, box.BottomLeft);
                    break;
                default:
                    context.DrawEllipse(null, pen, box);
                    context.DrawLine(pen, box.TopLeft + new Vector(2, 2), box.BottomRight - new Vector(2, 2));
                    break;
            }
        }

        public void Release() { _title = null; _stats.Clear(); _icons.Clear(); _scene = new(); }
    }
}
