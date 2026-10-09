namespace EveOPreview.Configuration.Implementation;

// Physical desktop pixels, matching the borderless thumbnail rectangle.
public sealed record ThumbnailRegion
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 384;
    public int Height { get; set; } = 216;
}
