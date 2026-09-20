using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Tests.Infrastructure;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class HotkeyTests(ITestOutputHelper output)
{
    [Fact]
    public void HeldModifiersRepeatAndEarlyModifierReleasePreserveCycles()
    {
        int forward = 0, backward = 0;
        var matcher = new WindowsHotkeyMatcher(action => action());
        matcher.Configure(new()
        {
            [Keys.Control | Keys.F8] = new("Control+F8", () => forward++),
            [Keys.Control | Keys.Shift | Keys.F8] = new("Control+Shift+F8", () => backward++)
        }, _ => false);
        Assert.False(matcher.Process(0xA2, true));
        for (int i = 0; i < 3; i++) { Assert.True(matcher.Process(0x77, true)); Assert.True(matcher.Process(0x77, false)); }
        matcher.Process(0xA0, true);
        Assert.True(matcher.Process(0x77, true));
        Assert.True(matcher.Process(0x77, true)); // Preserve deliberate hold-to-cycle auto-repeat.
        matcher.Process(0xA2, false); matcher.Process(0xA0, false);
        Assert.True(matcher.Process(0x77, false));
        Assert.False(matcher.Process(0x41, true));
        Assert.False(matcher.Process(0x41, false));
        Assert.Equal(3, forward); Assert.Equal(2, backward);
    }

    [Fact]
    public void ReleaseActionRunsOnceAndCaptureNeverRunsAConfiguredAction()
    {
        int actions = 0;
        Keys captured = Keys.None;
        var matcher = new WindowsHotkeyMatcher(action => action());
        var bindings = new Dictionary<Keys, HotkeyBinding> { [Keys.Control | Keys.F8] = new("Control+F8", () => actions++, true) };
        matcher.Configure(bindings, key => key == 0xA3);
        matcher.Process(0x77, true); matcher.Process(0x77, true);
        Assert.Equal(0, actions);
        matcher.Process(0xA3, false);
        Assert.True(matcher.Process(0x77, false));
        Assert.Equal(1, actions);
        matcher.Configure(bindings, _ => false, key => captured = key);
        matcher.Process(0xA2, true); matcher.Process(0x77, true); matcher.Process(0xA2, false);
        Assert.True(matcher.Process(0x77, false));
        Assert.Equal(Keys.Control | Keys.F8, captured);
        Assert.Equal(1, actions);
    }

    [Fact]
    public void UnmatchedInputAllocatesNothingAfterWarmup()
    {
        var matcher = new WindowsHotkeyMatcher(_ => throw new Exception("Unexpected action"));
        matcher.Configure(new() { [Keys.Control | Keys.F8] = new("Control+F8", () => { }) }, _ => false);
        for (int i = 0; i < 10000; i++) { matcher.Process(0x41, true); matcher.Process(0x41, false); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) { matcher.Process(0x41, true); matcher.Process(0x41, false); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void BusyUiNeverBlocksInputAndOldProfileActionsAreDiscarded()
    {
        var ui = new Queue<Action>();
        var actual = new List<int>();
        var queue = new HotkeyDispatchQueue(ui.Enqueue, ex => throw ex);
        for (int i = 0; i < 20; i++) { int value = i; queue.Enqueue(() => actual.Add(value)); }
        Assert.Empty(actual); Assert.Single(ui); // No synchronous UI invocation and no timer.
        while (ui.TryDequeue(out var drain)) drain();
        Assert.Equal(Enumerable.Range(0, 20), actual);
        queue.Enqueue(() => actual.Add(-1));
        queue.Invalidate();
        queue.Enqueue(() => actual.Add(20));
        while (ui.TryDequeue(out var drain)) drain();
        Assert.Equal(Enumerable.Range(0, 21), actual);
        actual.Clear();
        for (int i = 0; i < 1000; i++) { int value = i; queue.Enqueue(() => actual.Add(value)); }
        while (ui.TryDequeue(out var drain)) drain();
        Assert.Equal(Enumerable.Range(936, 64), actual); // Backlog remains bounded during a stalled UI.
    }

    [Fact]
    public void ProfileHotkeysDefaultToGlobalKeyDownAndRoundTrip()
    {
        var config = new ThumbnailConfiguration();
        Assert.False(config.UseWindowsHotkeys);
        Assert.False(config.GlobalHotkeysOnRelease);
        config.UseWindowsHotkeys = true;
        config.GlobalHotkeysOnRelease = true;
        var reloaded = Newtonsoft.Json.JsonConvert.DeserializeObject<ThumbnailConfiguration>(Newtonsoft.Json.JsonConvert.SerializeObject(config));
        Assert.True(reloaded.UseWindowsHotkeys);
        Assert.True(reloaded.GlobalHotkeysOnRelease);
    }

    [Fact]
    public Task WindowsRegistrationsReleaseAcrossCaptureProfileModeAndDisposal() => PrivateDesktopRunner.RunAsync("hotkey-lifecycle", output);

    internal static void CheckLifecycle()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var ui = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var service = new WindowsHotkeyService(logger, ui.Enqueue);
        int actions = 0;
        // Registrations can conflict with other desktop sessions, including a running app.
        // Probe unused keys rather than depending on the user's configured cycle shortcuts.
        var available = new List<uint>();
        for (uint key = 0x7C; key <= 0x87 && available.Count < 2; key++)
        {
            if (!RegisterHotKey(IntPtr.Zero, 17, 2, key)) continue;
            Assert.True(UnregisterHotKey(IntPtr.Zero, 17)); available.Add(key);
        }
        Assert.Equal(2, available.Count);
        uint firstKey = available[0], secondKey = available[1];
        string firstShortcut = "Control+" + (Keys)firstKey, secondShortcut = "Control+" + (Keys)secondKey;
        HotkeyBinding[] first = [new(firstShortcut, () => actions++)];
        HotkeyBinding[] second = [new(secondShortcut, () => actions++)];
        void Available(uint key, bool expected)
        {
            bool registered = RegisterHotKey(IntPtr.Zero, 17, 2, key);
            if (registered) Assert.True(UnregisterHotKey(IntPtr.Zero, 17));
            Assert.Equal(expected, registered);
        }
        var type = typeof(WindowsHotkeyService);
        var invoke = type.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
        var matcher = (WindowsHotkeyMatcher)type.GetField("_matcher", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
        void Input(Action action) => invoke.Invoke(service, [action, false]);
        try
        {
            Assert.True(RegisterHotKey(IntPtr.Zero, 18, 2, firstKey));
            try
            {
                service.Replace(first, HotkeyMode.OperatingSystem);
                Assert.Contains(firstShortcut, service.RegistrationWarning);
            }
            finally { Assert.True(UnregisterHotKey(IntPtr.Zero, 18)); }
            service.Replace(first, HotkeyMode.OperatingSystem);
            Assert.Empty(service.RegistrationWarning); Available(firstKey, false);
            service.Replace(second, HotkeyMode.OperatingSystem);
            Available(firstKey, true); Available(secondKey, false);
            using (var cancel = new CancellationTokenSource())
            {
                var capture = service.CaptureAsync(TimeSpan.FromSeconds(5), cancel.Token);
                Available(secondKey, true);
                service.Replace(first, HotkeyMode.OperatingSystem); // Profile changed while capture is pending.
                Available(firstKey, true);
                cancel.Cancel();
                Assert.ThrowsAny<OperationCanceledException>(() => capture.GetAwaiter().GetResult());
            }
            Available(firstKey, false); Available(secondKey, true);
            var timeout = service.CaptureAsync(TimeSpan.FromMilliseconds(30), CancellationToken.None);
            Assert.Throws<TimeoutException>(() => timeout.GetAwaiter().GetResult());
            Available(firstKey, false);
            foreach (bool escape in new[] { false, true })
            {
                var capture = service.CaptureAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                Available(firstKey, true);
                Input(() => { matcher.Process(0xA2, true); matcher.Process(escape ? 0x1B : (int)firstKey, true); matcher.Process(escape ? 0x1B : (int)firstKey, false); });
                Assert.Equal(escape ? null : new KeysConverter().ConvertToInvariantString(Keys.Control | (Keys)firstKey), capture.GetAwaiter().GetResult());
                Available(firstKey, false);
                Assert.Equal(0, actions);
            }
            service.Replace(first, HotkeyMode.Global);
            Available(firstKey, true);
            // Dedicated input thread still accepts keys when the UI queue is deliberately unpumped.
            Input(() => { matcher.Process(0xA2, true); matcher.Process((int)firstKey, true); matcher.Process((int)firstKey, false); });
            Assert.Equal(0, actions);
            Assert.Single(ui);
            while (ui.TryDequeue(out var action)) action();
            Assert.Equal(1, actions);
            service.Replace([], HotkeyMode.Global);
            Assert.Equal(IntPtr.Zero, (IntPtr)type.GetField("_hook", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service));
            service.Replace(first, HotkeyMode.OperatingSystem);
            var interrupted = service.CaptureAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            service.Dispose();
            Assert.ThrowsAny<OperationCanceledException>(() => interrupted.GetAwaiter().GetResult());
        }
        finally { service.Dispose(); }
        Available(firstKey, true); Available(secondKey, true);
    }

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
