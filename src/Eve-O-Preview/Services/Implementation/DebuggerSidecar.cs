using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EveOPreview.Services.Interop;

namespace EveOPreview.Services.Implementation
{
    public static class DebuggerSidecar
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct DEBUG_EVENT
        {
            public uint dwDebugEventCode;
            public uint dwProcessId;
            public uint dwThreadId;
            public uint dwPadding;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 160)]
            public byte[] u;
        }

        private const uint DBG_CONTINUE = 0x00010002;
        private const uint DBG_EXCEPTION_NOT_HANDLED = 0x80010001;
        private const uint BreakpointHasBeenReachedErrorCode = 0x80000003;

        public static void RunAsTheSideCar(string[] args)
        {
            uint targetPid = uint.Parse(args[1]);
            StartDebuggingLoop(targetPid);
            Environment.Exit(0);
        }

        public static void LaunchTheSideCar()
        {
            bool isDebuggerPresent = false;
            KernelNativeMethods.CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isDebuggerPresent);

            if (!isDebuggerPresent && !Debugger.IsAttached)
            {
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                int myPid = Process.GetCurrentProcess().Id;

                ProcessStartInfo newProcess = new ProcessStartInfo();
                newProcess.FileName = currentExe;
                newProcess.Arguments = $"--attach-debug-sidecar {myPid}";
                newProcess.CreateNoWindow = true;
                newProcess.WindowStyle = ProcessWindowStyle.Hidden;
                newProcess.UseShellExecute = false;

                Process.Start(newProcess);
            }
        }

        private static void StartDebuggingLoop(uint pid)
        {
            if (KernelNativeMethods.DebugActiveProcess(pid))
            {
                KernelNativeMethods.DebugSetProcessKillOnExit(false);

                DEBUG_EVENT dbgEvent;
                // see https://learn.microsoft.com/en-us/windows/win32/api/debugapi/nf-debugapi-waitfordebugevent
                // and https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-debug_event
                while (KernelNativeMethods.WaitForDebugEvent(out dbgEvent, uint.MaxValue))
                {
                    uint continueStatus = DBG_CONTINUE;

                    switch (dbgEvent.dwDebugEventCode)
                    {
                        case 1: // EXCEPTION_DEBUG_EVENT
                            uint excCode = BitConverter.ToUInt32(dbgEvent.u, 0);
                            continueStatus = (excCode == BreakpointHasBeenReachedErrorCode) ? DBG_CONTINUE : DBG_EXCEPTION_NOT_HANDLED;
                            break;
                        
                        case 3: // CREATE_PROCESS_DEBUG_EVENT
                            CloseHandleAtOffset(dbgEvent.u, 0); // hFile (at offset 0)
                            // DO NOT close hProcess or hThread (at offsets 8 and 16)
                            break;
                        case 6: // LOAD_DLL_DEBUG_EVENT
                            CloseHandleAtOffset(dbgEvent.u, 0); // hFile
                            break;
                        case 5: // EXIT_PROCESS_DEBUG_EVENT
                            // The main app closes. So close the debugger too.
                            KernelNativeMethods.ContinueDebugEvent(dbgEvent.dwProcessId, dbgEvent.dwThreadId, DBG_CONTINUE);
                            return;
                        case 2: // CREATE_THREAD_DEBUG_EVENT
                        case 4: // EXIT_THREAD_DEBUG_EVENT
                        case 7: // UNLOAD_DLL_DEBUG_EVENT
                        case 8: // OUTPUT_DEBUG_STRING_EVENT
                        case 9: // RIP_EVENT
                            break;
                    }

                    KernelNativeMethods.ContinueDebugEvent(dbgEvent.dwProcessId, dbgEvent.dwThreadId, continueStatus);
                }
            }
        }

        private static void CloseHandleAtOffset(byte[] u, int offset)
        {
            long val = BitConverter.ToInt64(u, offset);
            IntPtr h = new IntPtr(val);
            if (h != IntPtr.Zero && h != new IntPtr(-1))
            {
                KernelNativeMethods.CloseHandle(h);
            }
        }
    }
}