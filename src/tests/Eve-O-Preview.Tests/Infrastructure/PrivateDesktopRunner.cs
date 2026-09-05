using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace EveOPreview.Tests.Infrastructure;

internal static class PrivateDesktopRunner
{
    public static async Task RunAsync(string scenario, ITestOutputHelper output)
    {
        string resultPath = Path.GetTempFileName();
        IntPtr desktop = IntPtr.Zero;
        try
        {
            string name = "EveOPreviewTests-" + Guid.NewGuid().ToString("N");
            desktop = Native.CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x01ff, IntPtr.Zero);
            if (desktop == IntPtr.Zero) throw new Win32Exception();
            var startup = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfo>(), Desktop = name };
            // Use this assembly's apphost, not the IDE/testhost/dotnet process hosting xUnit.
            string executable = Path.ChangeExtension(typeof(Program).Assembly.Location, ".exe");
            var command = new StringBuilder($"\"{executable}\" --private-desktop \"{scenario}\" \"{resultPath}\"");
            // No console: the worker writes a log which becomes this xUnit test's output.
            const uint createNoWindow = 0x08000000;
            if (!Native.CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero,
                false, createNoWindow, IntPtr.Zero, null, ref startup, out var process)) throw new Win32Exception();
            try
            {
                var (wait, error) = await Task.Run(() =>
                {
                    uint result = Native.WaitForSingleObject(process.Process, 30000);
                    return (result, Marshal.GetLastWin32Error());
                });
                if (wait != 0)
                {
                    Native.TerminateProcess(process.Process, 1);
                    Native.WaitForSingleObject(process.Process, 5000);
                    output.WriteLine(File.ReadAllText(resultPath));
                    if (wait == 0x102) throw new TimeoutException($"Private-desktop test '{scenario}' timed out after 30 seconds.");
                    throw new Win32Exception(error);
                }
                if (!Native.GetExitCodeProcess(process.Process, out uint code)) throw new Win32Exception();
                string result = File.ReadAllText(resultPath);
                output.WriteLine(result);
                Assert.True(code == 0, $"Private-desktop test '{scenario}' exited with code {code}.\n{result}");
            }
            finally { Native.CloseHandle(process.Thread); Native.CloseHandle(process.Process); }
        }
        finally
        {
            if (desktop != IntPtr.Zero) Native.CloseDesktop(desktop);
            File.Delete(resultPath);
        }
    }
}
