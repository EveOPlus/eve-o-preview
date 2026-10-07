using System;
using System.Collections.Generic;
using System.Linq;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class CpuPlacementTests
{
    private static CpuSet[] Hybrid(int performance, int efficiency, bool smt)
    {
        var result = new List<CpuSet>();
        for (byte core = 0; core < performance + efficiency; core++)
            for (int thread = 0; thread < (core < performance && smt ? 2 : 1); thread++)
                result.Add(new((uint)(100 + result.Count), 0, (byte)result.Count, core, 0, 0, (byte)(core < performance ? 1 : 0)));
        return result.ToArray();
    }

    [Fact]
    public void AlderLakeKeepsFourPhysicalPCoresWithBothSmtThreads()
    {
        var cpus = Hybrid(6, 8, true);
        var plan = CpuPlacementPolicy.Create(cpus, []);
        Assert.Equal(8, plan.Active.Length);
        Assert.Equal(4, plan.Warm.Length);
        Assert.Equal(8, plan.Background.Length);
        Assert.Equal(4, cpus.Where(c => plan.Active.Contains(c.Id)).Select(c => c.Core).Distinct().Count());
        Assert.Empty(plan.Active.Intersect(plan.Warm));
        Assert.Empty(plan.Active.Intersect(plan.Background));
    }

    [Fact]
    public void SmtCapacityHintsCannotPutForegroundSiblingsIntoBackgroundPool()
    {
        var cpus = Hybrid(6, 8, true).Select(c => c with
        {
            EfficiencyClass = (byte)(c.Core < 6 ? c.LogicalProcessor % 2 == 0 ? 2 : 1 : 0)
        }).ToArray();
        var plan = CpuPlacementPolicy.Create(cpus, []);
        Assert.Equal(8, plan.Active.Length);
        Assert.All(cpus.Where(c => plan.Background.Contains(c.Id)), c => Assert.True(c.Core >= 6));
        foreach (var core in cpus.GroupBy(c => c.Core))
            Assert.True(core.All(c => plan.Active.Contains(c.Id)) || core.All(c => plan.Warm.Contains(c.Id)) || core.All(c => plan.Background.Contains(c.Id)));
    }

    [Fact]
    public void ArrowLakeDoesNotAssumeAdjacentIdsAreSmtOrPerformanceCores()
    {
        byte[] p = [0, 1, 10, 11, 12, 13, 22, 23];
        var cpus = Enumerable.Range(0, 24).Select(i => new CpuSet((uint)(100 + i), 0, (byte)i, (byte)i, 0, 0, (byte)(p.Contains((byte)i) ? 1 : 0))).ToArray();
        var plan = CpuPlacementPolicy.Create(cpus, []);
        Assert.Equal(new uint[] { 100, 101, 110, 111, 112, 113 }, plan.Active);
        Assert.Equal(new uint[] { 122, 123 }, plan.Warm);
        Assert.Equal(16, plan.Background.Length);
    }

    [Fact]
    public void ThreeCapacityClassesUseAllLowerClassesForBackground()
    {
        var cpus = Hybrid(6, 8, true).Select(c => c with { EfficiencyClass = (byte)(c.EfficiencyClass == 1 ? 2 : c.Core < 10 ? 1 : 0) }).ToArray();
        var plan = CpuPlacementPolicy.Create(cpus, []);
        Assert.Equal(new byte[] { 0, 1 }, cpus.Where(c => plan.Background.Contains(c.Id)).Select(c => c.EfficiencyClass).Distinct().Order());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void SmallHomogeneousProcessorsShareCoresInsteadOfSplittingSmt(int cores)
    {
        var cpus = Hybrid(cores, 0, true);
        var plan = CpuPlacementPolicy.Create(cpus, []);
        Assert.Equal(cores * 2, plan.Active.Length);
        Assert.Equal(plan.Active, plan.Warm);
        Assert.Equal(plan.Active, plan.Background);
    }

    [Fact]
    public void HomogeneousEightCoreCpuKeepsUsefulForegroundAndBackgroundPools()
    {
        var cpus = Hybrid(8, 0, true);
        var plan = CpuPlacementPolicy.Create(cpus, []);
        Assert.Equal(8, plan.Active.Length);
        Assert.Equal(8, plan.Background.Length);
        foreach (var core in cpus.GroupBy(c => c.Core))
            Assert.True(core.All(c => plan.Active.Contains(c.Id)) || core.All(c => plan.Background.Contains(c.Id)));
    }

    [Fact]
    public void CacheHomesAreBalancedAndRemainStableAcrossReplanning()
    {
        var cpus = Hybrid(16, 0, true).Select(c => c with { LastLevelCache = (byte)(c.Core / 8) }).ToArray();
        var first = CpuPlacementPolicy.Create(cpus, []);
        var second = CpuPlacementPolicy.Create(cpus, [first.Home]);
        Assert.NotEqual(first.Home, second.Home);
        Assert.All(first.Active.Concat(first.Background), id => Assert.Equal(first.Home.Cache, cpus.Single(c => c.Id == id).LastLevelCache));
        var again = CpuPlacementPolicy.Create(cpus, [first.Home, first.Home], first.Home);
        Assert.Equal(first.Home, again.Home);
    }

    [Fact]
    public void MoreThan64ProcessorsUseCpuSetIdsAndGroupRelativeLocality()
    {
        var cpus = Enumerable.Range(0, 80).Select(i => new CpuSet((uint)(100 + i), (ushort)(i / 40), (byte)(i % 40), (byte)(i % 40), 0, 0, 0)).ToArray();
        var first = CpuPlacementPolicy.Create(cpus, []);
        var second = CpuPlacementPolicy.Create(cpus, [first.Home]);
        Assert.Equal((ushort)1, second.Home.Group);
        Assert.All(second.Active.Concat(second.Background), id => Assert.InRange(id, 140u, 179u));
    }

    [Fact]
    public void ExplicitSetsAndHardAffinityAreIntersectedWithoutInventingCores()
    {
        var cpus = Hybrid(6, 8, true);
        var constraint = new CpuSetConstraints([100, 101, 102, 103], [0], 0b1100);
        var plan = CpuPlacementPolicy.Create(cpus.Where(constraint.Allows).ToArray(), []);
        Assert.Equal(new uint[] { 102, 103 }, plan.Active);
        Assert.Equal(plan.Active, plan.Background);
        Assert.Null(CpuPlacementPolicy.Create([], []));
        Assert.True(new CpuSetConstraints([], [1], 1UL << 63).Allows(new CpuSet(500, 1, 63, 40, 0, 0, 0)));
    }

    [Fact]
    public void ForegroundIsAppliedFirstAliasesAreDeduplicatedAndWarmRolesPersist()
    {
        var api = new FakeApi();
        using var service = Service(api);
        var a = new Client(1); var b = new Client(2); var c = new Client(3);
        service.UpdateAffinity(a, b, a, [b, c, a, new Client(1)]);
        Assert.Equal(new[] { 1, 2, 3 }, api.Writes.Select(w => w.Pid));
        Assert.Equal(8, api.Writes[0].Ids.Length);
        Assert.Equal(4, api.Writes[1].Ids.Length);
        api.Writes.Clear();
        for (int tick = 0; tick < 1000; tick++) service.UpdateAffinity(a, b, a, [a, b, c]);
        Assert.Empty(api.Writes);
        service.UpdateAffinity(a, null, null, [a, b, c]);
        Assert.Equal(2, Assert.Single(api.Writes).Pid);
        Assert.Equal(8, api.Writes[0].Ids.Length);
        Assert.Equal(1, api.TopologyReads);
        Assert.Equal(3, api.ConstraintReads);
    }

    [Fact]
    public void NewClientsAndRapidRoleChangesUseCachedTopology()
    {
        var api = new FakeApi();
        using var service = Service(api);
        var a = new Client(1); var b = new Client(2);
        service.UpdateAffinity(a, null, null, [a]);
        api.Writes.Clear();
        service.UpdateAffinity(b, null, a, [a, b]);
        Assert.Equal(new[] { 2, 1 }, api.Writes.Select(w => w.Pid));
        Assert.Equal(8, api.Writes[0].Ids.Length);
        Assert.Equal(1, api.TopologyReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestorePreservesEmptyOrExplicitCpuSetsAndStopIsTerminal(bool explicitSets)
    {
        var api = new FakeApi { Original = explicitSets ? [100, 101, 102, 103] : [] };
        using var service = Service(api);
        var client = new Client(1);
        service.UpdateAffinity(client, null, null, [client]);
        service.ResetAll([client]);
        Assert.Equal(api.Original, api.Writes.Last().Ids);
        service.UpdateAffinity(client, null, null, [client]);
        service.Stop([client]);
        int count = api.Writes.Count;
        service.UpdateAffinity(client, null, null, [client]);
        Assert.Equal(count, api.Writes.Count);
        Assert.Equal(api.Original, api.Writes.Last().Ids);
    }

    [Fact]
    public void FailedAssignmentsAndRestorationRemainRetryable()
    {
        var api = new FakeApi { FailWrites = true };
        using var service = Service(api);
        var client = new Client(1);
        service.UpdateAffinity(client, null, null, [client]);
        service.UpdateAffinity(client, null, null, [client]);
        Assert.Equal(2, api.Writes.Count);
        api.FailWrites = false;
        service.UpdateAffinity(client, null, null, [client]);
        api.FailWrites = true;
        service.ResetAll([client]);
        api.FailWrites = false;
        service.ResetAll([client]);
        Assert.Equal(5, api.Writes.Count);
        Assert.Empty(api.Writes.Last().Ids);
    }

    [Fact]
    public void DisabledUnsupportedOrStaleHandleMakesNoAssignments()
    {
        var api = new FakeApi { ValidHandle = false };
        var config = new ThumbnailConfiguration { EnableAutomaticCpuAffinity = false };
        using var service = new CpuAffinityService(new LoggerConfiguration().CreateLogger(), config, api);
        var client = new Client(1);
        service.UpdateAffinity(client, null, null, [client]);
        Assert.Equal(0, api.ConstraintReads);
        config.EnableAutomaticCpuAffinity = true;
        service.UpdateAffinity(client, null, null, [client]);
        Assert.Empty(api.Writes);
        api.ValidHandle = true;
        api.Topology = [];
        service.RefreshTopology();
        service.UpdateAffinity(client, null, null, [client]);
        Assert.Empty(api.Writes);
    }

    [Fact]
    public void TopologyRefreshReplansOnlyAfterItPublishesAChange()
    {
        var api = new FakeApi();
        using var service = Service(api);
        var client = new Client(1);
        service.UpdateAffinity(client, null, null, [client]);
        api.Topology = Hybrid(8, 16, false);
        service.RefreshTopology();
        service.UpdateAffinity(client, null, null, [client]);
        Assert.Equal(6, api.Writes.Last().Ids.Length);
        Assert.Equal(1, api.ConstraintReads);
    }

    [Fact]
    public void NativeParserSkipsFutureRecordsAndAllocatedSetsButRetainsParkedCpus()
    {
        var data = new byte[104];
        BitConverter.GetBytes(8u).CopyTo(data, 0);
        BitConverter.GetBytes(9u).CopyTo(data, 4);
        for (int i = 0; i < 3; i++)
        {
            int offset = 8 + i * 32;
            BitConverter.GetBytes(32u).CopyTo(data, offset);
            BitConverter.GetBytes((uint)(100 + i)).CopyTo(data, offset + 8);
            data[offset + 14] = (byte)i;
            data[offset + 15] = (byte)(i / 2);
            data[offset + 19] = (byte)(i == 0 ? 1 : i == 1 ? 2 : 0);
        }
        Assert.Equal(new uint[] { 100, 102 }, WindowsCpuSetApi.ParseTopology(data).Select(c => c.Id));
        Assert.Throws<InvalidOperationException>(() => WindowsCpuSetApi.ParseTopology(data.AsSpan(0, 50)));
        BitConverter.GetBytes(0u).CopyTo(data, 8);
        Assert.Throws<InvalidOperationException>(() => WindowsCpuSetApi.ParseTopology(data));
    }

    private static CpuAffinityService Service(FakeApi api) =>
        new(new LoggerConfiguration().CreateLogger(), new ThumbnailConfiguration { EnableAutomaticCpuAffinity = true }, api);

    private sealed record Client(int ProcessId) : IProcessInfo
    {
        public IntPtr MainWindowHandle => (IntPtr)(ProcessId + 1000);
        public IntPtr ProcessHandle => (IntPtr)ProcessId;
        public string Title => "EVE test";
    }

    private sealed class FakeApi : ICpuSetApi
    {
        internal CpuSet[] Topology = Hybrid(6, 8, true);
        internal uint[] Original = [];
        internal int TopologyReads, ConstraintReads;
        internal bool FailWrites, ValidHandle = true;
        internal readonly List<(int Pid, uint[] Ids)> Writes = [];
        public CpuSet[] ReadTopology() { TopologyReads++; return Topology; }
        public bool IsProcess(IntPtr handle, int processId) => ValidHandle && handle == (IntPtr)processId;
        public bool TryReadConstraints(IntPtr handle, out CpuSetConstraints constraints)
        {
            ConstraintReads++;
            constraints = new(Original, [0], ulong.MaxValue);
            return true;
        }
        public bool TrySet(IntPtr handle, uint[] ids) { Writes.Add(((int)handle, ids.ToArray())); return !FailWrites; }
    }
}
