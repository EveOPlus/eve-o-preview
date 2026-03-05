using System;

namespace EveOPreview.Services.Implementation
{
    sealed class ProcessInfo : IProcessInfo
    {
        public ProcessInfo(IntPtr handle, int processId, string title)
        {
            this.Handle = handle;
            this.ProcessId = processId;
            this.Title = title;
        }

        public IntPtr Handle { get; }
        public string Title { get; }
        public int ProcessId { get; }
    }
}