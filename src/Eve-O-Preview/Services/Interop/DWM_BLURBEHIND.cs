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

namespace EveOPreview.Services.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    class DWM_BLURBEHIND
    {
        public uint dwFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fEnable;
        public IntPtr hRegionBlur;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fTransitionOnMaximized;

        public const uint DWM_BB_ENABLE = 0x00000001;
        public const uint DWM_BB_BLURREGION = 0x00000002;
        public const uint DWM_BB_TRANSITIONONMAXIMIZED = 0x00000004;
    }
}