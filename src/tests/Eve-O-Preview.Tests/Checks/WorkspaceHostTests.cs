using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;
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

namespace EveOPreview.Tests.Checks;

public sealed class WorkspaceHostTests(ITestOutputHelper output)
{
    [Fact]
    public Task PortableWorkspaceEmbedsInWindowsAndSurvivesThemeAndVisibilityChanges() =>
        PrivateDesktopRunner.RunAsync("workspace-host", output);

    [Fact]
    public Task WorkspaceTracksDpiChangesWithoutReplacingContentOrLosingDrafts() =>
        PrivateDesktopRunner.RunAsync("workspace-dpi", output);

    internal static void CheckHost(bool checkDpi = false)
    {
        TestAvalonia.Initialize();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ClassicDesktopStyleApplicationLifetime();
        string preferencesPath = Path.Combine(Path.GetTempPath(), "EveOPreviewHost-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var configuration = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly
                .GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
            var location = new ProfileLocation { FriendlyName = "Host smoke", FullPath = "host-smoke.json" };
            var storage = Stub.Create<IConfigurationStorage>((method, _) => method.Name == "get_CurrentProfile" ? location : Stub.Default(method.ReturnType));
            var profiles = Stub.Create<IProfileManager>((method, _) => method.Name == "get_ProfileLocations" ? new List<ProfileLocation> { location } : Stub.Default(method.ReturnType));
            var preferences = new ApplicationPreferences(preferencesPath, logger);
            using var form = new WorkspaceWindow(context, logger, Stub.Create<IMediator>(), storage, configuration, profiles, preferences,
                Stub.Create<IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)));
            Assert.True(form.MinimizeToTray);
            form.MinimizeToTray = false; // Exercise the explicit full-close path below.
            form.FormCloseRequested = request => request.Allow = true;
            form.CommitSettingsAsync = () => Task.CompletedTask;
            form.CommitSizeAsync = () => Task.CompletedTask;
            form.Show();
            Pump();
            Assert.NotEqual(IntPtr.Zero, form.TryGetPlatformHandle()!.Handle);
            CaptureWindow(form, "dark-startup");
            var workspace = form.Workspace;
            if (checkDpi)
            {
                CheckDpiChanges(form, workspace, preferences);
                return;
            }
            Assert.True(workspace.Bounds.Width > 700 && workspace.Bounds.Height > 500,
                "The production workspace must receive its window client bounds.");
            var versionLabel = Assert.Single(workspace.GetVisualDescendants().OfType<TextBlock>(), control => control.Name == "application-version");
            Assert.Equal("EVE-O Preview", versionLabel.Text);
            string version = typeof(WorkspaceWindow).Assembly.GetName().Version!.ToString();
            form.SetVersionInfo(version); // Presenter supplies metadata after the shell is constructed.
            Pump();
            Assert.Equal("EVE-O Preview  \u00b7  " + version, versionLabel.Text);
            var modernSize = form.ClientSize;

            foreach (string theme in new[] { "Light", "Dark", "Legacy" })
            {
                var result = form.Backend.ExecuteAsync(new WorkspaceCommand("theme", Value: theme)).GetAwaiter().GetResult();
                Assert.True(result.Success, result.Message);
                Pump();
                Assert.Equal(theme, form.Backend.Read().Theme);
                CaptureWindow(form, theme.ToLowerInvariant());
                CheckDialogs(form, theme);
                preferences.SetThumbnailMenuOrder(ThumbnailMenuActions.Normalize(["skip-cycling"]));
                Pump();
                Assert.Equal("skip-cycling", EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailMenuOrder[0]);
                preferences.SetThumbnailMenuOrder(ThumbnailMenuActions.DefaultOrder);
                Assert.Equal("minimize", EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailMenuOrder[0]);
                preferences.SetThumbnailMenuTheme("gallente");
                Assert.Equal("gallente", EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailTheme);
                preferences.SetThumbnailMenuTheme(ThumbnailMenuThemes.FollowApp);
                workspace.Navigate("FpsAudio");
                Pump();
                Assert.True(form.IsVisible);
                if (theme == "Legacy")
                {
                    Assert.Equal(new Avalonia.Size(460, 417), form.ClientSize);
                    Assert.Equal(460, workspace.Bounds.Width);
                    Assert.False(form.CanMaximize || form.CanMinimize);
                }
                else Assert.True(workspace.Bounds.Width > 700);
                workspace.Navigate("About");
                Pump();
                Assert.DoesNotContain(workspace.GetVisualDescendants().OfType<Border>(), control => control.Name == "support-dialog");
                Click(workspace, "support-open");
                Assert.Contains(workspace.GetVisualDescendants().OfType<SelectableTextBlock>(), control => control.Text == "Aura Asuna");
                Click(workspace, "support-close");
                form.CycleGroups = new List<CycleGroup> { new() { Description = "Fleet", ClientsOrder = new() { [1] = "EVE - Aura" } } };
                form.Backend.NotifyChanged(); Pump();
                workspace.Navigate("Switching"); Pump();
                var originalBounds = form.Bounds;
                var originalState = form.WindowState;
                Click(workspace, "expand-cycle-order"); Pump();
                Assert.Equal(originalState, form.WindowState);
                Assert.Equal(originalBounds, form.Bounds);
                Assert.Contains(workspace.GetVisualDescendants().OfType<Border>(), control => control.Name == "expanded-cycle-order");
                Click(workspace, "close-cycle-order"); Pump();
                Assert.Equal(originalState, form.WindowState);
                Assert.Equal(originalBounds, form.Bounds);
                if (theme == "Legacy") Assert.False(form.CanMaximize || form.CanMinimize);
            }
            Assert.True(form.Backend.ExecuteAsync(new WorkspaceCommand("theme", Value: "Dark")).GetAwaiter().GetResult().Success);
            Pump();
            Assert.Equal(modernSize, form.ClientSize);
            CaptureWindow(form, "dark-restored");
            workspace.Navigate("FpsAudio");
            Pump();
            form.Hide();
            Pump();
            Assert.False(form.IsVisible);
            form.Show();
            Pump();
            Assert.True(form.IsVisible && workspace.IsEffectivelyVisible);

            form.Activate();
            workspace.Focus();
            var editor = workspace.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "setting-FpsFocused");
            Assert.True(editor.Focus(), "The workspace editor must actually receive focus before the inactive-refresh regression check.");
            Pump();
            using (var simulatedClient = new Form { Text = "EVE - Focus regression fixture" })
            {
                simulatedClient.Show();
                simulatedClient.Activate();
                Native.SetActiveWindow(simulatedClient.Handle);
                Pump();
                IntPtr active = Native.GetActiveWindow();
                IntPtr foreground = Native.GetForegroundWindow();
                Assert.NotEqual(IntPtr.Zero, active);
                Assert.Equal(simulatedClient.Handle, active);

                form.Backend.NotifyChanged();
                Pump();

                Assert.Equal(active, Native.GetActiveWindow());
                Assert.Equal(foreground, Native.GetForegroundWindow());
            }

            form.Activate();
            workspace.Focus();
            editor = workspace.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "setting-FpsFocused");
            editor.Text = "165";
            Pump();
            Assert.True(workspace.HasUnappliedEdits);
            form.Close();
            Pump();
            Assert.False(form.IsDisposed);
            Click(workspace, "cancel-confirmation");
            Assert.False(form.IsDisposed);
            Assert.True(workspace.HasUnappliedEdits);
            Assert.Equal("165", workspace.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "setting-FpsFocused").Text);

            form.Close();
            Pump();
            Click(workspace, "accept-confirmation");
            Assert.True(form.IsDisposed);
            Console.WriteLine("PASS: real Avalonia host on a private desktop; themes, navigation, hide/show, nonactivating refresh, draft close cancel/discard and disposal.");
        }
        finally { if (File.Exists(preferencesPath)) File.Delete(preferencesPath); }
    }

    private static void Click(WorkspaceView workspace, string name)
    {
        var button = workspace.GetVisualDescendants().OfType<Button>().Single(control => control.Name == name);
        Assert.True(button.IsEnabled && button.IsEffectivelyVisible);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
    }

    private static void CheckDialogs(WorkspaceWindow owner, string theme)
    {
        var originalFont = owner.TitleFontSettings;
        float originalSize = originalFont.Size;
        var fontCommand = owner.Backend.ExecuteAsync(new WorkspaceCommand("font-picker"));
        Pump();
        var font = Assert.Single(owner.OwnedWindows);
        Assert.IsType<Window>(font);
        Assert.Equal(owner.ActualThemeVariant, font.ActualThemeVariant);
        font.GetVisualDescendants().OfType<NumericUpDown>().Single(x => x.Name == "dialog-font-size").Value = 27.5m;
        CaptureDialog(font, "font-" + theme);
        font.GetVisualDescendants().OfType<Button>().Single(x => x.IsCancel).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestAvalonia.PumpUntil(() => fontCommand.IsCompleted);
        Assert.True(fontCommand.GetAwaiter().GetResult().WasCancelled);
        Assert.Same(originalFont, owner.TitleFontSettings);
        Assert.Equal(originalSize, owner.TitleFontSettings.Size);

        fontCommand = owner.Backend.ExecuteAsync(new WorkspaceCommand("font-picker")); Pump();
        font = Assert.Single(owner.OwnedWindows);
        font.GetVisualDescendants().OfType<NumericUpDown>().Single(x => x.Name == "dialog-font-size").Value = 16.5m;
        font.GetVisualDescendants().OfType<Button>().Single(x => x.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestAvalonia.PumpUntil(() => fontCommand.IsCompleted);
        Assert.True(fontCommand.GetAwaiter().GetResult().Success);
        Assert.Equal(16.5f, owner.TitleFontSettings.Size);
        owner.TitleFontSettings.Size = originalSize;

        var initialColor = owner.ActiveClientHighlightColor;
        var colorCommand = owner.Backend.ExecuteAsync(new WorkspaceCommand("color-picker", Target: "ActiveClientHighlightColor")); Pump();
        var color = Assert.Single(owner.OwnedWindows);
        color.GetVisualDescendants().OfType<ColorView>().Single(x => x.Name == "dialog-color").Color = Avalonia.Media.Colors.Cyan;
        CaptureDialog(color, "color-" + theme);
        color.Close(); TestAvalonia.PumpUntil(() => colorCommand.IsCompleted);
        Assert.Equal(initialColor, owner.ActiveClientHighlightColor);
        Assert.True(colorCommand.GetAwaiter().GetResult().WasCancelled);
    }

    private static void CaptureDialog(Window dialog, string name)
    {
        Pump();
        var size = PixelSize.FromSize(dialog.ClientSize, dialog.RenderScaling);
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(size, new Vector(96 * dialog.RenderScaling, 96 * dialog.RenderScaling));
        bitmap.Render(dialog);
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, "workspace-dialog-" + name.ToLowerInvariant() + ".png"));
    }

    private static void CheckDpiChanges(WorkspaceWindow form, WorkspaceView workspace, ApplicationPreferences preferences)
    {
        var window = form.TryGetPlatformHandle()!.Handle;
        workspace.Navigate("FpsAudio"); Pump();
        var editor = workspace.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "setting-FpsFocused");
        editor.Text = "137"; Assert.True(editor.Focus());
        foreach (var screen in form.Screens.All)
        {
            form.Position = new PixelPoint(screen.WorkingArea.X + 40, screen.WorkingArea.Y + 40); Pump();
            Console.WriteLine($"Native monitor {screen.DisplayName}: render scale {form.RenderScaling}.");
        }
        ChangeDpi(form, 96);
        foreach (int dpi in new[] { 120, 144, 192, 96, 144, 96 })
        {
            ChangeDpi(form, dpi);
            Assert.Equal(dpi / 96.0, form.RenderScaling);
            Assert.InRange(Math.Abs(workspace.Bounds.Width - form.ClientSize.Width), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - form.ClientSize.Height), 0, 1);
            Assert.Equal(window, form.TryGetPlatformHandle()!.Handle);
            Assert.Same(workspace, form.Content);
            Assert.Equal("137", editor.Text); Assert.True(editor.IsFocused);
        }
        workspace.Navigate("Previews"); Pump();
        var image = workspace.GetVisualDescendants().OfType<Image>().Single(control => control.Name == "title-preview-image");
        var bitmap = Assert.IsType<Avalonia.Media.Imaging.Bitmap>(image.Source);
        foreach (int dpi in new[] { 144, 192, 96 })
        {
            ChangeDpi(form, dpi);
            Assert.Same(bitmap, image.Source);
            Assert.InRange(Math.Abs(image.Width * form.RenderScaling - bitmap.PixelSize.Width), 0, 0.01);
            Assert.InRange(Math.Abs(image.Height * form.RenderScaling - bitmap.PixelSize.Height), 0, 0.01);
        }
        var modernSize = form.ClientSize;
        preferences.SetTheme("Legacy"); Pump();
        foreach (int dpi in new[] { 144, 192, 96, 120 })
        {
            ChangeDpi(form, dpi);
            Assert.Equal(dpi / 96.0, form.RenderScaling);
            Assert.InRange(Math.Abs(form.ClientSize.Width - 460), 0, 1);
            Assert.InRange(Math.Abs(form.ClientSize.Height - 417), 0, 1);
        }
        preferences.SetTheme("Dark"); Pump();
        Assert.InRange(Math.Abs(form.ClientSize.Width - modernSize.Width), 0, 1);
        Assert.InRange(Math.Abs(form.ClientSize.Height - modernSize.Height), 0, 1);
        ChangeDpi(form, 96);
        Assert.InRange(Math.Abs(form.ClientSize.Width - modernSize.Width), 0, 2);
        Console.WriteLine("Synthetic 100/125/150/200% DPI transitions retain native window, layout, focus and draft; modern size stays in logical units across Legacy.");
    }

    private static void ChangeDpi(WorkspaceWindow form, int dpi)
    {
        // Synthetic messages do not emulate Windows monitor selection/nonclient metrics.
        var handle = form.TryGetPlatformHandle()!.Handle;
        GetWindowRect(handle, out var outer);
        GetClientRect(handle, out var client);
        var width = (int)Math.Round(form.ClientSize.Width * dpi / 96.0);
        var height = (int)Math.Round(form.ClientSize.Height * dpi / 96.0);
        var suggested = new DpiRectangle { Left = outer.Left, Top = outer.Top,
            Right = outer.Left + width + (outer.Right - outer.Left - client.Right),
            Bottom = outer.Top + height + (outer.Bottom - outer.Top - client.Bottom) };
        SendMessage(handle, 0x02E0, new IntPtr(dpi | (dpi << 16)), ref suggested); Pump();
        GetWindowRect(handle, out outer); GetClientRect(handle, out client);
        Console.WriteLine($"DPI {dpi}: requested outer {suggested}, actual outer {outer}, client pixels {client}, logical {form.ClientSize}, scale {form.RenderScaling}");
        Assert.Equal(dpi / 96.0, form.RenderScaling);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DpiRectangle
    {
        public int Left, Top, Right, Bottom;
        public override readonly string ToString() => $"({Left},{Top}) {Right - Left}x{Bottom - Top}";
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, ref DpiRectangle rectangle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out DpiRectangle rectangle);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out DpiRectangle rectangle);

    private static void CaptureWindow(WorkspaceWindow form, string name)
    {
        // Window-only visual evidence includes the native caption; never capture the desktop.
        GetWindowRect(form.TryGetPlatformHandle()!.Handle, out var outer);
        using var bitmap = new System.Drawing.Bitmap(outer.Right - outer.Left, outer.Bottom - outer.Top);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            var dc = graphics.GetHdc();
            try { Assert.True(PrintWindow(form.TryGetPlatformHandle()!.Handle, dc, 2), "The workspace must render its own window for visual review."); }
            finally { graphics.ReleaseHdc(dc); }
        }
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, "workspace-chrome-" + name + ".png"));
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !System.Windows.Forms.SystemInformation.HighContrast)
        {
            var expected = System.Drawing.ColorTranslator.FromHtml(WorkspaceTheme.Get(form.Backend.Read().Theme).Sidebar);
            // Sample empty caption space, away from the title text and native caption buttons.
            Assert.Equal(expected.ToArgb(), bitmap.GetPixel(bitmap.Width / 2, (int)Math.Round(10 * form.RenderScaling)).ToArgb());
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);

    private static void Pump()
    {
        for (int i = 0; i < 4; i++)
        {
            TestAvalonia.Pump();
        }
    }
}
