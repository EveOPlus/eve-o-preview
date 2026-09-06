using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class PipeLifecycleTests
{
    [Fact]
    public async Task ReadyPipeReceivesFocusAndPredictionBeforeTheCallerReturns()
    {
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        config.FpsLimiterSettings.IsEnabled = true;
        using var logger = new LoggerConfiguration().CreateLogger();
        var hooks = new HookService(config, logger);
        var handle = new IntPtr(-Random.Shared.NextInt64(1, long.MaxValue));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var (predicting, bufferSize) in new[] { (false, 8192), (true, 8192), (false, 0), (true, 0) })
        {
            using var server = new NamedPipeServerStream($"EveoRobin_{handle}", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, bufferSize, bufferSize);
            var connection = server.WaitForConnectionAsync(deadline.Token);
            var time = Stopwatch.StartNew();
            Task sending = predicting ? hooks.TellEveClientFocusIsMaybeComingSoonAsync(handle, 5000) : hooks.TellEveClientFocusIsComingAsync(handle);
            Assert.True(time.ElapsedMilliseconds < 250, "Even an old zero-buffer pipe must not block the input callback");
            if (bufferSize > 0)
            {
                Assert.True(PeekNamedPipe(server.SafePipeHandle.DangerousGetHandle(), IntPtr.Zero, 0, IntPtr.Zero, out uint available, IntPtr.Zero));
                Assert.Equal(predicting ? 6u : 2u, available); // Bytes were issued before any UI continuation.
            }
            await connection;
            byte[] bytes = new byte[predicting ? 6 : 2];
            await server.ReadExactlyAsync(bytes, deadline.Token);
            Assert.Equal(predicting ? new byte[] { 0xA3, 0xB3, 0x88, 0x13, 0, 0 } : new byte[] { 0xA3, 0xB1 }, bytes);
            await sending;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PeekNamedPipe(IntPtr pipe, IntPtr buffer, uint bufferSize, IntPtr bytesRead, out uint available, IntPtr bytesLeft);

    [Fact]
    public async Task DisabledSettingsSendZeroTargetsAndShutdownClearsAudio()
    {
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        using var logger = new LoggerConfiguration().CreateLogger();
        var hooks = new HookService(config, logger);
        var handle = new IntPtr(-Random.Shared.NextInt64(1, long.MaxValue));
        using var server = new NamedPipeServerStream($"EveoRobin_{handle}", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        async Task Receive(byte[] expected)
        {
            await server.WaitForConnectionAsync(deadline.Token);
            var bytes = new byte[expected.Length];
            await server.ReadExactlyAsync(bytes, deadline.Token);
            Assert.Equal(expected, bytes);
            await server.WriteAsync(new byte[] { 1 }, deadline.Token);
            server.WaitForPipeDrain();
            server.Disconnect();
        }
        byte[] zeroTargets = { 0xA2, 0xF1, 0, 0, 0, 0, 0xF2, 0, 0, 0, 0, 0xF3, 0, 0, 0, 0 };
        var receive = Receive(zeroTargets);
        Assert.True(await hooks.UpdateTargetFpsAsync(handle));
        await receive;
        var process = Stub.Create<IProcessInfo>((method, args) => method.Name == "get_MainWindowHandle" ? handle : Stub.Default(method.ReturnType));
        var shutdown = hooks.StopAsync(new[] { process });
        await Receive(zeroTargets);
        await Receive(new byte[] { 0xA2, 0xC1 });
        await shutdown.WaitAsync(deadline.Token);
        Assert.False(await hooks.UpdateTargetFpsAsync(handle));
    }

    [Fact]
    public async Task ConnectedPeerWithoutReplyCannotHoldSettingsOrVersionQueriesOpen()
    {
        var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
        using var logger = new LoggerConfiguration().CreateLogger();
        var hooks = new HookService(config, logger);
        var handle = new IntPtr(-Random.Shared.NextInt64(1, long.MaxValue));
        using var server = new NamedPipeServerStream($"EveoRobin_{handle}", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var setting = hooks.UpdateTargetFpsAsync(handle);
        await server.WaitForConnectionAsync(deadline.Token);
        await server.ReadExactlyAsync(new byte[16], deadline.Token);
        var stopwatch = Stopwatch.StartNew();
        Assert.False(await setting.WaitAsync(deadline.Token));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        server.Disconnect();
        var version = hooks.GetVersionAsync(handle);
        await server.WaitForConnectionAsync(deadline.Token);
        await server.ReadExactlyAsync(new byte[2], deadline.Token);
        await server.WriteAsync(new byte[] { 100, 0, 0, 0, 65 }, deadline.Token); // Deliberately truncated version.
        Assert.Null(await version.WaitAsync(deadline.Token));
    }
}
