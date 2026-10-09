using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EveOPreview.UI;

/// <summary>A single desktop-pixel transform preserves display positions and proportions.</summary>
public sealed class RegionMonitorMap : Canvas
{
    private readonly WorkspaceTheme _theme;
    private readonly Func<string, string> _localize;
    private RegionSnapshot? _state;
    private string _selected = "";
    public event Action<string>? RegionSelected;

    public RegionMonitorMap(WorkspaceTheme theme, Func<string, string> localize)
    {
        _theme = theme; _localize = localize;
        Name = "region-monitor-map"; Height = 220; ClipToBounds = true;
        Background = WorkspaceTheme.Brush(theme.Inset);
        SizeChanged += (_, _) => DrawLayout();
    }

    public void Update(RegionSnapshot state, string selected)
    {
        if (_state != null && _selected == selected && _state.Enabled == state.Enabled
            && _state.Regions.SequenceEqual(state.Regions) && (_state.Monitors ?? []).SequenceEqual(state.Monitors ?? [])) return;
        _state = state; _selected = selected; DrawLayout();
    }

    private void DrawLayout()
    {
        Children.Clear();
        if (_state == null) return;
        var monitors = (_state.Monitors ?? []).Where(m => m.Width > 0 && m.Height > 0).ToArray();
        if (monitors.Length == 0)
        {
            var empty = Label(_localize("No displays available"));
            SetLeft(empty, 16); SetTop(empty, 16); Children.Add(empty); return;
        }
        double left = monitors.Min(m => m.X), top = monitors.Min(m => m.Y);
        double right = monitors.Max(m => (double)m.X + m.Width), bottom = monitors.Max(m => (double)m.Y + m.Height);
        double scale = Math.Min(Math.Max(1, Bounds.Width - 32) / (right - left), (Height - 32) / (bottom - top));
        double originX = (Bounds.Width - (right - left) * scale) / 2, originY = (Height - (bottom - top) * scale) / 2;
        Rect Map(int x, int y, int width, int height) => new(originX + (x - left) * scale, originY + (y - top) * scale, width * scale, height * scale);
        void Place(Control control, Rect bounds)
        {
            SetLeft(control, bounds.X); SetTop(control, bounds.Y); control.Width = bounds.Width; control.Height = bounds.Height;
            Children.Add(control);
        }
        for (int index = 0; index < monitors.Length; index++)
        {
            var monitor = monitors[index];
            var bounds = Map(monitor.X, monitor.Y, monitor.Width, monitor.Height);
            var screen = new Border { Name = "region-monitor-" + index, Background = WorkspaceTheme.Brush(_theme.Surface),
                BorderBrush = WorkspaceTheme.Brush(monitor.Primary ? _theme.Accent : _theme.Muted), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3) };
            ToolTip.SetTip(screen, monitor.Name);
            Place(screen, bounds);
            // The desktop bounds already contain rotation. Mark the display's physical
            // top edge to distinguish flipped displays without rotating its regions twice.
            var marker = new Border { Name = "region-monitor-top-" + index, Background = WorkspaceTheme.Brush(_theme.Muted), IsHitTestVisible = false };
            Rect mark = monitor.RotationDegrees switch
            {
                90 => new(bounds.Right - 4, bounds.Center.Y - 8, 3, 16),
                180 => new(bounds.Center.X - 8, bounds.Bottom - 4, 16, 3),
                270 => new(bounds.X + 1, bounds.Center.Y - 8, 3, 16),
                _ => new(bounds.Center.X - 8, bounds.Y + 1, 16, 3)
            };
            Place(marker, mark);
            var label = Label((index + 1).ToString()); label.IsHitTestVisible = false;
            Place(label, new(bounds.X + 6, bounds.Y + 6, 24, 20));
        }
        // Selected regions come last so overlapping templates remain visible.
        foreach (var region in _state.Regions.OrderBy(region => region.Id == _selected))
        {
            bool selected = region.Id == _selected;
            var bounds = Map(region.X, region.Y, region.Width, region.Height);
            var button = new Button { Name = "region-map-" + region.Id, Padding = new Thickness(2),
                FontSize = 11, MinWidth = 0, MinHeight = 0, ClipToBounds = true, HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(1),
                Background = WorkspaceTheme.Brush(_state.Enabled ? selected ? "#E6FFDA00" : "#99FFDA00" : "#998B9099"),
                Foreground = Brushes.Black, BorderBrush = WorkspaceTheme.Brush(selected ? "#FFF4A6" : "#D6AA00"),
                BorderThickness = new Thickness(selected ? 2 : 1) };
            if (bounds.Width >= 60 && bounds.Height >= 22)
                button.Content = new TextBlock { Text = region.Name, TextTrimming = TextTrimming.CharacterEllipsis };
            AutomationProperties.SetName(button, region.Name);
            ToolTip.SetTip(button, region.Name);
            button.Click += (_, _) => RegionSelected?.Invoke(region.Id);
            Place(button, bounds);
        }
    }

    private TextBlock Label(string text) => new() { Text = text, FontSize = 12, Foreground = WorkspaceTheme.Brush(_theme.Muted) };
}
