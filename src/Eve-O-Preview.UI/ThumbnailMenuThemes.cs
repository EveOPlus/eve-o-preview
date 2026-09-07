namespace EveOPreview.UI;

public sealed record ThumbnailMenuPalette(string Id, string Name, string Description,
    string Background, string Foreground, string Selection, string Border, string Muted);

/// <summary>Shared colors for the portable preview and native menu renderer.</summary>
public static class ThumbnailMenuThemes
{
    public const string FollowApp = "app";
    public static IReadOnlyList<ThumbnailMenuPalette> All { get; } = Array.AsReadOnly(new[]
    {
        new ThumbnailMenuPalette("light", "Light", "Clean paper and cool blue.", "#F8FAFC", "#16212E", "#E4EDFC", "#CCD4DF", "#64748B"),
        new ThumbnailMenuPalette("dark", "Dark", "Deep navy, matching the workspace.", "#151C29", "#E6EDF7", "#273A55", "#2D3D52", "#A3AFC3"),
        new ThumbnailMenuPalette("graphite", "Graphite", "Neutral charcoal with soft silver.", "#242424", "#F0F0F0", "#414141", "#626262", "#B1B1B1"),
        new ThumbnailMenuPalette("midnight", "Midnight", "Near-black blue with icy highlights.", "#0B1220", "#DBEAFE", "#163452", "#3E6587", "#96ACC4"),
        new ThumbnailMenuPalette("oled", "OLED Black", "Pure black with restrained gray.", "#000000", "#F3F4F6", "#252525", "#555555", "#A3A3A3"),
        new ThumbnailMenuPalette("nebula", "Nebula", "Violet shadows and lavender light.", "#1B1428", "#EEE5FF", "#43305C", "#805C9E", "#BBAACA"),
        new ThumbnailMenuPalette("eve-carbon", "EVE Carbon", "A dark capsuleer-console feel.", "#141719", "#DCE4E8", "#304049", "#607B87", "#A1B0B8"),
        new ThumbnailMenuPalette("amarr", "Amarr Gold", "Imperial gold on warm black.", "#211C12", "#F3D991", "#4B3C20", "#94783D", "#C0AD7C"),
        new ThumbnailMenuPalette("caldari", "Caldari Blue", "Steel blue and precise, cool contrast.", "#101D28", "#C8E8FF", "#23445C", "#4D809F", "#94B9D0"),
        new ThumbnailMenuPalette("gallente", "Gallente Green", "Emerald accents on deep green-gray.", "#121E1A", "#D3F2E2", "#284C3C", "#508B70", "#9CBDAD"),
        new ThumbnailMenuPalette("minmatar", "Minmatar Rust", "Burnished copper and industrial charcoal.", "#251814", "#FFDEC8", "#573628", "#AD7250", "#CFAD99")
    });

    private static readonly ThumbnailMenuPalette Legacy = new("legacy", "Legacy", "Classic dark and gold.",
        "#141416", "#D4AF37", "#39342A", "#74623A", "#B9A779");
    public static bool IsKnown(string id) => id == FollowApp || All.Any(p => p.Id == id);
    public static ThumbnailMenuPalette Resolve(string id, string appTheme) =>
        id == FollowApp ? appTheme == "Legacy" ? Legacy : All.First(p => p.Id == (appTheme == "Light" ? "light" : "dark"))
        : All.FirstOrDefault(p => p.Id == id) ?? Resolve(FollowApp, appTheme);
}
