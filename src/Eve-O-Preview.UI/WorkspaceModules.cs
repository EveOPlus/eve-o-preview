using Avalonia.Controls;

namespace EveOPreview.UI;

/// <summary>A new feature supplies its own page without adding platform services to the shell.</summary>
public sealed record WorkspaceModule(string Id, string Title, string Description,
    Func<IWorkspaceBackend, Control> CreateView);

public static class WorkspaceModules
{
    // Planned modules intentionally have no credential, network or simulated data operations.
    public static IReadOnlyList<string> ReservedIds { get; } = new[] { "Characters", "Dps" };
}
