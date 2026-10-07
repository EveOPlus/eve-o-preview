using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Input;
using EveOPreview.Mediator.Messages;
using EveOPreview.Mediator.Messages.Process;
using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interface;
using EveOPreview.Tests.Infrastructure;
using EveOPreview.View;
using MediatR;
using Newtonsoft.Json;
using Serilog;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class LoginApplicationTests
{
    [Fact]
    public void LoginOrderUsesNumericAccountThenPidWithPidFallbackAndExecutableClassification()
    {
        IProcessInfo[] clients = [new TestProcessInfo(30, "Eve"), new TestProcessInfo(10, "EVE"),
            new TestProcessInfo(20, "EVE"), new TestProcessInfo(5, "EVE"),
            new TestProcessInfo(1, "EVE - Named"), new TestProcessInfo(2, "EVE", "notepad")];
        var accounts = new Dictionary<int, long?> { [30] = 100, [20] = 100, [10] = 200 };
        Assert.Equal(new[] { 5, 20, 30, 10 }, ThumbnailManager.OrderLoginClients(clients, p => accounts.GetValueOrDefault(p.ProcessId)).Select(p => p.ProcessId));
        Assert.Equal(new[] { 5, 10, 20, 30 }, ThumbnailManager.OrderLoginClients(clients, _ => null).Select(p => p.ProcessId));
        IProcessInfo external = new TestProcessInfo(2, "EVE - Named", "notepad");
        Assert.False(external.IsEveClient);
        Assert.Equal("notepad (2)", external.PreviewTitle);
        Assert.Equal("EVE - Named", clients[4].PreviewTitle);
    }

    [Fact]
    public void LinkedLoginSlotsPreserveCharacterOrderAndNeverGuessMissingOrAmbiguousAccounts()
    {
        var titles = new[] { "EVE - Pilot Two", "EVE - Pilot One", "EVE - Alt One", "EVE - Unknown", "EVE - Named" };
        var accounts = new Dictionary<string, long?> { [titles[0]] = 200, [titles[1]] = 100, [titles[2]] = 100, [titles[4]] = 300 };
        IProcessInfo[] processes = [new TestProcessInfo(10, "EVE"), new TestProcessInfo(20, "EVE"),
            new TestProcessInfo(30, "EVE"), new TestProcessInfo(99, "EVE", "notepad")];
        var named = new Dictionary<string, IntPtr> { [titles[4]] = new(50) };
        var targets = ThumbnailManager.ResolveLoginGroup(titles, named, processes, title => accounts.GetValueOrDefault(title), p => p.ProcessId * 10);
        Assert.Equal(new IntPtr[] { 20, 10, 0, 0, 50 }, targets.Select(t => t.Handle));
        Assert.Equal(titles, targets.Select(t => t.Title));
        Assert.Equal(0, ThumbnailManager.FindNextGroupTarget(targets, -1, _ => false));
        Assert.Equal(1, ThumbnailManager.FindNextGroupTarget(targets, 0, _ => false));
        Assert.Equal(4, ThumbnailManager.FindNextGroupTarget(targets, 1, _ => false));
        Assert.Equal(0, ThumbnailManager.FindNextGroupTarget(targets, 4, _ => false));
        Assert.Equal(4, ThumbnailManager.FindNextGroupTarget(targets, 1, t => t == titles[1]));
        Assert.Equal(-1, ThumbnailManager.FindNextGroupTarget(targets, 1, _ => true));
        Array.Reverse(targets);
        Assert.Equal(4, ThumbnailManager.FindNextGroupTarget(targets, 3, _ => false));
        // A second login process on the same account makes character/process matching ambiguous.
        var ambiguous = ThumbnailManager.ResolveLoginGroup(titles, named, processes.Append(new TestProcessInfo(11, "EVE")),
            title => accounts.GetValueOrDefault(title), p => p.ProcessId == 11 ? 100 : p.ProcessId * 10);
        Assert.Equal(IntPtr.Zero, ambiguous[1].Handle); Assert.Equal(IntPtr.Zero, ambiguous[2].Handle);
        var missing = ThumbnailManager.ResolveLoginGroup(titles, named, processes, title => accounts.GetValueOrDefault(title), _ => null);
        Assert.Equal(new IntPtr[] { 0, 0, 0, 0, 50 }, missing.Select(t => t.Handle));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ThreeEntriesOnOneAccountVisitLoginWindowOncePerCycle(bool differentCharacters, bool backwards)
    {
        string first = "EVE - Pilot One";
        var order = new[] { first, "EVE - Other Account", differentCharacters ? "EVE - Alt Two" : first,
            "EVE - Third Account", differentCharacters ? "EVE - Alt Three" : first };
        IProcessInfo[] processes = [new TestProcessInfo(10, "EVE"), new TestProcessInfo(20, "EVE"), new TestProcessInfo(30, "EVE")];
        var named = new Dictionary<string, IntPtr>();
        var visited = new List<IntPtr>();
        var predictions = new List<IntPtr>();
        IntPtr active = IntPtr.Zero;
        for (int press = 0; press < 6; press++)
        {
            // Match the hotkey path: resolve afresh and recover the slot from the active HWND.
            var targets = ThumbnailManager.ResolveLoginGroup(order, named, processes,
                title => title == order[1] ? 200 : title == order[3] ? 300 : 100, p => p.ProcessId * 10);
            Assert.Equal(order, targets.Select(t => t.Title));
            Assert.Single(targets, t => t.Handle == new IntPtr(10));
            if (backwards) Array.Reverse(targets);
            int current = Array.FindIndex(targets, t => t.Handle == active && active != IntPtr.Zero);
            int next = ThumbnailManager.FindNextGroupTarget(targets, current, _ => false);
            active = targets[next].Handle;
            visited.Add(active);
            predictions.Add(targets[ThumbnailManager.FindNextGroupTarget(targets, next, _ => false)].Handle);
        }
        int[] cycle = backwards ? [30, 20, 10] : [10, 20, 30];
        Assert.Equal(cycle.Concat(cycle).Select(id => new IntPtr(id)), visited);
        Assert.Equal(visited.Skip(1).Append(visited[0]), predictions);
    }

    [Fact]
    public void NewProfileFieldsRoundTripAndOldProfilesResetThem()
    {
        var configuration = new ThumbnailConfiguration { CycleLoginClientsHotkey = "Control + Alt + L", PreviewApplications = ["notepad", "calc"] };
        configuration.CycleGroups.Add(new() { IncludeLoginClients = true, ClientsOrder = new() { [7] = "EVE - Pilot One" } });
        var loaded = JsonConvert.DeserializeObject<ThumbnailConfiguration>(JsonConvert.SerializeObject(configuration));
        Assert.True(loaded.CycleGroups.Last().IncludeLoginClients);
        Assert.False(JsonConvert.DeserializeObject<CycleGroup>("{\"Description\":\"Old profile\"}").IncludeLoginClients);
        Assert.Equal(configuration.CycleLoginClientsHotkey, loaded.CycleLoginClientsHotkey);
        Assert.Equal(configuration.PreviewApplications, loaded.PreviewApplications);
        var oldProfile = JsonConvert.DeserializeObject<ThumbnailConfiguration>("{}");
        JsonConvert.PopulateObject(JsonConvert.SerializeObject(oldProfile), configuration);
        Assert.Empty(configuration.PreviewApplications);
        Assert.Empty(configuration.CycleLoginClientsHotkey);
    }

    [Fact]
    public async Task LoginShortcutSharesConflictChecksWithExistingBindings()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var config = new ThumbnailConfiguration { CycleLoginClientsHotkey = "Control + Alt + L" };
        var keys = Stub.Create<IHotkeyService>((method, _) => method.Name == "CaptureAsync"
            ? Task.FromResult(config.CycleLoginClientsHotkey) : Stub.Default(method.ReturnType));
        var handler = new EveOPreview.Mediator.Handlers.Configuration.CaptureNewHotkeyHandler(keys, config, logger);
        var conflict = await handler.Handle(new CaptureNewHotkey("", 1000), TestContext.Current.CancellationToken);
        Assert.False(conflict.IsValid);
        var unchanged = await handler.Handle(new CaptureNewHotkey(config.CycleLoginClientsHotkey, 1000), TestContext.Current.CancellationToken);
        Assert.True(unchanged.IsValid);
    }

    [Fact]
    public async Task EveOnlyHandlersExcludeOtherExecutablesEvenWithEveTitles()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        var config = new ThumbnailConfiguration();
        IProcessInfo eve = new TestProcessInfo(10, "EVE"), other = new TestProcessInfo(20, "EVE - Spoof", "notepad");
        var monitor = Stub.Create<IProcessMonitor>((method, args) => method.Name switch
        {
            "GetAllProcesses" => new List<IProcessInfo> { eve, other },
            "LookupCachedProcessByWindowHandle" => (IntPtr)args[0] == eve.MainWindowHandle ? eve : other,
            _ => Stub.Default(method.ReturnType)
        });
        var touched = new List<IntPtr>();
        var hooks = Stub.Create<IHookService>((method, args) =>
        {
            if (method.Name == "TryInstallHooksAsync") touched.Add(((IProcessInfo)args[0]).MainWindowHandle);
            if (method.Name == "DisableFpsLimiterAsync") touched.Add((IntPtr)args[0]);
            return Stub.Default(method.ReturnType);
        });
        await new EveOPreview.Mediator.Handlers.Configuration.SetAudioSettingsHandler(monitor, hooks, logger).Handle(new SetAudioSettings(), CancellationToken.None);
        Assert.Equal(new[] { eve.MainWindowHandle }, touched);
        touched.Clear();
        config.FpsLimiterSettings.IsEnabled = false;
        await new EveOPreview.Mediator.Handlers.Configuration.SetFpsLimiterEnabledHandler(config, monitor, hooks, logger).Handle(new SetFpsLimiterEnabled(), CancellationToken.None);
        Assert.Equal(new[] { eve.MainWindowHandle }, touched);
        var cpu = Stub.Create<ICpuAffinityService>((method, args) =>
        {
            Assert.Equal("UpdateAffinity", method.Name);
            Assert.Null(args[0]); Assert.Same(eve, args[1]); Assert.Null(args[2]);
            Assert.Equal(new[] { eve }, (IEnumerable<IProcessInfo>)args[3]);
            return null;
        });
        await new EveOPreview.Mediator.Handlers.Process.UpdateCpuAffinityHandler(logger, monitor, cpu)
            .Handle(new UpdateCpuAffinity(other.MainWindowHandle, eve.MainWindowHandle, other.MainWindowHandle), CancellationToken.None);
        // A non-EVE target must return before attempting a pipe or resolving an executable.
        await new HookService(config, logger).TryInstallHooksAsync(other);
    }

    internal static void CheckLifecycle()
    {
        TestAvalonia.Initialize();
        using var logger = new LoggerConfiguration().CreateLogger();
        var config = new ThumbnailConfiguration { CycleLoginClientsHotkey = "Control + Alt + L" };
        var clients = new List<IProcessInfo> { new TestProcessInfo(20, "EVE"), new TestProcessInfo(10, "EVE"),
            new TestProcessInfo(30, "Same title", "notepad"), new TestProcessInfo(40, "Same title", "notepad") };
        var pending = clients.ToList(); var updated = new List<IProcessInfo>(); var removed = new List<IProcessInfo>();
        using var identities = new LoginIdentities(logger, clients);
        var group = new CycleGroup { ClientsOrder = new() { [4] = "EVE - Pilot Two", [9] = "EVE - Pilot One" },
            ForwardHotkeys = ["Control + Alt + F"], BackwardHotkeys = ["Control + Alt + B"] };
        config.CycleGroups.Add(group);
        var monitor = Stub.Create<IProcessMonitor>((method, args) =>
        {
            if (method.Name == "GetAllProcesses") return clients.ToList();
            if (method.Name == "LookupCachedProcessByWindowHandle") return clients.FirstOrDefault(p => p.MainWindowHandle == (IntPtr)args[0]);
            if (method.Name == "GetUpdatedProcesses")
            {
                args[0] = pending.ToList(); args[1] = updated.ToList(); args[2] = removed.ToList();
                pending.Clear(); updated.Clear(); removed.Clear();
            }
            return Stub.Default(method.ReturnType);
        });
        var activated = new List<IntPtr>();
        IntPtr predicted = IntPtr.Zero;
        var windows = Stub.Create<IWindowManager>((method, args) =>
        {
            if (method.Name == "ActivateWindow") activated.Add((IntPtr)args[0]);
            if (method.Name == "PredictUpcomingClient") predicted = (IntPtr)args[0];
            return Stub.Default(method.ReturnType);
        });
        var factory = Stub.Create<IThumbnailViewFactory>((method, args) =>
        {
            IntPtr id = (IntPtr)args[0]; string title = (string)args[1];
            return Stub.Create<IThumbnailView>((operation, values) =>
            {
                if (operation.Name == "set_Title") title = (string)values[0];
                return operation.Name switch { "get_Id" => id, "get_Title" => title, _ => Stub.Default(operation.ReturnType) };
            });
        });
        IReadOnlyList<HotkeyBinding> bindings = [];
        var keys = Stub.Create<IHotkeyService>((method, args) =>
        {
            if (method.Name == "Replace") bindings = ((IEnumerable<HotkeyBinding>)args[0]).ToArray();
            return Stub.Default(method.ReturnType);
        });
        var hooked = new List<int>();
        var hooks = Stub.Create<IHookService>((method, args) =>
        {
            if (method.Name == "TryInstallHooksAsync") hooked.Add(((IProcessInfo)args[0]).ProcessId);
            return Stub.Default(method.ReturnType);
        });
        using var manager = new ThumbnailManager(Stub.Create<IMediator>(), config, monitor, windows, factory, keys, hooks, Stub.Create<IGlobalEvents>(), logger,
            null, null, identities.Cache);
        void Discover() => typeof(ThumbnailManager).GetMethod("UpdateThumbnailsList", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        Discover();
        Assert.Equal(new[] { 20, 10 }, hooked);
        Assert.Equal(4, manager.GetAllKnownClients().Count);
        var external = manager.GetClientByPointer(new IntPtr(30));
        Assert.NotEqual(external.Title, manager.GetClientByPointer(new IntPtr(40)).Title);
        void GroupKey(bool forward) => Assert.Single(bindings, b => b.Shortcut == (forward ? group.ForwardHotkeys[0] : group.BackwardHotkeys[0])).Execute();
        GroupKey(true); Assert.Empty(activated); // Opt-in is required.
        group.IncludeLoginClients = true; manager.RegisterAllHotkeys();
        GroupKey(true); Assert.Equal(new IntPtr(20), activated.Last()); Assert.Equal(new IntPtr(10), predicted);
        GroupKey(true); Assert.Equal(new IntPtr(10), activated.Last()); Assert.Equal(new IntPtr(20), predicted);
        GroupKey(false); Assert.Equal(new IntPtr(20), activated.Last());
        var loginView = manager.GetClientByPointer(new IntPtr(10));
        clients[1] = new TestProcessInfo(10, "EVE - Pilot One"); updated.Add(clients[1]); Discover();
        GroupKey(true); Assert.Equal(new IntPtr(10), activated.Last()); Assert.Same(loginView, manager.GetActiveClient());
        GroupKey(true); Assert.Equal(new IntPtr(20), activated.Last());
        clients[1] = new TestProcessInfo(10, "EVE"); updated.Add(clients[1]); Discover();
        GroupKey(true); Assert.Equal(new IntPtr(10), activated.Last()); Assert.Same(loginView, manager.GetActiveClient());
        config.SetClientCycleSkipped("EVE - Pilot Two", true);
        GroupKey(true); Assert.Equal(new IntPtr(10), activated.Last());
        config.SetClientCycleSkipped("EVE - Pilot Two", false);
        group.IncludeLoginClients = false; manager.RegisterAllHotkeys();
        int groupCount = activated.Count; GroupKey(true); Assert.Equal(groupCount, activated.Count);
        manager.SetActive(new(external.Id, external)); activated.Clear();
        var loginBinding = Assert.Single(bindings, b => b.Shortcut == config.CycleLoginClientsHotkey);
        loginBinding.Execute(); manager.CycleLoginClients(); manager.CycleLoginClients();
        Assert.Equal(new IntPtr[] { 10, 20, 10 }, activated);
        clients[1] = new TestProcessInfo(10, "EVE - Logged in"); updated.Add(clients[1]);
        clients[2] = new TestProcessInfo(30, "New document title", "notepad"); updated.Add(clients[2]); Discover();
        Assert.Same(external, manager.GetClientByPointer(new IntPtr(30)));
        Assert.Equal("notepad (30)", external.Title);
        manager.CycleLoginClients(); Assert.Equal(new IntPtr(20), activated.Last());
        removed.Add(clients[0]); clients.RemoveAt(0); Discover();
        int count = activated.Count; manager.CycleLoginClients(); Assert.Equal(count, activated.Count);
        clients[0] = new TestProcessInfo(10, "Eve"); updated.Add(clients[0]); Discover();
        manager.CycleLoginClients(); Assert.Equal(new IntPtr(10), activated.Last());
    }

    private sealed class LoginIdentities : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "eveo-login-group-" + Guid.NewGuid().ToString("N"));
        private readonly HttpClient _http = new();
        public CharacterIdentityCache Cache { get; }
        public LoginIdentities(ILogger logger, ICollection<IProcessInfo> clients)
        {
            Directory.CreateDirectory(_root);
            string path = Path.Combine(_root, "Characters.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new[] {
                new CharacterIdentityCache.KnownCharacter("Pilot One", 90000001, 100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                new CharacterIdentityCache.KnownCharacter("Pilot Two", 90000002, 200, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) }));
            Cache = new(path, _http, (pid, _) => pid == 10 ? 100 : 200, logger, () => DateTimeOffset.UtcNow);
            Cache.ObserveProcesses(clients);
            Assert.True(SpinWait.SpinUntil(() => clients.Where(p => p.IsLoginClient).All(p => Cache.GetProcessUserId(p).HasValue), 3000));
            Assert.Equal(100L, Cache.GetCachedCharacterUserId("EVE - Pilot One"));
        }
        public void Dispose() { Cache.Dispose(); _http.Dispose(); Directory.Delete(_root, true); }
    }
}
