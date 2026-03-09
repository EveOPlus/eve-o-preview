//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EveOPreview.Helper;
using EveOPreview.Services.Interop;
using Serilog;

namespace EveOPreview.Services.Implementation
{
    public static class DebuggerSidecar
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct DEBUG_EVENT
        {
            [FieldOffset(0)] public uint dwDebugEventCode;
            [FieldOffset(4)] public uint dwProcessId;
            [FieldOffset(8)] public uint dwThreadId;
            // The union 'u' starts at offset 16 in 64-bit
            [FieldOffset(16)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 160)]
            public byte[] u;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OUTPUT_DEBUG_STRING_INFO
        {
            public IntPtr lpDebugStringData;
            public ushort fUnicode;
            public ushort nDebugStringLength;
        }

        private const uint DBG_CONTINUE = 0x00010002;
        private const uint DBG_EXCEPTION_NOT_HANDLED = 0x80010001;
        private const uint BreakpointHasBeenReachedErrorCode = 0x80000003;
        private const int PROCESS_VM_READ = 0x0010;

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
                Log.Logger.WithCallerInfo().Information("No debugger present. Launching debugger sidecar.");
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
            else
            {
                Log.Logger.WithCallerInfo().Information("A debugger is already present. Will not launch debugger sidecar.");
            }
        }

        private static void StartDebuggingLoop(uint pid)
        {
            KernelNativeMethods.OutputDebugString($"[Eve-O Sidecar] Attempting attach to PID: {pid}");
            
            if (KernelNativeMethods.DebugActiveProcess(pid))
            {
                KernelNativeMethods.DebugSetProcessKillOnExit(false);

                KernelNativeMethods.OutputDebugString($"[Eve-O Sidecar] Successfully attach to PID: {pid}");

                DEBUG_EVENT dbgEvent;
                // see https://learn.microsoft.com/en-us/windows/win32/api/debugapi/nf-debugapi-waitfordebugevent
                // and https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-debug_event
                while (KernelNativeMethods.WaitForDebugEvent(out dbgEvent, uint.MaxValue))
                {
                    uint continueStatus = DBG_CONTINUE;

                    //KernelNativeMethods.OutputDebugString($"[Eve-O Sidecar] Raw Event Code: {dbgEvent.dwDebugEventCode}");

                    switch (dbgEvent.dwDebugEventCode)
                    {
                        case 1: // EXCEPTION_DEBUG_EVENT
                            uint exceptionCode = BitConverter.ToUInt32(dbgEvent.u, 0);

                            try
                            {
                                IntPtr exceptionAddr = (IntPtr)BitConverter.ToInt64(dbgEvent.u, 16);
                                uint isFirstChance = BitConverter.ToUInt32(dbgEvent.u, 24); // 1 = First Chance, 0 = Second

                                string chance = (isFirstChance == 1) ? "First-Chance" : "Unhandled/Second-Chance";

                                // Log the details to DebugView
                                KernelNativeMethods.OutputDebugString(
                                    $"[Eve-O Sidecar] EXCEPTION: 0x{exceptionCode:X8} at 0x{exceptionAddr.ToInt64():X16} ({chance})"
                                );
                            }
                            catch
                            {
                            }

                            continueStatus = (exceptionCode == BreakpointHasBeenReachedErrorCode) ? DBG_CONTINUE : DBG_EXCEPTION_NOT_HANDLED;
                            break;
                        
                        case 3: // CREATE_PROCESS_DEBUG_EVENT
                            CloseHandleAtOffset(dbgEvent.u, 0); // hFile (at offset 0)
                            // DO NOT close hProcess or hThread (at offsets 8 and 16)
                            break;
                        case 6: // LOAD_DLL_DEBUG_EVENT
                            CloseHandleAtOffset(dbgEvent.u, 0); // hFile
                            break;
                        case 5: // EXIT_PROCESS_DEBUG_EVENT
                            KernelNativeMethods.OutputDebugString($"[Eve-O Sidecar] EXIT_PROCESS_DEBUG_EVENT received, closing debugger.");
                            KernelNativeMethods.ContinueDebugEvent(dbgEvent.dwProcessId, dbgEvent.dwThreadId, DBG_CONTINUE);
                            return;
                        case 8: // OUTPUT_DEBUG_STRING_EVENT
                            try
                            {
                                KernelNativeMethods.OutputDebugString("[Eve-O Sidecar] OUTPUT_DEBUG_STRING_EVENT received, forwarding through.");
                                IntPtr stringAddress = (IntPtr)BitConverter.ToInt64(dbgEvent.u, 0);
                                ushort isUnicode = BitConverter.ToUInt16(dbgEvent.u, 8);
                                ushort stringLen = BitConverter.ToUInt16(dbgEvent.u, 10);

                                if (stringLen > 0)
                                {
                                    // CAST dwProcessId to (int) to satisfy KernelNativeMethods.OpenProcess
                                    IntPtr hProcess = KernelNativeMethods.OpenProcess(PROCESS_VM_READ, false,
                                        (int)dbgEvent.dwProcessId);

                                    if (hProcess != IntPtr.Zero)
                                    {
                                        int byteCount = isUnicode != 0 ? stringLen * 2 : stringLen;
                                        byte[] buffer = new byte[byteCount];

                                        if (KernelNativeMethods.ReadProcessMemory(hProcess, stringAddress, buffer,
                                                buffer.Length, out _))
                                        {
                                            string message = (isUnicode != 0)
                                                ? System.Text.Encoding.Unicode.GetString(buffer)
                                                : System.Text.Encoding.ASCII.GetString(buffer);

                                            // This re-broadcasts it so Sysinternals DebugView can see it
                                            KernelNativeMethods.OutputDebugString(message.TrimEnd('\0'));
                                        }

                                        KernelNativeMethods.CloseHandle(hProcess);
                                    }
                                }
                            }
                            catch
                            {
                            }

                            continueStatus = DBG_CONTINUE;
                            break;
                        case 2: // CREATE_THREAD_DEBUG_EVENT
                        case 4: // EXIT_THREAD_DEBUG_EVENT
                        case 7: // UNLOAD_DLL_DEBUG_EVENT
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