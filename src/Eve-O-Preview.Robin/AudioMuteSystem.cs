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

    internal const int MaxMutedIds = 1024;
    private static uint[] _mutedIds = [];
    private static readonly Lock _muteLock = new();
    private static IntPtr _postEventAddr;
    private static IntPtr _vehHandle;
    private static ulong _pageStart;
    private static uint _originalProtection;
    private static volatile bool _monitorReady;
    private static volatile bool _captureDiagnostics;

    internal static bool IsAudioMutingEnabled => Volatile.Read(ref _mutedIds).Length > 0;
    internal static bool IsMonitorReady => _monitorReady;
    private static bool IsMonitoringEnabled => IsAudioMutingEnabled || _captureDiagnostics;

    internal static void InstallAudioMonitor()
    {
        var module = NativeMethods.GetModuleHandle("_audio2.dll");
        if (module == IntPtr.Zero) return; // The DirectX mock has no EVE audio engine.
        var postEvent = NativeMethods.GetProcAddress(module,
            "?PostEvent@SoundEngine@AK@@YAII_KIP6AXW4AkCallbackType@@PEAUAkCallbackInfo@@@ZPEAXIPEAUAkExternalSourceInfo@@I@Z");
        var action = NativeMethods.GetProcAddress(module,
            "?ExecuteActionOnPlayingID@SoundEngine@AK@@YAXW4AkActionOnEventType@12@IHW4AkCurveInterpolation@@@Z");
        if (postEvent == IntPtr.Zero || action == IntPtr.Zero) { Error("Audio exports unavailable; interception was not installed"); return; }
        // Entering a managed callback while this DLL's loader entry point detaches a
        // thread can re-enter a shutting-down NativeAOT thread. Never guard that page.
        byte* image = (byte*)module;
        uint entryRva = *(uint*)(image + *(int*)(image + 0x3C) + 40);
        nuint pageMask = ~((nuint)Environment.SystemPageSize - 1);
        if (entryRva != 0 && (((nuint)image + entryRva) & pageMask) == ((nuint)postEvent & pageMask))
        { Error("Audio export shares the DLL entry-point page; interception was not installed"); return; }
        if (NativeMethods.VirtualQuery(postEvent, out var page, (nuint)sizeof(NativeMethods.MEMORY_BASIC_INFORMATION)) == 0 ||
            (page.Protect & PAGE_GUARD) != 0) { Error("Audio page is unavailable or already guarded"); return; }
        _postEventAddr = postEvent;
        _pageStart = (ulong)postEvent & ~((ulong)Environment.SystemPageSize - 1);
        _originalProtection = page.Protect;
        _executeAction = (delegate* unmanaged[Cdecl]<int, uint, int, int, void>)action;
        _vehHandle = NativeMethods.AddVectoredExceptionHandler(1, &VectoredHandler);
        if (_vehHandle == IntPtr.Zero) return;
        _monitorReady = true;
        SetBreakpointWhenPostEventAddressIsRead();
    }

    private static void SetBreakpointWhenPostEventAddressIsRead()
    {
        if (_monitorReady && IsMonitoringEnabled &&
            !NativeMethods.VirtualProtect(_postEventAddr, 1, _originalProtection | PAGE_GUARD, out _))
            _monitorReady = false;
    }

    private struct PendingEvent
    {
        public uint EventId;
        public ulong GameObjectId;
        public ulong ReturnAddress;
        public ulong StackPointer;
    }
    [InlineArray(16)] private struct PendingEvents { private PendingEvent _first; }
    [ThreadStatic] private static PendingEvents _pending;
    [ThreadStatic] private static int _depth;
    [ThreadStatic] private static bool _rearmGuard;
    [ThreadStatic] private static bool _hadTrapFlag;
    [ThreadStatic] private static bool _executingAction;
    [ThreadStatic] private static ulong _savedDr0;
    [ThreadStatic] private static ulong _savedDr7Slot;
    private const ulong Dr7SlotMask = 0xF0003;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static int VectoredHandler(EXCEPTION_POINTERS* pointers) => HandleException(pointers);

    internal static int HandleException(EXCEPTION_POINTERS* pointers)
    {
        var record = pointers->ExceptionRecord;
        var ctx = pointers->ContextRecord;
        if (record->ExceptionCode == STATUS_GUARD_PAGE_VIOLATION)
        {
            // Guard exceptions concern an entire page, including accesses from other code.
            if (_vehHandle == IntPtr.Zero || record->NumberParameters < 2 ||
                record->ExceptionInformation[1] < _pageStart ||
                record->ExceptionInformation[1] - _pageStart >= (ulong)Environment.SystemPageSize)
                return EXCEPTION_CONTINUE_SEARCH;
            if (ctx->Rip == (ulong)_postEventAddr && ctx->Rsp != 0 && !_executingAction && IsMonitoringEnabled)
            {
                bool ownsSlot = _depth > 0 && ctx->Dr0 == _pending[_depth - 1].ReturnAddress && (ctx->Dr7 & 3) == 1;
                // A foreign debugger's slot is never overwritten. Discard stale state after an unwind.
                if (_depth > 0 && !ownsSlot) _depth = 0;
                if (ownsSlot && ctx->Rsp >= _pending[0].StackPointer)
                {
                    ctx->Dr0 = _savedDr0;
                    ctx->Dr7 = (ctx->Dr7 & ~Dr7SlotMask) | _savedDr7Slot;
                    _depth = 0;
                    ownsSlot = false;
                }
                if (_depth < 16 && (ownsSlot || (ctx->Dr7 & 3) == 0))
                {
                    if (_depth == 0) { _savedDr0 = ctx->Dr0; _savedDr7Slot = ctx->Dr7 & Dr7SlotMask; }
                    _pending[_depth++] = new PendingEvent
                    {
                        EventId = (uint)ctx->Rcx, GameObjectId = ctx->Rdx,
                        ReturnAddress = *(ulong*)ctx->Rsp, StackPointer = ctx->Rsp
                    };
                    ctx->Dr0 = _pending[_depth - 1].ReturnAddress;
                    ctx->Dr7 = (ctx->Dr7 & ~Dr7SlotMask) | DR7_ENABLE_L0;
                }
            }
            _hadTrapFlag = (ctx->EFlags & TRAP_FLAG) != 0;
            _rearmGuard = true;
            ctx->EFlags |= TRAP_FLAG;
            return EXCEPTION_CONTINUE_EXECUTION;
        }
        if (record->ExceptionCode != STATUS_SINGLE_STEP) return EXCEPTION_CONTINUE_SEARCH;

        bool handled = false;
        bool foreignTrap = false;
        // Windows can report a trap-flag step with DR6 == 0 (observed in the native
        // smoke process during DLL thread attach). Track the step we requested;
        // don't require BS when no hardware slot reports a coincident exception.
        if (_rearmGuard && ((ctx->Dr6 & (1UL << 14)) != 0 || (ctx->Dr6 & 0xF) == 0))
        {
            _rearmGuard = false;
            foreignTrap = _hadTrapFlag;
            if (!_hadTrapFlag) { ctx->EFlags &= ~TRAP_FLAG; ctx->Dr6 &= ~(1UL << 14); }
            SetBreakpointWhenPostEventAddressIsRead();
            handled = true;
        }
        if (_depth > 0 && (ctx->Dr6 & DR6_DETECT_B0) != 0 &&
            ctx->Rip == _pending[_depth - 1].ReturnAddress && ctx->Dr0 == ctx->Rip && (ctx->Dr7 & 3) == 1)
        {
            var entry = _pending[--_depth];
            ctx->Dr0 = _depth == 0 ? _savedDr0 : _pending[_depth - 1].ReturnAddress;
            if (_depth == 0) ctx->Dr7 = (ctx->Dr7 & ~Dr7SlotMask) | _savedDr7Slot;
            ctx->Dr6 &= ~DR6_DETECT_B0;
            uint playingId = (uint)ctx->Rax;
            if (_executeAction != null && playingId != 0 && IsMuted(entry.EventId))
            {
                _executingAction = true;
                _executeAction((int)AkActionOnEventType.Stop, playingId, 0, (int)AkCurveInterpolation.Constant);
                _executingAction = false;
            }
            if (_captureDiagnostics) AudioLog.Add(entry.EventId, entry.GameObjectId);
            handled = true;
        }
        // Preserve debugger/other instrumentation exceptions even if one of our conditions coincided.
        return handled && !foreignTrap && (ctx->Dr6 & 0xE00F) == 0 ? EXCEPTION_CONTINUE_EXECUTION : EXCEPTION_CONTINUE_SEARCH;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMuted(uint eventId)
    {
        var ids = Volatile.Read(ref _mutedIds);
        return ids.Length != 0 && Array.BinarySearch(ids, eventId) >= 0;
    }

    internal static void AddMutedId(uint id) => UpdateMutedIds([id], false, false);
    internal static void RemoveMutedId(uint id) => UpdateMutedIds([id], false, true);
    internal static void ClearMutedIds()
    {
        lock (_muteLock)
        {
            _captureDiagnostics = false;
            UpdateMutedIds([], true, false);
        }
    }

    internal static void SetDiagnosticCapture(bool enabled)
    {
        lock (_muteLock)
        {
            if (enabled) AudioLog.ClearEventHistory(); // Allocate the bounded ring on the pipe thread before capture.
            _captureDiagnostics = enabled;
            UpdatePageProtection();
        }
    }

    private static void UpdatePageProtection()
    {
        if (IsMonitoringEnabled) SetBreakpointWhenPostEventAddressIsRead();
        else if (_vehHandle != IntPtr.Zero) NativeMethods.VirtualProtect(_postEventAddr, 1, _originalProtection, out _);
    }

    internal static void UpdateMutedIds(uint[] ids, bool replace, bool remove)
    {
        lock (_muteLock)
        {
            var current = Volatile.Read(ref _mutedIds);
            uint[] replacement = (remove ? current.Except(ids) : replace ? ids : current.Concat(ids)).Distinct().Order().ToArray();
            if (replacement.Length > MaxMutedIds) throw new InvalidDataException("The audio mute set exceeds capacity.");
            // Allocate/sort on the pipe thread, then publish once. Exception callbacks only read immutable data.
            Volatile.Write(ref _mutedIds, replacement);
            UpdatePageProtection();
        }
    }

    internal static List<uint> GetMutedIds() => Volatile.Read(ref _mutedIds).ToList();

    internal enum AkActionOnEventType : int
    {
        // Wwise AkSoundEngine.h ABI: 1 is Pause, which can leave voices suspended
        // after muting is cleared. Stop must be zero.
        Stop = 0,
        Pause = 1,
        Resume = 2,
        Break = 3,
        ReleaseEnvelope = 4
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

    private static readonly int[] _versions = new int[LogSize];
    internal static void Add(uint eventID, ulong gameObjectID)
    {
        int index = Interlocked.Increment(ref _globalSequenceCount) & LogMask;
        int version = Volatile.Read(ref _versions[index]);
        if ((version & 1) != 0 || Interlocked.CompareExchange(ref _versions[index], version + 1, version) != version) return;
        _eventHistory[index] = new LoggedAudioEvent { EventID = eventID, GameObjectID = gameObjectID, Timestamp = Stopwatch.GetTimestamp() };
        Volatile.Write(ref _versions[index], unchecked(version + 2));
    }

    public static List<LoggedAudioEvent> GetOrderedEventHistory()
    {
        var result = new List<LoggedAudioEvent>();
        for (int i = 0; i < LogSize; i++)
        {
            int version = Volatile.Read(ref _versions[i]);
            if ((version & 1) != 0) continue;
            var entry = _eventHistory[i];
            Thread.MemoryBarrier();
            if (version == Volatile.Read(ref _versions[i]) && entry.EventID != 0) result.Add(entry);
        }
        return result.OrderByDescending(e => e.Timestamp).ToList();
    }

    public static void ClearEventHistory()
    {
        for (int i = 0; i < LogSize; i++)
        {
            int version = Volatile.Read(ref _versions[i]);
            if ((version & 1) != 0 || Interlocked.CompareExchange(ref _versions[i], version + 1, version) != version) continue;
            _eventHistory[i] = default;
            Volatile.Write(ref _versions[i], unchecked(version + 2));
        }
    }
}