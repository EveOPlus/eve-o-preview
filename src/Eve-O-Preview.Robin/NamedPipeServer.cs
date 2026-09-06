// Eve-O-Preview.Robin is a companion program / sidekick (think Batman and Robin) to provide a native runtime for communicating with the client, such as permitting AllowSetForegroundWindow and limiting DirectX frames per minute.
// Copyright (C) 2026  Aura Asuna
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using static EveOPreview.Robin.DebugLogger;
using static EveOPreview.Robin.Global;

namespace EveOPreview.Robin;

public static class NamedPipeServer
{
    internal static volatile bool IsNamedPipeRunning = true;
    private static NamedPipeServerStream? _server;
    private static string _pipeName = "EveoRobin_" + ThisClientsHandle;
    private static readonly Lazy<string> BuildIdentity = new(() =>
    {
        string version = typeof(DxHook).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        using var process = Process.GetCurrentProcess();
        string? path = process.Modules.Cast<ProcessModule>().FirstOrDefault(m => string.Equals(m.ModuleName, "Eve-O-Preview.Robin.dll", StringComparison.OrdinalIgnoreCase))?.FileName;
        string hash = path == null ? "managed" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return $"{version}; SHA256={hash}";
    });

    public static void Initialize() => _server = CreateNamedPipeServer();

    internal static NamedPipeServerStream CreateNamedPipeServer()
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var user = identity.User ?? throw new InvalidOperationException("No current user for the pipe ACL.");
            var security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
            return NamedPipeServerStreamAcl.Create(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 8192, 8192, security);
        }
        return new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    internal static async Task StartPipeServer()
    {
        int failures = 0;
        while (IsNamedPipeRunning)
        {
            try
            {
                _server ??= CreateNamedPipeServer();
                await _server.WaitForConnectionAsync().ConfigureAwait(false);
                using var deadline = new CancellationTokenSource(1000);
                try
                {
                    await ProcessConnectionAsync(_server, deadline.Token).ConfigureAwait(false);
                    failures = 0;
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is OperationCanceledException || ex is ArgumentException)
                {
                    // A disconnected, malformed or stalled client is not a server failure.
                    // Keep the listener available for the next connection.
                }
                finally
                {
                    // EOF can leave the managed stream in Broken state even though IsConnected
                    // is false. Recreate it instead of trying to reuse that disconnected instance.
                    _server.Dispose();
                    _server = null;
                }
            }
            catch (Exception ex)
            {
                Error(ex, "Robin pipe listener failed");
                _server?.Dispose();
                _server = null;
                if (++failures >= 100)
                {
                    DxHook.SetFpsTargets(0, 0, 0);
                    AudioMuteSystem.ClearMutedIds();
                    IsNamedPipeRunning = false;
                }
                else await Task.Delay(100).ConfigureAwait(false);
            }
        }
        _server?.Dispose();
    }

    private static async Task ProcessConnectionAsync(Stream stream, CancellationToken cancellation)
    {
        var header = new byte[2];
        await stream.ReadExactlyAsync(header, cancellation).ConfigureAwait(false);
        byte direction = header[0], command = header[1];
        int payloadLength = (direction, command) switch
        {
            (0xA3, 0xB3) or (0xA2, 0xB4) => 4,
            (0xA2, 0xF1) => 14,
            (0xA2, 0xC2 or 0xC3 or 0xC6) => 4,
            (0xA2, 0xC7) => 1,
            _ => 0
        };
        byte[] payload = new byte[payloadLength];
        await stream.ReadExactlyAsync(payload, cancellation).ConfigureAwait(false);
        if (direction == 0xA2 && command is 0xC2 or 0xC3 or 0xC6)
        {
            int count = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload);
            if (count < 0 || count > AudioMuteSystem.MaxMutedIds) throw new InvalidDataException("Invalid audio list length.");
            Array.Resize(ref payload, 4 + count * 4);
            await stream.ReadExactlyAsync(payload.AsMemory(4), cancellation).ConfigureAwait(false);
        }
        // Only complete, bounded requests reach the state-changing code.
        using var input = new MemoryStream(payload);
        using var reader = new BinaryReader(input);
        using var response = new MemoryStream();
        using (var writer = new BinaryWriter(response, System.Text.Encoding.UTF8, leaveOpen: true))
            ProcessCommand(direction, command, reader, writer);
        if (response.Length > 0)
        {
            await stream.WriteAsync(response.GetBuffer().AsMemory(0, (int)response.Length), cancellation).ConfigureAwait(false);
            // Wait for the peer to consume the reply and close before disconnecting (which
            // can discard unread pipe bytes). A stalled peer still has the same deadline.
            await stream.ReadAsync(new byte[1], cancellation).ConfigureAwait(false);
        }
    }

    private static void ProcessCommand(byte direction, byte command, BinaryReader reader, BinaryWriter writer)
    {
        switch (direction, command)
        {
            case (0xA3, 0xB1):
                DxHook.PrepareForFocus();
                return;
            case (0xA3, 0xB3):
                Volatile.Write(ref DxHook.PredictedFocusTimeoutMs, Math.Clamp(reader.ReadInt32(), 1, 30000));
                DxHook.SetOurWindowInFocus(FocusType.Predicted);
                return;
            case (0xA2, 0xB4): ClaimOwnership(reader.ReadInt32()); break;
            case (0xA2, 0xF1):
                int foreground = reader.ReadInt32();
                if (reader.ReadByte() != 0xF2) throw new InvalidDataException("Invalid background FPS marker.");
                int background = reader.ReadInt32();
                if (reader.ReadByte() != 0xF3) throw new InvalidDataException("Invalid predicted FPS marker.");
                int predicted = reader.ReadInt32();
                DxHook.SetFpsTargets(foreground, background, predicted);
                break;
            case (0xA2, 0xC1): AudioMuteSystem.ClearMutedIds(); break;
            case (0xA2, 0xC7): AudioMuteSystem.SetDiagnosticCapture(reader.ReadBoolean()); break;
            case (0xA2, 0xC2 or 0xC3 or 0xC6):
                int count = reader.ReadInt32();
                var ids = new uint[count];
                for (int i = 0; i < count; i++) ids[i] = reader.ReadUInt32();
                AudioMuteSystem.UpdateMutedIds(ids, replace: command == 0xC6, remove: command == 0xC2);
                break;
            case (0xA1, 0xB2): writer.Write((byte)1); return;
            case (0xA1, 0xB5): writer.Write((byte)2); return; // Protocol capabilities: atomic audio replacement.
            case (0xA1, 0xB6): writer.Write((byte)(DxHook.HooksInstalled ? 1 : 0)); return;
            case (0xA1, 0xB8): writer.Write((byte)(AudioMuteSystem.IsMonitorReady ? 1 : 0)); return;
            case (0xA1, 0xB7):
                byte[] identity = Encoding.UTF8.GetBytes(BuildIdentity.Value);
                writer.Write(identity.Length);
                writer.Write(identity);
                return;
            case (0xA1, 0xA1):
                var targets = DxHook.Targets;
                writer.Write((byte)0xF1); writer.Write(targets.Foreground);
                writer.Write((byte)0xF2); writer.Write(targets.Background);
                writer.Write((byte)0xF3); writer.Write(targets.Predicted);
                writer.Write((byte)1);
                return;
            case (0xA1, 0xC4):
                var muted = AudioMuteSystem.GetMutedIds();
                writer.Write(muted.Count);
                foreach (uint id in muted) writer.Write(id);
                return;
            case (0xA1, 0xC5):
                var history = AudioLog.GetOrderedEventHistory();
                writer.Write(history.Count);
                foreach (var entry in history) { writer.Write(entry.EventID); writer.Write(entry.GameObjectID); writer.Write(entry.Timestamp); }
                return;
            default: writer.Write((byte)0); return;
        }
        writer.Write((byte)1);
    }
}
