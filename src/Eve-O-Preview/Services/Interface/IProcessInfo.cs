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

namespace EveOPreview.Services
{
    public interface IProcessInfo
    {
        /// <summary>
        /// Window handle (HWND) for the main window of the Eve Client.
        /// </summary>
        public IntPtr MainWindowHandle { get; }

        /// <summary>
        /// New Kernel MainWindowHandle that we open for process-level operations like CPU Affinity and Priority.
        /// </summary>
        public IntPtr ProcessHandle { get; }
        string Title { get; }
        int ProcessId { get; }
    }
}