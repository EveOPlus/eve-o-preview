namespace EveOPreview.UI;

public sealed record RegionItem(string Id, string Name, int X, int Y, int Width, int Height);
// Bounds are physical desktop pixels, already oriented by the display system.
public sealed record RegionMonitor(string Name, int X, int Y, int Width, int Height, int RotationDegrees, bool Primary);
public sealed record RegionSnapshot(IReadOnlyList<RegionItem> Regions,
    IReadOnlyDictionary<string, string> Assignments, bool Editing, string Error,
    int MinimumWidth, int MinimumHeight, int MaximumWidth, int MaximumHeight,
    bool Enabled = true, bool DragDocking = true, IReadOnlyList<RegionMonitor>? Monitors = null);

public interface IWorkspaceRegions
{
    RegionSnapshot ReadRegions();
    void StopRegionEditing();
}
