using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Input;
using EveOPreview.Services;
using EveOPreview.UI;
using EveOPreview.View;
using EveOPreview.View.Rendering;
using MediatR;
using Microsoft.Data.Sqlite;
using Serilog;

namespace EveOPreview;

/// <summary>
/// Standalone bundle acceptance mode. Exercises the actual desktop and thumbnail
/// hosts with owned synthetic windows and temporary configuration, without starting
/// discovery, injecting Robin, reading user profiles or registering global shortcuts.
/// </summary>
internal static class DesktopValidation
{
    internal static int Run(string[] args)
    {
        var root = Path.Combine(Path.GetTempPath(), "EveOPreviewDesktop-" + Guid.NewGuid().ToString("N"));
        var outputIndex = Array.IndexOf(args, "--validation-output");
        string output = outputIndex >= 0 && outputIndex + 1 < args.Length
            ? Path.GetFullPath(args[outputIndex + 1]) : root;
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(output);
        var foreground = GetForegroundWindow();
        var checks = new List<string>();
        int result = 1;
        try
        {
            AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithClassicDesktopLifetime(args);
            var lifetime = (ClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            using var logger = new LoggerConfiguration().CreateLogger();
            using var input = new WindowsGlobalPointerInput(logger);
            var builder = Program.CreateApplicationContainerBuilder(logger, input, lifetime);
            builder.Register(context => new ProfileManager(logger, context.Resolve<IMediator>(), Path.Combine(root, "Profiles")))
                .As<IProfileManager>().SingleInstance();
            builder.RegisterInstance(new EmptyPortraits()).As<IWorkspacePortraitProvider>();
            using var container = builder.Build();
            var workspace = container.Resolve<WorkspaceWindow>();
            workspace.FormCloseRequested = request => request.Allow = true;
            lifetime.MainWindow = workspace;
            Dispatcher.UIThread.Post(async () =>
            {
                Window source = null;
                var previews = new List<ThumbnailView>();
                try
                {
                    await Task.Delay(250);
                    Require(workspace.TryGetPlatformHandle()?.Handle != IntPtr.Zero, "Avalonia workspace HWND created");
                    foreach (var theme in new[] { "Light", "Dark", "Legacy" })
                    {
                        var change = await workspace.Backend.ExecuteAsync(new WorkspaceCommand("theme", Value: theme));
                        Require(change.Success, "Workspace theme " + theme);
                        await Task.Delay(100);
                        var size = workspace.Workspace.Bounds.Size;
                        using var rendered = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)));
                        rendered.Render(workspace.Workspace);
                        rendered.Save(Path.Combine(output, "workspace-" + theme.ToLowerInvariant() + ".png"));
                    }
                    using (var database = new SqliteConnection("Data Source=:memory:"))
                    {
                        database.Open();
                        using var command = database.CreateCommand();
                        command.CommandText = "SELECT sqlite_version()";
                        Require(command.ExecuteScalar() is string, "Bundled SQLite native library");
                    }
                    source = new Window
                    {
                        Title = "EVE - Standalone validation source", Width = 640, Height = 360,
                        ShowActivated = false, ShowInTaskbar = false, Position = new PixelPoint(40, 420),
                        Background = Brushes.SteelBlue,
                        Content = new TextBlock { Text = "EVE-O Preview synthetic client", Foreground = Brushes.White, FontSize = 28 }
                    };
                    source.Show();
                    await Task.Delay(250);
                    var sourceHandle = source.TryGetPlatformHandle().Handle;
                    var config = container.Resolve<IThumbnailConfiguration>();
                    config.EnableThumbnailSnap = false;
                    config.ShowThumbnailFrames = false;
                    config.EnableAutomaticCpuAffinity = false;
                    var factory = container.Resolve<IThumbnailViewFactory>();
                    var preferences = container.Resolve<ApplicationPreferences>();
                    preferences.SetPreviewOverlayRenderer("NativeComposition");
                    foreach (bool compatibility in new[] { false, true })
                    {
                        config.EnableCompatibilityMode = compatibility;
                        var preview = (ThumbnailView)factory.Create(sourceHandle, "EVE - Standalone validation source", new System.Drawing.Size(384, 216));
                        previews.Add(preview);
                        preview.ThumbnailLocation = new System.Drawing.Point(40 + previews.Count * 400, 100);
                        preview.IsOverlayEnabled = true;
                        preview.SetOpacity(1);
                        preview.SetFrames(false);
                        preview.Show();
                        await Task.Delay(150);
                        preview.Refresh(true);
                        Require(preview.Handle != IntPtr.Zero && IsWindowVisible(preview.Handle), compatibility ? "Static preview host" : "DWM preview host");
                        Require(preview.ThumbnailSize == new System.Drawing.Size(384, 216), "Native client-pixel dimensions");
                        Require(preview.OverlayRenderer == OverlayRendererKind.NativeComposition, "DirectComposition graphics active");
                        var handle = preview.Handle;
                        preview.Hide();
                        preview.Show();
                        Require(preview.Handle == handle, "Hide/show retains destination HWND");
                        Require(preview.RestoreAndBringToFront(), "Nonactivating native restoration");
                    }
                    var windows = container.Resolve<IWindowManager>();
                    using (var captured = windows.GetStaticThumbnail(sourceHandle))
                        Require(captured != null, "Static backend captures only the owned synthetic source");
                    var forbidden = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name)
                        .Where(name => name is "System.Windows.Forms" or "PresentationFramework" or "PresentationCore" or "Gma.System.MouseKeyHook"
                            || name?.StartsWith("Avalonia.Win32.Interoperability", StringComparison.Ordinal) == true).ToArray();
                    Require(forbidden.Length == 0, "No retired UI or input assemblies loaded");
                    result = 0;
                }
                catch (Exception error)
                {
                    checks.Add("FAIL: " + error);
                    Console.Error.WriteLine(error);
                }
                finally
                {
                    foreach (var preview in previews) preview.Dispose();
                    source?.Close();
                    workspace.Dispose();
                    File.WriteAllText(Path.Combine(output, "desktop-validation.json"), System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Passed = result == 0, Checks = checks,
                        Scope = "Standalone synthetic desktop hosts, UI resources and SQLite. No live EVE, Robin injection, driver reset or mixed-DPI validation."
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    lifetime.Shutdown(result);
                }
            });
            lifetime.Start(args);
            return result;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if (foreground != IntPtr.Zero) SetForegroundWindow(foreground);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        void Require(bool success, string check)
        {
            if (!success) throw new InvalidOperationException(check);
            checks.Add("PASS: " + check);
        }
    }

    private sealed class EmptyPortraits : IWorkspacePortraitProvider
    {
        public Task<byte[]> GetCharacterPortraitAsync(long characterId) => Task.FromResult<byte[]>(null);
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
}
