using System;
using EveOPreview.Services;

namespace EveOPreview.Tests.Infrastructure;

internal sealed record TestProcessInfo(int ProcessId, string Title, string ProcessName = "ExeFile") : IProcessInfo
{
    public IntPtr MainWindowHandle { get; init; } = new(ProcessId);
    public IntPtr ProcessHandle { get; init; }
}
