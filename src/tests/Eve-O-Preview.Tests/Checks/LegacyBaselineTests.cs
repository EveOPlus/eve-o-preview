using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Model;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class LegacyBaselineTests(ITestOutputHelper output)
{
    [Fact]
    public Task CaptureOriginalSettingsTabsForLegacyLayoutComparison() =>
        PrivateDesktopRunner.RunAsync("legacy-original-capture", output);

    internal static void CaptureOriginal()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        using var form = new MainForm(context, logger);
        form.SetThumbnailSizeLimitations(new Size(192, 108), new Size(960, 540));
        form.ThumbnailSize = new Size(320, 180);
        form.ThumbnailOpacity = .95;
        form.MinimizeToTray = true;
        form.EnableClientLayoutTracking = false;
        form.HideActiveClientThumbnail = false;
        form.MinimizeInactiveClients = false;
        form.ShowThumbnailsAlwaysOnTop = true;
        form.HideThumbnailsOnLostFocus = false;
        form.EnablePerClientThumbnailLayouts = false;
        form.EnableAutomaticCpuAffinity = true;
        form.EnableThumbnailZoom = true;
        form.ThumbnailZoomFactor = 3;
        form.ShowThumbnailOverlays = true;
        form.ShowThumbnailFrames = false;
        form.EnableActiveClientHighlight = true;
        form.ActiveClientHighlightColor = Color.FromArgb(114, 201, 255);
        form.TitleFontSettings = new FontSettings { Name = "Segoe UI", Size = 11, Style = FontStyle.Bold,
            ForeColor = Color.White, OutlineColor = Color.Black, OutlineWidth = 1 };
        form.FpsLimiterSettings = new FpsLimiterSettings { IsEnabled = true, FpsFocused = 144, FpsBackground = 30, FpsPredictingFocus = 60 };
        form.AudioMuteSettings = new AudioMuteSettings { MuteJumpGateTunnel = true, CustomMutedEventIds = [12345, 67890] };
        form.CycleGroups = [new CycleGroup { Description = "Combat wing", ClientsOrder = new() { [0] = "EVE - Aura Asuna", [1] = "EVE - Sera Voss" },
            ForwardHotkeys = ["Control + Tab", ""], BackwardHotkeys = ["Control + Shift + Tab", ""] }];
        form.ToggleHideAllActiveHotkey = "Control + Alt + H";
        form.MinimizeAllClientsHotkey = "Control + Alt + M";
        form.LoadedProfileName = "Multibox fleet";
        form.UpdateProfileList([new ProfileLocation { FriendlyName = "Default", FullPath = "default.json" },
            new ProfileLocation { FriendlyName = "Multibox fleet", FullPath = "fleet.json" }]);
        form.SetVersionInfo("10.0.0.12");
        form.SetDocumentationUrl("https://discord.gg/HzQHBtTEcB");
        form.AddThumbnails(new[] { "EVE - Aura Asuna", "EVE - Sera Voss", "EVE - Kaelen Orin" }
            .Select(title => Stub.Create<IThumbnailDescription>((method, args) => method.Name == "get_Title" ? title : Stub.Default(method.ReturnType))).ToArray());
        ((Form)form).Show();
        Application.DoEvents();
        var tabs = (TabControl)form.Controls.Find("ContentTabControl", true).Single();
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "original-ui");
        Directory.CreateDirectory(outputDirectory);
        Console.WriteLine($"Original form: outer {form.Width}x{form.Height}; client {form.ClientSize.Width}x{form.ClientSize.Height}; DPI {form.DeviceDpi}; font {form.Font.Name} {form.Font.SizeInPoints}pt; tabs width {tabs.ItemSize.Height}, row {tabs.ItemSize.Width}.");
        foreach (TabPage tab in tabs.TabPages)
        {
            tabs.SelectedTab = tab;
            Application.DoEvents();
            form.Refresh();
            using var screenshot = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
            screenshot.Save(Path.Combine(outputDirectory, "original-" + tab.Name + ".png"));
            var origin = form.PointToScreen(Point.Empty);
            var metrics = Descendants(tab).Where(control => control.Visible).Select(control =>
            {
                Point point = control.PointToScreen(Point.Empty);
                return new { control.Name, Type = control.GetType().Name, control.Text,
                    X = point.X - origin.X, Y = point.Y - origin.Y, control.Width, control.Height,
                    Font = control.Font.Name, FontSize = control.Font.SizeInPoints,
                    ForeColor = control.ForeColor.ToArgb(), BackColor = control.BackColor.ToArgb() };
            }).ToArray();
            File.WriteAllText(Path.Combine(outputDirectory, "original-" + tab.Name + ".json"),
                JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Equal(9, tabs.TabPages.Count);
        Console.WriteLine("Original screenshots and control metrics: " + outputDirectory);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
