# Application, settings, and message flow

Read this page for startup, UI changes, persistence, profiles, and message routing. Paths link to the implementation reviewed on 2026-09-06; verify current symbols before editing. Native/window behavior is covered in the [window guide](windows-and-thumbnails.md) and [Robin guide](robin.md).


## Composition and lifecycle

[Program.Main](../../Eve-O-Preview/Program.cs) is STA. `--attach-debug-sidecar` takes an early alternate path and does not run ordinary startup. Normal startup configures Serilog, acquires the single-instance token, installs exception handlers, builds the Autofac controller, initializes WinForms, launches the debugger sidecar, and runs `MainFormPresenter`.

`GetInstanceToken` first tries `Mutex.OpenExisting`, treats an existing/inaccessible mutex as another instance, and creates a named mutex only after the other failure path. A static field retains the token for the application lifetime. Its comment records a prior Windows mutex failure that paralyzed the .NET finalizer thread and later manifested as out-of-memory exceptions. Preserve that rationale when evaluating a simpler implementation; the source review does not reproduce or independently confirm the historic failure.

`InitializeApplicationController` explicitly registers the runtime graph:

| Lifetime | Registrations and purpose |
| --- | --- |
| Singleton instances | Serilog logger, `Hook.GlobalEvents()` from MouseKeyHook, WinForms `ApplicationContext` |
| Singleton low-level services | `WindowManager`, `HookService`, `ProcessMonitor`, `CpuAffinityService` |
| Singleton configuration/events | `ProfileManager`, `ConfigurationStorage`, `AppConfig`, `ThumbnailConfiguration`, `GlobalEvents` |
| Singleton application objects | `ThumbnailManager`, `ThumbnailViewFactory`, `ThumbnailDescription`, concrete `MainFormPresenter`, `ApplicationController` |
| Per dependency views | `StaticThumbnailView`, `LiveThumbnailView`, `MainForm` as `IMainFormView` |
| Assembly-scanned handlers | MediatR registration using the main assembly and the Autofac service-provider bridge |

Registration is not inferred from interface names. For example, thumbnail notification handlers request concrete `MainFormPresenter`, then retain it as `IMainFormPresenter`. The presenter creates per-title `ThumbnailDescription` instances itself despite a singleton description registration. `IIocContainer` is a leftover abstraction, while [ApplicationController](../../Eve-O-Preview/ApplicationBase/ApplicationController.cs) uses Autofac's `ILifetimeScope` directly. The `MediatR.Mediator.LicenseKey = "Community"` setting configures that dependency; it is not a user-feature entitlement gate.

[Presenter<TView>.Run](../../Eve-O-Preview/ApplicationBase/Presenter.cs) calls the view's `Show`. [MainForm.Show](../../Eve-O-Preview/View/Implementation/MainForm.cs) deliberately hides the base method: it assigns `ApplicationContext.MainForm`, invokes `FormActivated`, then enters `Application.Run`. Restoring the tray window calls `base.Show()` to avoid entering another application loop. `FormActivated` is this startup callback, not a general OS foreground event.

The [MainFormPresenter constructor](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) wires callbacks, subscribes to profile events, requests the current/default profile and sends `ChangeSelectedProfile`. `Activate` subsequently loads settings again, reloads controls, optionally minimizes, and sends `StartService`. `ThumbnailManager` can be constructed through notification-handler resolution during these operations; do not assume every singleton is initialized only at `StartService`.

On close, `MinimizeToTray` and `_exitApplication` determine whether to minimize or exit. Explicit exit sets `_exitApplication`, then closes the view. Full shutdown runs `StopService` on `Task.Run` and synchronously waits, saves the configuration and allows the close. [StartStopServiceHandler](../../Eve-O-Preview/Mediator/Handlers/Services/StartStopServiceHandler.cs) stops the manager timer, resets cached clients' CPU affinity, and sends FPS disable operations. It does not unload Robin or clear audio muting. The UI thread still waits for this task; the comment about avoiding UI blocking is not proof of a deadlock-free shutdown, especially with a dispatcher timer and synchronous pipe response reads.

[ExceptionHandler](../../Eve-O-Preview/ApplicationBase/ExceptionHandler.cs) uses a deliberately small static-logger/message-box fallback, then exits with code 1. In a DEBUG build with a debugger attached, its setup returns without installing handlers. [LoggerHelpers.WithCallerInfo](../../Eve-O-Preview/Helper/LoggerHelpers.cs) attaches compile-time caller metadata; preserve useful structured logging, but measure logging costs in high-frequency work.

## UI and configuration contracts

Read [IMainFormView](../../Eve-O-Preview/View/Interface/IMainFormView.cs), [MainForm](../../Eve-O-Preview/View/Implementation/MainForm.cs), [its designer](../../Eve-O-Preview/View/Implementation/MainForm.Designer.cs), and [MainFormPresenter](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) together. The view exposes properties and `Action`/`Func` callbacks; the presenter connects those to settings and messages. This is a WinForms presenter architecture, not WPF data binding, despite WPF being enabled in the project.

### Ordinary setting edit

1. A designer-wired event changes a control and invokes `ApplicationSettingsChanged` unless `_suppressEvents` is set.
2. `MainFormPresenter.SaveApplicationSettings` copies view values into the existing `IThumbnailConfiguration`, publishes a frame notification only if the frame setting changed, publishes a font notification on every save, refreshes parsed hotkeys, and sends `SaveConfiguration`.
3. `SaveConfigurationHandler` delegates to storage. Every save while automatic CPU affinity is disabled additionally sends `ResetAllCpuAffinity`; this is not limited to the enabled-to-disabled transition.
4. Some properties are consumed on the next thumbnail refresh; others require an explicit handler or native update. Saving JSON alone does not establish that every active window/client received the new setting.

`SaveApplicationSettings` and several view-facing methods are `async void`. Other paths use `.Result`, `.GetAwaiter().GetResult()` or fire-and-forget MediatR calls. MediatR is an in-process dispatcher; these calls do not inherently marshal to the UI thread or create a serialized background queue. Trace the actual caller thread and awaited boundary before changing concurrency.

### References and feedback suppression

- `CycleGroups`, `FpsLimiterSettings`, and `AudioMuteSettings` are assigned directly from configuration to the view. Their UI handlers mutate the shared objects. The general save method therefore does not need separate FPS/audio copy-back assignments; introducing copies would require new synchronization.
- `TitleFontSettings` is different: the view getter constructs a new object from controls. Most Boolean/numeric settings are copied explicitly during reload/save.
- View setters temporarily set `_suppressEvents`. The presenter uses `_suppressSizeNotifications`; the thumbnail manager uses `_ignoreViewEvents` around programmatic geometry changes. Preserve the feedback boundaries rather than deleting apparently repetitive setters.
- A user thumbnail resize changes configuration, propagates size to other views, publishes `ThumbnailActiveSizeUpdated`, and updates the main form under suppression. A main-form size edit publishes `ThumbnailConfiguredSizeUpdated` to the manager. These are different directions of the same feedback loop.
- The check state in the All Clients list means **disabled/hidden**, not enabled. The presenter caches descriptions by full title and persists `description.IsDisabled` through `ToggleThumbnail`.

`ViewZoomAnchorConverter.Convert` intentionally casts between `ZoomAnchor` and `ViewZoomAnchor` by integer value. Both enum orders (`NW, N, NE, W, C, E, SW, S, SE`) must stay aligned if this converter is retained. This is a compatibility shortcut with a concrete ordering dependency.

### FPS and audio edit paths

FPS numeric controls commit on `Leave`; their handlers update the shared object, request a configuration save and invoke `FpsLimiterChanged`. The `Go` button (`btnDummyFpsSave`) has no click handler: it gives focus somewhere to move so the edit's `Leave` event runs. Enable/disable has a separate callback and handler. Adding an unconditional click operation can duplicate an existing commit.

Custom audio `TextChanged` validates only; `Enter`, `Leave`, and form close call `SaveCustomMutedEventIds`. [AudioMuteSettings.TryParseCustomMutedEventIds](../../Eve-O-Preview/Configuration/Implementation/AudioMuteSettings.cs) accepts comma-separated decimal `uint` values, trims/skips empty entries, and deduplicates in input order. Any invalid token rejects the whole edit; invalid text leaves the previous settings intact. Empty input clears custom IDs. Valid unchanged lists return without another save/send. Presets and custom IDs are combined later by `HookService`; see [the exact pipe and native behavior](robin.md).

The FPS/Audio tab contains `fpsBottomPanel` with `AutoScroll=true` and anchored group boxes. Keep the hint's Enter/leave behavior, validation colors, and control wiring in sync when changing it. [CustomAudioTests](../../tests/Eve-O-Preview.Tests/Checks/CustomAudioTests.cs) checks the actual production UI methods, persistence and host pipe sender, but not native sound interception.

## Message routing map

Messages live under [Mediator/Messages](../../Eve-O-Preview/Mediator/Messages); handlers live under [Mediator/Handlers](../../Eve-O-Preview/Mediator/Handlers). Search the message type to find both the origin and receiver. Several paths use `INotification`; others use `IRequest`, optionally with a response. Folder/type spelling is not always identical.

| Message or group | Receiver and outcome |
| --- | --- |
| `StartService`, `StopService` | `StartStopServiceHandler`: manager timer; stop also affinity/FPS reset |
| `SaveConfiguration` | `SaveConfigurationHandler` -> `ConfigurationStorage.Save` |
| `ChangeSelectedProfile` | `ChangeSelectedProfileHandler`: set current location, load, publish selected notification |
| `SelectedProfileChangedNotification` | Handler -> `GlobalEvents.CurrentProfileChanged`; also publishes font update |
| `ProfileListChangedNotification` | Handler -> `GlobalEvents.ProfileListChanged` -> presenter -> view list |
| `GetCurrentProfileLocation` | Handler returns storage's current location, falling back to `ProfileManager.GetDefaultProfileLocation` |
| `CloneCurrentProfile`, `DeleteCurrentProfile`, `RenameCurrentProfile` | Corresponding handler -> `ProfileManager` operation |
| `CaptureNewHotkey` -> `CaptureNewHotkeyResponse` | Capture handler listens for input and checks duplicates; returns validity, key data/text and error |
| `RefreshHotkeys` | Refresh handler cleans null strings and rebuilds parsed key collections; does not itself call `RegisterAllHotkeys` |
| `SetFpsLimiter` | Handler sends target updates through `HookService` to known clients |
| `SetFpsLimiterEnabled` | Handler installs hooks if enabled, sends zero FPS targets if disabled |
| `SetAudioSettings` | Handler installs/updates hooks, then sends mute settings to known clients |
| `UpdateCpuAffinity` | Handler resolves active/next/previous HWNDs from the cache, calls CPU service |
| `ResetAllCpuAffinity` | Handler resets the cache's known processes |
| `ThumbnailListUpdated` | Handler adds/removes presenter descriptions and main-form list entries |
| `ThumbnailConfiguredSizeUpdated` | Handler -> manager `UpdateThumbnailsSize` |
| `ThumbnailActiveSizeUpdated` | Handler -> presenter `UpdateThumbnailSize` |
| `ThumbnailFrameSettingsUpdated` | Handler -> manager `UpdateThumbnailFrames` |
| `ThumbnailFontTitleSettingsUpdated` | `ThumbnailTitleFontSettingsUpdatedHandler` -> manager `UpdateThumbnailTitleFont` |
| `ThumbnailLocationUpdated` | Handler saves title/active-client-relative location, then sends `SaveConfiguration` |
| `ThumbnailToggleHideAll` | Handler toggles transient configuration state and publishes changed notification |
| `ThumbnailToggleHideAllChangedNotification` | Handler -> presenter -> button/tab status; manager observes hide state on refresh |
| `MinimizeClient`, `MinimizeAllClients` | Handlers call `WindowManager.MinimizeWindow(..., true)`; source filenames use `Minimise` |

[GlobalEvents](../../Eve-O-Preview/Services/Implementation/GlobalEvents.cs) is a synchronous bridge for two profile events. Presenter listeners reload controls/refresh lists; the manager's current-profile listener re-registers global hotkey delegates. Do not assume this bridge reapplies every feature when a profile changes.

## Persisted model and defaults

[ThumbnailConfiguration](../../Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs) is the active model behind [IThumbnailConfiguration](../../Eve-O-Preview/Configuration/Interface/IThumbnailConfiguration.cs). `AppConfig.ConfigFileName` is retained but is not how the active profile path is resolved.

| Area | Defaults and persisted contract |
| --- | --- |
| Schema and identity | `ConfigVersion=3`; full title strings key layouts, disabled entries, priorities, highlights and cycle membership |
| Refresh and visibility | 500 ms refresh; Always on top enabled; minimize-to-tray, hide-active, minimize-inactive, hide-on-lost-focus disabled; hide delay 2 refresh cycles |
| Renderer | `EnableCompatibilityMode=false`, serialized as `CompatibilityMode`; factory selects live/static when constructing a view |
| Geometry | 384x216; min 192x108, max 960x540; snapping enabled; login location `(5,5)` |
| Appearance | `ThumbnailOpacity=0.5`, JSON `ThumbnailsOpacity`; overlays on, frames off; active highlight off, thickness 3 |
| Zoom | Off, factor 2, NW; enabled property serialized as `EnableThumbnailZoom` |
| Layout dictionaries | Private `[JsonProperty]` members `PerClientLayout`, `FlatLayout`, `ClientLayout`, `DisableThumbnail`, `PriorityClients` remain part of the JSON contract |
| Cycle groups | Ordered `SortedDictionary<int,string> ClientsOrder`; forward/backward hotkey string lists; parsed key lists are `[JsonIgnore]` |
| FPS/audio | Shared nested models; desktop FPS disabled with 144/20/45 foreground/background/predicted targets; both audio presets off and custom list empty |
| CPU affinity | `EnableAutomaticCpuAffinity=true` |
| Runtime-only state | `IsTemporarilyHidingAllThumbnails` and parsed general hotkeys are `[JsonIgnore]` |

`GetThumbnailLocation` checks the active client's per-client layout only when per-client layouts are enabled and an active-client name is available, then falls back to the flat layout and finally the supplied default. `SetThumbnailLocation` ignores per-client writes without an active-client name. Setting `EnablePerClientThumbnailLayouts=false` clears that dictionary; setting `EnableClientLayoutTracking=false` clears stored game-window layouts. These setters have data effects even when invoked during loading; preserve or deliberately migrate the contract if changing them.

`IsThumbnailDisabled` combines the transient Hide All flag and the stored per-title flag. Layouts and per-client options are distinct from process-handle cache state; a character-title change can affect persisted lookup without changing the HWND.

`ApplyRestrictions` clamps refresh to 300-1000 ms, size to configured min/max, opacity to 20-100 percent, zoom factor to 2-10 and highlight thickness to 1-6. It does not comprehensively validate nested settings, null collections, fonts or FPS controls. It runs after successful population/migrations, not automatically on every property setter or save. Native FPS validation is separate.

## Profile storage and migration

[ProfileManager](../../Eve-O-Preview/Configuration/Implementation/ProfileManager.cs) prefers an existing Profiles directory beside the executable, then LocalAppData, creating a root when needed. It ensures Default exists, protects Default from rename/delete, refreshes the cached locations, and keeps the shared selected location current after a rename. Clone saves current state first, including a fresh Default with no JSON yet. Profile names reject blank/trimmed, traversal, trailing-dot, invalid and reserved Windows names.

[ConfigurationStorage](../../Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs) loads into a fresh default candidate, ignores explicit nulls, applies restrictions and migrations, then populates the existing singleton. Invalid input returns false; ChangeSelectedProfile restores the previous location and does not publish a selected-profile notification. Missing files load defaults. A committed load whose hotkey subscriber fails is logged as a subscriber failure, not rolled back inconsistently. Load/save are serialized; saves write a temporary file and replace the destination only after writing succeeds.

Version 1 migrates both old cycle groups while preserving clients with duplicate order values. Version 2 groups legacy client hotkeys with distinct incremented keys and stores ConfigVersion=3. Repeat load/save is covered by isolated workflow tests. Full `EVE - ...` titles and historical JSON names remain persisted identities.

Profile notification updates the UI and existing views, timer/hide intervals, font/geometry/frames and native settings, and resets prior affinity. Compatibility changes recreate views; ordinary profile changes preserve live DWM. Native install/reuse applies both FPS (including disabled targets) and audio. The factory reads current font/renderer settings when creating a new preview.

## Hotkey capture and UI details

[CaptureNewHotkeyHandler](../../Eve-O-Preview/Mediator/Handlers/Configuration/CaptureNewHotkeyHandler.cs) uses global `KeyDown` despite the method name `CaptureNextKeyUp`. It ignores modifier-only presses, records `KeyData`, pumps `Application.DoEvents()` and sleeps 15 ms while waiting, and unregisters the temporary delegate in `finally`. The presenter requests a 10,000 ms timeout and the form disables itself while listening. This is a synchronous message-pumping workaround: replacing it with a blocking wait without a message pump can stop input delivery; a redesign needs deliberate reentrancy/cancellation handling.

Escape clears a binding to `Keys.None`. Duplicate detection includes general bindings and all groups; retaining the currently edited binding is permitted. [RefreshHotkeysHandler](../../Eve-O-Preview/Mediator/Handlers/Configuration/RefreshHotkeysHandler.cs) reparses strings, while actual global delegate registration/consumption lives in `ThumbnailManager`. Read [the window guide](windows-and-thumbnails.md) before changing cycling or registration semantics.

[ClientNameInputBox](../../Eve-O-Preview/View/Implementation/ClientNameInputBox.cs) shows known client names and allows text selection. Its [designer](../../Eve-O-Preview/View/Implementation/ClientNameInputBox.Designer.cs) also declares interface inheritance and properties, so designer files cannot universally be treated as layout-only. Do not rename `EVE - ...` strings merely to match the displayed text.

[OutlinedLabel](../../Eve-O-Preview/View/CustomControl/OutlinedLabel.cs) chooses smoothing deliberately to avoid artifacts against transparent backgrounds: outline drawing starts above 0.1 width, and fill antialiasing is enabled only above 1.9. [DarkModeContextMenuStrip](../../Eve-O-Preview/View/CustomControl/DarkModeContextMenuStrip.cs) has private nested renderer/color-table classes alongside similarly named types in [DarkGoldRenderer.cs](../../Eve-O-Preview/View/CustomControl/DarkGoldRenderer.cs); follow constructor/type resolution before styling. The live About tab belongs to MainForm. The separate `PreviewToy.AboutBox` files are excluded by the current project and contain stale resource references.

## Remaining boundaries

The settings workflow now protects font/size event suppression, fractional and incomplete numeric input, final-group deletion, cancelled/empty client selections, retained Move Up selection and hotkey capture timeout. RefreshHotkeys reparses and publishes HotkeysChanged so registrations follow group replacement as well as edits. FontSettings itself supplies defaults for partial nested profiles.

View callbacks still include async void and some fire-and-forget MediatR dispatch. UI methods must run on their owning thread; storage serialization is not a general transaction over controls and native clients. SaveApplicationSettings copies all controls before its first await. Shutdown cancels the first FormClosing request, yields back to the UI pump, awaits native cleanup, saves and closes once. Repeated close requests do not start duplicate cleanup. This avoids blocking MediatR continuations on the UI thread. Close-to-tray remains profile-scoped.

See [current defect status and evidence](reported-bugs.md) rather than treating old suspected defects as behavior to preserve.

## Change checklist and validation

For a new setting, trace model/interface -> JSON/defaults/restrictions -> view property/control event -> presenter reload/save -> request/notification -> runtime consumer -> save/load/profile switch. For a Robin setting include host/server framing and native bounds. Extend relevant regression coverage for a substantive behavior change; see the [coverage matrix and commands](build-and-test.md).

Use isolated profile fixtures for missing/old/current fields, repeat-load idempotence, malformed nested values and renamed/cloned locations. Use the existing private-desktop runner for UI/window tests instead of launching production startup. Verify real game behavior separately when a setting reaches Robin. A passing parser/UI test does not establish full client reconfiguration or native correctness.
