using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EveOPreview.UI;

/// <summary>Reusable compact editors. Values stay in the caller's draft/save flow.</summary>
public static class WorkspacePickers
{
    public static ComboBox FontFamily(IEnumerable<string> families, string selected, string name, string accessibleName, string? inheritLabel = null)
    {
        var choices = families.Prepend(selected).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Length > 0).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var picker = new ComboBox { Name = name, ItemsSource = inheritLabel is null ? choices : choices.Prepend("").ToArray(),
            SelectedItem = selected, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32, MaxDropDownHeight = 260,
            FlowDirection = FlowDirection.LeftToRight,
            ItemTemplate = new FuncDataTemplate<string>((item, _) => new TextBlock { Text = string.IsNullOrEmpty(item) ? inheritLabel : item,
                FontSize = 12, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis }) };
        AutomationProperties.SetName(picker, accessibleName);
        ScrollViewer.SetVerticalScrollBarVisibility(picker, ScrollBarVisibility.Visible);
        ScrollViewer.SetHorizontalScrollBarVisibility(picker, ScrollBarVisibility.Disabled);
        ScrollViewer.SetAllowAutoHide(picker, false);
        picker.TemplateApplied += (_, args) =>
        {
            if (args.NameScope.Find<Popup>("PART_Popup") is { } popup)
                popup.Opened += (_, _) =>
                {
                    if (popup.Child is not Control child) return;
                    foreach (var scroll in child.GetVisualDescendants().OfType<ScrollViewer>())
                    { scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Visible; scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; scroll.AllowAutoHide = false; }
                };
        };
        return picker;
    }

    public static Control Search(AutoCompleteBox picker, IEnumerable choices, string accessibleName,
        Func<object?, string>? display = null, string? buttonName = null)
    {
        var row = new Grid { ColumnDefinitions = new("*,32") };
        row.Children.Add(picker);
        var list = new ListBox { Name = picker.Name + "-choices", ItemsSource = choices, MaxHeight = 260,
            ItemTemplate = new FuncDataTemplate<object>((item, _) => new TextBlock
                { Text = display?.Invoke(item) ?? item?.ToString() ?? "", FontSize = 12, TextWrapping = TextWrapping.Wrap }) };
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Visible);
        ScrollViewer.SetAllowAutoHide(list, false);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        list.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is { } scroll)
            {
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                scroll.AllowAutoHide = false;
            }
        };
        var flyout = new Flyout { Content = list, Placement = PlacementMode.BottomEdgeAlignedLeft };
        bool opening = false;
        void Browse()
        {
            picker.IsDropDownOpen = false;
            opening = true;
            try { list.SelectedItem = picker.SelectedItem ?? choices.Cast<object>().FirstOrDefault(x => x.ToString() == picker.Text); }
            finally { opening = false; }
            // Fluent caps the presenter width. A wider explicit child is centred
            // and clipped, cutting off the beginning of long item/font names.
            list.Width = Math.Clamp(row.Bounds.Width - 24, 240, 400);
            flyout.ShowAt(row);
            foreach (var scroll in list.GetVisualDescendants().OfType<ScrollViewer>())
            {
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                scroll.AllowAutoHide = false;
            }
            if (list.SelectedItem is not null) list.ScrollIntoView(list.SelectedItem);
            list.Focus();
        }
        var browse = new Button { Content = "⌄", Name = buttonName ?? picker.Name + "-browse", Padding = new(4),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        browse.Click += (_, _) => Browse();
        FlyoutBase.SetAttachedFlyout(browse, flyout);
        AutomationProperties.SetName(browse, accessibleName);
        list.SelectionChanged += (_, _) =>
        {
            if (opening || list.SelectedItem is not { } selected) return;
            picker.SelectedItem = selected;
            picker.Text = selected.ToString();
            flyout.Hide();
        };
        picker.KeyDown += (_, e) =>
        {
            if (e.Key == Key.F4 || e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            { Browse(); e.Handled = true; }
        };
        ScrollViewer.SetVerticalScrollBarVisibility(picker, ScrollBarVisibility.Visible);
        ScrollViewer.SetAllowAutoHide(picker, false);
        Grid.SetColumn(browse, 1); row.Children.Add(browse);
        return row;
    }

    public static ColorPicker Color(Color initial, string name, string accessibleName)
    {
        var picker = Configure(new ColorPicker { Name = name, Color = initial, HorizontalAlignment = HorizontalAlignment.Stretch });
        AutomationProperties.SetName(picker, accessibleName);
        return picker;
    }

    public static void ShowColor(Control anchor, Color initial, Action<Color> changed)
    {
        var view = Configure(new ColorView { Name = "workspace-color-popup", Color = initial });
        view.ColorChanged += (_, e) => changed(e.NewColor);
        new Flyout { Content = view }.ShowAt(anchor);
    }

    private static T Configure<T>(T view) where T : ColorView
    {
        view.IsAlphaEnabled = false; view.IsAlphaVisible = false; view.IsAccentColorsVisible = false;
        view.IsColorModelVisible = false; view.IsColorComponentsVisible = true; view.IsHexInputVisible = true;
        view.ColorModel = ColorModel.Rgba;
        view.IsComponentSliderVisible = true; view.IsComponentTextInputVisible = true;
        view.Palette = null; // Prevent the theme's default Fluent palette from replacing these colours.
        view.PaletteColumnCount = 6;
        view.PaletteColors = new[]
        {
            "#FFFFFF", "#D4E8FF", "#DFDFDC", "#AAB5C5", "#566170", "#000000",
            "#FF7666", "#FF9990", "#FF8A5B", "#FFD166", "#C58C5D", "#8A5D3B",
            "#72C9FF", "#66AAFF", "#A6C8E3", "#8FE5B9", "#7DCFB6", "#ADFF2F",
            "#AA44FF", "#AC9CFF", "#E995AE", "#FFCC66", "#7EA5FF", "#EE3344"
        }.Select(Avalonia.Media.Color.Parse).ToArray();
        view.FlowDirection = FlowDirection.LeftToRight;
        return view;
    }
}
