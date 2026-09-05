using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.View;
using MediatR;
using Newtonsoft.Json.Linq;
using Serilog;

using EveOPreview.Tests.Infrastructure;

using System.Threading.Tasks;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class FeatureAvailabilityTests(ITestOutputHelper output)
{
    private static readonly System.Reflection.Assembly AppAssembly = typeof(ThumbnailView).Assembly;

    private static IThumbnailConfiguration CreateConfiguration() => (IThumbnailConfiguration)Activator.CreateInstance(
        AppAssembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));

    [Theory]
    [InlineData(null)]
    [InlineData("malformed")]
    [InlineData("1|Expired|2000-01-01.invalid")]
    public void LegacyProfilesPreserveFeaturesAndDropLicensingFields(string key)
    {
        var config = CreateConfiguration();
        using var logger = new LoggerConfiguration().CreateLogger();
        string path = Path.GetTempFileName();
        try
        {
            var location = new ProfileLocation { FullPath = path, FriendlyName = "Compatibility test" };
            var profiles = Stub.Create<IProfileManager>((method, args) => location);
            var storage = (IConfigurationStorage)Activator.CreateInstance(
                AppAssembly.GetType("EveOPreview.Configuration.Implementation.ConfigurationStorage"),
                Stub.Create<IAppConfig>(), config, Stub.Create<IMediator>(), profiles, logger, Stub.Create<IGlobalEvents>());
            var oldProfile = new JObject
            {
                ["ConfigVersion"] = 3,
                ["IsPremium"] = false,
                ["FpsLimiterSettings"] = new JObject { ["IsEnabled"] = true, ["FpsFocused"] = 123 },
                ["AudioMuteSettings"] = new JObject { ["MuteJumpGateTunnel"] = true },
                ["EnableAutomaticCpuAffinity"] = true
            };
            if (key != null) oldProfile["PremiumLicenseKey"] = key;
            File.WriteAllText(path, oldProfile.ToString());
            storage.Load();
            Assert.True(config.FpsLimiterSettings.IsEnabled);
            Assert.Equal(123, config.FpsLimiterSettings.FpsFocused);
            Assert.True(config.AudioMuteSettings.MuteJumpGateTunnel);
            Assert.True(config.EnableAutomaticCpuAffinity);
            storage.Save();
            var saved = JObject.Parse(File.ReadAllText(path));
            Assert.Null(saved["PremiumLicenseKey"]);
            Assert.Null(saved["IsPremium"]);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FpsLimitingHonorsUserSettingWithoutLicense(bool isEnabled)
    {
        var config = CreateConfiguration();
        config.FpsLimiterSettings.IsEnabled = isEnabled;
        using var logger = new LoggerConfiguration().CreateLogger();
        int enabled = 0, disabled = 0;
        var processes = Stub.Create<IProcessMonitor>((method, args) => new List<IProcessInfo> { Stub.Create<IProcessInfo>() });
        var hooks = Stub.Create<IHookService>((method, args) =>
        {
            if (method.Name == "TryInstallHooksAsync") enabled++;
            if (method.Name == "DisableFpsLimiterAsync") disabled++;
            return Stub.Default(method.ReturnType);
        });
        var handler = (IRequestHandler<SetFpsLimiterEnabled>)Activator.CreateInstance(
            AppAssembly.GetType("EveOPreview.Mediator.Handlers.Configuration.SetFpsLimiterEnabledHandler"), config, processes, hooks, logger);
        await handler.Handle(new SetFpsLimiterEnabled(), CancellationToken.None);
        Assert.Equal(isEnabled ? 1 : 0, enabled);
        Assert.Equal(isEnabled ? 0 : 1, disabled);
    }

    [Fact]
    public void ApplicationDoesNotEmbedLicensingKey() =>
        Assert.DoesNotContain(AppAssembly.GetManifestResourceNames(), name => name.Contains("Premium"));

    [Fact]
    public Task FeatureControlsAreAvailableByDefault() =>
        PrivateDesktopRunner.RunAsync("feature-controls", output);

    internal static void CheckControls()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        using var form = new MainForm(context, logger);
        ((Form)form).Show(); // Avoid MainForm.Show(), which starts an application message loop.
        var tabs = (TabControl)form.Controls.Find("ContentTabControl", true).Single();
        tabs.SelectedTab = (TabPage)form.Controls.Find("FpsLimiterTabPage", true).Single();
        foreach (string name in new[] { "groupBoxFpsLimits", "groupBoxAudioMuting", "chbIsFpsThrottlingEnabled",
            "numericFpsForegroundLimit", "numericFpsBackgroundLimit", "numericFpsPredictedLimit" })
        {
            var control = form.Controls.Find(name, true).Single();
            Assert.True(control.Enabled && control.Visible, name + " should be available by default");
        }
        Assert.True(form.Controls.Find("chbAutoCpuAffinity", true).Single().Enabled,
            "CPU affinity control should be available by default");
    }
}
