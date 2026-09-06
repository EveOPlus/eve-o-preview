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

using System.Diagnostics;

namespace EveOPreview.Robin;

internal static class Global
{
    // We're going to assume that the MainWindowHandle is the one we care about.
    // This may not be true for every game, but it should hold true most the time, and it should do what I need for now...
    internal static IntPtr ThisClientsHandle = Process.GetCurrentProcess().MainWindowHandle;

    internal static volatile int OwnerProcessId = -1;
    private static readonly object OwnerLock = new();
    private static IntPtr _ownerHandle;

    internal static void ClaimOwnership(int pid)
    {
        if (pid <= 0) throw new InvalidDataException("An owner PID must be positive.");
        IntPtr handle = NativeMethods.OpenProcess(0x100000, false, pid); // SYNCHRONIZE pins this process identity.
        if (handle == IntPtr.Zero) throw new InvalidDataException("Owner process is unavailable.");
        lock (OwnerLock)
        {
            if (_ownerHandle != IntPtr.Zero) NativeMethods.CloseHandle(_ownerHandle);
            _ownerHandle = handle;
            OwnerProcessId = pid;
        }
    }

    internal static void StartOwnerWatchdog() => _ = Task.Run(async () =>
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            lock (OwnerLock)
            {
                if (_ownerHandle == IntPtr.Zero || NativeMethods.WaitForSingleObject(_ownerHandle, 0) == 258) continue;
                NativeMethods.CloseHandle(_ownerHandle);
                _ownerHandle = IntPtr.Zero;
                OwnerProcessId = 0;
                DxHook.SetFpsTargets(0, 0, 0);
                AudioMuteSystem.ClearMutedIds();
            }
        }
    });

}