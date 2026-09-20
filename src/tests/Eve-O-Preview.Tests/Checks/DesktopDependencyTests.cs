using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using EveOPreview.View;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class DesktopDependencyTests
{
    [Fact]
    public void ProductionHostsAndResolvedDependenciesAreIndependentOfRetiredDesktopFrameworks()
    {
        Assert.True(typeof(Window).IsAssignableFrom(typeof(WorkspaceWindow)));
        Assert.True(typeof(Window).IsAssignableFrom(typeof(ThumbnailView)));
        Assert.True(typeof(Window).IsAssignableFrom(typeof(ThumbnailOverlay)));
        var assembly = typeof(WorkspaceWindow).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => IsRetired(reference.Name));
        Assert.DoesNotContain(assembly.GetTypes(), type => typeof(System.Windows.Forms.Control).IsAssignableFrom(type));
        string deps = Path.ChangeExtension(assembly.Location, ".deps.json");
        using var document = JsonDocument.Parse(File.ReadAllText(deps));
        foreach (var library in document.RootElement.GetProperty("libraries").EnumerateObject())
            Assert.False(IsRetired(library.Name.Split('/')[0]), "Retired transitive dependency: " + library.Name);
        using var runtime = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(assembly.Location, ".runtimeconfig.json")));
        Assert.DoesNotContain("Microsoft.WindowsDesktop.App", runtime.RootElement.GetRawText());
    }

    private static bool IsRetired(string name) => name is "System.Windows.Forms" or "PresentationFramework" or "PresentationCore"
        or "WindowsBase" or "MouseKeyHook" or "Gma.System.MouseKeyHook" or "Avalonia.Win32.Interoperability";
}
