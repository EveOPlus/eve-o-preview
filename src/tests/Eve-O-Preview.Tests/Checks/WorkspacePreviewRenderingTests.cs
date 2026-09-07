using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.UI;
using EveOPreview.View;
using EveOPreview.View.CustomControl;
using Gma.System.MouseKeyHook;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class WorkspacePreviewRenderingTests(ITestOutputHelper output)
{
    private static readonly Color Background = Color.FromArgb(16, 24, 37);

    [Fact]
    public async Task StillCaptureSkipsUnavailableClientsAndReturnsABoundedImageWithoutActivation()
    {
        using var logger = new Serilog.LoggerConfiguration().CreateLogger();
        var clients = Enumerable.Range(1, 3).Select(id => Stub.Create<IProcessInfo>((method, _) => method.Name switch
        {
            "get_MainWindowHandle" => new IntPtr(id),
            "get_Title" => "EVE - Pilot " + id,
            _ => Stub.Default(method.ReturnType)
        })).ToArray();
        var processes = Stub.Create<IProcessMonitor>((method, _) => method.Name == "GetAllProcesses" ? clients : throw new InvalidOperationException(method.Name));
        var captured = new List<IntPtr>();
        var windows = Stub.Create<IWindowManager>((method, args) =>
        {
            var handle = (IntPtr)args[0];
            if (method.Name == "IsWindowMinimized") return handle == new IntPtr(3);
            if (method.Name != "GetStaticThumbnail") throw new InvalidOperationException("Unexpected native operation: " + method.Name);
            captured.Add(handle);
            var image = new Bitmap(1920, 1080);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(handle == new IntPtr(1) ? Color.Black : Color.CornflowerBlue);
            return image;
        });
        var service = new WindowsWorkspacePreviewCapture(processes, windows, logger);
        var still = await service.CapturePreviewStillAsync("EVE - Pilot 3");
        Assert.NotNull(still);
        Assert.Equal("EVE - Pilot 2", still.Title);
        Assert.Equal(new[] { new IntPtr(1), new IntPtr(2) }, captured);
        using var stream = new MemoryStream(still.Png);
        using var result = new Bitmap(stream);
        Assert.Equal(new Size(960, 540), result.Size);
        Assert.Equal(Color.CornflowerBlue.ToArgb(), result.GetPixel(480, 270).ToArgb());
    }

    [Fact]
    public Task PreviewPixelsMatchTheActualNativeTitleAndHighlightControls() =>
        PrivateDesktopRunner.RunAsync("workspace-preview-pixels", output);

    [Fact]
    public Task StaticCaptureContainsOnlyTheRequestedClientWhenAnotherWindowCoversIt() =>
        PrivateDesktopRunner.RunAsync("workspace-client-capture", output);

    internal static void CheckClientCapture()
    {
        using var logger = new Serilog.LoggerConfiguration().CreateLogger();
        var windows = new EveOPreview.Services.Implementation.WindowManager(Stub.Create<IHookService>(), logger);
        foreach (bool redirect in new[] { true, false })
        {
            using var client = new CaptureClient(redirect)
            {
                Text = "EVE - Capture target", BackColor = Color.CornflowerBlue,
                StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), ClientSize = new Size(640, 400)
            };
            client.Controls.Add(new Panel { BackColor = Color.LimeGreen, Location = new Point(20, 25), Size = new Size(140, 80) });
            client.Show(); client.Refresh(); Application.DoEvents();
            using var cover = new Form
            {
                Text = "Other application", BackColor = Color.Magenta, FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual, Bounds = client.Bounds, TopMost = true
            };
            cover.Show(); cover.Refresh(); cover.Activate(); Application.DoEvents();
            var active = EveOPreview.Tests.Infrastructure.Native.GetActiveWindow();
            Assert.Equal(cover.Handle, active);
            using var captured = windows.GetStaticThumbnail(client.Handle);
            if (redirect)
            {
                var bitmap = Assert.IsType<Bitmap>(captured);
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, "covered-client.png"));
                Assert.Equal(client.ClientSize, bitmap.Size);
                Assert.Equal(Color.CornflowerBlue.ToArgb(), bitmap.GetPixel(600, 350).ToArgb());
                Assert.Equal(Color.LimeGreen.ToArgb(), bitmap.GetPixel(40, 45).ToArgb());
                Assert.DoesNotContain(Color.Magenta.ToArgb(), Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, 200).ToArgb()));
            }
            else
            {
                // This GDI-only test window has no compositor surface or DirectComposition
                // content to render. Return no image instead of copying the window covering it.
                Assert.Null(captured);
            }
            Assert.Equal(active, EveOPreview.Tests.Infrastructure.Native.GetActiveWindow());
            client.WindowState = FormWindowState.Minimized; Application.DoEvents();
            Assert.Null(windows.GetStaticThumbnail(client.Handle));
            var closedHandle = client.Handle;
            client.Close();
            Assert.Null(windows.GetStaticThumbnail(closedHandle));
        }
        Assert.Null(windows.GetStaticThumbnail(IntPtr.Zero));
        Console.WriteLine("PASS: covered client pixels and child controls exclude the covering window without activation; missing render surface and minimized/closed/zero HWND return no image.");
    }

    private sealed class CaptureClient(bool redirect) : Form
    {
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                if (!redirect) parameters.ExStyle |= 0x00200000; // WS_EX_NOREDIRECTIONBITMAP, used by DirectComposition windows.
                return parameters;
            }
        }
    }

    internal static void CheckPixels()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var renderer = new WindowsWorkspacePreviewRenderer();
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "title-preview");
        Directory.CreateDirectory(outputDirectory);
        var fonts = new[]
        {
            new FontSettings { Name = "Consolas", Size = 14.25f, Style = FontStyle.Regular, ForeColor = Color.Red, OutlineColor = Color.Lime, OutlineWidth = 0, PositionOffsetFromLeft = 10, PositionOffsetFromTop = 5 },
            new FontSettings { Name = "Segoe UI", Size = 18, Style = FontStyle.Bold | FontStyle.Underline, ForeColor = Color.White, OutlineColor = Color.Black, OutlineWidth = 3, PositionOffsetFromLeft = 24, PositionOffsetFromTop = 12 },
            new FontSettings { Name = "Arial", Size = 24, Style = FontStyle.Italic, ForeColor = Color.Cyan, OutlineColor = Color.Magenta, OutlineWidth = 2.5f, PositionOffsetFromLeft = 120, PositionOffsetFromTop = 40 },
            new FontSettings { Name = "Consolas", Size = 19, Style = FontStyle.Bold, ForeColor = Color.Yellow, OutlineColor = Color.Blue, OutlineWidth = 1, PositionOffsetFromLeft = -8, PositionOffsetFromTop = -3 }
        };
        using var owner = new Form();
        owner.Show();
        foreach (var settings in fonts)
        {
            string name = settings.Name.Replace(" ", "-") + "-" + settings.Size.ToString(CultureInfo.InvariantCulture);
            using var overlay = new ThumbnailOverlay(owner, (_, _) => { })
            {
                ClientSize = new Size(384, 216), BackColor = Background
            };
            overlay.SetOverlayLabel("Aura Asuna");
            overlay.SetOverlayFont(settings);
            overlay.Show();
            Application.DoEvents();
            using var expected = new Bitmap(384, 216, PixelFormat.Format32bppArgb);
            // DrawToBitmap omits the label of this layered transparency-key window.
            // Paint the actual production instance using its native, autosized bounds.
            var label = (OutlinedLabel)overlay.Controls.Find("OverlayLabel", true).Single();
            using (var graphics = Graphics.FromImage(expected))
            {
                graphics.Clear(Background);
                graphics.TranslateTransform(label.Left, label.Top);
                graphics.SetClip(label.ClientRectangle, System.Drawing.Drawing2D.CombineMode.Intersect);
                typeof(OutlinedLabel).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(label, [new PaintEventArgs(graphics, label.ClientRectangle)]);
            }
            var request = Settings(settings);
            using var encoded = new MemoryStream(renderer.RenderPreview(new(request, "EVE - Aura Asuna")).Png);
            using var actual = new Bitmap(encoded);
            expected.Save(Path.Combine(outputDirectory, "native-" + name + ".png"));
            actual.Save(Path.Combine(outputDirectory, "sample-" + name + ".png"));
            AssertPixelsEqual(expected, actual, "Actual ThumbnailOverlay title " + name);
            Assert.True(Pixels(actual).Where((_, index) => index % 4 == 0).Distinct().Count() > 1,
                "The comparison must contain actual rendered title pixels.");
        }

        foreach (var marker in new[] { ("Circle with slash", true), ("Pause", true), ("Cross", false) })
        {
            var font = fonts[1];
            using var overlay = new ThumbnailOverlay(owner, (_, _) => { }) { ClientSize = new Size(384, 216), BackColor = Background };
            overlay.SetOverlayLabel("Aura Asuna"); overlay.SetOverlayFont(font);
            overlay.EnableOverlayLabel(marker.Item2);
            overlay.SetCycleSkipIndicator(true, marker.Item1, Color.Red);
            overlay.Show(); Application.DoEvents();
            var label = (OutlinedLabel)overlay.Controls.Find("OverlayLabel", true).Single();
            Assert.True(label.Visible, "A skipped marker remains visible with character titles disabled.");
            using var expected = new Bitmap(384, 216, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(expected))
            {
                graphics.Clear(Background); graphics.TranslateTransform(label.Left, label.Top);
                graphics.SetClip(label.ClientRectangle, System.Drawing.Drawing2D.CombineMode.Intersect);
                typeof(OutlinedLabel).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(label, [new PaintEventArgs(graphics, label.ClientRectangle)]);
            }
            var settings = Settings(font);
            settings["ShowThumbnailOverlays"] = marker.Item2.ToString();
            settings["CycleSkipIndicatorStyle"] = marker.Item1;
            settings["CycleSkipIndicatorColor"] = "#FF0000";
            using var encoded = new MemoryStream(renderer.RenderPreview(new(settings, "EVE - Aura Asuna", CycleSkipped: true)).Png);
            using var actual = new Bitmap(encoded);
            AssertPixelsEqual(expected, actual, "Skipped marker " + marker.Item1);
            Assert.True(Enumerable.Range(0, actual.Width).Any(x => Enumerable.Range(0, actual.Height).Any(y => actual.GetPixel(x, y).ToArgb() == Color.Red.ToArgb())), "The red marker must actually be rendered.");
            actual.Save(Path.Combine(outputDirectory, "skip-" + marker.Item1.Replace(" ", "-") + ".png"));
            overlay.SetCycleSkipIndicator(false, marker.Item1, Color.Red);
            if (!marker.Item2) Assert.False(label.Visible);
        }

        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly
            .GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        config.ActiveClientHighlightColor = Color.FromArgb(0x72, 0xC9, 0xFF);
        using var highlighted = new HighlightPreview(config) { ClientSize = new Size(384, 216), Title = "EVE - Highlight sample" };
        highlighted.SetFrames(false);
        highlighted.ClientSize = new Size(384, 216);
        highlighted.Show();
        foreach (int thickness in new[] { 1, 3, 6 })
        {
            highlighted.SetHighlight(true, thickness);
            highlighted.RefreshAppearance();
            Application.DoEvents();
            using var expected = new Bitmap(384, 216, PixelFormat.Format32bppArgb);
            highlighted.DrawToBitmap(expected, new Rectangle(Point.Empty, expected.Size));
            var request = Settings(fonts[0]);
            request["ShowThumbnailOverlays"] = "false";
            request["EnableActiveClientHighlight"] = "true";
            request["ActiveClientHighlightThickness"] = thickness.ToString();
            request["ActiveClientHighlightColor"] = "#72C9FF";
            using var encoded = new MemoryStream(renderer.RenderPreview(new(request)).Png);
            using var actual = new Bitmap(encoded);
            expected.Save(Path.Combine(outputDirectory, "native-highlight-" + thickness + ".png"));
            actual.Save(Path.Combine(outputDirectory, "highlight-" + thickness + ".png"));
            AssertPixelsEqual(expected, actual, "Actual ThumbnailView highlight width " + thickness);
        }
        Console.WriteLine("PASS: exact bitmap equality with actual native ThumbnailOverlay across 4 fonts/styles/colors/outlines/offsets, and actual ThumbnailView highlight geometry at 1/3/6px.");
        Console.WriteLine("Native/sample image pairs: " + outputDirectory);
    }

    private static Dictionary<string, string> Settings(FontSettings font) => new()
    {
        ["ThumbnailWidth"] = "384", ["ThumbnailHeight"] = "216", ["ShowThumbnailOverlays"] = "true",
        ["TitleFontName"] = font.Name, ["TitleFontSize"] = font.Size.ToString(CultureInfo.InvariantCulture),
        ["TitleFontStyle"] = font.Style.ToString(), ["TitleFontForeColor"] = ColorTranslator.ToHtml(Color.FromArgb(font.ForeColor.ToArgb())),
        ["TitleFontOutlineColor"] = ColorTranslator.ToHtml(Color.FromArgb(font.OutlineColor.ToArgb())),
        ["TitleFontOutlineWidth"] = font.OutlineWidth.ToString(CultureInfo.InvariantCulture),
        ["TitleFontOffsetLeft"] = font.PositionOffsetFromLeft.ToString(), ["TitleFontOffsetTop"] = font.PositionOffsetFromTop.ToString(),
        ["EnableActiveClientHighlight"] = "false"
    };

    private static byte[] Pixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static void AssertPixelsEqual(Bitmap expected, Bitmap actual, string context)
    {
        Assert.Equal(expected.Size, actual.Size);
        byte[] original = Pixels(expected), preview = Pixels(actual);
        int different = 0, first = -1;
        for (int i = 0; i < original.Length; i++)
            if (original[i] != preview[i]) { different++; if (first < 0) first = i; }
        Assert.True(different == 0, $"{context}: {different} channel differences, first pixel ({first / 4 % expected.Width}, {first / 4 / expected.Width}).");
    }

    private sealed class HighlightPreview : ThumbnailView
    {
        private readonly Panel _image = new() { BackColor = Background };
        public HighlightPreview(IThumbnailConfiguration config)
            : base(Stub.Create<IWindowManager>(), config, null, null, Stub.Create<IKeyboardMouseEvents>())
        {
            Controls.Add(_image);
            _image.BringToFront();
        }
        protected override void RefreshThumbnail(bool forceRefresh) { }
        protected override void ResizeThumbnail(int width, int height, int top, int right, int bottom, int left) =>
            _image.Bounds = new Rectangle(left, top, width - left - right, height - top - bottom);
    }
}
