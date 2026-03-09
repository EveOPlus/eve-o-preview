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
using Serilog.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveOPreview.Services.Implementation
{
    public class HookService : IHookService
    {
        private const byte PIPE_FIRE_AND_FORGET = 0xA3;
        private const byte PIPE_SET_FOCUSED_COMMAND = 0xB1;
        private const byte PIPE_PREDICT_FOCUS_COMMAND = 0xB3;

        private const byte PIPE_QUERY = 0xA1;
        private const byte PIPE_PING_REQUEST_CODE = 0xB2;

        private const byte PIPE_UPDATE = 0xA2;
        private const byte PIPE_FPS_PREFIX_BYTE_FOCUSED = 0xF1;
        private const byte PIPE_FPS_PREFIX_BYTE_BACKGROUND = 0xF2;
        private const byte PIPE_FPS_PREFIX_BYTE_PREDICT = 0xF3;
        private const byte PIPE_TAKE_OWNERSHIP_COMMAND = 0xB4;

        private const byte PIPE_SOUND_UNMUTE_ALL = 0xC1;
        private const byte PIPE_SOUND_MUTE_LIST = 0xC3;

        private const byte PIPE_SUCCESS_RESPONSE_CODE = 0x01;
        
        private readonly IThumbnailConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IntPtr, Guid> _initializedClients = new ConcurrentDictionary<IntPtr, Guid>();
        private readonly object _lock = new object();
        
        public HookService(IThumbnailConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }
        
        public bool Ping(IntPtr handle)
        {
            try
            {
                // Ping request is 0xA1 0xB2, Expect a 0x01 response.
                using (var client = GetClientsNamedPipe(handle, PipeDirection.InOut))
                {
                    client.Connect(100);
                    using (var writer = new BinaryWriter(client))
                    using (var reader = new BinaryReader(client))
                    {
                        writer.Write(PIPE_QUERY);
                        writer.Write(PIPE_PING_REQUEST_CODE);
                        writer.Flush();

                        var response = reader.ReadByte();

                        _logger.Verbose("Pipe Successfully pinged Robin.");
                        return response == PIPE_SUCCESS_RESPONSE_CODE;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Verbose(ex, "Failed to ping Robin.");
                return false;
            }
        }
        
        public async Task TellEveClientFocusIsComingAsync(IntPtr handle)
        {
            if (!CanAccessFpsLimiter())
            {
                return;
            }
            
            await Task.Run(() =>
            {
                // Fire and forget
                try
                {
                    using (var client = GetClientsNamedPipe(handle, PipeDirection.Out))
                    {
                        client.Connect(100);
                        using (var writer = new BinaryWriter(client))
                        {
                            writer.Write(PIPE_FIRE_AND_FORGET);
                            writer.Write(PIPE_SET_FOCUSED_COMMAND);
                            writer.Flush();
                            
                            _logger.Verbose($"Pipe sent FireAndForget SetFocusedCommand for handle {handle}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Pipe Error while calling FireAndForget SetFocusedCommand for handle {handle}");
                    // Just be silent and don't block anything else.
                }
            });
        }
        
        public async Task TellEveClientFocusIsMaybeComingSoonAsync(IntPtr handle, int timeoutMs = 5000)
        {
            if (!CanAccessFpsLimiter())
            {
                return;
            }

            await Task.Run(() =>
            {
                // Fire and forget
                try
                {
                    using (var client = GetClientsNamedPipe(handle, PipeDirection.Out))
                    {
                        client.Connect(100);
                        using (var writer = new BinaryWriter(client))
                        {
                            writer.Write(PIPE_FIRE_AND_FORGET);
                            writer.Write(PIPE_PREDICT_FOCUS_COMMAND);
                            writer.Write(timeoutMs); // How long to wait before for focus before return to normal.
                            writer.Flush();
                            
                            _logger.Verbose($"Pipe sent FireAndForget PredictFocusCommand for handle {handle}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Pipe Error while calling FireAndForget PredictFocusCommand for handle {handle}");
                    // Just be silent and don't block anything else.
                }
            });
        }

        public async Task<bool> UpdateTargetFpsAsync(IntPtr handle)
        {
            if (!CanAccessFpsLimiter())
            {
                return false;
            }

            var fpsSettings = _configuration.FpsLimiterSettings;
            int foregroundFps = fpsSettings.IsEnabled ? fpsSettings.FpsFocused : 0;
            int backgroundFps = fpsSettings.IsEnabled ? fpsSettings.FpsBackground : 0;
            int predictiveFps = fpsSettings.IsEnabled ? fpsSettings.FpsPredictingFocus : 0;

            var result = await SetExactTargetFpsAsync(handle, foregroundFps, backgroundFps, predictiveFps);

            _logger.Information($"Pipe succeeded to set target fps = {result} with settings {nameof(foregroundFps)}={foregroundFps} {nameof(backgroundFps)}={backgroundFps} {nameof(predictiveFps)}={predictiveFps} for handle {handle}");

            return result;
        }

        public async Task<bool> DisableFpsLimiterAsync(IntPtr handle)
        {
            return await SetExactTargetFpsAsync(handle, 0, 0, 0).ConfigureAwait(false);
        }

        public async Task TryInstallHooksAsync(IProcessInfo procInfo)
        {
            if (!CanAccessFpsLimiter() && !CanAccessAudioMute())
            {
                return;
            }

            bool isFirstTimeInitializing = _initializedClients.TryAdd(procInfo.Handle, Guid.NewGuid());

            await Task.Run(() =>
            {
                try
                {
                    if (!isFirstTimeInitializing || Ping(procInfo.Handle))
                    {
                        // Already installed, as the pipe is active and responding. Don't try to install another hook.
                        // e.g. If Eve-O was previously running and started up again, while Eve clients are already initialized.
                    }
                    else
                    {
                        var basePath = AppContext.BaseDirectory;
                        var dllPath = Path.Combine(basePath, "Eve-O-Preview.Robin.dll");
                        if (!File.Exists(dllPath)) throw new Exception($"Unable to find Eve-O-Preview.Robin.dll at: {dllPath}");
                        
                        var proc = Process.GetProcessById(procInfo.ProcessId);

                        _logger.Information($"Eve-O-Preview.Robin Working on handle {proc.MainWindowHandle}");
                        IntPtr hProc = KernelNativeMethods.OpenProcess(KernelNativeMethods.PROCESS_ALL_ACCESS, false, proc.Id);
                        if (hProc == IntPtr.Zero) throw new Exception("Failed to open process.");

                        string fullPath = Path.GetFullPath(dllPath);
                        byte[] pathBytes = Encoding.ASCII.GetBytes(fullPath + "\0");

                        _logger.Verbose($"Allocate memory for DLL path string");
                        IntPtr remoteAddr = KernelNativeMethods.VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length, KernelNativeMethods.MEM_COMMIT_RESERVE, KernelNativeMethods.PAGE_EXECUTE_READWRITE);
                        if (remoteAddr == IntPtr.Zero) throw new Exception("Memory allocation failed.");

                        _logger.Verbose($"Write DLL path to target process");
                        if (!KernelNativeMethods.WriteProcessMemory(hProc, remoteAddr, pathBytes, (uint)pathBytes.Length, out _))
                            throw new Exception("Failed to write to memory.");

                        _logger.Verbose($"Call LoadLibraryA in target process");
                        IntPtr loadLibAddr = KernelNativeMethods.GetProcAddress(KernelNativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryA");
                        IntPtr hThread = KernelNativeMethods.CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLibAddr, remoteAddr, 0, IntPtr.Zero);

                        if (hThread == IntPtr.Zero) throw new Exception("CreateRemoteThread for LoadLibrary failed.");

                        System.Threading.Thread.Sleep(2000); // Wait for module to load

                        _logger.Verbose($"Verify and find 'Initialize' export offset");
                        proc.Refresh();
                        var loadedModule = proc.Modules.Cast<ProcessModule>().FirstOrDefault(m => m.FileName.Contains("Eve-O-Preview.Robin"));
                        if (loadedModule == null) throw new Exception("DLL was not loaded into the target process.");

                        IntPtr localModule = KernelNativeMethods.LoadLibrary(fullPath);
                        IntPtr localInitAddr = KernelNativeMethods.GetProcAddress(localModule, "Initialize");
                        if (localInitAddr == IntPtr.Zero) throw new Exception("Could not find 'Initialize' export in DLL.");

                        // Calculate remote address: (Target Base + (Local Init - Local Base))
                        long offset = localInitAddr.ToInt64() - localModule.ToInt64();
                        IntPtr remoteInitAddr = new IntPtr(loadedModule.BaseAddress.ToInt64() + offset);

                        // Execute 'Initialize' in target process
                        _logger.Verbose($"Execute 'Initialize' in target process");
                        KernelNativeMethods.CreateRemoteThread(hProc, IntPtr.Zero, 0, remoteInitAddr, IntPtr.Zero, 0, IntPtr.Zero);

                        _logger.Information("Successfully initialized Eve-O-Preview.Robin");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Unhandled exception in {nameof(TryInstallHooksAsync)}");

                    _initializedClients.TryRemove(procInfo.Handle, out _);
                }
            });

            // Make sure it has a moment to initialize, just to be safe.
            await Task.Delay(1000);
            
            // Take ownership of the currently running hook.
            await SendTakeOwnershipCommand(procInfo.Handle, Process.GetCurrentProcess().Id);

            // Regardless if we just installed it, or it was already installed, set the FPS to our target
            await UpdateTargetFpsAsync(procInfo.Handle);
            await UpdateMutedAudioAsync(procInfo.Handle);
        }

        private const uint jump_gates_start_play = 3689163958;
        private const uint jump_gates_exit_play = 1537508544;
        private const uint jump_gates_lightning_play = 1768044352;
        private const uint location_banner_play = 2377891014;
        private const uint location_banner_data_clicks_play = 3090840445;
        public async Task<bool> UpdateMutedAudioAsync(IntPtr handle)
        {
            if (!CanAccessAudioMute())
            {
                return false;
            }

            await ClearMutedAudioListAsync(handle);

            var mutedEventIds = new List<uint>();

            if (_configuration.AudioMuteSettings.MuteJumpGateTunnel)
            {
                mutedEventIds.Add(jump_gates_start_play);
                mutedEventIds.Add(jump_gates_exit_play);
                mutedEventIds.Add(jump_gates_lightning_play);
                
                _logger.Information("Pipe Muting gate tunnel audio.");
            }

            if (_configuration.AudioMuteSettings.MuteLocationBanner)
            {
                mutedEventIds.Add(location_banner_play);
                mutedEventIds.Add(location_banner_data_clicks_play);
                
                _logger.Information("Pipe Muting location banner audio.");
            }

            return await SetMutedAudioListAsync(handle, mutedEventIds);
        }

        private async Task<bool> ClearMutedAudioListAsync(IntPtr handle)
        {
            return await Task.Run<bool>(() =>
            {
                try
                {
                    // 0xA2 0xC1 clear all muted sounds. expect 0x01 result.
                    using (var client = GetClientsNamedPipe(handle, PipeDirection.InOut))
                    {
                        client.Connect(100);
                        using (var writer = new BinaryWriter(client))
                        using (var reader = new BinaryReader(client))
                        {
                            writer.Write(PIPE_UPDATE);
                            writer.Write(PIPE_SOUND_UNMUTE_ALL);
                            writer.Flush();

                            var response = reader.ReadByte();

                            _logger.Verbose($"Pipe sent PipeUpdate PipeSoundUnmuteAll for handle {handle}");
                            return response == PIPE_SUCCESS_RESPONSE_CODE;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Pipe Error while calling PipeUpdate PipeSoundUnmuteAll for handle {handle}");
                    return false;
                }
            });
        }

        private async Task<bool> SetMutedAudioListAsync(IntPtr handle, List<uint> mutedEventIds)
        {
            return await Task.Run<bool>(() =>
            {
                try
                {
                    // 0xA2 0xC3 send length followed by list of uint, expect 0x01 result.
                    using (var client = GetClientsNamedPipe(handle, PipeDirection.InOut))
                    {
                        client.Connect(100);
                        using (var writer = new BinaryWriter(client))
                        using (var reader = new BinaryReader(client))
                        {
                            writer.Write(PIPE_UPDATE);
                            writer.Write(PIPE_SOUND_MUTE_LIST);
                            
                            writer.Write(mutedEventIds.Count);
                            foreach (var eventId in mutedEventIds)
                            {
                                writer.Write(eventId);
                            }

                            writer.Flush();

                            var response = reader.ReadByte();
                            _logger.Verbose($"Pipe sent PipeUpdate PipeSoundMuteList for handle {handle}");

                            return response == PIPE_SUCCESS_RESPONSE_CODE;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Pipe Error while calling PipeUpdate PipeSoundMuteList for handle {handle}");
                    return false;
                }
            });
        }

        private async Task<bool> SendTakeOwnershipCommand(IntPtr handle, int newOwnerProcessId)
        {
            return await Task.Run<bool>(() =>
            {
                try
                {
                    // Take ownership is 0xA2 0xB4 (int)ownerProcessId, Expect a 0x01 response.
                    using (var client = GetClientsNamedPipe(handle, PipeDirection.InOut))
                    {
                        client.Connect(100);
                        using (var writer = new BinaryWriter(client))
                        using (var reader = new BinaryReader(client))
                        {
                            writer.Write(PIPE_UPDATE);
                            writer.Write(PIPE_TAKE_OWNERSHIP_COMMAND);
                            writer.Write(newOwnerProcessId);
                            writer.Flush();

                            var response = reader.ReadByte();
                            _logger.Verbose($"Pipe sent PipeUpdate PipeTakeOwnershipCommand for handle {handle}");

                            return response == PIPE_SUCCESS_RESPONSE_CODE;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Pipe Error while calling PipeUpdate PipeTakeOwnershipCommand for handle {handle}");
                    return false;
                }
            });
        }

        private async Task<bool> SetExactTargetFpsAsync(IntPtr handle, int foregroundFps, int backgroundFps, int predictiveFps)
        {
            return await Task.Run<bool>(() =>
            {
                try
                {
                    return MakeThePipeCallToSetFps(handle, foregroundFps, backgroundFps, predictiveFps);
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);
        }

        private bool MakeThePipeCallToSetFps(IntPtr handle, int foregroundFps, int backgroundFps, int predictiveFps)
        {
            try
            {
                // Update FPS is 0xA2 0xF1 (int)foreground 0xF2 (int)background 0xF3 (int)predictive, Expect a 0x01 response.
                using (var client = GetClientsNamedPipe(handle, PipeDirection.InOut))
                {
                    client.Connect(100);
                    using (var writer = new BinaryWriter(client))
                    using (var reader = new BinaryReader(client))
                    {
                        writer.Write(PIPE_UPDATE);
                        writer.Write(PIPE_FPS_PREFIX_BYTE_FOCUSED);
                        writer.Write(foregroundFps);
                        writer.Write(PIPE_FPS_PREFIX_BYTE_BACKGROUND);
                        writer.Write(backgroundFps);
                        writer.Write(PIPE_FPS_PREFIX_BYTE_PREDICT);
                        writer.Write(predictiveFps);
                        writer.Flush();

                        var response = reader.ReadByte();

                        return response == PIPE_SUCCESS_RESPONSE_CODE;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Pipe Error while calling PipeUpdate PipeFpsPrefixBytes for handle {handle}");
                return false;
            }
        }

        private NamedPipeClientStream GetClientsNamedPipe(IntPtr handle, PipeDirection direction)
        {
            return new NamedPipeClientStream(".", GetClientsPipeName(handle), direction, PipeOptions.None);
        }

        private string GetClientsPipeName(IntPtr handle)
        {
            return $"EveoRobin_{handle}";
        }

        private bool CanAccessFpsLimiter()
        {
            return _configuration.IsPremium && _configuration.FpsLimiterSettings.IsEnabled;
        }

        private bool CanAccessAudioMute()
        {
            return _configuration.IsPremium;
        }
    }
}