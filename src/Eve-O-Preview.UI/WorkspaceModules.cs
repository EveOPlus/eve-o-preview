using Avalonia.Controls;

namespace EveOPreview.UI;

/// <summary>A new feature supplies its own page without adding platform services to the shell.</summary>
public sealed record WorkspaceModule(string Id, string Title, string Description,
    Func<IWorkspaceBackend, Control> CreateView, IWorkspaceModuleDrafts? Drafts = null)
{
    public IReadOnlyList<WorkspaceModuleSearchTarget> SearchTargets { get; init; } = [];
}

/// <summary>A searchable section can prepare its retained navigation before the shell opens the module.</summary>
public sealed record WorkspaceModuleSearchTarget(string Id, string Title, IReadOnlyList<string> Keywords, Action PrepareNavigation);

public interface IWorkspaceModuleDrafts
{
    bool HasUnappliedEdits { get; }
    void Discard();
}
public interface IWorkspaceLiveModule
{
    void RefreshWorkspace();
}

public static class WorkspaceModules
{
    // Planned modules intentionally have no credential, network or simulated data operations.
    public static IReadOnlyList<string> ReservedIds { get; } = new[] { "Characters", "Dps" };
}
