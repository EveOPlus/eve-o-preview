//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using EveOPreview.Configuration;
using EveOPreview.Services.Interface;
using EveOPreview.Services.Interop;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EveOPreview.Services.Implementation;

public class HookService : IHookService
{
    private const int PipeTimeoutMs = 1000;
    private const int MaxMutedIds = 1024;
    private readonly IThumbnailConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<IntPtr, SemaphoreSlim> _pipeGates = new();
    private readonly ConcurrentDictionary<(int Pid, IntPtr Window), Lazy<Task>> _installations = new();
    private readonly ConcurrentDictionary<IntPtr, bool> _supportsReplaceAudio = new();
    private volatile bool _stopping;

    public HookService(IThumbnailConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool Ping(IntPtr handle) => PingAsync(handle).GetAwaiter().GetResult();
    private async Task<bool> PingAsync(IntPtr handle) =>
        await SendAsync(handle, w => { w.Write((byte)0xA1); w.Write((byte)0xB2); }).ConfigureAwait(false) == 1;

    public async Task<string> GetVersionAsync(IntPtr handle)
    {
        var gate = _pipeGates.GetOrAdd(handle, _ => new SemaphoreSlim(1, 1));
        bool entered = false;
        using var timeout = new CancellationTokenSource(PipeTimeoutMs);
        try
        {
            await gate.WaitAsync(timeout.Token).ConfigureAwait(false);
            entered = true;
            using var client = new NamedPipeClientStream(".", $"EveoRobin_{handle}", PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(100, timeout.Token).ConfigureAwait(false);
            await client.WriteAsync(new byte[] { 0xA1, 0xB7 }, timeout.Token).ConfigureAwait(false);
            var lengthBytes = new byte[4];
            await client.ReadExactlyAsync(lengthBytes, timeout.Token).ConfigureAwait(false);
            int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if (length < 1 || length > 512) return null;
            var bytes = new byte[length];
            await client.ReadExactlyAsync(bytes, timeout.Token).ConfigureAwait(false);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is OperationCanceledException || ex is UnauthorizedAccessException) { return null; }
        finally { if (entered) gate.Release(); }
    }

    public async Task TellEveClientFocusIsComingAsync(IntPtr handle)
    {
        if (_stopping || !_configuration.FpsLimiterSettings.IsEnabled) return;
        if (await TrySendFocusNowAsync(handle, new byte[] { 0xA3, 0xB1 }).ConfigureAwait(false)) return;
        await SendAsync(handle, w => { w.Write((byte)0xA3); w.Write((byte)0xB1); }, reply: false, timeoutMs: 150, takeGate: false).ConfigureAwait(false);
    }

    public async Task TellEveClientFocusIsMaybeComingSoonAsync(IntPtr handle, int timeoutMs = 5000)
    {
        if (_stopping || !_configuration.FpsLimiterSettings.IsEnabled) return;
        byte[] payload = new byte[6] { 0xA3, 0xB3, 0, 0, 0, 0 };
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2), Math.Clamp(timeoutMs, 1, 30000));
        if (await TrySendFocusNowAsync(handle, payload).ConfigureAwait(false)) return;
        await SendAsync(handle, w => { w.Write((byte)0xA3); w.Write((byte)0xB3); w.Write(Math.Clamp(timeoutMs, 1, 30000)); },
            reply: false, timeoutMs: 150, takeGate: false).ConfigureAwait(false);
    }

    private static async Task<bool> TrySendFocusNowAsync(IntPtr handle, byte[] payload)
    {
        if (handle == IntPtr.Zero) return false;
        try
        {
            // An available pipe accepts this tiny, buffered message inline. ConnectAsync
            // dispatches the connect through the thread pool; don't put it ahead of focus.
            using var client = new NamedPipeClientStream(".", $"EveoRobin_{handle}", PipeDirection.Out, PipeOptions.Asynchronous);
            client.Connect(0);
            using var timeout = new CancellationTokenSource(150);
            // Issue the write now, but don't block the input thread on an old, zero-buffer
            // or unresponsive server. Overlapped completion only owns cleanup afterward.
            await client.WriteAsync(payload, timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is UnauthorizedAccessException || ex is OperationCanceledException)
        { return false; }
    }

    public Task<bool> UpdateTargetFpsAsync(IntPtr handle)
    {
        if (_stopping) return Task.FromResult(false);
        var settings = _configuration.FpsLimiterSettings;
        return SetExactTargetFpsAsync(handle, settings.IsEnabled ? settings.FpsFocused : 0,
            settings.IsEnabled ? settings.FpsBackground : 0, settings.IsEnabled ? settings.FpsPredictingFocus : 0);
    }
        
    public Task<bool> DisableFpsLimiterAsync(IntPtr handle) => SetExactTargetFpsAsync(handle, 0, 0, 0);

    private async Task<bool> SetExactTargetFpsAsync(IntPtr handle, int foreground, int background, int predicted) =>
        await SendAsync(handle, w =>
        {
            w.Write((byte)0xA2); w.Write((byte)0xF1); w.Write(foreground);
            w.Write((byte)0xF2); w.Write(background); w.Write((byte)0xF3); w.Write(predicted);
        }).ConfigureAwait(false) == 1;

    public async Task TryInstallHooksAsync(IProcessInfo process)
    {
        if (_stopping || process == null || process.MainWindowHandle == IntPtr.Zero) return;
        var key = (process.ProcessId, process.MainWindowHandle);
        var installation = _installations.GetOrAdd(key, _ => new Lazy<Task>(() => InstallAndConfigureAsync(process)));
        try { await installation.Value.ConfigureAwait(false); }
        finally { _installations.TryRemove(new KeyValuePair<(int, IntPtr), Lazy<Task>>(key, installation)); }
    }

    private async Task InstallAndConfigureAsync(IProcessInfo process)
    {
        try
        {
            bool present = await PingAsync(process.MainWindowHandle).ConfigureAwait(false);
            if (_stopping) return;
            if (!present)
            {
                // When no native feature is requested, don't inject merely to send zero targets.
                var audio = _configuration.AudioMuteSettings;
                if (!_configuration.FpsLimiterSettings.IsEnabled && !audio.MuteJumpGateTunnel &&
                    !audio.MuteLocationBanner && audio.CustomMutedEventIds.Count == 0) return;
                await Task.Run(() => Inject(process)).ConfigureAwait(false);
                bool ready = false;
                for (int i = 0; i < 20 && !_stopping; i++)
                {
                    if (await PingAsync(process.MainWindowHandle).ConfigureAwait(false)) { ready = true; break; }
                    await Task.Delay(50).ConfigureAwait(false);
                }
                if (!ready) throw new IOException("Robin did not become ready after initialization.");
            }
            if (_stopping) return;
            if (await SendAsync(process.MainWindowHandle, w =>
                { w.Write((byte)0xA2); w.Write((byte)0xB4); w.Write(Environment.ProcessId); }).ConfigureAwait(false) != 1) return;
            string identity = await GetVersionAsync(process.MainWindowHandle).ConfigureAwait(false);
            _logger.Information("Robin in PID {Pid}: {BuildIdentity}", process.ProcessId, identity ?? "Legacy build (no version query); restart this client to load the current DLL");
            await UpdateTargetFpsAsync(process.MainWindowHandle).ConfigureAwait(false);
            await UpdateMutedAudioAsync(process.MainWindowHandle).ConfigureAwait(false);
        }
        catch (Exception ex) { _logger.Error(ex, "Robin initialization failed for PID {Pid}", process.ProcessId); }
    }
        
    private void Inject(IProcessInfo info)
    {
        using var process = Process.GetProcessById(info.ProcessId);
        if (process.HasExited || process.MainWindowHandle != info.MainWindowHandle)
            throw new InvalidOperationException("The target client changed before injection.");
        string source = Path.Combine(AppContext.BaseDirectory, "Eve-O-Preview.Robin.dll");
        if (!File.Exists(source)) throw new FileNotFoundException("Publish the native Robin DLL beside the host executable.", source);
        // Loaded modules outlive the host. Load a versioned copy so installation files remain replaceable.
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
        string directory = Path.Combine(Path.GetTempPath(), "Eve-O Preview", "Robin", hash);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Eve-O-Preview.Robin.dll");
        try { File.Copy(source, path, overwrite: false); }
        catch (IOException) when (File.Exists(path))
        {
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != hash) throw;
        }
        // Use only the rights needed to load the DLL and start its exported initializer.
        IntPtr target = KernelNativeMethods.OpenProcess(0x043A, false, info.ProcessId);
        if (target == IntPtr.Zero) throw new Win32Exception();
        IntPtr remotePath = IntPtr.Zero, loaderThread = IntPtr.Zero, initThread = IntPtr.Zero, localModule = IntPtr.Zero;
        bool loaderFinished = false;
        try
        {
            process.Refresh();
            var loaded = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "Eve-O-Preview.Robin.dll", StringComparison.OrdinalIgnoreCase));
            if (loaded == null)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(path + "\0");
                remotePath = KernelNativeMethods.VirtualAllocEx(target, IntPtr.Zero, (uint)bytes.Length, 0x3000, 0x04);
                if (remotePath == IntPtr.Zero || !KernelNativeMethods.WriteProcessMemory(target, remotePath, bytes, (uint)bytes.Length, out IntPtr written) || written.ToInt64() != bytes.Length)
                    throw new Win32Exception();
                IntPtr loadLibrary = KernelNativeMethods.GetProcAddress(KernelNativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                loaderThread = KernelNativeMethods.CreateRemoteThread(target, IntPtr.Zero, 0, loadLibrary, remotePath, 0, IntPtr.Zero);
                if (loaderThread == IntPtr.Zero) throw new Win32Exception();
                if (KernelNativeMethods.WaitForSingleObject(loaderThread, 5000) != 0) throw new TimeoutException("Target DLL loader did not finish.");
                loaderFinished = true;
                process.Refresh();
                loaded = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.FileName, path, StringComparison.OrdinalIgnoreCase));
                if (loaded == null) throw new IOException("The native DLL did not load in the target.");
            }
            // Map just the PE image for export offsets: don't initialize another NativeAOT runtime in the host.
            localModule = KernelNativeMethods.LoadLibraryEx(loaded.FileName, IntPtr.Zero, 1);
            if (localModule == IntPtr.Zero) throw new Win32Exception();
            IntPtr initialize = KernelNativeMethods.GetProcAddress(localModule, "Initialize");
            if (initialize == IntPtr.Zero) throw new IOException("The DLL has no native Initialize export.");
            IntPtr remoteInitialize = loaded.BaseAddress + (int)(initialize.ToInt64() - localModule.ToInt64());
            initThread = KernelNativeMethods.CreateRemoteThread(target, IntPtr.Zero, 0, remoteInitialize, IntPtr.Zero, 0, IntPtr.Zero);
            if (initThread == IntPtr.Zero) throw new Win32Exception();
            if (KernelNativeMethods.WaitForSingleObject(initThread, 5000) != 0) throw new TimeoutException("Robin initializer did not finish.");
        }
        finally
        {
            if (localModule != IntPtr.Zero) KernelNativeMethods.FreeLibrary(localModule);
            if (initThread != IntPtr.Zero) KernelNativeMethods.CloseHandle(initThread);
            if (remotePath != IntPtr.Zero)
            {
                if (loaderThread == IntPtr.Zero || loaderFinished) KernelNativeMethods.VirtualFreeEx(target, remotePath, 0, 0x8000);
                else
                {
                    // The loader may still read its argument. Keep ownership until it exits; never free live remote memory.
                    IntPtr retainedTarget = target, retainedThread = loaderThread, retainedPath = remotePath;
                    _ = Task.Run(() =>
                    {
                        KernelNativeMethods.WaitForSingleObject(retainedThread, uint.MaxValue);
                        KernelNativeMethods.VirtualFreeEx(retainedTarget, retainedPath, 0, 0x8000);
                        KernelNativeMethods.CloseHandle(retainedThread);
                        KernelNativeMethods.CloseHandle(retainedTarget);
                    });
                    target = loaderThread = IntPtr.Zero;
                }
            }
            if (loaderThread != IntPtr.Zero) KernelNativeMethods.CloseHandle(loaderThread);
            if (target != IntPtr.Zero) KernelNativeMethods.CloseHandle(target);
        }
    }

    public async Task<bool> UpdateMutedAudioAsync(IntPtr handle)
    {
        if (_stopping) return false;
        var settings = _configuration.AudioMuteSettings;
        var ids = new List<uint>(settings.CustomMutedEventIds);
        if (settings.MuteJumpGateTunnel) ids.AddRange(new uint[] { 3689163958, 1537508544, 1768044352 });
        if (settings.MuteLocationBanner) ids.AddRange(new uint[] { 2377891014, 3090840445 });
        ids = ids.Distinct().ToList();
        if (ids.Count > MaxMutedIds) { _logger.Warning("Audio mute list exceeds {Limit} IDs", MaxMutedIds); return false; }
        if (!_supportsReplaceAudio.TryGetValue(handle, out bool replace))
        {
            replace = await SendAsync(handle, w => { w.Write((byte)0xA1); w.Write((byte)0xB5); }).ConfigureAwait(false) == 2;
            _supportsReplaceAudio[handle] = replace;
        }
        if (_stopping) return false;
        if (replace) return await SendAudioIdsAsync(handle, 0xC6, ids).ConfigureAwait(false);
        // Older already-injected Robin only supports clear then add. Hold the gate across both connections.
        var gate = _pipeGates.GetOrAdd(handle, _ => new SemaphoreSlim(1, 1));
        using var timeout = new CancellationTokenSource(PipeTimeoutMs * 2);
        try
        {
            await gate.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                if (await SendAsync(handle, w => { w.Write((byte)0xA2); w.Write((byte)0xC1); }, takeGate: false).ConfigureAwait(false) != 1) return false;
                if (_stopping) return false;
                return await SendAudioIdsAsync(handle, 0xC3, ids, takeGate: false).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) { return false; }
    }

    private async Task<bool> SendAudioIdsAsync(IntPtr handle, byte command, List<uint> ids, bool takeGate = true) =>
        await SendAsync(handle, w => { w.Write((byte)0xA2); w.Write(command); w.Write(ids.Count); foreach (uint id in ids) w.Write(id); }, takeGate: takeGate).ConfigureAwait(false) == 1;

    public void ForgetClient(IProcessInfo process)
    {
        _supportsReplaceAudio.TryRemove(process.MainWindowHandle, out _);
        _pipeGates.TryRemove(process.MainWindowHandle, out _);
    }

    public async Task StopAsync(IEnumerable<IProcessInfo> processes)
    {
        _stopping = true;
        await Task.WhenAll(_installations.Values.Where(t => t.IsValueCreated).Select(t => t.Value)).ConfigureAwait(false);
        await Task.WhenAll(processes.Select(async p =>
        {
            await DisableFpsLimiterAsync(p.MainWindowHandle).ConfigureAwait(false);
            await SendAsync(p.MainWindowHandle, w => { w.Write((byte)0xA2); w.Write((byte)0xC1); }).ConfigureAwait(false);
        })).ConfigureAwait(false);
    }

    private async Task<int> SendAsync(IntPtr handle, Action<BinaryWriter> write, bool reply = true, int timeoutMs = PipeTimeoutMs, bool takeGate = true)
    {
        if (handle == IntPtr.Zero) return -1;
        var gate = _pipeGates.GetOrAdd(handle, _ => new SemaphoreSlim(1, 1));
        bool entered = false;
        using var timeout = new CancellationTokenSource(timeoutMs);
        try
        {
            if (takeGate) { await gate.WaitAsync(timeout.Token).ConfigureAwait(false); entered = true; }
            using var client = new NamedPipeClientStream(".", $"EveoRobin_{handle}", reply ? PipeDirection.InOut : PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(100, timeout.Token).ConfigureAwait(false);
            using var payload = new MemoryStream();
            using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true)) write(writer);
            await client.WriteAsync(payload.GetBuffer().AsMemory(0, (int)payload.Length), timeout.Token).ConfigureAwait(false);
            if (!reply) return 1;
            var response = new byte[1];
            await client.ReadExactlyAsync(response, timeout.Token).ConfigureAwait(false);
            return response[0];
        }
        catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is OperationCanceledException || ex is UnauthorizedAccessException)
        {
            _logger.Debug("Robin pipe unavailable for HWND {Handle}: {Reason}", handle, ex.Message);
            return -1;
        }
        finally { if (entered) gate.Release(); }
    }
}
