using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Handlers.Thumbnails;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services.Interface;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using MediatR;
using Newtonsoft.Json.Linq;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class ProfileWorkflowTests
{
    private static readonly Assembly App = typeof(MainForm).Assembly;

    [Fact]
    public async Task SwitchingProfilesResetsOmittedFieldsAndRejectsPartialLoads()
    {
        using var fixture = new Fixture();
        var first = fixture.WriteProfile("First", """
            {"ConfigVersion":3,"MinimizeToTray":true,"ShowThumbnailFrames":true,
             "FpsLimiterSettings":{"IsEnabled":true,"FpsFocused":88},
             "TitleFontSettings":{"Name":"Arial","Size":26},
             "FlatLayout":{"EVE - A":"111, 222"},"DisableThumbnail":{"EVE - A":true}}
            """);
        var second = fixture.WriteProfile("Second", """
            {"ConfigVersion":3,"AudioMuteSettings":null,"CycleGroups":[null],
             "FpsLimiterSettings":{"FpsBackground":-3,"FpsFocused":9999}}
            """);
        await fixture.Switch(first);
        Assert.True(fixture.Config.MinimizeToTray);
        Assert.Equal(255, fixture.Config.TitleFontSettings.ForeColor.A);
        Assert.Equal(3, fixture.Config.TitleFontSettings.OutlineWidth);
        Assert.True(fixture.Config.IsThumbnailDisabled("EVE - A"));
        await fixture.Switch(second);
        Assert.False(fixture.Config.MinimizeToTray);
        Assert.False(fixture.Config.ShowThumbnailFrames);
        Assert.False(fixture.Config.IsThumbnailDisabled("EVE - A"));
        Assert.False(fixture.Config.FpsLimiterSettings.IsEnabled);
        Assert.Equal(1000, fixture.Config.FpsLimiterSettings.FpsFocused);
        Assert.Equal(0, fixture.Config.FpsLimiterSettings.FpsBackground);
        Assert.Equal(14.25f, fixture.Config.TitleFontSettings.Size);
        Assert.Empty(fixture.Config.CycleGroups);
        var broken = fixture.WriteProfile("Broken", """{"MinimizeToTray":true,"ThumbnailSize":"invalid"}""");
        await fixture.Switch(broken);
        Assert.Same(second, fixture.Storage.CurrentProfile);
        Assert.False(fixture.Config.MinimizeToTray);
        fixture.Storage.Save();
        Assert.False((bool)JObject.Parse(File.ReadAllText(second.FullPath))["MinimizeToTray"]);
        Assert.Contains("invalid", File.ReadAllText(broken.FullPath));
        await fixture.Switch(fixture.Profiles.GetDefaultProfileLocation());
        Assert.Equal(144, fixture.Config.FpsLimiterSettings.FpsFocused);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task LegacyCycleMigrationPreservesClientsAcrossSaveAndReload(int version)
    {
        using var fixture = new Fixture();
        string json = version == 1 ? """
            {"ConfigVersion":1,"CycleGroup1ForwardHotkeys":["F1"],
             "CycleGroup1ClientsOrder":{"EVE - A":2,"EVE - B":1,"EVE - C":1},
             "ClientHotkey":{"EVE - D":"F2","EVE - E":"F2"}}
            """ : """
            {"ConfigVersion":2,"ClientHotkey":{"EVE - D":"F2","EVE - E":"F2"}}
            """;
        await fixture.Switch(fixture.WriteProfile("Legacy", json));
        Assert.Equal(3, fixture.Config.ConfigVersion);
        var hotkeyGroup = Assert.Single(fixture.Config.CycleGroups, g => g.ForwardHotkeys.Contains("F2"));
        Assert.Equal(new[] { "EVE - D", "EVE - E" }, hotkeyGroup.ClientsOrder.Values);
        if (version == 1) Assert.Equal(new[] { "EVE - B", "EVE - C", "EVE - A" }, fixture.Config.CycleGroups[0].ClientsOrder.Values);
        fixture.Storage.Save();
        string saved = File.ReadAllText(fixture.Storage.CurrentProfile.FullPath);
        fixture.Storage.Load();
        fixture.Storage.Save();
        Assert.Equal(saved, File.ReadAllText(fixture.Storage.CurrentProfile.FullPath));
    }

    [Fact]
    public async Task MissingDefaultAndRenamedProfilesRemainUsableAfterRestart()
    {
        using var fixture = new Fixture();
        Assert.NotNull(fixture.Profiles.GetDefaultProfileLocation());
        Assert.Contains(fixture.Profiles.ProfileLocations, p => p.FriendlyName == "Existing");
        fixture.Profiles.RenameCurrentProfile(new RenameCurrentProfile("No longer default"));
        Assert.True(Directory.Exists(Path.Combine(fixture.Root, "Default")));
        fixture.Profiles.CloneCurrentProfile();
        Assert.Contains(fixture.Profiles.ProfileLocations, p => p.FriendlyName == "Default (2)");
        await fixture.Switch(fixture.WriteProfile("Named", "{\"MinimizeToTray\":true}"));
        foreach (string invalid in new[] { "", "..", "CON", "AUX.txt", "COM1", "../escape" })
            fixture.Profiles.RenameCurrentProfile(new RenameCurrentProfile(invalid));
        Assert.Equal("Named", fixture.Storage.CurrentProfile.FriendlyName);
        fixture.Profiles.RenameCurrentProfile(new RenameCurrentProfile("Renamed"));
        fixture.Config.HideActiveClientThumbnail = true;
        fixture.Storage.Save();
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Named")));
        Assert.Contains(fixture.Profiles.ProfileLocations, p => p.FriendlyName == "Renamed");
        fixture.Profiles.RefreshProfileLocations();
        await fixture.Switch(fixture.Profiles.ProfileLocations.Single(p => p.FriendlyName == "Renamed"));
        Assert.True(fixture.Config.HideActiveClientThumbnail);
        Assert.True(fixture.Config.MinimizeToTray);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "eveo-profile-" + Guid.NewGuid());
        public readonly IThumbnailConfiguration Config = (IThumbnailConfiguration)Activator.CreateInstance(App.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        public readonly ProfileManager Profiles;
        public readonly IConfigurationStorage Storage;
        private readonly Serilog.Core.Logger _logger = new LoggerConfiguration().CreateLogger();
        private readonly ChangeSelectedProfileHandler _switch;

        public Fixture()
        {
            WriteProfile("Existing", "{}");
            var refresh = (IRequestHandler<RefreshHotkeys>)Activator.CreateInstance(App.GetType("EveOPreview.Mediator.Handlers.Configuration.RefreshHotkeysHandler"), Config, _logger, Stub.Create<IGlobalEvents>());
            var mediator = Stub.Create<IMediator>((method, args) =>
            {
                if (args.Length > 0 && args[0] is GetCurrentProfileLocation) return Task.FromResult(Storage?.CurrentProfile ?? Profiles.GetDefaultProfileLocation());
                if (args.Length > 0 && args[0] is RefreshHotkeys request) return refresh.Handle(request, CancellationToken.None);
                if (args.Length > 0 && args[0]?.GetType().Name == "SaveConfiguration") { Storage.Save(); return Task.CompletedTask; }
                return Stub.Default(method.ReturnType);
            });
            Profiles = (ProfileManager)Activator.CreateInstance(typeof(ProfileManager), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { _logger, mediator, Root }, null);
            Storage = (IConfigurationStorage)Activator.CreateInstance(App.GetType("EveOPreview.Configuration.Implementation.ConfigurationStorage"), Stub.Create<IAppConfig>(), Config, mediator, Profiles, _logger, Stub.Create<IGlobalEvents>());
            _switch = new ChangeSelectedProfileHandler(Storage, mediator, _logger);
        }

        public ProfileLocation WriteProfile(string name, string json)
        {
            string dir = Path.Combine(Root, name);
            Directory.CreateDirectory(dir);
            var location = new ProfileLocation { FriendlyName = name, FolderPath = dir, FullPath = Path.Combine(dir, "EVE-O Preview.json") };
            File.WriteAllText(location.FullPath, json);
            return location;
        }

        public Task Switch(ProfileLocation location) => _switch.Handle(new ChangeSelectedProfile(location), CancellationToken.None);
        public void Dispose() { Directory.Delete(Root, true); _logger.Dispose(); }
    }
}
