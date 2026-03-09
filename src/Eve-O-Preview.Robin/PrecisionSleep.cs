// Eve-O-Preview.Robin is a companion program / sidekick (think Batman and Robin) to provide a native runtime for communicating with the client, such as permitting AllowSetForegroundWindow and limiting DirectX frames per minute.
// Copyright (C) 2026  Aura Asuna
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.using System.Diagnostics;

namespace EveOPreview.Robin;

using System.Runtime.InteropServices;

internal unsafe class PrecisionSleep
{
    private readonly IntPtr _timerHandle;
    private const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;
    private const int TIMER_MODIFY_STATE = 0x0002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr lpTimerAttributes, string? lpTimerName, uint dwFlags, uint dwDesiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimer(IntPtr hTimer, in long pDueTime, int lPeriod, IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    public PrecisionSleep()
    {
        _timerHandle = CreateWaitableTimerExW(IntPtr.Zero, null, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_MODIFY_STATE | 0x100000);
    }

    public void Sleep(double milliseconds)
    {
        if (milliseconds <= 0) return;

        // SetWaitableTimer expects time in 100-nanosecond intervals.
        long relativeTime = -(long)(milliseconds * 10000.0);

        if (SetWaitableTimer(_timerHandle, in relativeTime, 0, IntPtr.Zero, IntPtr.Zero, false))
        {
            WaitForSingleObject(_timerHandle, 0xFFFFFFFF);
        }
    }

    ~PrecisionSleep()
    {
        CloseHandle(_timerHandle);
    }
}