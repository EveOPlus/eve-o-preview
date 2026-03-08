using Serilog;
using System.IO;
using System.Runtime.CompilerServices;

namespace EveOPreview.Helper
{
    public static class LoggerHelpers
    {
        public static ILogger WithCallerInfo(this ILogger logger, [CallerMemberName] string member = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return logger
                .ForContext("Class", Path.GetFileNameWithoutExtension(file))
                .ForContext("Method", member)
                .ForContext("Line", line);
        }
    }
}