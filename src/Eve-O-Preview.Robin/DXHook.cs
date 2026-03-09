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

    internal static int TargetFpsInFocus = 144;
    internal static int TargetFpsInBackground = 30;
    internal static int TargetFpsInPredictFocus = 600;
    internal static double PerFrameTargetMsInFocus = 1000.0 / TargetFpsInFocus;
    internal static double PerFrameTargetMsInBackground = 1000.0 / TargetFpsInBackground;
    internal static double PerFrameTargetMsInPredictFocus = 1000.0 / TargetFpsInPredictFocus;
    internal static int PredictedFocusTimeoutMs = 5000;
    internal static bool IsFpsThrottleActive = true;

    private static FocusType _ourFocus = FocusType.Background;
    private static bool _ignoreNextLostFocus = false;

    private static readonly uint CurrentPid = (uint)Process.GetCurrentProcess().Id;
    private static readonly Stopwatch LastFrameSw = Stopwatch.StartNew();
    private static readonly Stopwatch LastCheckedFocusSw = Stopwatch.StartNew();
    private static readonly PrecisionSleep PrecisionSleep = new();

    [UnmanagedCallersOnly(EntryPoint = "Initialize", CallConvs = [typeof(CallConvStdcall)])]
    public static void Initialize()
    {
        try
        {
            // Name the pipe based on the MainWindowHandle so clients don't conflict and so it's easy to find, we can also use this like a mutex which should work on linux too.
            NamedPipeServer.Initialize();

            // Setup a named pipe so we can manage the target fps from another process such as Eve-O Preview.
            Task.Run(NamedPipeServer.StartPipeServer);
        }
        catch (Exception ex)
        {
            // Double on using the named pipe like a cross-platform mutex. If the named pipe is already taken then don't hook again.
            Error(ex, "Failed to create named pipe server");
            return;
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
                *presentEntryPtr = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)&HookedPresent;
                // Set the protection back to what it was before we got here.
                NativeMethods.VirtualProtect((IntPtr)presentEntryPtr, (UIntPtr)sizeof(nint), oldProtect, out _);
            }

            bool isDx12Game = false;
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                if (module.ModuleName?.ToLower() == "d3d12.dll")
                {
                    //Log($"Located DirectX 12 module is loaded.");
                    isDx12Game = true;
                    break;
                }
            }

            if (isDx12Game)
            {
                //Log($"Locating Present1 should at index 22 for DirectX 12");
                void** presentEntry22 = &vTablePointer[22];
                _originalPresent1 = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr, int>)*presentEntry22;

                if (NativeMethods.VirtualProtect((IntPtr)presentEntry22, (UIntPtr)sizeof(nint), PAGE_EXECUTE_READWRITE, out var oldProtectDx12))
                {
                    Info($"Hooking into Present1");
                    *presentEntry22 = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr, int>)&HookedPresent1;

                    NativeMethods.VirtualProtect((IntPtr)presentEntry22, (UIntPtr)sizeof(nint), oldProtectDx12, out _);
                }
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FocusType GetOurCurrentFocus()
    {
        var msBeforeNextCheck = _ourFocus != FocusType.Predicted ? 3000 : PredictedFocusTimeoutMs;
        if (LastCheckedFocusSw.ElapsedMilliseconds < msBeforeNextCheck)
        {
            return _ourFocus;
        }

        // Just as a safe guard lets manually check if we're in focus if it's been a while since anything happened.
        // We depend mostly on events updating our focus now so we most the time we can trust it but checking once every few seconds is a trivial backup.
        // This is mostly to protect us accidentally thinking we have focus e.g. if the named pipe gives us focus, but then we lost it somehow in a race condition.
        IntPtr foregroundHandle = NativeMethods.GetForegroundWindow();
        var currentFocus = IsThisOurHandle(foregroundHandle) ? FocusType.Foreground : FocusType.Background;
        SetOurWindowInFocus(currentFocus);

        EnsureOwnerIsStillAlive();

        return _ourFocus;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureOwnerIsStillAlive()
    {
        if (OwnerProcessId == -1)
        {
            return;
        }

        if (IsProcessRunning(OwnerProcessId))
        {
            return;
        }

        OwnerProcessId = 0;
        IsFpsThrottleActive = false;
    }

    private static void HandleForegroundChangedEvent(IntPtr handleTakingFocus)
    {
        var currentFocus = IsThisOurHandle(handleTakingFocus) ? FocusType.Foreground : FocusType.Background;
        SetOurWindowInFocus(currentFocus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetOurWindowInFocus(FocusType newFocus)
    {
        if (_ignoreNextLostFocus && newFocus == FocusType.Background)
        {
            _ignoreNextLostFocus = false;
            return;
        }

        _ourFocus = newFocus;
        LastCheckedFocusSw.Restart();

        if (newFocus == FocusType.Predicted)
        {
            _ignoreNextLostFocus = true;
        }

        if (OwnerProcessId != -1 && newFocus == FocusType.Foreground)
        {
            NativeMethods.AllowSetForegroundWindow(OwnerProcessId);
        }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrottleTheFrame()
    {
        if (IsFpsThrottleActive)
        {
            var focusAtStartOfFrame = GetOurCurrentFocus();

            double perFrameTargetMs;
            switch (focusAtStartOfFrame)
            {
                case FocusType.Background:
                    perFrameTargetMs = PerFrameTargetMsInBackground;
                    break;
                case FocusType.Foreground:
                    perFrameTargetMs = PerFrameTargetMsInFocus;
                    break;
                case FocusType.Predicted:
                    perFrameTargetMs = PerFrameTargetMsInPredictFocus > 0.9
                        ? PerFrameTargetMsInPredictFocus
                        : PerFrameTargetMsInBackground;
                    break;
                default:
                    perFrameTargetMs = 0;
                    break;
            }

            double elapsed = LastFrameSw.Elapsed.TotalMilliseconds;
            if (elapsed < perFrameTargetMs)
            {
                double timeLeftToWait = perFrameTargetMs - elapsed;

                // ToDo: Wait out the bulk in a PrecisionSleep, with a callback to interrupt if we need to take focus.
                // Then we can remove the whole loop and check logic for a sleep that is much more predicable on the CPU.

                // First lets wait out some big chunks, but still small enough to break out if we take focus.
                // This should only really be needed if we're going super slow fps and need to snap back to high speed.
                while (timeLeftToWait > 16.0)
                {
                    PrecisionSleep.Sleep(15); // We can do a Sleep(1) here which will pass about 15-16ms but our PrecisionSleep uses a waitable object so should be better on the CPU.

                    // If we received focus, then break out of this frame to reduce lag when switching.
                    if (focusAtStartOfFrame != FocusType.Foreground && GetOurCurrentFocus() == FocusType.Foreground)
                    {
                        LastFrameSw.Restart();
                        return;
                    }

                    // Refresh remaining time left to wait.
                    timeLeftToWait = perFrameTargetMs - LastFrameSw.Elapsed.TotalMilliseconds;
                }

                // Wait out the final big chunk to save the CPU a bit longer. We're so close to the end theres no point trying to escape.
                if (timeLeftToWait > 1.0)
                {
                    PrecisionSleep.Sleep(timeLeftToWait - 0.5);
                }

                // Busy wait for the final high-precision micro-seconds
                while (LastFrameSw.Elapsed.TotalMilliseconds < perFrameTargetMs)
                {
                    Thread.SpinWait(10);
                }
            }

            // Start timing the next frame so we know how much extra delay we need to add.
            LastFrameSw.Restart();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int HookedPresent(IntPtr swapChain, uint syncInterval, uint flags)
    {
        ThrottleTheFrame();

        // Let DirectX 11 do its thing to generate the next frame.
        //return _originalPresent(swapChain, syncInterval, flags);
        return _originalPresent(swapChain, IsFpsThrottleActive ? 0 : syncInterval, flags);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int HookedPresent1(IntPtr swapChain, uint syncInterval, uint flags, IntPtr present1Parameters)
    {
        ThrottleTheFrame();

        // Let DirectX 12 do its thing to generate the next frame.
        return _originalPresent1(swapChain, IsFpsThrottleActive ? 0 : syncInterval, flags, present1Parameters);
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private static bool IsProcessRunning(int pid)
    {
        IntPtr processHandle = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (processHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(processHandle);
            return true;
        }
        return false;
    }

    const uint MB_OK = 0x00000000; // just a ding
    const uint MB_ICONERROR = 0x00000010; // another noise for testing

    private const int PAGE_EXECUTE_READWRITE = 0x40;
}