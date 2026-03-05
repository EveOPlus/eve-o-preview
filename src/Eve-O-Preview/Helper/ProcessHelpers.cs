using EveOPreview.Services;
using System.Diagnostics;
using EveOPreview.Services.Implementation;

namespace EveOPreview.Helper
{
    public static class ProcessHelpers
    {
        public static IProcessInfo ToProcessInfo(this Process process)
        {
            if (process == null)
            {
                return null;
            }
            
            return new ProcessInfo(process.MainWindowHandle, process.Id, process.MainWindowTitle);
        }
    }
}