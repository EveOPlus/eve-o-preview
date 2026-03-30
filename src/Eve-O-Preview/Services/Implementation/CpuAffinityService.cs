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
using EveOPreview.Helper;
using EveOPreview.Services.Interface;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using static EveOPreview.Services.Interop.KernelNativeMethods;
using static System.Net.Mime.MediaTypeNames;

namespace EveOPreview.Services.Implementation;

public class CpuAffinityService : ICpuAffinityService
{
    private readonly ILogger _logger;
    private readonly IThumbnailConfiguration _config;
    private const int MaxBitsPerGroup = 64;
    private const ulong SingleCoreBit = 1UL;
    public List<int> PCores { get; } = [];
    public List<int> ECores { get; } = [];

    private IntPtr _activeMask; // Where we will place the current active client.
    private IntPtr _nextMask; // Where we will place the predicted next active client.
    private IntPtr _prevMask; // Where we will place the previous client (ready to go back quickly if they reverse the expected order)
    private IntPtr _backgroundMask; // Where all background processes will run otherwise.
    private IntPtr _allCoresMask; // List of all cores so we can easily turn our automation back off.

    private readonly HashSet<IntPtr?> _currentBackgroundHandles = [];

    private bool _isOurCpuAbleToSupportAffinity = true;
    private Lock _lock = new ();
    private bool _isRunning = false;

    public CpuAffinityService(ILogger logger, IThumbnailConfiguration config)
    {
        _logger = logger;
        _config = config;
        DetectCores();
        _logger.Information($"Auto detected CPU Architecture with {PCores.Count} Performance Cores and {ECores} Efficiency Cores.");

        PreCalculateZones();
    }

    public void UpdateAffinity(IProcessInfo active, IProcessInfo next, IProcessInfo prev, IEnumerable<IProcessInfo> allClients)
    {
        if (!_isOurCpuAbleToSupportAffinity || !_config.IsPremium || !_config.EnableAutomaticCpuAffinity)
        {
            _logger.Verbose("EnableAutomaticCpuAffinity is disabled, or not supported. Skipping...");
            return;
        }

        lock (_lock)
        {
            _isRunning = true; // If we've ever changed the affinity, flag it so we know.
        }

        if (active == null)
        {
            active = next;
            next = null;

            if (active == null)
            {
                active = prev;
                prev = null;
            }
        }

        ApplyFast(active?.ProcessHandle, _activeMask);
        ApplyFast(next?.ProcessHandle, _nextMask);
        ApplyFast(prev?.ProcessHandle, _prevMask);

        lock (_lock)
        {
            _currentBackgroundHandles.Remove(active?.ProcessHandle);
            _currentBackgroundHandles.Remove(next?.ProcessHandle);
            _currentBackgroundHandles.Remove(prev?.ProcessHandle);
        }

        foreach (var processInfo in allClients)
        {
            if (processInfo == active || processInfo == next || processInfo == prev)
            {
                continue;
            }

            lock (_lock)
            {
                if (!_currentBackgroundHandles.Add(processInfo?.ProcessHandle))
                {
                    continue;
                }
            }

            ApplyFast(processInfo?.ProcessHandle, _backgroundMask);
        }

        _logger.Verbose($"Set Affinity as: Active Pid = {active?.ProcessId}, Next Pid = {next?.ProcessId}, Previous Pid = {prev?.ProcessId}");
    }
    public void ResetAll(IEnumerable<IProcessInfo> allClients)
    {
        lock (_lock)
        {
            if (!_isRunning)
            {
                // If we never touched the affinity, we have nothing to reset.
                return;
            }
        }

        _logger.WithCallerInfo().Information($"Resetting CPU Affinity to all cores.");


        foreach (var client in allClients)
        {
            if (client == null || client.ProcessHandle == IntPtr.Zero)
            {
                continue;
            }

            SetProcessAffinityMask(client.ProcessHandle, _allCoresMask);
        }

        lock (_lock)
        {
            // Clear our tracking state so we can turn it back on again later without getting confused.
            _currentBackgroundHandles.Clear();
            _isRunning = false;
        }
    }

    private void ApplyFast(IntPtr? hProcess, IntPtr mask, uint priority = NORMAL_PRIORITY_CLASS)
    {
        if (hProcess == null || hProcess == IntPtr.Zero)
        {
            return;
        }

        SetProcessAffinityMask(hProcess.Value, mask);
        // SetPriorityClass(hProcess.Value, priority); I don't think theres much value manually setting priority. The windows scheduler can handle this for us... leaving this here to make it easy to switch back on if needed.
    }

    private void DetectCores()
    {
        uint length = 0;
        GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, IntPtr.Zero, ref length);

        IntPtr buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, buffer, ref length))
            {
                IntPtr current = buffer;
                int offset = 0;

                while (offset < length)
                {
                    var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>(current);

                    var core = info.Processor;

                    if (_logger.IsEnabled(LogEventLevel.Verbose))
                    {
                        var data = JsonConvert.SerializeObject(info);
                        _logger.Verbose($"Core Information: {data}");
                    }

                    // EfficiencyClass: Higher is better (P-Core), Lower is 0 (E-Core)
                    // GroupMask contains the logical processor bits
                    byte effClass = core.EfficiencyClass;
                    ulong mask = core.GroupMask.Mask;

                    for (int i = 0; i < MaxBitsPerGroup; i++)
                    {
                        if ((mask & (SingleCoreBit << i)) != 0)
                        {
                            if (effClass > 0)
                            {
                                PCores.Add(i);
                            }
                            else
                            {
                                ECores.Add(i);
                            }
                        }
                    }

                    offset += info.Size;
                    current = IntPtr.Add(current, info.Size);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        // Fallback: If no E-cores detected (AMD or older Intel), all are P-Cores
        if (ECores.Count == 0 && PCores.Count == 0)
        {
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                PCores.Add(i);
            }
        }
    }

    private void PreCalculateZones()
    {
        var pThreads = PCores;
        int pCount = pThreads.Count;

        _allCoresMask = CreateMask(PCores.Concat(ECores));

        // One-time logic to decide how to best divide the CPU into zones.
        if (pCount >= 8) // High: 2 threads each plus 2 threads free for OS, events, etc.
        {
            _logger.WithCallerInfo().Information("Using the High strategy with 8 or more performance threads.");
            _activeMask = CreateMask(pThreads.GetRange(0, 2));
            _nextMask = CreateMask(pThreads.GetRange(2, 2));
            _prevMask = CreateMask(pThreads.GetRange(4, 2));
            _backgroundMask = CreateMask(pThreads.Skip(6)); // We will override this later if we have access to E-Cores. But if we have no E-Cores then put the rest of the clients here in the background.
        }
        else if (pCount >= 4) // Mid: 1 thread each plus 1 thread free for OS, events, etc.
        {
            _logger.WithCallerInfo().Information("Using the Mid strategy with 4 or more performance threads.");
            _activeMask = CreateMask(pThreads.GetRange(0, 1));
            _nextMask = CreateMask(pThreads.GetRange(1, 1));
            _prevMask = CreateMask(pThreads.GetRange(2, 1));
            _backgroundMask = CreateMask(pThreads.Skip(3)); // We will override this later if we have access to E-Cores. But if we have no E-Cores then put the rest of the clients here in the background.

        }
        else // Low: Not enough threads available to be worth manually managing affinity.
        {
            _isOurCpuAbleToSupportAffinity = false;
            return;
        }

        if (ECores.Count > 0) // If we have E-Cores then use them for the background clients. Otherwise, just use the remaining cores.
        {
            _logger.WithCallerInfo().Information("Using E-Cores for all other background clients.");
            _backgroundMask = CreateMask(ECores);
        }

        _logger.WithCallerInfo().Verbose($"_activeMask = {_activeMask}, _nextMask = {_nextMask}, _prevMask = {_prevMask}, _backgroundMask = {_backgroundMask}");
    }
    
    private IntPtr CreateMask(IEnumerable<int> indices)
    {
        ulong mask = 0;
        foreach (int i in indices)
        {
            mask |= (SingleCoreBit << i);
        }

        return (IntPtr)mask;
    }
}