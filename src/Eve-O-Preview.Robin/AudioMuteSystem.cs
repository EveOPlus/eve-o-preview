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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static EveOPreview.Robin.DebugLogger;
using static EveOPreview.Robin.NativeMethods;

namespace EveOPreview.Robin;

internal static unsafe class AudioMuteSystem
{
    private const int PAGE_EXECUTE_READWRITE = 0x40;
    
    private const uint STATUS_GUARD_PAGE_VIOLATION = 0x80000001;
    private const uint STATUS_SINGLE_STEP = 0x80000004;
    private const uint TRAP_FLAG = 0x100;
    private const uint PAGE_GUARD = 0x100;

    private const int EXCEPTION_CONTINUE_EXECUTION = -1;
    private const int EXCEPTION_CONTINUE_SEARCH = 0;

    private const ulong DR7_ENABLE_L0 = 0x1; // Bit 0: Enables Local Breakpoint 0 (Dr0)
    private const ulong DR6_DETECT_B0 = 0x1; // Bit 0: Detected Breakpoint 0 Condition

    private static delegate* unmanaged[Cdecl]<int, uint, int, int, void> _executeAction;

    private static readonly uint[] _mutedIds = new uint[1024];
    private static int _mutedCount = 0;
    private static readonly Lock _muteLock = new();

    private static IntPtr _postEventAddr;
    private static IntPtr _vehHandle;

    internal static bool IsAudioMutingEnabled => _mutedCount > 0;

    internal static void InstallAudioMonitor()
    {
        var audio2Module = NativeMethods.GetModuleHandle("_audio2.dll");

        _postEventAddr = NativeMethods.GetProcAddress(audio2Module,
            "?PostEvent@SoundEngine@AK@@YAII_KIP6AXW4AkCallbackType@@PEAUAkCallbackInfo@@@ZPEAXIPEAUAkExternalSourceInfo@@I@Z");

        if (_postEventAddr == IntPtr.Zero)
        {
            Error($"[{nameof(InstallAudioMonitor)}] Could not resolve PostEvent");
            return;
        }

        _vehHandle = NativeMethods.AddVectoredExceptionHandler(1, &VectoredHandler);
        if (_vehHandle == IntPtr.Zero)
        {
            return;
        }

        SetBreakpointWhenPostEventAddressIsRead();

        IntPtr executeActionOnPlayingIDAddr = NativeMethods.GetProcAddress(audio2Module, "?ExecuteActionOnPlayingID@SoundEngine@AK@@YAXW4AkActionOnEventType@12@IHW4AkCurveInterpolation@@@Z");
        _executeAction = (delegate* unmanaged[Cdecl]<int, uint, int, int, void>)executeActionOnPlayingIDAddr;
        if (_executeAction == null)
        {
            Error($"[{nameof(InstallAudioMonitor)}] Could not resolve ExecuteActionOnPlayingID");
        }
        else
        {
            Info("Located ExecuteActionOnPlayingID");
        }
    }

    private static void SetBreakpointWhenPostEventAddressIsRead()
    {
        if (IsAudioMutingEnabled)
        {
            uint oldProtect;
            NativeMethods.VirtualProtect(_postEventAddr, 1, PAGE_EXECUTE_READWRITE | PAGE_GUARD, out oldProtect);
        }
    }

    [ThreadStatic] private static uint _currentEventId;
    [ThreadStatic] private static ulong _currentGameObjectId;
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    public static unsafe int VectoredHandler(EXCEPTION_POINTERS* pExp)
    {
        var code = pExp->ExceptionRecord->ExceptionCode;
        var ctx = pExp->ContextRecord;

        if (code == STATUS_GUARD_PAGE_VIOLATION)
        {
            if (ctx->Rip == (ulong)_postEventAddr)
            {
                // Keep track of the eventId for when we hit the response breakpoint.
                _currentEventId = (uint)ctx->Rcx;
                _currentGameObjectId = (uint)ctx->Rdx;

                if (ctx->Rsp != 0)
                {
                    // Set return breakpoint so we can capture the response playingId later.
                    ctx->Dr0 = *(ulong*)ctx->Rsp;
                    ctx->Dr7 |= DR7_ENABLE_L0;
                }
            }

            ctx->EFlags |= TRAP_FLAG; // Force Single Step to Re-arm
            return EXCEPTION_CONTINUE_EXECUTION;
        }

        if (code == STATUS_SINGLE_STEP)
        {
            // Check if this specific step was our Hardware Breakpoint (Return)
            if ((ctx->Dr6 & DR6_DETECT_B0) != 0)
            {
                uint playingId = (uint)ctx->Rax;

                // Cleanup HW registers
                ctx->Dr0 = 0;
                ctx->Dr7 &= ~DR7_ENABLE_L0; // Disable L0
                ctx->Dr6 &= ~DR6_DETECT_B0; // Acknowledge/Clear B0 status

                if (IsMuted(_currentEventId) && playingId != 0)
                {
                    _executeAction((int)AkActionOnEventType.Stop, playingId, 0, (int)AkCurveInterpolation.Constant);
                    Info($"Audio Stop Actioned on PlayingID: {playingId} EventID: {_currentEventId}");
                }
#if DEBUG
                AudioLog.Add(_currentEventId, _currentGameObjectId);
#endif
            }

            // Always re-arm the Page Guard on EVERY single step
            SetBreakpointWhenPostEventAddressIsRead();

            return EXCEPTION_CONTINUE_EXECUTION;
        }

        // Not our exception, let the next handler try
        return EXCEPTION_CONTINUE_SEARCH;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMuted(uint eventId)
    {
        if (_mutedCount == 0)
        {
            return false;
        }

        return Array.BinarySearch(_mutedIds, 0, _mutedCount, eventId) >= 0;
    }

    internal static void AddMutedId(uint id)
    {
        lock (_muteLock)
        {
            var isZeroAtStart = _mutedCount == 0;
            if (_mutedCount >= _mutedIds.Length || IsMuted(id))
            {
                return;
            }

            _mutedIds[_mutedCount++] = id;
            Array.Sort(_mutedIds, 0, _mutedCount);

            if (isZeroAtStart)
            {
                // If it was zero before adding new values then we almost certainly have nothing monitoring.
                SetBreakpointWhenPostEventAddressIsRead();
            }
        }
    }

    internal static void RemoveMutedId(uint id)
    {
        lock (_muteLock)
        {
            int index = Array.BinarySearch(_mutedIds, 0, _mutedCount, id);
            if (index < 0)
            {
                return;
            }

            // Shift elements left to fill the gap (Native-speed move)
            if (index < _mutedCount - 1)
            {
                Array.Copy(_mutedIds, index + 1, _mutedIds, index, _mutedCount - index - 1);
            }

            _mutedCount--;
            _mutedIds[_mutedCount] = 0; // Clear stale entry
        }
    }

    internal static void ClearMutedIds()
    {
        lock (_muteLock)
        {
            Array.Clear(_mutedIds, 0, _mutedIds.Length);
            _mutedCount = 0;
        }
    }

    internal static List<uint> GetMutedIds()
    {
        lock (_muteLock)
        {
            return _mutedIds.Take(_mutedCount).ToList();
        }
    }

    internal enum AkActionOnEventType : int
    {
        Stop = 1,
        Pause = 2,
        Resume = 3,
        Break = 4,
        ReleaseEnvelope = 5,
        Mute = 6,
        Unmute = 7
    }

    internal enum AkCurveInterpolation : int
    {
        Log3 = 0, // Logarithmic (Curving slowly at first, then fast)
        Sine = 1, // Sine wave (Smooth start and end)
        Log1 = 2, // Logarithmic (Faster initial drop than Log3)
        InvSCurve = 3, // Inversed S-Curve
        Linear = 4, // Linear (Default straight-line transition)
        SCurve = 5, // S-Curve (Smooth transition)
        Exp1 = 6, // Exponential (Slow drop, then accelerates)
        SineRecip = 7, // Reciprocal of a sine curve
        Exp3 = 8, // Exponential (Steepest acceleration)
        Constant = 9  // Constant (Instant jump, no interpolation)
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct LoggedAudioEvent
{
    public uint EventID;
    public ulong GameObjectID;
    public long Timestamp;
}

internal static unsafe class AudioLog
{
    private const int LogSize = 128;
    private const int LogMask = LogSize - 1;
    private static readonly LoggedAudioEvent[] _eventHistory = new LoggedAudioEvent[LogSize];
    private static int _globalSequenceCount = 0;

    internal static void Add(uint eventID, ulong gameObjectID)
    {
        try
        {
            int sequence = Interlocked.Increment(ref _globalSequenceCount);
            int index = sequence & LogMask;

            ref var entry = ref _eventHistory[index];
            entry.EventID = eventID;
            entry.GameObjectID = gameObjectID;
            entry.Timestamp = Stopwatch.GetTimestamp();
        }
        catch (Exception ex)
        {
            Error(ex, $"{nameof(AudioLog)}.{nameof(Add)}");
        }
    }

    public static List<LoggedAudioEvent> GetOrderedEventHistory()
    {
        return _eventHistory
            .Where(e => e.EventID != 0)
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    public static void ClearEventHistory()
    {
        Interlocked.Exchange(ref _globalSequenceCount, 0);
        Array.Clear(_eventHistory, 0, LogSize);
    }
}