using System;
using System.Diagnostics;
using System.IO;
using Cake.Common.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks
{
    [IsDependentOn(typeof(Build))]
    public sealed class ValidateWorkspace : FrostingTask<Context>
    {
        public override void Run(Context context)
        {
            // Copy only the executable into an empty directory. Adjacent build DLLs
            // must not hide a missing dependency in the distributable single file.
            var staging = Path.Combine(context.BinFolder, "workspace-validation");
            Directory.CreateDirectory(staging);
            var executable = Path.Combine(staging, "EVE-O Preview.exe");
            File.Copy(Path.Combine(context.BinFolder, "EVE-O Preview.exe"), executable, true);
            using var process = Process.Start(new ProcessStartInfo(executable, "--validate-workspace")
            {
                WorkingDirectory = staging,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill(entireProcessTree: true);
                throw new Exception("Published workspace validation timed out.");
            }
            var output = stdout.GetAwaiter().GetResult();
            var error = stderr.GetAwaiter().GetResult();
            context.Information(output);
            if (process.ExitCode != 0 || !output.Contains("Workspace startup validation passed"))
                throw new Exception("Published workspace failed to start: " + error);
        }
    }
}
