using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using MediatR;
using Newtonsoft.Json.Linq;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CustomAudioTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("", true)]
    [InlineData("  , , ", true)]
    [InlineData("0, 123, 4294967295, 123,", true)]
    [InlineData("123,\r\n456", true)]
    [InlineData("123, -1", false)]
    [InlineData("4294967296", false)]
    [InlineData("123, bad", false)]
    [InlineData("1.5", false)]
    [InlineData("0x123", false)]
    public void ParsesOnlyUnsignedDecimalIds(string text, bool valid)
    {
        Assert.Equal(valid, AudioMuteSettings.TryParseCustomMutedEventIds(text, out var ids));
        if (!valid) Assert.Empty(ids);
    }

    [Fact]
    public void RemovesDuplicateIdsAndKeepsUnsignedRange()
    {
        Assert.True(AudioMuteSettings.TryParseCustomMutedEventIds(" 0, 123, 4294967295, 123, ", out var ids));
        Assert.Equal(new uint[] { 0, 123, uint.MaxValue }, ids);
    }

    [Fact]
    public Task CustomAudioUiSavesValidChangesAndPreservesSettingsOnInvalidInput() =>
        PrivateDesktopRunner.RunAsync("custom-audio-ui", output);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendsCustomIdsAlongsidePresetsAndRemovesClearedIds(bool presets)
    {
        var config = CreateConfiguration();
        config.AudioMuteSettings = new AudioMuteSettings
        {
            MuteJumpGateTunnel = presets,
            MuteLocationBanner = presets,
            CustomMutedEventIds = [123, uint.MaxValue, 3689163958, 123]
        };
        using var logger = new LoggerConfiguration().CreateLogger();
        var hook = new HookService(config, logger);
        // A synthetic handle gives this test its own pipe; no EVE process or hook is used.
        var handle = new IntPtr(-Random.Shared.NextInt64(1, long.MaxValue));
        using var server = new NamedPipeServerStream($"EveoRobin_{handle}", PipeDirection.InOut,
            1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        async Task<uint[]> ReceiveUpdate()
        {
            await server.WaitForConnectionAsync(timeout.Token);
            byte[] header = new byte[2];
            await server.ReadExactlyAsync(header, timeout.Token);
            Assert.Equal(new byte[] { 0xA2, 0xC1 }, header);
            await server.WriteAsync(new byte[] { 1 }, timeout.Token);
            server.WaitForPipeDrain();
            server.Disconnect();

            await server.WaitForConnectionAsync(timeout.Token);
            await server.ReadExactlyAsync(header, timeout.Token);
            Assert.Equal(new byte[] { 0xA2, 0xC3 }, header);
            byte[] length = new byte[4];
            await server.ReadExactlyAsync(length, timeout.Token);
            int count = BitConverter.ToInt32(length);
            Assert.InRange(count, 0, 10);
            byte[] payload = new byte[count * 4];
            await server.ReadExactlyAsync(payload, timeout.Token);
            await server.WriteAsync(new byte[] { 1 }, timeout.Token);
            server.WaitForPipeDrain();
            server.Disconnect();
            return Enumerable.Range(0, count).Select(i => BitConverter.ToUInt32(payload, i * 4)).ToArray();
        }

        uint[] presetIds = presets ? [3689163958, 1537508544, 1768044352, 2377891014, 3090840445] : [];
        var receive = ReceiveUpdate();
        Assert.True(await hook.UpdateMutedAudioAsync(handle).WaitAsync(timeout.Token));
        Assert.Equal(presetIds.Concat(new uint[] { 123, uint.MaxValue, 3689163958 }).Distinct().Order(), (await receive).Order());

        config.AudioMuteSettings.CustomMutedEventIds.Clear();
        receive = ReceiveUpdate();
        Assert.True(await hook.UpdateMutedAudioAsync(handle).WaitAsync(timeout.Token));
        Assert.Equal(presetIds.Order(), (await receive).Order());
    }

    private static IThumbnailConfiguration CreateConfiguration() => (IThumbnailConfiguration)Activator.CreateInstance(
        typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));

    internal static void CheckUi()
    {
        var config = CreateConfiguration();
        using var logger = new LoggerConfiguration().CreateLogger();
        string path = Path.GetTempFileName();
        try
        {
            var location = new ProfileLocation { FullPath = path, FriendlyName = "Custom audio test" };
            var storage = (IConfigurationStorage)Activator.CreateInstance(
                typeof(ThumbnailView).Assembly.GetType("EveOPreview.Configuration.Implementation.ConfigurationStorage"),
                Stub.Create<IAppConfig>(), config, Stub.Create<IMediator>(),
                Stub.Create<IProfileManager>((method, args) => location), logger, Stub.Create<IGlobalEvents>());
            using var context = new ApplicationContext();
            using var form = new MainForm(context, logger);
            int saves = 0, updates = 0;
            form.ApplicationSettingsChanged = () => { storage.Save(); saves++; };
            form.AudioSettingsChanged = () => updates++;
            form.AudioMuteSettings = config.AudioMuteSettings;
            Assert.Equal(0, saves); // Loading a profile must not save it back.
            ((Form)form).Show();
            var tabs = (TabControl)form.Controls.Find("ContentTabControl", true).Single();
            tabs.SelectedTab = (TabPage)form.Controls.Find("FpsLimiterTabPage", true).Single();
            var input = (TextBox)form.Controls.Find("txtCustomMutedEventIds", true).Single();
            var hint = (Label)form.Controls.Find("lblCustomMutedEventIdsHint", true).Single();
            void Leave() => typeof(Control).GetMethod("OnLeave", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(input, [EventArgs.Empty]);

            input.Text = "123, 4294967295, 123";
            Leave();
            Assert.Equal(new uint[] { 123, uint.MaxValue }, config.AudioMuteSettings.CustomMutedEventIds);
            Assert.Equal("123, 4294967295", input.Text);
            Assert.Equal(1, saves);
            Assert.Equal(1, updates);
            Assert.Equal(new uint[] { 123, uint.MaxValue },
                JObject.Parse(File.ReadAllText(path))["AudioMuteSettings"]["CustomMutedEventIds"].ToObject<uint[]>());
            storage.Load();
            form.AudioMuteSettings = config.AudioMuteSettings;
            Assert.Equal("123, 4294967295", input.Text);
            Assert.Equal(1, saves);

            using (var screenshot = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
                screenshot.Save(Path.Combine(AppContext.BaseDirectory, "fps-audio-ui.png"));
            }

            input.Text = "123, invalid";
            Leave();
            Assert.Equal(1, saves);
            Assert.Equal(1, updates);
            Assert.Equal(new uint[] { 123, uint.MaxValue }, config.AudioMuteSettings.CustomMutedEventIds);
            Assert.Contains("not saved", hint.Text);
            Assert.True(hint.Visible);

            using (var screenshot = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
                screenshot.Save(Path.Combine(AppContext.BaseDirectory, "fps-audio-invalid-ui.png"));
            }

            input.Text = "456, 456";
            var enter = new KeyEventArgs(Keys.Enter);
            typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, [enter]);
            Assert.True(enter.SuppressKeyPress);
            Assert.Equal(new uint[] { 456 }, config.AudioMuteSettings.CustomMutedEventIds);
            Assert.Equal(2, saves);
            Assert.Equal(2, updates);
            Leave();
            Assert.Equal(2, saves); // Enter followed by leaving must not send the same change twice.

            input.Text = "";
            Leave();
            Assert.Empty(config.AudioMuteSettings.CustomMutedEventIds);
            Assert.Equal(3, saves);
            Assert.Equal(3, updates);
            storage.Load();
            Assert.Empty(config.AudioMuteSettings.CustomMutedEventIds);

            form.AudioMuteSettings = config.AudioMuteSettings;
            input.Text = "789";
            typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(form, [new FormClosingEventArgs(CloseReason.UserClosing, false)]);
            Assert.Equal(4, saves);
            Assert.Equal(4, updates);
            storage.Load();
            Assert.Equal(new uint[] { 789 }, config.AudioMuteSettings.CustomMutedEventIds);
        }
        finally { File.Delete(path); }
    }
}
