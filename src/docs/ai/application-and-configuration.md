# Application, settings, and message flow

Read this page for startup, UI changes, persistence, profiles, and message routing. Native/window behavior is covered in the [window guide](windows-and-thumbnails.md) and [Robin guide](robin.md). The [UI review](ui-review.md) records the prior UI inventory and the capability preservation map for modernization.

## Portable workspace and Windows host

The settings content lives in [Eve-O-Preview.UI](../../Eve-O-Preview.UI/Eve-O-Preview.UI.csproj), a plain `net10.0` Avalonia library. [WorkspaceContract](../../Eve-O-Preview.UI/WorkspaceContract.cs) defines `IWorkspaceBackend`, `WorkspaceSnapshot`, immutable record DTOs, `WorkspaceCommand` and `CommandResult`. Its public contract contains no WinForms controls, HWNDs, GDI types or credentials. The [SettingCatalog](../../Eve-O-Preview.UI/SettingCatalog.cs) describes searchable labels, pages, input kinds and validation; editing only catalog text cannot implement a new native feature.

[WorkspaceWindow](../../Eve-O-Preview/View/Implementation/WorkspaceWindow.cs) is the Avalonia desktop window implementing `IMainFormView`. Its content is the existing portable `WorkspaceView`; there is no embedded child host or second toolkit loop. It owns Avalonia `TrayIcon`/`NativeMenu` and coalesces presenter refreshes through `Dispatcher.UIThread`. Previews and overlays also use Avalonia window hosts; Windows adapters retain DWM and native composition.

`MainFormPresenter` owns settings save/reload. [IAsyncSettingsView](../../Eve-O-Preview/View/Interface/IAsyncSettingsView.cs) supplies awaitable settings/size commits so the workspace reports persistence failures. Compatibility `Action` callbacks remain for original-form test fixtures; both paths share `SaveApplicationSettingsAsync`. The production backend owns asynchronous shortcut capture and client selection; no synchronous presenter modal/capture path remains.

The modern sidebar version label refreshes with backend snapshots: `SetVersionInfo` arrives after the shell is constructed during presenter activation. Until metadata arrives it shows only the application name, without an empty separator.

The registered Augments module (`Dps`) appears under WORKSPACE, immediately after
Previews & layout, with the shared bar-chart navigation icon. Its existing module
identity and settings-search section destinations remain the navigation route.

`WorkspaceWindow.ApplyTitleBarTheme` applies the shared palette to the native caption/text using `DwmSetWindowAttribute` on opening and theme/settings changes. A guard prevents recursive notifications. High contrast restores system colors; exact caption colors require Windows 11, with native fallback for unsupported attributes. [The DWM contract](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) defines these flags. `WorkspaceHostTests` captures only its isolated window, including caption pixels.

Theme and DPI updates retain the native window. Validation exercises the actual Avalonia host; older embedded-host captures do not establish this host's behavior.

Avalonia 11.3.20 owns `WM_DPICHANGED` and workspace render scaling directly. The process declares PerMonitorV2. Workspace sizes and the modern size retained across Legacy switches are device-independent units; Legacy keeps 460 by 417 logical units. After changing decorations, the workspace corrects client pixels using the current native frame extents and Avalonia render scale, without moving or activating the window. No parent/child DPI synchronization adapter remains. `WorkspaceHostTests` sends synthetic 100/125/150/200 percent transitions, records requested/actual native rectangles and checks retained HWND, content, focus and draft. Synthetic messages do not establish real monitor selection/nonclient metrics.

`PreviewSurface` observes the Avalonia root's `ScalingChanged` event so Actual size means native pixels without recapturing the client. Responsive pages recheck final width before queued reflows; intermediate DPI bounds must not replace editors or lose focus.

The original [MainForm](../../tests/Eve-O-Preview.Tests/LegacyReference/MainForm.cs) and designer are non-shipped test references. The **Legacy theme is a presentation of the production Avalonia workspace**, not a second application. The control sections below distinguish old fixture event mechanics from current workspace commands.

Legacy is maintenance-only and locked to its existing feature set. Retain its current controls and continue bug fixes, compatibility maintenance and updates to existing features. New features and modules target Light/Dark only; shared backend improvements must preserve existing Legacy behavior without adding new Legacy controls. Users are expected to migrate to modern themes. Appearance explains this beside the Legacy choice, and Legacy's idle status area displays **Legacy may lack features.** with a tooltip directing users to Light/Dark for new features. Errors, retry feedback and unapplied-edit guidance take priority over this nonmodal notice.

Legacy retains the original 460 by 417 logical client area, 120-pixel tab rail and control positions. `WorkspaceWindow.ApplyNativeTheme` selects those bounds and restores modern client size on exit. Existing font/color actions use owned [WorkspaceDialogs](../../Eve-O-Preview/View/Implementation/WorkspaceDialogs.cs) Avalonia dialogs with shared pickers; cancel does not mutate configuration. The font sample includes all four style flags and point sizes; the expanded color editor shares the workspace palette and RGB controls. Dialog labels follow the workspace language, with Legacy remaining English.

Title previews cross the optional `IWorkspacePreviewRenderer` capability. [WindowsWorkspacePreviewRenderer](../../Eve-O-Preview/View/Implementation/WindowsWorkspacePreviewRenderer.cs) uses the same `OverlaySceneRasterizer` title path as the native overlay to produce a PNG, using saved values overlaid with current drafts. It preserves the live title's font size, outline/fill order, clipping and signed offsets; it removes the `EVE - ` prefix using the same rule as `ThumbnailView`. Highlight geometry follows `ThumbnailView.HighlightThumbnail`, including wider side insets to preserve image aspect ratio. Rendering reuses an optional background PNG; font/color edits never trigger capture, persist drafts or modify the live rendering/focus path. Actual size is the default, accounting for the Avalonia host's render scale. Modern previews use a compact title-and-border strip; Fit width scales to the available width without shrinking the whole client height into that strip. The native image still uses the configured thumbnail dimensions and can be scrolled to inspect large fonts or offsets. Both sample-only controls (active character and skipped marker) live directly below the sample and stay pinned while settings scroll, including at minimum window size. Legacy retains its original gray font sample at actual size.

The modern sample name prefers a named online client, then a saved character from the active profile's layouts or cycle groups, then `EVE - Sample Name`. `IThumbnailConfiguration.GetKnownClientTitles` exposes saved identities without changing profile JSON or leaking layout dictionaries into the portable UI. Login-only `EVE` windows and example highlight-color entries are excluded. Automatic defaults can improve when discovery supplies clients, but never overwrite a manually edited sample name.

`IWorkspacePreviewCapture` is a separate asynchronous, optional capability. [WindowsWorkspacePreviewCapture](../../Eve-O-Preview/View/Implementation/WindowsWorkspacePreviewCapture.cs) reads the existing process cache and uses `IWindowManager.GetStaticThumbnail`, preferring the sample character, skipping minimized/unavailable/empty black frames and trying another named client when needed. It never activates/restores a client, starts discovery/injection, or registers a DWM thumbnail. A successful PNG is bounded to 960 by 540 pixels, retained only in workspace memory and reused across page rebuilds, theme/profile switches and edits. No screenshot file is saved by the app. Failed attempts are not polled repeatedly; newly discovered characters or the explicit **Refresh image** action allow another attempt. The image's source is labelled separately from the editable sample title. Capture failure keeps the last usable image or the neutral title/border background.

The shared native helper now renders the specified window into memory with `PrintWindow`, without a display-DC copy or screen fallback. The earlier `GetDC`/`BitBlt` implementation could include monitor content on the user's live setup; passing an HWND was not sufficient validation. See [static capture](windows-and-thumbnails.md) for the window-only path and covered-window regression. Restarting the app clears a still retained by an older running build.

### Appearance scope and profile identity

Modern font and colour editors reuse [WorkspacePickers](../../Eve-O-Preview.UI/WorkspacePickers.cs),
including title/border settings, per-client colours, generic setting/search rows,
profile identity and Augments. Fonts/items use searchable fields plus full-list
popups with visible vertical scrollbars. Colour popups provide a spectrum,
RGB sliders/numbers, hex entry and a shared 24-colour palette. Selection edits
the caller's existing draft; each page retains its Apply/Save action.
`WorkspaceApp` loads the matching Avalonia colour-control theme once.

**Previews & layout → Title & highlight → Current solar system** controls system
visibility, colour, position relative to the title and a separate font size (or
**Use title size**). These fields use the normal preview drafts and Apply/Reset
controls. `WindowsWorkspaceBackend` maps the `ShowCurrentSolarSystem` and
`SolarSystem*` keys to the existing global log preferences through
`SaveLogSettingsAsync`, including clients with combat overrides. They do not
change gameplay profiles or DPS appearance. The sample shows the selected
character's known system, or Jita as an illustrative fallback. Legacy excludes
these controls and the system sample.

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

[NativeMenuTheme](../../Eve-O-Preview/View/CustomControl/NativeMenuTheme.cs) applies the saved shared palettes and action order to Avalonia thumbnail context menus. Each menu owns its Opening handler; no global list retains preview instances. The workspace also sets the application's Avalonia theme for tray/native menu presentation. The former Forms renderers are non-shipped fixtures. Menu changes preserve native image delivery, custom title/highlight settings and focus operations.

The modern sidebar uses `WorkspaceView.Brand` and embedded `Assets/EveOPreviewSilver.png`, the user-selected silver concept (source image `exec-dd25704a-1a01-4fc2-a4dc-a5d74701ded7.png`). Its baked checkerboard was removed with an alpha mask, without recoloring or redrawing the foreground. Preserve the selected artwork's node proportions, connections, highlights and shading; do not tint it for the theme. The earlier original-pixel gold recolor and its source remain separate assets, not the active sidebar resource. The app icon itself remains unchanged.

About displays the supplied transparent EVE Online Partner badge in every theme. `WorkspaceView.Partner` loads `Assets/EvePartner.png` (the PartnerBadge3 artwork) from the UI assembly as an Avalonia resource, shared across page renders without network access. Modern About anchors it at the top right in a compact rounded dark container for readable white lettering in Light and Dark; Legacy retains a compact badge in its fixed layout. Preserve the source artwork and aspect ratio.

### Voluntary support and public portraits

About provides adjacent **Documentation** and **Discord** buttons. The `documentation` command opens the GitHub project page (including its README); `discord` opens the community invite. Donation details keep a smaller **Pop into Discord and say hi** link in the fixed footer. Opening Discord leaves donation details available. Close, Escape or a click on the backdrop dismisses donation details; clicks inside the dialog do not. Dismissal resolves the previous focus control by its stable name because commands can refresh the page behind the dialog (notably Legacy About). Explicit Exit is beside Help & About in the sidebar (beside About in Legacy); the modern donation button spans their combined width.

[WorkspaceView.Support](../../Eve-O-Preview.UI/WorkspaceView.Support.cs) provides the donation invitation at the top of Overview and About, plus a compact portrait button in the modern sidebar. Legacy keeps its tab geometry and exposes the invitation in About / Donate. Messaging presents an ISK gift as an optional thank-you to the developer, explaining how it supports development, community help and time to play. Details open only on an explicit click, support Escape/Close with focus restoration, and copy **Aura Asuna** to the clipboard. The recipient's character ID is **95465272**. There are no automatic prompts, reminders or feature gates. Donation copy makes no promise about future advertising.

`IWorkspacePortraitProvider` keeps image retrieval behind the platform adapter. The singleton [CharacterPortraitCache](../../Eve-O-Preview/Services/Implementation/CharacterPortraitCache.cs) requests 128-pixel JPEG portraits from FC and stores `Cache/Portraits/<characterId>.jpg` under the directory already resolved for `ApplicationPreferences.FilePath`. It therefore shares the portable/installed fallback policy without adding a configuration file. Fresh images are reused for seven days; stale images are returned immediately while a deduplicated background refresh runs. Replacement is atomic, failed refreshes retain the previous image, and retry backoff is one hour. Portrait bytes are also reused in memory. A slow image request never blocks the settings UI.

Modern Clients and cycle order now use the shared character identity map to resolve names through public ESI and reuse this portrait cache. Discovery obtains missing EVE user IDs from a running client's launch-token subject, retaining only derived IDs; no launch token is saved or sent to ESI. See [character identities and portraits](character-identities.md) for persistence, weekly refresh, reader boundaries and tests. ESI authentication remains separate future work. The production app does not bundle a portrait; the image under the smoke test's `Fixtures` directory is only a controlled test response.

### Character order and temporary skips

[WorkspaceView.CycleOrder](../../Eve-O-Preview.UI/WorkspaceView.CycleOrder.cs) provides the shared order editor. Its list reserves right-side scrollbar clearance, supports handle dragging with edge scrolling and insertion feedback, and retains Up/Down buttons and Control+Up/Down on the handle. A drop issues one `group-client-move` command with a full title and zero-based destination; the backend validates the destination and updates the existing `ClientsOrder` dictionary in place, retaining hotkey references. Escape, lost pointer capture and drops outside the list cancel without saving. A profile or order change invalidates a pending drag.

The top-right expand control fills the existing workspace window with the order list. It does not resize or maximize EVE-O. Done/Escape returns to the normal layout. Scroll position and settings drafts survive order edits. Legacy retains its compact list and has both a selected-character Skip/Resume action and access to the expanded drag editor.

`client-cycle-skip` and the thumbnail context menu both send [SetClientCycleSkipped](../../Eve-O-Preview/Mediator/Messages/Thumbnails/SetClientCycleSkipped.cs). The handler changes one exact-title exclusion set shared by all groups in the active profile. `ThumbnailConfiguration.SelectCycleSkipProfile` selects a session-only set keyed by the successfully loaded profile path. Skips survive switching away and back, including a reconnect with the same title, and reset on restart; they never enter JSON, remove order entries, hide thumbnails or prevent manual thumbnail activation. `CycleSkipChanged` updates the workspace and existing overlay label immediately without DWM re-registration or focus changes.

The profile's `CycleSkipIndicatorStyle` and `CycleSkipIndicatorColor` customize the marker (circle with slash/red by default, with Pause and Cross alternatives). These ordinary profile settings persist alongside existing title settings; no new settings file is created. The title editor previews a skipped sample using the shared `OverlaySceneRasterizer` on Windows. The marker is drawn beside the name, or alone at the title position when names are hidden. See the [cycle selection rules](windows-and-thumbnails.md#hotkeys-and-cycle-semantics).

### Future platform and integration boundary

A future Linux host can implement the workspace backend contract and reuse the UI. It still needs discovery, activation, capture, hotkey, tray, affinity and native-feature implementations or explicit capability gaps. The current host, its `net10.0-windows` target, DWM and Robin remain Windows-specific. Do not describe the app as supporting Linux because the UI library builds without Windows types.

Characters/ESI remains hidden. `WorkspaceModules.ReservedIds` retains `Characters` and `Dps`; the Windows host registers the implemented `Dps` module as **Augments** through `CombatLogView.CreateModule`. Without a registered module, reserved navigation returns to Overview (General in Legacy). Legacy rejects both modules. See [combat logs](combat-logs.md) for automatic/manual folder selection, ingestion/storage, shared and per-client settings, alpha/DPS/repair/location graphics, incoming-name flashes and simulation through the production event path with temporary statistics.

Future integrations must distinguish window titles, runtime window identity and EVE character IDs. ESI authentication/token storage remains unimplemented; damage/log interpretation belongs to the host's `CombatLogService`. The [module handoff](../../Eve-O-Preview.UI/AGENTS.md#future-workspace-modules) names the registration/lifecycle route. See the [extension requirements](ui-review.md#future-dps-and-esi-extension-requirements) before adding other integrations.


## Composition and lifecycle

`Program.CreateApplicationContainerBuilder` supplies the production Autofac registrations; `InitializeApplicationController` builds them and resolves the controller. `WorkspaceCompositionTests` checks this same graph with a temporary profile root. The `--validate-workspace` executable mode also resolves and renders the workspace with isolated configuration, bypassing normal startup services and the single-instance check. Cake uses this mode to catch runtime assembly/resource failures that an in-process test can miss.

The UI project reference is excluded from Costura embedding and follows normal copy-local updates during development. The .NET SDK bundles it during single-file publishing. This prevents an old loose `Eve-O-Preview.UI.dll` from overriding a newer embedded copy and causing a `TypeLoadException` wrapped by Autofac while constructing `WorkspaceWindow`.

[Program.Main](../../Eve-O-Preview/Program.cs) is STA. `--attach-debug-sidecar` takes an early alternate path. Normal startup configures logging, acquires the historical single-instance token, initializes Avalonia with `SetupWithClassicDesktopLifetime`, installs exception handlers, builds Autofac, resolves presenter and singleton workspace, launches the sidecar, then calls the one desktop lifetime's `Start`. `OnMainWindowClose` keeps hidden-to-tray operation alive. Services are disposed when the lifetime returns. `--validate-workspace` and `--validate-desktop` use isolated production composition without discovery, Robin injection or personal profiles.

`GetInstanceToken` first tries `Mutex.OpenExisting`, treats an existing/inaccessible mutex as another instance, and creates a named mutex only after the other failure path. A static field retains the token for the application lifetime. Its comment records a prior Windows mutex failure that paralyzed the .NET finalizer thread and later manifested as out-of-memory exceptions. Preserve that rationale when evaluating a simpler implementation; the source review does not reproduce or independently confirm the historic failure.

`CreateApplicationContainerBuilder` explicitly registers the runtime graph:

| Lifetime | Registrations and purpose |
| --- | --- |
| Singleton instances | Logger, Windows `IGlobalPointerInput`, Avalonia `IClassicDesktopStyleApplicationLifetime` |
| Singleton low-level services | `WindowManager`, `HookService`, `ProcessMonitor`, `CpuAffinityService` |
| Singleton configuration/events | `ProfileManager`, `ConfigurationStorage`, `AppConfig`, `ThumbnailConfiguration`, `ApplicationPreferences`, `CharacterPortraitCache`, `GlobalEvents` |
| Singleton application objects | `ThumbnailManager`, `ThumbnailViewFactory`, `ThumbnailDescription`, concrete `MainFormPresenter`, `ApplicationController` |
| Views | Per dependency `StaticThumbnailView`/`LiveThumbnailView`; singleton `WorkspaceWindow` as itself and `IMainFormView` |
| Assembly-scanned handlers | MediatR registration using the main assembly and the Autofac service-provider bridge |

Registration is not inferred from interface names. For example, thumbnail notification handlers request concrete `MainFormPresenter`, then retain it as `IMainFormPresenter`. The presenter creates per-title `ThumbnailDescription` instances itself despite a singleton description registration. `IIocContainer` is a leftover abstraction, while [ApplicationController](../../Eve-O-Preview/ApplicationBase/ApplicationController.cs) uses Autofac's `ILifetimeScope` directly. The `MediatR.Mediator.LicenseKey = "Community"` setting configures that dependency; it is not a user-feature entitlement gate.

[Presenter<TView>.Run](../../Eve-O-Preview/ApplicationBase/Presenter.cs) remains a simple `Show` helper. Normal startup resolves the presenter before assigning `lifetime.MainWindow`; the lifetime then opens it. `WorkspaceWindow.Opened` invokes `FormActivated` once after callbacks are installed, including when startup preferences immediately minimize/hide to tray. Restore uses ordinary Avalonia `Show`/`Activate` and never repeats service activation.

The [MainFormPresenter constructor](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) wires callbacks, subscribes to profile events, requests the current/default profile and sends `ChangeSelectedProfile`. `Activate` subsequently loads settings again, reloads controls, optionally minimizes, and sends `StartService`. `ThumbnailManager` can be constructed through notification-handler resolution during these operations; do not assume every singleton is initialized only at `StartService`.

On close, the workspace first protects unapplied edits; `MinimizeToTray` and the presenter's `_exitApplication` then determine whether to minimize or exit. Explicit exit sets `_exitApplication` before closing the view. Ordinary Exit cancels the first close request, yields back to the UI pump, awaits `StopService`, saves configuration and closes once cleanup completes. `_shutdownInProgress` and `_shutdownCompleted` prevent repeated close requests from starting duplicate cleanup. [StartStopServiceHandler](../../Eve-O-Preview/Mediator/Handlers/Services/StartStopServiceHandler.cs) stops the manager timer, calls the CPU service's terminal reset, and awaits `HookService.StopAsync` for installation completion and native FPS/audio reset requests. Robin is not unloaded. See [Robin](robin.md) for bounded pipe behavior and what native cleanup responses establish.

[ExceptionHandler](../../Eve-O-Preview/ApplicationBase/ExceptionHandler.cs) installs CLR fatal logging before native platform initialization, then registers the Avalonia dispatcher handler after setup. UI exceptions show an owned Avalonia error dialog; CLR failures log without waiting for a UI pump. Both paths drain the bounded log sink before exit code 1. In a DEBUG build with a debugger attached, setup returns without installing handlers. [LoggerHelpers.WithCallerInfo](../../Eve-O-Preview/Helper/LoggerHelpers.cs) attaches compile-time caller metadata; preserve useful structured logging, but measure logging costs in high-frequency work. Normal startup wraps the rotating file logger in [AsyncLogSink](../../Eve-O-Preview/Helper/AsyncLogSink.cs), a bounded background writer. Input/render callers never wait for disk writes; overload reports dropped diagnostic events, and application exit drains accepted events within a 500 ms budget. Keep `-v` filtering and millisecond event timestamps when changing this route.

### Windows session ending

Windows shutdown/restart/sign-out is separate from Exit. The workspace accepts `WM_QUERYENDSESSION` immediately, before busy/draft/tray guards. No save, minimize or service stop happens until a true `WM_ENDSESSION`; cancelled queries preserve drafts, services and visibility.

Avalonia 11.3.20's hidden dispatch window needs the same protection. [Win32Platform.WndProc](https://github.com/AvaloniaUI/Avalonia/blob/11.3.20/src/Windows/Avalonia.Win32/Win32Platform.cs) raises `ShutdownRequested` during the query; [ClassicDesktopStyleApplicationLifetime](https://github.com/AvaloniaUI/Avalonia/blob/11.3.20/src/Avalonia.Controls/ApplicationLifetimes/ClassicDesktopStyleApplicationLifetime.cs) would close windows then. [WindowsSessionLifetime](../../Eve-O-Preview/ApplicationBase/WindowsSessionLifetime.cs) finds only the UI thread's `AvaloniaMessageWindow` and subclasses that HWND for the session message pair. Queries succeed and cancellation does nothing; confirmed end invokes the same idempotent workspace callback. The rooted delegate is removed on disposal. Recheck pinned upstream source when upgrading Avalonia.

`MainFormPresenter.EndWindowsSession` starts/reuses one stop and background save, waiting at most two seconds without pumping. `IThumbnailManager.StopAsync(true)` stops UI timers and the foreground observer synchronously, marks combat logging stopped through a nonblocking wake, and queues native hotkey unregister off-thread. The stop handler starts enumeration, affinity locks and pipe cleanup independently, so a stalled input thread cannot prevent the client reset attempt. Manager disposal reuses the pending stop. Overlapping Exit shares those tasks and cannot reclose after session completion. Applied-settings persistence and Robin's owner-exit reset remain fallbacks if Windows terminates unfinished work; the native protocol is unchanged. This callback budget does not describe the later bounded native-thread joins during container disposal.

`AsyncLogSink` gives draining and destination disposal 500 ms at exit. Its writer owns disposal even after the deadline, avoiding a concurrent write/dispose race. A blocked disk cannot hold exit indefinitely; pending diagnostics may be lost when the process ends.

[WindowsShutdownTests](../../tests/Eve-O-Preview.Tests/Checks/WindowsShutdownTests.cs) sends private-worker messages through the production workspace, hidden Avalonia window, presenter and stop handler with substituted clients/storage. Cases cover tray on/off, drafts, busy state, cancelled queries at both HWNDs, confirmed end via either HWND, repeated confirmation, stalled work and overlapping Exit. An actual `ThumbnailManager` case stalls native keyboard replacement and checks that the timer stops, client reset begins and manager disposal does not repeat the blocked wait. Original-form scenarios remain non-shipped comparison fixtures. Actual Windows shutdown/sign-out and live EVE teardown remain separate validation gates.

## UI and configuration contracts

Read [IMainFormView](../../Eve-O-Preview/View/Interface/IMainFormView.cs), [WorkspaceWindow](../../Eve-O-Preview/View/Implementation/WorkspaceWindow.cs), [WindowsWorkspaceBackend](../../Eve-O-Preview/View/Implementation/WindowsWorkspaceBackend.cs), and [MainFormPresenter](../../Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) together for the active path. The host view exposes properties and callbacks; the presenter connects them to settings and messages. The portable UI uses backend snapshots/commands, while Windows implementation types stay in the host.

`WindowsWorkspaceBackend.Read` constructs a fresh snapshot of settings, active profile, available profiles, ordered cycle groups and client visibility. `IsThumbnailIndividuallyDisabled` separates a client's saved visibility from the transient Hide All state; the existing `IsThumbnailDisabled` still combines both for native preview policy. Displaying the snapshot does not mutate configuration.

`ExecuteAsync` serializes commands with `IsBusy`, validates supported settings, mutates only explicit view/configuration bindings and awaits the established commit route. FPS enable, FPS targets and audio changes then call their dedicated mediator handlers. Commands also cover profile operations, group membership/order, shortcut capture/clear, global preview actions, documentation, exit and native font/color selection. Active highlight thickness has a visible 1–6 pixel editor bound to its existing profile property. Do not populate the entire profile from this smaller snapshot: title-keyed layouts, historical aliases and nested settings must retain their existing identities.

[WorkspaceView.AdvancedSettings](../../Eve-O-Preview.UI/WorkspaceView.AdvancedSettings.cs) exposes the previously JSON-only options. Modern **Previews & layout → Advanced** and Legacy **Thumbnail / General → Advanced preview settings** edit snapping, focus-loss hide delay, client check interval, compatibility capture, four resize bounds and login preview coordinates. `HideDelaySeconds` is a UI conversion of the existing integer `HideThumbnailsDelay`: the backend rounds up to whole checks using the current refresh period. Changing that period changes the effective delay; the UI shows the resulting seconds. Historical JSON names, units and storage locations stay unchanged.

The `preview-size-limits` command supplies all four dimensions in `WorkspaceCommand.Settings`, validates minimum <= maximum and existing application limits, and clamps the current size in the same save. [WindowsWorkspaceBackend.AdvancedSettings](../../Eve-O-Preview/View/Implementation/WindowsWorkspaceBackend.AdvancedSettings.cs) updates backend/view bounds together. These advanced commands save through `SaveConfiguration`, then publish [ThumbnailRuntimeSettingsUpdated](../../Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailRuntimeSettingsUpdated.cs); its handler invokes `IThumbnailManager.ApplyRuntimeSettings`. This refreshes timers, hide delay, size bounds, login position and visuals without replacing views unless capture mode changed. Saving failures restore the changed configuration; the UI retains unapplied edits.

**Clients → Character colors & minimization** (Legacy **Active Clients → Colors / priority**) opens the shared online/offline character editor. `WorkspaceSnapshot.ClientPreferences` combines discovered and saved full titles with existing color/priority entries. `client-preferences` validates the exact existing title or prefixes a new offline name, then updates `PerClientActiveClientHighlightColor` and the private `PriorityClients` list in place. Empty color means inherit the profile color; false priority removes the minimization exception. The shared color picker and palette stage edits until **Save character settings**. These are profile settings, independent of the session-only cycling skip and global theme.

The workspace's ordinary toggles and zoom-anchor buttons commit directly; typed/choice fields use Apply or Enter, with a separate discard action. Search (`Ctrl+K`) edits the same catalog entries in place. Draft values are view state rather than profile JSON, and background refresh preserves their text/focus where possible. Switching profiles and closing/exiting prompt before discarding unapplied edits; minimizing to the tray preserves them. Profile selection and the active profile's accent cue remain visible throughout the shell. Failed setting updates retain retry state and expose Retry update, so an already-mutated in-memory value does not prevent retrying persistence.

The dedicated modern preview editor stages its title, highlight, size and hover-zoom controls together. Its persistent Apply changes/Reset edits controls remain visible beside the sample (above the editor at compact widths). The sample updates drafts without applying them to clients. Numeric controls retain fractional font/outline values with invariant parsing. Ordinary window-behavior controls retain immediate application. Legacy preserves the original Enter/leave-field save workflow and opens owned Avalonia font/color dialogs. `CommandResult.WasCancelled` distinguishes an accepted picker from cancellation so accepting a new value clears superseded drafts while Cancel preserves them. Search results provide a direct route to the specialized title editor.

Modern hotkey recording has an explicit Record action, ten-second capture window and separate Clear action. Escape cancels recording and retains the shortcut. The Windows adapter still uses the existing message-pumping capture handler, yielding first so the listening status can paint; it is not a portable global-hotkey implementation. Group edits preserve full-title members and additional persisted hotkeys beyond the visible slots. Explicit Apply must allow retry after a failed persistence or native-update attempt, even if the value already changed in memory.

A returned save/command result does not prove real native effectiveness: existing hook methods can log failures or return unsuccessful pipe outcomes without failing the entire mediator command. Keep user-facing feedback and validation reports precise about saved settings, requested updates and observed game behavior.

The subsections below retain details of [MainForm](../../tests/Eve-O-Preview.Tests/LegacyReference/MainForm.cs) and [its designer](../../tests/Eve-O-Preview.Tests/LegacyReference/MainForm.Designer.cs), which remain parity references and regression-test targets. Their event mechanics describe the old form, not Avalonia data binding.

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

The original FPS/Audio test fixture contains `fpsBottomPanel` with `AutoScroll=true` and anchored group boxes. [CustomAudioTests](../../tests/Eve-O-Preview.Tests/Checks/CustomAudioTests.cs) preserves that reference's validation assertions alongside persistence and host pipe checks. The production Avalonia settings integration and workspace smoke checks cover the current editors; none of these establish native sound interception.

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
| `RefreshHotkeys` | Cleans null strings, rebuilds parsed key collections and publishes `HotkeysChanged`; the manager replaces its complete input-service snapshot |
| `SetFpsLimiter` | Handler sends target updates through `HookService` to known clients |
| `SetFpsLimiterEnabled` | Handler installs hooks if enabled, sends zero FPS targets if disabled |
| `SetAudioSettings` | Handler installs/updates hooks, then sends mute settings to known clients |
| `UpdateCpuAffinity` | Handler resolves active/next/previous HWNDs from the cache, calls CPU service |
| `ResetAllCpuAffinity` | Handler resets the cache's known processes |
| `ThumbnailListUpdated` | Handler adds/removes presenter descriptions and main-form list entries |
| `ThumbnailConfiguredSizeUpdated` | Handler -> manager `UpdateThumbnailsSize` |
| `ThumbnailActiveSizeUpdated` | Handler -> presenter `UpdateThumbnailSize`: synchronize view and configuration size under feedback suppression; normal saves persist it without committing unrelated workspace edits |
| `ThumbnailFrameSettingsUpdated` | Handler -> manager `UpdateThumbnailFrames` |
| `ThumbnailFontTitleSettingsUpdated` | `ThumbnailTitleFontSettingsUpdatedHandler` -> manager `UpdateThumbnailTitleFont` |
| `ThumbnailLocationUpdated` | Handler saves title/active-client-relative location, then sends `SaveConfiguration` |
| `ThumbnailToggleHideAll` | Handler toggles transient configuration state and publishes changed notification |
| `ThumbnailToggleHideAllChangedNotification` | Handler -> presenter -> button/tab status; manager observes hide state on refresh |
| `MinimizeClient`, `MinimizeAllClients` | Handlers call `WindowManager.MinimizeWindow(..., true)`; source filenames use `Minimise` |

[GlobalEvents](../../Eve-O-Preview/Services/Implementation/GlobalEvents.cs) is a synchronous bridge for two profile events. Presenter listeners reload controls/refresh lists; the manager's current-profile listener replaces the complete hotkey-service binding snapshot. Do not assume this bridge reapplies every feature when a profile changes.

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

[ShortcutKeys](../../Eve-O-Preview/Input/ShortcutKeys.cs) retains virtual-key/modifier values without toolkit enums. [ShortcutText](../../Eve-O-Preview/Input/ShortcutText.cs) parses existing invariant strings/aliases; parsed properties remain `JsonIgnore`, so profile shortcut fields do not migrate. `WindowsHotkeyService` runs a native message-only window/pump on its dedicated thread for both hook and `RegisterHotKey` modes. Actions post at Avalonia `Send`; diagnostic passthrough yields at `Background`. Keyboard callbacks do not synchronously call focus, UI or IPC.

[IGlobalPointerInput](../../Eve-O-Preview/Input/IGlobalPointerInput.cs) exposes desktop-pixel move/up events, position, buttons and modifiers. Its Windows adapter installs only a mouse hook while gesture subscriptions exist and delivers asynchronously on Avalonia's UI dispatcher. A preallocated queue coalesces movement into one pending dispatcher post, retains up to eight releases and delivers each final move before its release. Generation changes invalidate queued events after cancellation; a drain yields after at most nine packets. The hook copies structs under a short lock without per-move allocation, UI work or synchronous waits. Subscription boundaries wait up to five seconds for the hook thread, ensuring installation before the gesture proceeds; disposal joins that thread with a bound. Focused-window keys do not replace global input.

[CaptureNewHotkeyHandler](../../Eve-O-Preview/Mediator/Handlers/Configuration/CaptureNewHotkeyHandler.cs) awaits `IHotkeyService.CaptureAsync`; capture suspends every binding, completes on release of the first non-modifier key with its original down modifiers, and restores the latest profile in `finally`. The workspace requests a 10-second timeout asynchronously. Escape, timeout and cancellation preserve the old binding.

Escape maps to `ShortcutKeys.None`; the workspace leaves the existing binding unchanged. Duplicate detection includes general bindings and every group, while permitting the currently edited shortcut. [RefreshHotkeysHandler](../../Eve-O-Preview/Mediator/Handlers/Configuration/RefreshHotkeysHandler.cs) reparses profile strings and publishes `HotkeysChanged`; `ThumbnailManager` then replaces the full service snapshot using the current profile's `IThumbnailConfiguration.UseWindowsHotkeys` and `GlobalHotkeysOnRelease`. Both settings are persisted in the profile and apply immediately on a profile switch. The default global hook and optional Windows registrations share the same profile, capture and shutdown lifecycle. Read [hotkey implementation and validation](windows-and-thumbnails.md#hotkeys-and-cycle-semantics).

Modern Hotkey method choices are neutrally labeled Global input and Windows hotkeys. `GlobalHotkeyTrigger` maps Key down/Key up to the profile's `GlobalHotkeysOnRelease` field; only Global input exposes that control. Both controls await the existing settings commit, and profile load/save/clone includes their values. `ConfigurationStorage` seeds omitted fields from the earlier global settings through `ApplicationPreferences.ApplyLegacyHotkeyDefaults`; explicit profile values take precedence, and subsequent profile saves include both fields. Without earlier settings, defaults are Global input and Key down. Hidden `DiagnosticHotkeyPassthrough` remains session-only and does not write the global settings file or gameplay profiles. Ten rapid dropdown open/close cycles reveal it; ordinary search keeps it hidden until then. Selecting or switching to a profile using Windows hotkeys clears passthrough, while retaining that profile's saved trigger choice. Backend validation rejects enabling passthrough or changing trigger timing while Windows hotkeys is selected.

Client selection is the existing workspace editor; full `EVE - ...` strings retain identity independently of labels. The original [ClientNameInputBox](../../tests/Eve-O-Preview.Tests/LegacyReference/ClientNameInputBox.cs) and designer are test fixtures only.

Production title/sample rasterization uses `OverlaySceneRasterizer`. [OutlinedLabel](../../tests/Eve-O-Preview.Tests/LegacyReference/OutlinedLabel.cs) and the former Forms menu renderers exist only as non-shipped comparison fixtures. Production context menus use Avalonia and `NativeMenuTheme`. Historical `PreviewToy.AboutBox` files remain excluded from the shipping project.

## Remaining boundaries

The settings workflow now protects font/size event suppression, fractional and incomplete numeric input, final-group deletion, cancelled/empty client selections, retained Move Up selection and hotkey capture timeout. RefreshHotkeys reparses and publishes HotkeysChanged so registrations follow group replacement as well as edits. FontSettings itself supplies defaults for partial nested profiles.

View callbacks still include async void and some fire-and-forget MediatR dispatch. UI methods must run on their owning thread; storage serialization is not a general transaction over controls and native clients. SaveApplicationSettings copies all controls before its first await. Ordinary Exit cancels the first Closing request, yields back to the UI pump, awaits native cleanup, saves and closes once. Repeated close requests do not start duplicate cleanup. This avoids blocking MediatR continuations on the UI thread. Close-to-tray remains profile-scoped. New configurations and profiles omitting `MinimizeToTray` default to true; an explicitly saved false remains false. The existing setting also controls starting in the tray; that behavior is unchanged.

See [current defect status and evidence](reported-bugs.md) rather than treating old suspected defects as behavior to preserve.

## Change checklist and validation

For a new setting, trace model/interface -> JSON/defaults/restrictions -> view property/control event -> presenter reload/save -> request/notification -> runtime consumer -> save/load/profile switch. For a Robin setting include host/server framing and native bounds. Extend relevant regression coverage for a substantive behavior change; see the [coverage matrix and commands](build-and-test.md).

Use isolated profile fixtures for missing/old/current fields, repeat-load idempotence, malformed nested values and renamed/cloned locations. Use the existing private-desktop runner for UI/window tests instead of launching production startup. Verify real game behavior separately when a setting reaches Robin. A passing parser/UI test does not establish full client reconfiguration or native correctness.

### Module section search

`WorkspaceModule.SearchTargets` supplies section labels, common aliases and a
callback that sets retained module navigation before `WorkspaceView.Navigate`.
Search matches each query word against English aliases and localized labels.
Augments routes DPS/reps/alpha/simulation to Thumbnail augments, log/logs/logging
and SDE/data terms to Data setup, and counters/jumps/reset to Overview. Matching
sections replace the duplicate generic module result and count in the search
summary. Hidden modern modules stay hidden in Legacy search. New or renamed
features must update these routes or `SettingCatalog` alongside their controls.
