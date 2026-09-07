namespace EveOPreview.UI;

/// <summary>Stable preference identities shared by menu editors and platform adapters.</summary>
public static class ThumbnailMenuActions
{
    public static IReadOnlyList<string> ActionIds { get; } =
        Array.AsReadOnly(new[] { "minimize", "minimize-all", "skip-cycling", "move", "resize" });
    public static IReadOnlyList<string> DefaultOrder { get; } =
        Array.AsReadOnly(new[] { "minimize", "minimize-all", "divider:minimize", "skip-cycling", "divider:skip", "move", "resize" });

    public static bool IsDivider(string? id) => id is "divider:default" or "divider:minimize" or "divider:skip"
        || (id?.StartsWith("divider:", StringComparison.Ordinal) == true && Guid.TryParseExact(id[8..], "N", out _));

    public static string Label(string id) => id switch
    {
        "minimize" => "Minimize",
        "minimize-all" => "Minimize all clients",
        "skip-cycling" => "Skip / resume cycling",
        "move" => "Move thumbnail",
        "resize" => "Resize thumbnail",
        _ => IsDivider(id) ? "Divider" : id
    };

    // Older or hand-edited settings cannot hide actions. New actions join the end.
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? order)
    {
        if (order is null) return DefaultOrder;
        var result = new List<string>();
        foreach (var id in order.Concat(ActionIds).Distinct(StringComparer.Ordinal))
        {
            if (ActionIds.Contains(id)) result.Add(id);
            else if (IsDivider(id) && result.Count > 0) result.Add(id);
        }
        while (result.Count > 0 && IsDivider(result[^1])) result.RemoveAt(result.Count - 1);
        return result.AsReadOnly();
    }

    public static bool CanMove(IReadOnlyList<string> order, string id, int destination)
    {
        if (!order.Contains(id) || destination < 0 || destination >= order.Count) return false;
        var next = order.ToList(); next.Remove(id); next.Insert(destination, id);
        return next.SequenceEqual(Normalize(next));
    }
}
