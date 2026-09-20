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
using Avalonia.Controls.ApplicationLifetimes;
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
    [InlineData("workspace-input-stalled")]
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
        bool inputStalled = scenario == "workspace-input-stalled";
        bool closing = scenario.EndsWith("closing", StringComparison.Ordinal);
        TestAvalonia.Initialize();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var context = new ApplicationContext();
        using var releaseSave = new ManualResetEventSlim(!stalled);
        using var releaseKeyboard = new ManualResetEventSlim(!inputStalled);
        var releaseNative = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!stalled && !closing) releaseNative.SetResult();
        var app = typeof(ThumbnailView).Assembly;
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
            if (method.Name is "Stop" or "StopAsync")
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
        ThumbnailManager realManager = null;
        if (inputStalled)
        {
            bool stopArmed = false;
            var keyboard = Stub.Create<IHotkeyService>((method, _) =>
            {
                if (method.Name == "Replace" && stopArmed)
                {
                    Assert.NotEqual(uiThread, Environment.CurrentManagedThreadId);
                    Interlocked.Increment(ref stops);
                    releaseKeyboard.Wait();
                }
                return Stub.Default(method.ReturnType);
            });
            realManager = new ThumbnailManager(mediator, config, processes, Stub.Create<IWindowManager>(),
                Stub.Create<IThumbnailViewFactory>(), keyboard, hooks, Stub.Create<IGlobalEvents>(), logger);
            stopArmed = true;
            handler = Activator.CreateInstance(app.GetType("EveOPreview.Mediator.Handlers.Services.StartStopServiceHandler"),
                realManager, processes, hooks, Stub.Create<ICpuAffinityService>(), logger);
            // Only keyboard cleanup stalls in this regression. Persistence and client
            // reset must complete despite the actual manager waiting on native input.
            releaseSave.Set();
            releaseNative.TrySetResult();
        }
        string preferencesPath = Path.Combine(Path.GetTempPath(), "EveOPreviewShutdown-" + Guid.NewGuid().ToString("N") + ".json");
        WorkspaceWindow workspaceWindow = null;
        var lifetime = Stub.Create<IClassicDesktopStyleApplicationLifetime>((method, _) =>
        {
            if (method.Name == "Shutdown") workspaceWindow?.Close();
            return Stub.Default(method.ReturnType);
        });
        if (modern) workspaceWindow = new WorkspaceWindow(lifetime, logger, mediator, storage, config, profiles,
            new ApplicationPreferences(preferencesPath, logger), Stub.Create<IWorkspacePortraitProvider>((_, _) => Task.FromResult<byte[]>(null)));
        using IDisposable form = modern ? workspaceWindow : new MainForm(context, logger);
        var view = (IMainFormView)form;
        IntPtr Handle() => modern ? workspaceWindow.TryGetPlatformHandle()!.Handle : ((Form)form).Handle;
        bool Disposed() => modern ? workspaceWindow.IsDisposed : ((Form)form).IsDisposed;
        object State() => modern ? workspaceWindow.WindowState : ((Form)form).WindowState;
        try
        {
            if (modern) workspaceWindow.Show(); else ((Form)form).Show();
            _ = new MainFormPresenter(Stub.Create<IApplicationController>(), view, mediator, config, storage,
                Stub.Create<IGlobalEvents>(), profiles, logger);
            view.MinimizeToTray = scenario.EndsWith("tray", StringComparison.Ordinal);
            TestAvalonia.Pump();
            WorkspaceView workspace = null;
            if (modern)
            {
                workspace = workspaceWindow.Workspace;
                if (scenario.EndsWith("drafts", StringComparison.Ordinal))
                {
                    workspace.Navigate("FpsAudio");
                    TestAvalonia.Pump();
                    workspace.GetVisualDescendants().OfType<Avalonia.Controls.TextBox>()
                        .Single(control => control.Name == "setting-FpsFocused").Text = "165";
                    TestAvalonia.Pump();
                    Assert.True(workspace.HasUnappliedEdits);
                }
                if (scenario.EndsWith("busy", StringComparison.Ordinal))
                    typeof(WindowsWorkspaceBackend).GetProperty(nameof(WindowsWorkspaceBackend.IsBusy))
                        .SetValue(workspaceWindow.Backend, true);
            }
            bool hadDrafts = workspace?.HasUnappliedEdits == true;
            var windowState = State();
            // Both shutdown/restart (0) and sign-out (ENDSESSION_LOGOFF).
            foreach (long flags in new long[] { 0, 0x80000000 })
            {
                Assert.Equal(new IntPtr(1), SendMessage(Handle(), 0x0011, IntPtr.Zero, new IntPtr(flags)));
                SendMessage(Handle(), 0x0016, IntPtr.Zero, new IntPtr(flags)); // Another app cancelled.
                if (modern)
                {
                    // Avalonia owns a second, hidden dispatch HWND. Its stock lifetime
                    // closes during QUERY; the production adapter must defer that too.
                    Assert.NotEqual(IntPtr.Zero, workspaceWindow.SessionMessageWindow);
                    Assert.Equal(new IntPtr(1), SendMessage(workspaceWindow.SessionMessageWindow, 0x0011, IntPtr.Zero, new IntPtr(flags)));
                    SendMessage(workspaceWindow.SessionMessageWindow, 0x0016, IntPtr.Zero, new IntPtr(flags));
                }
                TestAvalonia.Pump();
                Assert.False(Disposed());
                Assert.Equal(windowState, State());
                Assert.Equal(hadDrafts, workspace?.HasUnappliedEdits == true);
                Assert.Equal(0, stops);
                Assert.Equal(0, saves);
            }
            if (closing)
            {
                view.Close();
                TestAvalonia.Pump();
                Assert.Equal(1, stops);
                Assert.False(Disposed());
            }
            Assert.Equal(new IntPtr(1), SendMessage(Handle(), 0x0011, IntPtr.Zero, IntPtr.Zero));
            var timer = realManager is null ? null : (Avalonia.Threading.DispatcherTimer)typeof(ThumbnailManager)
                .GetField("_thumbnailUpdateTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(realManager);
            timer?.Start();
            var duration = Stopwatch.StartNew();
            var confirmationWindow = modern && scenario.EndsWith("busy", StringComparison.Ordinal)
                ? workspaceWindow.SessionMessageWindow : Handle();
            SendMessage(confirmationWindow, 0x0016, new IntPtr(1), IntPtr.Zero);
            Assert.True(Disposed());
            Assert.True(duration.Elapsed < TimeSpan.FromSeconds(4), "Confirmed shutdown exceeded its bounded cleanup budget.");
            Assert.Equal(1, stops);
            Assert.Equal(1, Volatile.Read(ref saves));
            Assert.Equal(1, Volatile.Read(ref nativeStops));
            if (inputStalled)
            {
                Assert.False(timer.IsEnabled);
                Assert.False(releaseKeyboard.IsSet);
                // Container disposal follows the callback. It must reuse the pending
                // input stop and never synchronously unregister the same hooks again.
                realManager.Dispose();
                Assert.True(duration.Elapsed < TimeSpan.FromSeconds(4));
            }
            view.WindowsSessionEnding(); // Repeated notification must not duplicate cleanup.
            Assert.Equal(1, stops);
            Assert.Equal(1, Volatile.Read(ref saves));
            Console.WriteLine($"{scenario}: query accepted, cancellation preserved state, confirmed cleanup returned in {duration.ElapsedMilliseconds} ms.");
        }
        finally
        {
            releaseSave.Set();
            releaseKeyboard.Set();
            releaseNative.TrySetResult();
            // Complete any pre-existing ordinary-close UI continuation.
            for (int i = 0; i < 20; i++) { TestAvalonia.Pump(); Thread.Sleep(5); }
            realManager?.Dispose();
            if (File.Exists(preferencesPath)) File.Delete(preferencesPath);
        }
    }
}
