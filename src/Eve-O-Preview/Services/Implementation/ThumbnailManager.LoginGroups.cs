using System;
using System.Collections.Generic;
using System.Linq;

namespace EveOPreview.Services;

sealed partial class ThumbnailManager
{
    private void CycleGroupClients(bool forwards, SortedDictionary<int, string> order, bool includeLoginClients)
    {
        if (!includeLoginClients) { CycleNextClient(forwards, order); return; }
        if (_activationInProgress || _stopped) return;
        // Resolve from memory on each press. No discovery, launch-token reads or HTTP on the hotkey path.
        var named = _thumbnailViews.Values.Where(v => v.Title != DEFAULT_CLIENT_TITLE).GroupBy(v => v.Title, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
        var targets = ResolveLoginGroup(order.Values, named,
            _processMonitor.GetAllProcesses().Where(p => _thumbnailViews.ContainsKey(p.MainWindowHandle)),
            title => _identities?.GetCachedCharacterUserId(title), p => _identities?.GetProcessUserId(p));
        if (!forwards) Array.Reverse(targets);
        int current = Array.FindIndex(targets, target => target.Handle == _activeClient.Handle && target.Handle != IntPtr.Zero);
        if (current < 0) current = Array.FindIndex(targets, target => target.Title == _activeClient.Title);
        int next = FindNextGroupTarget(targets, current, _configuration.IsClientCycleSkipped);
        if (next < 0) return;
        int predicted = FindNextGroupTarget(targets, next, _configuration.IsClientCycleSkipped);
        ActivateClient(_thumbnailViews[targets[next].Handle], predicted == next || predicted < 0 ? IntPtr.Zero : targets[predicted].Handle,
            _activeClient.Handle, saveLayouts: false);
    }

    internal static (string Title, IntPtr Handle)[] ResolveLoginGroup(IEnumerable<string> order,
        IReadOnlyDictionary<string, IntPtr> named, IEnumerable<IProcessInfo> processes,
        Func<string, long?> characterAccount, Func<IProcessInfo, long?> processAccount)
    {
        // Multiple login processes for one account cannot be assigned to a particular character.
        // Leave those to the dedicated login shortcut until discovery makes the match unique.
        var loginByAccount = processes.Where(p => p.IsLoginClient)
            .Select(p => (Process: p, Account: processAccount(p))).Where(p => p.Account is > 0)
            .GroupBy(p => p.Account.Value).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single().Process.MainWindowHandle);
        // Deduplicate the source window, including repeated titles and different characters
        // on one account, before reversing traversal or finding the predicted next client.
        var used = new HashSet<IntPtr>();
        return order.Select(title =>
        {
            IntPtr handle = named.GetValueOrDefault(title);
            if (handle == IntPtr.Zero && characterAccount(title) is long account && account > 0)
                loginByAccount.TryGetValue(account, out handle);
            if (handle != IntPtr.Zero && !used.Add(handle)) handle = IntPtr.Zero;
            return (title, handle);
        }).ToArray();
    }

    internal static int FindNextGroupTarget((string Title, IntPtr Handle)[] targets, int current, Func<string, bool> skipped)
    {
        for (int step = 1; step <= targets.Length; step++)
        {
            int index = (current + step) % targets.Length;
            if (targets[index].Handle != IntPtr.Zero && !skipped(targets[index].Title)) return index;
        }
        return -1;
    }
}
