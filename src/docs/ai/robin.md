# Robin: injected runtime, frame pacing, audio and host protocol

This is a source-based navigation guide, reviewed on 2026-09-06. Recheck the named symbols before editing: the implementation is authoritative. The performance mechanisms below explain intent and tradeoffs; they are not measurements or a claim that every GPU, game build or race has been validated. Observed concerns are listed separately from behavior to preserve.


## Start with the process boundary

The desktop application and Robin execute in **different processes**. [HookService](../../Eve-O-Preview/Services/Implementation/HookService.cs) runs in EVE-O Preview; [DxHook](../../Eve-O-Preview.Robin/DXHook.cs), the pipe server, foreground listener and audio exception handler run inside each injected game process. The spelling is `DXHook.cs` for the file and `DxHook` for the class. Desktop dependency injection, MediatR and Serilog do not extend into Robin.

[Robin's project](../../Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj) targets `net10.0`, enables Native AOT, emits a shared native library (`NativeLib=Shared`), targets x64 and permits unsafe code. It references SharpDX.Direct3D11 and SharpDX.DXGI 4.2.0. `DxHook.Initialize` is the native `Initialize` export, marked `UnmanagedCallersOnly` with the declared Stdcall convention. A normal managed build DLL is not interchangeable with the published native DLL used by `LoadLibrary`. Keep architecture, exports, native signatures and pointer layouts aligned when changing the build. The non-Windows pipe branch is not evidence of a supported non-Windows runtime: injection, rendering, timers and event hooks use Windows APIs.

### Feature entry points

| User-visible behavior | Follow these symbols across the boundary |
| --- | --- |
| Newly discovered client | [ThumbnailManager.UpdateThumbnailsList](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs) → `HookService.TryInstallHooksAsync` → native `DxHook.Initialize` |
| Enable/disable or update FPS settings | [SetFpsLimiterEnabledHandler.Handle](../../Eve-O-Preview/Mediator/Handlers/Configuration/SetFpsLimiterEnabledHandler.cs) → install/update or `DisableFpsLimiterAsync` → `NamedPipeServer.A2F1_SetFpsTargets` |
| Activate a client immediately | [WindowManager.ActivateWindow](../../Eve-O-Preview/Services/Implementation/WindowManager.cs) starts `TellEveClientFocusIsComingAsync` without awaiting it, then makes Windows focus calls. The notification sends pipe `A3 B1` → `DxHook.SetOurWindowInFocus` only when desktop FPS limiting is enabled; Windows activation still proceeds when disabled. |
| Prepare the next client for a possible switch | `WindowManager.PredictUpcomingClient` → `TellEveClientFocusIsMaybeComingSoonAsync` → pipe `A3 B3` → predicted focus state, with the pipe notification gated by desktop `FpsLimiterSettings.IsEnabled` |
| Actual OS foreground changes | [WinEventHook.RunHookListener / OnForegroundChanged](../../Eve-O-Preview.Robin/WinEventHook.cs) → `DxHook.HandleForegroundChangedEvent` |
| Change preset/custom muted sounds | [SetAudioSettingsHandler.Handle](../../Eve-O-Preview/Mediator/Handlers/Configuration/SetAudioSettingsHandler.cs) installs/updates hooks, then `HookService.UpdateMutedAudioAsync` → clear/mute pipe commands → [AudioMuteSystem](../../Eve-O-Preview.Robin/AudioMuteSystem.cs) |
| Native diagnostics for the desktop process | [Program.Main](../../Eve-O-Preview/Program.cs) → [DebuggerSidecar.LaunchTheSideCar / RunAsTheSideCar](../../Eve-O-Preview/Services/Implementation/DebuggerSidecar.cs); this sidecar attaches to the desktop application's PID, not each game client |

## Injection and ownership lifecycle

`HookService.TryInstallHooksAsync` reserves a window handle in `_initializedClients`. The first attempt pings `EveoRobin_{handle}`; an existing responder lets a restarted desktop application reuse an already injected Robin. Otherwise it:

1. Finds `Eve-O-Preview.Robin.dll` next to `Environment.ProcessPath`.
2. Opens the target with `PROCESS_ALL_ACCESS`, allocates remote memory, and writes an ASCII, null-terminated absolute DLL path.
3. Starts a remote thread at `LoadLibraryA`, sleeps 2000 ms, then finds the loaded module by filename containing `Eve-O-Preview.Robin`.
4. Loads the same DLL locally and resolves `Initialize`. Its offset from the local module base is added to the target module base to calculate the remote export address.
5. Starts a second remote thread at that calculated address. After the host's `Task.Run` body returns, it waits 1000 ms, claims ownership with the desktop PID, and sends FPS/audio settings. It does not wait for the remote initialization thread to finish.

Robin initializes the named pipe **before** installing foreground, DXGI and audio hooks. The pipe is also used to discourage duplicate installation. A successful ping or host “Successfully initialized” log therefore does not prove that presentation or audio interception succeeded. Initialization failures are logged independently for DXGI and audio, and the pipe can remain responsive.

[Global](../../Eve-O-Preview.Robin/Global.cs) initially takes the current process's `MainWindowHandle`. The pipe name is captured once from this value. `DxHook.IsThisOurHandle` normally compares HWNDs; if the stored handle is zero, it can learn an HWND by matching its PID. Learning it later does **not** rename the already captured pipe. HWND identity is therefore part of the protocol and startup assumptions, not merely a display detail.

`OwnerProcessId=-1` means no owner check. `A2 B4` replaces it with the controller PID. While throttling and periodically reconciling focus, `EnsureOwnerIsStillAlive` tries `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`; failure sets owner to zero and disables throttling. A foreground state with owner other than `-1` calls `AllowSetForegroundWindow(owner)`, giving the controller a native permission opportunity. This is not an unconditional guarantee that subsequent Windows activation succeeds.

No normal detach/unload path restores DXGI pointers, removes the vectored exception handler or stops the listener/pipe thread. Disabling FPS sends zero targets; it does not unload Robin or clear muted sounds. A changed native DLL is not loaded into a client whose old Robin already answers the ping. Use a fresh controlled client when validating a changed injected binary.

## Frame pacing: preserve the reason for each shortcut

[DxHook.Initialize](../../Eve-O-Preview.Robin/DXHook.cs) creates and disposes a tiny D3D11 hardware device and a 1×1 dummy windowed swapchain to obtain a vtable. It saves slot **8** as `_originalPresent`, temporarily changes memory protection, writes `HookedPresent`, then restores protection. If the current module list contains `d3d12.dll`, it also treats slot **22** as `Present1`. This relies on the dummy interface exposing the right layout and the game using the patched implementation. The module check is a heuristic, not capability negotiation or proof that every actual swapchain is intercepted.

Both callbacks throttle **before** forwarding to the saved native function. When `IsFpsThrottleActive` is true, they forward `syncInterval=0`; when false they preserve the caller's original interval. Flags, `this` pointer, return value and `Present1` parameter pointer must retain their native meaning. Changing the location of the delay or always forcing zero changes frame pacing and the disabled state.

`ThrottleTheFrame` uses `LastFrameSw` to account for time spent since the previous throttle completed, then waits only the remaining target interval. The target is selected from [FocusType](../../Eve-O-Preview.Robin/FocusType.cs):

| State/rule | Current behavior |
| --- | --- |
| Foreground/background | Uses the corresponding `PerFrameTargetMs...` value |
| Predicted | Uses the predictive interval only if it is greater than 0.9 ms; otherwise uses the background interval |
| Valid target range | Pipe parser accepts 1–1000 FPS; values outside that range become zero for that state |
| Whole limiter enabled | Foreground interval + background interval > 0; a predictive value alone does not enable it |
| Native startup defaults | Enabled; foreground 144, background 30, predicted 600 FPS |
| Desktop model defaults | [FpsLimiterSettings](../../Eve-O-Preview/Configuration/Implementation/FpsLimiterSettings.cs): disabled; foreground 144, background 20, predicted 45 FPS |

The wait deliberately combines three mechanisms:

- While more than 16 ms remain, [PrecisionSleep.Sleep](../../Eve-O-Preview.Robin/PrecisionSleep.cs) waits 15 ms at a time. After each chunk, a transition from a non-foreground state to foreground breaks out early and restarts the frame clock, reducing the delay of switching out of very low background FPS.
- For a remainder above 1 ms, it waits `remainder - 0.5` ms with a high-resolution waitable timer.
- `Thread.SpinWait(10)` completes the final interval against the stopwatch. This spends some CPU to reduce the precision lost to scheduling.

`PrecisionSleep` creates one timer with `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION=0x2`, uses negative relative due times in 100 ns units (`-milliseconds * 10000`), and waits indefinitely for that timer. It closes the handle in a finalizer. Replacing this with a full busy loop or a single long sleep discards the existing CPU/latency tradeoff. Treat 15/16/1/0.5 ms as tunable implementation choices requiring measurements, not unexplained constants to “simplify.”

Focus is primarily updated by OS events and pipe commands, avoiding an OS foreground query on every presentation. `GetOurCurrentFocus` returns the cached state until 3000 ms pass, or until `PredictedFocusTimeoutMs` elapses for predicted state (default 5000 ms). Reconciliation then queries the OS, updates state and checks ownership. `WinEventHook` uses a dedicated background thread with a Windows message pump, `EVENT_SYSTEM_FOREGROUND=0x3`, and `WINEVENT_OUTOFCONTEXT`. It ignores non-window object IDs and consecutive duplicate HWNDs. The pump is required for this design.

Prediction deliberately sets `_ignoreNextLostFocus`; the next background update is consumed instead of replacing predicted state. This protects a speculative boost from an intervening lost-focus notification. The flag is not cleared on a foreground update, and the long-wait escape tests specifically for **Foreground**, not Predicted. When debugging prediction latency, trace those actual transitions rather than assuming that any focus-state change interrupts sleep.

## Pipe contract: change both ends together

Read [HookService](../../Eve-O-Preview/Services/Implementation/HookService.cs) and [NamedPipeServer](../../Eve-O-Preview.Robin/NamedPipeServer.cs) together. The Windows pipe grants the current user full control, accepts one instance/connection at a time and uses byte mode with synchronous I/O. Each connection carries one command. Payload integers are little-endian `BinaryReader`/`BinaryWriter` values; there is no protocol version, general payload length or request ID.

| Request bytes | Payload | Reply / receiver |
| --- | --- | --- |
| `A3 B1` | None | No reply; `A3B1_SetFocusNow` sets Foreground |
| `A3 B3` | `Int32 timeoutMs` | No reply; `A3B3_PrepareToTakeFocusSoon` sets Predicted and stores timeout |
| `A2 B4` | `Int32 ownerPid` | `01`; `A2B4_ClaimProcessOwnership` |
| `A2 F1` | `Int32 foreground`, `F2`, `Int32 background`, `F3`, `Int32 predicted` | `01`; `A2F1_SetFpsTargets` |
| `A2 C1` | None | `01`; clear all muted IDs |
| `A2 C2` | `Int32 count`, then `count` × `UInt32 eventId` | `01`; remove listed muted IDs |
| `A2 C3` | `Int32 count`, then `count` × `UInt32 eventId` | `01`; add listed muted IDs |
| `A1 B2` | None | `01`; ping |
| `A1 A1` | None | `F1`, `Int32 foreground`, `F2`, `Int32 background`, `F3`, `Int32 predicted`, `01` |
| `A1 C4` | None | `Int32 count`, then `UInt32` muted IDs; no trailing success byte |
| `A1 C5` | None | `Int32 count`, then records of `UInt32 EventID`, `UInt64 GameObjectID`, **`Int64 Timestamp`**; no trailing success byte |

`A1 A1`, `A1 C4`, `A1 C5` and `A2 C2` exist on the server but are not exposed by the current `IHookService` desktop interface. The history comment says `ulong timestamp`; the actual field/writer uses signed `long`. The timestamp is `Stopwatch.GetTimestamp()` ticks, not milliseconds or UTC.

`HookService` connects with a 100 ms timeout and moves most I/O onto `Task.Run`. Fire-and-forget refers to the absence of a protocol reply; connection and writing still occur. `Ping` itself is synchronous. Update/query paths then call a blocking `ReadByte` without a separate response timeout. The server processes commands serially, flushes, and disconnects.

The server catches processing exceptions, waits 10 ms, disposes/recreates the pipe and increments `failureCount`. That count is cumulative, never reset by success. Above 100 failures it stops the loop, disables FPS throttling and shows a message box in the target process. A failure while recreating the pipe is outside the original try body. This recovery path deserves explicit validation when changing framing or cancellation.

## Audio interception: game-specific native machinery

[AudioMuteSystem.InstallAudioMonitor](../../Eve-O-Preview.Robin/AudioMuteSystem.cs) resolves two exact decorated exports from `_audio2.dll`: `AK::SoundEngine::PostEvent` and `ExecuteActionOnPlayingID`. Their full decorated strings are in that method. They are ABI assumptions about the loaded game module; preserve them until a new game build is examined. It registers `VectoredHandler` with first priority and resolves `_executeAction` as an unmanaged Cdecl function pointer.

The design observes a returned playing ID and stops that specific playback, rather than changing global process volume:

1. If at least one muted ID exists, mark the page containing `_postEventAddr` as `PAGE_EXECUTE_READWRITE | PAGE_GUARD`.
2. On a guard exception at exactly `Rip == _postEventAddr`, capture the event ID from `Rcx` and game object from `Rdx` into thread-static fields. Read the return address from `Rsp`, put it in hardware breakpoint register `Dr0`, and enable local breakpoint zero with `Dr7` bit 0.
3. Set the trap flag (`EFlags |= 0x100`) to single-step and rearm the guard.
4. On a single-step reporting `Dr6` bit 0, take the returned playing ID from `Rax`, clear the breakpoint state, and call `ExecuteActionOnPlayingID(Stop=1, playingId, 0, Constant=9)` if that event is muted and the playing ID is nonzero.

Guard status is page-wide and is cleared after access, explaining why the code rearms it from the single-step path. This also means the `VirtualProtect(..., 1, ...)` request is not instrumentation confined to one byte or one function. See Microsoft's [guard-page semantics](https://learn.microsoft.com/en-us/windows/win32/memory/creating-guard-pages).

The muted set is a preallocated `uint[1024]` with `_mutedCount`. Writers take `_muteLock`, keep the active prefix sorted, and deduplicate; `IsMuted` performs an unlocked binary search with an immediate empty-set return. Adding the first ID arms the guard. Entries beyond capacity are silently ignored. Preserve the sorted-prefix/count contract if retaining binary search, and evaluate native callback cost before adding allocation, logging or coarse locks to its lookup path. Existing stop-action logging already formats a debug string inside the handler, so the whole callback is not allocation-free.

`HookService.UpdateMutedAudioAsync` clears all IDs first, combines custom IDs with enabled presets, deduplicates, then sends the list. It ignores the clear operation's Boolean result; the returned result is the add operation's acknowledgment. `A2 C3` itself adds to the existing set, not replaces it. Preset IDs currently are:

| Preset | Events and unsigned IDs in HookService |
| --- | --- |
| Jump gate tunnel | `jump_gates_start_play=3689163958`, `jump_gates_exit_play=1537508544`, `jump_gates_lightning_play=1768044352` |
| Location banner | `location_banner_play=2377891014`, `location_banner_data_clicks_play=3090840445` |

[AudioMuteSettings.TryParseCustomMutedEventIds](../../Eve-O-Preview/Configuration/Implementation/AudioMuteSettings.cs) accepts comma-separated unsigned decimal IDs, rejects the entire input on any invalid token, and removes duplicates. Do not accidentally narrow IDs to signed `int` or reinterpret them as hexadecimal when extending the UI/protocol.

`AudioLog` uses a 128-entry ring (`LogMask=127`) and `Interlocked.Increment` to allocate a slot, then writes the entry fields. Query results filter zero IDs and sort newest timestamp first. Calls to `AudioLog.Add` from the exception handler are compiled only under `#if DEBUG`. Thus release history is normally empty, and even debug history depends on exceptions occurring while monitoring is armed. The ring is not a complete event audit or an atomic snapshot.

## Native lifetime and diagnostics

[NativeMethods](../../Eve-O-Preview.Robin/NativeMethods.cs) contains source-generated `LibraryImport` declarations, unmanaged callback types, `MSG`, `EXCEPTION_RECORD`, `EXCEPTION_POINTERS` and the x64 `CONTEXT64`. Its layout places `Dr0` at `0x48`, `Rax` at `0x78`, `Rip` at `0xF8`, followed by reserved storage up to 1232 bytes. The exception handler edits the original native context through pointers: changing field sizes/order, packing, calling conventions or return marshaling is a behavioral change.

[DebugLogger](../../Eve-O-Preview.Robin/DebugLogger.cs) writes `OutputDebugString` messages with `[EVE-O HOOK]`, a statically captured main HWND, and severity. [DebuggerSidecar](../../Eve-O-Preview/Services/Implementation/DebuggerSidecar.cs) is a separate invocation of the desktop EXE with `--attach-debug-sidecar <pid>`; it checks for an existing debugger before launching hidden. It calls `DebugSetProcessKillOnExit(false)`, forwards debug strings, continues breakpoint exception `0x80000003`, and returns `DBG_EXCEPTION_NOT_HANDLED` for other exception codes. Its raw `DEBUG_EVENT` union starts at byte 16 and is interpreted with x64 offsets. Do not mistake these desktop debugger messages for proof that a particular game's Robin hooked successfully.

[ProcessHelpers.ToProcessInfo / OpenKernelHandle](../../Eve-O-Preview/Helper/ProcessHelpers.cs) opens an owned process handle with rights `0x1200` (`PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION`) for priority/affinity work. `MainWindowHandle` is an HWND; `ProcessHandle` is a kernel handle. They are not interchangeable even though both are `IntPtr`. [ProcessMonitor.GetUpdatedProcesses](../../Eve-O-Preview/Services/Implementation/ProcessMonitor.cs) closes removed clients' process handles through `CloseKernelHandle`; this helper does not solve every allocation path described below.

## Observed concerns, not requirements to preserve

These findings come from code inspection. They identify concrete follow-up work and validation needs; this documentation change does not fix them or claim a live-client reproduction.

- **Disabled-at-start mismatch.** New thumbnails request installation unconditionally, and Robin starts enabled. `UpdateTargetFpsAsync` returns early when the desktop setting is disabled, instead of sending zero targets. A newly injected client can retain Robin's default throttling despite the disabled desktop setting. An explicit `DisableFpsLimiterAsync` does send zeros. Separate this from the intended enabled/disabled contract.
- **Injection state and cleanup.** `_initializedClients` is keyed only by HWND and is removed on a caught installation exception, not on client removal or pipe failure. Subsequent reservations skip the ping; handle reuse and overlapping attempts can leave stale state. The injector does not close its process/remote-thread handles, free the remote path allocation or release its local `LoadLibrary` reference. It sleeps instead of waiting on the loader thread, and does not check the second `CreateRemoteThread` result. ASCII `LoadLibraryA` cannot represent every installation path.
- **No guaranteed readiness or disable deadline.** Pipe readiness precedes hook installation. Owner checks occur only through active throttling and timed focus reconciliation; OS events restart that timer. `IsProcessRunning` tests whether a process can be opened, not process exit state or original PID identity. There is no independent owner watchdog, and owner loss does not clear the audio mute set.
- **Presentation interface assumptions.** The code reads slot 22 from the original dummy `SwapChain` vtable without querying `IDXGISwapChain1`. `Present1` belongs to the extended interface, so that slot assumption needs verification for the actual interface/driver. Loading `d3d12.dll` alone does not establish it. See Microsoft's [IDXGISwapChain1 contract](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nn-dxgi1_2-idxgiswapchain1).
- **Shared presentation/focus state.** Both callbacks share one stopwatch and one waitable timer; their behavior assumes suitable presentation serialization. Pipe, event-listener and rendering threads read/write ordinary static fields and restart shared stopwatches without a publication/locking protocol. Do not describe these fields as thread-safe merely because the callbacks are small.
- **Timer failure may spend CPU.** Timer creation failure is not checked. If `SetWaitableTimer` fails, `Sleep` returns immediately, so the surrounding loops can spend CPU until their stopwatch deadline. The wait result is also ignored.
- **Exception ownership is broad.** `VectoredHandler` claims every guard-page and single-step exception, including those not proven to originate from its guarded page or breakpoint. It overwrites/clears `Dr0` without preserving a previous owner and does not establish a per-call stack for reentrant `PostEvent`. These are debugger/other-instrumentation and nested-call risks, not safe invariants to repeat.
- **Audio initialization and mutable state.** Missing `_executeAction` is logged but later invocation is not guarded against null. Protection changes are unchecked and original protection is not restored through a teardown path. `IsMuted` reads while writers sort/shift/clear the same array; locking only the writers does not make those searches consistent. `Rdx` is cast to `uint` before being stored in the `ulong` game-object field, truncating upper bits. Ring slot allocation is atomic but whole records and history reads are not.
- **Protocol success is permissive.** Unknown `A2` commands still receive success. FPS fields are applied progressively, marker validation is partial, mute-list counts have no upper bound, and response reads have no timeout. Clear-and-add uses two connections and is not atomic. Do not treat an acknowledgment as strong transactional validation.
- **Process polling leaks candidates.** `ProcessMonitor.GetUpdatedProcesses` calls `ToProcessInfo` on every matching poll. For unchanged titles, the newly opened handle is discarded without closure; on a title replacement, the old cached handle is replaced without closure. Fixing this requires explicit ownership across cache transitions, not blindly closing the shared cached handle.
- **Sidecar first-chance labeling is suspect.** It reads `isFirstChance` from union offset 24. From the documented x64 exception-record layout, that is `NumberParameters`; `dwFirstChance` follows the 152-byte exception record. The displayed first/second-chance label can therefore be wrong. This offset inference follows Microsoft's [EXCEPTION_DEBUG_INFO](https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-exception_debug_info) and [EXCEPTION_RECORD](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-exception_record) definitions. Also examine `GetMessage`'s Boolean declaration if correcting native message-loop error handling.

## Diagnose and validate changes

| Symptom | First inspection points |
| --- | --- |
| Pipe missing, injection fails, or works only from some paths | `HookService.TryInstallHooksAsync`; published native DLL location/export/architecture; `Global.ThisClientsHandle`; `NamedPipeServer.Initialize` |
| Pipe answers but no FPS/audio effect | `DxHook.Initialize` ordering; debug output; vtable protection/write success; audio export resolution; default/settings mismatch |
| Slow wake from low background FPS | `ThrottleTheFrame` 15 ms chunks and foreground-only escape; `WindowManager.ActivateWindow`; pipe contention/response blocking; predicted-state transitions |
| Periodic unexpected foreground/background state | `SetOurWindowInFocus`, `_ignoreNextLostFocus`, `LastCheckedFocusSw`, `WinEventHook` duplicate suppression, cached HWND |
| FPS setting zero behaves unexpectedly or VSync changes | `A2F1_SetFpsTargets`; predictive fallback; `IsFpsThrottleActive`; forwarded `syncInterval` |
| Audio events missing from history or wrong IDs | `#if DEBUG`, mute-set arming, ring size/filter, register casts, signed timestamp wire type |
| Audio muting crashes/interferes with debugger | `VectoredHandler`, `_executeAction`, exception ownership, DR0/DR6/DR7, page protection, `CONTEXT64` |
| CPU/handles grow with clients or settings changes | timer failure path; guard/single-step frequency; stop-action logging; injector cleanup; `ProcessMonitor` candidate handles |

[CustomAudioTests](../../tests/Eve-O-Preview.Tests/Checks/CustomAudioTests.cs) verifies parser/UI persistence and the host's `A2 C1` then `A2 C3` bytes using a **synthetic named-pipe server**, without injecting a process. [FeatureAvailabilityTests](../../tests/Eve-O-Preview.Tests/Checks/FeatureAvailabilityTests.cs) verifies FPS handler routing with stubs. Neither validates Robin's vtable patch, native exception handler, FPS accuracy or host/client timing races.

For a relevant implementation change, keep host protocol checks and native runtime validation distinct. A controlled Windows x64 native validation should identify the exact published binary, DX mode and game build; exercise enable/disable, zero limits, very low background FPS, actual/predicted switching, owner restart, empty/nonempty audio lists and both present callbacks where reachable. Compare frame-time distribution, switching latency, CPU use and handle counts when tuning performance. Mark unperformed native checks explicitly instead of treating a managed build or mocked test as equivalent evidence.

## Review inventory

Every tracked file originally present in `Eve-O-Preview.Robin` was read in full for this guide: [AudioMuteSystem.cs](../../Eve-O-Preview.Robin/AudioMuteSystem.cs), [DXHook.cs](../../Eve-O-Preview.Robin/DXHook.cs), [DebugLogger.cs](../../Eve-O-Preview.Robin/DebugLogger.cs), [Eve-O-Preview.Robin.csproj](../../Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj), [FocusType.cs](../../Eve-O-Preview.Robin/FocusType.cs), [Global.cs](../../Eve-O-Preview.Robin/Global.cs), [NamedPipeServer.cs](../../Eve-O-Preview.Robin/NamedPipeServer.cs), [NativeMethods.cs](../../Eve-O-Preview.Robin/NativeMethods.cs), [PrecisionSleep.cs](../../Eve-O-Preview.Robin/PrecisionSleep.cs), and [WinEventHook.cs](../../Eve-O-Preview.Robin/WinEventHook.cs). The host's [HookService.cs](../../Eve-O-Preview/Services/Implementation/HookService.cs), [DebuggerSidecar.cs](../../Eve-O-Preview/Services/Implementation/DebuggerSidecar.cs), and [ProcessHelpers.cs](../../Eve-O-Preview/Helper/ProcessHelpers.cs) were also read in full. Linked configuration, handlers, callers and tests were cross-checked for the claims above. This is a static source review, not native execution certification.
