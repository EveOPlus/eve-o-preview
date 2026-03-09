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
using System.Drawing;

namespace EveOPreview.Services
{
    public interface IWindowManager
    {
        bool IsCompositionEnabled { get; }
        bool IsCurrentlySwitching { get; set; }

        IntPtr GetForegroundWindowHandle();
        void ActivateWindow(IntPtr handle);
        void MinimizeWindow(IntPtr handle, bool enableAnimation);
        void MoveWindow(IntPtr handle, int left, int top, int width, int height);
        void MaximizeWindow(IntPtr handle);
        (int Left, int Top, int Right, int Bottom) GetWindowPosition(IntPtr handle);
        bool IsWindowMaximized(IntPtr handle);
        bool IsWindowMinimized(IntPtr handle);
        IDwmThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source);
        Image GetStaticThumbnail(IntPtr source);
        void PredictUpcomingClient(IntPtr upcomingHandle);
    }
}