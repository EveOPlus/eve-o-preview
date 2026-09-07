using Avalonia;
using Avalonia.Themes.Fluent;

namespace EveOPreview.UI;

public sealed class WorkspaceApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
