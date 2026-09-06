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

using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static EveOPreview.Robin.DebugLogger;
using static EveOPreview.Robin.Global;

namespace EveOPreview.Robin;

public unsafe class DxHook
{
    // See signature https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-present plus "this" pointer.
    private static delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int> _originalPresent;

    // See signature https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgiswapchain1-present1 "plus" this pointer.
    private static delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr, int> _originalPresent1;

    internal sealed record FpsTargets(int Foreground, int Background, int Predicted)
    {
        public bool Enabled => Foreground > 0 || Background > 0;
        public double Interval(FocusType focus)
        {
            int fps = focus == FocusType.Foreground ? Foreground
                : focus == FocusType.Predicted && Predicted > 0 ? Predicted : Background;
            return Enabled && fps > 0 ? 1000.0 / fps : 0;
        }
    }

    private static FpsTargets _targets = new(0, 0, 0);
    internal static FpsTargets Targets => Volatile.Read(ref _targets);
    internal static void SetFpsTargets(int foreground, int background, int predicted) =>
        Volatile.Write(ref _targets, new FpsTargets(ValidateFps(foreground), ValidateFps(background), ValidateFps(predicted)));
    private static int ValidateFps(int fps) => fps is >= 1 and <= 1000 ? fps : 0;

    private static readonly object FocusLock = new();
    private static volatile FocusType _ourFocus = FocusType.Background;
    private static bool _ignoreNextLostFocus;
    private static long _focusChangedAt = Stopwatch.GetTimestamp();
    internal static int PredictedFocusTimeoutMs = 5000;
    private static readonly uint CurrentPid = (uint)Environment.ProcessId;
    [ThreadStatic] private static long _lastFrameTimestamp;
    [ThreadStatic] private static PrecisionSleep? _precisionSleep;
    [ThreadStatic] private static int _presentDepth;
    private static int _initialized;
    internal static bool HooksInstalled;
    private static long _focusBoostUntil;

    [UnmanagedCallersOnly(EntryPoint = "Initialize", CallConvs = [typeof(CallConvStdcall)])]
    public static uint Initialize(IntPtr parameter)
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return 0;
        try
        {
            // Name the pipe based on the MainWindowHandle so clients don't conflict and so it's easy to find, we can also use this like a mutex which should work on linux too.
            NamedPipeServer.Initialize();

            // Setup a named pipe so we can manage the target fps from another process such as Eve-O Preview.
            _ = Task.Run(NamedPipeServer.StartPipeServer);
            Global.StartOwnerWatchdog();
        }
        catch (Exception ex)
        {
            // Double on using the named pipe like a cross-platform mutex. If the named pipe is already taken then don't hook again.
            Error(ex, "Failed to create named pipe server");
            return 1;
        }

        //Log($"Subscribing to EVENT_SYSTEM_FOREGROUND");
        WinEventHook.StartListening(HandleForegroundChangedEvent);

        try
        {
            //Log($"Setup dummy DXGI objects to find the VTable address");
            using var factory = new Factory1();
            using var device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.None);
            using var swapChain = new SwapChain(factory, device, new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(1, 1, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                Usage = Usage.RenderTargetOutput,
                OutputHandle = Process.GetCurrentProcess().MainWindowHandle,
                SampleDescription = new SampleDescription(1, 0),
                IsWindowed = true
            });

            void** vTablePointer = *(void***)swapChain.NativePointer;

            //Log($"Locating Present should at index 8 for DirectX 11");
            void** presentEntryPtr = &vTablePointer[8];
            _originalPresent = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)*presentEntryPtr;

            // Grant access to the memory address
            if (NativeMethods.VirtualProtect((IntPtr)presentEntryPtr, (UIntPtr)sizeof(nint), PAGE_EXECUTE_READWRITE, out var oldProtect))
            {
                Info($"Hooking into Present");
                HooksInstalled = true;
                *presentEntryPtr = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)&HookedPresent;
                // Set the protection back to what it was before we got here.
                NativeMethods.VirtualProtect((IntPtr)presentEntryPtr, (UIntPtr)sizeof(nint), oldProtect, out _);
            }

            // Present1 belongs to IDXGISwapChain1, including on D3D11. Query the interface
            // before reading its vtable; loading d3d12.dll says nothing about that layout.
            // Use the COM ABI directly. SharpDX's generic QueryInterface constructs wrappers
            // through reflection, which can be trimmed from a NativeAOT shared library.
            Guid swapChain1Id = new("790a45f7-0d42-4876-983a-0a55cfe6f4aa");
            IntPtr swapChain1 = IntPtr.Zero;
            var queryInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)vTablePointer[0];
            if (queryInterface(swapChain.NativePointer, &swapChain1Id, &swapChain1) >= 0 && swapChain1 != IntPtr.Zero)
            {
                void** extendedVtable = *(void***)swapChain1;
                try
                {
                    void** entry = &extendedVtable[22];
                    _originalPresent1 = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr, int>)*entry;
                    if (NativeMethods.VirtualProtect((IntPtr)entry, (UIntPtr)sizeof(nint), PAGE_EXECUTE_READWRITE, out var protection))
                    {
                        *entry = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr, int>)&HookedPresent1;
                        NativeMethods.VirtualProtect((IntPtr)entry, (UIntPtr)sizeof(nint), protection, out _);
                    }
                    else Error("Could not protect the Present1 vtable entry");
                }
                finally { ((delegate* unmanaged[Stdcall]<IntPtr, uint>)extendedVtable[2])(swapChain1); }
            }
        }
        catch (Exception ex)
        {
            Error(ex, $"{nameof(Initialize)} Initializing DirectX");
        }

        try
        {
            AudioMuteSystem.InstallAudioMonitor();
        }
        catch (Exception ex)
        {
            Error(ex);
        }
        return HooksInstalled ? 0u : 1u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FocusType GetOurCurrentFocus()
    {
        var focus = _ourFocus;
        int timeout = focus == FocusType.Predicted ? Volatile.Read(ref PredictedFocusTimeoutMs) : 3000;
        if (Stopwatch.GetElapsedTime(Volatile.Read(ref _focusChangedAt)).TotalMilliseconds >= timeout)
        {
            SetOurWindowInFocus(IsThisOurHandle(NativeMethods.GetForegroundWindow()) ? FocusType.Foreground : FocusType.Background, reconcile: true);
        }
        return _ourFocus;
    }

    private static void HandleForegroundChangedEvent(IntPtr handle) =>
        SetOurWindowInFocus(IsThisOurHandle(handle) ? FocusType.Foreground : FocusType.Background);

    internal static void SetOurWindowInFocus(FocusType focus, bool reconcile = false)
    {
        lock (FocusLock)
        {
            if (!reconcile && _ignoreNextLostFocus && focus == FocusType.Background)
            {
                _ignoreNextLostFocus = false;
                return;
            }
            _ignoreNextLostFocus = focus == FocusType.Predicted;
            Volatile.Write(ref _focusChangedAt, Stopwatch.GetTimestamp());
            _ourFocus = focus;
        }
        if (OwnerProcessId > 0 && focus == FocusType.Foreground) NativeMethods.AllowSetForegroundWindow(OwnerProcessId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsThisOurHandle(IntPtr theHandleToCheck)
    {
        // If we know the handle, just check it directly
        // Note: We should always know this because we've started being lazy and assuming it's the main window. but leaving code for future changes if needed.
        if (ThisClientsHandle != IntPtr.Zero)
        {
            return ThisClientsHandle == theHandleToCheck;
        }

        // If there's no handle, it can't be ours.
        if (theHandleToCheck == IntPtr.Zero)
        {
            return false;
        }

        // If we don't know the handle yet, compare the process Id until we find it.
        NativeMethods.GetWindowThreadProcessId(theHandleToCheck, out uint foregroundPid);

        if (foregroundPid == CurrentPid)
        {
            ThisClientsHandle = theHandleToCheck;
            return true;
        }

        return false;
    }

    private static double CurrentFrameInterval()
    {
        if (Stopwatch.GetTimestamp() < Volatile.Read(ref _focusBoostUntil)) return 0;
        return Targets.Interval(GetOurCurrentFocus());
    }

    internal static void PrepareForFocus()
    {
        // Release Present briefly even when the configured foreground rate is 1 FPS.
        // This is request-time state only; the render callback allocates nothing.
        Volatile.Write(ref _focusBoostUntil, Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4);
        SetOurWindowInFocus(FocusType.Foreground);
    }

    private static bool ThrottleTheFrame()
    {
        double interval = CurrentFrameInterval();
        if (interval <= 0) { _lastFrameTimestamp = 0; return false; }
        if (_lastFrameTimestamp == 0) { _lastFrameTimestamp = Stopwatch.GetTimestamp(); return true; }
        _precisionSleep ??= new PrecisionSleep();
        while (true)
        {
            interval = CurrentFrameInterval();
            if (interval <= 0) { _lastFrameTimestamp = 0; return false; }
            double remaining = interval - Stopwatch.GetElapsedTime(_lastFrameTimestamp).TotalMilliseconds;
            if (remaining <= 0) break;
            // Recheck focus and disable during both the long wait and final remainder.
            if (remaining > 1) _precisionSleep.Sleep(Math.Min(15, remaining - 0.5));
            else Thread.SpinWait(10);
        }
        _lastFrameTimestamp = Stopwatch.GetTimestamp();
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int HookedPresent(IntPtr swapChain, uint syncInterval, uint flags)
    {
        if (_presentDepth != 0 || (flags & 9) != 0) return _originalPresent(swapChain, syncInterval, flags);
        _presentDepth++;
        try { return _originalPresent(swapChain, ThrottleTheFrame() ? 0 : syncInterval, flags); }
        finally { _presentDepth--; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int HookedPresent1(IntPtr swapChain, uint syncInterval, uint flags, IntPtr parameters)
    {
        if (_presentDepth != 0 || (flags & 9) != 0) return _originalPresent1(swapChain, syncInterval, flags, parameters);
        _presentDepth++;
        try { return _originalPresent1(swapChain, ThrottleTheFrame() ? 0 : syncInterval, flags, parameters); }
        finally { _presentDepth--; }
    }

    private const int PAGE_EXECUTE_READWRITE = 0x40;
}
