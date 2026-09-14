using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EveOPreview.Preview;

namespace EveOPreview.UI.Previews;

/// <summary>A compact sample of the same filled/stroked symbols used by thumbnails.</summary>
public sealed class OverlaySymbolPreview : Control
{
    private readonly List<(Geometry Geometry, bool Filled)> _geometry = [];
    public OverlaySymbol Symbol { get; private set; }
    public Color Color { get; private set; }

    public OverlaySymbolPreview(OverlaySymbol symbol, Color color)
    {
        Width = Height = 32;
        ClipToBounds = true;
        Update(symbol, color);
    }

    public void Update(OverlaySymbol symbol, Color color)
    {
        if (symbol != Symbol || _geometry.Count == 0)
        {
            _geometry.Clear();
            void Add(float[] points, bool filled)
            {
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(new Point(points[0], points[1]), filled);
                    for (int i = 2; i < points.Length; i += 2) path.LineTo(new Point(points[i], points[i + 1]));
                    path.EndFigure(filled);
                }
                _geometry.Add((geometry, filled));
            }
            foreach (var polygon in OverlaySymbols.Fills(symbol)) Add(polygon, true);
            foreach (var stroke in OverlaySymbols.Strokes(symbol)) Add(stroke, false);
        }
        Symbol = symbol;
        Color = color;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // A neutral thumbnail-like background keeps pale icons legible in both themes.
        context.DrawRectangle(new SolidColorBrush(Avalonia.Media.Color.Parse("#202632")), null, new Rect(Bounds.Size), 4, 4);
        using var transform = context.PushTransform(Matrix.CreateScale(1.5, 1.5) * Matrix.CreateTranslation(4, 4));
        var brush = new SolidColorBrush(Color);
        foreach (var (geometry, filled) in _geometry)
        {
            context.DrawGeometry(null, new Pen(Brushes.Black, 3, lineJoin: PenLineJoin.Round), geometry);
            context.DrawGeometry(filled ? brush : null, filled ? null : new Pen(brush, 1.5, lineJoin: PenLineJoin.Round), geometry);
        }
    }
}
