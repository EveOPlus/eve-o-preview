using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private WorkspaceLocalization _localization = new("en");
    private TextBlock RawText(string value, double size = 13, string? color = null, bool bold = false,
        HorizontalAlignment align = HorizontalAlignment.Stretch, Thickness? margin = null) => new()
    {
        Text = value, FontSize = size, Foreground = B(color ?? _theme.Text),
        FontWeight = bold ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = align, Margin = margin ?? default, FlowDirection = Avalonia.Media.FlowDirection.LeftToRight
    };
    private string L(string english) => _theme.Legacy ? english : _localization.Get(english);
    private string F(FormattableString text) => _theme.Legacy ? text.ToString(System.Globalization.CultureInfo.InvariantCulture) : _localization.Format(text);

    private void AddLanguageSettings()
    {
        if (_theme.Legacy) return;
        var languages = new[] { new WorkspaceLocalization.Language("auto", L("Automatic (Windows language)")) }
            .Concat(WorkspaceLocalization.Languages).ToArray();
        var selector = new ComboBox
        {
            Name = "ui-language", ItemsSource = languages, MinWidth = 230, MaxWidth = 460,
            HorizontalAlignment = HorizontalAlignment.Stretch, FlowDirection = Avalonia.Media.FlowDirection.LeftToRight,
            ItemTemplate = new FuncDataTemplate<WorkspaceLocalization.Language>((language, _) => RawText(language?.NativeName ?? ""))
        };
        selector.SelectedItem = languages.FirstOrDefault(x => x.Code == WorkspaceLocalization.NormalizePreference(_snapshot.UiLanguage)) ?? languages[0];
        Avalonia.Automation.AutomationProperties.SetName(selector, L("Language"));
        selector.SelectionChanged += async (_, _) =>
        {
            if (selector.SelectedItem is WorkspaceLocalization.Language language && language.Code != _snapshot.UiLanguage)
                await Run(new("language", Value: language.Code));
        };
        _page.Children.Add(Card(new StackPanel { Spacing = 9, Children =
        {
            Text("Language", 16, _theme.Text, true),
            Text("Choose the language for Light and Dark. Applies immediately to every profile; your unapplied edits are kept.", 12, _theme.Muted),
            selector
        } }));
    }
}
