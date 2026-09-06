using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EveOPreview.Configuration;
using EveOPreview.Robin;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.View;
using Serilog;

ErrorMode.SetErrorMode(0x8003);
if (args is ["--owner"]) { Console.ReadLine(); return; }
if (args.Length == 3 && args[1] == "--focus-mock") { await MockFocus(args[0], args[2]); return; }
if (args.Length is 3 or 4 && args[1] == "--client") { await LiveClient(args); return; }
if (args.Length != 2) throw new ArgumentException("Usage: Robin.NativeSmoke <published Robin DLL> <probe.exe or Eve-O-Mock ExeFile.exe>");
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
string expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0])));
File.Copy(args[0], Path.Combine(AppContext.BaseDirectory, "Eve-O-Preview.Robin.dll"), true);
bool mock = Path.GetFileName(args[1]).Equals("ExeFile.exe", StringComparison.OrdinalIgnoreCase);
using var target = Process.Start(new ProcessStartInfo(Path.GetFullPath(args[1]))
{
    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
    RedirectStandardInput = !mock, RedirectStandardOutput = !mock, RedirectStandardError = !mock,
    WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]))
});
try
{
    IntPtr handle;
    if (mock)
    {
        do { await Task.Delay(100, deadline.Token); target.Refresh(); handle = target.MainWindowHandle; }
        while (handle == IntPtr.Zero && !target.HasExited);
    }
    else handle = new IntPtr(long.Parse(await target.StandardOutput.ReadLineAsync(deadline.Token) ?? throw new Exception("Probe failed to create a window")));
    Check(handle != IntPtr.Zero, "Target has no window");
    var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
    config.FpsLimiterSettings.IsEnabled = true;
    config.FpsLimiterSettings.FpsFocused = config.FpsLimiterSettings.FpsBackground = 30;
    config.AudioMuteSettings.CustomMutedEventIds = new() { 42, 99 };
    using var logger = new LoggerConfiguration().WriteTo.File(Path.Combine(AppContext.BaseDirectory, "native-smoke.log")).CreateLogger();
    var hooks = new HookService(config, logger);
    var process = new ProbeProcess(target.Id, handle);
    await hooks.TryInstallHooksAsync(process).WaitAsync(deadline.Token);
    string identity = await hooks.GetVersionAsync(handle);
    Check(identity?.EndsWith("SHA256=" + expectedHash) == true, "Loaded Robin hash mismatch: " + identity);
    Console.WriteLine("Loaded " + identity);
    Check((await Exchange(handle, new byte[] { 0xA1, 0xB6 }, 1))[0] == 1, "DXGI hooks were not installed");
    Check((await Exchange(handle, new byte[] { 0xA1, 0xB5 }, 1))[0] == 2, "Atomic audio capability");
    Check((await ReadIds(handle)).SequenceEqual(new uint[] { 42, 99 }), "Initial audio configuration was not applied");
    if (!mock) Check((await Exchange(handle, new byte[] { 0xA1, 0xB8 }, 1))[0] == 1, "Audio monitor was not installed");
    await Exchange(handle, new byte[] { 0xA2, 0xC7, 1 }, 1);
    // Incomplete/oversized updates must not change state or kill the listener.
    await Exchange(handle, new byte[] { 0xA2, 0xC6, 2, 0, 0, 0, 1 }, 0);
    await Exchange(handle, new byte[] { 0xA2, 0xC6, 1, 4, 0, 0 }, 0);
    for (int i = 0; i < 110; i++) await Exchange(handle, new byte[] { 0xA2 }, 0);
    Check((await ReadIds(handle)).SequenceEqual(new uint[] { 42, 99 }), "Malformed audio update changed live state");
    using (var stalled = new NamedPipeClientStream(".", $"EveoRobin_{handle}", PipeDirection.InOut, PipeOptions.Asynchronous))
    {
        await stalled.ConnectAsync(1000, deadline.Token);
        await stalled.WriteAsync(new byte[] { 0xA2 }, deadline.Token);
        await Task.Delay(1250, deadline.Token);
    }
    Check(hooks.Ping(handle), "Listener did not recover from stalled request");
    if (!mock)
    {
        await target.StandardInput.WriteLineAsync("test");
        string output = await target.StandardOutput.ReadLineAsync(deadline.Token);
        if (output == null) await target.WaitForExitAsync(deadline.Token);
        Check(output?.StartsWith("PASS ") == true, "Native probe failed: " + output + (target.HasExited ? $"exit=0x{target.ExitCode:X8} " + await target.StandardError.ReadToEndAsync() : ""));
        Console.WriteLine(output);
        var history = await ReadHistory(handle);
        Check(history.Any(e => e.Id == 42 && e.Object == 0x123456789ABCDEF0UL), "Audio history lost the 64-bit game object ID");
        await target.StandardInput.WriteLineAsync("focus");
        var wakeTimes = new List<double>();
        for (int i = 0; i < 8; i++)
        {
            Check(await target.StandardOutput.ReadLineAsync(deadline.Token) == "FOCUS_READY", "Native focus wait ready");
            await Task.Delay(30, deadline.Token); // The render thread is now inside its 1 FPS wait.
            long sent = Stopwatch.GetTimestamp();
            Task wake = hooks.TellEveClientFocusIsComingAsync(handle);
            string[] fields = (await target.StandardOutput.ReadLineAsync(deadline.Token)).Split(' ');
            Check(fields[0] == "FOCUS_DONE", "Native focus wait completion");
            double milliseconds = (long.Parse(fields[1]) - sent) * 1000.0 / Stopwatch.Frequency;
            Check(milliseconds >= 0 && milliseconds < 75, $"1 FPS wake took {milliseconds:F2} ms");
            wakeTimes.Add(milliseconds);
            await wake;
            await target.StandardInput.WriteLineAsync("next");
        }
        Console.WriteLine($"PASS pipe wake to native Present/Present1 return at 1 FPS: {string.Join(", ", wakeTimes.Select(ms => ms.ToString("F2")))} ms");
    }
    // Audio cleanup must work even when FPS is disabled and no Present calls arrive.
    config.FpsLimiterSettings.IsEnabled = false;
    Check(await hooks.UpdateTargetFpsAsync(handle), "Disable FPS");
    using (var owner = Process.Start(new ProcessStartInfo(Environment.ProcessPath, "--owner")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true }))
    {
        await Exchange(handle, Payload(w => { w.Write((byte)0xA2); w.Write((byte)0xB4); w.Write(owner.Id); }), 1);
        owner.StandardInput.Close();
        await owner.WaitForExitAsync(deadline.Token);
        await Task.Delay(1300, deadline.Token);
    }
    Check((await ReadIds(handle)).Length == 0, "Owner exit did not clear audio while FPS was disabled");
    Check(await hooks.UpdateMutedAudioAsync(handle), "Restore audio for shutdown test");
    await hooks.StopAsync(new[] { process }).WaitAsync(deadline.Token);
    Check((await ReadIds(handle)).Length == 0, "Host shutdown did not clear audio");
    var fps = await Exchange(handle, new byte[] { 0xA1, 0xA1 }, 16);
    Check(BitConverter.ToInt32(fps, 1) == 0 && BitConverter.ToInt32(fps, 6) == 0 && BitConverter.ToInt32(fps, 11) == 0, "Shutdown FPS targets");
    // Replacing the installation copy succeeds while the injected, hash-addressed copy stays loaded.
    File.Copy(args[0], Path.Combine(AppContext.BaseDirectory, "Eve-O-Preview.Robin.dll"), true);
    Console.WriteLine("PASS injection, identity, malformed/stalled pipe recovery, owner exit, shutdown, unlocked installation DLL");
    if (!mock)
    {
        await target.StandardInput.WriteLineAsync("exit");
        await target.WaitForExitAsync(deadline.Token);
        Check(target.ExitCode == 0, "Native probe exit");
    }
}
catch
{
    if (!mock && target.HasExited) Console.Error.WriteLine($"Probe exit=0x{target.ExitCode:X8}: " + await target.StandardError.ReadToEndAsync());
    throw;
}
finally { if (!target.HasExited) { target.Kill(); await target.WaitForExitAsync(); } }

// These allocation checks use the production source in managed form; native timing above uses the published AOT DLL.
var throttle = typeof(DxHook).GetMethod("ThrottleTheFrame", BindingFlags.NonPublic | BindingFlags.Static).CreateDelegate<Func<bool>>();
DxHook.SetFpsTargets(1000, 1000, 1000);
for (int i = 0; i < 100; i++) throttle();
long allocated = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 256; i++) throttle();
long frameBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
AudioMuteSystem.UpdateMutedIds(new uint[] { 42, 99 }, true, false);
for (int i = 0; i < 100; i++) AudioMuteSystem.IsMuted(42);
allocated = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 100000; i++) AudioMuteSystem.IsMuted((uint)i);
long lookupBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
Check(frameBytes == 0 && lookupBytes == 0, $"Steady hot-path allocations: frame={frameBytes}, audio lookup={lookupBytes}");
Console.WriteLine("PASS warmed managed-source allocations: 256 frame waits=0 bytes; 100000 audio lookups=0 bytes");

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static byte[] Payload(Action<BinaryWriter> write) { using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory); write(writer); return memory.ToArray(); }
static async Task MockFocus(string dll, string mockPath)
{
    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)));
    File.Copy(dll, Path.Combine(AppContext.BaseDirectory, "Eve-O-Preview.Robin.dll"), true);
    var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
    config.FpsLimiterSettings.IsEnabled = true;
    config.FpsLimiterSettings.FpsFocused = 60;
    config.FpsLimiterSettings.FpsBackground = config.FpsLimiterSettings.FpsPredictingFocus = 1;
    using var logger = new LoggerConfiguration().CreateLogger();
    var hooks = new HookService(config, logger);
    var windows = new WindowManager(hooks, logger);
    var children = new List<Process>();
    var processes = new List<ProbeProcess>();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    try
    {
        for (int i = 0; i < 4; i++)
        {
            var child = Process.Start(new ProcessStartInfo(Path.GetFullPath(mockPath))
            { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(mockPath)) });
            children.Add(child);
            do { await Task.Delay(50, deadline.Token); child.Refresh(); } while (child.MainWindowHandle == IntPtr.Zero && !child.HasExited);
            Check(!child.HasExited, "Mock exited before creating a window");
            var process = new ProbeProcess(child.Id, child.MainWindowHandle);
            processes.Add(process);
            await hooks.TryInstallHooksAsync(process);
            Check((await hooks.GetVersionAsync(process.MainWindowHandle))?.EndsWith("SHA256=" + hash) == true, "Unexpected Mock Robin build");
        }
        for (int i = 0; i < 16; i++)
        {
            var process = processes[i % processes.Count];
            var time = Stopwatch.StartNew();
            windows.ActivateWindow(process.MainWindowHandle);
            while (windows.GetForegroundWindowHandle() != process.MainWindowHandle && time.ElapsedMilliseconds < 100)
                Thread.Sleep(1);
            Check(windows.GetForegroundWindowHandle() == process.MainWindowHandle, $"Mock cycle {i}: foreground rejected");
            Check(ErrorMode.SendMessageTimeout(process.MainWindowHandle, 0, IntPtr.Zero, IntPtr.Zero, 3, 100, out _) != IntPtr.Zero,
                $"Mock cycle {i}: foreground client did not process input messages");
            Console.WriteLine($"PASS Mock cycle {i + 1}: foreground and message processing in {time.Elapsed.TotalMilliseconds:F2} ms");
            await Task.Delay(300, deadline.Token);
        }
    }
    finally
    {
        await hooks.StopAsync(processes);
        foreach (var child in children)
        {
            if (!child.HasExited) { child.CloseMainWindow(); if (!child.WaitForExit(2000)) child.Kill(); }
            child.Dispose();
        }
    }
}
static async Task LiveClient(string[] arguments)
{
    string dll = Path.GetFullPath(arguments[0]);
    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)));
    File.Copy(dll, Path.Combine(AppContext.BaseDirectory, "Eve-O-Preview.Robin.dll"), true);
    using var client = Process.GetProcessById(int.Parse(arguments[2]));
    Check(client.ProcessName.Equals("exefile", StringComparison.OrdinalIgnoreCase) && client.MainWindowHandle != IntPtr.Zero, "Expected a running EVE client with a main window");
    var process = new ProbeProcess(client.Id, client.MainWindowHandle);
    var config = (IThumbnailConfiguration)Activator.CreateInstance(typeof(MainForm).Assembly.GetType("EveOPreview.Configuration.Implementation.ThumbnailConfiguration"));
    // A sentinel enables injection; it is immediately cleared before diagnostics.
    config.AudioMuteSettings.CustomMutedEventIds = new() { uint.MaxValue };
    using var logger = new LoggerConfiguration().WriteTo.File(Path.Combine(AppContext.BaseDirectory, "native-smoke.log")).CreateLogger();
    var hooks = new HookService(config, logger);
    Check(!hooks.Ping(process.MainWindowHandle), "This mode requires a fresh client, so it cannot replace another host's active settings");
    try
    {
        await hooks.TryInstallHooksAsync(process);
        var identity = await hooks.GetVersionAsync(process.MainWindowHandle);
        Check(identity?.EndsWith("SHA256=" + hash) == true, "Client has a different Robin build; restart it before this test: " + identity);
        Console.WriteLine($"Client {client.Id} {client.MainWindowTitle}: {identity}");
        Console.WriteLine("DXGI=" + (await Exchange(process.MainWindowHandle, new byte[] { 0xA1, 0xB6 }, 1))[0] + " Audio=" + (await Exchange(process.MainWindowHandle, new byte[] { 0xA1, 0xB8 }, 1))[0]);
        await Exchange(process.MainWindowHandle, new byte[] { 0xA2, 0xC1 }, 1);
        await Exchange(process.MainWindowHandle, new byte[] { 0xA2, 0xC7, 1 }, 1);
        await Task.Delay(15000);
        var history = await ReadHistory(process.MainWindowHandle);
        Console.WriteLine("Recent events: " + string.Join(", ", history.GroupBy(e => e.Id).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} x{g.Count()}")));
        string resultPath = Path.Combine(AppContext.BaseDirectory, $"client-{client.Id}-audio.json");
        File.WriteAllText(resultPath, System.Text.Json.JsonSerializer.Serialize(history, new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        Console.WriteLine("History: " + resultPath);
        if (arguments.Length == 4)
        {
            uint id = uint.Parse(arguments[3]);
            await Exchange(process.MainWindowHandle, Payload(w => { w.Write((byte)0xA2); w.Write((byte)0xC6); w.Write(1); w.Write(id); }), 1);
            Console.WriteLine($"Muting event {id} for 20 seconds");
            await Task.Delay(20000);
        }
    }
    finally { await hooks.StopAsync(new[] { process }); }
    Check((await ReadIds(process.MainWindowHandle)).Length == 0, "Live client audio was not cleared");
    Console.WriteLine("PASS client responsive; FPS, muting and diagnostic capture disabled");
}
static async Task<List<(uint Id, ulong Object, long Timestamp)>> ReadHistory(IntPtr handle)
{
    using var timeout = new CancellationTokenSource(3000);
    using var pipe = new NamedPipeClientStream(".", $"EveoRobin_{handle}", PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipe.ConnectAsync(2000, timeout.Token);
    await pipe.WriteAsync(new byte[] { 0xA1, 0xC5 }, timeout.Token);
    byte[] header = new byte[4]; await pipe.ReadExactlyAsync(header, timeout.Token);
    int count = BitConverter.ToInt32(header); Check(count is >= 0 and <= 128, "History reply length");
    byte[] bytes = new byte[count * 20]; await pipe.ReadExactlyAsync(bytes, timeout.Token);
    return Enumerable.Range(0, count).Select(i => (BitConverter.ToUInt32(bytes, i * 20), BitConverter.ToUInt64(bytes, i * 20 + 4), BitConverter.ToInt64(bytes, i * 20 + 12))).ToList();
}
static async Task<byte[]> Exchange(IntPtr handle, byte[] request, int length)
{
    using var timeout = new CancellationTokenSource(3000);
    using var pipe = new NamedPipeClientStream(".", $"EveoRobin_{handle}", PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipe.ConnectAsync(2000, timeout.Token);
    await pipe.WriteAsync(request, timeout.Token);
    byte[] response = new byte[length];
    await pipe.ReadExactlyAsync(response, timeout.Token);
    return response;
}
static async Task<uint[]> ReadIds(IntPtr handle)
{
    using var timeout = new CancellationTokenSource(3000);
    using var pipe = new NamedPipeClientStream(".", $"EveoRobin_{handle}", PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipe.ConnectAsync(2000, timeout.Token);
    await pipe.WriteAsync(new byte[] { 0xA1, 0xC4 }, timeout.Token);
    byte[] length = new byte[4]; await pipe.ReadExactlyAsync(length, timeout.Token);
    int count = BitConverter.ToInt32(length); Check(count is >= 0 and <= 1024, "Audio reply length");
    byte[] bytes = new byte[count * 4]; await pipe.ReadExactlyAsync(bytes, timeout.Token);
    return Enumerable.Range(0, count).Select(i => BitConverter.ToUInt32(bytes, i * 4)).ToArray();
}
sealed record ProbeProcess(int ProcessId, IntPtr MainWindowHandle) : IProcessInfo
{
    public IntPtr ProcessHandle => IntPtr.Zero;
    public string Title => "Robin native smoke";
}
static class ErrorMode
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    internal static extern uint SetErrorMode(uint mode);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out IntPtr result);
}
