using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private Action? _cancelMenuDrag;
    private ScrollViewer? _menuOrderScroll;
    private string _menuEditorTab = "Order";
    private bool _menuNarrow;

    private void RenderThumbnailMenu()
    {
        var body = new StackPanel { Spacing = 10, Name = "thumbnail-menu-editor" };
        if (_theme.Legacy)
        {
            body.Children.Add(Text("Thumbnail right-click menu", 16, _theme.Text, true));
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (string tab in new[] { "Order", "Theme" })
            {
                var button = ActionButton(tab, () => { _menuEditorTab = tab; RenderPage(true); }, "menu-tab-" + tab, _menuEditorTab == tab);
                button.Padding = new Thickness(12, 4); tabs.Children.Add(button);
            }
            body.Children.Add(tabs);
            body.Children.Add(_menuEditorTab == "Order" ? MenuOrderEditor() : MenuThemeEditor());
            Put(_legacyCanvas!, body, 12, 12, 302, 386);
        }
        else
        {
            Heading("Thumbnail right-click menu", "Drag to arrange actions and dividers. Changes save for all profiles.");
            _menuNarrow = Bounds.Width < 1000;
            var panels = new Grid { ColumnDefinitions = new ColumnDefinitions(_menuNarrow ? "*" : "*,280"),
                RowDefinitions = new RowDefinitions(_menuNarrow ? "Auto,Auto" : "Auto") };
            panels.Children.Add(Card(MenuOrderEditor()));
            var theme = Card(MenuThemeEditor());
            theme.Margin = _menuNarrow ? new Thickness(0, 12, 0, 0) : new Thickness(12, 0, 0, 0);
            theme.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(theme, _menuNarrow ? 0 : 1); Grid.SetRow(theme, _menuNarrow ? 1 : 0);
            panels.Children.Add(theme); body.Children.Add(panels); _page.Children.Add(body);
        }
    }

    private Control MenuOrderEditor()
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(Text("First action = double right-click. Use + to insert a divider below an action.", 12, _theme.Muted));
        var order = ThumbnailMenuActions.Normalize(_snapshot.ThumbnailMenuOrder);
        double offset = _menuOrderScroll?.Offset.Y ?? 0;
        double rowHeight = _theme.Legacy ? 30 : 34;
        double rowSpacing = _theme.Legacy ? 2 : 4;
        var rowsPanel = new StackPanel { Spacing = rowSpacing, Margin = new Thickness(0, 0, 12, 0) };
        var scroll = new ScrollViewer { Name = "menu-order-scroll", Content = rowsPanel,
            Height = Math.Min(order.Count * (rowHeight + rowSpacing) - rowSpacing, _theme.Legacy ? 222 : 340),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _menuOrderScroll = scroll;
        var rows = new List<Control>(); var markers = new List<Border>();
        int number = 0;
        for (int i = 0; i < order.Count; i++)
        {
            string id = order[i]; string label = ThumbnailMenuActions.Label(id);
            bool divider = ThumbnailMenuActions.IsDivider(id);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("18,20,*,Auto,Auto,Auto"), Height = rowHeight, Name = "menu-order-row-" + id };
            if (i == 0) row.Background = B(_theme.AccentSurface);
            var handle = new Border { Name = "menu-order-drag-" + id, Tag = id, Focusable = true,
                Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.SizeAll), Child = Text("≡", 16, _theme.Muted) };
            AutomationProperties.SetName(handle, "Drag " + label);
            ToolTip.SetTip(handle, "Drag to move " + label + ". Ctrl+Up/Down also moves it.");
            row.Children.Add(handle);
            var index = Text(divider ? "" : (++number).ToString(), 11, _theme.Muted);
            Grid.SetColumn(index, 1); row.Children.Add(index);
            Control text = Text(label, _theme.Legacy ? 11 : 12, divider ? _theme.Muted : _theme.Text, i == 0);
            if (divider)
                text = new Border { BorderBrush = B(_theme.Border), BorderThickness = new Thickness(0, 1), Padding = new Thickness(4, 1),
                    VerticalAlignment = VerticalAlignment.Center, Child = text };
            Grid.SetColumn(text, 2); row.Children.Add(text);
            for (int direction = -1; direction <= 1; direction += 2)
            {
                bool up = direction < 0;
                var button = MenuSmallButton(up ? "↑" : "↓", new("thumbnail-menu-move", id, Position: i + direction),
                    "menu-order-" + (up ? "up-" : "down-") + id, "Move " + label + (up ? " up" : " down"));
                button.IsEnabled = ThumbnailMenuActions.CanMove(order, id, i + direction);
                Grid.SetColumn(button, up ? 3 : 4); row.Children.Add(button);
            }
            var divide = MenuSmallButton(divider ? "×" : "+", new(divider ? "thumbnail-menu-divider-remove" : "thumbnail-menu-divider-add", id),
                (divider ? "menu-divider-remove-" : "menu-divider-add-") + id, divider ? "Remove divider" : "Insert divider below " + label);
            divide.IsEnabled = divider || i < order.Count - 1 && !ThumbnailMenuActions.IsDivider(order[i + 1]);
            Grid.SetColumn(divide, 5); row.Children.Add(divide);
            var marker = new Border { Height = 2, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            var container = new Grid { Children = { row, marker } };
            rows.Add(container); markers.Add(marker); rowsPanel.Children.Add(container);
            WireMenuDrag(handle, row, i, order, scroll, rows, markers);
            int source = i;
            handle.KeyDown += async (_, e) =>
            {
                if (e.KeyModifiers != KeyModifiers.Control || e.Key is not (Key.Up or Key.Down)) return;
                int target = source + (e.Key == Key.Up ? -1 : 1); e.Handled = true;
                if (ThumbnailMenuActions.CanMove(order, id, target)) { await Run(new("thumbnail-menu-move", id, Position: target)); FocusMenuRow(id); }
            };
        }
        body.Children.Add(scroll);
        scroll.Offset = new Vector(0, offset);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var reset = CommandButton("Reset order", new("thumbnail-menu-reset"), "menu-order-reset");
        reset.IsEnabled = !order.SequenceEqual(ThumbnailMenuActions.DefaultOrder);
        buttons.Children.Add(reset);
        buttons.Children.Add(ActionButton("Back", () => Navigate("Appearance"), "menu-order-back"));
        body.Children.Add(buttons);
        return body;
    }

    private Button MenuSmallButton(string label, WorkspaceCommand command, string name, string description)
    {
        var button = CommandButton(label, command, name);
        button.Padding = new Thickness(0); button.Width = _theme.Legacy ? 25 : 30; button.Height = _theme.Legacy ? 26 : 30;
        button.Margin = new Thickness(2, 0, 0, 0);
        AutomationProperties.SetName(button, description); ToolTip.SetTip(button, description);
        return button;
    }

    private Control MenuThemeEditor()
    {
        var body = new StackPanel { Spacing = 8 };
        if (!_theme.Legacy) body.Children.Add(Text("Menu theme", 14, _theme.Text, true));
        var ids = new[] { ThumbnailMenuThemes.FollowApp }.Concat(ThumbnailMenuThemes.All.Select(p => p.Id)).ToArray();
        var picker = new ComboBox { Name = "menu-theme", ItemsSource = new[] { "Follow application theme" }.Concat(ThumbnailMenuThemes.All.Select(p => p.Name)).ToArray(),
            SelectedIndex = Math.Max(0, Array.IndexOf(ids, _snapshot.ThumbnailMenuTheme)), MaxDropDownHeight = 300,
            HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
        AutomationProperties.SetName(picker, "Thumbnail menu theme");
        picker.SelectionChanged += async (_, _) => { if (picker.SelectedIndex >= 0) await Run(new("thumbnail-menu-theme", Value: ids[picker.SelectedIndex])); };
        body.Children.Add(picker);
        var palette = ThumbnailMenuThemes.Resolve(_snapshot.ThumbnailMenuTheme, _snapshot.Theme);
        body.Children.Add(Text(palette.Description, 12, _theme.Muted));
        var preview = new StackPanel { Spacing = 0 };
        foreach (var id in ThumbnailMenuActions.Normalize(_snapshot.ThumbnailMenuOrder))
        {
            bool first = preview.Children.Count == 0;
            preview.Children.Add(ThumbnailMenuActions.IsDivider(id)
                ? new Border { Height = 1, Background = B(palette.Border), Margin = new Thickness(8, 4) }
                : new Border { Background = B(first ? palette.Selection : palette.Background), Padding = new Thickness(10, 5),
                    Child = Text(ThumbnailMenuActions.Label(id), 12, palette.Foreground) });
        }
        body.Children.Add(new Border { Name = "thumbnail-menu-preview", Background = B(palette.Background), BorderBrush = B(palette.Border),
            BorderThickness = new Thickness(1), Padding = new Thickness(2), Child = new ScrollViewer { Content = preview,
                MaxHeight = _theme.Legacy ? 180 : 340, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } });
        body.Children.Add(Text("Menu preview · first action highlighted", 11, _theme.Muted));
        if (_theme.Legacy) body.Children.Add(ActionButton("Back", () => Navigate("Appearance"), "menu-order-back"));
        return body;
    }

    private void FocusMenuRow(string id) => this.GetVisualDescendants().OfType<Control>()
        .FirstOrDefault(c => c.Name == "menu-order-drag-" + id)?.Focus();

    private void WireMenuDrag(Border handle, Grid row, int source, IReadOnlyList<string> order,
        ScrollViewer scroll, List<Control> rows, List<Border> markers)
    {
        IPointer? pointer = null;
        Point start = default, current = default;
        bool moved = false; int destination = source;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
        void Clear() { foreach (var marker in markers) marker.Background = Brushes.Transparent; }
        void Cancel()
        {
            timer.Stop(); var captured = pointer; pointer = null; _cancelMenuDrag = null;
            row.Opacity = 1; Clear();
            if (captured?.Captured == handle) captured.Capture(null);
        }
        void Locate()
        {
            Clear(); if (!moved) return;
            int insertion = rows.Count;
            for (int n = 0; n < rows.Count; n++)
                if (rows[n].TranslatePoint(default, scroll) is { } p && current.Y < p.Y + rows[n].Bounds.Height / 2) { insertion = n; break; }
            destination = insertion > source ? insertion - 1 : insertion;
            if (destination == source) return;
            var marker = markers[Math.Min(insertion, markers.Count - 1)];
            marker.VerticalAlignment = insertion == markers.Count ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            marker.Background = B(ThumbnailMenuActions.CanMove(order, order[source], destination) ? _theme.Accent : _theme.Danger);
        }
        timer.Tick += (_, _) =>
        {
            if (!moved || current.X < 0 || current.X > scroll.Bounds.Width) return;
            double step = current.Y < 24 ? -12 : current.Y > scroll.Bounds.Height - 24 ? 12 : 0;
            if (step != 0) scroll.Offset = new Vector(0, Math.Clamp(scroll.Offset.Y + step, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
            Locate();
        };
        handle.PointerPressed += (_, e) =>
        {
            if (_busy || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            _cancelMenuDrag?.Invoke(); pointer = e.Pointer; start = current = e.GetPosition(scroll); moved = false;
            handle.Focus(); pointer.Capture(handle); _cancelMenuDrag = Cancel; timer.Start(); e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (pointer != e.Pointer) return;
            current = e.GetPosition(scroll);
            if (!moved && Math.Abs(current.Y - start.Y) + Math.Abs(current.X - start.X) >= 5) { moved = true; row.Opacity = .55; }
            Locate(); e.Handled = true;
        };
        handle.PointerReleased += async (_, e) =>
        {
            if (pointer != e.Pointer) return;
            current = e.GetPosition(scroll); Locate();
            bool commit = moved && destination != source && new Rect(scroll.Bounds.Size).Contains(current);
            int target = destination; Cancel(); e.Handled = true;
            if (commit && ThumbnailMenuActions.Normalize(_backend.Read().ThumbnailMenuOrder).SequenceEqual(order))
            {
                await Run(new("thumbnail-menu-move", order[source], Position: target)); FocusMenuRow(order[source]);
            }
            else RefreshFromBackend();
        };
        handle.PointerCaptureLost += (_, _) => { if (pointer is not null) { Cancel(); RefreshFromBackend(); } };
        handle.DetachedFromVisualTree += (_, _) => { if (pointer is not null) Cancel(); };
    }
}
