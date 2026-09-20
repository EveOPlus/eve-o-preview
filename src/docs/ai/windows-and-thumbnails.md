# Windows, thumbnails, cycling, and CPU affinity

Use this guide when a prompt concerns preview rendering, disappearing/overlapping previews, focus changes, client discovery, hotkeys, geometry, or CPU allocation. It describes the source reviewed on 2026-09-06. Follow the linked symbols in the current checkout before changing behavior; the limitations section records findings, not behavior to preserve.


## Start with the correct owner

For the Windows DWM/compatibility backend contracts and native graphics overlay, see the [preview rendering guide](preview-rendering.md). The [technology review](preview-rendering-review.md) records the broader platform options and remaining Linux work.

| Concern | Source and entry points |
| --- | --- |
| Preview lifecycle, visibility policy, switching, cycle order, layout persistence | [ThumbnailManager.cs](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs): `UpdateThumbnailsList`, `RefreshThumbnails`, `SetActive`, `CycleNextClient`, `SwitchActiveClient` |
| Native focus, restore/minimize, capture | [WindowManager.cs](../../Eve-O-Preview/Services/Implementation/WindowManager.cs): `ActivateWindow`, `MakeApiCallsToSetForegroundAndFocus`, `MinimizeWindow`, `GetLiveThumbnail`, `GetStaticThumbnail` |
| Native rendering relationship | [DwmThumbnail.cs](../../Eve-O-Preview/Services/Implementation/DwmThumbnail.cs): `Register`, `Move`, `Update`, `Unregister` |
| Preview geometry, overlay, highlight, mouse gestures, native preview restore | [ThumbnailView.cs](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs): `RestoreAndBringToFront`, `Refresh`, `HighlightThumbnail`, `RefreshOverlay`, custom mouse mode |
| Live image maintenance/recovery | [LiveThumbnailView.cs](../../Eve-O-Preview/View/Implementation/LiveThumbnailView.cs): `RefreshThumbnail`, `ResizeThumbnail`; image lifetime in [WindowsDwmPreviewBackend](../../Eve-O-Preview/View/Rendering/WindowsDwmPreviewBackend.cs) |
| Compatibility capture | [StaticThumbnailView.cs](../../Eve-O-Preview/View/Implementation/StaticThumbnailView.cs), [StaticThumbnailImage.cs](../../Eve-O-Preview/View/Implementation/StaticThumbnailImage.cs) |
| Selecting live versus compatibility view | [ThumbnailViewFactory.cs](../../Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs): `Create` |
| Client enumeration and handle cache | [ProcessMonitor.cs](../../Eve-O-Preview/Services/Implementation/ProcessMonitor.cs), [ProcessInfo.cs](../../Eve-O-Preview/Services/Implementation/ProcessInfo.cs), [ProcessHelpers.cs](../../Eve-O-Preview/Helper/ProcessHelpers.cs) |
| CPU topology and placement | [CpuAffinityService.cs](../../Eve-O-Preview/Services/Implementation/CpuAffinityService.cs): `RefreshTopology`, `UpdateAffinity`, `ResetAll`; [CpuPlacementPolicy.cs](../../Eve-O-Preview/Services/Implementation/CpuPlacementPolicy.cs) |
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

[LiveThumbnailView.RefreshThumbnail](../../Eve-O-Preview/View/Implementation/LiveThumbnailView.cs) delegates maintenance to its [WindowsDwmPreviewBackend](../../Eve-O-Preview/View/Rendering/WindowsDwmPreviewBackend.cs) session, which deliberately retains a healthy relationship:

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

`RaiseActivatedThumbnail` updates the order and raises the selected visible/enabled preview immediately after requesting source focus and submitting its selection frame, before layout work and timer maintenance. Both `SetActive` and the click activation callback use it. It skips immediate native raising when always-on-top is off, the preview is hidden/disabled, or the active-client-preview-hiding option is enabled.

Keep the immediate raise and periodic repair as separate responsibilities. Reordering every refresh can disturb other topmost windows; relying only on cached `TopMost` cannot repair native visibility/z-order changes.

### Compatibility mode has different cost and lifetime rules

The workspace's advanced settings use `ThumbnailRuntimeSettingsUpdatedHandler` → `ThumbnailManager.ApplyRuntimeSettings`, also called after profile changes. Ordinary edits reuse current views/DWM relationships while updating interval, hide delay, size bounds, login position and appearance. A changed `EnableCompatibilityMode` closes existing views and rebuilds them using the existing process cache and factory. This exception is required to switch between live and static renderers; it must not run for an ordinary color, layout or timer edit. Private-desktop `SettingsIntegrationTests` covers both directions of the renderer switch and retained view identity for ordinary edits.

The factory reads the current `EnableCompatibilityMode` when creating each view. Static views call [WindowManager.GetStaticThumbnail](../../Eve-O-Preview/Services/Implementation/WindowManager.cs) on forced refresh. The same helper supplies the settings sample's one-shot background. It rejects zero/invalid/minimized HWNDs and either client dimension below 300, then uses `PrintWindow(PW_CLIENTONLY | PW_RENDERFULLCONTENT)` into a fresh memory bitmap. If that produces no content, a traditional `PW_CLIENTONLY` rendering request supports GDI windows without a compositor surface. Failed or sampled all-black results return null. It never reads a display DC or falls back to `GetDC`/`BitBlt`: live testing exposed monitor/occluding content from the earlier display-copy path despite passing an EVE HWND. The view's `WindowsStaticPreviewBackend` session replaces a non-null image and disposes the previous image. Null capture keeps the last valid image.

`PrintWindow` is synchronous; the settings sample runs it on its existing background task and does not recapture during edits. Compatibility mode retains its synchronous forced-refresh schedule; this is separate from normal DWM preview rendering. The `WorkspacePreviewRenderingTests` covered-client scenario verifies actual client/child pixels under another opaque window, no focus change, and no image for unsupported, closed or minimized targets. Real background-EVE capture must be checked separately from private-desktop GDI tests.

[StaticThumbnailImage.WndProc](../../Eve-O-Preview/View/Implementation/StaticThumbnailImage.cs) returns `HTTRANSPARENT` for `WM_NCHITTEST` so the picture control does not intercept the form's mouse behavior. Preserve this when changing rendering controls.

## Focus, switching, and prediction

[WindowManager.ActivateWindow](../../Eve-O-Preview/Services/Implementation/WindowManager.cs) validates the HWND with IsWindow, issues the existing pipe wake, restores a minimized source asynchronously, then immediately calls SetForegroundWindow/SetFocus with the existing SwitchToThisWindow fallback. There is no WM_NULL responsiveness probe: a throttled client must receive the wake before any focus request, and must not be rejected because it is waiting in Present. IsCurrentlySwitching covers the synchronous attempt and is cleared in finally. Windows activation may still complete asynchronously in the target input queue.

`SwitchActiveClient` handles the previous source: if `MinimizeInactiveClients` is enabled and the old title is not a priority client, minimize it without animation, then update active state/order. Merely switching to a non-EVE window does not run this path to minimize clients.

`MinimizeWindow(handle, true)` asynchronously posts `WM_SYSCOMMAND/SC_MINIMIZE`; `false` edits `WINDOWPLACEMENT.showCmd` and calls `SetWindowPlacement`. Preserve this distinction when changing switch latency. An explicit thumbnail minimize uses the animated path; inactive-client minimization uses the non-animated path.

Cycle hotkeys, direct client hotkeys and thumbnail clicks share synchronous `SetActive` behavior. Commit only the in-memory selection/MRU metadata, request source focus immediately, then submit the old/new selection frames. Preview raising, layouts, active-preview visibility, minimization, prediction and affinity follow. Focus takes precedence over all graphics work. The enhanced Windows renderer uses a retained overlay frame: routine selection changes do not resize the DWM image, repaint the host/title, capture an image, or upload another surface after each client's color pixel is cached. Compatibility graphics submits its pending background-border paint with `Control.Update` after focus. After the dedicated input thread posts a high-priority UI action, no Task.Run, affinity await, timer tick or additional UI continuation gates the focus request or frame submission. The keyboard hook itself never waits for that action. The production affinity handler applies cached CPU-set plans after the first focus request; pending async completion does not suppress a subsequent cycle. The activation guard covers reentrant calls only. Mouse handlers must not subsequently refresh the image or clear the already-selected client's own border.

[ForegroundWindowObserver](../../Eve-O-Preview/Services/Implementation/ForegroundWindowObserver.cs) subscribes to `EVENT_SYSTEM_FOREGROUND` with `WINEVENT_OUTOFCONTEXT` on the existing UI message loop. `Start` registers once and `Stop`/`Dispose` unhooks; the callback delegate remains rooted. Known EVE source notifications (Alt-Tab or direct game clicks) coalesce into one high-priority (`DispatcherPriority.Send`) UI dispatch, which reads the latest actual foreground and reconciles selection/appearance without requesting focus again or waiting for discovery. Native event callbacks can reenter during activation: do not simply discard a notification because its event HWND differs from the current foreground inside that callback. Reading the latest foreground after dispatch avoids replaying stale event HWNDs and losing the final transition. `Send` uses foreground dispatcher processing; WPF can defer `Input` priority while native input remains pending. Stopped and unknown windows are ignored; the normal activation guard prevents reentry. Polling remains the discovery/recovery and external-focus-loss hiding path, including fallback if registration fails. This adds no injected callback, input hook, or IPC channel. See [Microsoft's event-hook contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook).

Production logging uses [AsyncLogSink](../../Eve-O-Preview/Helper/AsyncLogSink.cs): a single background reader writes the existing rotating file sink. Its bounded 2,048-event queue never waits on an input/render producer. Overload drops events and reports their count; shutdown allows 500 ms for draining accepted events and disposing the file sink. The worker owns disposal; blocked writes or disposal may outlive that budget without blocking process exit. This matters especially with `-v`, where switching emits multiple diagnostic records. Millisecond event timestamps and the active-outline submission record aid comparison; synchronous disk flushes must not be restored to input callbacks.

## Hotkeys and cycle semantics

`ThumbnailManager.RegisterAllHotkeys` creates a complete binding snapshot and calls the platform-neutral [IHotkeyService](../../Eve-O-Preview/Services/Interface/IHotkeyService.cs). It runs on startup, profile changes, `GlobalEvents.HotkeysChanged` and input-mode changes. [WindowsHotkeyService](../../Eve-O-Preview/Services/Implementation/WindowsHotkeyService.cs) owns a dedicated STA message-loop thread for both input modes. The interface contains shortcut strings and actions, with no HWND, WinForms key types or message-loop assumptions; a future Linux host can replace the adapter and UI scheduler without changing binding/profile/capture orchestration. No Linux backend is implemented.

- **Global input (default):** a `WH_KEYBOARD_LL` callback updates modifier state, does a dictionary lookup and suppresses matched down/up events. No UI work, text translation, logging, focus calls, IPC, configuration reads or affinity work occur in the normal callback. Unmatched matcher calls allocate zero bytes after warmup. Only configured shortcuts install a hook; clearing the last binding removes it. The separate MouseKeyHook instance remains for existing thumbnail mouse gestures, not keyboard subscriptions.
- **Dispatch:** [HotkeyDispatchQueue](../../Eve-O-Preview/Services/Implementation/HotkeyDispatchQueue.cs) normally posts immediately using `DispatcherPriority.Send`, without debounce, polling, thread-pool scheduling or synchronous UI waits. It preserves order, runs finite batches, and invalidates pending actions on replacement/capture/stop. A 64-action bound drops the oldest backlog only if the UI cannot keep up, avoiding an unbounded sequence of stale switches after a stall. Native Windows focus completion still depends on target/client scheduling.
- **Windows hotkeys:** `RegisterHotKey`/`WM_HOTKEY` replaces the keyboard hook during normal use. All old registrations and queued old `WM_HOTKEY` messages are removed before installing the new snapshot, including on profile switches. Registration conflicts/reserved keys appear in the workspace. `IThumbnailConfiguration.UseWindowsHotkeys` defaults to false and is saved in the selected profile. Modern themes expose the `HotkeyInputMethod` dropdown under Switching & hotkeys > Global shortcuts, neutrally labeled **Global input** and **Windows hotkeys**, with a brief description of each mechanism. Legacy receives no new control. Windows owns chord/repeat behavior; do not claim all modifiers must be released between presses or that native hotkeys are inherently less smooth.
- **Trigger timing:** `GlobalHotkeyTrigger` offers **Key down** (default) and **Key up** only while Global input is selected, including in settings search. The selected profile's persisted `IThumbnailConfiguration.GlobalHotkeysOnRelease` selects `HotkeyBinding.OnRelease` for cycle, hide and minimize bindings. Key down preserves repeat cycling; key up runs once when the main shortcut key is released, even if a modifier was released first. Native Windows hotkeys expose no release event here and always use their normal activation message; their mode retains but ignores that profile's saved global-input trigger choice. Changing either control awaits the existing profile settings commit. Every registration replacement reads the current configuration, so profile loads immediately apply both choices along with their bindings. Profiles missing these fields are seeded from earlier global values, if present, and the next profile save writes both fields; explicit profile values always win.
- **Diagnostic passthrough:** in modern themes, ten complete open/close cycles of the Hotkey method dropdown within ten seconds (no gap over two seconds) reveal **Diagnostic key passthrough**. Search cannot expose it before unlocking. It is off initially, session-only, explicitly marked not for production, disabled for Windows hotkeys and reset when that method is selected. `ApplicationPreferences.DiagnosticHotkeyPassthrough` is never persisted. In global mode the service calls `CallNextHookEx` for matched down/up events, then posts a bounded action handoff to its own input window. Diagnostic actions use a separate, invalidated `DispatcherPriority.Background` queue so pending UI input precedes focus changes; ordinary hotkeys keep Send priority. Both selected trigger edges work without synthesizing or replaying keys. This hands input onward before action dispatch; it is not an acknowledgement of EVE message processing or a gameplay response. Capture still suppresses the recorded shortcut. Queues are invalidated on mode/profile/trigger/diagnostic changes, capture and shutdown.
- **Capture:** all active registrations/actions are suspended before installing a temporary capture hook. The first non-modifier chord completes on its main-key release. Success, Escape, timeout and cancellation restore the latest binding snapshot; profile edits during capture remain dormant until then. Disposal cancels capture and releases native resources. The modern UI awaits capture without a `DoEvents`/sleep loop.

`FindNextClientInCycleGroup` walks full exact titles in integer-position order, forward or backward, skipping both offline and temporarily excluded titles. It retains a just-skipped active character's original position before seeking the next eligible member, wraps once, and returns null when none are eligible. When the active title is absent from that group, cycling starts at the first eligible entry in the requested direction. Destination and next-client prediction use the same rule; an all-skipped group leaves the active client unchanged, and a one-eligible-member group wraps safely.

Temporary exclusions are shared across groups within the active profile and live only for the current session. The workspace and thumbnail right-click Skip/Resume actions send `SetClientCycleSkipped`; they do not change saved group membership, thumbnail visibility or manual activation. `CycleSkipChanged` repaints a small marker beside the title through the existing `ThumbnailOverlay`/`OutlinedLabel`, including when the name itself is hidden. Marker shape/color are normal profile settings; runtime skip state is not serialized. Label and marker mouse-up events use the existing thumbnail click/context-menu route. None of these operations recreate a DWM relationship, raise previews or add IPC.

[WindowsHotkeyMatcher](../../Eve-O-Preview/Services/Implementation/WindowsHotkeyMatcher.cs) matches full chords, keeps modifiers held across successive cycle keys, and retains held-key repeat cycling in key-down mode. Consumed main-key releases remain suppressed if modifiers are released first, unless diagnostic passthrough is enabled. Modifier-only shortcuts are not registered. Existing profile key strings continue to use `KeysConverter` at registration/capture boundaries, never on the keypress hot path.

[HotkeyTests](../../tests/Eve-O-Preview.Tests/Checks/HotkeyTests.cs) covers held modifiers, auto-repeat, early modifier release, allocation-free unmatched matching, queue ordering/invalidation/bounds, native registration conflicts, mode/profile replacement, capture completion/cancellation/timeout and disposal. The settings integration check requires source focus then immediate border submission within the dispatched UI action even while affinity completion is pending. These checks complement, rather than replace, the opt-in [live input timing harness](../../tests/Preview.RenderingSmoke/README.md#live-hotkey-input-latency).

Mouse input retains the existing shared MouseKeyHook service. [ThumbnailMouseTests](../../tests/Eve-O-Preview.Tests/Checks/ThumbnailMouseTests.cs) verifies click/modifier routing, title/overlay input, hover zoom, menus, movement/resizing and cleanup in both modes, including hotkey mode changes and capture while mouse subscriptions are active. The live harness's `--mouse-checks` option separately exercises actual mouse delivery, right-button hold dragging, menu movement/free resizing and Shift-resizing against real previews in both input modes.

Hotkey controls and status messages use all 18 workspace language catalogs.
Registration warnings cross the service boundary as `FormattableString` templates
with raw shortcut arguments; the workspace backend translates them when reading
or reporting settings results. Translation never runs in the keyboard callback.
Legacy keeps English messages. See the [translation context guide](../../Eve-O-Preview.UI/Localization/README.md).

## Geometry, hovering, and event feedback

The [ThumbnailView](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs) comment explicitly keeps current size/position management in the view for responsiveness. Do not route high-frequency mouse moves through configuration persistence or mediator handlers merely to enforce a stricter presenter pattern.

- `Refresh` maintains the image, highlight rectangle, and overlay, in that order. `_isSizeChanged`/`_isLocationChanged` gate geometry work; a separate `_isHighlightChanged` flag avoids treating selection as a resize. Live/static rectangle setters also skip unchanged values.
- `SetOpacity` maps values at or above 0.9 to 1.0 and ignores differences below 0.1. The overlay uses 1.0 above 0.8, otherwise `1.0 - (1.0 - opacity) / 2`. Its implementation, not the nearby prose comment, is the precise formula.
- `SuppressResizeEvent` ignores resize events for 500 ms after operations known to produce inconsistent WinForms `ClientSize` events. Manager `_ignoreViewEvents` separately suppresses feedback while programmatically changing multiple views.
- Enhanced highlighting is four retained overlay edges of equal native-pixel thickness, above the image and alerts. The game image keeps its full destination and aspect ratio. Compatibility graphics retains its historical inset image/background border, whose sides preserve the inset image's original aspect ratio.
- Hover enters by saving base size/location, enabling a global manager hover flag, setting full opacity/topmost, and optionally zooming. While this flag is set, periodic location, opacity, topmost, and snap updates are suppressed so they do not fight the hover state.
- `ZoomIn` changes size **before** location. The source describes a focus-lost/focus-regained oscillation if this ordering is reversed. It temporarily removes maximum size; `ZoomOut` restores saved size, maximum size, and location. The nine [ViewZoomAnchor](../../Eve-O-Preview/View/Interface/ViewZoomAnchor.cs) values specify the anchor.
- A user resize propagates the selected client size to every thumbnail and publishes `ThumbnailActiveSizeUpdated`. The presenter updates both the settings view and `IThumbnailConfiguration.ThumbnailSize`, so the normal exit save retains the resized dimensions. This path suppresses size feedback and does not commit unrelated workspace edits or write the profile on every mouse move. `SettingsIntegrationTests` exercises the production notification handler, Avalonia workspace, exit save and profile reload with both growing and shrinking dimensions; repeated live-startup or mixed-DPI shrink remains unverified. A move updates configuration immediately but delays its save notification/snapping by two eligible refresh rounds. Repeated movement resets that delay; changing the queued source or active-client context flushes the previous notification.
- Snapping requires `EnableThumbnailSnap` and borderless previews. It checks nine selected corner pairs, using a threshold of `max(20, dimension / 10)`, then snaps to the first matching other preview. Preserve the distinction between client size and decorated window size.
- Custom Move/Resize uses global mouse-move/up subscriptions only while active. A context menu offers both; holding right-click for the [designer's 350 ms timer](../../Eve-O-Preview/View/Implementation/ThumbnailView.Designer.cs) starts drag movement and applies the offset already travelled. Shift-resize maintains the starting ratio. Exiting custom mouse mode unsubscribes the handlers.
- **Minimize is first by default; the thumbnail menu order is configurable.** Appearance → Thumbnail right-click menu supports dragging all five actions and explicit divider rows. The default has one divider after Minimize All and another after Skip/Resume. Dividers never relocate automatically; insert/remove commands edit the saved sequence. The first action opens under the initial pointer, so a second right-click without moving invokes it. Putting Skip/Resume first toggles the character's profile-wide session skip. Preserve the existing opening offset; overlay/title clicks convert their local point to preview-client coordinates. `ApplicationPreferences.ThumbnailMenuOrder` and `ThumbnailMenuTheme` share the existing global settings file. The host updates `NativeMenuTheme`; each opening applies layout and palette without recreating action handlers. Removed separator controls are disposed. Tray styling remains independent of thumbnail customization.
- **The open thumbnail menu is part of hover interaction.** `ThumbnailView.IsContextMenuOpen` covers opening/visible state; mouse leave defers zoom/opacity restoration until the menu closes. Reentry while open cannot overwrite saved base geometry. The visible menu handle is a known thumbnail handle for focus-based hiding. `RestoreThumbnailZOrder` retains dirty state while any thumbnail menu is open, so a routine tick cannot cover the popup with previews. Closed/mouse-up events cancel the hold-to-move timer, while a held first right-click retains the existing 350 ms drag behavior. Hiding/closing a preview closes its menu. These changes do not re-register DWM or change native client activation.

The [overlay](../../Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs) is an owned borderless form with fuchsia transparency key, an outlined title, and a transparent picture area whose mouse-up forwards to the preview's click handler. `ShowThumbnailOverlays` controls the label; the overlay form itself participates in positioning, ownership, and native restore. Both preview and overlay set `ShowWithoutActivation` and `WS_EX_TOOLWINDOW` and omit taskbar entries. Title display removes `EVE - `; configuration/client identity keeps the full title.

Client-window layouts are separate from thumbnail locations. `ApplyClientLayout` moves/maximizes named clients with saved layouts when discovered or renamed. `UpdateClientLayouts`, currently called after click activation, saves each named client's geometry if maximized or if both coordinates are strictly between -10,000 and 31,000 and both dimensions exceed 10. These guards prevent saving typical minimized/off-screen sentinel positions; they are not general multi-monitor validation.

## CPU placement uses Windows CPU Sets

[CpuAffinityService](../../Eve-O-Preview/Services/Implementation/CpuAffinityService.cs) applies process-default CPU Sets rather than changing hard affinity masks. [WindowsCpuSetApi](../../Eve-O-Preview/Services/Interop/WindowsCpuSetApi.cs) reads group-relative physical-core, last-level-cache, NUMA-node and efficiency-class metadata. [CpuPlacementPolicy](../../Eve-O-Preview/Services/Implementation/CpuPlacementPolicy.cs) makes a cached role plan for each process. CPU-set IDs are opaque; logical CPU numbers are never used to guess SMT sibling relationships. Physical-core capacity is the highest class reported by its eligible logical processors, so different SMT hints cannot split one core between foreground and background.

This is a topology-aware policy that cooperates with the Windows scheduler. It does not read Intel Thread Director telemetry, classify individual EVE threads, identify AMD preferred/X3D cores, or promise to outperform the scheduler's dispatch latency. Windows chooses threads and processors within the selected pools. The public [CPU Sets documentation](https://learn.microsoft.com/en-us/windows/win32/procthread/cpu-sets) describes their interaction with power management, hard affinity and thread-selected CPU Sets. Intel also recommends scheduler-cooperative affinity in its [hybrid architecture guide](https://www.intel.com/content/www/us/en/developer/articles/guide/12th-gen-intel-core-processor-gamedev-guide.html). Task Manager can still show the full hard-affinity mask; query process-default CPU Sets to verify this policy.

| Available physical cores in the preferred cache pool | Active | Predicted next and previous | Background |
| --- | --- | --- | --- |
| Hybrid, at least four performance cores | All performance cores except two, including their SMT siblings | Share the remaining two performance cores | All lower capacity classes in the same group/NUMA node |
| Hybrid, fewer than four performance cores | All performance cores | Share the active pool | All lower capacity classes in the same group/NUMA node |
| Homogeneous, more than four cores | First half, rounded up, including SMT siblings | Share the active pool | Remaining half |
| Homogeneous, up to four cores | Share all available cores | Same pool | Same pool |

For example, unrestricted 12700H topology (6P/8E with P-core SMT) produces an active pool of four physical P-cores/eight logical processors, a shared warm pool of two P-cores/four logical processors, and eight background E-cores. An unrestricted 285K topology (8P/16E without SMT) produces 6/2/16 logical processors. These are starting policies, not measured optimal budgets. Pools constrain placement, not CPU-time quotas or exclusive reservations; other applications still share those processors.

Each process keeps a stable group/NUMA/cache home across role changes. Initial homes are balanced by tracked process count relative to physical-core capacity; this is not live utilization balancing. Prefer one performance cache domain, extending within the same node if it contains fewer than four performance cores and other cores are available. Background efficiency cores may have a different cache but stay in that group/node. This avoids routine CCD/NUMA movement on each switch; it does not relocate memory already allocated by the client. Multi-group CPU IDs are supported, but Windows still honors each thread's group restrictions. Do not claim complete multi-group EVE scaling from simulated topology tests.

Discovery runs at construction and on a ten-second background timer while enabled. Only changed immutable snapshots invalidate cached plans. A focus change requests foreground activation and submits selection frames first, then applies the new active client's CPU Sets before updating other roles. Process aliases are deduplicated by PID, with active winning over next and previous. Unchanged assignments are skipped. Neither topology discovery nor process enumeration runs in the keypress callback.

The predicted and previous roles persist until a subsequent role change; there is no timeout. Ordinary foreground changes, including Alt+Tab, promote the actual EVE client and retain the old client as previous. The existing thumbnail maintenance timer admits newly discovered clients and applies changed topology without discarding valid predictions.

Before the first assignment, save the process's original default CPU Sets and intersect candidates with those sets, its process groups and any available single-group hard-affinity mask. Never change hard affinity, priority class, power mode or thread-specific settings. Windows enforces hard/thread restrictions above process-default CPU Sets. Allocated CPU Sets are conservatively excluded; parked CPUs remain eligible so Windows owns parking. Unsupported/invalid initial topology leaves placement to Windows. A failed refresh retains the last valid snapshot.

Reset restores the exact original CPU-set list, including an empty list meaning no process-default assignment. Failed native assignments/restores remain retryable; stop sets a terminal flag before restoration so delayed callbacks cannot reapply placement. Owned kernel handles are protected with SafeHandle references and checked against the PID before native use. Preserve configuration/profile/shutdown reset wiring. Use source HWNDs only to look up the cached process; native CPU APIs take its kernel handle.

Diagnostics log topology counts/capacity classes, per-process group/node/cache and role-pool sizes, plus assignment/restoration failures. For performance validation compare automation off/on using the same workload and graphics/FPS settings. Record frame-time percentiles and foreground-switch-to-first-frame latency with PresentMon, and CPU sampled/precise scheduling plus ready time with an elevated WPR/WPA trace. Include GPU utilization, dedicated/shared GPU memory, system commit and hard faults to distinguish CPU placement effects from GPU or memory pressure. CPU time alone does not prove a frame-rate benefit.

Validation: `CpuPlacementTests` covers Alder Lake SMT, noncontiguous Arrow Lake P-cores, three capacity classes, small and large homogeneous CPUs, cache-home balancing/stability, multiple groups, restrictions, role persistence, caching, topology refresh, parser bounds, failed writes/restores and terminal shutdown. The isolated native settings worker verifies real CPU-set assignment/restoration (empty and explicit original sets), unchanged hard affinity, focus ordering and Alt+Tab/maintenance routing. Actual game frame-time gains, thermal behavior, AMD X3D placement, and physical multi-group machines still require representative live measurements.

## Lifetime fixes and remaining validation boundaries

ProcessMonitor owns disposable ProcessInfo kernel handles, disposes enumeration wrappers, locks cache mutation and returns snapshots. Unchanged polls do not open handles; title changes share ownership; removed/reused HWNDs release the old record. Thumbnail disposal releases DWM/static images/overlays/components and global mouse subscriptions. Static capture validates HWND/dimensions before allocating its memory bitmap, releases its destination HDC in finally, and disposes failed/empty bitmaps.

Profile changes apply live settings while suppressing geometry feedback, and only renderer changes recreate views. Highlight color/thickness refresh even when enabled state is unchanged. Active titles refresh on login/character rename without requiring an HWND change. Queued moves retain the latest title/location.

CPU placement uses group-aware CPU-set IDs and preserves physical-core relationships and original restrictions. Synthetic multi-group tests validate planning, while actual game behavior across processor groups remains unverified. Native focus success, mixed-DPI layouts, multiple-monitor occlusion, long-session stutter and hardware-specific scheduling latency remain live validation tasks. See [defect status](reported-bugs.md).

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
| Background clients consume unexpected CPU or affinity has no effect | CPU-set topology and per-process role plans, original hard/thread restrictions, `UpdateCpuAffinity` routing, persistent predictions, native assignment failures, GPU/commit pressure |
| Resource count grows over time | Process handle polling/replacement, compatibility GDI early return, view/overlay/component cleanup, and hotkey/custom mouse subscriptions |

For behavior changes, use the existing [thumbnail z-order checks](../../tests/Eve-O-Preview.Tests/Checks/ThumbnailZOrderTests.cs) and [live-thumbnail checks](../../tests/Eve-O-Preview.Tests/Checks/LiveThumbnailTests.cs), then the test guide's documented execution path. Cover hidden previews, owner/overlay ordering, preserved foreground focus, healthy versus failed DWM relationships, overlap/MRU behavior, source minimize/restore, compatibility images, hover, and relevant feature settings. Only broaden tests to behaviors affected by the edit.

## Reviewed-file coverage

The later [Augments integration](combat-logs.md) is in
`ThumbnailManager.CombatLogs`. Log-worker events marshal to the manager's owning
dispatcher, coalesce by character title and update only retained stat scenes.
Finite alpha/repair expiry and rolling DPS clocks do not read EVE files. Start,
Stop, title changes and disposal coordinate subscription/visual lifetime.
Simulation targets all visible thumbnails by default, with optional per-client
selection. It uses the normal notification/aggregation/rendering path in a modern
theme. Temporary overview statistics are discarded on completion; real ingestion
continues. DWM relationships, focus and z-order are preserved. Source window
geometry and Robin are outside this path.

This guide's review read the complete contents of the following files, including designer wiring and `.resx` metadata. `HookService.cs` and `DebuggerSidecar.cs` belong to the separate hook-runtime review; main-form/presenter/configuration/mediator/test implementation coverage belongs to the other repository guides.

- `Eve-O-Preview/Services/Implementation/`: `CpuAffinityService.cs`, `DwmThumbnail.cs`, `GlobalEvents.cs`, `ProcessInfo.cs`, `ProcessMonitor.cs`, `ThumbnailManager.cs`, `WindowManager.cs`.
- `Eve-O-Preview/Services/Interface/`: `ICpuAffinityService.cs`, `IDwmThumbnail.cs`, `IGlobalEvents.cs`, `IHookService.cs`, `IProcessInfo.cs`, `IProcessMonitor.cs`, `IThumbnailManager.cs`, `IWindowManager.cs`, `InteropConstants.cs`.
- `Eve-O-Preview/Services/Interop/`: `DWM_BLURBEHIND.cs`, `DWM_THUMBNAIL_PROPERTIES.cs`, `DWM_TNP_CONSTANTS.cs`, `DwmNativeMethods.cs`, `Gdi32NativeMethods.cs`, `KernelNativeMethods.cs`, `MARGINS.cs`, `RECT.cs`, `User32NativeMethods.cs`, `WINDOWPLACEMENT.cs`.
- `Eve-O-Preview/View/Implementation/`: `LiveThumbnailView.cs`, `LiveThumbnailView.resx`, `StaticThumbnailImage.cs`, `StaticThumbnailView.cs`, `ThumbnailDescription.cs`, `ThumbnailOverlay.cs`, `ThumbnailOverlay.Designer.cs`, `ThumbnailOverlay.resx`, `ThumbnailView.cs`, `ThumbnailView.Designer.cs`, `ThumbnailView.resx`, `ThumbnailViewFactory.cs`.
- `Eve-O-Preview/View/Interface/`: `IThumbnailDescription.cs`, `IThumbnailView.cs`, `IThumbnailViewFactory.cs`, `ViewZoomAnchor.cs`.
- `Eve-O-Preview/Helper/`: `HotkeyHelpers.cs`; additionally `ProcessHelpers.cs` to trace raw handle ownership.

The three reviewed thumbnail `.resx` files contain schema/designer metadata, with no bitmap payload: thumbnail context-menu/tooltip/timer tray positions, overlay local-control generation metadata, and an empty live-view resource set.
