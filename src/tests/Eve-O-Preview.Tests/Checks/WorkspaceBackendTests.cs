using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.UI;
using EveOPreview.Preview;
using EveOPreview.View;
using MediatR;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public interface IWorkspaceTestView : IMainFormView, IAsyncSettingsView { }

public sealed class WorkspaceBackendTests
{
    [Fact]
    public async Task StatisticsResetForwardsSelectedCharacterAndCounterScope()
    {
        string selected = null; CombatResetScope scope = CombatResetScope.All;
        var logs = Stub.Create<IWorkspaceCombatLogs>((method, args) =>
        {
            if (method.Name == "ResetCombatAsync" && args.Length == 2)
            { scope = (CombatResetScope)args[0]; selected = (string)args[1]; return Task.FromResult(CommandResult.Ok()); }
            return Stub.Default(method.ReturnType);
        });
        using var fixture = new Fixture(logs);
        Assert.True((await fixture.Backend.ResetCombatAsync(CombatResetScope.Repairs, "Sample Pilot")).Success);
        Assert.Equal("Sample Pilot", selected); Assert.Equal(CombatResetScope.Repairs, scope);
        Assert.True((await fixture.Backend.ResetCombatAsync(CombatResetScope.Damage)).Success);
        Assert.Null(selected); Assert.Equal(CombatResetScope.Damage, scope);
    }

    [Fact]
    public async Task SolarSystemLayoutSettingsApplyToEveryClientWithoutChangingCombatStyles()
    {
        var perClient = new LogOverlayOptions { Advanced = true, FontSize = 22, Preset = "Minimal",
            CustomAppearance = CombatAppearance.Preset("Classic") with { IncomingColor = "#112233" } };
        var settings = new CombatLogSettings { Overlays = new Dictionary<string, LogOverlayOptions> { ["EVE - Pilot One"] = perClient } };
        var logs = Stub.Create<IWorkspaceCombatLogs>((method, args) =>
        {
            if (method.Name == "ReadLogSettings") return settings;
            if (method.Name == "SaveLogSettingsAsync") { settings = (CombatLogSettings)args[0]; return Task.FromResult(CommandResult.Ok()); }
            return Stub.Default(method.ReturnType);
        });
        using var fixture = new Fixture(logs);
        Assert.True((await fixture.Execute("setting", "ShowCurrentSolarSystem", "False")).Success);
        Assert.True((await fixture.Execute("setting", "SolarSystemColor", "#55BBDD")).Success);
        Assert.False(settings.DefaultOverlay.SolarSystem);
        Assert.False(settings.DefaultOverlay.Advanced);
        Assert.Equal("#55BBDD", settings.DefaultOverlay.GetAppearance().SystemColor);
        Assert.Equal(perClient with { SolarSystem = false, SystemColor = "#55BBDD" }, settings.Overlays["EVE - Pilot One"]);
        Assert.Equal("false", fixture.Backend.Read().Settings["ShowCurrentSolarSystem"]);
        Assert.Equal("#55BBDD", fixture.Backend.Read().Settings["SolarSystemColor"]);
        Assert.False((await fixture.Execute("setting", "SolarSystemColor", "invalid")).Success);
        Assert.False((await fixture.Execute("setting", "ShowCurrentSolarSystem", "invalid")).Success);
        Assert.True((await fixture.Execute("setting", "SolarSystemPlacement", "Left")).Success);
        Assert.True((await fixture.Execute("setting", "SolarSystemFontSize", "22")).Success);
        Assert.All(settings.Overlays.Values.Append(settings.DefaultOverlay), options =>
        { Assert.Equal(SubtitlePlacement.Left, options.SystemPlacement); Assert.Equal(22f, options.SystemFontSize); });
        Assert.False((await fixture.Execute("setting", "SolarSystemPlacement", "invalid")).Success);
        Assert.True((await fixture.Execute("setting", "SolarSystemFontSize", "0")).Success);
        Assert.Null(settings.DefaultOverlay.SystemFontSize);
        Assert.True((await fixture.Execute("setting", "TitlePosition", "BottomRight")).Success);
        Assert.All(settings.Overlays.Values.Append(settings.DefaultOverlay), options => Assert.Equal(OverlayPosition.BottomRight, options.TitlePosition));
        Assert.Equal("BottomRight", fixture.Backend.Read().Settings["TitlePosition"]);
        Assert.Equal(perClient.CustomAppearance, settings.Overlays["EVE - Pilot One"].CustomAppearance);
        Assert.False((await fixture.Execute("setting", "TitlePosition", "invalid")).Success);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task HotkeyMethodIsValidatedAndCommitsTheSelectedProfile()
    {
        using var fixture = new Fixture();
        Assert.Equal("Global", fixture.Backend.Read().Settings["HotkeyInputMethod"]);
        Assert.True((await fixture.Execute("setting", "HotkeyInputMethod", "Windows")).Success);
        Assert.True(fixture.Configuration.UseWindowsHotkeys);
        Assert.Equal("Windows", fixture.Backend.Read().Settings["HotkeyInputMethod"]);
        Assert.False((await fixture.Execute("setting", "HotkeyInputMethod", "invalid")).Success);
        Assert.True(fixture.Configuration.UseWindowsHotkeys);
        Assert.True((await fixture.Execute("setting", "HotkeyInputMethod", "Global")).Success);
        Assert.False(fixture.Configuration.UseWindowsHotkeys);
        Assert.Equal(2, fixture.Commits);
        Assert.False(File.Exists(fixture.Preferences.FilePath));
    }

    [Fact]
    public async Task HotkeyDiagnosticsAreSessionOnlyAndReleaseTimingIsGlobalOnly()
    {
        using var fixture = new Fixture();
        Assert.Equal("False", fixture.Backend.Read().Settings["DiagnosticHotkeyPassthrough"]);
        Assert.Equal("KeyDown", fixture.Backend.Read().Settings["GlobalHotkeyTrigger"]);
        Assert.False((await fixture.Execute("setting", "DiagnosticHotkeyPassthrough", "invalid")).Success);
        Assert.False((await fixture.Execute("setting", "GlobalHotkeyTrigger", "invalid")).Success);
        Assert.True((await fixture.Execute("setting", "GlobalHotkeyTrigger", "KeyUp")).Success);
        Assert.True((await fixture.Execute("setting", "DiagnosticHotkeyPassthrough", "True")).Success);
        Assert.True(fixture.Preferences.DiagnosticHotkeyPassthrough);
        using var logger = new LoggerConfiguration().CreateLogger();
        var reloaded = new ApplicationPreferences(fixture.Preferences.FilePath, logger);
        Assert.True(fixture.Configuration.GlobalHotkeysOnRelease);
        Assert.False(reloaded.DiagnosticHotkeyPassthrough);
        Assert.True((await fixture.Execute("setting", "HotkeyInputMethod", "Windows")).Success);
        Assert.False(fixture.Preferences.DiagnosticHotkeyPassthrough);
        Assert.False((await fixture.Execute("setting", "DiagnosticHotkeyPassthrough", "True")).Success);
        Assert.False((await fixture.Execute("setting", "GlobalHotkeyTrigger", "KeyDown")).Success);
        Assert.True((await fixture.Execute("setting", "HotkeyInputMethod", "Global")).Success);
        Assert.Equal("KeyUp", fixture.Backend.Read().Settings["GlobalHotkeyTrigger"]);
        Assert.Equal("False", fixture.Backend.Read().Settings["DiagnosticHotkeyPassthrough"]);
        Assert.Equal(3, fixture.Commits);
    }

    [Theory]
    [InlineData("HotkeyInputMethod", "Windows")]
    [InlineData("GlobalHotkeyTrigger", "KeyUp")]
    public async Task HotkeyProfileChangesAwaitTheSettingsCommit(string key, string value)
    {
        using var fixture = new Fixture();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.CommitAction = () => saved.Task;
        var command = fixture.Execute("setting", key, value);
        Assert.False(command.IsCompleted);
        saved.SetResult();
        Assert.True((await command).Success);
        Assert.Equal(value, fixture.Backend.Read().Settings[key]);
        Assert.Equal(1, fixture.Commits);
    }

    [Fact]
    public async Task PreviewGraphicsIsGlobalAndDoesNotCommitGameplaySettings()
    {
        using var fixture = new Fixture();
        var before = fixture.Backend.Read();
        Assert.True((await fixture.Execute("setting", "PreviewOverlayRenderer", "Legacy")).Success);
        Assert.Equal("Legacy", fixture.Backend.Read().Settings["PreviewOverlayRenderer"]);
        Assert.Equal("Legacy", fixture.Preferences.PreviewOverlayRenderer);
        Assert.Equal(before.Theme, fixture.Backend.Read().Theme);
        Assert.False((await fixture.Execute("setting", "PreviewOverlayRenderer", "Unknown")).Success);
        Assert.Equal("Legacy", fixture.Backend.Read().Settings["PreviewOverlayRenderer"]);
        Assert.False((await fixture.Execute("preview-test-alert")).Success);
        Assert.True((await fixture.Execute("setting", "PreviewOverlayRenderer", "NativeComposition")).Success);
        Assert.False((await fixture.Execute("preview-test-alert", "EVE - Unavailable")).Success);
        Assert.False((await fixture.Execute("preview-test-alert")).Success);
        Assert.Equal(0, fixture.Commits);
        Assert.Empty(fixture.Messages);
    }

    [Fact]
    public async Task LanguageIsGlobalAndDoesNotCommitGameplaySettings()
    {
        using var fixture = new Fixture();
        var before = fixture.Backend.Read();
        var result = await fixture.Execute("language", value: "fr-CA");
        Assert.True(result.Success);
        Assert.Equal("fr", fixture.Backend.Read().UiLanguage);
        Assert.Equal("fr", fixture.Preferences.UiLanguage);
        Assert.Equal(before.Theme, fixture.Backend.Read().Theme);
        Assert.Equal(before.Settings, fixture.Backend.Read().Settings);
        Assert.Equal(0, fixture.Commits);
        Assert.Empty(fixture.Messages);
    }

    [Theory]
    [InlineData("FpsFocused", "-1")]
    [InlineData("FpsFocused", "NaN")]
    [InlineData("FpsFocused", "25.5")]
    [InlineData("ThumbnailWidth", "20")]
    [InlineData("ThumbnailOpacity", "101")]
    [InlineData("AudioCustomMutedEventIds", "42, -1")]
    [InlineData("AudioCustomMutedEventIds", "4294967296")]
    [InlineData("ActiveClientHighlightColor", "#GGGGGG")]
    [InlineData("EnableThumbnailZoom", "not-a-boolean")]
    [InlineData("ThumbnailZoomAnchor", "not-an-anchor")]
    public async Task InvalidEditorsPreserveSavedValuesAndDoNotApplyNativeSettings(string key, string value)
    {
        using var fixture = new Fixture();
        string before = fixture.Backend.Read().Settings[key];
        var audio = fixture.View.AudioMuteSettings;

        var result = await fixture.Execute("setting", key, value);

        Assert.False(result.Success);
        Assert.Equal(before, fixture.Backend.Read().Settings[key]);
        Assert.Same(audio, fixture.View.AudioMuteSettings);
        Assert.Equal(new uint[] { 7, 11 }, audio.CustomMutedEventIds);
        Assert.Equal(0, fixture.Commits);
        Assert.Empty(fixture.Messages);
    }

    [Theory]
    [InlineData("FpsFocused", "165", typeof(SetFpsLimiter))]
    [InlineData("FpsEnabled", "true", typeof(SetFpsLimiterEnabled))]
    [InlineData("AudioMuteJumpGateTunnel", "true", typeof(SetAudioSettings))]
    [InlineData("AudioCustomMutedEventIds", "42, 42, 4294967295", typeof(SetAudioSettings))]
    public async Task AdvancedEditorsAwaitPersistenceBeforeApplyingNativeSettings(string key, string value, Type messageType)
    {
        using var fixture = new Fixture();
        var pendingSave = new TaskCompletionSource();
        fixture.CommitAction = () => pendingSave.Task;
        var resultTask = fixture.Execute("setting", key, value);

        Assert.False(resultTask.IsCompleted);
        Assert.True(fixture.Backend.IsBusy);
        Assert.Empty(fixture.Messages);
        var overlapping = await fixture.Execute("theme", value: "Light");
        Assert.False(overlapping.Success);
        Assert.Equal("Dark", fixture.Preferences.Theme);

        pendingSave.SetResult();
        var result = await resultTask;
        Assert.True(result.Success, result.Message);
        Assert.False(fixture.Backend.IsBusy);
        Assert.Equal(1, fixture.Commits);
        Assert.Single(fixture.Messages);
        Assert.IsType(messageType, fixture.Messages[0]);
        if (key == "AudioCustomMutedEventIds")
            Assert.Equal(new uint[] { 42, uint.MaxValue }, fixture.View.AudioMuteSettings.CustomMutedEventIds);
    }

    [Fact]
    public async Task PreviewSizeUsesTheSizeCommitAndRespectsRuntimeLimits()
    {
        using var fixture = new Fixture();
        fixture.Backend.MinimumThumbnailSize = new Size(240, 135);
        fixture.Backend.MaximumThumbnailSize = new Size(800, 450);
        var rejected = await fixture.Execute("setting", "ThumbnailWidth", "200");
        Assert.False(rejected.Success);
        var result = await fixture.Execute("setting", "ThumbnailWidth", "640");

        Assert.True(result.Success, result.Message);
        Assert.Equal(new Size(640, 216), fixture.View.ThumbnailSize);
        Assert.Equal(1, fixture.SizeCommits);
        Assert.Equal(0, fixture.Commits);
        Assert.Empty(fixture.Messages);
    }

    [Fact]
    public async Task FailedPersistenceDoesNotReportSuccessOrApplyNativeSettings()
    {
        using var fixture = new Fixture();
        fixture.CommitAction = () => Task.FromException(new IOException("Profile is read only"));

        var result = await fixture.Execute("setting", "FpsFocused", "165");

        Assert.False(result.Success);
        Assert.Contains("Profile is read only", result.Message);
        Assert.False(fixture.Backend.IsBusy);
        Assert.Empty(fixture.Messages);
    }

    [Fact]
    public async Task FailedPersistenceCanBeRetriedWithTheSameEditedValue()
    {
        using var fixture = new Fixture();
        fixture.CommitAction = () => Task.FromException(new IOException("Profile is read only"));
        Assert.False((await fixture.Execute("setting", "FpsFocused", "165")).Success);
        fixture.CommitAction = () => Task.CompletedTask;

        var retry = await fixture.Execute("setting", "FpsFocused", "165");

        Assert.True(retry.Success, retry.Message);
        Assert.Equal(2, fixture.Commits);
        Assert.IsType<SetFpsLimiter>(Assert.Single(fixture.Messages));
    }

    [Fact]
    public async Task EditingCycleOrderPreservesFullTitlesAndAdditionalShortcuts()
    {
        using var fixture = new Fixture();
        const string mockTitle = "Mock client - exact persisted title";
        var group = new CycleGroup
        {
            Description = "Fleet",
            ClientsOrder = new() { [3] = "EVE - Alice", [8] = mockTitle },
            ForwardHotkeys = ["Control + Tab", "F1", "F2", "F3"],
            BackwardHotkeys = ["Shift + Tab", "F4", "F5"]
        };
        fixture.View.CycleGroups.Add(group);

        Assert.True((await fixture.Execute("group-client-up", "0", mockTitle)).Success);
        Assert.Equal(new[] { mockTitle, "EVE - Alice" }, group.ClientsOrder.Values);
        Assert.Equal(new[] { 0, 1 }, group.ClientsOrder.Keys);
        Assert.True((await fixture.Execute("hotkey-clear", "group:0:forward:1")).Success);
        Assert.Equal(new[] { "Control + Tab", "", "F2", "F3" }, group.ForwardHotkeys);
        Assert.Equal(new[] { "Shift + Tab", "F4", "F5" }, group.BackwardHotkeys);
        Assert.True((await fixture.Execute("group-client-add", "0", "Offline character")).Success);
        Assert.Equal("EVE - Offline character", group.ClientsOrder[2]);
        var orderIdentity = group.ClientsOrder;
        Assert.True((await fixture.Backend.ExecuteAsync(new("group-client-move", "0", "EVE - Offline character", 0))).Success);
        Assert.Same(orderIdentity, group.ClientsOrder);
        Assert.Equal(new[] { "EVE - Offline character", mockTitle, "EVE - Alice" }, group.ClientsOrder.Values);
        Assert.True((await fixture.Backend.ExecuteAsync(new("group-client-move", "0", "EVE - Offline character", 2))).Success);
        var before = group.ClientsOrder.Values.ToArray();
        Assert.False((await fixture.Backend.ExecuteAsync(new("group-client-move", "0", mockTitle, 99))).Success);
        Assert.Equal(before, group.ClientsOrder.Values);
    }

    [Fact]
    public async Task TemporarySkipIsSharedByGroupsWithoutSavingOrHidingTheCharacter()
    {
        using var fixture = new Fixture();
        const string title = "EVE - Alice";
        fixture.View.CycleGroups.Add(new() { Description = "Fleet", ClientsOrder = new() { [3] = title } });
        fixture.View.CycleGroups.Add(new() { Description = "Scouts", ClientsOrder = new() { [9] = title } });
        int changes = 0;
        fixture.Backend.Changed += () => changes++;
        var result = await fixture.Execute("client-cycle-skip", title, "true");
        Assert.True(result.Success, result.Message);
        Assert.All(fixture.Backend.Read().CycleGroups, group => Assert.Contains(title, group.SkippedClients));
        Assert.False(fixture.Configuration.IsThumbnailDisabled(title));
        Assert.Equal(0, fixture.Commits);
        Assert.IsType<SetClientCycleSkipped>(Assert.Single(fixture.Messages));
        int before = changes;
        fixture.Configuration.SetClientCycleSkipped(title, false); // The thumbnail menu uses the same state notification.
        Assert.True(changes > before);
        Assert.All(fixture.Backend.Read().CycleGroups, group => Assert.Empty(group.SkippedClients));
        Assert.False((await fixture.Execute("client-cycle-skip", "Missing", "true")).Success);
    }

    [Fact]
    public async Task TemporarilyHidingAllDoesNotChangeIndividualClientVisibility()
    {
        using var fixture = new Fixture();
        const string title = "EVE - Alice";
        bool disabled = false;
        var description = Stub.Create<IThumbnailDescription>((method, args) => method.Name switch
        {
            "get_Title" => title,
            "get_IsDisabled" => disabled,
            "set_IsDisabled" => disabled = (bool)args[0],
            _ => Stub.Default(method.ReturnType)
        });
        fixture.Backend.AddClients([description]);
        fixture.Configuration.IsTemporarilyHidingAllThumbnails = true;

        Assert.True(fixture.Backend.Read().AllPreviewsHidden);
        Assert.True(Assert.Single(fixture.Backend.Read().Clients).PreviewVisible);
        Assert.True((await fixture.Execute("client-visible", title, "false")).Success);
        Assert.True(disabled);
        fixture.Configuration.IsTemporarilyHidingAllThumbnails = false;
        Assert.False(Assert.Single(fixture.Backend.Read().Clients).PreviewVisible);
        Assert.Equal("SaveConfiguration", Assert.Single(fixture.Messages).GetType().Name);
    }

    [Fact]
    public async Task ProfileAccentPersistsSeparatelyFromApplicationTheme()
    {
        using var fixture = new Fixture();

        Assert.True((await fixture.Execute("setting", "ProfileAccentColor", "#AABBCC")).Success);
        Assert.Equal("#AABBCC", fixture.Configuration.UiAccentColor);
        Assert.Equal("SaveConfiguration", Assert.Single(fixture.Messages).GetType().Name);
        Assert.True((await fixture.Execute("theme", value: "Legacy")).Success);
        Assert.Equal("Legacy", new ApplicationPreferences(fixture.AppearancePath, fixture.Logger).Theme);
        Assert.Equal("#AABBCC", fixture.Configuration.UiAccentColor);
        Assert.Single(fixture.Messages);
    }

    [Fact]
    public async Task MenuOrderSavesGloballyAndRejectsInvalidMovesWithoutChangingProfiles()
    {
        using var fixture = new Fixture();
        var result = await fixture.Backend.ExecuteAsync(new("thumbnail-menu-move", "skip-cycling", Position: 0));
        Assert.True(result.Success, result.Message);
        Assert.Equal("skip-cycling", fixture.Backend.Read().ThumbnailMenuOrder[0]);
        Assert.Equal(fixture.Backend.Read().ThumbnailMenuOrder, new ApplicationPreferences(fixture.AppearancePath, fixture.Logger).ThumbnailMenuOrder);
        string saved = File.ReadAllText(fixture.AppearancePath);
        foreach (var command in new WorkspaceCommand[] { new("thumbnail-menu-move", "unknown", Position: 0), new("thumbnail-menu-move", "minimize", Position: -1), new("thumbnail-menu-move", "minimize", Position: 7), new("thumbnail-menu-move", "minimize"), new("thumbnail-menu-move", "divider:minimize", Position: 0) })
            Assert.False((await fixture.Backend.ExecuteAsync(command)).Success);
        Assert.Equal(saved, File.ReadAllText(fixture.AppearancePath));
        Assert.True((await fixture.Execute("thumbnail-menu-reset")).Success);
        Assert.Equal(ThumbnailMenuActions.DefaultOrder, fixture.Backend.Read().ThumbnailMenuOrder);
        Assert.True((await fixture.Execute("thumbnail-menu-divider-add", "minimize")).Success);
        var order = fixture.Backend.Read().ThumbnailMenuOrder;
        string divider = order[1];
        Assert.True(ThumbnailMenuActions.IsDivider(divider));
        Assert.Equal("minimize-all", order[2]);
        Assert.False((await fixture.Execute("thumbnail-menu-divider-add", "minimize")).Success);
        Assert.True((await fixture.Backend.ExecuteAsync(new("thumbnail-menu-move", divider, Position: 4))).Success);
        Assert.Equal(divider, fixture.Backend.Read().ThumbnailMenuOrder[4]);
        Assert.True((await fixture.Execute("thumbnail-menu-divider-remove", divider)).Success);
        Assert.False((await fixture.Execute("thumbnail-menu-divider-remove", "minimize")).Success);
        Assert.True((await fixture.Execute("thumbnail-menu-theme", value: "amarr")).Success);
        Assert.False((await fixture.Execute("thumbnail-menu-theme", value: "invalid")).Success);
        Assert.Equal("amarr", new ApplicationPreferences(fixture.AppearancePath, fixture.Logger).ThumbnailMenuTheme);
        Assert.Equal(0, fixture.Commits);
        Assert.Empty(fixture.Messages);
    }

    [Fact]
    public async Task FailedProfileOperationCannotClaimTheRequestedProfileIsActive()
    {
        using var fixture = new Fixture();
        var result = await fixture.Execute("profile-switch", "fleet.json");
        Assert.False(result.Success);
        Assert.Equal("Default", fixture.Backend.Read().ProfileName);
    }

    [Fact]
    public async Task AdvancedSettingsSaveAndNotifyWithoutReplacingProfileObjects()
    {
        using var fixture = new Fixture();
        var colors = fixture.Configuration.PerClientActiveClientHighlightColor;
        foreach (var (key, value) in new[] { ("ThumbnailRefreshPeriod", "750"), ("HideDelaySeconds", "1.1"),
            ("EnableThumbnailSnap", "false"), ("EnableCompatibilityMode", "true"), ("LoginThumbnailLeft", "-120"), ("LoginThumbnailTop", "45") })
        {
            fixture.Messages.Clear();
            Assert.True((await fixture.Execute("setting", key, value)).Success);
            Assert.Collection(fixture.Messages, message => Assert.Equal("SaveConfiguration", message.GetType().Name),
                message => Assert.IsType<ThumbnailRuntimeSettingsUpdated>(message));
        }
        Assert.Equal(2, fixture.Configuration.HideThumbnailsDelay);
        Assert.Equal("1.5", fixture.Backend.Read().Settings["HideDelaySeconds"]);
        Assert.Equal(new Point(-120, 45), fixture.Configuration.LoginThumbnailLocation);
        Assert.Same(colors, fixture.Configuration.PerClientActiveClientHighlightColor);
        var copy = (IThumbnailConfiguration)Newtonsoft.Json.JsonConvert.DeserializeObject(
            Newtonsoft.Json.JsonConvert.SerializeObject(fixture.Configuration), fixture.Configuration.GetType());
        Assert.Equal(750, copy.ThumbnailRefreshPeriod);
        Assert.True(copy.EnableCompatibilityMode);
        Assert.False(copy.EnableThumbnailSnap);
        Assert.Equal(2, copy.HideThumbnailsDelay);
        Assert.Equal(new Point(-120, 45), copy.LoginThumbnailLocation);
    }

    [Fact]
    public async Task ResizeBoundsApplyTogetherClampCurrentSizeAndRollbackOnSaveFailure()
    {
        using var fixture = new Fixture();
        var limits = new Dictionary<string, string> { ["ThumbnailMinimumWidth"] = "400", ["ThumbnailMinimumHeight"] = "240",
            ["ThumbnailMaximumWidth"] = "600", ["ThumbnailMaximumHeight"] = "400" };
        Assert.True((await fixture.Backend.ExecuteAsync(new("preview-size-limits", Settings: limits))).Success);
        Assert.Equal(new Size(400, 240), fixture.Configuration.ThumbnailMinimumSize);
        Assert.Equal(new Size(400, 240), fixture.Configuration.ThumbnailSize);
        Assert.Equal(fixture.Configuration.ThumbnailSize, fixture.View.ThumbnailSize);
        Assert.Equal("600", fixture.Backend.Read().Settings["ThumbnailMaximumWidth"]);
        fixture.Messages.Clear();
        limits["ThumbnailMinimumWidth"] = "700";
        Assert.False((await fixture.Backend.ExecuteAsync(new("preview-size-limits", Settings: limits))).Success);
        Assert.Empty(fixture.Messages);
        Assert.Equal(new Size(400, 240), fixture.Configuration.ThumbnailMinimumSize);
        limits["ThumbnailMinimumWidth"] = "450";
        fixture.FailSave = true;
        Assert.False((await fixture.Backend.ExecuteAsync(new("preview-size-limits", Settings: limits))).Success);
        Assert.Equal(new Size(400, 240), fixture.Configuration.ThumbnailMinimumSize);
        Assert.Equal(new Size(400, 240), fixture.View.ThumbnailSize);
        Assert.DoesNotContain(fixture.Messages, message => message is ThumbnailRuntimeSettingsUpdated);
    }

    [Fact]
    public async Task OfflineCharacterOverridesRoundTripAndCanReturnToProfileDefaults()
    {
        using var fixture = new Fixture();
        var command = new WorkspaceCommand("client-preferences", "Offline Pilot", "#72C9FF",
            Settings: new Dictionary<string, string> { ["Priority"] = "true" });
        Assert.True((await fixture.Backend.ExecuteAsync(command)).Success);
        var entry = Assert.Single(fixture.Backend.Read().ClientPreferences, entry => entry.Title == "EVE - Offline Pilot");
        Assert.True(entry.Priority);
        Assert.Equal("#72C9FF", entry.BorderColor);
        var copy = (IThumbnailConfiguration)Newtonsoft.Json.JsonConvert.DeserializeObject(
            Newtonsoft.Json.JsonConvert.SerializeObject(fixture.Configuration), fixture.Configuration.GetType());
        Assert.True(copy.IsPriorityClient(entry.Title));
        Assert.Equal(Color.FromArgb(0x72, 0xC9, 0xFF).ToArgb(), copy.PerClientActiveClientHighlightColor[entry.Title].ToArgb());
        fixture.FailSave = true;
        var remove = command with { Target = entry.Title, Value = "", Settings = new Dictionary<string, string> { ["Priority"] = "false" } };
        Assert.False((await fixture.Backend.ExecuteAsync(remove)).Success);
        Assert.True(fixture.Configuration.IsPriorityClient(entry.Title));
        Assert.True(fixture.Configuration.PerClientActiveClientHighlightColor.ContainsKey(entry.Title));
        fixture.FailSave = false;
        Assert.True((await fixture.Backend.ExecuteAsync(remove)).Success);
        Assert.False(fixture.Configuration.IsPriorityClient(entry.Title));
        Assert.False(fixture.Configuration.PerClientActiveClientHighlightColor.ContainsKey(entry.Title));
        Assert.True(copy.IsPriorityClient(entry.Title)); // A separate profile retains its settings.
        Assert.False((await fixture.Backend.ExecuteAsync(command with { Target = "EVE - " })).Success);
        Assert.False((await fixture.Backend.ExecuteAsync(command with { Value = "#invalid" })).Success);
    }

    [Fact]
    public async Task FailedAdvancedSaveRestoresConfigurationWithoutRuntimeNotification()
    {
        using var fixture = new Fixture { FailSave = true };
        var interval = fixture.Configuration.ThumbnailRefreshPeriod;
        Assert.False((await fixture.Execute("setting", "ThumbnailRefreshPeriod", "750")).Success);
        Assert.Equal(interval, fixture.Configuration.ThumbnailRefreshPeriod);
        Assert.DoesNotContain(fixture.Messages, message => message is ThumbnailRuntimeSettingsUpdated);
    }

    [Fact]
    public async Task RegistrationErrorsUseCurrentLanguageAndPreserveRawShortcutNames()
    {
        const string shortcut = "Ctrl+F16";
        FormattableString[] messages = [$"Windows could not register these shortcuts (they may be reserved or in use): {shortcut}", $"Invalid shortcuts: {"Unknown+Key"}"];
        string english = string.Join(" ", messages.Select(x => x.ToString()));
        var thumbnails = Stub.Create<EveOPreview.Services.IThumbnailManager>((method, _) => method.Name switch
        {
            "get_HotkeyRegistrationWarnings" => messages,
            "get_HotkeyRegistrationWarning" => english,
            _ => Stub.Default(method.ReturnType)
        });
        using var fixture = new Fixture(thumbnails: thumbnails);
        foreach (string language in new[] { "de", "ja", "ar" })
        {
            fixture.Preferences.SetLanguage(language);
            var localization = new WorkspaceLocalization(language);
            string expected = string.Join(" ", messages.Select(localization.Format));
            Assert.NotEqual(english, expected);
            Assert.Equal(expected, fixture.Backend.Read().Settings["HotkeyRegistrationWarning"]);
            var result = await fixture.Execute("setting", "HotkeyInputMethod", "Windows");
            Assert.False(result.Success);
            Assert.Equal(expected, result.Localize(localization.Get));
            Assert.Contains(shortcut, result.Message);
            Assert.Contains("Unknown+Key", result.Message);
        }
        fixture.Preferences.SetTheme("Legacy");
        Assert.Equal(english, fixture.Backend.Read().Settings["HotkeyRegistrationWarning"]);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly string AppearancePath = Path.Combine(Path.GetTempPath(), "EveOPreviewAppearance-" + Guid.NewGuid().ToString("N") + ".json");
        public readonly Serilog.Core.Logger Logger = new LoggerConfiguration().CreateLogger();
        public IWorkspaceTestView View { get; }
        public IThumbnailConfiguration Configuration { get; }
        public ApplicationPreferences Preferences { get; }
        public WindowsWorkspaceBackend Backend { get; }
        public List<object> Messages { get; } = [];
        public int Commits { get; private set; }
        public int SizeCommits { get; private set; }
        public Func<Task> CommitAction { get; set; } = () => Task.CompletedTask;
        public bool FailSave { get; set; }

        public Fixture(IWorkspaceCombatLogs combatLogs = null, EveOPreview.Services.IThumbnailManager thumbnails = null)
        {
            Configuration = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly
                .GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
            var values = new Dictionary<string, object>();
            View = Stub.Create<IWorkspaceTestView>((method, args) =>
            {
                if (method.Name.StartsWith("set_", StringComparison.Ordinal))
                {
                    values[method.Name[4..]] = args[0];
                    return null;
                }
                if (method.Name.StartsWith("get_", StringComparison.Ordinal) && values.TryGetValue(method.Name[4..], out var value))
                    return value;
                return Stub.Default(method.ReturnType);
            });
            View.ThumbnailSize = new Size(384, 216);
            View.ThumbnailOpacity = .5;
            View.ThumbnailZoomFactor = 2;
            View.FpsLimiterSettings = new FpsLimiterSettings { FpsFocused = 120 };
            View.AudioMuteSettings = new AudioMuteSettings { CustomMutedEventIds = [7, 11] };
            View.TitleFontSettings = new FontSettings();
            View.CycleGroups = [];
            View.CommitSettingsAsync = () => { Commits++; return CommitAction(); };
            View.CommitSizeAsync = () => { SizeCommits++; return CommitAction(); };
            var mediator = Stub.Create<IMediator>((method, args) =>
            {
                if (method.Name is "Send" or "Publish") Messages.Add(args[0]);
                if (FailSave && args.FirstOrDefault()?.GetType().Name == "SaveConfiguration") throw new IOException("Profile is read-only.");
                if (args.FirstOrDefault() is SetClientCycleSkipped skip)
                    return new EveOPreview.Mediator.Handlers.Thumbnails.SetClientCycleSkippedHandler(Configuration).Handle(skip, default);
                return Stub.Default(method.ReturnType);
            });
            var defaultProfile = new ProfileLocation { FriendlyName = "Default", FullPath = "default.json" };
            var profiles = new List<ProfileLocation> { defaultProfile, new() { FriendlyName = "Fleet", FullPath = "fleet.json" } };
            var storage = Stub.Create<IConfigurationStorage>((method, _) => method.Name == "get_CurrentProfile" ? defaultProfile : Stub.Default(method.ReturnType));
            var profileManager = Stub.Create<IProfileManager>((method, _) => method.Name == "get_ProfileLocations" ? profiles : Stub.Default(method.ReturnType));
            Preferences = new ApplicationPreferences(AppearancePath, Logger);
            Backend = new WindowsWorkspaceBackend(View, View, mediator, storage, Configuration, profileManager, Preferences, Logger,
                Stub.Create<IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)), combatLogs: combatLogs, thumbnails: thumbnails);
        }

        public Task<CommandResult> Execute(string action, string target = "", string value = "") => Backend.ExecuteAsync(new(action, target, value));

        public void Dispose()
        {
            Backend.Dispose();
            Logger.Dispose();
            if (File.Exists(AppearancePath)) File.Delete(AppearancePath);
        }
    }
}
