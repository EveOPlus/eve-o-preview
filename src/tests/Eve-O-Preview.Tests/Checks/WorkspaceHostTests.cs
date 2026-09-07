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
using Avalonia.Win32.Interoperability;
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
using Application = System.Windows.Forms.Application;
using ApplicationContext = System.Windows.Forms.ApplicationContext;
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
        AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        string preferencesPath = Path.Combine(Path.GetTempPath(), "EveOPreviewHost-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var configuration = (IThumbnailConfiguration)Activator.CreateInstance(typeof(ThumbnailView).Assembly
                .GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
            var location = new ProfileLocation { FriendlyName = "Host smoke", FullPath = "host-smoke.json" };
            var storage = Stub.Create<IConfigurationStorage>((method, _) => method.Name == "get_CurrentProfile" ? location : Stub.Default(method.ReturnType));
            var profiles = Stub.Create<IProfileManager>((method, _) => method.Name == "get_ProfileLocations" ? new List<ProfileLocation> { location } : Stub.Default(method.ReturnType));
            var preferences = new ApplicationPreferences(preferencesPath, logger);
            using var form = new WorkspaceForm(context, logger, Stub.Create<IMediator>(), storage, configuration, profiles, preferences,
                Stub.Create<IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)));
            Assert.True(form.MinimizeToTray);
            form.MinimizeToTray = false; // Exercise the explicit full-close path below.
            form.FormCloseRequested = request => request.Allow = true;
            form.CommitSettingsAsync = () => Task.CompletedTask;
            form.CommitSizeAsync = () => Task.CompletedTask;
            ((Form)form).Show(); // Do not call the production message-loop entry point.
            Pump();
            Assert.NotEqual(IntPtr.Zero, form.Handle);
            CaptureWindow(form, "dark-startup");
            var host = Assert.IsAssignableFrom<WinFormsAvaloniaControlHost>(Assert.Single(form.Controls.Cast<System.Windows.Forms.Control>()));
            Assert.True(host.IsHandleCreated && host.Visible);
            var workspace = Assert.IsType<WorkspaceView>(host.Content);
            if (checkDpi)
            {
                CheckDpiChanges(form, host, workspace, preferences);
                return;
            }
            Assert.True(workspace.Bounds.Width > 700 && workspace.Bounds.Height > 500,
                "The production Avalonia child must receive the host client bounds.");
            var versionLabel = Assert.Single(workspace.GetVisualDescendants().OfType<TextBlock>(), control => control.Name == "application-version");
            Assert.Equal("EVE-O Preview", versionLabel.Text);
            string version = typeof(WorkspaceForm).Assembly.GetName().Version!.ToString();
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
                Assert.True(host.Visible);
                if (theme == "Legacy")
                {
                    Assert.Equal(new System.Drawing.Size(460, 417), form.ClientSize);
                    Assert.Equal(460, workspace.Bounds.Width);
                    Assert.False(form.MaximizeBox || form.MinimizeBox);
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
                if (theme == "Legacy") Assert.False(form.MaximizeBox || form.MinimizeBox);
            }
            Assert.True(form.Backend.ExecuteAsync(new WorkspaceCommand("theme", Value: "Dark")).GetAwaiter().GetResult().Success);
            Pump();
            Assert.Equal(modernSize, form.ClientSize);
            CaptureWindow(form, "dark-restored");
            workspace.Navigate("FpsAudio");
            Pump();
            form.Hide();
            Pump();
            Assert.False(form.Visible);
            ((Form)form).Show();
            Pump();
            Assert.True(form.Visible && host.Visible);

            form.Activate();
            host.Focus();
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
            host.Focus();
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
            Console.WriteLine("PASS: real WinForms/Avalonia host on a private desktop; themes, navigation, hide/show, nonactivating refresh, draft close cancel/discard and disposal.");
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

    private static void CheckDpiChanges(WorkspaceForm form, WinFormsAvaloniaControlHost host,
        WorkspaceView workspace, ApplicationPreferences preferences)
    {
        var root = TopLevel.GetTopLevel(workspace)!;
        var window = form.Handle;
        var child = root.TryGetPlatformHandle()!.Handle;
        workspace.Navigate("FpsAudio");
        Pump();
        var editor = workspace.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "setting-FpsFocused");
        editor.Text = "137";
        Assert.True(editor.Focus());

        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            form.Location = new System.Drawing.Point(screen.WorkingArea.Left + 40, screen.WorkingArea.Top + 40);
            Pump();
            Assert.Equal(form.DeviceDpi / 96.0, root.RenderScaling);
            Console.WriteLine($"Native monitor {screen.DeviceName}: DPI {form.DeviceDpi}, render scale {root.RenderScaling}.");
        }
        ChangeDpi(form, 96);

        foreach (int dpi in new[] { 120, 144, 192, 96, 144, 96 })
        {
            ChangeDpi(form, dpi);
            Assert.Equal(dpi / 96.0, root.RenderScaling);
            Assert.InRange(Math.Abs(workspace.Bounds.Width - host.ClientSize.Width / root.RenderScaling), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - host.ClientSize.Height / root.RenderScaling), 0, 1);
            Assert.Equal(window, form.Handle);
            Assert.Equal(child, root.TryGetPlatformHandle()!.Handle);
            Assert.Same(workspace, host.Content);
            Assert.Equal("137", editor.Text);
            Assert.True(editor.IsFocused);
        }

        workspace.Navigate("Previews");
        Pump();
        var image = workspace.GetVisualDescendants().OfType<Image>().Single(control => control.Name == "title-preview-image");
        var bitmap = Assert.IsType<Avalonia.Media.Imaging.Bitmap>(image.Source);
        foreach (int dpi in new[] { 144, 192, 96 })
        {
            ChangeDpi(form, dpi);
            Assert.Same(bitmap, image.Source); // Reuse the client still and rendered title.
            Assert.InRange(Math.Abs(image.Width * root.RenderScaling - bitmap.PixelSize.Width), 0, 0.01);
            Assert.InRange(Math.Abs(image.Height * root.RenderScaling - bitmap.PixelSize.Height), 0, 0.01);
        }

        var modernSize = form.ClientSize;
        preferences.SetTheme("Legacy");
        Pump();
        foreach (int dpi in new[] { 144, 192, 96, 120 })
        {
            ChangeDpi(form, dpi);
            Assert.Equal(dpi / 96.0, root.RenderScaling);
            Assert.Equal((int)Math.Round(460 * dpi / 96.0), form.ClientSize.Width);
            Assert.Equal((int)Math.Round(417 * dpi / 96.0), form.ClientSize.Height);
            // Synthetic DPI messages cannot change the OS nonclient metrics; check
            // logical layout against the actual child's pixel bounds, as above.
            Assert.InRange(Math.Abs(workspace.Bounds.Width - host.ClientSize.Width / root.RenderScaling), 0, 1);
            Assert.InRange(Math.Abs(workspace.Bounds.Height - host.ClientSize.Height / root.RenderScaling), 0, 1);
        }
        preferences.SetTheme("Dark");
        Pump();
        Assert.InRange(Math.Abs(form.ClientSize.Width - modernSize.Width * 1.25), 0, 1);
        Assert.InRange(Math.Abs(form.ClientSize.Height - modernSize.Height * 1.25), 0, 1);
        ChangeDpi(form, 96);
        Assert.InRange(Math.Abs(form.ClientSize.Width - modernSize.Width), 0, 2);
        Console.WriteLine("Synthetic 100/125/150/200% DPI transitions retained the embedded window, layout, focus and draft; Legacy restores modern size at the current DPI.");
    }

    private static void ChangeDpi(Form form, int dpi)
    {
        // Exercise the production WM_DPICHANGED path without changing anyone's display settings.
        // This does not emulate Windows' monitor selection or nonclient metrics.
        double factor = dpi / (double)form.DeviceDpi;
        var bounds = form.Bounds;
        var suggested = new DpiRectangle { Left = bounds.Left, Top = bounds.Top,
            Right = bounds.Left + (int)Math.Round(bounds.Width * factor),
            Bottom = bounds.Top + (int)Math.Round(bounds.Height * factor) };
        SendMessage(form.Handle, 0x02E0, new IntPtr(dpi | (dpi << 16)), ref suggested);
        Pump();
        var host = (WinFormsAvaloniaControlHost)form.Controls[0];
        var root = TopLevel.GetTopLevel(host.Content)!;
        Console.WriteLine($"DPI {dpi}: form {form.ClientSize}, minimum {form.MinimumSize}, host {host.ClientSize}, root {root.ClientSize}, workspace {host.Content.Bounds}, scale {root.RenderScaling}");
        Assert.Equal(dpi, form.DeviceDpi);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DpiRectangle { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, ref DpiRectangle rectangle);

    private static void CaptureWindow(WorkspaceForm form, string name)
    {
        // Window-only visual evidence includes the native caption; never capture the desktop.
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            var dc = graphics.GetHdc();
            try { Assert.True(PrintWindow(form.Handle, dc, 2), "The workspace must render its own window for visual review."); }
            finally { graphics.ReleaseHdc(dc); }
        }
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, "workspace-chrome-" + name + ".png"));
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !System.Windows.Forms.SystemInformation.HighContrast)
        {
            var expected = System.Drawing.ColorTranslator.FromHtml(WorkspaceTheme.Get(form.Backend.Read().Theme).Sidebar);
            // Sample empty caption space, away from the title text and native caption buttons.
            Assert.Equal(expected.ToArgb(), bitmap.GetPixel(bitmap.Width / 2, (int)Math.Round(10 * form.DeviceDpi / 96.0)).ToArgb());
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);

    private static void Pump()
    {
        for (int i = 0; i < 4; i++)
        {
            Application.DoEvents();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }
}
