# Robin: injected NativeAOT runtime

Updated 2026-09-06 after the defect investigation. The desktop host and Robin run in separate processes. Read [DXHook.cs](../../Eve-O-Preview.Robin/DXHook.cs), [PrecisionSleep](../../Eve-O-Preview.Robin/PrecisionSleep.cs), [AudioMuteSystem](../../Eve-O-Preview.Robin/AudioMuteSystem.cs) and both [host](../../Eve-O-Preview/Services/Implementation/HookService.cs)/[server](../../Eve-O-Preview.Robin/NamedPipeServer.cs) endpoints before changing native behavior.

## Build and process boundary

Robin targets .NET 10, x64, `PublishAot=true`, `NativeLib=Shared`. Publish it; a managed build DLL cannot be injected. `Initialize` is `uint Initialize(IntPtr)` with unmanaged Stdcall, matching a Windows thread entry point. Initialization is guarded once per loaded module. SharpDX supplies the dummy D3D11 device/swapchain; the extended swapchain interface is queried through the native COM ABI because SharpDX's reflective generic wrapper construction failed in the published AOT test.

Desktop Autofac, MediatR and Serilog do not extend into Robin. Frame and audio exception callbacks must avoid recurring allocations, routine logging and IPC. Settings, pipe parsing and explicit diagnostics may allocate off those paths. A render thread creates its precision timer on first use; do not describe the complete runtime as GC-free.

## Injection and ownership lifecycle

`HookService.TryInstallHooksAsync` deduplicates in-flight work by PID/HWND, pings first, and skips new injection if neither FPS nor audio is requested. Failure permits retry. Discovery forgets per-client protocol capability state when a client disappears.

For injection, the host hashes the published DLL next to `AppContext.BaseDirectory`, copies it to `%TEMP%/Eve-O Preview/Robin/<SHA256>/Eve-O-Preview.Robin.dll`, and loads that immutable copy. It uses UTF-16 `LoadLibraryW`, waits for remote loader/initializer threads, maps the PE locally with `DONT_RESOLVE_DLL_REFERENCES` to calculate the export offset, and releases local mappings, process/thread handles and remote arguments. A timed-out loader retains its argument and handles until that thread finishes; freeing memory that it may still read would be unsafe. The installation DLL remains replaceable while clients run.

The pipe starts before DXGI/audio initialization. Ping proves pipe responsiveness only. Query `A1 B6` for Present installation, `A1 B8` for audio monitor readiness, and `A1 B7` for informational version plus the SHA-256 of the actually loaded DLL. These statuses do not prove every game swapchain is intercepted. `10.0.0.12` identifies the focus-handoff revision; the hash distinguishes local rebuilds. An old injected binary is reused until the client exits. Do not force-unload patched callbacks.

`Global.ThisClientsHandle` is captured from `Process.MainWindowHandle`; initialize only after a real main window exists. `A2 B4` claims a positive owner PID and pins it with a SYNCHRONIZE handle. A separate one-second watchdog checks that handle, independent of rendering/FPS settings, and disables FPS, mute state and capture after owner exit. Normal host shutdown also sends zero FPS and clears audio/capture. The module remains loaded until the client exits.

## Frame pacing and focus

The dummy swapchain supplies Present slot 8. A successful native QueryInterface for IDXGISwapChain1 supplies Present1 slot 22. Both original pointers stay rooted for module lifetime. Disabled states preserve the caller's sync interval; limited states use zero. TEST and DO_NOT_WAIT calls bypass pacing. A thread-static recursion guard prevents nested Present1/Present calls from pacing twice.

FPS targets are one immutable snapshot, atomically published. Native startup is disabled. Values 1-1000 are valid; zero/out-of-range disables that state. Foreground or background must be nonzero to enable the limiter. Predicted uses its target when positive, otherwise background. Desktop defaults remain disabled, 144 foreground / 20 background / 45 predicted.

Each presenting thread has its own timestamp and timer. Long waits retain 15 ms chunks and reread both focus and targets, including the final remainder/spin. The existing A3 B1 pipe command calls PrepareForFocus, publishing a 250 ms unthrottled handoff interval before updating focus. This releases Present even if foreground, background and predicted targets are all 1 FPS, and gives the client time to process activation. Normal configured pacing resumes afterward. This adds only timestamp state; no extra IPC channel, native hook or per-frame allocation is introduced. High-resolution timer creation falls back to a regular timer; waits are bounded and failure falls back to sleeping. Multiple swapchains on one presenting thread still share a frame clock; this is a validation boundary.

Foreground events update cached focus. Reconciliation checks Windows after three seconds, or the predicted timeout (1-30000 ms). Prediction ignores one immediate lost-focus notification; foreground clears that suppression, and timeout reconciliation bypasses it. The owner watchdog no longer runs from the Present callback. The WinEvent thread retains its message pump and distinguishes GetMessage error (-1) from quit (0).

## Pipe contract: change both ends together

The current-user ACL pipe is `EveoRobin_<decimal HWND>`, byte mode, one connection/request per instance. Integers are little-endian. Both endpoints use async I/O. Host settings/query operations have a total one-second gate/connect/write/read budget. Focus/prediction first attempt Connect(0) and issue the small existing command on the caller thread, before returning to Windows activation. Overlapped completion handles write cleanup without blocking on old zero-buffer servers. A busy/unavailable pipe retains a bounded 150 ms async fallback; focus does not queue behind the host settings semaphore. A connected peer cannot block a response indefinitely. The listener gives each connection one second, including waiting for the peer to consume a reply and close, then disposes/recreates the instance. This avoids reusing a stream left Broken after EOF.

Complete bounded payloads are read before mutation. Malformed, truncated, disconnected and stalled requests are not fatal listener failures. Operational failures retry; only repeated listener failures disable native features. No in-game failure message box is shown.

| Request | Payload | Reply |
| --- | --- | --- |
| `A3 B1` | none | none; immediate focus handoff, temporarily release pacing |
| `A3 B3` | Int32 timeoutMs | none; predicted focus |
| `A2 B4` | Int32 ownerPid | 01 |
| `A2 F1` | Int32 foreground, F2, Int32 background, F3, Int32 predicted | 01 |
| `A2 C1` | none | 01; clear mute set and disable capture |
| `A2 C2` / `A2 C3` | Int32 count, UInt32 IDs | 01; remove/add |
| `A2 C6` | Int32 count, UInt32 IDs | 01; atomically replace mute set |
| `A2 C7` | byte Boolean | 01; opt-in bounded audio history capture, available in Release |
| `A1 B2` | none | 01; ping |
| `A1 B5` | none | 02; atomic audio replacement supported |
| `A1 B6` | none | byte Boolean; Present hook installed |
| `A1 B7` | none | Int32 UTF-8 byte length, UTF-8 version and SHA256 text |
| `A1 B8` | none | byte Boolean; audio monitor ready |
| `A1 A1` | none | F1, Int32 foreground, F2, Int32 background, F3, Int32 predicted, 01 |
| `A1 C4` | none | Int32 count, UInt32 muted IDs |
| `A1 C5` | none | Int32 count, records: UInt32 event, UInt64 game object, Int64 stopwatch timestamp |

Audio lists are capped at 1024 IDs. The host deduplicates presets/custom IDs before updating. Capability discovery selects atomic replacement; legacy Robin uses clear/add under one host gate and checks the clear acknowledgment. Version responses are bounded to 512 bytes. Capture history is capped at 128 records; timestamps are Stopwatch ticks, not UTC.

## Audio interception and remaining native risks

Both decorated exports in `_audio2.dll` must resolve before installing the VEH. Their strings in `InstallAudioMonitor` are game-specific ABI assumptions. The monitor preserves the code page's original protection and refuses an already guarded page or one shared with the DLL entry point. The latter was exposed by a synthetic DLL whose CRT detach code occupied the guarded page: re-entering a shutting-down NativeAOT thread can fail fast. The minimal test audio DLL has no loader entry point. Recheck code layout after EVE updates.

A guard exception on Robin's page captures exact PostEvent entry arguments and a return breakpoint. Other guard pages continue to the next handler. A fixed 16-entry thread-local stack tracks nested PostEvent calls and the full 64-bit game object. Foreign enabled DR0 slots are not overwritten; saved register state is restored, stale state after unwind is discarded, and coincident foreign exceptions continue searching. Windows native testing also exposed trap-flag steps with DR6 zero; rearming tracks the step Robin requested instead of requiring BS alone.

The Wwise action ABI is Stop=0, Pause=1, Resume=2; the previous Stop=1 definition paused playback and could retain voices after clearing. This is pinned independently in the native fixture against [Audiokinetic AkSoundEngine.h](https://github.com/audiokinetic/WwiseIncludes/blob/master/SDK/include/AK/SoundEngine/Common/AkSoundEngine.h).

At a matching return breakpoint, a nonzero returned playing ID is stopped only if its event ID is in the current immutable sorted mute snapshot. Writers allocate/sort once on the pipe thread; callback lookup takes no lock. Routine stop-action logging was removed. Explicit capture uses a preallocated ring with per-slot publication versions; contention drops a sample rather than blocking. `A2 C7` warms/clears the ring before enabling recording. Clear/shutdown/owner exit turn capture off.

PAGE_GUARD is page-wide and clears on access, not a byte-specific detour. Concurrent threads can miss an event during the rearm interval; debugger interaction, unusual unwinds, loader code sharing the page, and nesting beyond the bounded stack remain limitations. This mechanism cannot promise every event is intercepted. See [Microsoft guard-page semantics](https://learn.microsoft.com/en-us/windows/win32/memory/creating-guard-pages).

Preset IDs remain in HookService: jump tunnel 3689163958/1537508544/1768044352; location banner 2377891014/3090840445. Custom IDs are unsigned decimal values; do not narrow them to signed Int32.

## Diagnostics and validation

[DebuggerSidecar](../../Eve-O-Preview/Services/Implementation/DebuggerSidecar.cs) attaches to the desktop app, not EVE. Its x64 first-chance field is at offset 152. Robin writes startup/failure diagnostics through OutputDebugString. Neither diagnostics nor a responsive pipe establish end-to-end frame timing.

Use the optional [native smoke test](../../tests/Robin.NativeSmoke/README.md) after publishing to exercise production injection, version/hash, malformed/stalled pipes, DXGI Present/Present1 pacing, synthetic nested audio, foreign guard forwarding, owner exit and shutdown. Its separate warmed allocation checks compile the production source in managed form; they are not a native GC trace or full VEH allocation measurement.

Real EVE validation on 2026-09-06 loaded 10.0.0.11 with the matching hash into a fresh Niko Meko process, reported both hook statuses ready, and captured repeated UI events. The user confirmed fitting rig-toggle event 1384800364 became silent and returned after the mute cleared. See [defect status](reported-bugs.md) for current results and remaining desktop/performance checks. Mixed-monitor/DPI preview behavior, long-session browser stutter, multiple swapchains/GPUs and hybrid CPU latency still require representative live measurements.
