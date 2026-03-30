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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EveOPreview.Helper;

namespace EveOPreview.Services.Implementation
{
    sealed class ProcessMonitor : IProcessMonitor
    {
        #region Private constants
        private const string DEFAULT_PROCESS_NAME = "ExeFile";
        private const string CURRENT_PROCESS_NAME = "EVE-O Preview";
        #endregion

        #region Private fields
        private readonly object _lockObj = new object();
        public IDictionary<IntPtr, IProcessInfo> ProcessCache { get; }
        private IProcessInfo _currentProcessInfo;
        #endregion

        public ProcessMonitor()
        {
            this.ProcessCache = new Dictionary<IntPtr, IProcessInfo>(512);
            
            // This field cannot be initialized properly in constructor
            // At the moment this code is executed the main application window is not yet initialized
            this._currentProcessInfo = new ProcessInfo(IntPtr.Zero, IntPtr.Zero, 0, "");
        }

        private bool IsMonitoredProcess(string processName)
        {
            // This is a possible extension point
            return String.Equals(processName, ProcessMonitor.DEFAULT_PROCESS_NAME, StringComparison.OrdinalIgnoreCase);
        }

        private IProcessInfo GetCurrentProcessInfo()
        {
            var currentProcess = Process.GetCurrentProcess();
            return currentProcess.ToProcessInfo();
        }

        public IProcessInfo GetMainProcess()
        {
            if (this._currentProcessInfo.MainWindowHandle == IntPtr.Zero)
            {
                var processInfo = this.GetCurrentProcessInfo();

                // Are we initialized yet?
                if (processInfo.Title != "")
                {
                    this._currentProcessInfo = processInfo;
                }
            }

            return this._currentProcessInfo;
        }

        public ICollection<IProcessInfo> GetAllProcesses()
        {
            lock (_lockObj)
            {
                return this.ProcessCache.Values.ToList();
            }
            
            //ICollection<IProcessInfo> result = new List<IProcessInfo>(this._processCache.Count);
            //
            //// TODO Lock list here just in case
            //foreach (KeyValuePair<IntPtr, IProcessInfo> entry in this._processCache)
            //{
            //    result.Add(new ProcessInfo(entry.Key, entry.Value.ProcessId, entry.Value.Title));
            //}

            //return result;
        }

        public void GetUpdatedProcesses(out ICollection<IProcessInfo> addedProcesses, out ICollection<IProcessInfo> updatedProcesses, out ICollection<IProcessInfo> removedProcesses)
        {
            addedProcesses = new List<IProcessInfo>(16);
            updatedProcesses = new List<IProcessInfo>(16);
            removedProcesses = new List<IProcessInfo>(16);

            IList<IntPtr> knownProcesses = new List<IntPtr>(this.ProcessCache.Keys);
            foreach (Process process in Process.GetProcesses())
            {
                string processName = process.ProcessName;

                if (!this.IsMonitoredProcess(processName))
                {
                    continue;
                }

                IntPtr mainWindowHandle = process.MainWindowHandle;
                if (mainWindowHandle == IntPtr.Zero)
                {
                    continue; // No need to monitor non-visual processes
                }

                var processInfo = process.ToProcessInfo();
                this.ProcessCache.TryGetValue(mainWindowHandle, out IProcessInfo cachedProcess);

                if (cachedProcess?.Title == null)
                {
                    // This is a new process in the list
                    this.ProcessCache.Add(mainWindowHandle, processInfo);
                    addedProcesses.Add(processInfo);
                }
                else
                {
                    // This is an already known process
                    if (cachedProcess.Title != processInfo.Title)
                    {
                        this.ProcessCache[mainWindowHandle] = processInfo;
                        updatedProcesses.Add(new ProcessInfo(mainWindowHandle, processInfo.ProcessHandle, processInfo.ProcessId, processInfo.Title));
                    }

                    knownProcesses.Remove(mainWindowHandle);
                }
            }

            foreach (IntPtr index in knownProcesses)
            {
                var cachedProcess = this.ProcessCache[index];
                removedProcesses.Add(new ProcessInfo(index, cachedProcess.ProcessHandle, cachedProcess.ProcessId, cachedProcess.Title));
                this.ProcessCache.Remove(index);
            }

            foreach (var removedProcess in removedProcesses)
            {
                removedProcess.CloseKernelHandle();
            }
        }

        public IProcessInfo LookupCachedProcessByWindowHandle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return null;
            }

            ProcessCache.TryGetValue(windowHandle, out var procInfo);

            return procInfo;
        }
    }
}
