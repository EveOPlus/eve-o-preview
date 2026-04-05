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

        private const uint jump_gates_start_play = 3689163958;
        private const uint jump_gates_exit_play = 1537508544;
        private const uint jump_gates_lightning_play = 1768044352;
        private const uint location_banner_play = 2377891014;
        private const uint location_banner_data_clicks_play = 3090840445;
        
        private readonly IThumbnailConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IntPtr, Guid> _initializedClients = new ConcurrentDictionary<IntPtr, Guid>();
        private readonly object _lock = new object();
        
        public HookService(IThumbnailConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _logger.Information("HookService: Initialized. Premium={Premium}, FpsLimiterEnabled={FpsLimiterEnabled}", configuration.IsPremium, configuration.FpsLimiterSettings.IsEnabled);
        }
        
        public bool Ping(IntPtr handle)
        {
            _logger.Verbose("HookService.Ping: Attempting to ping Robin for handle 0x{Handle:X}", handle);

            try
            {
                // Ping request is 0xA1 0xB2, Expect a 0x01 response.
                using (var client = GetClientsNamedPipe(handle, PipeDirection.InOut))
                {
                    _logger.Verbose("HookService.Ping: Connecting to Robin pipe (100ms timeout)");
                    client.Connect(100);

                    using (var writer = new BinaryWriter(client))
                    using (var reader = new BinaryReader(client))
                    {
                        writer.Write(PIPE_QUERY);
                        writer.Write(PIPE_PING_REQUEST_CODE);
                        writer.Flush();

                        _logger.Verbose("HookService.Ping: Ping request sent (0xA1 0xB2)");

                        var response = reader.ReadByte();

                        bool success = response == PIPE_SUCCESS_RESPONSE_CODE;
                        _logger.Verbose("HookService.Ping: Handle 0x{Handle:X} - Response: 0x{Response:X2}, Success: {Success}", handle, response, success);
                        return success;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "HookService.Ping: Exception while pinging Robin for handle 0x{Handle:X}", handle);
                return false;
            }
        }
        
        public async Task TellEveClientFocusIsComingAsync(IntPtr handle)
        {
            _logger.Verbose("HookService.TellEveClientFocusIsComingAsync: Notifying Robin of incoming focus for handle 0x{Handle:X}", handle);

            if (!CanAccessFpsLimiter())
            {
                _logger.Verbose("HookService.TellEveClientFocusIsComingAsync: Cannot access FPS limiter (Premium={Premium}, FpsEnabled={FpsEnabled}). Skipping notification.", _configuration.IsPremium, _configuration.FpsLimiterSettings.IsEnabled);
                return;
            }

            _logger.Verbose("HookService.TellEveClientFocusIsComingAsync: Sending FireAndForget SetFocusedCommand (0xA3 0xB1) for handle 0x{Handle:X}", handle);
            
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
                            
                            _logger.Verbose("HookService.TellEveClientFocusIsComingAsync: FireAndForget SetFocusedCommand sent for handle 0x{Handle:X}", handle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "HookService.TellEveClientFocusIsComingAsync: Error sending FireAndForget SetFocusedCommand for handle 0x{Handle:X}", handle);
                    // Just be silent and don't block anything else.
                }
            });
        }
        
        public async Task TellEveClientFocusIsMaybeComingSoonAsync(IntPtr handle, int timeoutMs = 5000)
        {
            _logger.Verbose("HookService.TellEveClientFocusIsMaybeComingSoonAsync: Predicting focus for handle 0x{Handle:X} with timeout {TimeoutMs}ms", handle, timeoutMs);
            
            if (!CanAccessFpsLimiter())
            {
                _logger.Verbose("HookService.TellEveClientFocusIsMaybeComingSoonAsync: Cannot access FPS limiter. Skipping prediction.");
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
                            
                            _logger.Verbose("HookService.TellEveClientFocusIsMaybeComingSoonAsync: FireAndForget PredictFocusCommand sent for handle 0x{Handle:X}", handle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "HookService.TellEveClientFocusIsMaybeComingSoonAsync: Error sending PredictFocusCommand for handle 0x{Handle:X}", handle);
                    // Just be silent and don't block anything else.
                }
            });
        }

        public async Task<bool> UpdateTargetFpsAsync(IntPtr handle)
        {
            _logger.Verbose("HookService.UpdateTargetFpsAsync: Updating FPS settings for handle 0x{Handle:X}", handle);
            
            if (!CanAccessFpsLimiter())
            {
                _logger.Verbose("HookService.UpdateTargetFpsAsync: Cannot access FPS limiter. Skipping update.");
                return false;
            }

            var fpsSettings = _configuration.FpsLimiterSettings;
            int foregroundFps = fpsSettings.IsEnabled ? fpsSettings.FpsFocused : 0;
            int backgroundFps = fpsSettings.IsEnabled ? fpsSettings.FpsBackground : 0;
            int predictiveFps = fpsSettings.IsEnabled ? fpsSettings.FpsPredictingFocus : 0;

            _logger.Verbose("HookService.UpdateTargetFpsAsync: FPS values - Foreground={Foreground}, Background={Background}, Predictive={Predictive}", foregroundFps, backgroundFps, predictiveFps);
            
            var result = await SetExactTargetFpsAsync(handle, foregroundFps, backgroundFps, predictiveFps);

            _logger.Verbose("HookService.UpdateTargetFpsAsync: FPS update result={Result} for handle 0x{Handle:X} with Foreground={Foreground} Background={Background} Predictive={Predictive}", result, handle, foregroundFps, backgroundFps, predictiveFps);

            return result;
        }

        public async Task<bool> DisableFpsLimiterAsync(IntPtr handle)
        {
            _logger.Verbose("HookService.DisableFpsLimiterAsync: Disabling FPS limiter for handle 0x{Handle:X}", handle);
            var result = await SetExactTargetFpsAsync(handle, 0, 0, 0).ConfigureAwait(false);
            _logger.Verbose("HookService.DisableFpsLimiterAsync: Result={Result}", result);
            return result;
        }

        public async Task TryInstallHooksAsync(IProcessInfo procInfo)
        {
            _logger.Verbose("HookService.TryInstallHooksAsync: Attempting to install hooks for process {Title} (PID: {PID}, Handle: 0x{Handle:X})", procInfo.Title, procInfo.ProcessId, procInfo.MainWindowHandle);
            
            if (!CanAccessFpsLimiter() && !CanAccessAudioMute())
            {
                _logger.Verbose("HookService.TryInstallHooksAsync: Cannot access FPS limiter or audio mute. Skipping hook installation.");
                return;
            }

            bool isFirstTimeInitializing = _initializedClients.TryAdd(procInfo.MainWindowHandle, Guid.NewGuid());
            _logger.Verbose("HookService.TryInstallHooksAsync: Is first time initializing: {IsFirstTime}", isFirstTimeInitializing);

            await Task.Run(() =>
            {
                try
                {
                    if (!isFirstTimeInitializing || Ping(procInfo.MainWindowHandle))
                    {
                        _logger.Verbose("HookService.TryInstallHooksAsync: Hooks already installed for handle 0x{Handle:X}. Skipping installation.", procInfo.MainWindowHandle);
                        // Already installed, as the pipe is active and responding. Don't try to install another hook.
                        // e.g. If Eve-O was previously running and started up again, while Eve clients are already initialized.
                    }
                    else
                    {
                        _logger.Verbose("HookService.TryInstallHooksAsync: Installing Eve-O-Preview.Robin for handle 0x{Handle:X}", procInfo.MainWindowHandle);
                        
                        var basePath = System.IO.Path.GetDirectoryName(System.Environment.ProcessPath);
                        var dllPath = Path.Combine(basePath, "Eve-O-Preview.Robin.dll");
                        if (!File.Exists(dllPath)) throw new Exception($"Unable to find Eve-O-Preview.Robin.dll at: {dllPath}");
                        
                        var proc = Process.GetProcessById(procInfo.ProcessId);

                        _logger.Verbose("HookService.TryInstallHooksAsync: Opening process handle");
                        IntPtr hProc = KernelNativeMethods.OpenProcess(KernelNativeMethods.PROCESS_ALL_ACCESS, false, proc.Id);
                        if (hProc == IntPtr.Zero) throw new Exception("Failed to open process.");

                        string fullPath = Path.GetFullPath(dllPath);
                        byte[] pathBytes = Encoding.ASCII.GetBytes(fullPath + "\0");

                        _logger.Verbose("HookService.TryInstallHooksAsync: Allocating memory for DLL path ({ByteCount} bytes)", pathBytes.Length);
                        IntPtr remoteAddr = KernelNativeMethods.VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length, KernelNativeMethods.MEM_COMMIT_RESERVE, KernelNativeMethods.PAGE_EXECUTE_READWRITE);
                        if (remoteAddr == IntPtr.Zero) throw new Exception("Memory allocation failed.");

                        _logger.Verbose("HookService.TryInstallHooksAsync: Writing DLL path to target process");
                        if (!KernelNativeMethods.WriteProcessMemory(hProc, remoteAddr, pathBytes, (uint)pathBytes.Length, out _))
                            throw new Exception("Failed to write to memory.");

                        _logger.Verbose("HookService.TryInstallHooksAsync: Creating remote thread to call LoadLibraryA");
                        IntPtr loadLibAddr = KernelNativeMethods.GetProcAddress(KernelNativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryA");
                        IntPtr hThread = KernelNativeMethods.CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLibAddr, remoteAddr, 0, IntPtr.Zero);

                        if (hThread == IntPtr.Zero) throw new Exception("CreateRemoteThread for LoadLibrary failed.");

                        _logger.Verbose("HookService.TryInstallHooksAsync: Waiting 2000ms for module to load");
                        System.Threading.Thread.Sleep(2000); // Wait for module to load

                        _logger.Verbose("HookService.TryInstallHooksAsync: Verifying DLL loaded and finding Initialize export");
                        proc.Refresh();
                        var loadedModule = proc.Modules.Cast<ProcessModule>().FirstOrDefault(m => m.FileName.Contains("Eve-O-Preview.Robin"));
                        if (loadedModule == null) throw new Exception("DLL was not loaded into the target process.");

                        _logger.Verbose("HookService.TryInstallHooksAsync: DLL loaded at 0x{BaseAddress:X}", loadedModule.BaseAddress);
                        
                        IntPtr localModule = KernelNativeMethods.LoadLibrary(fullPath);
                        IntPtr localInitAddr = KernelNativeMethods.GetProcAddress(localModule, "Initialize");
                        if (localInitAddr == IntPtr.Zero) throw new Exception("Could not find 'Initialize' export in DLL.");

                        // Calculate remote address: (Target Base + (Local Init - Local Base))
                        long offset = localInitAddr.ToInt64() - localModule.ToInt64();
                        IntPtr remoteInitAddr = new IntPtr(loadedModule.BaseAddress.ToInt64() + offset);

                        // Execute 'Initialize' in target process
                        _logger.Verbose("HookService.TryInstallHooksAsync: Creating remote thread to call Initialize");
                        KernelNativeMethods.CreateRemoteThread(hProc, IntPtr.Zero, 0, remoteInitAddr, IntPtr.Zero, 0, IntPtr.Zero);

                        _logger.Information("HookService.TryInstallHooksAsync: Successfully initialized Eve-O-Preview.Robin for handle 0x{Handle:X}", procInfo.MainWindowHandle);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "HookService.TryInstallHooksAsync: Unhandled exception while installing hooks for handle 0x{Handle:X}", procInfo.MainWindowHandle);

                    _initializedClients.TryRemove(procInfo.MainWindowHandle, out _);
                }
            });

            // Make sure it has a moment to initialize, just to be safe.
            _logger.Verbose("HookService.TryInstallHooksAsync: Waiting 1000ms for hook initialization");
            await Task.Delay(1000);
            
            // Take ownership of the currently running hook.
            _logger.Verbose("HookService.TryInstallHooksAsync: Sending take ownership command");
            await SendTakeOwnershipCommand(procInfo.MainWindowHandle, Process.GetCurrentProcess().Id);

            // Regardless if we just installed it, or it was already installed, set the FPS to our target
            _logger.Verbose("HookService.TryInstallHooksAsync: Updating FPS settings");
            await UpdateTargetFpsAsync(procInfo.MainWindowHandle);
            
            _logger.Verbose("HookService.TryInstallHooksAsync: Updating audio settings");
            await UpdateMutedAudioAsync(procInfo.MainWindowHandle);
            
            _logger.Verbose("HookService.TryInstallHooksAsync: Hook installation completed for handle 0x{Handle:X}", procInfo.MainWindowHandle);
        }

        public async Task<bool> UpdateMutedAudioAsync(IntPtr handle)
        {
            _logger.Verbose("HookService.UpdateMutedAudioAsync: Updating muted audio for handle 0x{Handle:X}", handle);
            
            if (!CanAccessAudioMute())
            {
                _logger.Verbose("HookService.UpdateMutedAudioAsync: Cannot access audio mute (not premium). Skipping.");
                return false;
            }

            await ClearMutedAudioListAsync(handle);

            var mutedEventIds = new List<uint>();

            if (_configuration.AudioMuteSettings.MuteJumpGateTunnel)
            {
                mutedEventIds.Add(jump_gates_start_play);
                mutedEventIds.Add(jump_gates_exit_play);
                mutedEventIds.Add(jump_gates_lightning_play);
                
                _logger.Verbose("HookService.UpdateMutedAudioAsync: Adding jump gate tunnel sounds to mute list (3 event IDs)");
            }

            if (_configuration.AudioMuteSettings.MuteLocationBanner)
            {
                mutedEventIds.Add(location_banner_play);
                mutedEventIds.Add(location_banner_data_clicks_play);
                
                _logger.Verbose("HookService.UpdateMutedAudioAsync: Adding location banner sounds to mute list (2 event IDs)");
            }

            _logger.Verbose("HookService.UpdateMutedAudioAsync: Setting {EventCount} muted audio event IDs for handle 0x{Handle:X}", mutedEventIds.Count, handle);
            var result = await SetMutedAudioListAsync(handle, mutedEventIds);
            _logger.Verbose("HookService.UpdateMutedAudioAsync: Result: {Result}", result);
            return result;
        }

        private async Task<bool> ClearMutedAudioListAsync(IntPtr handle)
        {
            _logger.Verbose("HookService.ClearMutedAudioListAsync: Clearing all muted audio for handle 0x{Handle:X}", handle);
            
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

                            bool success = response == PIPE_SUCCESS_RESPONSE_CODE;
                            _logger.Verbose("HookService.ClearMutedAudioListAsync: Response: 0x{Response:X2}, Success: {Success}", response, success);
                            return success;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "HookService.ClearMutedAudioListAsync: Error clearing muted audio for handle 0x{Handle:X}", handle);
                    return false;
                }
            });
        }

        private async Task<bool> SetMutedAudioListAsync(IntPtr handle, List<uint> mutedEventIds)
        {
            _logger.Verbose("HookService.SetMutedAudioListAsync: Setting {EventCount} muted event IDs for handle 0x{Handle:X}", mutedEventIds.Count, handle);
            
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
                            bool success = response == PIPE_SUCCESS_RESPONSE_CODE;
                            _logger.Verbose("HookService.SetMutedAudioListAsync: Response: 0x{Response:X2}, Success: {Success}", response, success);

                            return success;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "HookService.SetMutedAudioListAsync: Error setting muted audio list for handle 0x{Handle:X}", handle);
                    return false;
                }
            });
        }

        private async Task<bool> SendTakeOwnershipCommand(IntPtr handle, int newOwnerProcessId)
        {
            _logger.Verbose("HookService.SendTakeOwnershipCommand: Sending take ownership command for handle 0x{Handle:X} to PID {OwnerPID}", handle, newOwnerProcessId);
            
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
                            bool success = response == PIPE_SUCCESS_RESPONSE_CODE;
                            _logger.Verbose("HookService.SendTakeOwnershipCommand: Response: 0x{Response:X2}, Success: {Success}", response, success);

                            return success;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "HookService.SendTakeOwnershipCommand: Error sending take ownership command for handle 0x{Handle:X}", handle);
                    return false;
                }
            });
        }

        private async Task<bool> SetExactTargetFpsAsync(IntPtr handle, int foregroundFps, int backgroundFps, int predictiveFps)
        {
            _logger.Verbose("HookService.SetExactTargetFpsAsync: Setting exact FPS for handle 0x{Handle:X} (Foreground={FG}, Background={BG}, Predictive={Pred})", handle, foregroundFps, backgroundFps, predictiveFps);
            
            return await Task.Run<bool>(() =>
            {
                try
                {
                    var result = MakeThePipeCallToSetFps(handle, foregroundFps, backgroundFps, predictiveFps);
                    _logger.Verbose("HookService.SetExactTargetFpsAsync: Result: {Result}", result);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "HookService.SetExactTargetFpsAsync: Exception while setting exact FPS for handle 0x{Handle:X}", handle);
                    return false;
                }
            }).ConfigureAwait(false);
        }

        private bool MakeThePipeCallToSetFps(IntPtr handle, int foregroundFps, int backgroundFps, int predictiveFps)
        {
            _logger.Verbose("HookService.MakeThePipeCallToSetFps: Making pipe call to set FPS for handle 0x{Handle:X} (FG={FG}, BG={BG}, Pred={Pred})", handle, foregroundFps, backgroundFps, predictiveFps);
            
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
                        bool success = response == PIPE_SUCCESS_RESPONSE_CODE;
                        _logger.Verbose("HookService.MakeThePipeCallToSetFps: Response: 0x{Response:X2}, Success: {Success}", response, success);

                        return success;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "HookService.MakeThePipeCallToSetFps: Pipe error while setting FPS for handle 0x{Handle:X}", handle);
                return false;
            }
        }

        private NamedPipeClientStream GetClientsNamedPipe(IntPtr handle, PipeDirection direction)
        {
            var pipeName = GetClientsPipeName(handle);
            _logger.Verbose("HookService.GetClientsNamedPipe: Creating named pipe client: {PipeName}", pipeName);
            return new NamedPipeClientStream(".", pipeName, direction, PipeOptions.None);
        }

        private string GetClientsPipeName(IntPtr handle)
        {
            return $"EveoRobin_{handle}";
        }

        private bool CanAccessFpsLimiter()
        {
            bool canAccess = _configuration.IsPremium && _configuration.FpsLimiterSettings.IsEnabled;
            if (!canAccess)
            {
                _logger.Verbose("HookService.CanAccessFpsLimiter: Access denied (Premium={Premium}, FpsEnabled={FpsEnabled})", _configuration.IsPremium, _configuration.FpsLimiterSettings.IsEnabled);
            }
            return canAccess;
        }

        private bool CanAccessAudioMute()
        {
            bool canAccess = _configuration.IsPremium;
            if (!canAccess)
            {
                _logger.Verbose("HookService.CanAccessAudioMute: Access denied (Premium={Premium})", _configuration.IsPremium);
            }
            return canAccess;
        }
    }
}