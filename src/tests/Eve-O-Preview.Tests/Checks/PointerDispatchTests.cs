using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Input;
using EveOPreview.Tests.Infrastructure;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class PointerDispatchTests(ITestOutputHelper output)
{
    [Fact]
    public Task DisconnectedNativeThreadAllowsWindowSubscriptionCleanup() =>
        PrivateDesktopRunner.RunAsync("pointer-thread-exit", output);

    internal static void CheckNativeThreadExit()
    {
        TestAvalonia.Initialize();
        using var logger = new LoggerConfiguration().CreateLogger();
        using var input = new WindowsGlobalPointerInput(logger);
        int delivered = 0;
        EventHandler<GlobalPointerEventArgs> handler = (_, _) => delivered++;
        input.MouseMove += handler;
        input.MouseUp += handler;
        object Field(string name) => typeof(WindowsGlobalPointerInput)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
        var window = (WindowsMessageWindow)Field("_window");
        uint nativeThread = GetWindowThreadProcessId(window.Handle, out uint process);
        Assert.Equal((uint)Environment.ProcessId, process);
        ((PointerDispatchQueue)Field("_dispatch")).Enqueue(Point.Empty, PointerButtons.None,
            PointerButtons.Right, ShortcutKeys.None, (int)Field("_generation"));
        // Stop only this service's native loop, leaving its owning workspace alive.
        Assert.True(PostThreadMessage(nativeThread, 0x0012, IntPtr.Zero, IntPtr.Zero));
        Assert.True(((Thread)Field("_thread")).Join(TimeSpan.FromSeconds(2)));
        var elapsed = Stopwatch.StartNew();
        input.MouseMove -= handler;
        input.MouseUp -= handler;
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Throws<InvalidOperationException>(() => input.MouseMove += handler);
        Assert.Null(Field("_move")); // A failed add must not retain the owner.
        TestAvalonia.Pump();
        Assert.Equal(0, delivered);
        input.Dispose(); // Disposal also tolerates the unavailable native thread.
        Assert.Throws<ObjectDisposedException>(() => input.MouseUp += handler);
        Assert.Null(Field("_up"));
    }

    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);

    [Fact]
    public void BusyUiCoalescesMotionWithoutPerSampleAllocationAndDeliversReleaseAtFinalPosition()
    {
        var jobs = new Queue<Action>();
        var deliveries = new List<(string Kind, Point Position)>();
        var queue = new PointerDispatchQueue(jobs.Enqueue, () => 1,
            args => deliveries.Add(("move", args.Location)), args => deliveries.Add(("up", args.Location)));
        queue.Enqueue(Point.Empty, PointerButtons.None, PointerButtons.Right, ShortcutKeys.None, 1);
        for (int i = 0; i < 10000; i++) queue.Enqueue(new Point(i, i), PointerButtons.None, PointerButtons.Right, ShortcutKeys.None, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) queue.Enqueue(new Point(i, -i), PointerButtons.None, PointerButtons.Right, ShortcutKeys.Shift, 1);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Single(jobs); Assert.Empty(deliveries);
        queue.Enqueue(new Point(100001, -100001), PointerButtons.Right, PointerButtons.None, ShortcutKeys.Shift, 1);
        jobs.Dequeue()();
        Assert.Equal(new[] { ("move", new Point(100001, -100001)), ("up", new Point(100001, -100001)) }, deliveries);
        Assert.Empty(jobs);
    }

    [Fact]
    public void ReleaseBacklogIsBoundedAndOldGestureDoesNotReachNewSubscribers()
    {
        var jobs = new Queue<Action>(); int generation = 1, delivered = 0;
        var queue = new PointerDispatchQueue(jobs.Enqueue, () => generation, _ => delivered++, _ => delivered++);
        for (int i = 0; i < 1000; i++) queue.Enqueue(new Point(i, 0), PointerButtons.Right, PointerButtons.None, ShortcutKeys.None, generation);
        Assert.Single(jobs); jobs.Dequeue()(); Assert.Equal(16, delivered); Assert.Empty(jobs);
        queue.Enqueue(Point.Empty, PointerButtons.None, PointerButtons.Right, ShortcutKeys.None, generation);
        generation++;
        jobs.Dequeue()(); Assert.Equal(16, delivered);
    }

    [Fact]
    public void ContinuousMovesYieldAfterFiniteBatch()
    {
        var jobs = new Queue<Action>(); int delivered = 0;
        PointerDispatchQueue queue = null;
        queue = new PointerDispatchQueue(jobs.Enqueue, () => 1, _ =>
        {
            delivered++;
            queue.Enqueue(new Point(delivered, 0), PointerButtons.None, PointerButtons.Right, ShortcutKeys.None, 1);
        }, _ => { });
        queue.Enqueue(Point.Empty, PointerButtons.None, PointerButtons.Right, ShortcutKeys.None, 1);
        jobs.Dequeue()(); Assert.InRange(delivered, 1, 9); Assert.Single(jobs);
    }
}
