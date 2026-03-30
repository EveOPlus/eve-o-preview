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

using EveOPreview.Services;
using EveOPreview.Services.Implementation;
using EveOPreview.Services.Interop;
using System;
using System.Diagnostics;

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

            var kernelHandle = process.OpenKernelHandle();

            return new ProcessInfo(process.MainWindowHandle, kernelHandle, process.Id, process.MainWindowTitle);
        }

        public static IntPtr OpenKernelHandle(this Process process)
        {
            // Open a lightweight Kernel MainWindowHandle for Affinity/Priority
            // 0x0200 = PROCESS_SET_INFORMATION
            // 0x1000 = PROCESS_QUERY_LIMITED_INFORMATION
            IntPtr pHandle = KernelNativeMethods.OpenProcess(0x1200, false, process.Id);

            return pHandle;
        }

        public static void CloseKernelHandle(this IProcessInfo processInfo)
        {
            if (processInfo == null || processInfo.ProcessHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                KernelNativeMethods.CloseHandle(processInfo.ProcessHandle);
            }
            catch
            {
                // Nothing to do here, just don't crash anything else.
            }
        }
    }
}