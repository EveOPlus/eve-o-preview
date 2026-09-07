using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.UI;
using EveOPreview.View;
using MediatR;
using Serilog;
using Xunit;
using Form = System.Windows.Forms.Form;
using Bitmap = System.Drawing.Bitmap;
using Size = System.Drawing.Size;
using Image = Avalonia.Controls.Image;
using Application = System.Windows.Forms.Application;

namespace EveOPreview.Tests.Checks;

public sealed class WorkspaceVisualReviewTests(ITestOutputHelper output)
{
    [Fact]
    public Task CaptureNativeWorkspaceAndVerifyThePinnedTitleEditor() =>
        PrivateDesktopRunner.RunAsync("workspace-native-visuals", output);

    internal static void CaptureNative()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new System.Windows.Forms.ApplicationContext();
        string path = Path.Combine(Path.GetTempPath(), "EveOPreviewNativeVisual-" + Guid.NewGuid().ToString("N") + ".json");
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "native-ui");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly
                .GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
            var profile = new ProfileLocation { FriendlyName = "Multibox fleet", FullPath = "fleet.json" };
            var storage = Stub.Create<IConfigurationStorage>((method, _) => method.Name == "get_CurrentProfile" ? profile : Stub.Default(method.ReturnType));
            var profiles = Stub.Create<IProfileManager>((method, _) => method.Name == "get_ProfileLocations"
                ? new List<ProfileLocation> { new() { FriendlyName = "Default", FullPath = "default.json" }, profile } : Stub.Default(method.ReturnType));
            int captures = 0;
            var pendingStill = new TaskCompletionSource<WorkspaceClientStill>();
            var capture = Stub.Create<IWorkspacePreviewCapture>((_, args) =>
            {
                captures++;
                Assert.Equal("EVE - Aura Asuna", args[0]);
                return pendingStill.Task;
            });
            using var form = new WorkspaceForm(context, logger, Stub.Create<IMediator>(), storage, config, profiles, new ApplicationPreferences(path, logger),
                Stub.Create<IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)), capture);
            form.FormCloseRequested = request => request.Allow = true;
            form.CommitSettingsAsync = () => Task.CompletedTask;
            form.CommitSizeAsync = () => Task.CompletedTask;
            form.ThumbnailSize = new Size(320, 180);
            form.ThumbnailOpacity = .95;
            form.MinimizeToTray = true;
            form.EnableThumbnailZoom = true;
            form.ThumbnailZoomFactor = 3;
            form.ShowThumbnailOverlays = true;
            form.EnableActiveClientHighlight = true;
            form.ActiveClientHighlightColor = Color.FromArgb(114, 201, 255);
            form.TitleFontSettings = new FontSettings { Name = "Segoe UI", Size = 11, Style = FontStyle.Bold, ForeColor = Color.White,
                OutlineColor = Color.Black, OutlineWidth = 1 };
            form.FpsLimiterSettings = new FpsLimiterSettings { IsEnabled = true, FpsFocused = 144, FpsBackground = 30, FpsPredictingFocus = 60 };
            form.AudioMuteSettings = new AudioMuteSettings { MuteJumpGateTunnel = true, CustomMutedEventIds = [12345, 67890] };
            form.CycleGroups = [new CycleGroup { Description = "Combat wing", ClientsOrder = new() { [0] = "EVE - Aura Asuna", [1] = "EVE - Sera Voss" },
                ForwardHotkeys = ["Control + Tab", ""], BackwardHotkeys = ["Control + Shift + Tab", ""] }];
            form.ToggleHideAllActiveHotkey = "Control + Alt + H";
            form.MinimizeAllClientsHotkey = "Control + Alt + M";
            form.SetVersionInfo("10.0.0.12");
            ((Form)form).Show();
            Pump();
            var workspace = Assert.IsType<WorkspaceView>(((Avalonia.Win32.Interoperability.WinFormsAvaloniaControlHost)form.Controls[0]).Content);

            var groups = form.CycleGroups;
            form.CycleGroups = [];
            form.AddThumbnails([Stub.Create<IThumbnailDescription>((method, _) => method.Name == "get_Title" ? "EVE" : Stub.Default(method.ReturnType))]);
            workspace.RefreshFromBackend();
            workspace.NavigatePreviewTab("Titles"); Pump();
            Assert.Equal("EVE - Sample Name", Find<TextBox>(workspace, "preview-character-title").Text);
            Assert.Equal(0, captures); // A login window is not a logged-in character.
            config.SetThumbnailLocation("EVE - Saved Pilot", "", new System.Drawing.Point(10, 20));
            workspace.RefreshFromBackend(); Pump();
            Assert.Equal("EVE - Saved Pilot", Find<TextBox>(workspace, "preview-character-title").Text);
            form.CycleGroups = groups;
            form.AddThumbnails(new[] { "EVE - Aura Asuna", "EVE - Sera Voss", "EVE - Kaelen Orin" }
                .Select(title => Stub.Create<IThumbnailDescription>((method, _) => method.Name == "get_Title" ? title : Stub.Default(method.ReturnType))).ToArray());
            workspace.RefreshFromBackend(); Pump();
            Assert.Equal("EVE - Aura Asuna", Find<TextBox>(workspace, "preview-character-title").Text);
            Assert.Equal(1, captures);
            Find<TextBox>(workspace, "preview-character-title").Text = "EVE - My edited sample";
            Pump(); workspace.RefreshFromBackend(); Pump();
            Assert.Equal("EVE - My edited sample", Find<TextBox>(workspace, "preview-character-title").Text);
            Assert.Equal(1, captures); // Neither a refresh nor typing starts another capture.
            using (var still = new Bitmap(320, 180))
            using (var graphics = Graphics.FromImage(still))
            using (var encoded = new MemoryStream())
            {
                graphics.Clear(Color.DarkSlateBlue);
                graphics.FillRectangle(Brushes.SlateGray, 160, 0, 160, 180);
                still.Save(encoded, System.Drawing.Imaging.ImageFormat.Png);
                pendingStill.SetResult(new("EVE - Aura Asuna", encoded.ToArray()));
            }
            WaitUntil(() =>
            {
                using var pixels = GetPreviewBitmap(workspace);
                return pixels.GetPixel(80, 100).ToArgb() == Color.DarkSlateBlue.ToArgb()
                    && pixels.GetPixel(240, 100).ToArgb() == Color.SlateGray.ToArgb();
            }, "The captured client still must appear behind the native title and highlight.");
            Assert.Equal("EVE - My edited sample", Find<TextBox>(workspace, "preview-character-title").Text);
            Find<TextBox>(workspace, "preview-character-title").Text = "EVE - Aura Asuna"; Pump();

            SetTheme(form, "Legacy");
            foreach (string page in new[] { "General", "Thumbnail", "Zoom", "Overlay", "ActiveClients", "CycleGroups", "FpsAudio", "Profiles", "About" })
            {
                workspace.Navigate(page);
                Pump();
                Capture(workspace, Path.Combine(outputDirectory, "legacy-" + page.ToLowerInvariant() + ".png"));
            }

            foreach (string theme in new[] { "Light", "Dark" })
            {
                SetTheme(form, theme);
                form.ClientSize = new Size(1180, 800);
                workspace.NavigatePreviewTab("Titles");
                Pump();
                WaitUntil(() => Find<Border>(workspace, "title-preview").GetVisualDescendants().OfType<Image>().Any(image => image.Source is not null),
                    "The native preview image did not become available.");
                AssertSampleControls(workspace);
                Capture(workspace, Path.Combine(outputDirectory, theme.ToLowerInvariant() + "-title-editor.png"));
                form.ClientSize = new Size(784, 581);
                Pump();
                AssertSampleControls(workspace);
                Capture(workspace, Path.Combine(outputDirectory, theme.ToLowerInvariant() + "-title-editor-minimum.png"));
                Assert.True(Find<ScrollViewer>(workspace, "preview-editor-scroll").Viewport.Height >= 200,
                    $"At the minimum window size, title controls need at least 200 pixels of editor height beside the pinned preview; got {Find<ScrollViewer>(workspace, "preview-editor-scroll").Viewport.Height}.");
            }

            var preview = Find<Border>(workspace, "title-preview");
            var position = preview.TranslatePoint(default, workspace);
            Assert.NotNull(position);
            Find<AutoCompleteBox>(workspace, "setting-TitleFontName").Text = "Consolas";
            Pump();
            SetDecimalByTyping(workspace, "TitleFontSize", 14.25m);
            SetText(workspace, "TitleFontForeColor", "#FF0000");
            SetText(workspace, "TitleFontOutlineColor", "#00FF00");
            SetDecimalByTyping(workspace, "TitleFontOutlineWidth", 1.25m);
            SetNumber(workspace, "TitleFontOffsetLeft", 30);
            SetNumber(workspace, "TitleFontOffsetTop", 15);
            SetText(workspace, "ActiveClientHighlightColor", "#00FFFF");
            SetNumber(workspace, "ActiveClientHighlightThickness", 6);
            Find<ToggleButton>(workspace, "font-style-Bold").IsChecked = true;
            Find<ToggleButton>(workspace, "font-style-Italic").IsChecked = true;
            Pump();
            var scroll = Find<ScrollViewer>(workspace, "preview-editor-scroll");
            scroll.ScrollToEnd();
            Pump();
            Assert.Equal(position, Find<Border>(workspace, "title-preview").TranslatePoint(default, workspace));
            Assert.True(preview.IsEffectivelyVisible);
            AssertSampleControls(workspace);
            Assert.Equal("Segoe UI", form.TitleFontSettings.Name); // Draft changes have not yet applied.
            Assert.Equal(Color.White.ToArgb(), form.TitleFontSettings.ForeColor.ToArgb());
            WaitUntil(() =>
            {
                using var image = GetPreviewBitmap(workspace);
                var colors = Colors(image);
                return colors.Contains(Color.Red.ToArgb()) && colors.Contains(Color.Lime.ToArgb()) &&
                    image.GetPixel(image.Width / 2, 5).ToArgb() == Color.Cyan.ToArgb();
            }, "The scheduled preview did not render the complete font/color/highlight draft.");
            using (var pixels = GetPreviewBitmap(workspace))
            {
                Assert.Contains(Color.Red.ToArgb(), Colors(pixels));
                Assert.Contains(Color.Lime.ToArgb(), Colors(pixels));
                Assert.Contains(Color.Cyan.ToArgb(), Colors(pixels));
                pixels.Save(Path.Combine(outputDirectory, "native-title-draft-pixels.png"));
            }
            Capture(workspace, Path.Combine(outputDirectory, "dark-title-editor-draft-minimum.png"));
            Click(workspace, "apply-preview-settings");
            Assert.Equal("Consolas", form.TitleFontSettings.Name);
            Assert.Equal(14.25f, form.TitleFontSettings.Size);
            Assert.Equal(1.25f, form.TitleFontSettings.OutlineWidth);
            Assert.Equal(FontStyle.Bold | FontStyle.Italic, form.TitleFontSettings.Style);
            Assert.Equal(Color.Red.ToArgb(), form.TitleFontSettings.ForeColor.ToArgb());
            Assert.Equal(Color.Lime.ToArgb(), form.TitleFontSettings.OutlineColor.ToArgb());
            Assert.Equal(30, form.TitleFontSettings.PositionOffsetFromLeft);
            Assert.Equal(15, form.TitleFontSettings.PositionOffsetFromTop);
            Assert.Equal(6, config.ActiveClientHighlightThickness);
            Assert.Equal(1, captures); // Reflow, themes, draft rendering and Apply all reuse the one still.
            form.ClientSize = new Size(1180, 800); Pump();
            Click(workspace, "preview-refresh-image"); Pump();
            Assert.Equal(2, captures);
            Console.WriteLine("PASS: native Legacy9tabs plus modern title editor captures; real preview pixels follow draft font/colors/highlight; editor scroll keeps preview pinned atminimum; grouped apply retains all edits.");
            Console.WriteLine("Native UI captures: " + outputDirectory);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void AssertSampleControls(WorkspaceView workspace)
    {
        var preview = Find<Border>(workspace, "title-preview");
        var states = Find<WrapPanel>(workspace, "preview-sample-states");
        var active = Find<CheckBox>(workspace, "preview-active-client");
        var skipped = Find<CheckBox>(workspace, "preview-skipped-client");
        Assert.Same(states, active.Parent);
        Assert.Same(states, skipped.Parent);
        Assert.True(preview.Bounds.Height <= 96);
        foreach (var control in new[] { active, skipped })
        {
            var position = control.TranslatePoint(default, workspace).Value;
            Assert.True(control.IsEffectivelyVisible);
            Assert.True(position.Y >= preview.TranslatePoint(default, workspace).Value.Y + preview.Bounds.Height);
            Assert.True(position.X + control.Bounds.Width <= workspace.Bounds.Width);
        }
        Click(workspace, "preview-scale-fit");
        Pump();
        var image = Find<Image>(workspace, "title-preview-image");
        Assert.True(image.Bounds.Height > preview.Bounds.Height,
            "Fit width must preserve a readable title instead of squeezing the entire empty thumbnail into the title strip.");
        Assert.True(image.Bounds.Width <= preview.Bounds.Width);
        Click(workspace, "preview-scale-actual");
        skipped.IsChecked = true;
        Pump();
        using (var pixels = GetPreviewBitmap(workspace)) Assert.Contains(Color.Red.ToArgb(), Colors(pixels));
        skipped.IsChecked = false;
        Pump();
    }

    private static HashSet<int> Colors(Bitmap bitmap)
    {
        var colors = new HashSet<int>();
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++) colors.Add(bitmap.GetPixel(x, y).ToArgb());
        return colors;
    }

    private static Bitmap GetPreviewBitmap(WorkspaceView workspace)
    {
        var image = Find<Border>(workspace, "title-preview").GetVisualDescendants().OfType<Image>().Single();
        var bitmap = Assert.IsAssignableFrom<Avalonia.Media.Imaging.Bitmap>(image.Source);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        using var decoded = new Bitmap(stream);
        return new Bitmap(decoded);
    }

    private static T Find<T>(WorkspaceView workspace, string name) where T : Control =>
        workspace.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void SetText(WorkspaceView workspace, string key, string value) { Find<TextBox>(workspace, "setting-" + key).Text = value; Pump(); }
    private static void SetNumber(WorkspaceView workspace, string key, decimal value) { Find<NumericUpDown>(workspace, "setting-" + key).Value = value; Pump(); }
    private static void SetDecimalByTyping(WorkspaceView workspace, string key, decimal value)
    {
        var numeric = Find<NumericUpDown>(workspace, "setting-" + key);
        var input = numeric.GetVisualDescendants().OfType<TextBox>().Single();
        Assert.True(input.Focus());
        input.Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Pump();
        Find<TextBox>(workspace, "setting-TitleFontForeColor").Focus();
        Pump();
        Assert.Equal(value, numeric.Value);
    }
    private static void Click(WorkspaceView workspace, string name) { Find<Button>(workspace, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
    private static void SetTheme(WorkspaceForm form, string theme) { Assert.True(form.Backend.ExecuteAsync(new WorkspaceCommand("theme", Value: theme)).GetAwaiter().GetResult().Success); Pump(); }
    private static void Capture(WorkspaceView workspace, string path)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)workspace.Bounds.Width, (int)workspace.Bounds.Height), new Vector(96, 96));
        bitmap.Render(workspace);
        bitmap.Save(path);
    }
    private static void Pump()
    {
        for (int i = 0; i < 4; i++) { Application.DoEvents(); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    }
    private static void WaitUntil(Func<bool> condition, string message)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(3))
        {
            Pump();
            if (condition()) return;
            System.Threading.Thread.Sleep(10);
        }
        Assert.Fail(message);
    }
}
