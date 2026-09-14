using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.VisualTree;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using EveOPreview.Presenters;
using EveOPreview.Services;
using EveOPreview.Services.Interface;
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

public sealed class WindowsShutdownTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("workspace-tray")]
    [InlineData("workspace-exit")]
    [InlineData("workspace-drafts")]
    [InlineData("workspace-busy")]
    [InlineData("workspace-stalled")]
    [InlineData("workspace-closing")]
    [InlineData("old-tray")]
    [InlineData("old-exit")]
    public Task WindowsShutdownAcceptsQueryAndCleansUpOnlyAfterConfirmation(string scenario) =>
        PrivateDesktopRunner.RunAsync("session-" + scenario, output);

    // Only sends messages to this worker's own HWND on an undisplayed desktop.
    // No ExitWindowsEx, shutdown.exe, real EVE processes, or user profiles.
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    internal static void Check(string scenario)
    {
        bool modern = scenario.StartsWith("workspace", StringComparison.Ordinal);
        bool stalled = scenario.EndsWith("stalled", StringComparison.Ordinal);
        bool closing = scenario.EndsWith("closing", StringComparison.Ordinal);
        if (modern) AppBuilder.Configure<WorkspaceApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        using var releaseSave = new ManualResetEventSlim(!stalled);
        var releaseNative = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!stalled && !closing) releaseNative.SetResult();
        var app = typeof(MainForm).Assembly;
        var config = (IThumbnailConfiguration)Activator.CreateInstance(app.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        var location = new ProfileLocation { FriendlyName = "Default", FullPath = "unused-shutdown-test.json" };
        int stops = 0, saves = 0, nativeStops = 0;
        int uiThread = Environment.CurrentManagedThreadId;
        var storage = Stub.Create<IConfigurationStorage>((method, _) =>
        {
            if (method.Name == "get_CurrentProfile") return location;
            if (method.Name == "Save") { Interlocked.Increment(ref saves); releaseSave.Wait(); }
            return Stub.Default(method.ReturnType);
        });
        var profiles = Stub.Create<IProfileManager>((method, _) => method.Name == "get_ProfileLocations"
            ? new List<ProfileLocation> { location } : Stub.Default(method.ReturnType));
        var manager = Stub.Create<IThumbnailManager>((method, _) =>
        {
            if (method.Name == "Stop")
            {
                Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
                stops++;
            }
            return Stub.Default(method.ReturnType);
        });
        var processes = Stub.Create<IProcessMonitor>((method, _) => method.Name == "GetAllProcesses"
            ? new List<IProcessInfo>() : Stub.Default(method.ReturnType));
        var hooks = Stub.Create<IHookService>((method, _) =>
        {
            if (method.Name == "StopAsync") { Interlocked.Increment(ref nativeStops); return releaseNative.Task; }
            return Stub.Default(method.ReturnType);
        });
        // Use the production stop handler as well as the presenter and form;
        // only external native clients and persistence are substituted.
        var handler = Activator.CreateInstance(app.GetType("EveOPreview.Mediator.Handlers.Services.StartStopServiceHandler"),
            manager, processes, hooks, Stub.Create<ICpuAffinityService>(), logger);
        var mediator = Stub.Create<IMediator>((method, args) =>
        {
            if (args.Length > 0 && args[0] is GetCurrentProfileLocation) return Task.FromResult(location);
            if (args.Length > 0 && args[0]?.GetType().Name == "StopService")
                return handler.GetType().GetMethod("Handle", [args[0].GetType(), typeof(CancellationToken)])
                    .Invoke(handler, [args[0], CancellationToken.None]);
            return Stub.Default(method.ReturnType);
        });
        string preferencesPath = Path.Combine(Path.GetTempPath(), "EveOPreviewShutdown-" + Guid.NewGuid().ToString("N") + ".json");
        using Form form = modern
            ? new WorkspaceForm(context, logger, mediator, storage, config, profiles, new ApplicationPreferences(preferencesPath, logger),
                Stub.Create<IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)))
            : new MainForm(context, logger);
        var view = (IMainFormView)form;
        try
        {
            form.Show();
            _ = new MainFormPresenter(Stub.Create<IApplicationController>(), view, mediator, config, storage,
                Stub.Create<IGlobalEvents>(), profiles, logger);
            view.MinimizeToTray = scenario.EndsWith("tray", StringComparison.Ordinal);
            Application.DoEvents();
            WorkspaceView workspace = null;
            if (modern)
            {
                var host = (Avalonia.Win32.Interoperability.WinFormsAvaloniaControlHost)form.Controls[0];
                workspace = (WorkspaceView)host.Content;
                if (scenario.EndsWith("drafts", StringComparison.Ordinal))
                {
                    workspace.Navigate("FpsAudio");
                    Application.DoEvents();
                    workspace.GetVisualDescendants().OfType<Avalonia.Controls.TextBox>()
                        .Single(control => control.Name == "setting-FpsFocused").Text = "165";
                    Application.DoEvents();
                    Assert.True(workspace.HasUnappliedEdits);
                }
                if (scenario.EndsWith("busy", StringComparison.Ordinal))
                    typeof(WindowsWorkspaceBackend).GetProperty(nameof(WindowsWorkspaceBackend.IsBusy))
                        .SetValue(((WorkspaceForm)form).Backend, true);
            }
            bool hadDrafts = workspace?.HasUnappliedEdits == true;
            var windowState = form.WindowState;
            // Both shutdown/restart (0) and sign-out (ENDSESSION_LOGOFF).
            foreach (long flags in new long[] { 0, 0x80000000 })
            {
                Assert.Equal(new IntPtr(1), SendMessage(form.Handle, 0x0011, IntPtr.Zero, new IntPtr(flags)));
                SendMessage(form.Handle, 0x0016, IntPtr.Zero, new IntPtr(flags)); // Another app cancelled.
                Application.DoEvents();
                Assert.False(form.IsDisposed);
                Assert.Equal(windowState, form.WindowState);
                Assert.Equal(hadDrafts, workspace?.HasUnappliedEdits == true);
                Assert.Equal(0, stops);
                Assert.Equal(0, saves);
            }
            if (closing)
            {
                form.Close();
                Application.DoEvents();
                Assert.Equal(1, stops);
                Assert.False(form.IsDisposed);
            }
            Assert.Equal(new IntPtr(1), SendMessage(form.Handle, 0x0011, IntPtr.Zero, IntPtr.Zero));
            var duration = Stopwatch.StartNew();
            SendMessage(form.Handle, 0x0016, new IntPtr(1), IntPtr.Zero);
            Assert.True(form.IsDisposed);
            Assert.True(duration.Elapsed < TimeSpan.FromSeconds(4), "Confirmed shutdown exceeded its bounded cleanup budget.");
            Assert.Equal(1, stops);
            Assert.Equal(1, Volatile.Read(ref saves));
            Assert.Equal(1, Volatile.Read(ref nativeStops));
            view.WindowsSessionEnding(); // Repeated notification must not duplicate cleanup.
            Assert.Equal(1, stops);
            Assert.Equal(1, Volatile.Read(ref saves));
            Console.WriteLine($"{scenario}: query accepted, cancellation preserved state, confirmed cleanup returned in {duration.ElapsedMilliseconds} ms.");
        }
        finally
        {
            releaseSave.Set();
            releaseNative.TrySetResult();
            // Complete any pre-existing ordinary-close UI continuation.
            for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(5); }
            if (File.Exists(preferencesPath)) File.Delete(preferencesPath);
        }
    }
}
