using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.Services.Implementation;

// CPU-set IDs are opaque. Core/cache/node indices are relative to a processor group.
internal sealed record CpuSet(uint Id, ushort Group, byte LogicalProcessor, byte Core,
    byte LastLevelCache, byte NumaNode, byte EfficiencyClass);

internal readonly record struct CpuLocality(ushort Group, byte Node, byte Cache);
internal sealed record CpuPlacement(CpuLocality Home, uint[] Active, uint[] Warm, uint[] Background);

internal static class CpuPlacementPolicy
{
    internal static CpuPlacement Create(IReadOnlyList<CpuSet> eligible,
        IEnumerable<CpuLocality> existingHomes, CpuLocality? previousHome = null)
    {
        if (eligible.Count == 0) return null;
        // Classify the physical core first: some Windows versions can report
        // different capacity hints for its primary and SMT logical processors.
        var cores = eligible.GroupBy(c => (c.Group, c.Core)).Select(g => new
        {
            g.Key, Capacity = g.Max(c => c.EfficiencyClass), Sets = g.ToArray(),
            Home = new CpuLocality(g.Key.Group, g.First().NumaNode, g.First().LastLevelCache)
        }).ToArray();
        byte performanceClass = cores.Max(c => c.Capacity);
        var homes = cores.Where(c => c.Capacity == performanceClass).GroupBy(c => c.Home).ToArray();
        var occupancy = existingHomes.GroupBy(h => h).ToDictionary(g => g.Key, g => g.Count());
        // Stable placement avoids moving a client's working set between CCDs/nodes
        // on each focus change. This does not migrate already-allocated NUMA memory.
        var home = homes.FirstOrDefault(g => g.Key == previousHome) ?? homes
            .OrderBy(g => (double)occupancy.GetValueOrDefault(g.Key) / g.Count())
            .ThenByDescending(g => g.Count())
            .ThenBy(g => g.Key.Group).ThenBy(g => g.Key.Node).ThenBy(g => g.Key.Cache).First();
        var node = cores.Where(c => c.Home.Group == home.Key.Group && c.Home.Node == home.Key.Node).ToArray();
        var performance = node.Where(c => c.Capacity == performanceClass)
            .OrderByDescending(c => c.Home.Cache == home.Key.Cache)
            .ThenBy(c => c.Key.Core).ToArray();
        // Prefer one cache domain, but never manufacture a one-core active pool
        // when more physical performance cores exist in the same NUMA node.
        int localCores = home.Count();
        int poolSize = Math.Min(performance.Length, Math.Max(4, localCores));
        var pool = performance.Take(poolSize).ToArray();
        var efficiency = node.Where(c => c.Capacity < performanceClass).SelectMany(c => c.Sets).ToArray();
        uint[] Ids(IEnumerable<CpuSet> sets) => sets.Select(c => c.Id).Order().ToArray();
        if (efficiency.Length > 0)
        {
            // All lower capacity classes participate (including three-tier CPUs).
            int activeCores = pool.Length >= 4 ? pool.Length - 2 : pool.Length;
            var active = Ids(pool.Take(activeCores).SelectMany(c => c.Sets));
            var warm = pool.Length > activeCores ? Ids(pool.Skip(activeCores).SelectMany(c => c.Sets)) : active;
            return new(home.Key, active, warm, Ids(efficiency));
        }
        if (pool.Length <= 4)
        {
            var shared = Ids(pool.SelectMany(c => c.Sets));
            return new(home.Key, shared, shared, shared);
        }
        // Homogeneous CPUs need a useful background pool too. Predicted/previous
        // clients share the foreground half instead of reserving two more zones.
        int foregroundCores = (pool.Length + 1) / 2;
        var foreground = Ids(pool.Take(foregroundCores).SelectMany(c => c.Sets));
        return new(home.Key, foreground, foreground, Ids(pool.Skip(foregroundCores).SelectMany(c => c.Sets)));
    }
}
