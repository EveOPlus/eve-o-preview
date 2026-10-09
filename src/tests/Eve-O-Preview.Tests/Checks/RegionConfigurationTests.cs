using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Services.Interface;
using EveOPreview.Tests.Infrastructure;
using MediatR;
using Newtonsoft.Json;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class RegionConfigurationTests
{
    [Fact]
    public void RegionProfilesRoundTripAndOlderProfilesClearAssignments()
    {
        string root = Path.Combine(Path.GetTempPath(), "eveo-regions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var logger = new LoggerConfiguration().CreateLogger();
            var location = new ProfileLocation { FriendlyName = "Regions", FullPath = Path.Combine(root, "regions.json") };
            var profiles = Stub.Create<IProfileManager>((method, _) => method.Name == "GetDefaultProfileLocation" ? location : Stub.Default(method.ReturnType));
            var config = new ThumbnailConfiguration();
            var storage = new ConfigurationStorage(Stub.Create<IAppConfig>(), config, Stub.Create<IMediator>(), profiles, logger, Stub.Create<IGlobalEvents>());
            IThumbnailConfiguration contract = config;
            config.SetThumbnailLocation("EVE - One", "", new Point(30, 40));
            config.ThumbnailRegions.Add(new() { Id = "left", Name = "Left", X = -450, Y = -100, Width = 440, Height = 280 });
            config.ClientRegionAssignments["EVE - One"] = "left";
            config.ClientRegionAssignments["EVE - Offline"] = "left";
            storage.Save();
            config.ThumbnailRegions.Clear(); config.ClientRegionAssignments.Clear();
            Assert.True(storage.Load());
            Assert.Equal(new Point(-450, -100), config.GetThumbnailLocation("EVE - One", "EVE - Other", Point.Empty));
            Assert.Equal(new Size(440, 280), contract.GetThumbnailSize("EVE - Offline"));
            Assert.Contains("EVE - Offline", config.GetKnownClientTitles());
            Assert.True(storage.Load());
            Assert.Single(config.ThumbnailRegions);
            Assert.Equal(2, config.ClientRegionAssignments.Count);
            config.EnableThumbnailRegions = false; config.EnableRegionDragDocking = false;
            storage.Save();
            config.EnableThumbnailRegions = true; config.EnableRegionDragDocking = true;
            Assert.True(storage.Load());
            Assert.False(config.EnableThumbnailRegions); Assert.False(config.EnableRegionDragDocking);
            Assert.Null(config.GetThumbnailRegion("EVE - One"));
            Assert.Equal(new Point(30, 40), config.GetThumbnailLocation("EVE - One", "", Point.Empty));
            Assert.Equal(2, config.ClientRegionAssignments.Count);
            storage.CurrentProfile = new ProfileLocation { FriendlyName = "Old", FullPath = Path.Combine(root, "old.json") };
            File.WriteAllText(storage.CurrentProfile.FullPath, "{}");
            Assert.True(storage.Load());
            Assert.Empty(config.ThumbnailRegions); Assert.Empty(config.ClientRegionAssignments);
            Assert.True(config.EnableThumbnailRegions); Assert.True(config.EnableRegionDragDocking);
            storage.CurrentProfile = location;
            Assert.True(storage.Load());
            Assert.Single(config.ThumbnailRegions);
            // A failed actual file write must be observable so region commands can roll back.
            storage.CurrentProfile = new ProfileLocation { FullPath = Path.Combine(root, "missing", "blocked.json") };
            Assert.Throws<DirectoryNotFoundException>(() => storage.Save());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void InvalidRegionDataIsRestrictedWithoutTouchingOtherLayouts()
    {
        var config = JsonConvert.DeserializeObject<ThumbnailConfiguration>("""
            {"ThumbnailRegions":[null,{"Id":""},{"Id":"a","Name":null,"Width":-1,"Height":9999},{"Id":"a","Name":"Duplicate"}],
             "ClientRegionAssignments":{"EVE - One":"a","EVE - Missing":"missing"," ":"a"}}
            """);
        config.ApplyRestrictions();
        Assert.Single(config.ThumbnailRegions);
        Assert.Equal(config.ThumbnailMinimumSize.Width, config.ThumbnailRegions[0].Width);
        Assert.Equal(config.ThumbnailMaximumSize.Height, config.ThumbnailRegions[0].Height);
        Assert.Single(config.ClientRegionAssignments);
        config.SetThumbnailLocation("EVE - One", "", new Point(999, 999));
        Assert.NotEqual(new Point(999, 999), config.GetThumbnailLocation("EVE - One", "", Point.Empty));
    }
}
