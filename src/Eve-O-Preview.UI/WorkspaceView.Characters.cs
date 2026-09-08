using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace EveOPreview.UI;

public sealed partial class WorkspaceView
{
    private readonly Dictionary<long, (byte[] Bytes, Bitmap Bitmap)> _characterPortraits = new();

    private Control CharacterPortrait(string title, double size)
    {
        var fallback = RawText(ClientInitial(title), 12, _theme.Accent, true, HorizontalAlignment.Center);
        var image = new Image { Stretch = Stretch.UniformToFill, IsVisible = false };
        var content = new Grid { Children = { fallback, image } };
        var border = new Border { Name = "character-portrait-" + title, Width = size, Height = size,
            CornerRadius = new CornerRadius(6), ClipToBounds = true, Background = B(_theme.AccentSurface),
            VerticalAlignment = VerticalAlignment.Center, Child = content };
        AutomationProperties.SetName(border, F($"Portrait of {title}"));
        // Keep row geometry stable and retain initials when lookup/image retrieval is unavailable.
        if (!_theme.Legacy && _backend is IWorkspaceCharacterProvider characters && _backend is IWorkspacePortraitProvider portraits)
        {
            bool attached = false;
            border.DetachedFromVisualTree += (_, _) => attached = false;
            border.AttachedToVisualTree += async (_, _) =>
            {
                attached = true;
                try
                {
                    var character = await characters.GetCharacterAsync(title);
                    if (_disposed || !attached || character?.CharacterId is not long id) return;
                    var bytes = await portraits.GetCharacterPortraitAsync(id);
                    if (_disposed || !attached || bytes is null) return;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_disposed || !attached) return;
                        if (!_characterPortraits.TryGetValue(id, out var cached) || !ReferenceEquals(cached.Bytes, bytes))
                        {
                            var bitmap = new Bitmap(new MemoryStream(bytes, false));
                            // Other rows may still reference the previous bitmap until this refresh completes.
                            if (cached.Bitmap is not null) _retiredCharacterPortraits.Add(cached.Bitmap);
                            _characterPortraits[id] = cached = (bytes, bitmap);
                        }
                        image.Source = cached.Bitmap;
                        image.IsVisible = true;
                        fallback.IsVisible = false;
                    });
                }
                catch { /* Unavailable identities, network failures and bad images retain initials. */ }
            };
        }
        return border;
    }

    private readonly List<Bitmap> _retiredCharacterPortraits = new();
    private void DisposeCharacterPortraits()
    {
        foreach (var portrait in _characterPortraits.Values) portrait.Bitmap.Dispose();
        foreach (var portrait in _retiredCharacterPortraits) portrait.Dispose();
        _characterPortraits.Clear();
        _retiredCharacterPortraits.Clear();
    }
}
