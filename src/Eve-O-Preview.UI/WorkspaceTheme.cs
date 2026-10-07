using Avalonia.Media;

namespace EveOPreview.UI;

public sealed record WorkspaceTheme(string Name, string Background, string Sidebar, string Surface,
    string Inset, string Border, string Text, string Muted, string Accent, string AccentSurface, string Positive,
    string Danger, double Radius)
{
    public bool Legacy => Name == "Legacy";
    public static WorkspaceTheme Get(string? name) => name switch
    {
        "Light" => new("Light", "#F4F6FB", "#FFFFFF", "#FFFFFF", "#F4F6FB", "#DFE5EF", "#20283B", "#66728A", "#4D60CE", "#E9EDFF", "#18795A", "#BE3548", 12),
        "Legacy" => new("Legacy", "#F0F0F0", "#E8E8E8", "#FFFFFF", "#F5F5F5", "#B8B8B8", "#202020", "#585858", "#1E61AA", "#DCEBFA", "#247142", "#B62333", 2),
        _ => new("Dark", "#10141E", "#141925", "#1B2231", "#151C29", "#2C3549", "#ECF0FA", "#A2AFC5", "#9CABFF", "#2B3657", "#7ED6B4", "#FF9BA6", 12),
    };
    public static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
