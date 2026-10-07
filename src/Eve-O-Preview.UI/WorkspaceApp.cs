using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Markup.Xaml.Styling;

namespace EveOPreview.UI;

public sealed class WorkspaceApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://EveOPreview.UI/"))
            { Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml") });
    }
}
