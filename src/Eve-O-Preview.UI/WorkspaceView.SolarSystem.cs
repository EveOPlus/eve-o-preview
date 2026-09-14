using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private Control TitlePositionField()
    {
        var choices = SettingCatalog.Find("TitlePosition")!.Options!;
        var input = new ComboBox { Name = "setting-TitlePosition", ItemsSource = choices, SelectedItem = DraftValue("TitlePosition", "TopLeft"),
            HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 280,
            ItemTemplate = new FuncDataTemplate<string>((value, _) => Text(AugmentLabels.Position(Enum.Parse<EveOPreview.Preview.OverlayPosition>(value!), L))) };
        ScrollViewer.SetVerticalScrollBarVisibility(input, ScrollBarVisibility.Visible);
        ScrollViewer.SetAllowAutoHide(input, false);
        input.SelectionChanged += (_, _) => { if (input.SelectedItem is string value) StagePreviewSetting("TitlePosition", value); };
        return LabeledPreviewField("TitlePosition", "Title position", input);
    }
    private Control BuildSolarSystemEditor()
    {
        var panel = new StackPanel { Name = "preview-solar-system", Spacing = 10 };
        panel.Children.Add(Text("Current solar system", 13, _theme.Text, true));
        panel.Children.Add(PreviewToggle("ShowCurrentSolarSystem", "Show system name"));
        var placement = new ComboBox
        {
            Name = "setting-SolarSystemPlacement", ItemsSource = SettingCatalog.Find("SolarSystemPlacement")!.Options,
            SelectedItem = DraftValue("SolarSystemPlacement", "Below"), HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 200, ItemTemplate = new FuncDataTemplate<string>((value, _) => Text(value ?? ""))
        };
        AutomationProperties.SetName(placement, L("Placement"));
        ScrollViewer.SetVerticalScrollBarVisibility(placement, ScrollBarVisibility.Visible);
        ScrollViewer.SetAllowAutoHide(placement, false);
        placement.SelectionChanged += (_, _) => { if (placement.SelectedItem is string value) StagePreviewSetting("SolarSystemPlacement", value); };
        panel.Children.Add(LabeledPreviewField("SolarSystemPlacement", "Placement", placement));
        decimal.TryParse(DraftValue("SolarSystemFontSize", "0"), CultureInfo.InvariantCulture, out var saved);
        decimal.TryParse(DraftValue("TitleFontSize", "14"), CultureInfo.InvariantCulture, out var titleSize);
        var inherit = new CheckBox { Name = "solar-system-use-title-size", Content = L("Use title size"), IsChecked = saved == 0, FontSize = 12 };
        var size = new NumericUpDown { Name = "setting-SolarSystemFontSize", Minimum = 1, Maximum = 200,
            Value = saved > 0 ? saved : Math.Clamp(titleSize, 1, 200), Increment = 1, FormatString = "0.##",
            NumberFormat = CultureInfo.InvariantCulture.NumberFormat, IsEnabled = saved > 0,
            FlowDirection = Avalonia.Media.FlowDirection.LeftToRight };
        AutomationProperties.SetName(size, L("Font size (pixels)"));
        size.ValueChanged += (_, _) => { if (inherit.IsChecked != true) StagePreviewSetting("SolarSystemFontSize", (size.Value ?? 1).ToString(CultureInfo.InvariantCulture)); };
        inherit.IsCheckedChanged += (_, _) =>
        {
            size.IsEnabled = inherit.IsChecked != true;
            StagePreviewSetting("SolarSystemFontSize", inherit.IsChecked == true ? "0" : (size.Value ?? 1).ToString(CultureInfo.InvariantCulture));
        };
        panel.Children.Add(inherit);
        panel.Children.Add(LabeledPreviewField("SolarSystemFontSize", "Font size (pixels)", size));
        panel.Children.Add(ColorField("SolarSystemColor", "Solar system text"));
        return panel;
    }
}
