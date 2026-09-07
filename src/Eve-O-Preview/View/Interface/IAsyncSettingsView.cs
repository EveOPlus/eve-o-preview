using System;
using System.Threading.Tasks;

namespace EveOPreview.View;

// Optional awaitable path lets a modern view report save failures and pending work.
public interface IAsyncSettingsView
{
    Func<Task> CommitSettingsAsync { get; set; }
    Func<Task> CommitSizeAsync { get; set; }
}
