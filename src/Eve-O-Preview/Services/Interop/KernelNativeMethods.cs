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
using System.Runtime.InteropServices;
using static EveOPreview.Services.Implementation.DebuggerSidecar;

namespace EveOPreview.Services.Interop
{
    public static partial class KernelNativeMethods
    {
        public const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        public const uint MEM_COMMIT_RESERVE = 0x3000;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;
        
        [DllImport("kernel32.dll")]
        public static extern bool DebugActiveProcess(uint dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool WaitForDebugEvent(out DEBUG_EVENT lpDebugEvent, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, uint dwContinueStatus);

        [DllImport("kernel32.dll")]
        public static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isPresent);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern bool DebugSetProcessKillOnExit(bool killOnExit);

        [DllImport("kernel32.dll")]
        public static extern int GetProcessId(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern void OutputDebugString(string lpOutputString);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        public static void OutputDebug(string message)
        {
            OutputDebugString($"[EveOPreview] {message}");
        }

        public static void OutputDebug(string message, IntPtr targetClientHandle)
        {
            OutputDebug($"[EveClient_{targetClientHandle}] {message}");
        }

        #region GetLogicalProcessorInformationEx Structures

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP relationship, IntPtr buffer, ref uint returnedLength);

        public enum LOGICAL_PROCESSOR_RELATIONSHIP
        {
            RelationProcessorCore = 0
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
            public int Size;
            public PROCESSOR_RELATIONSHIP Processor;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSOR_RELATIONSHIP
        {
            public byte Flags;
            public byte EfficiencyClass;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Reserved;
            public GROUP_MASK GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GROUP_MASK
        {
            public UIntPtr Mask;
            public ushort Group;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public ushort[] Reserved;
        }

        #endregion

        #region Set Process Affinity

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);
        
        public const uint HIGH_PRIORITY_CLASS = 0x00000080;
        public const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;

        #endregion
    }
}