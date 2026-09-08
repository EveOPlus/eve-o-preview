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

After releasing its cache lock, discovery supplies a snapshot to the shared
[character identity cache](character-identities.md). This schedules missing or
weekly identity refreshes in the background. It never performs ESI requests or
command-line reads within discovery's lock, and does not alter preview handles,
focus, injection or cycle identity keys.

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

The workspace's advanced settings use `ThumbnailRuntimeSettingsUpdatedHandler` → `ThumbnailManager.ApplyRuntimeSettings`, also called after profile changes. Ordinary edits reuse current views/DWM relationships while updating interval, hide delay, size bounds, login position and appearance. A changed `EnableCompatibilityMode` closes existing views and rebuilds them using the existing process cache and factory. This exception is required to switch between live and static renderers; it must not run for an ordinary color, layout or timer edit. Private-desktop `SettingsIntegrationTests` covers both directions of the renderer switch and retained view identity for ordinary edits.

The factory reads the current `EnableCompatibilityMode` when creating each view. Static views call [WindowManager.GetStaticThumbnail](../../Eve-O-Preview/Services/Implementation/WindowManager.cs) on forced refresh. The same helper supplies the settings sample's one-shot background. It rejects zero/invalid/minimized HWNDs and either client dimension below 300, then uses `PrintWindow(PW_CLIENTONLY | PW_RENDERFULLCONTENT)` into a fresh memory bitmap. If that produces no content, a traditional `PW_CLIENTONLY` rendering request supports GDI windows without a compositor surface. Failed or sampled all-black results return null. It never reads a display DC or falls back to `GetDC`/`BitBlt`: live testing exposed monitor/occluding content from the earlier display-copy path despite passing an EVE HWND. `StaticThumbnailView` replaces a non-null image and disposes the previous image. Null capture keeps the last valid image.

`PrintWindow` is synchronous; the settings sample runs it on its existing background task and does not recapture during edits. Compatibility mode retains its synchronous forced-refresh schedule; this is separate from normal DWM preview rendering. The `WorkspacePreviewRenderingTests` covered-client scenario verifies actual client/child pixels under another opaque window, no focus change, and no image for unsupported, closed or minimized targets. Real background-EVE capture must be checked separately from private-desktop GDI tests.

[StaticThumbnailImage.WndProc](../../Eve-O-Preview/View/Implementation/StaticThumbnailImage.cs) returns `HTTRANSPARENT` for `WM_NCHITTEST` so the picture control does not intercept the form's mouse behavior. Preserve this when changing rendering controls.

## Focus, switching, and prediction

[WindowManager.ActivateWindow](../../Eve-O-Preview/Services/Implementation/WindowManager.cs) validates the HWND with IsWindow, issues the existing pipe wake, restores a minimized source asynchronously, then immediately calls SetForegroundWindow/SetFocus with the existing SwitchToThisWindow fallback. There is no WM_NULL responsiveness probe: a throttled client must receive the wake before any focus request, and must not be rejected because it is waiting in Present. IsCurrentlySwitching covers the synchronous attempt and is cleared in finally. Windows activation may still complete asynchronously in the target input queue.

`SwitchActiveClient` handles the previous source: if `MinimizeInactiveClients` is enabled and the old title is not a priority client, minimize it without animation, then update active state/order. Merely switching to a non-EVE window does not run this path to minimize clients.

`MinimizeWindow(handle, true)` asynchronously posts `WM_SYSCOMMAND/SC_MINIMIZE`; `false` edits `WINDOWPLACEMENT.showCmd` and calls `SetWindowPlacement`. Preserve this distinction when changing switch latency. An explicit thumbnail minimize uses the animated path; inactive-client minimization uses the non-animated path.

Cycle hotkeys, direct client hotkeys and thumbnail clicks share synchronous SetActive behavior: raise the preview, commit the selected client, apply both old/new borders, thumbnail locations and active-preview visibility, and request focus on the input thread. ThumbnailView.RefreshAppearance updates border geometry/overlay without image capture or DWM re-registration. No Task.Run, affinity await, timer tick or UI continuation gates these changes. The production affinity handler applies cached masks after the first focus request; pending async completion does not suppress a subsequent cycle. The activation guard covers reentrant calls only. This ordering corrects the delayed-border and 1 FPS focus regressions reported after the initial defect pass.

## Hotkeys and cycle semantics

`RegisterAllHotkeys` removes every tracked down/up delegate before rebuilding group and general bindings. It runs in the constructor, on profile changes and on `GlobalEvents.HotkeysChanged` after parsing group edits. [GlobalEvents.cs](../../Eve-O-Preview/Services/Implementation/GlobalEvents.cs) invokes subscribers directly; it does not marshal threads or queue notifications.

`FindNextClientInCycleGroup` walks full exact titles in integer-position order, forward or backward, skipping both offline and temporarily excluded titles. It retains a just-skipped active character's original position before seeking the next eligible member, wraps once, and returns null when none are eligible. When the active title is absent from that group, cycling starts at the first eligible entry in the requested direction. Destination and next-client prediction use the same rule; an all-skipped group leaves the active client unchanged, and a one-eligible-member group wraps safely.

Temporary exclusions are shared across groups within the active profile and live only for the current session. The workspace and thumbnail right-click Skip/Resume actions send `SetClientCycleSkipped`; they do not change saved group membership, thumbnail visibility or manual activation. `CycleSkipChanged` repaints a small marker beside the title through the existing `ThumbnailOverlay`/`OutlinedLabel`, including when the name itself is hidden. Marker shape/color are normal profile settings; runtime skip state is not serialized. Label and marker mouse-up events use the existing thumbnail click/context-menu route. None of these operations recreate a DWM relationship, raise previews or add IPC.

Cycle key-down checks `e.KeyData`, ignores `Keys.None`, ignores a press during `IsCurrentlySwitching`, then cycles and marks the event handled. General hide/minimize actions run on key-up intentionally, to reduce interference with cycling. [HotkeyHelpers.ToHotkeys](../../Eve-O-Preview/Helper/HotkeyHelpers.cs) uses `KeysConverter.ConvertFromInvariantString`; empty/invalid input becomes `Keys.None` and invalid input is logged.

Do not normalize KeyCode and KeyData as equivalent. Cycle key-down matches full KeyData; consumed main keys are tracked so key-up remains handled after modifiers release. General bindings match full chords and ignore None/previously handled events. The integration test exercises these production delegates with affinity completion held pending and requires selection/borders before the activation call.

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
- **Minimize is first by default; the thumbnail menu order is configurable.** Appearance → Thumbnail right-click menu supports dragging all five actions and explicit divider rows. The default has one divider after Minimize All and another after Skip/Resume. Dividers never relocate automatically; insert/remove commands edit the saved sequence. The first action opens under the initial pointer, so a second right-click without moving invokes it. Putting Skip/Resume first toggles the character's profile-wide session skip. Preserve the existing opening offset; overlay/title clicks convert their local point to preview-client coordinates. `ApplicationPreferences.ThumbnailMenuOrder` and `ThumbnailMenuTheme` share the existing global settings file. The host updates `NativeMenuTheme`; each opening applies layout and palette without recreating action handlers. Removed separator controls are disposed. Tray styling remains independent of thumbnail customization.
- **The open thumbnail menu is part of hover interaction.** `ThumbnailView.IsContextMenuOpen` covers opening/visible state; mouse leave defers zoom/opacity restoration until the menu closes. Reentry while open cannot overwrite saved base geometry. The visible menu handle is a known thumbnail handle for focus-based hiding. `RestoreThumbnailZOrder` retains dirty state while any thumbnail menu is open, so a routine tick cannot cover the popup with previews. Closed/mouse-up events cancel the hold-to-move timer, while a held first right-click retains the existing 350 ms drag behavior. Hiding/closing a preview closes its menu. These changes do not re-register DWM or change native client activation.

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

`UpdateAffinity` returns unless CPU support and `EnableAutomaticCpuAffinity` permit it. A missing active record falls back to next, then previous. It applies active/next/previous masks directly and uses `_currentBackgroundHandles` to skip redundant native background assignments. Removing those foreground-role handles from the set allows later transitions back to background to be applied. `ResetAll` restores each saved original affinity mask; native failures remain retryable. Stop sets a terminal flag under the same lock before resetting, so late activation work cannot reapply affinity during shutdown. Active/next/previous aliases are deduplicated by PID, with active winning. Desired masks are intersected with the original restriction. Preserve reset wiring when changing configuration/shutdown behavior.

Use source HWNDs to look up `IProcessInfo` before calling this service; apply affinity using its kernel `ProcessHandle`. Do not enumerate processes or recompute topology inside the keypress path.

## Lifetime fixes and remaining validation boundaries

ProcessMonitor owns disposable ProcessInfo kernel handles, disposes enumeration wrappers, locks cache mutation and returns snapshots. Unchanged polls do not open handles; title changes share ownership; removed/reused HWNDs release the old record. Thumbnail disposal releases DWM/static images/overlays/components and global mouse subscriptions. Static capture validates HWND/dimensions before allocating its memory bitmap, releases its destination HDC in finally, and disposes failed/empty bitmaps.

Profile changes apply live settings while suppressing geometry feedback, and only renderer changes recreate views. Highlight color/thickness refresh even when enabled state is unchanged. Active titles refresh on login/character rename without requiring an HWND change. Queued moves retain the latest title/location.

CPU topology now includes GroupCount, maps homogeneous efficiency-class-zero processors to performance cores and refuses unsupported processor groups rather than using the wrong mask. This is not general support for more than 64 processors. Native focus success, mixed-DPI layouts, multiple-monitor occlusion, long-session stutter and hardware-specific affinity latency remain live validation tasks. See [defect status](reported-bugs.md).

## Symptom-to-source navigation

| Symptom or requested change | Inspect first, then follow |
| --- | --- |
| Preview briefly blanks when rapidly cycling | `LiveThumbnailView.RefreshThumbnail` and `DwmThumbnail.Update`; ensure healthy registrations survive and native reordering never refreshes/re-registers images |
| Preview stays behind another preview, or disappears after native minimize/desktop activity | `RaiseActivatedThumbnail`, `RestoreThumbnailZOrder`, `RestoreAndBringToFront`; compare manager `IsActive`, native iconic state, image/overlay z order, dirty triggers, and always-on-top setting |
| Typing goes into a preview/overlay instead of EVE | Preview native restore flags/owned form behavior, then `WindowManager.MakeApiCallsToSetForegroundAndFocus`; avoid solving image visibility by activating preview forms |
| Preview freezes but the game remains responsive | Determine live versus compatibility view; inspect DWM `Update`/recovery or forced static capture. For source FPS throttling, cross into `IHookService` and the injected runtime |
| EVE source switch takes too long | Hotkey delegate â†’ cycle affinity/prediction â†’ `WindowManager.ActivateWindow` â†’ hook service; keep UI work, native activation, and game-frame wake-up measurements distinct |
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
