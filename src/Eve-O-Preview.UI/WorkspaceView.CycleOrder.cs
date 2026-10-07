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
    private int? _expandedOrderGroup;
    private ScrollViewer? _orderScroll;
    private Action? _cancelOrderDrag;
    private bool _orderNarrow;

    private Control OrderHeader(CycleGroupItem group)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Text("CHARACTER ORDER", 10, _theme.Muted, true));
        var expand = ActionButton("⛶", () => ExpandOrder(group.Id), "expand-cycle-order");
        expand.Padding = new Thickness(8, 3);
        ToolTip.SetTip(expand, L("Expand character order"));
        AutomationProperties.SetName(expand, L("Expand character order"));
        Grid.SetColumn(expand, 1); header.Children.Add(expand);
        return header;
    }

    private Control OrderList(CycleGroupItem group, bool expanded)
    {
        var offset = _orderScroll?.Offset ?? default;
        var members = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 18, 0) };
        var scroll = new ScrollViewer { Name = "group-members-scroll", Content = members,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        if (!expanded) scroll.MaxHeight = 240;
        var rows = new List<Control>();
        var indicators = new List<Border>();
        for (int i = 0; i < group.Clients.Count; i++)
        {
            int index = i;
            string title = group.Clients[i];
            bool skipped = group.SkippedClients?.Contains(title) == true;
            bool online = _snapshot.Clients.Any(c => c.Title == title);
            var row = new Grid { Name = "group-row-" + i, ColumnDefinitions = new ColumnDefinitions(_theme.Legacy ? "22,25,*,Auto" : "22,25,38,*,Auto"),
                Margin = new Thickness(0, 3), MinHeight = 42 };
            var handle = new Border { Name = "group-drag-" + i, Tag = title, Focusable = true,
                Cursor = new Cursor(StandardCursorType.SizeAll), Background = Brushes.Transparent,
                Child = Text("≡", 20, _theme.Muted), VerticalAlignment = VerticalAlignment.Stretch };
            AutomationProperties.SetName(handle, F($"Drag {title} to reorder; Control Up or Down also moves it"));
            ToolTip.SetTip(handle, L("Drag to reorder · Ctrl+↑ / Ctrl+↓"));
            row.Children.Add(handle);
            var number = Text((i + 1).ToString("00"), 11, _theme.Muted);
            Grid.SetColumn(number, 1); row.Children.Add(number);
            var label = RawText(title, 12, skipped ? _theme.Muted : _theme.Text, true);
            label.TextWrapping = TextWrapping.NoWrap;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            ToolTip.SetTip(label, title);
            var identity = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                Children = { label, Text(skipped ? "Skipped in all groups" : online ? "Running" : "Offline", 10,
                    skipped ? _theme.Danger : online ? _theme.Positive : _theme.Muted) } };
            if (!_theme.Legacy)
            {
                var portrait = CharacterPortrait(title, 30);
                Grid.SetColumn(portrait, 2); row.Children.Add(portrait);
            }
            Grid.SetColumn(identity, _theme.Legacy ? 2 : 3); row.Children.Add(identity);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            var skip = CommandButton(skipped ? "Resume" : "Skip", new("client-cycle-skip", title, (!skipped).ToString()), "group-skip-" + i);
            skip.MinWidth = 53;
            ToolTip.SetTip(skip, L("Skip or resume this character in every cycle group in this profile. Resets when EVE-O restarts."));
            AutomationProperties.SetName(skip, skipped ? F($"Resume {title} in all cycle groups") : F($"Skip {title} in all cycle groups"));
            var up = CommandButton("↑", new("group-client-up", group.Id.ToString(), title), "group-up-" + i);
            var down = CommandButton("↓", new("group-client-down", group.Id.ToString(), title), "group-down-" + i);
            up.IsEnabled = i > 0; down.IsEnabled = i < group.Clients.Count - 1;
            AutomationProperties.SetName(up, F($"Move {title} up"));
            AutomationProperties.SetName(down, F($"Move {title} down"));
            var remove = CommandButton("×", new("group-client-remove", group.Id.ToString(), title), "group-remove-" + i);
            ToolTip.SetTip(remove, F($"Remove {title} from this group"));
            AutomationProperties.SetName(remove, F($"Remove {title} from this group"));
            foreach (var action in new[] { skip, up, down, remove }) { action.Padding = new Thickness(6, 5); actions.Children.Add(action); }
            Grid.SetColumn(actions, _theme.Legacy ? 3 : 4); row.Children.Add(actions);
            var container = new Grid { Children = { row } };
            var indicator = new Border { Height = 2, Background = Brushes.Transparent, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
            container.Children.Add(indicator);
            rows.Add(container); indicators.Add(indicator); members.Children.Add(container);
            WireOrderDrag(handle, row, index, group, scroll, rows, indicators);
            handle.KeyDown += async (_, e) =>
            {
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key is not (Key.Up or Key.Down)) return;
                e.Handled = true;
                await Run(new(e.Key == Key.Up ? "group-client-up" : "group-client-down", group.Id.ToString(), title));
                FocusOrderTitle(title);
            };
        }
        if (group.Clients.Count == 0) members.Children.Add(Text("No characters yet. Add a running client or an offline character.", 12, _theme.Muted));
        _orderScroll = scroll;
        scroll.AttachedToVisualTree += (_, _) => scroll.Offset = offset;
        return scroll;
    }

    private void FocusOrderTitle(string title) => (_expandedOrderGroup is not null ? (Visual?)_confirmation : this)!.GetVisualDescendants().OfType<Border>()
        .FirstOrDefault(c => c.Name?.StartsWith("group-drag-", StringComparison.Ordinal) == true && Equals(c.Tag, title))?.Focus();

    private void WireOrderDrag(Border handle, Grid row, int source, CycleGroupItem group, ScrollViewer scroll,
        List<Control> rows, List<Border> indicators)
    {
        IPointer? pointer = null;
        Point start = default, current = default;
        bool moved = false;
        int destination = source;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
        string profile = _snapshot.ProfileName;
        void ClearIndicators() { foreach (var marker in indicators) marker.Background = Brushes.Transparent; }
        void Cancel()
        {
            timer.Stop();
            var captured = pointer; pointer = null;
            _cancelOrderDrag = null;
            row.Opacity = 1; ClearIndicators();
            if (captured?.Captured == handle) captured.Capture(null);
        }
        void LocateDrop()
        {
            ClearIndicators();
            if (!moved) return;
            int insertion = rows.Count;
            for (int n = 0; n < rows.Count; n++)
            {
                var origin = rows[n].TranslatePoint(default, scroll);
                if (origin is { } p && current.Y < p.Y + rows[n].Bounds.Height / 2) { insertion = n; break; }
            }
            destination = insertion > source ? insertion - 1 : insertion;
            if (destination == source) return;
            var marker = indicators[Math.Min(insertion, indicators.Count - 1)];
            marker.VerticalAlignment = insertion == indicators.Count ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            marker.Background = B(_theme.Accent);
        }
        timer.Tick += (_, _) =>
        {
            if (!moved || current.X < 0 || current.X > scroll.Bounds.Width) return;
            double step = current.Y < 28 ? -14 : current.Y > scroll.Bounds.Height - 28 ? 14 : 0;
            if (step != 0) scroll.Offset = new Vector(0, Math.Clamp(scroll.Offset.Y + step, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
            LocateDrop();
        };
        handle.PointerPressed += (_, e) =>
        {
            if (_busy || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            _cancelOrderDrag?.Invoke();
            pointer = e.Pointer; start = current = e.GetPosition(scroll); moved = false;
            handle.Focus(); pointer.Capture(handle); _cancelOrderDrag = Cancel;
            timer.Start(); e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (pointer != e.Pointer) return;
            current = e.GetPosition(scroll);
            if (!moved && Math.Abs(current.Y - start.Y) + Math.Abs(current.X - start.X) >= 5) { moved = true; row.Opacity = .55; }
            LocateDrop(); e.Handled = true;
        };
        handle.PointerReleased += async (_, e) =>
        {
            if (pointer != e.Pointer) return;
            current = e.GetPosition(scroll); LocateDrop();
            bool commit = moved && destination != source && new Rect(scroll.Bounds.Size).Contains(current);
            int target = destination;
            Cancel(); e.Handled = true;
            var latest = _backend.Read();
            var latestGroup = latest.CycleGroups.FirstOrDefault(g => g.Id == group.Id);
            if (commit && latest.ProfileName == profile && latestGroup?.Name == group.Name && latestGroup.Clients.SequenceEqual(group.Clients))
            {
                await Run(new("group-client-move", group.Id.ToString(), group.Clients[source], target));
                FocusOrderTitle(group.Clients[source]);
            }
            else RefreshFromBackend();
        };
        handle.PointerCaptureLost += (_, _) => { if (pointer is not null) { Cancel(); RefreshFromBackend(); } };
        handle.DetachedFromVisualTree += (_, _) => { if (pointer is not null) Cancel(); };
    }

    private void ExpandOrder(int groupId)
    {
        DismissConfirmation();
        _confirmationPreviousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        _expandedOrderGroup = groupId;
        _confirmation = new Border { Name = "expanded-cycle-order", Background = B(_theme.Surface), Padding = new Thickness(18) };
        Grid.SetColumnSpan(_confirmation, 2); _root.Children.Add(_confirmation);
        foreach (var child in _root.Children.Where(c => c != _confirmation)) child.IsEnabled = false;
        RenderExpandedOrder();
        this.GetVisualDescendants().OfType<Button>().FirstOrDefault(c => c.Name == "close-cycle-order")?.Focus();
    }

    private void RenderExpandedOrder()
    {
        if (_confirmation is null || _expandedOrderGroup is not int groupId) return;
        var group = _snapshot.CycleGroups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) { DismissConfirmation(); return; }
        var focusName = (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control)?.Name;
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(Text(F($"Character order · {group.Name}"), 20, _theme.Text, true));
        var close = ActionButton("↙ Done", DismissConfirmation, "close-cycle-order");
        AutomationProperties.SetName(close, L("Restore character order section"));
        Grid.SetColumn(close, 1); header.Children.Add(close); body.Children.Add(header);
        var hint = Text("Drag the handles to reorder. Skip keeps a character in place and pauses cycling to it in every group in this profile.", 12, _theme.Muted);
        hint.Margin = new Thickness(0, 0, 0, 12); Grid.SetRow(hint, 1); body.Children.Add(hint);
        var list = OrderList(group, true); Grid.SetRow(list, 2); body.Children.Add(list);
        var footer = Text(_messageError ? _message : "Order saves automatically. Skips last until EVE-O restarts. Escape returns to settings.", 11, _messageError ? _theme.Danger : _theme.Muted);
        footer.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(footer, 3); body.Children.Add(footer);
        KeyboardNavigation.SetTabNavigation(body, KeyboardNavigationMode.Cycle);
        _confirmation.Child = body;
        if (focusName is not null) body.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == focusName)?.Focus();
    }
}
