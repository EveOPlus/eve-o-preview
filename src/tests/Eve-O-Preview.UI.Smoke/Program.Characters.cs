using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EveOPreview.UI.Smoke;

internal static partial class Program
{
    private static int CheckCharacterPortraits(string output)
    {
        var resolved = new TaskCompletionSource<WorkspaceCharacter?>();
        var backend = new SmokeBackend { CharacterResult = resolved.Task };
        backend.CompletePortrait();
        using var view = new WorkspaceView(backend);
        var window = new Window { Width = 1180, Height = 820, Content = view };
        window.Show(); view.Navigate("Clients"); Flush();
        var placeholders = Portraits(view);
        Require(placeholders.Length == backend.Read().Clients.Count, "Every modern client row needs a portrait slot.");
        Require(placeholders.All(p => p.GetVisualDescendants().OfType<Image>().All(i => i.Source is null)), "Pending identity lookups must retain placeholders.");
        // A slow identity lookup must not block client controls or change row bounds.
        var originalBounds = placeholders.Select(p => p.Bounds.Size).ToArray();
        FindControl<ToggleSwitch>(view, "client-EVE - Aura Asuna").IsChecked = false; Flush();
        Require(backend.Commands.Last().Action == "client-visible", "Portrait loading blocked a client command.");
        resolved.SetResult(new("Synthetic identity", 95465272, 12345678)); Flush();
        Require(Portraits(view).All(HasPortrait), "Resolved identities must populate client portrait images.");
        Require(originalBounds.SequenceEqual(Portraits(view).Select(p => p.Bounds.Size)), "Portrait loading changed client row geometry.");
        int renders = 0;
        foreach (var theme in new[] { "Light", "Dark" })
        {
            backend.ExecuteAsync(new("theme", Value: theme)).GetAwaiter().GetResult(); Flush();
            foreach (var size in new[] { new Size(1180, 820), new Size(784, 581) })
            {
                window.Width = size.Width; window.Height = size.Height;
                view.Navigate("Clients"); Flush();
                Require(Portraits(view).All(HasPortrait), "Portraits disappeared after a theme change.");
                Capture(window, Path.Combine(output, $"portraits-{theme}-{size.Width}-clients.png")); renders++;
                view.Navigate("Switching"); Flush();
                Require(Portraits(view).Length > 0 && Portraits(view).All(HasPortrait), "Cycle order needs portraits, including offline members.");
                Capture(window, Path.Combine(output, $"portraits-{theme}-{size.Width}-order.png")); renders++;
                Click(view, "expand-cycle-order"); Flush();
                Require(Portraits(view).All(HasPortrait), "Expanded cycle order must retain portraits.");
                Capture(window, Path.Combine(output, $"portraits-{theme}-{size.Width}-expanded.png")); renders++;
                Click(view, "close-cycle-order"); Flush();
                view.Navigate("Clients");
            }
        }
        backend.ExecuteAsync(new("language", Value: "ar")).GetAwaiter().GetResult(); Flush();
        view.Navigate("Clients"); Flush();
        Require(view.FlowDirection == FlowDirection.RightToLeft && Portraits(view).All(HasPortrait), "RTL must preserve portraits.");
        Capture(window, Path.Combine(output, "portraits-arabic-clients.png")); renders++;
        backend.ExecuteAsync(new("theme", Value: "Legacy")).GetAwaiter().GetResult(); Flush();
        view.Navigate("CycleGroups"); Flush();
        Require(Portraits(view).Length == 0, "Legacy must retain its original character list.");
        window.Close();
        Console.WriteLine("PASS: delayed identity lookup, portrait population, stable row geometry, client commands, cycle order, themes, compact and RTL layouts.");
        return renders;
    }

    private static Border[] Portraits(WorkspaceView view) => view.GetVisualDescendants().OfType<Border>()
        .Where(b => b.Name?.StartsWith("character-portrait-", StringComparison.Ordinal) == true).ToArray();
    private static bool HasPortrait(Border border) => border.GetVisualDescendants().OfType<Image>().Any(i => i.Source is not null && i.IsVisible);
}
