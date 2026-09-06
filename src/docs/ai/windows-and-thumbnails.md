# Windows, thumbnails, cycling, and CPU affinity

Use this guide when a prompt concerns preview rendering, disappearing/overlapping previews, focus changes, client discovery, hotkeys, geometry, or CPU allocation. It describes the source reviewed on 2026-09-06. Follow the linked symbols in the current checkout before changing behavior; the limitations section records findings, not behavior to preserve.


## Start with the correct owner

| Concern | Source and entry points |
| --- | --- |
| Preview lifecycle, visibility policy, switching, cycle order, layout persistence | [ThumbnailManager.cs](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs): `UpdateThumbnailsList`, `RefreshThumbnails`, `SetActive`, `CycleNextClient`, `SwitchActiveClient` |
| Native focus, restore/minimize, capture | [WindowManager.cs](../../Eve-O-Preview/Services/Implementation/WindowManager.cs): `ActivateWindow`, `MakeApiCallsToSetForegroundAndFocus`, `MinimizeWindow`, `GetLiveThumbnail`, `GetStaticThumbnail` |
| Native rendering relationship | [DwmThumbnail.cs](../../Eve-O-Preview/Services/Implementation/DwmThumbnail.cs): `Register`, `Move`, `Update`, `Unregister` |
| Preview geometry, overlay, highlight, mouse gestures, native preview restore | [ThumbnailView.cs](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs): `RestoreAndBringToFront`, `Refresh`, `HighlightThumbnail`, `RefreshOverlay`, custom mouse mode |
| Live image maintenance/recovery | [LiveThumbnailView.cs](../../Eve-O-Preview/View/Implementation/LiveThumbnailView.cs): `RefreshThumbnail`, `RegisterThumbnail`, `ResizeThumbnail` |
| Compatibility capture | [StaticThumbnailView.cs](../../Eve-O-Preview/View/Implementation/StaticThumbnailView.cs), [StaticThumbnailImage.cs](../../Eve-O-Preview/View/Implementation/StaticThumbnailImage.cs) |
| Selecting live versus compatibility view | [ThumbnailViewFactory.cs](../../Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs): `Create` |
| Client enumeration and handle cache | [ProcessMonitor.cs](../../Eve-O-Preview/Services/Implementation/ProcessMonitor.cs), [ProcessInfo.cs](../../Eve-O-Preview/Services/Implementation/ProcessInfo.cs), [ProcessHelpers.cs](../../Eve-O-Preview/Helper/ProcessHelpers.cs) |
| CPU topology and placement | [CpuAffinityService.cs](../../Eve-O-Preview/Services/Implementation/CpuAffinityService.cs): `DetectCores`, `PreCalculateZones`, `UpdateAffinity`, `ResetAll` |
| Hook/FPS/audio boundary | [IHookService.cs](../../Eve-O-Preview/Services/Interface/IHookService.cs); follow its implementation and the hook guide for injected behavior |

`IThumbnailView.Id` is the **EVE source HWND**. `ThumbnailView.Handle` is the **preview form HWND**, and the overlay owns another HWND. `IProcessInfo.ProcessHandle` is a **kernel process handle** used for process operations; `ProcessId` is a PID. These values share integer-like representations but are not interchangeable. `IsKnownHandle` recognizes the source, preview, and overlay so interacting with a preview still counts as client activity.

The `Services/Implementation` folder does not imply a uniform namespace. `ThumbnailManager` and many service interfaces use `EveOPreview.Services`; several implementations use `EveOPreview.Services.Implementation`. Navigate by symbols and DI registrations, not namespace guesses.

## Lifecycle and refresh order

`ThumbnailManager` creates a WPF `DispatcherTimer` while managing WinForms views. Its interval is copied from `ThumbnailRefreshPeriod` in the constructor. `Start` starts the timer and calls `RefreshThumbnails`; first discovery happens in `ThumbnailUpdateTimerTick`, which calls `UpdateThumbnailsList` before `RefreshThumbnails`.

`ProcessMonitor.GetUpdatedProcesses` enumerates processes, accepts `ExeFile` case-insensitively, and skips processes without a main window. It keys `ProcessCache` by the source HWND and returns added/renamed/removed records. Titles are full window titles. The literal `EVE` denotes the login screen; do not apply named-client layout behavior to it. `GetMainProcess` delays caching the preview application's own window until the main window has initialized.

For each added source, `UpdateThumbnailsList`:

1. Creates a live/static view through the factory and assigns source HWND, title, size, and font.
2. Applies overlay, frame, size-limit, topmost, and initial location settings. Login previews use `LoginThumbnailLocation`; named previews use `GetThumbnailLocation(title, activeClientTitle, currentLocation)`.
3. Adds it to `_thumbnailViews`, inserts its source HWND at the **oldest** end of `_thumbnailActivationOrder`, and attaches manager callbacks.
4. Applies a stored client-window layout and starts `TryInstallHooksAsync` without awaiting it.

Renames update `view.Title`, publish removed/added title entries, and reapply client layout. Removal removes both dictionary/list entries, clears most callbacks, closes the view/overlay, and publishes `ThumbnailListUpdated` when the named list changes. Discovery is `async void`; its final mediator publication can still be running when the timer proceeds to refresh.

`RefreshThumbnails` executes these policies in order:

1. Return immediately for a null foreground HWND, a transient state during activation. Avoid saving/hiding/reordering against that unknown state.
2. Identify an EVE/preview/overlay HWND or the main application window. Mark z order dirty for a foreground transition into an EVE/main window, or an always-on-top setting change.
3. Update the active client when the foreground HWND is a known **source** HWND; remember other non-client windows in `_externalApplication`.
4. Compute focus-loss hiding, then count down `_hideThumbnailsDelay`. This is a count of refresh rounds, not milliseconds.
5. Force rendering maintenance every two non-null-foreground refresh calls (`FORCED_REFRESH_CYCLE_THRESHOLD = 2`). Additional explicit refresh calls also affect that count.
6. Suppress view callbacks; process delayed location saving/snapping when no hover effect is active.
7. Hide disabled previews, previews subject to global focus-loss hiding, or the configured active-client preview. Otherwise apply location/opacity/topmost when not hovering, overlay settings, and active highlight; then show or refresh.
8. Restore dirty thumbnail z order and reenable callbacks.

`HideActiveClientThumbnail` and per-title disabling are independent checks. A hidden preview remains in `_thumbnailViews`, and cycling filters by running titles, not preview visibility. `_activeClient` remembers the last selected EVE client even when an external app takes foreground.

## Preserve these rendering and responsiveness decisions

### DWM is a persistent live relationship

The normal preview is a DWM source-to-destination relationship, not a screenshot polled every timer tick. [DwmThumbnail.Register](../../Eve-O-Preview/Services/Implementation/DwmThumbnail.cs) enables visibility, full DWM opacity, destination rectangle, and source-client-area-only rendering. Window/overlay opacity is handled separately by `ThumbnailView.SetOpacity`.

[LiveThumbnailView.RefreshThumbnail](../../Eve-O-Preview/View/Implementation/LiveThumbnailView.cs) deliberately retains a healthy relationship:

- When a relationship exists and `forceRefresh` is false, return immediately.
- On forced maintenance, call `IDwmThumbnail.Update`. A successful update keeps the existing relationship.
- When absent or unusable, register/move/update the replacement **before** unregistering the obsolete relationship.

Do not replace this with unconditional unregister/register on switching, highlighting, or every forced refresh: the source comment identifies a visible image gap. Do not gate live maintenance on which source is foreground; healthy background previews also need persistent relationships and failed relationships need recovery. `ResizeThumbnail` caches its rectangle and only submits changed destination bounds.

`DwmThumbnail.Update` returns false for unavailable composition, a zero relationship handle, or caught `ArgumentException`/`COMException`. This return is the recovery signal. `Move` only changes stored properties; `Update` submits them. The imports in [DwmNativeMethods.cs](../../Eve-O-Preview/Services/Interop/DwmNativeMethods.cs) use `PreserveSig = false`, so this error path is exception-based; changing the interop contract requires changing the callers too.

### Native visibility and managed intent are different

`ThumbnailView.IsActive` is the manager's intent that a preview should be shown. It is not source-window focus and is not equivalent to native `IsWindowVisible` or WinForms' cached `Visible`/`TopMost` values. `RestoreAndBringToFront` must leave an intentionally hidden preview hidden.

For an intended-visible preview, [RestoreAndBringToFront](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs) restores iconic owner/overlay HWNDs with `SW_SHOWNOACTIVATE`, reasserts topmost, then uses native `SetWindowPos` on the image followed by the overlay with:

```text
HWND_TOPMOST
SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER
```

The order leaves each label above its image. `SWP_NOOWNERZORDER` prevents raising an overlay from unexpectedly reordering its owner. The native calls repair visibility/topmost even when managed cached state says nothing changed. Keep `SetLastError = true` on `SetWindowPos` so manager warnings can report the native failure.

This method deliberately does **not** call `Form.Show`, image refresh, or DWM registration. Its source comment documents that showing an owned WinForms form can focus its active control even with `ShowWithoutActivation`. Native restoration must preserve both EVE keyboard focus and existing rendered images; rendering maintenance belongs to the normal refresh path.

### Z order follows activation history and changes only when needed

`_thumbnailActivationOrder` stores oldest first and newest last. New previews enter at index 0; activation removes/reappends the source HWND. `RestoreThumbnailZOrder` raises intended-visible previews in this order, leaving the most recently activated preview on top among overlapping previews. It runs only when `_refreshThumbnailZOrder` is dirty and `ShowThumbnailsAlwaysOnTop` is enabled. Failed raises dirty the next retry.

`RaiseActivatedThumbnail` updates the order and raises the selected visible/enabled preview immediately, before client activation, layout work, and timer maintenance. Both `SetActive` and the click activation callback use it. It skips immediate native raising when always-on-top is off, the preview is hidden/disabled, or the active-client-preview-hiding option is enabled.

Keep the immediate raise and periodic repair as separate responsibilities. Reordering every refresh can disturb other topmost windows; relying only on cached `TopMost` cannot repair native visibility/z-order changes.

### Compatibility mode has different cost and lifetime rules

The factory captures `EnableCompatibilityMode` at construction. Static views use `GetDC`/`GetClientRect`/compatible bitmap/`BitBlt` in [WindowManager.GetStaticThumbnail](../../Eve-O-Preview/Services/Implementation/WindowManager.cs); capture occurs only on forced refresh and rejects either dimension below 300. `StaticThumbnailView` replaces a non-null image and disposes the previous image. Null capture keeps the last valid image.

[StaticThumbnailImage.WndProc](../../Eve-O-Preview/View/Implementation/StaticThumbnailImage.cs) returns `HTTRANSPARENT` for `WM_NCHITTEST` so the picture control does not intercept the form's mouse behavior. Preserve this when changing rendering controls.

## Focus, switching, and prediction

[WindowManager.ActivateWindow](../../Eve-O-Preview/Services/Implementation/WindowManager.cs) sends `TellEveClientFocusIsComingAsync` without awaiting it, then marks `IsCurrentlySwitching`, attempts `SetForegroundWindow` and `SetFocus`, falls back to `SwitchToThisWindow` when the foreground call fails, and asynchronously restores a source with `WS_MINIMIZE`. `finally` clears the switching flag. The flag describes this synchronous attempt; it does not await the OS restore or hook work and is not a queue/mutex.

`SwitchActiveClient` handles the previous source: if `MinimizeInactiveClients` is enabled and the old title is not a priority client, minimize it without animation, then update active state/order. Merely switching to a non-EVE window does not run this path to minimize clients.

`MinimizeWindow(handle, true)` sends `WM_SYSCOMMAND/SC_MINIMIZE`; `false` edits `WINDOWPLACEMENT.showCmd` and calls `SetWindowPlacement`. Preserve this distinction when changing switch latency. An explicit thumbnail minimize uses the animated path; inactive-client minimization uses the non-animated path.

There are two activation paths in [ThumbnailManager](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs):

| Trigger | Current sequence |
| --- | --- |
| Cycle hotkey | Compute next and next-next titles; send `UpdateCpuAffinity(next, nextNext, oldActive)`; `SetActive(next)`; `PredictUpcomingClient(nextNext)` |
| Thumbnail click | Immediately raise; `Task.Run` sends affinity update with the clicked source only and activates its source; continuation via `TaskScheduler.FromCurrentSynchronizationContext` switches active state, updates client layouts, and refreshes previews |

The click continuation deliberately returns to the current UI synchronization context. Preserve that boundary around WinForms operations. The cycle path is synchronous from the hotkey delegate; neither path awaits all mediator sends. `PredictUpcomingClient` skips zero HWNDs and sends `TellEveClientFocusIsMaybeComingSoonAsync` to prepare the following client. The hook implementation owns what preparation means; thumbnail rendering and source-game FPS are separate paths.

## Hotkeys and cycle semantics

`RegisterAllHotkeys` removes every tracked down/up delegate before rebuilding group and general bindings. It runs in the constructor and on `GlobalEvents.CurrentProfileChanged`. [GlobalEvents.cs](../../Eve-O-Preview/Services/Implementation/GlobalEvents.cs) invokes subscribers directly; it does not marshal threads or queue notifications.

`FindNextClientInCycleGroup` uses full exact titles, removes configured titles with no running preview entry, orders integer positions ascending/descending, and wraps to the first remaining entry. When the active title is absent from that group, cycling starts at that first entry. An empty filtered group produces no selected view, and `SetActive` ignores the null view.

Cycle key-down checks `e.KeyData`, ignores `Keys.None`, ignores a press during `IsCurrentlySwitching`, then cycles and marks the event handled. General hide/minimize actions run on key-up intentionally, to reduce interference with cycling. [HotkeyHelpers.ToHotkeys](../../Eve-O-Preview/Helper/HotkeyHelpers.cs) uses `KeysConverter.ConvertFromInvariantString`; empty/invalid input becomes `Keys.None` and invalid input is logged.

Do not silently normalize `KeyCode` and `KeyData` as equivalent. Current cycle key-up compares `KeyCode` with the stored hotkey; general toggle-hide compares `KeyData`, whereas general minimize compares `KeyCode`. Modified-chord behavior needs explicit down/up regression coverage when edited. Follow `CycleGroup` configuration parsing and the main form's other keyboard subscriptions when investigating conflicts or duplicate handling.

## Geometry, hovering, and event feedback

The [ThumbnailView](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs) comment explicitly keeps current size/position management in the view for responsiveness. Do not route high-frequency mouse moves through configuration persistence or mediator handlers merely to enforce a stricter presenter pattern.

- `Refresh` maintains the image, highlight rectangle, and overlay, in that order. `_isSizeChanged`/`_isLocationChanged` gate unnecessary work; live/static rectangle setters also skip unchanged values.
- `SetOpacity` maps values at or above 0.9 to 1.0 and ignores differences below 0.1. The overlay uses 1.0 above 0.8, otherwise `1.0 - (1.0 - opacity) / 2`. Its implementation, not the nearby prose comment, is the precise formula.
- `SuppressResizeEvent` ignores resize events for 500 ms after operations known to produce inconsistent WinForms `ClientSize` events. Manager `_ignoreViewEvents` separately suppresses feedback while programmatically changing multiple views.
- Highlighting shrinks the image inside the form's background border. The width calculation preserves the original client-area aspect ratio instead of subtracting an equal horizontal border unconditionally.
- Hover enters by saving base size/location, enabling a global manager hover flag, setting full opacity/topmost, and optionally zooming. While this flag is set, periodic location, opacity, topmost, and snap updates are suppressed so they do not fight the hover state.
- `ZoomIn` changes size **before** location. The source describes a focus-lost/focus-regained oscillation if this ordering is reversed. It temporarily removes maximum size; `ZoomOut` restores saved size, maximum size, and location. The nine [ViewZoomAnchor](../../Eve-O-Preview/View/Interface/ViewZoomAnchor.cs) values specify the anchor.
- A user resize propagates the selected client size to every thumbnail and publishes `ThumbnailActiveSizeUpdated`. A move updates configuration immediately but delays its save notification/snapping by two eligible refresh rounds. Repeated movement resets that delay; changing the queued source or active-client context flushes the previous notification.
- Snapping requires `EnableThumbnailSnap` and borderless previews. It checks nine selected corner pairs, using a threshold of `max(20, dimension / 10)`, then snaps to the first matching other preview. Preserve the distinction between client size and decorated window size.
- Custom Move/Resize uses global mouse-move/up subscriptions only while active. A context menu offers both; holding right-click for the [designer's 350 ms timer](../../Eve-O-Preview/View/Implementation/ThumbnailView.Designer.cs) starts drag movement and applies the offset already travelled. Shift-resize maintains the starting ratio. Exiting custom mouse mode unsubscribes the handlers.

The [overlay](../../Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs) is an owned borderless form with fuchsia transparency key, an outlined title, and a transparent picture area whose mouse-up forwards to the preview's click handler. `ShowThumbnailOverlays` controls the label; the overlay form itself participates in positioning, ownership, and native restore. Both preview and overlay set `ShowWithoutActivation` and `WS_EX_TOOLWINDOW` and omit taskbar entries. Title display removes `EVE - `; configuration/client identity keeps the full title.

Client-window layouts are separate from thumbnail locations. `ApplyClientLayout` moves/maximizes named clients with saved layouts when discovered or renamed. `UpdateClientLayouts`, currently called after click activation, saves each named client's geometry if maximized or if both coordinates are strictly between -10,000 and 31,000 and both dimensions exceed 10. These guards prevent saving typical minimized/off-screen sentinel positions; they are not general multi-monitor validation.

## CPU affinity is a precomputed scheduling strategy

[CpuAffinityService](../../Eve-O-Preview/Services/Implementation/CpuAffinityService.cs) detects topology and precomputes masks once in its constructor. `PCores`/`ECores` actually contain **logical processor bit indices**, including SMT siblings, rather than objects representing physical cores.

| Detected performance threads | Active | Predicted next | Previous | Background if no E threads |
| --- | --- | --- | --- | --- |
| At least 8 | First 2 | Next 2 | Next 2 | Remaining indices after 6 |
| 4–7 | First 1 | Next 1 | Next 1 | Remaining indices after 3 |
| Below 4 | Automatic affinity unsupported | — | — | — |

If E threads exist, their mask replaces the background mask. The service does not set priority class (`SetPriorityClass` is commented out); comments about spare OS capacity describe intent, not an exclusive CPU reservation.

`UpdateAffinity` returns unless CPU support and `EnableAutomaticCpuAffinity` permit it. A missing active record falls back to next, then previous. It applies active/next/previous masks directly and uses `_currentBackgroundHandles` to skip redundant native background assignments. Removing those foreground-role handles from the set allows later transitions back to background to be applied. `ResetAll` restores the computed all-core mask only after the service has run, then clears tracking. Preserve reset wiring when changing configuration/shutdown behavior.

Use source HWNDs to look up `IProcessInfo` before calling this service; apply affinity using its kernel `ProcessHandle`. Do not enumerate processes or recompute topology inside the keypress path.

## Observed limitations and investigation leads

These are findings from source inspection. They were not fixed or reproduced on a live game during this documentation review. Do not turn them into compatibility requirements or claim complete resource/thread safety.

| Area | Source-observed concern and a useful investigation |
| --- | --- |
| Process handles | `ProcessHelpers.ToProcessInfo` opens a raw kernel handle on each call. `GetUpdatedProcesses` calls it for every monitored process on every scan, discards the newly opened handle on unchanged titles, and replaces cached records on title change without closing the old handle. Only removed cached records are explicitly closed there. Investigate growing handle counts before speeding up polling. |
| Static capture cleanup | `GetStaticThumbnail` obtains a source DC before the less-than-300 check, then returns null without releasing it. The success path releases/deletes GDI resources but has no `finally` for failures. |
| Lifecycle cleanup | `ThumbnailManager.Stop` only stops its timer; it does not unsubscribe tracked keyboard/profile handlers or close views. `LiveThumbnailView` has no explicit close/dispose override to unregister its current DWM relationship. `ThumbnailView` has component-owned timers/menu objects but its designer has no `Dispose` override; custom global mouse handlers are removed on normal mode exit, not explicitly on `Close`. Audit actual lifetime/OS cleanup before introducing repeated manager/view recreation. |
| Threading | Process-cache writes do not take the lock used by `GetAllProcesses`; `GetAllKnownClients` returns the live mutable preview dictionary. The switching flag is a plain boolean, and affinity operations are not serialized as one entire update. Existing partial locks do not make arbitrary concurrent access safe. |
| CPU classification | `DetectCores` sends every logical bit with `EfficiencyClass == 0` to `ECores`. The fallback fills `PCores` only when **both** lists are empty, so a topology reported entirely as class 0 results in zero P threads and disabled automation. This differs from the nearby fallback comment's broader claim about older/AMD CPUs. |
| CPU groups | The topology binding represents one group mask and placement builds one 64-bit mask; group IDs are not used by the strategy. Do not assume correct support for multiple processor groups or more than 64 logical processors. Verify current Windows layouts/API behavior before extending this. |
| Affinity result/aliasing | Native `SetProcessAffinityMask` results are ignored, including before adding background handles to the cache. Active/next/previous can be the same client in small cycle groups, and successive mask applications then overwrite earlier roles. `ResetAll` restores all detected cores, not a saved pre-existing affinity restriction. |
| Cached settings | Factory compatibility mode and initial title font are captured at construction; the timer interval is too. `UpdateThumbnailTitleFont` updates existing views, but the factory retains its original font reference. Trace factory/manager reconstruction and profile changes before promising live updates. |
| Active title after rename | `SwitchActiveClient` returns early when the HWND matches, so title rename alone does not refresh `_activeClient.Title` there. Investigate login-to-character or character-change cycle/layout mismatches. |
| Highlight updates | `SetHighlight(bool, int)` returns when the requested enabled state is unchanged, so a new thickness/color may wait for a state transition. `SetDefaultBorderColor` resets a lazy color lookup rather than immediately repainting the active border. |
| Native success | Most native focus/minimize/geometry operations do not propagate success; a log saying activation completed is not proof that Windows granted foreground focus. `IsCompositionEnabled` is cached on `WindowManager` construction. |

## Symptom-to-source navigation

| Symptom or requested change | Inspect first, then follow |
| --- | --- |
| Preview briefly blanks when rapidly cycling | `LiveThumbnailView.RefreshThumbnail` and `DwmThumbnail.Update`; ensure healthy registrations survive and native reordering never refreshes/re-registers images |
| Preview stays behind another preview, or disappears after native minimize/desktop activity | `RaiseActivatedThumbnail`, `RestoreThumbnailZOrder`, `RestoreAndBringToFront`; compare manager `IsActive`, native iconic state, image/overlay z order, dirty triggers, and always-on-top setting |
| Typing goes into a preview/overlay instead of EVE | Preview native restore flags/owned form behavior, then `WindowManager.MakeApiCallsToSetForegroundAndFocus`; avoid solving image visibility by activating preview forms |
| Preview freezes but the game remains responsive | Determine live versus compatibility view; inspect DWM `Update`/recovery or forced static capture. For source FPS throttling, cross into `IHookService` and the injected runtime |
| EVE source switch takes too long | Hotkey delegate → cycle affinity/prediction → `WindowManager.ActivateWindow` → hook service; keep UI work, native activation, and game-frame wake-up measurements distinct |
| Wrong client/no client cycles | Full exact window titles, process discovery/rename, selected profile's `CycleGroup.ClientsOrder`, filtering/wrap logic, parsed `Keys`, down/up handling |
| Previews vanish when interacting with the app | `IsKnownHandle`, `IsMainWindowActive`, `HideThumbnailsOnLostFocus`, tick-count delay, disabled title list, and active-preview hiding |
| Drag/zoom fights itself or persists a zoomed size | View geometry ordering, saved base state, `_isHoverEffectActive`, `_ignoreViewEvents`, 500 ms resize suppression, delayed save queue |
| Layout differs after changing active clients | Full title/context keys passed to `GetThumbnailLocation`/`SetThumbnailLocation`, rename behavior, login `EVE` exception, then configuration implementation |
| Background clients consume unexpected CPU or affinity has no effect | Topology lists/masks, config feature availability, `UpdateCpuAffinity` handler, prediction, background-handle cache, native results and processor-group limitations |
| Resource count grows over time | Process handle polling/replacement, compatibility GDI early return, view/overlay/component cleanup, and hotkey/custom mouse subscriptions |

For behavior changes, use the existing [thumbnail z-order checks](../../tests/Eve-O-Preview.Tests/Checks/ThumbnailZOrderTests.cs) and [live-thumbnail checks](../../tests/Eve-O-Preview.Tests/Checks/LiveThumbnailTests.cs), then the test guide's documented execution path. Cover hidden previews, owner/overlay ordering, preserved foreground focus, healthy versus failed DWM relationships, overlap/MRU behavior, source minimize/restore, compatibility images, hover, and relevant feature settings. Only broaden tests to behaviors affected by the edit.

## Reviewed-file coverage

This guide's review read the complete contents of the following files, including designer wiring and `.resx` metadata. `HookService.cs` and `DebuggerSidecar.cs` belong to the separate hook-runtime review; main-form/presenter/configuration/mediator/test implementation coverage belongs to the other repository guides.

- `Eve-O-Preview/Services/Implementation/`: `CpuAffinityService.cs`, `DwmThumbnail.cs`, `GlobalEvents.cs`, `ProcessInfo.cs`, `ProcessMonitor.cs`, `ThumbnailManager.cs`, `WindowManager.cs`.
- `Eve-O-Preview/Services/Interface/`: `ICpuAffinityService.cs`, `IDwmThumbnail.cs`, `IGlobalEvents.cs`, `IHookService.cs`, `IProcessInfo.cs`, `IProcessMonitor.cs`, `IThumbnailManager.cs`, `IWindowManager.cs`, `InteropConstants.cs`.
- `Eve-O-Preview/Services/Interop/`: `DWM_BLURBEHIND.cs`, `DWM_THUMBNAIL_PROPERTIES.cs`, `DWM_TNP_CONSTANTS.cs`, `DwmNativeMethods.cs`, `Gdi32NativeMethods.cs`, `KernelNativeMethods.cs`, `MARGINS.cs`, `RECT.cs`, `User32NativeMethods.cs`, `WINDOWPLACEMENT.cs`.
- `Eve-O-Preview/View/Implementation/`: `LiveThumbnailView.cs`, `LiveThumbnailView.resx`, `StaticThumbnailImage.cs`, `StaticThumbnailView.cs`, `ThumbnailDescription.cs`, `ThumbnailOverlay.cs`, `ThumbnailOverlay.Designer.cs`, `ThumbnailOverlay.resx`, `ThumbnailView.cs`, `ThumbnailView.Designer.cs`, `ThumbnailView.resx`, `ThumbnailViewFactory.cs`.
- `Eve-O-Preview/View/Interface/`: `IThumbnailDescription.cs`, `IThumbnailView.cs`, `IThumbnailViewFactory.cs`, `ViewZoomAnchor.cs`.
- `Eve-O-Preview/Helper/`: `HotkeyHelpers.cs`; additionally `ProcessHelpers.cs` to trace raw handle ownership.

The three reviewed thumbnail `.resx` files contain schema/designer metadata, with no bitmap payload: thumbnail context-menu/tooltip/timer tray positions, overlay local-control generation metadata, and an empty live-view resource set.
