using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EveOPreview.UI;
using EveOPreview.Mediator.Messages;

namespace EveOPreview.View;

public sealed partial class WindowsWorkspaceBackend
{
    private async Task<CommandResult> EditPreviewApplicationAsync(WorkspaceCommand command)
    {
        bool add = command.Action == "application-add";
        if (add && !(await GetPreviewApplicationsAsync()).Any(p => p.ProcessName.Equals(command.Target, StringComparison.OrdinalIgnoreCase)))
            return CommandResult.Error("Select a running application with a window.");
        var before = _configuration.PreviewApplications;
        var selected = (before ?? new()).ToList();
        selected.RemoveAll(p => p.Equals(command.Target, StringComparison.OrdinalIgnoreCase));
        if (add) selected.Add(command.Target);
        _configuration.PreviewApplications = selected;
        try { await _mediator.Send(new SaveConfiguration()); }
        catch { _configuration.PreviewApplications = before; throw; }
        return CommandResult.Ok(add ? "Application thumbnails enabled" : "Application thumbnails removed");
    }

    public Task<IReadOnlyList<PreviewApplication>> GetPreviewApplicationsAsync() => Task.Run<IReadOnlyList<PreviewApplication>>(() =>
    {
        var applications = new List<PreviewApplication>();
        foreach (var process in Process.GetProcesses())
        using (process)
        {
            try
            {
                if (process.Id == Environment.ProcessId || process.MainWindowHandle == IntPtr.Zero
                    || process.ProcessName.Equals("ExeFile", StringComparison.OrdinalIgnoreCase)) continue;
                applications.Add(new(process.ProcessName, process.MainWindowTitle, process.Id));
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
        }
        return applications.OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.ProcessId).ToArray();
    });
}
