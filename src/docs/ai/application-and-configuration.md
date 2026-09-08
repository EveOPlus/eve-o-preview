# Application, settings, and message flow

Read this page for startup, UI changes, persistence, profiles, and message routing. The baseline review was on 2026-09-06; the portable workspace architecture was added on 2026-09-07. Verify current symbols before editing. Native/window behavior is covered in the [window guide](windows-and-thumbnails.md) and [Robin guide](robin.md). The [UI review](ui-review.md) records the prior UI inventory and the capability preservation map for modernization.

## Portable workspace and Windows host

The settings content lives in [Eve-O-Preview.UI](../../Eve-O-Preview.UI/Eve-O-Preview.UI.csproj), a plain `net10.0` Avalonia library. [WorkspaceContract](../../Eve-O-Preview.UI/WorkspaceContract.cs) defines `IWorkspaceBackend`, `WorkspaceSnapshot`, immutable record DTOs, `WorkspaceCommand` and `CommandResult`. Its public contract contains no WinForms controls, HWNDs, GDI types or credentials. The [SettingCatalog](../../Eve-O-Preview.UI/SettingCatalog.cs) describes searchable labels, pages, input kinds and validation; editing only catalog text cannot implement a new native feature.

[WorkspaceForm](../../Eve-O-Preview/View/Implementation/WorkspaceForm.cs) remains a Windows lifetime adapter implementing `IMainFormView`. It embeds the Avalonia content through `WinFormsAvaloniaControlHost`, owns the existing application context and tray icon, and routes presenter updates into coalesced backend notifications. It supplies ordinary minimize/restore and explicit Exit, while the presenter continues to decide close-to-tray versus cleanup. The native previews and overlays continue to use their established WinForms/DWM path.

`MainFormPresenter` still owns the settings save/reload route. Its optional [IAsyncSettingsView](../../Eve-O-Preview/View/Interface/IAsyncSettingsView.cs) contract supplies `CommitSettingsAsync` and `CommitSizeAsync` so the new adapter can await persistence and report failures. The old `Action` callback path remains available for the retained WinForms form and existing isolated regression tests. The two paths share `SaveApplicationSettingsAsync`; do not create a second independent configuration model inside the portable view.

The modern sidebar version label refreshes with backend snapshots: `SetVersionInfo` arrives after the shell is constructed during presenter activation. Until metadata arrives it shows only the application name, without an empty separator.

`WorkspaceForm.ApplyTitleBarTheme` applies the shared public `WorkspaceTheme` palette to the native Windows caption and title text using `DwmSetWindowAttribute`. It runs on theme changes, handle creation and Windows theme/settings notifications; a reentrancy guard prevents recursive native theme notifications. Dark/Light changes are applied before the Legacy-size early return. High-contrast mode restores system caption/text colors. Unsupported attributes on older Windows retain the native fallback; exact caption/text colors require Windows 11. The [Windows DWM attribute contract](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) defines the flags. The existing workspace host check captures only the test window with `PrintWindow` and checks caption pixels across themes; it does not capture a monitor.

These captures verify startup, theme switches and return from Legacy. Forcing the parent's protected `RecreateHandle` with an attached Avalonia host timed out in a separate private-desktop check; arbitrary parent-handle recreation remains an unverified lifecycle path and needs investigation before relying on it. Normal theme switching does not force handle recreation.

[WorkspaceAvaloniaHost](../../Eve-O-Preview/View/CustomControl/WorkspaceAvaloniaHost.cs) bridges monitor DPI changes to the existing Avalonia child. The process already declares PerMonitorV2 in its manifest. Windows sends child HWNDs [WM_DPICHANGED_AFTERPARENT](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged-afterparent), but Avalonia 11.3.20's embedded window updates its render scale through WM_DPICHANGED. The adapter synchronizes after creation/reparenting and DPI changes, passing a child-relative rectangle, then sends WM_SIZE so logical client bounds refresh even when docking already set the same physical bounds. `WorkspaceForm.WndProc` also synchronizes after WinForms completes its DPI layout. Do not pass the top-level desktop rectangle to the child, recreate either HWND, or rebuild workspace content to change DPI. Recheck the [upstream hosting implementation](https://github.com/AvaloniaUI/Avalonia/blob/11.3.20/src/Windows/Avalonia.Win32.Interoperability/WinForms/WinFormsAvaloniaControlHost.cs) when updating Avalonia.

Workspace initial geometry records its current WinForms DPI baseline and scales once. Modern size retained across Legacy switches is stored in device-independent units; restore and minimum sizes use the current monitor DPI. Legacy keeps its 460 by 417 logical client layout. `PreviewSurface` observes the Avalonia root's `ScalingChanged` event so Actual size continues to mean native pixels without recapturing the client. Responsive pages recheck their final width before queued reflows; intermediate DPI bounds must not unnecessarily replace editors or lose focus.

The old [MainForm](../../Eve-O-Preview/View/Implementation/MainForm.cs) and designer remain useful parity references and test targets. The **Legacy theme is a presentation of the new workspace**, not a second legacy application mode. When following the control-by-control sections below, distinguish their old event mechanics from the current workspace commands.

Legacy is maintenance-only and locked to its existing feature set. Retain its current controls and continue bug fixes, compatibility maintenance and updates to existing features. New features and modules target Light/Dark only; shared backend improvements must preserve existing Legacy behavior without adding new Legacy controls. Users are expected to migrate to modern themes. Appearance explains this beside the Legacy choice, and Legacy's idle status area displays **Legacy may lack features.** with a tooltip directing users to Light/Dark for new features. Errors, retry feedback and unapplied-edit guidance take priority over this nonmodal notice.

Legacy recreates the original 460 × 417 client area, 120-pixel tab rail and original control positions at 96 DPI. `WorkspaceForm.ApplyNativeTheme` switches to those compact bounds and restores the previous modern client size when leaving Legacy. Original font/color buttons use the backend's owned Windows dialogs; cancellation does not change configuration. The portable UI contains no Windows dialog types.

Title previews cross the optional `IWorkspacePreviewRenderer` capability. [WindowsWorkspacePreviewRenderer](../../Eve-O-Preview/View/Implementation/WindowsWorkspacePreviewRenderer.cs) paints the actual `OutlinedLabel.OnPaint` into a PNG, using saved values overlaid with current drafts. It preserves the live title's font size, outline/fill order, clipping and signed offsets; it removes the `EVE - ` prefix using the same rule as `ThumbnailView`. Highlight geometry follows `ThumbnailView.HighlightThumbnail`, including wider side insets to preserve image aspect ratio. Rendering reuses an optional background PNG; font/color edits never trigger capture, persist drafts or modify the live rendering/focus path. Actual size is the default, accounting for the Avalonia host's render scale. Modern previews use a compact title-and-border strip; Fit width scales to the available width without shrinking the whole client height into that strip. The native image still uses the configured thumbnail dimensions and can be scrolled to inspect large fonts or offsets. Both sample-only controls (active character and skipped marker) live directly below the sample and stay pinned while settings scroll, including at minimum window size. Legacy retains its original gray font sample at actual size.

The modern sample name prefers a named online client, then a saved character from the active profile's layouts or cycle groups, then `EVE - Sample Name`. `IThumbnailConfiguration.GetKnownClientTitles` exposes saved identities without changing profile JSON or leaking layout dictionaries into the portable UI. Login-only `EVE` windows and example highlight-color entries are excluded. Automatic defaults can improve when discovery supplies clients, but never overwrite a manually edited sample name.

`IWorkspacePreviewCapture` is a separate asynchronous, optional capability. [WindowsWorkspacePreviewCapture](../../Eve-O-Preview/View/Implementation/WindowsWorkspacePreviewCapture.cs) reads the existing process cache and uses `IWindowManager.GetStaticThumbnail`, preferring the sample character, skipping minimized/unavailable/empty black frames and trying another named client when needed. It never activates/restores a client, starts discovery/injection, or registers a DWM thumbnail. A successful PNG is bounded to 960 by 540 pixels, retained only in workspace memory and reused across page rebuilds, theme/profile switches and edits. No screenshot file is saved by the app. Failed attempts are not polled repeatedly; newly discovered characters or the explicit **Refresh image** action allow another attempt. The image's source is labelled separately from the editable sample title. Capture failure keeps the last usable image or the neutral title/border background.

The shared native helper now renders the specified window into memory with `PrintWindow`, without a display-DC copy or screen fallback. The earlier `GetDC`/`BitBlt` implementation could include monitor content on the user's live setup; passing an HWND was not sufficient validation. See [static capture](windows-and-thumbnails.md) for the window-only path and covered-window regression. Restarting the app clears a still retained by an older running build.

### Appearance scope and profile identity

Light and Dark also expose **Appearance → Language** through
[WorkspaceView.Localization](../../Eve-O-Preview.UI/WorkspaceView.Localization.cs).
`ApplicationPreferences.UiLanguage` is global and defaults to `auto`. The backend
`language` command saves it independently of gameplay profiles, publishes the
updated snapshot and rebuilds the workspace while retaining drafts. Legacy remains
English. [WorkspaceLocalization](../../Eve-O-Preview.UI/Localization/WorkspaceLocalization.cs)
resolves regional cultures, loads embedded catalogs and formats display messages
without changing data parsing culture. Arabic uses right-to-left layout; native
preview geometry, zoom anchor grids and raw input values remain left-to-right.
See [catalog maintenance](../../Eve-O-Preview.UI/Localization/README.md) for language
coverage, extension instructions and translation review boundaries.

[ApplicationPreferences](../../Eve-O-Preview/Configuration/Implementation/ApplicationPreferences.cs) owns the single generic `EVE-O Preview.settings.json` file for application-wide preferences. It follows the gameplay-profile resolver's portable/installed decision: use the parent of `IProfileManager.ProfileRootDirectory` when writable, otherwise `%LOCALAPPDATA%\Eve-O Preview`. A temporary write probe checks the preferred directory without retaining a file. This keeps application settings beside the `Profiles` directory, rather than inside one selected profile or in an appearance-only file.

Supported theme values are `Light`, `Dark` and `Legacy`; missing, unrecognized or unreadable preferences fall back to Dark. Global settings now have their own `ConfigVersion`, currently 1, separate from gameplay profile migrations. Files missing this version or older than `ExplicitThemeSelectionVersion` start in Dark regardless of their old Theme value. Reading does not rewrite the file. Any later preference save records the effective theme and current version atomically, preserving other fields; an unrelated menu edit cannot resurrect an older Legacy selection. Manually selecting Legacy after migration records that choice so it survives restart. Keep the explicit-selection threshold at version 1 through unrelated future schema upgrades, retain newer version numbers, and never reset the theme when loading an older gameplay profile.

The document is retained as a JSON object so changing Theme preserves unknown fields reserved for future global preferences. Writes use a temporary file followed by replacement, and update the in-memory theme only after writing. Switching gameplay profiles must retain the selected application theme.

`ThumbnailMenuOrder` shares this same file and atomic write path. [ThumbnailMenuActions](../../Eve-O-Preview.UI/ThumbnailMenuActions.cs) defines stable action/divider IDs and defaults: Minimize, Minimize All, divider, Skip/Resume, divider, Move, Resize. Normalization removes unknown/duplicate IDs and appends missing actions, retaining access after upgrades or hand edits. Saved action-only lists have no implicit dividers; reset applies the new defaults. Explicit dividers retain their positions rather than being inferred from action grouping.

[WorkspaceView.ThumbnailMenu](../../Eve-O-Preview.UI/WorkspaceView.ThumbnailMenu.cs) is the shared compact editor reached from Appearance (Legacy: About → More). Pointer dragging, edge scrolling, Ctrl+Up/Down and arrow buttons move both actions and divider rows. Escape, outside drops, capture loss and changed backend order cancel a drag; one valid drop saves once. `thumbnail-menu-move` validates the ID/destination and keeps an action first/last; `thumbnail-menu-divider-add/remove` edits explicit gaps, while `thumbnail-menu-reset` restores defaults. Adjacent divider rows retain their identities while rearranging. The first item determines the existing double-right-click shortcut.

`ThumbnailMenuTheme` is another field in this global settings file, defaulting to `app` (follow application theme). [ThumbnailMenuThemes](../../Eve-O-Preview.UI/ThumbnailMenuThemes.cs) shares palette colors between the portable live preview and native renderer. The 11 selectable styles include Light, Dark, Graphite, Midnight, OLED Black, Nebula, EVE Carbon and four faction-inspired styles. `thumbnail-menu-theme` validates and saves the selection independently of workspace theme and profile. Legacy uses Order/Theme editor tabs to keep controls inside its original window. Menu layout and theme changes take effect on the next opening; tray styling follows the application theme.

Profile accents are a separate, limited customization: the profile JSON field `UiAccentColor` is exposed by the workspace command key `ProfileAccentColor`. Each profile has its own readable palette choice so users can identify the active profile while retaining the global theme. Preserve that accent through clone, save/reload and profile switch, use a defined fallback for old profiles, and keep it separate from game-preview border/title colors. This is not an arbitrary user stylesheet mechanism.

Light and Dark show the active profile once in the top header: the accented `active-profile-card` contains `profile-picker`. The dropdown ends with a nonselectable divider and **Manage profiles…** (`manage-profiles`), which opens the Profiles page, restores the selected profile in the picker and preserves drafts. There is no separate management button, duplicate sidebar profile card, Profiles navigation button or repeated Overview/management-page profile summary. The rename field and available-profile list remain functional management controls. Selected text uses an explicit theme foreground and ellipsis template; the dropdown retains full names and tooltips. The header must fit beside search at minimum width. Legacy retains its established rail indicator and Profiles tab.

[NativeMenuTheme](../../Eve-O-Preview/View/CustomControl/NativeMenuTheme.cs) applies the application theme to tray and thumbnail context menus when they open. The host updates its current theme; individual menus own their Opening handler, so changing the theme does not require retaining every preview in a global subscription list. Legacy keeps the previous dark/gold menu renderer; Windows high-contrast mode uses system menu rendering. Only menu chrome changes; game content, custom preview labels/highlights, DWM lifetime and focus operations do not change.

The modern sidebar uses `WorkspaceView.Brand` and embedded `Assets/EveOPreviewSilver.png`, the user-selected silver concept (source image `exec-dd25704a-1a01-4fc2-a4dc-a5d74701ded7.png`). Its baked checkerboard was removed with an alpha mask, without recoloring or redrawing the foreground. Preserve the selected artwork's node proportions, connections, highlights and shading; do not tint it for the theme. The earlier original-pixel gold recolor and its source remain separate assets, not the active sidebar resource. The app icon itself remains unchanged.

About displays the supplied transparent EVE Online Partner badge in every theme. `WorkspaceView.Partner` loads `Assets/EvePartner.png` (the PartnerBadge3 artwork) from the UI assembly as an Avalonia resource, shared across page renders without network access. Modern About anchors it at the top right in a compact rounded dark container for readable white lettering in Light and Dark; Legacy retains a compact badge in its fixed layout. Preserve the source artwork and aspect ratio.

### Voluntary support and public portraits

About provides adjacent **Documentation** and **Discord** buttons. The `documentation` command opens the GitHub project page (including its README); `discord` opens the community invite. Donation details keep a smaller **Pop into Discord and say hi** link in the fixed footer. Opening Discord leaves donation details available. Close, Escape or a click on the backdrop dismisses donation details; clicks inside the dialog do not. Dismissal resolves the previous focus control by its stable name because commands can refresh the page behind the dialog (notably Legacy About). Explicit Exit is beside Help & About in the sidebar (beside About in Legacy); the modern donation button spans their combined width.

[WorkspaceView.Support](../../Eve-O-Preview.UI/WorkspaceView.Support.cs) provides the donation invitation at the top of Overview and About, plus a compact portrait button in the modern sidebar. Legacy keeps its tab geometry and exposes the invitation in About / Donate. Messaging presents an ISK gift as an optional thank-you to the developer, explaining how it supports development, community help and time to play. Details open only on an explicit click, support Escape/Close with focus restoration, and copy **Aura Asuna** to the clipboard. The recipient's character ID is **95465272**. There are no automatic prompts, reminders or feature gates. Donation copy makes no promise about future advertising.

`IWorkspacePortraitProvider` keeps image retrieval behind the platform adapter. The singleton [CharacterPortraitCache](../../Eve-O-Preview/Services/Implementation/CharacterPortraitCache.cs) requests 128-pixel JPEG portraits from FC and stores `Cache/Portraits/<characterId>.jpg` under the directory already resolved for `ApplicationPreferences.FilePath`. It therefore shares the portable/installed fallback policy without adding a configuration file. Fresh images are reused for seven days; stale images are returned immediately while a deduplicated background refresh runs. Replacement is atomic, failed refreshes retain the previous image, and retry backoff is one hour. Portrait bytes are also reused in memory. A slow image request never blocks the settings UI.

Future character/ESI views can supply resolved character IDs to the same cache. Name-to-ID resolution and ESI authentication remain future integration work; persisted client window titles are not character IDs. The public portrait does not require ESI login or credentials. The production app does not bundle a portrait; the image under the smoke test's `Fixtures` directory is only a controlled test response.

### Character order and temporary skips

[WorkspaceView.CycleOrder](../../Eve-O-Preview.UI/WorkspaceView.CycleOrder.cs) provides the shared order editor. Its list reserves right-side scrollbar clearance, supports handle dragging with edge scrolling and insertion feedback, and retains Up/Down buttons and Control+Up/Down on the handle. A drop issues one `group-client-move` command with a full title and zero-based destination; the backend validates the destination and updates the existing `ClientsOrder` dictionary in place, retaining hotkey references. Escape, lost pointer capture and drops outside the list cancel without saving. A profile or order change invalidates a pending drag.

The top-right expand control fills the existing workspace window with the order list. It does not resize or maximize EVE-O. Done/Escape returns to the normal layout. Scroll position and settings drafts survive order edits. Legacy retains its compact list and has both a selected-character Skip/Resume action and access to the expanded drag editor.

`client-cycle-skip` and the thumbnail context menu both send [SetClientCycleSkipped](../../Eve-O-Preview/Mediator/Messages/Thumbnails/SetClientCycleSkipped.cs). The handler changes one exact-title exclusion set shared by all groups in the active profile. `ThumbnailConfiguration.SelectCycleSkipProfile` selects a session-only set keyed by the successfully loaded profile path. Skips survive switching away and back, including a reconnect with the same title, and reset on restart; they never enter JSON, remove order entries, hide thumbnails or prevent manual thumbnail activation. `CycleSkipChanged` updates the workspace and existing overlay label immediately without DWM re-registration or focus changes.

The profile's `CycleSkipIndicatorStyle` and `CycleSkipIndicatorColor` customize the marker (circle with slash/red by default, with Pause and Cross alternatives). These ordinary profile settings persist alongside existing title settings; no new settings file is created. The title editor previews a skipped sample using the actual `OutlinedLabel` renderer on Windows. The marker is drawn beside the name, or alone at the title position when names are hidden. See the [cycle selection rules](windows-and-thumbnails.md#hotkeys-and-cycle-semantics).

### Future platform and integration boundary

A future Linux host can implement the workspace backend contract and reuse the UI. It still needs discovery, activation, capture, hotkey, tray, affinity and native-feature implementations or explicit capability gaps. The current host, its `net10.0-windows` target, DWM and Robin remain Windows-specific. Do not describe the app as supporting Linux because the UI library builds without Windows types.

Characters/ESI and DPS overviews are hidden in all themes, including search and Legacy More/About links. `WorkspaceModules.ReservedIds` retains `Characters` and `Dps`, while `WorkspaceView` accepts optional `WorkspaceModule` registrations for ready features. The current Windows host registers none. Without an implemented module, direct navigation to a reserved ID returns to Overview (General in Legacy). Registered modules appear in modern navigation/search; Legacy rejects them. Keep dormant planned-page renderers as internal design references, not user-facing promises.

Future character connections and DPS pages must distinguish window titles, runtime window identity and EVE character IDs. ESI authentication/token storage, data refresh and damage/log interpretation belong behind host services. The [module handoff](../../Eve-O-Preview.UI/AGENTS.md#future-workspace-modules) names the registration/lifecycle route and the unfinished backend work. See the [extension requirements](ui-review.md#future-dps-and-esi-extension-requirements) before implementing real integration controls.


## Composition and lifecycle

`Program.CreateApplicationContainerBuilder` supplies the production Autofac registrations; `InitializeApplicationController` builds them and resolves the controller. `WorkspaceCompositionTests` checks this same graph with a temporary profile root. The `--validate-workspace` executable mode also resolves and renders the workspace with isolated configuration, bypassing normal startup services and the single-instance check. Cake uses this mode to catch runtime assembly/resource failures that an in-process test can miss.

The UI project reference is excluded from Costura embedding and follows normal copy-local updates during development. The .NET SDK bundles it during single-file publishing. This prevents an old loose `Eve-O-Preview.UI.dll` from overriding a newer embedded copy and causing a `TypeLoadException` wrapped by Autofac while constructing `WorkspaceForm`.

[Program.Main](../../Eve-O-Preview/Program.cs) is STA. `--attach-debug-sidecar` takes an early alternate path and does not run ordinary startup. Normal startup configures Serilog, acquires the single-instance token, installs exception handlers, initializes WinForms and Avalonia without starting a separate Avalonia application loop, builds the Autofac controller, launches the debugger sidecar, and runs `MainFormPresenter`.

`GetInstanceToken` first tries `Mutex.OpenExisting`, treats an existing/inaccessible mutex as another instance, and creates a named mutex only after the other failure path. A static field retains the token for the application lifetime. Its comment records a prior Windows mutex failure that paralyzed the .NET finalizer thread and later manifested as out-of-memory exceptions. Preserve that rationale when evaluating a simpler implementation; the source review does not reproduce or independently confirm the historic failure.

`CreateApplicationContainerBuilder` explicitly registers the runtime graph:

| Lifetime | Registrations and purpose |
| --- | --- |
| Singleton instances | Serilog logger, `Hook.GlobalEvents()` from MouseKeyHook, WinForms `ApplicationContext` |
| Singleton low-level services | `WindowManager`, `HookService`, `ProcessMonitor`, `CpuAffinityService` |
| Singleton configuration/events | `ProfileManager`, `ConfigurationStorage`, `AppConfig`, `ThumbnailConfiguration`, `ApplicationPreferences`, `CharacterPortraitCache`, `GlobalEvents` |
| Singleton application objects | `ThumbnailManager`, `ThumbnailViewFactory`, `ThumbnailDescription`, concrete `MainFormPresenter`, `ApplicationController` |
| Per dependency views | `StaticThumbnailView`, `LiveThumbnailView`, `WorkspaceForm` as `IMainFormView` |
| Assembly-scanned handlers | MediatR registration using the main assembly and the Autofac service-provider bridge |

Registration is not inferred from interface names. For example, thumbnail notification handlers request concrete `MainFormPresenter`, then retain it as `IMainFormPresenter`. The presenter creates per-title `ThumbnailDescription` instances itself despite a singleton description registration. `IIocContainer` is a leftover abstraction, while [ApplicationController](../../Eve-O-Preview/ApplicationBase/ApplicationController.cs) uses Autofac's `ILifetimeScope` directly. The `MediatR.Mediator.LicenseKey = "Community"` setting configures that dependency; it is not a user-feature entitlement gate.

[Presenter<TView>.Run](../../Eve-O-Preview/ApplicationBase/Presenter.cs) calls the view's `Show`. The registered [WorkspaceForm.Show](../../Eve-O-Preview/View/Implementation/WorkspaceForm.cs) deliberately hides the base method: it assigns `ApplicationContext.MainForm`, invokes `FormActivated`, then enters the WinForms `Application.Run` loop. Restoring the tray window calls `base.Show()` to avoid entering another application loop. `FormActivated` is this startup callback, not a general OS foreground event. The retained `MainForm` uses the same lifetime convention.

The [MainFormPresenter constructor](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) wires callbacks, subscribes to profile events, requests the current/default profile and sends `ChangeSelectedProfile`. `Activate` subsequently loads settings again, reloads controls, optionally minimizes, and sends `StartService`. `ThumbnailManager` can be constructed through notification-handler resolution during these operations; do not assume every singleton is initialized only at `StartService`.

On close, the workspace first protects unapplied edits; `MinimizeToTray` and the presenter's `_exitApplication` then determine whether to minimize or exit. Explicit exit sets `_exitApplication` before closing the view. Full shutdown cancels the first close request, yields back to the UI pump, awaits `StopService`, saves configuration and closes once cleanup completes. `_shutdownInProgress` and `_shutdownCompleted` prevent repeated close requests from starting duplicate cleanup. [StartStopServiceHandler](../../Eve-O-Preview/Mediator/Handlers/Services/StartStopServiceHandler.cs) stops the manager timer, calls the CPU service's terminal reset, and awaits `HookService.StopAsync` for installation completion and native FPS/audio reset requests. Robin is not unloaded. See [Robin](robin.md) for bounded pipe behavior and what native cleanup responses establish.

[ExceptionHandler](../../Eve-O-Preview/ApplicationBase/ExceptionHandler.cs) uses a deliberately small static-logger/message-box fallback, then exits with code 1. In a DEBUG build with a debugger attached, its setup returns without installing handlers. [LoggerHelpers.WithCallerInfo](../../Eve-O-Preview/Helper/LoggerHelpers.cs) attaches compile-time caller metadata; preserve useful structured logging, but measure logging costs in high-frequency work.

## UI and configuration contracts

Read [IMainFormView](../../Eve-O-Preview/View/Interface/IMainFormView.cs), [WorkspaceForm](../../Eve-O-Preview/View/Implementation/WorkspaceForm.cs), [WindowsWorkspaceBackend](../../Eve-O-Preview/View/Implementation/WindowsWorkspaceBackend.cs), and [MainFormPresenter](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) together for the active path. The host view exposes properties and callbacks; the presenter connects them to settings and messages. The portable UI uses backend snapshots/commands, while Windows implementation types stay in the host.

`WindowsWorkspaceBackend.Read` constructs a fresh snapshot of settings, active profile, available profiles, ordered cycle groups and client visibility. `IsThumbnailIndividuallyDisabled` separates a client's saved visibility from the transient Hide All state; the existing `IsThumbnailDisabled` still combines both for native preview policy. Displaying the snapshot does not mutate configuration.

`ExecuteAsync` serializes commands with `IsBusy`, validates supported settings, mutates only explicit view/configuration bindings and awaits the established commit route. FPS enable, FPS targets and audio changes then call their dedicated mediator handlers. Commands also cover profile operations, group membership/order, shortcut capture/clear, global preview actions, documentation, exit and native font/color selection. Active highlight thickness has a visible 1–6 pixel editor bound to its existing profile property. Do not populate the entire profile from this smaller snapshot: title-keyed layouts, historical aliases and nested settings must retain their existing identities.

[WorkspaceView.AdvancedSettings](../../Eve-O-Preview.UI/WorkspaceView.AdvancedSettings.cs) exposes the previously JSON-only options. Modern **Previews & layout → Advanced** and Legacy **Thumbnail / General → Advanced preview settings** edit snapping, focus-loss hide delay, client check interval, compatibility capture, four resize bounds and login preview coordinates. `HideDelaySeconds` is a UI conversion of the existing integer `HideThumbnailsDelay`: the backend rounds up to whole checks using the current refresh period. Changing that period changes the effective delay; the UI shows the resulting seconds. Historical JSON names, units and storage locations stay unchanged.

The `preview-size-limits` command supplies all four dimensions in `WorkspaceCommand.Settings`, validates minimum <= maximum and existing application limits, and clamps the current size in the same save. [WindowsWorkspaceBackend.AdvancedSettings](../../Eve-O-Preview/View/Implementation/WindowsWorkspaceBackend.AdvancedSettings.cs) updates backend/view bounds together. These advanced commands save through `SaveConfiguration`, then publish [ThumbnailRuntimeSettingsUpdated](../../Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailRuntimeSettingsUpdated.cs); its handler invokes `IThumbnailManager.ApplyRuntimeSettings`. This refreshes timers, hide delay, size bounds, login position and visuals without replacing views unless capture mode changed. Saving failures restore the changed configuration; the UI retains unapplied edits.

**Clients → Character colors & minimization** (Legacy **Active Clients → Colors / priority**) opens the shared online/offline character editor. `WorkspaceSnapshot.ClientPreferences` combines discovered and saved full titles with existing color/priority entries. `client-preferences` validates the exact existing title or prefixes a new offline name, then updates `PerClientActiveClientHighlightColor` and the private `PriorityClients` list in place. Empty color means inherit the profile color; false priority removes the minimization exception. The shared color picker and palette stage edits until **Save character settings**. These are profile settings, independent of the session-only cycling skip and global theme.

The workspace's ordinary toggles and zoom-anchor buttons commit directly; typed/choice fields use Apply or Enter, with a separate discard action. Search (`Ctrl+K`) edits the same catalog entries in place. Draft values are view state rather than profile JSON, and background refresh preserves their text/focus where possible. Switching profiles and closing/exiting prompt before discarding unapplied edits; minimizing to the tray preserves them. Profile selection and the active profile's accent cue remain visible throughout the shell. Failed setting updates retain retry state and expose Retry update, so an already-mutated in-memory value does not prevent retrying persistence.

The dedicated modern preview editor stages its title, highlight, size and hover-zoom controls together. Its persistent Apply changes/Reset edits controls remain visible beside the sample (above the editor at compact widths). The sample updates drafts without applying them to clients. Numeric controls retain fractional font/outline values with invariant parsing. Ordinary window-behavior controls retain immediate application. Legacy preserves the original Enter/leave-field save workflow and native font/color dialogs. `CommandResult.WasCancelled` distinguishes an accepted picker from cancellation so accepting a new value clears superseded drafts while Cancel preserves them. Search results provide a direct route to the specialized title editor.

Modern hotkey recording has an explicit Record action, ten-second capture window and separate Clear action. Escape cancels recording and retains the shortcut. The Windows adapter still uses the existing message-pumping capture handler, yielding first so the listening status can paint; it is not a portable global-hotkey implementation. Group edits preserve full-title members and additional persisted hotkeys beyond the visible slots. Explicit Apply must allow retry after a failed persistence or native-update attempt, even if the value already changed in memory.

A returned save/command result does not prove real native effectiveness: existing hook methods can log failures or return unsuccessful pipe outcomes without failing the entire mediator command. Keep user-facing feedback and validation reports precise about saved settings, requested updates and observed game behavior.

The subsections below retain details of [MainForm](../../Eve-O-Preview/View/Implementation/MainForm.cs) and [its designer](../../Eve-O-Preview/View/Implementation/MainForm.Designer.cs), which remain parity references and regression-test targets. Their event mechanics describe the old form, not Avalonia data binding.

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
| Refresh and visibility | 500 ms refresh; Always on top and minimize-to-tray enabled; hide-active, minimize-inactive, hide-on-lost-focus disabled; hide delay 2 refresh cycles |
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

View callbacks still include async void and some fire-and-forget MediatR dispatch. UI methods must run on their owning thread; storage serialization is not a general transaction over controls and native clients. SaveApplicationSettings copies all controls before its first await. Shutdown cancels the first FormClosing request, yields back to the UI pump, awaits native cleanup, saves and closes once. Repeated close requests do not start duplicate cleanup. This avoids blocking MediatR continuations on the UI thread. Close-to-tray remains profile-scoped. New configurations and profiles omitting `MinimizeToTray` default to true; an explicitly saved false remains false. The existing setting also controls starting in the tray; that behavior is unchanged.

See [current defect status and evidence](reported-bugs.md) rather than treating old suspected defects as behavior to preserve.

## Change checklist and validation

For a new setting, trace model/interface -> JSON/defaults/restrictions -> view property/control event -> presenter reload/save -> request/notification -> runtime consumer -> save/load/profile switch. For a Robin setting include host/server framing and native bounds. Extend relevant regression coverage for a substantive behavior change; see the [coverage matrix and commands](build-and-test.md).

Use isolated profile fixtures for missing/old/current fields, repeat-load idempotence, malformed nested values and renamed/cloned locations. Use the existing private-desktop runner for UI/window tests instead of launching production startup. Verify real game behavior separately when a setting reaches Robin. A passing parser/UI test does not establish full client reconfiguration or native correctness.
