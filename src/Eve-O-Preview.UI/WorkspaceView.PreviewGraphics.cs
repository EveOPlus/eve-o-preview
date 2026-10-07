using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private string? _previewAlertTarget;

    private void BuildPreviewGraphicsEditor(Panel parent)
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(Text("Preview graphics", 14, _theme.Text, true));
        var picker = new ComboBox
        {
            Name = "setting-PreviewOverlayRenderer",
            ItemsSource = new[] { "NativeComposition", "Legacy" },
            ItemTemplate = new FuncDataTemplate<string>((value, _) => Text(value == "Legacy" ? "Compatibility graphics" : "Enhanced graphics")),
            SelectedItem = DraftValue("PreviewOverlayRenderer", "NativeComposition"),
            HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32,
            Foreground = B(_theme.Text), Background = B(_theme.Surface),
        };
        AutomationProperties.SetName(picker, L("Preview graphics"));
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is string renderer) StagePreviewSetting("PreviewOverlayRenderer", renderer);
        };
        content.Children.Add(picker);
        content.Children.Add(Text("Enhanced graphics supports animated alerts. Compatibility graphics uses the existing title renderer. This choice applies to every profile.", 11, _theme.Muted));
        var titles = _snapshot.Clients.Select(client => client.Title).ToArray();
        if (_previewAlertTarget is null || !titles.Contains(_previewAlertTarget)) _previewAlertTarget = titles.FirstOrDefault();
        content.Children.Add(Text("Test on this character", 11, _theme.Muted));
        var target = new ComboBox
        {
            Name = "preview-alert-target", ItemsSource = titles, SelectedItem = _previewAlertTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32,
            Foreground = B(_theme.Text), Background = B(_theme.Surface),
            ItemTemplate = new FuncDataTemplate<string>((title, _) =>
            {
                var text = RawText(title ?? "");
                text.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
                text.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                ToolTip.SetTip(text, title);
                return text;
            }),
        };
        AutomationProperties.SetName(target, L("Test on this character"));
        target.SelectionChanged += (_, _) => _previewAlertTarget = target.SelectedItem as string;
        content.Children.Add(target);
        var test = ActionButton("Test visual alert", async () => await Run(new("preview-test-alert", Target: _previewAlertTarget ?? "")), "preview-test-alert");
        test.IsEnabled = titles.Length > 0 && _snapshot.Settings.GetValueOrDefault("PreviewOverlayRenderer", "NativeComposition") == "NativeComposition";
        ToolTip.SetTip(test, L("Show a synthetic flash on the selected preview. This does not detect combat events."));
        content.Children.Add(test);
        content.Children.Add(Text("Apply changes before testing. The test uses a synthetic alert; combat detection is not enabled.", 11, _theme.Muted));
        parent.Children.Add(Card(content, new Thickness(14)));
    }
}
