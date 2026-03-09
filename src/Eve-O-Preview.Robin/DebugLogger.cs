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

internal static class DebugLogger
{
    private static readonly IntPtr MainHandle = Process.GetCurrentProcess().MainWindowHandle;
    public static void Info(string message)
    {
        NativeMethods.OutputDebugString($"[EVE-O HOOK] [{MainHandle}] [INFO] {message}");
    }

    public static void Error(string message)
    {
        NativeMethods.OutputDebugString($"[EVE-O HOOK] [{MainHandle}] [ERROR] {message}");
    }

    public static void Error(Exception ex)
    {
        Error(ex.ToString());
    }

    public static void Error(Exception ex, string message)
    {
        Error($"{message}: {ex}");
    }
}