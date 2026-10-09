using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.UI;
using EveOPreview.View;
using EveOPreview.Tests.Infrastructure;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed partial class WorkspaceBackendTests
{
    [Fact]
    public async Task RegionChoicesIncludeOfflineCycleGroupMembersButNotUnrelatedHistory()
    {
        using var fixture = new Fixture();
        fixture.View.CycleGroups = [new() { ClientsOrder = new() { [0] = "EVE - Offline" } },
            new() { ClientsOrder = new() { [0] = "EVE - Other group" } }];
        fixture.Configuration.SetThumbnailLocation("EVE - Unrelated", "", new(50, 50));
        Assert.True((await fixture.Execute("region-add")).Success);
        string id = fixture.Backend.ReadRegions().Regions.Single().Id;
        Assert.True((await fixture.Execute("region-assign", "EVE - Offline", id)).Success);
        Assert.True((await fixture.Execute("region-assign", "EVE - Other group", id)).Success);
        Assert.False((await fixture.Execute("region-assign", "EVE - Unrelated", id)).Success);
        Assert.Equal(2, fixture.Configuration.ClientRegionAssignments.Count);
    }

    [Fact]
    public async Task RegionTogglesRetainAssignmentsRollBackFailuresAndStopEditing()
    {
        System.Action cancel = null;
        int layouts = 0;
        using var fixture = new Fixture(thumbnails: Stub.Create<EveOPreview.Services.IThumbnailManager>((method, args) =>
        {
            if (method.Name == "SetRegionEditCancel") cancel = (System.Action)args[0];
            if (method.Name == "ApplyRegionLayout") layouts++;
            return Stub.Default(method.ReturnType);
        }));
        // No regions needed: test the global Escape lifetime without creating windows.
        Assert.True((await fixture.Execute("region-edit", value: "true")).Success);
        Assert.NotNull(cancel); Assert.True(fixture.Backend.ReadRegions().Editing);
        cancel(); Assert.False(fixture.Backend.ReadRegions().Editing); Assert.Null(cancel);
        Assert.True((await fixture.Execute("region-edit", value: "true")).Success);
        Assert.True((await fixture.Execute("region-enabled", value: "false")).Success);
        Assert.Null(cancel); Assert.False(fixture.Backend.ReadRegions().Editing);
        Assert.False((await fixture.Execute("region-edit", value: "true")).Success);
        Assert.False(fixture.Backend.ReadRegions().Enabled);
        Assert.True((await fixture.Execute("region-add")).Success);
        string id = fixture.Backend.ReadRegions().Regions.Single().Id;
        fixture.Configuration.ClientRegionAssignments["EVE - One"] = id;
        fixture.FailSave = true;
        Assert.False((await fixture.Execute("region-enabled", value: "true")).Success);
        Assert.False(fixture.Backend.ReadRegions().Enabled);
        Assert.False((await fixture.Execute("region-drag-docking", value: "false")).Success);
        Assert.True(fixture.Backend.ReadRegions().DragDocking);
        fixture.FailSave = false;
        Assert.True((await fixture.Execute("region-enabled", value: "true")).Success);
        Assert.True((await fixture.Execute("region-drag-docking", value: "false")).Success);
        Assert.NotNull(fixture.Configuration.GetThumbnailRegion("EVE - One"));
        Assert.False(fixture.Backend.ReadRegions().DragDocking);
        Assert.Equal(id, fixture.Backend.ReadRegions().Assignments["EVE - One"]);
        Assert.Equal(4, layouts);
    }

    [Fact]
    public async Task RegionsAssignManyClientsValidateBoundsAndRollBackFailedSaves()
    {
        using var fixture = new Fixture();
        var config = fixture.Configuration;
        config.SetThumbnailLocation("EVE - One", "", new Point(15, 25));
        config.SetThumbnailLocation("EVE - Two", "", new Point(55, 65));
        fixture.Backend.AddClients(new[] { "EVE - One", "EVE - Two", "notepad (123)" }
            .Select(title => Stub.Create<IThumbnailDescription>((method, _) => method.Name == "get_Title" ? title : Stub.Default(method.ReturnType))).ToArray());
        Assert.True((await fixture.Execute("region-add")).Success);
        string id = fixture.Backend.ReadRegions().Regions.Single().Id;
        var values = new Dictionary<string, string> { ["Name"] = "Left screen", ["X"] = "-800", ["Y"] = "120", ["Width"] = "450", ["Height"] = "300" };
        Assert.True((await fixture.Backend.ExecuteAsync(new("region-update", id, Settings: values))).Success);
        Assert.True((await fixture.Execute("region-rename", id, "Left screen")).Success);
        Assert.Equal(new Point(-800, 120), new Point(config.ThumbnailRegions[0].X, config.ThumbnailRegions[0].Y));
        foreach (string title in new[] { "EVE - One", "EVE - Two" })
            Assert.True((await fixture.Execute("region-assign", title, id)).Success);
        Assert.Equal(new Point(-800, 120), config.GetThumbnailLocation("EVE - One", "", Point.Empty));
        Assert.Equal(new Size(450, 300), config.GetThumbnailSize("EVE - Two"));
        Assert.Equal(2, fixture.Backend.ReadRegions().Assignments.Count);
        Assert.False((await fixture.Execute("region-assign", "EVE - Missing", id)).Success);
        config.SetThumbnailLocation("EVE - Historical", "", new Point(20, 20));
        config.SetThumbnailLocation("notepad (99)", "", new Point(20, 20));
        Assert.False((await fixture.Execute("region-assign", "EVE - Historical", id)).Success);
        Assert.False((await fixture.Execute("region-assign", "notepad (99)", id)).Success);
        Assert.True((await fixture.Execute("region-assign", "notepad (123)", id)).Success);
        Assert.True((await fixture.Execute("region-assign", "notepad (123)", "")).Success);
        Assert.False((await fixture.Execute("region-assign", "EVE - One", "missing")).Success);
        values["Width"] = "0";
        Assert.False((await fixture.Backend.ExecuteAsync(new("region-update", id, Settings: values))).Success);
        values["Width"] = "500";
        fixture.FailSave = true;
        Assert.False((await fixture.Execute("region-rename", id, "Failed name")).Success);
        Assert.Equal("Left screen", fixture.Backend.ReadRegions().Regions.Single().Name);
        Assert.False((await fixture.Backend.ExecuteAsync(new("region-update", id, Settings: values))).Success);
        Assert.Equal(450, fixture.Backend.ReadRegions().Regions.Single().Width);
        Assert.False((await fixture.Execute("region-delete", id)).Success);
        Assert.Equal(2, config.ClientRegionAssignments.Count);
        Assert.False((await fixture.Execute("region-assign", "EVE - One", "")).Success);
        Assert.Equal(id, config.ClientRegionAssignments["EVE - One"]);
        fixture.FailSave = false;
        Assert.True((await fixture.Execute("region-delete", id)).Success);
        Assert.Empty(config.ClientRegionAssignments);
        Assert.Equal(new Point(15, 25), config.GetThumbnailLocation("EVE - One", "", Point.Empty));
        fixture.Preferences.SetTheme("Legacy");
        Assert.False((await fixture.Execute("region-add")).Success);
        Assert.Empty(config.ThumbnailRegions);
    }
}
