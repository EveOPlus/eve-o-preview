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
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EveOPreview.Services.Implementation;

public sealed class CpuAffinityService : ICpuAffinityService, IDisposable
{
    private readonly ILogger _logger;
    private readonly IThumbnailConfiguration _config;
    private readonly ICpuSetApi _api;
    private readonly Lock _lock = new();
    private readonly Timer _topologyTimer;
    private readonly Dictionary<(int Pid, IntPtr Handle), PlacementState> _states = [];
    private CpuSet[] _topology = [];
    private bool _stopped;

    private sealed class PlacementState(CpuSetConstraints constraints)
    {
        internal readonly CpuSetConstraints Constraints = constraints;
        internal CpuSet[] Topology;
        internal CpuPlacement Placement;
        internal uint[] Applied;
        internal bool FailureLogged;
    }

    public CpuAffinityService(ILogger logger, IThumbnailConfiguration config)
        : this(logger, config, new WindowsCpuSetApi(), true) { }

    internal CpuAffinityService(ILogger logger, IThumbnailConfiguration config, ICpuSetApi api, bool refreshTopology = false)
    {
        _logger = logger;
        _config = config;
        _api = api;
        RefreshTopology();
        // Discovery never runs in a focus callback. Publish immutable snapshots.
        if (refreshTopology)
            _topologyTimer = new Timer(_ => { if (_config.EnableAutomaticCpuAffinity) RefreshTopology(); }, null, 10000, 10000);
    }

    internal void RefreshTopology()
    {
        try
        {
            var topology = _api.ReadTopology();
            if (!topology.SequenceEqual(Volatile.Read(ref _topology)))
            {
                Volatile.Write(ref _topology, topology);
                _logger.Information("CPU placement topology: {Logical} logical processors, {Physical} physical cores, {Groups} groups, {Nodes} NUMA domains, capacity classes {Classes}",
                    topology.Length, topology.Select(c => (c.Group, c.Core)).Distinct().Count(),
                    topology.Select(c => c.Group).Distinct().Count(), topology.Select(c => (c.Group, c.NumaNode)).Distinct().Count(),
                    string.Join(",", topology.Select(c => c.EfficiencyClass).Distinct().Order()));
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or EntryPointNotFoundException or InvalidOperationException)
        {
            // Initial discovery failure leaves scheduling entirely to Windows.
            _logger.Warning(ex, "CPU-set topology unavailable; retaining the existing scheduling policy");
        }
    }

    public void UpdateAffinity(IProcessInfo active, IProcessInfo next, IProcessInfo prev, IEnumerable<IProcessInfo> allClients)
    {
        lock (_lock)
        {
            if (_stopped || !_config.EnableAutomaticCpuAffinity) return;
            var clients = allClients.Where(p => p != null).GroupBy(p => p.ProcessId).Select(g => g.First()).ToArray();
            var live = clients.Select(p => (p.ProcessId, p.ProcessHandle)).ToHashSet();
            foreach (var key in _states.Keys.Where(k => !live.Contains(k)).ToArray()) _states.Remove(key);
            active ??= next ?? prev;
            if (next?.ProcessId == active?.ProcessId) next = null;
            if (prev?.ProcessId == active?.ProcessId || prev?.ProcessId == next?.ProcessId) prev = null;
            var topology = Volatile.Read(ref _topology);
            if (topology.Length == 0) return;
            // Expand the foreground pool first. Never change hard affinity or priority.
            foreach (var client in clients.OrderByDescending(p => p.ProcessId == active?.ProcessId))
            {
                WithHandle(client, handle =>
                {
                    var key = (client.ProcessId, client.ProcessHandle);
                    if (!_states.TryGetValue(key, out var state))
                    {
                        if (!_api.TryReadConstraints(handle, out var constraints)) return false;
                        state = new(constraints);
                        _states.Add(key, state);
                    }
                    if (state.Topology != topology)
                    {
                        var eligible = topology.Where(state.Constraints.Allows).ToArray();
                        state.Placement = CpuPlacementPolicy.Create(eligible,
                            _states.Where(s => s.Key != key && s.Value.Placement != null).Select(s => s.Value.Placement.Home), state.Placement?.Home);
                        state.Topology = topology;
                        if (state.Placement != null)
                            _logger.Debug("CPU placement PID {Pid}: group {Group}, NUMA {Node}, cache {Cache}; active {Active}, warm {Warm}, background {Background} logical processors",
                                client.ProcessId, state.Placement.Home.Group, state.Placement.Home.Node, state.Placement.Home.Cache,
                                state.Placement.Active.Length, state.Placement.Warm.Length, state.Placement.Background.Length);
                    }
                    // An empty plan must not accidentally clear the user's CPU sets.
                    uint[] desired = state.Placement == null ? state.Constraints.OriginalSets
                        : client.ProcessId == active?.ProcessId ? state.Placement.Active
                        : client.ProcessId == next?.ProcessId || client.ProcessId == prev?.ProcessId ? state.Placement.Warm
                        : state.Placement.Background;
                    if (state.Applied != null && desired.SequenceEqual(state.Applied)) return true;
                    if (!_api.TrySet(handle, desired))
                    {
                        if (!state.FailureLogged) _logger.Warning("CPU-set assignment failed for PID {Pid}; will retry", client.ProcessId);
                        state.FailureLogged = true;
                        return false;
                    }
                    state.Applied = desired;
                    state.FailureLogged = false;
                    return true;
                });
            }
        }
    }

    public void ResetAll(IEnumerable<IProcessInfo> allClients)
    {
        lock (_lock)
        {
            foreach (var client in allClients.Where(p => p != null))
            {
                var key = (client.ProcessId, client.ProcessHandle);
                if (_states.TryGetValue(key, out var state))
                {
                    if (WithHandle(client, handle => _api.TrySet(handle, state.Constraints.OriginalSets))) _states.Remove(key);
                    else
                    {
                        state.Applied = null;
                        _logger.Warning("Restoring original CPU sets failed for PID {Pid}; restore remains pending", client.ProcessId);
                    }
                }
            }
        }
    }

    public void Stop(IEnumerable<IProcessInfo> allClients)
    {
        lock (_lock)
        {
            _stopped = true;
            _topologyTimer?.Dispose();
            ResetAll(allClients);
        }
    }

    public void Dispose() => _topologyTimer?.Dispose();

    private bool WithHandle(IProcessInfo process, Func<IntPtr, bool> operation)
    {
        var owned = (process as ProcessInfo)?.OwnedHandle;
        bool addedRef = false;
        try
        {
            owned?.DangerousAddRef(ref addedRef);
            IntPtr handle = process.ProcessHandle;
            return handle != IntPtr.Zero && _api.IsProcess(handle, process.ProcessId) && operation(handle);
        }
        catch (ObjectDisposedException) { return false; }
        finally { if (addedRef) owned.DangerousRelease(); }
    }
}
