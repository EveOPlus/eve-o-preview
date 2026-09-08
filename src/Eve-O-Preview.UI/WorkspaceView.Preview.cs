using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private readonly List<PreviewSurface> _previewSurfaces = new();
    private DispatcherTimer? _previewTimer;
    private TextBlock? _previewScaleText;
    private TextBlock? _previewStatusText;
    private string _previewCharacter = "EVE - Sample Name";
    private bool _previewCharacterEdited;
    private WorkspaceClientStill? _previewStill;
    private Bitmap? _previewStillBitmap;
    private bool _previewCapturePending;
    private readonly HashSet<string> _previewCaptureAttempts = new(StringComparer.Ordinal);
    private bool _previewActive = true;
    private bool _previewSkipped;
    private bool _previewActualSize = true;
    private string? _previewRenderError;
    private int _previewPixelWidth = 384, _previewPixelHeight = 216;
    private WorkspacePreviewImage? _lastPreviewImage;

    private static bool IsCharacterTitle(string title) => title.StartsWith("EVE - ", StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(title[6..]);

    private void UpdatePreviewCharacter()
    {
        if (_previewCharacterEdited) return;
        var online = _snapshot.Clients.Select(client => client.Title).Where(IsCharacterTitle).ToArray();
        var offline = (_snapshot.SavedClientTitles ?? []).Concat(_snapshot.CycleGroups.SelectMany(group => group.Clients))
            .Where(IsCharacterTitle).ToArray();
        _previewCharacter = online.FirstOrDefault(title => title == _previewCharacter) ?? online.FirstOrDefault()
            ?? offline.FirstOrDefault(title => title == _previewCharacter) ?? offline.FirstOrDefault() ?? "EVE - Sample Name";
    }

    private string PreviewBackgroundDescription => _previewCapturePending ? "Taking a still from an open client..."
        : _previewStill is not null ? "Still image: " + _previewStill.Title + "."
        : "No client image available; showing the title and border.";

    private async void CapturePreviewStill(bool refresh = false)
    {
        if (_disposed || _theme.Legacy || _previewCapturePending || _backend is not IWorkspacePreviewCapture capture
            || (!refresh && _previewStill is not null)) return;
        var online = _snapshot.Clients.Select(client => client.Title).Where(IsCharacterTitle).ToArray();
        if (online.Length == 0 || (!refresh && online.All(_previewCaptureAttempts.Contains))) return;
        _previewCaptureAttempts.UnionWith(online);
        _previewCapturePending = true;
        UpdatePreviewEditorStatus();
        try
        {
            var still = await capture.CapturePreviewStillAsync(_previewCharacter);
            if (_disposed || still is null) return;
            using var stream = new MemoryStream(still.Png, false);
            var bitmap = new Bitmap(stream);
            _previewStillBitmap?.Dispose();
            _previewStillBitmap = bitmap;
            _previewStill = still;
        }
        catch { /* A sample capture is optional; retain the last usable image and editable title. */ }
        finally
        {
            _previewCapturePending = false;
            if (!_disposed) UpdateFontPreview();
        }
    }

    // Used by both workspace layouts. The available rectangle affects only display scale;
    // the renderer always receives the configured thumbnail dimensions and raw font values.
    private Control BuildTitlePreview(double width, double height)
    {
        var surface = new PreviewSurface(UpdatePreviewScale)
        {
            FlowDirection = FlowDirection.LeftToRight, Name = "title-preview", Width = width > 0 ? width : double.NaN, Height = height,
            Background = B(_theme.Legacy ? "#F0F0F0" : "#101925"), ClipToBounds = true,
            BorderBrush = B(_theme.Border), BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(surface, L("Preview of character title, position and active border"));
        surface.SizeChanged += (_, _) => UpdatePreviewScale();
        surface.ActualSize = _theme.Legacy || _previewActualSize;
        surface.FitWidthOnly = !_theme.Legacy;
        surface.HidePanBars = _theme.Legacy;
        _previewSurfaces.Add(surface);
        if (_lastPreviewImage is not null)
        {
            using var stream = new MemoryStream(_lastPreviewImage.Png, false);
            surface.SetBitmap(new Bitmap(stream));
        }
        UpdateFontPreview();
        CapturePreviewStill();
        return surface;
    }

    private void AddFontPreview() => _page.Children.Add(BuildTitlePreview(0, 160));

    private IReadOnlyDictionary<string, string> PreviewSettings()
    {
        var settings = new Dictionary<string, string>(_snapshot.Settings, StringComparer.Ordinal);
        foreach (var draft in _drafts) settings[draft.Key] = draft.Value;
        return settings;
    }

    private void SchedulePreview()
    {
        _previewTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _previewTimer.Tick -= PreviewTick;
        _previewTimer.Tick += PreviewTick;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void PreviewTick(object? sender, EventArgs args) { _previewTimer?.Stop(); UpdateFontPreview(); }

    private void UpdateFontPreview()
    {
        if (_disposed || _previewSurfaces.Count == 0) return;
        var settings = PreviewSettings();
        try
        {
            foreach (var definition in SettingCatalog.All.Where(s => s.Page is "Thumbnail" or "Overlay"))
                if (settings.TryGetValue(definition.Key, out var value) && SettingCatalog.Validate(EffectiveDefinition(definition), value, L) is { } error)
                    throw new ArgumentException(L(definition.Label) + ": " + error);
            if (_backend is IWorkspacePreviewRenderer renderer)
            {
                var rendered = renderer.RenderPreview(new WorkspacePreviewRequest(settings, _theme.Legacy ? "Sample" : _previewCharacter, !_theme.Legacy && _previewActive, _theme.Legacy ? "#F0F0F0" : "#101925", _previewSkipped, _theme.Legacy ? null : _previewStill?.Png));
                _lastPreviewImage = rendered;
                _previewPixelWidth = rendered.Width;
                _previewPixelHeight = rendered.Height;
                foreach (var surface in _previewSurfaces)
                {
                    using var stream = new MemoryStream(rendered.Png, false);
                    var bitmap = new Bitmap(stream);
                    surface.SetBitmap(bitmap);
                }
            }
            else
            {
                _previewPixelWidth = int.TryParse(settings.GetValueOrDefault("ThumbnailWidth"), out var width) ? Math.Clamp(width, 1, 1920) : 384;
                _previewPixelHeight = int.TryParse(settings.GetValueOrDefault("ThumbnailHeight"), out var height) ? Math.Clamp(height, 1, 1080) : 216;
                foreach (var surface in _previewSurfaces)
                    surface.SetFallback(new PortablePreviewCanvas(settings, _theme.Legacy ? "Sample" : _previewCharacter, !_theme.Legacy && _previewActive, _theme.Legacy ? "#F0F0F0" : "#101925", _previewSkipped, _theme.Legacy ? null : _previewStillBitmap) { Width = _previewPixelWidth, Height = _previewPixelHeight });
            }
            _previewRenderError = null;
        }
        catch (Exception ex) { _previewRenderError = ex.Message; }
        UpdatePreviewScale();
        UpdatePreviewEditorStatus();
    }

    private void UpdatePreviewScale()
    {
        if (_previewSurfaces.Count == 0) return;
        var surface = _previewSurfaces[0];
        var scale = surface.DisplayScale(_previewPixelWidth, _previewPixelHeight);
        var description = F($"{_previewPixelWidth} × {_previewPixelHeight} px · { (scale >= .999 ? L("actual pixel size") : F($"displayed at {Math.Round(scale * 100)}%"))}");
        if (_previewScaleText is not null) _previewScaleText.Text = description;
        foreach (var item in _previewSurfaces) ToolTip.SetTip(item, description + ". " + L(_theme.Legacy ? "Title sample." : PreviewBackgroundDescription) + L(" Scroll to inspect content beyond this viewport."));
    }

    private void ReleaseTitlePreviews()
    {
        _previewTimer?.Stop();
        foreach (var surface in _previewSurfaces) surface.Dispose();
        _previewSurfaces.Clear();
        _previewScaleText = null;
        _previewStatusText = null;
        _previewApplyButton = null;
        _previewResetButton = null;
        _previewApplyStatus = null;
        _previewValidation.Clear();
    }

    private sealed class PreviewSurface : Border, IDisposable
    {
        private TopLevel? _root;
        private readonly Action _scaleChanged;
        private Bitmap? _bitmap;
        private readonly Image _image = new() { Name = "title-preview-image" };
        private readonly ScrollViewer _actualViewer = new() { Name = "title-preview-pan", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        private Viewbox? _fallback;
        private bool _actualSize = true;
        public bool FitWidthOnly { get; set; }
        public bool ActualSize { get => _actualSize; set { _actualSize = value; ConstrainBitmapSize(); } }
        public double DisplayScale(double width, double height)
        {
            if (_actualSize) return 1;
            var renderScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var widthScale = Math.Max(1, Bounds.Width - (FitWidthOnly ? 18 : 2)) * renderScale / width;
            return Math.Min(1, FitWidthOnly ? widthScale : Math.Min(widthScale, Math.Max(1, Bounds.Height - 2) * renderScale / height));
        }
        public bool HidePanBars
        {
            set
            {
                _actualViewer.HorizontalScrollBarVisibility = value ? Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
                _actualViewer.VerticalScrollBarVisibility = value ? Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            }
        }
        public PreviewSurface(Action scaleChanged)
        {
            _scaleChanged = scaleChanged;
            AttachedToVisualTree += (_, _) =>
            {
                _root = TopLevel.GetTopLevel(this);
                if (_root is not null) _root.ScalingChanged += OnRenderScalingChanged;
                ConstrainBitmapSize();
            };
            DetachedFromVisualTree += (_, _) => DetachRoot();
            SizeChanged += (_, _) => ConstrainBitmapSize();
        }
        private void OnRenderScalingChanged(object? sender, EventArgs e)
        {
            ConstrainBitmapSize();
            _scaleChanged();
        }
        private void DetachRoot()
        {
            if (_root is not null) _root.ScalingChanged -= OnRenderScalingChanged;
            _root = null;
        }
        public void SetBitmap(Bitmap bitmap)
        {
            var old = _bitmap;
            _bitmap = bitmap;
            _fallback = null;
            _image.Source = bitmap;
            ConstrainBitmapSize();
            old?.Dispose();
        }
        private void ConstrainBitmapSize()
        {
            if (_bitmap is null) { ConstrainFallbackSize(); return; }
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            if (_actualSize || FitWidthOnly)
            {
                var fit = DisplayScale(_bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
                _image.Width = _bitmap.PixelSize.Width * fit / scale; _image.Height = _bitmap.PixelSize.Height * fit / scale;
                _image.MaxWidth = double.PositiveInfinity; _image.MaxHeight = double.PositiveInfinity;
                _image.Stretch = Stretch.Fill;
                _image.HorizontalAlignment = HorizontalAlignment.Left; _image.VerticalAlignment = VerticalAlignment.Top;
                if (Child != _actualViewer)
                {
                    Child = null;
                    _actualViewer.Content = _image;
                    Child = _actualViewer;
                }
                else if (_actualViewer.Content != _image) _actualViewer.Content = _image;
            }
            else
            {
                _image.Width = double.NaN; _image.Height = double.NaN;
                _image.MaxWidth = _bitmap.PixelSize.Width / scale; _image.MaxHeight = _bitmap.PixelSize.Height / scale;
                _image.Stretch = Stretch.Uniform;
                _image.HorizontalAlignment = HorizontalAlignment.Center; _image.VerticalAlignment = VerticalAlignment.Center;
                if (Child != _image) { _actualViewer.Content = null; Child = _image; }
            }
        }
        public void SetFallback(Control canvas)
        {
            _bitmap?.Dispose(); _bitmap = null;
            _image.Source = null;
            _fallback = new Viewbox { Child = canvas };
            ConstrainFallbackSize();
        }
        private void ConstrainFallbackSize()
        {
            if (_fallback?.Child is not Control canvas) return;
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            Child = null; _actualViewer.Content = null;
            if (_actualSize || FitWidthOnly)
            {
                var fit = DisplayScale(canvas.Width, canvas.Height);
                _fallback.Width = canvas.Width * fit / scale; _fallback.Height = canvas.Height * fit / scale;
                _fallback.MaxWidth = double.PositiveInfinity; _fallback.MaxHeight = double.PositiveInfinity;
                _fallback.Stretch = Stretch.Fill;
                _fallback.HorizontalAlignment = HorizontalAlignment.Left; _fallback.VerticalAlignment = VerticalAlignment.Top;
                _actualViewer.Content = _fallback; Child = _actualViewer;
            }
            else
            {
                _fallback.Width = double.NaN; _fallback.Height = double.NaN;
                _fallback.MaxWidth = canvas.Width / scale; _fallback.MaxHeight = canvas.Height / scale;
                _fallback.Stretch = Stretch.Uniform;
                _fallback.HorizontalAlignment = HorizontalAlignment.Center; _fallback.VerticalAlignment = VerticalAlignment.Center;
                Child = _fallback;
            }
        }
        public void Dispose() { DetachRoot(); Child = null; _actualViewer.Content = null; _image.Source = null; _bitmap?.Dispose(); _bitmap = null; }
    }

    // The Windows host supplies the exact production renderer. Other hosts get a clearly
    // labelled illustrative fallback while they implement their platform's title renderer.
    private sealed class PortablePreviewCanvas(IReadOnlyDictionary<string, string> settings, string title, bool active, string background, bool skipped, Bitmap? still) : Control
    {
        public override void Render(DrawingContext context)
        {
            context.FillRectangle(B(background), Bounds.WithX(0).WithY(0));
            if (still is not null) context.DrawImage(still, new Rect(still.Size), new Rect(Bounds.Size));
            double Number(string key, double fallback) => double.TryParse(settings.GetValueOrDefault(key), CultureInfo.InvariantCulture, out var value) ? value : fallback;
            IBrush ColorBrush(string key, string fallback) => Color.TryParse(settings.GetValueOrDefault(key), out var color) ? new SolidColorBrush(color) : B(fallback);
            bool Enabled(string key) => bool.TryParse(settings.GetValueOrDefault(key), out var value) && value;
            var origin = new Point(Number("TitleFontOffsetLeft", 0), Number("TitleFontOffsetTop", 0));
            if (skipped)
            {
                double size = Math.Clamp(Math.Ceiling(Number("TitleFontSize", 14)), 12, 22);
                var box = new Rect(origin.X + 2, origin.Y + 3, size - 3, size - 3);
                var markerPen = new Pen(ColorBrush("CycleSkipIndicatorColor", "#FF0000"), 2);
                switch (settings.GetValueOrDefault("CycleSkipIndicatorStyle"))
                {
                    case "Pause":
                        context.DrawLine(markerPen, box.TopLeft + new Vector(2, 0), box.BottomLeft + new Vector(2, 0));
                        context.DrawLine(markerPen, box.TopRight - new Vector(2, 0), box.BottomRight - new Vector(2, 0)); break;
                    case "Cross": context.DrawLine(markerPen, box.TopLeft, box.BottomRight); context.DrawLine(markerPen, box.TopRight, box.BottomLeft); break;
                    default: context.DrawEllipse(null, markerPen, box); context.DrawLine(markerPen, box.TopLeft + new Vector(2, 2), box.BottomRight - new Vector(2, 2)); break;
                }
                origin += new Vector(size + 5, 0);
            }
            if (Enabled("ShowThumbnailOverlays"))
            {
                var style = settings.GetValueOrDefault("TitleFontStyle", "Regular");
                var face = new Typeface(new FontFamily(settings.GetValueOrDefault("TitleFontName", "Arial")), style.Contains("Italic") ? FontStyle.Italic : FontStyle.Normal, style.Contains("Bold") ? FontWeight.Bold : FontWeight.Normal);
                var text = new FormattedText(title.StartsWith("EVE - ", StringComparison.Ordinal) ? title[6..] : title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, Math.Clamp(Number("TitleFontSize", 14), 1, 200), ColorBrush("TitleFontForeColor", "#FFFFFF"));
                if (text.BuildGeometry(origin) is { } geometry)
                {
                    var outline = Number("TitleFontOutlineWidth", 0);
                    if (outline > 0) context.DrawGeometry(null, new Pen(ColorBrush("TitleFontOutlineColor", "#000000"), outline), geometry);
                    context.DrawGeometry(ColorBrush("TitleFontForeColor", "#FFFFFF"), null, geometry);
                }
            }
            if (active && Enabled("EnableActiveClientHighlight"))
            {
                var thickness = Math.Clamp(Number("ActiveClientHighlightThickness", 3), 1, 6);
                context.DrawRectangle(null, new Pen(ColorBrush("ActiveClientHighlightColor", "#809DFF"), thickness), new Rect(thickness / 2, thickness / 2, Math.Max(0, Bounds.Width - thickness), Math.Max(0, Bounds.Height - thickness)));
            }
        }
    }
}
