# Defect investigation guide

Source baseline: `60944b521e3c5b442dd379b5208c53a91ae3a573`, version 10.0.0.9. These twelve entries are investigation candidates and UX limitations, not confirmed runtime defects. Verify each against the current implementation before changing behavior. See the [feature backlog](feature-backlog.md) for broader enhancements.

## BUG-001: Missing Default profile can break startup

[ProfileManager](../../Eve-O-Preview/Configuration/Implementation/ProfileManager.cs) accepts an existing profile root without ensuring Default exists; `GetDefaultProfileLocation` can return null. [MainFormPresenter.ReloadApplicationSettings](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) dereferences `CurrentProfile.FriendlyName`. Default deletion is guarded, but renaming lacks the same protection and can leave the stored location stale.

Check startup with an isolated profile root containing named profiles but no Default, then test rename and restart. Preserve existing profiles and establish a coherent fallback. Initial profile migration support does not eliminate this separate missing-location risk.

## BUG-002: Profile switch changes close-to-tray behavior

`MinimizeToTray` is profile-scoped and defaults to false in [ThumbnailConfiguration](../../Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs). [MainFormPresenter.Close](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) deliberately exits when it is false. This is a settings-scope/UX question, not an exception.

Check opposing profile values, the visible setting, window close and explicit Exit. Any move to application-wide preferences needs a migration policy.

## BUG-003: Previews fall behind client windows

[ThumbnailManager](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs) maintains dirty MRU order and raises the selected preview before activation. [ThumbnailView.RestoreAndBringToFront](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs) repairs native visibility/topmost state without activating the preview or rebuilding DWM content. These protections do not establish correctness in every desktop environment.

Distinguish occlusion, intentional hiding, native hidden/minimized windows, missing process discovery and visible-but-blank rendering. Capture source/preview/overlay HWNDs, foreground HWND, intended visibility, actual visibility, iconic state and relative z-order before changing settings. Compare cycling, clicking, application transitions, long sessions and multiple monitors. A topmost toggle or restart is a diagnostic experiment, not proof of a permanent fix.

Use [ThumbnailZOrderTests](../../tests/Eve-O-Preview.Tests/Checks/ThumbnailZOrderTests.cs) and [LiveThumbnailTests](../../tests/Eve-O-Preview.Tests/Checks/LiveThumbnailTests.cs), then validate relevant real desktop conditions. Preserve stable DWM relationships and nonactivating restoration; avoid unconditional hide/show or re-registration every poll.

## BUG-004: Cycle hotkeys fail with certain foreground applications

Trace [Program](../../Eve-O-Preview/Program.cs), [ThumbnailManager.RegisterCycleClientHotkey](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs), [WindowManager.ActivateWindow](../../Eve-O-Preview/Services/Implementation/WindowManager.cs) and [app.manifest](../../Eve-O-Preview/app.manifest). Global input matching and foreground activation are separate stages.

Compare a non-conflicting binding across foreground applications and privilege levels. Distinguish an undelivered key, a declined switch and unsuccessful activation. Absence of a Verbose event in an Information-level log proves nothing about delivery. Global cycling while another app is active is supported behavior; application-specific scope would be a feature decision.

## BUG-005: Severe lag and background browser stutter

This is a performance investigation scenario with no established cause. Examine [Program.SetupLogger](../../Eve-O-Preview/Program.cs), [ThumbnailManager](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs), [CpuAffinityService](../../Eve-O-Preview/Services/Implementation/CpuAffinityService.cs), [HookService](../../Eve-O-Preview/Services/Implementation/HookService.cs) and [DxHook](../../Eve-O-Preview.Robin/DXHook.cs).

Measure switch latency, frame times, per-core/per-process CPU, GPU/memory/disk activity, handle counts and log growth. Compare logging level, affinity and verified FPS targets one variable at a time. Aggregate CPU utilization is not a bottleneck diagnosis. Fresh target processes are necessary when comparing injected binaries: restarting only the host can reuse Robin.

## BUG-006: Log volume obstructs reporting and diagnosis

[Program.SetupLogger](../../Eve-O-Preview/Program.cs) configures daily file rolling with seven retained files; Verbose is enabled by `--verbose` or `-v`. Hot paths contain numerous verbose entries. File retention alone does not establish convenient diagnostic file sizes.

Measure bytes per minute at representative client counts. Consider bounded captures or reduced event rates while retaining errors and necessary context. Do not add persistent per-key logging or more high-frequency output without measuring cost. See the [build/test guide](build-and-test.md) for log locations.

## BUG-007: Installation files remain in use after host exit

[HookService](../../Eve-O-Preview/Services/Implementation/HookService.cs) loads Robin into target processes. [StartStopServiceHandler](../../Eve-O-Preview/Mediator/Handlers/Services/StartStopServiceHandler.cs) stops polling, resets affinity and requests zero FPS targets; it does not unload native hooks or clear audio state. Host exit and native-module lifetime are distinct.

Identify the exact locked file and holder before/after normal target shutdown. Separate access-denied errors from module-in-use failures. Any hot-unload design must safely restore callbacks and native state; forced unload is unsafe while patched entry points remain. See [Robin lifetime constraints](robin.md#injection-and-ownership-lifecycle).

## BUG-008: Hotkeys stop after client stalls

Treat persistent input loss and delayed repeat processing as separate hypotheses. [Program](../../Eve-O-Preview/Program.cs) installs global events; [ThumbnailManager](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs) performs cycling work from the callback. There is no explicit hook-health recovery mechanism in these routes.

Measure callback duration, synchronous native/IPC work and dispatch backlog with a controlled unresponsive target. An Async suffix alone does not establish nonblocking execution. Distinguish hook removal, host UI blocking, queued repeats and activation failure. Preserve modifier handling, event suppression and ordering; stale queued inputs must not unexpectedly activate later clients.

## BUG-009: Preview sizes reset or diverge

Trace [MainFormPresenter.ReloadApplicationSettings](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs), [ThumbnailManager.UpdateThumbnailsList/UpdateThumbnailsSize/ThumbnailViewResized](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs), [ThumbnailView](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs) and [MainForm.ThumbnailSizeChanged_Handler](../../Eve-O-Preview/View/Implementation/MainForm.cs). Profile controls, existing-view propagation and resize feedback follow different paths.

Compare persisted dimensions, control values and actual bounds across profile switches and new-client creation. Vary DPI, zoom, frames and event-suppression timing independently. Preserve delayed resize feedback and stable DWM registration; do not assume every geometry discrepancy shares a visibility defect's cause.

## BUG-010: Cycle-list selection jumps after Move Up

[MainForm.cycleGroupMoveClientOrderUpButton_Click/RefreshSelectedCycleGroup](../../Eve-O-Preview/View/Implementation/MainForm.cs) swaps dictionary values and rebuilds the BindingSource without restoring selected identity or scroll position. This is a source-supported selection-reset concern, not evidence of mouse-pointer movement.

Test repeated Move Up from a middle/bottom entry in a scrollable list, including sparse order keys. Keep the moved character selected and maintain sensible scrolling without changing cycle semantics. Broader ordering controls are FEAT-005.

## BUG-011: Cycling recovers only after profile switch

[ThumbnailManager.HandleCurrentProfileChanged/RegisterAllHotkeys](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs) rebuilds delegates. [RefreshHotkeysHandler](../../Eve-O-Preview/Mediator/Handlers/Configuration/RefreshHotkeysHandler.cs) clears/refills existing parsed lists without rebuilding registration. Existing key edits can update captured lists, while group creation/removal/replacement needs separate verification.

Test fresh startup, existing profiles, adding/removing groups, editing bindings and switching profiles. Record registration counts and captured group identities. Inspect [MainForm](../../Eve-O-Preview/View/Implementation/MainForm.cs), [MainFormPresenter](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) and startup subscription order. Recovery after rebuilding registrations would not by itself prove configuration corruption.

## BUG-012: New previews use stale font settings

[SelectedProfileChangedNotificationHandler](../../Eve-O-Preview/Mediator/Handlers/Configuration/SelectedProfileChangedNotificationHandler.cs) updates existing previews. [ThumbnailViewFactory](../../Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs) caches its constructor's FontSettings reference, while profile population/saving can replace that object. [ThumbnailManager.UpdateThumbnailsList](../../Eve-O-Preview/Services/Implementation/ThumbnailManager.cs) does not override the factory font on creation.

Switch between visibly different profiles, then create a preview. Separately edit a font and add a client. Compare existing/new views with active configuration before a later save refreshes them. Fix settings ownership if needed; avoid full visual updates every poll to mask stale construction state.

## Additional regression checks

- Hide All uses a temporary global flag in [ThumbnailToggleHideAllHandler](../../Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailToggleHideAllHandler.cs), separate from per-title disabled state. Test character A → login → B → A while toggling visibility; preserve intentional disabled entries.
- Current source rejects Keys.None in general hotkey handling, provides instant capture tooltips and removes premium gating. Verify these implementations before proposing duplicate fixes.
- Named-pipe timeouts and missing DWM previews have different owners. Investigate each independently; do not assume one log error explains every visible symptom.

For future tasks, name a BUG ID, read the linked subsystem/source/tests, reproduce the scenario and establish acceptance criteria. Record implementation commits and validation when resolved. Keep source-supported concerns distinct from unverified runtime hypotheses.
