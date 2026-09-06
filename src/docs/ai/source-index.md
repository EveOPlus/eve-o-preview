# Complete source index and review scope

Baseline: `60944b521e3c5b442dd379b5208c53a91ae3a573`, reviewed 2026-09-06. This inventory contains all **208 tracked baseline files**: 191 under `src/`, 17 elsewhere. Paths in the tables are relative to the Git root. Newly added AI guides/instruction files are listed separately at the end, not included in the baseline count.

The application, Robin, tests, Mock, configuration, message/handler classes, native bindings, project files, release code, designer wiring and textual resources were reviewed across the subsystem guides. Repeated generated wrappers and resource schemas were inspected for their role, control wiring and payload boundaries. This inventory is a navigation and coverage record, not an assertion that every defect has been found.

Binary/resource treatment: icon and partner image metadata were inspected, not their pixels; MainForm's embedded icons were decoded for metadata; the release CSS font is a binary payload. The public certificate was inspected without importing it and contains no private key. Both GPL v3 files were identified and verified byte-identical; this was not a license/legal audit. `assets/stuff.zip` contains `stuff.7z`; the inner archive was not unpacked or audited and no build reference to it was found in the reviewed release code. None of these payloads was executed.

Excluded from source review: ignored package/download caches, `bin`/`obj`/`publish`, `.vs`, generated local state, user settings, logs, profiles and temporary output. There are no tracked `tools` sources or CI workflow files at this baseline. The local `.vscode` directory was empty. The IDE log name in the task did not supply log contents. No application/test/native hook/release task was run for this documentation-only change; see [validation scope](build-and-test.md).

Use [the entry guide](../../README.md) to route by feature; use this page when a file is unfamiliar. Namespaces and filenames are not always identical, and some tracked files are intentionally excluded from compilation.

## Repository root (3)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [.gitignore](../../../.gitignore) | Ignored build/cache/profile/log artifacts. | [build-and-test](build-and-test.md) |
| [LICENSE](../../../LICENSE) | GPL v3 text; byte-identical to the packaged application license. | [build-and-test](build-and-test.md) |
| [README.md](../../../README.md) | User-facing feature/install/release documentation; contains older runtime claims. | [build-and-test](build-and-test.md) |

## Assets (2)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [assets/PartnerBadge.png](../../../assets/PartnerBadge.png) | Binary partner artwork; metadata only. | [build-and-test](build-and-test.md) |
| [assets/stuff.zip](../../../assets/stuff.zip) | Opaque archive; outer inventory contains stuff.7z. Inner content not audited. | [build-and-test](build-and-test.md) |

## Release tooling (12)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [build/Build.csproj](../../../build/Build.csproj) | Project target, dependencies, configuration, artifacts and build inclusion. | [build-and-test](build-and-test.md) |
| [build/Configuration.cs](../../../build/Configuration.cs) | Release paths, configuration, projects and machine-specific signing path. | [build-and-test](build-and-test.md) |
| [build/Context.cs](../../../build/Context.cs) | Cake build context and root working directory. | [build-and-test](build-and-test.md) |
| [build/Lifetime.cs](../../../build/Lifetime.cs) | Every-task output-directory cleanup and conditional NuGet download. | [build-and-test](build-and-test.md) |
| [build/Program.cs](../../../build/Program.cs) | Cake host and release lifetime/task registration. | [build-and-test](build-and-test.md) |
| [build/Tasks/Build.cs](../../../build/Tasks/Build.cs) | Main app and Robin publish into shared release bin. | [build-and-test](build-and-test.md) |
| [build/Tasks/Default.cs](../../../build/Tasks/Default.cs) | Release dependency chain and final console wait. | [build-and-test](build-and-test.md) |
| [build/Tasks/Documentation.cs](../../../build/Tasks/Documentation.cs) | Root user README to PDF using theme. | [build-and-test](build-and-test.md) |
| [build/Tasks/Sign.cs](../../../build/Tasks/Sign.cs) | Optional certificate/password signing and timestamping. | [build-and-test](build-and-test.md) |
| [build/Tasks/Zip.cs](../../../build/Tasks/Zip.cs) | Release zip entries and separate certificate copy. | [build-and-test](build-and-test.md) |
| [build/Themes/Github/Theme.css](../../../build/Themes/Github/Theme.css) | PDF styling; contains embedded binary WOFF font data. | [build-and-test](build-and-test.md) |
| [build/Themes/Github/Theme.html](../../../build/Themes/Github/Theme.html) | PDF HTML template. | [build-and-test](build-and-test.md) |

## Source solution (1)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/EVE-O-Preview.sln](../../../src/EVE-O-Preview.sln) | Full solution including app, Robin, Mock, tests, and release tooling. | [build-and-test](build-and-test.md) |

## Manual Mock application (12)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Mock/App.config](../../../src/Eve-O-Mock/App.config) | Framework 4.8 runtime configuration matching this legacy project. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/App.xaml](../../../src/Eve-O-Mock/App.xaml) | WPF startup resource/application declaration. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/App.xaml.cs](../../../src/Eve-O-Mock/App.xaml.cs) | WPF application code-behind. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/Eve-O-Mock.csproj](../../../src/Eve-O-Mock/Eve-O-Mock.csproj) | Project target, dependencies, configuration, artifacts and build inclusion. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/MainWindow.xaml](../../../src/Eve-O-Mock/MainWindow.xaml) | Swap-chain HelixToolkit viewport and camera. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/MainWindow.xaml.cs](../../../src/Eve-O-Mock/MainWindow.xaml.cs) | Rotating randomized cube/window for manual preview observation. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/Properties/AssemblyInfo.cs](../../../src/Eve-O-Mock/Properties/AssemblyInfo.cs) | Legacy assembly identity/version metadata. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/Properties/Resources.Designer.cs](../../../src/Eve-O-Mock/Properties/Resources.Designer.cs) | Generated resource/settings accessors and defaults. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/Properties/Resources.resx](../../../src/Eve-O-Mock/Properties/Resources.resx) | Resource schema/designer metadata; binary payloads are not C# logic. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/Properties/Settings.Designer.cs](../../../src/Eve-O-Mock/Properties/Settings.Designer.cs) | Generated resource/settings accessors and defaults. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/Properties/Settings.settings](../../../src/Eve-O-Mock/Properties/Settings.settings) | Designer settings schema/defaults. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Mock/packages.config](../../../src/Eve-O-Mock/packages.config) | Legacy exact package versions; restore into src/packages. | [build-and-test](build-and-test.md) |

## Robin injected library (10)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview.Robin/AudioMuteSystem.cs](../../../src/Eve-O-Preview.Robin/AudioMuteSystem.cs) | PAGE_GUARD/return-breakpoint audio stop, fixed sorted IDs, debug history ring. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/DXHook.cs](../../../src/Eve-O-Preview.Robin/DXHook.cs) | DxHook class: native Initialize, DXGI vtable patch, focus and pacing. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/DebugLogger.cs](../../../src/Eve-O-Preview.Robin/DebugLogger.cs) | OutputDebugString hook diagnostics with cached HWND. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj](../../../src/Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj) | Project target, dependencies, configuration, artifacts and build inclusion. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/FocusType.cs](../../../src/Eve-O-Preview.Robin/FocusType.cs) | Foreground/background/predicted native state. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/Global.cs](../../../src/Eve-O-Preview.Robin/Global.cs) | Target HWND/PID identity shared by native subsystems. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/NamedPipeServer.cs](../../../src/Eve-O-Preview.Robin/NamedPipeServer.cs) | Byte commands, FPS/owner/focus/audio handlers, history queries and recovery. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/NativeMethods.cs](../../../src/Eve-O-Preview.Robin/NativeMethods.cs) | LibraryImport declarations and x64 mutable native context layouts. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/PrecisionSleep.cs](../../../src/Eve-O-Preview.Robin/PrecisionSleep.cs) | High-resolution waitable timer and relative 100 ns due times. | [robin](robin.md) |
| [src/Eve-O-Preview.Robin/WinEventHook.cs](../../../src/Eve-O-Preview.Robin/WinEventHook.cs) | Background foreground-event thread and Windows message pump. | [robin](robin.md) |

## Main application entry and packaging (14)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/AboutBox.Designer.cs](../../../src/Eve-O-Preview/AboutBox.Designer.cs) | Excluded legacy AboutBox controls and stale resource reference. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/AboutBox.cs](../../../src/Eve-O-Preview/AboutBox.cs) | Legacy PreviewToy AboutBox; excluded from compilation. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/AboutBox.resx](../../../src/Eve-O-Preview/AboutBox.resx) | Excluded legacy AboutBox resource schema. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Eve-O-Preview.csproj](../../../src/Eve-O-Preview/Eve-O-Preview.csproj) | Project target, dependencies, configuration, artifacts and build inclusion. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/Eve-O-Preview.sln](../../../src/Eve-O-Preview/Eve-O-Preview.sln) | Application-only nested solution; excludes tests. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/EveoPreviewRootCA.crt](../../../src/Eve-O-Preview/EveoPreviewRootCA.crt) | Public X.509 certificate copied by project/packager; no private key present. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/FodyWeavers.xml](../../../src/Eve-O-Preview/FodyWeavers.xml) | Costura weaving configuration. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/FodyWeavers.xsd](../../../src/Eve-O-Preview/FodyWeavers.xsd) | Generated weaver schema. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/LICENSE.txt](../../../src/Eve-O-Preview/LICENSE.txt) | Packaged GPL v3 text; identical to root LICENSE. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/Launch Eve-O Preview with Verbose Logging.cmd](<../../../src/Eve-O-Preview/Launch Eve-O Preview with Verbose Logging.cmd>) | Starts app with -v; does not change working directory. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/Program.cs](../../../src/Eve-O-Preview/Program.cs) | STA startup, single-instance workaround, logger, Autofac composition, sidecar dispatch. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/app.config](../../../src/Eve-O-Preview/app.config) | Legacy .NET Framework startup declaration; project target is authoritative. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/app.manifest](../../../src/Eve-O-Preview/app.manifest) | asInvoker, uiAccess=false, DPI declarations and historical OS comments. | [build-and-test](build-and-test.md) |
| [src/Eve-O-Preview/icon.ico](../../../src/Eve-O-Preview/icon.ico) | Binary application icon; metadata only. | [build-and-test](build-and-test.md) |

## Main application: ApplicationBase (9)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/ApplicationBase/ApplicationController.cs](../../../src/Eve-O-Preview/ApplicationBase/ApplicationController.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/ExceptionHandler.cs](../../../src/Eve-O-Preview/ApplicationBase/ExceptionHandler.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/IApplicationController.cs](../../../src/Eve-O-Preview/ApplicationBase/IApplicationController.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/IIocContainer.cs](../../../src/Eve-O-Preview/ApplicationBase/IIocContainer.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/IPresenter.cs](../../../src/Eve-O-Preview/ApplicationBase/IPresenter.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/IPresenterGeneric.cs](../../../src/Eve-O-Preview/ApplicationBase/IPresenterGeneric.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/IView.cs](../../../src/Eve-O-Preview/ApplicationBase/IView.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/Presenter.cs](../../../src/Eve-O-Preview/ApplicationBase/Presenter.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/ApplicationBase/PresenterGeneric.cs](../../../src/Eve-O-Preview/ApplicationBase/PresenterGeneric.cs) | Presenter/controller/view abstraction or application infrastructure. | [application-and-configuration](application-and-configuration.md) |

## Main application: Configuration (15)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/Configuration/Implementation/AppConfig.cs](../../../src/Eve-O-Preview/Configuration/Implementation/AppConfig.cs) | Legacy ConfigFileName holder; not active profile resolution. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Implementation/AudioMuteSettings.cs](../../../src/Eve-O-Preview/Configuration/Implementation/AudioMuteSettings.cs) | Preset/custom ID model and unsigned-decimal parser. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs](../../../src/Eve-O-Preview/Configuration/Implementation/ConfigurationStorage.cs) | Populate/save singleton JSON, migrations, restrictions, hotkey refresh. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Implementation/CycleGroup.cs](../../../src/Eve-O-Preview/Configuration/Implementation/CycleGroup.cs) | Persisted title order and hotkeys; runtime parsed-key lists. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Implementation/FontSettings.cs](../../../src/Eve-O-Preview/Configuration/Implementation/FontSettings.cs) | Font, outline, color and offset data. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Implementation/FpsLimiterSettings.cs](../../../src/Eve-O-Preview/Configuration/Implementation/FpsLimiterSettings.cs) | Desktop enabled flag and three FPS targets. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Implementation/ProfileManager.cs](../../../src/Eve-O-Preview/Configuration/Implementation/ProfileManager.cs) | Profile root resolution, legacy relocation, list/clone/rename/delete. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs](../../../src/Eve-O-Preview/Configuration/Implementation/ThumbnailConfiguration.cs) | Shared settings, JSON names, layout maps, defaults, clamps. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Interface/ClientLayout.cs](../../../src/Eve-O-Preview/Configuration/Interface/ClientLayout.cs) | Persisted game-window geometry/maximized data. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Interface/IAppConfig.cs](../../../src/Eve-O-Preview/Configuration/Interface/IAppConfig.cs) | Configuration/storage/profile contract implemented by shared services. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Interface/IConfigurationStorage.cs](../../../src/Eve-O-Preview/Configuration/Interface/IConfigurationStorage.cs) | Configuration/storage/profile contract implemented by shared services. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Interface/IProfileManager.cs](../../../src/Eve-O-Preview/Configuration/Interface/IProfileManager.cs) | Configuration/storage/profile contract implemented by shared services. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Interface/IThumbnailConfiguration.cs](../../../src/Eve-O-Preview/Configuration/Interface/IThumbnailConfiguration.cs) | Configuration/storage/profile contract implemented by shared services. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Interface/ZoomAnchor.cs](../../../src/Eve-O-Preview/Configuration/Interface/ZoomAnchor.cs) | Persisted enum order paired with ViewZoomAnchor. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Configuration/Model/ProfileLocation.cs](../../../src/Eve-O-Preview/Configuration/Model/ProfileLocation.cs) | Friendly name, directory and full JSON path. | [application-and-configuration](application-and-configuration.md) |

## Main application: Excpetions (1)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/Excpetions/HotkeyAlreadyExistsException.cs](../../../src/Eve-O-Preview/Excpetions/HotkeyAlreadyExistsException.cs) | Duplicate-key diagnostic carrying both locations; existing folder spelling. | [application-and-configuration](application-and-configuration.md) |

## Main application: Helper (3)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/Helper/HotkeyHelpers.cs](../../../src/Eve-O-Preview/Helper/HotkeyHelpers.cs) | KeysConverter parsing; invalid input becomes Keys.None. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Helper/LoggerHelpers.cs](../../../src/Eve-O-Preview/Helper/LoggerHelpers.cs) | Structured compile-time caller metadata. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Helper/ProcessHelpers.cs](../../../src/Eve-O-Preview/Helper/ProcessHelpers.cs) | Open/close raw kernel handles and construct ProcessInfo. | [windows-and-thumbnails](windows-and-thumbnails.md) |

## Main application: Mediator (55)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/CaptureNewHotkeyHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/CaptureNewHotkeyHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/ChangeSelectedProfileHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/ChangeSelectedProfileHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/CloneCurrentProfileHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/CloneCurrentProfileHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/DeleteCurrentProfileHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/DeleteCurrentProfileHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/GetCurrentProfileLocationHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/GetCurrentProfileLocationHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/ProfileListChangedNotificationHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/ProfileListChangedNotificationHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/RefreshHotkeysHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/RefreshHotkeysHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/RenameCurrentProfileHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/RenameCurrentProfileHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/SaveConfigurationHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/SaveConfigurationHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/SelectedProfileChangedNotificationHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/SelectedProfileChangedNotificationHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/SetAudioSettingsHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/SetAudioSettingsHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/SetFpsLimiterEnabledHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/SetFpsLimiterEnabledHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/SetFpsLimiterHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/SetFpsLimiterHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Configuration/ThumbnailToggleHideAllChangedNotificationHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Configuration/ThumbnailToggleHideAllChangedNotificationHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Process/ResetAllCpuAffinityHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Process/ResetAllCpuAffinityHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Process/UpdateCpuAffinityHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Process/UpdateCpuAffinityHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Services/StartStopServiceHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Services/StartStopServiceHandler.cs) | Message handler; see routing map and its downstream service. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/MinimiseAllClientsHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/MinimiseAllClientsHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/MinimiseClientHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/MinimiseClientHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailActiveSizeUpdatedHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailActiveSizeUpdatedHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailConfiguredSizeUpdatedHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailConfiguredSizeUpdatedHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailFrameSettingsUpdatedHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailFrameSettingsUpdatedHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailListUpdatedHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailListUpdatedHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailLocationUpdatedHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailLocationUpdatedHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailTitleFontSettingsUpdatedHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailTitleFontSettingsUpdatedHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailToggleHideAllHandler.cs](../../../src/Eve-O-Preview/Mediator/Handlers/Thumbnails/ThumbnailToggleHideAllHandler.cs) | Message handler; see routing map and its downstream service. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Base/NotificationBase.cs](../../../src/Eve-O-Preview/Mediator/Messages/Base/NotificationBase.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/CaptureNewHotkey.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/CaptureNewHotkey.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/CaptureNewHotkeyResponse.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/CaptureNewHotkeyResponse.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/ChangeSelectedProfile.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/ChangeSelectedProfile.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/CloneCurrentProfile.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/CloneCurrentProfile.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/DeleteCurrentProfile.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/DeleteCurrentProfile.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/GetCurrentProfileLocation.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/GetCurrentProfileLocation.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/ProfileListChangedNotification.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/ProfileListChangedNotification.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/RefreshHotkeys.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/RefreshHotkeys.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/RenameCurrentProfile.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/RenameCurrentProfile.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/SaveConfiguration.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/SaveConfiguration.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/SelectedProfileChangedNotification.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/SelectedProfileChangedNotification.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/SetAudioSettings.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/SetAudioSettings.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/SetFpsLimiter.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/SetFpsLimiter.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Configuration/SetFpsLimiterEnabled.cs](../../../src/Eve-O-Preview/Mediator/Messages/Configuration/SetFpsLimiterEnabled.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Process/ResetAllCpuAffinity.cs](../../../src/Eve-O-Preview/Mediator/Messages/Process/ResetAllCpuAffinity.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Process/UpdateCpuAffinity.cs](../../../src/Eve-O-Preview/Mediator/Messages/Process/UpdateCpuAffinity.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Services/StartService.cs](../../../src/Eve-O-Preview/Mediator/Messages/Services/StartService.cs) | Request/notification payload; follow its type through the routing map. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Mediator/Messages/Services/StopService.cs](../../../src/Eve-O-Preview/Mediator/Messages/Services/StopService.cs) | Request/notification payload; follow its type through the routing map. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/MinimiseAllClients.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/MinimiseAllClients.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/MinimiseClient.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/MinimiseClient.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailActiveSizeUpdated.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailActiveSizeUpdated.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailConfiguredSizeUpdated.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailConfiguredSizeUpdated.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailFontTitleSettingsUpdated.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailFontTitleSettingsUpdated.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailFrameSettingsUpdated.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailFrameSettingsUpdated.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailListUpdated.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailListUpdated.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailLocationUpdated.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailLocationUpdated.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailToggleHideAll.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailToggleHideAll.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailToggleHideAllChangedNotification.cs](../../../src/Eve-O-Preview/Mediator/Messages/Thumbnails/ThumbnailToggleHideAllChangedNotification.cs) | Request/notification payload; follow its type through the routing map. | [application-and-configuration](application-and-configuration.md) |

## Main application: Presenters (4)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs](../../../src/Eve-O-Preview/Presenters/Implementation/MainFormPresenter.cs) | UI callbacks, reload/save, profile bridge, thumbnail descriptions, shutdown. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Presenters/Implementation/ViewZoomAnchorConverter.cs](../../../src/Eve-O-Preview/Presenters/Implementation/ViewZoomAnchorConverter.cs) | Numeric enum-cast shortcut requiring aligned enum orders. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Presenters/Interface/IMainFormPresenter.cs](../../../src/Eve-O-Preview/Presenters/Interface/IMainFormPresenter.cs) | Thumbnail list/size presenter contract. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Presenters/Interface/ViewCloseRequest.cs](../../../src/Eve-O-Preview/Presenters/Interface/ViewCloseRequest.cs) | Mutable allow/cancel close decision. | [application-and-configuration](application-and-configuration.md) |

## Main application: Properties (2)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/Properties/Resources.Designer.cs](../../../src/Eve-O-Preview/Properties/Resources.Designer.cs) | Generated resource/settings accessors and defaults. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/Properties/Resources.resx](../../../src/Eve-O-Preview/Properties/Resources.resx) | Resource schema/designer metadata; binary payloads are not C# logic. | [application-and-configuration](application-and-configuration.md) |

## Main application: Services (28)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/Services/Implementation/CpuAffinityService.cs](../../../src/Eve-O-Preview/Services/Implementation/CpuAffinityService.cs) | Topology detection, precomputed role masks, background cache, reset. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Implementation/DebuggerSidecar.cs](../../../src/Eve-O-Preview/Services/Implementation/DebuggerSidecar.cs) | Hidden host debugger, raw DEBUG_EVENT decoding and debug output. | [robin](robin.md) |
| [src/Eve-O-Preview/Services/Implementation/DwmThumbnail.cs](../../../src/Eve-O-Preview/Services/Implementation/DwmThumbnail.cs) | DWM registration, destination properties, update/recovery signal. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Implementation/GlobalEvents.cs](../../../src/Eve-O-Preview/Services/Implementation/GlobalEvents.cs) | Synchronous profile-event bridge. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Implementation/HookService.cs](../../../src/Eve-O-Preview/Services/Implementation/HookService.cs) | Host injection, ownership/FPS/focus/audio pipe sender and cached install state. | [robin](robin.md) |
| [src/Eve-O-Preview/Services/Implementation/ProcessInfo.cs](../../../src/Eve-O-Preview/Services/Implementation/ProcessInfo.cs) | PID, source HWND, title, owned kernel handle record. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Implementation/ProcessMonitor.cs](../../../src/Eve-O-Preview/Services/Implementation/ProcessMonitor.cs) | ExeFile discovery and HWND-keyed added/renamed/removed cache. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs](../../../src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs) | Preview policy/lifecycle, MRU, hotkeys, cycling, hover/layout, delayed saves. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Implementation/WindowManager.cs](../../../src/Eve-O-Preview/Services/Implementation/WindowManager.cs) | Win32 activation/layout/minimize, DWM creation, GDI capture. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/ICpuAffinityService.cs](../../../src/Eve-O-Preview/Services/Interface/ICpuAffinityService.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/IDwmThumbnail.cs](../../../src/Eve-O-Preview/Services/Interface/IDwmThumbnail.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/IGlobalEvents.cs](../../../src/Eve-O-Preview/Services/Interface/IGlobalEvents.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/IHookService.cs](../../../src/Eve-O-Preview/Services/Interface/IHookService.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/IProcessInfo.cs](../../../src/Eve-O-Preview/Services/Interface/IProcessInfo.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/IProcessMonitor.cs](../../../src/Eve-O-Preview/Services/Interface/IProcessMonitor.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/IThumbnailManager.cs](../../../src/Eve-O-Preview/Services/Interface/IThumbnailManager.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/IWindowManager.cs](../../../src/Eve-O-Preview/Services/Interface/IWindowManager.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interface/InteropConstants.cs](../../../src/Eve-O-Preview/Services/Interface/InteropConstants.cs) | Host service/native-constant contract; match implementation and registration. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/DWM_BLURBEHIND.cs](../../../src/Eve-O-Preview/Services/Interop/DWM_BLURBEHIND.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/DWM_THUMBNAIL_PROPERTIES.cs](../../../src/Eve-O-Preview/Services/Interop/DWM_THUMBNAIL_PROPERTIES.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/DWM_TNP_CONSTANTS.cs](../../../src/Eve-O-Preview/Services/Interop/DWM_TNP_CONSTANTS.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/DwmNativeMethods.cs](../../../src/Eve-O-Preview/Services/Interop/DwmNativeMethods.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/Gdi32NativeMethods.cs](../../../src/Eve-O-Preview/Services/Interop/Gdi32NativeMethods.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/KernelNativeMethods.cs](../../../src/Eve-O-Preview/Services/Interop/KernelNativeMethods.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/MARGINS.cs](../../../src/Eve-O-Preview/Services/Interop/MARGINS.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/RECT.cs](../../../src/Eve-O-Preview/Services/Interop/RECT.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/User32NativeMethods.cs](../../../src/Eve-O-Preview/Services/Interop/User32NativeMethods.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/Services/Interop/WINDOWPLACEMENT.cs](../../../src/Eve-O-Preview/Services/Interop/WINDOWPLACEMENT.cs) | Host native import, flags or structure layout; preserve ABI/caller contract. | [windows-and-thumbnails](windows-and-thumbnails.md) |

## Main application: View (27)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/Eve-O-Preview/View/CustomControl/DarkGoldRenderer.cs](../../../src/Eve-O-Preview/View/CustomControl/DarkGoldRenderer.cs) | Standalone renderer/color table; distinguish nested namesakes. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/CustomControl/DarkModeContextMenuStrip.cs](../../../src/Eve-O-Preview/View/CustomControl/DarkModeContextMenuStrip.cs) | Dark menu plus nested renderer/color table. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/CustomControl/OutlinedLabel.cs](../../../src/Eve-O-Preview/View/CustomControl/OutlinedLabel.cs) | Transparent outlined text with intentional smoothing thresholds. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Implementation/ClientNameInputBox.Designer.cs](../../../src/Eve-O-Preview/View/Implementation/ClientNameInputBox.Designer.cs) | Controls plus interface inheritance and selection properties. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Implementation/ClientNameInputBox.cs](../../../src/Eve-O-Preview/View/Implementation/ClientNameInputBox.cs) | Known/manual client selection dialog behavior. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Implementation/ClientNameInputBox.resx](../../../src/Eve-O-Preview/View/Implementation/ClientNameInputBox.resx) | Resource schema/designer metadata; binary payloads are not C# logic. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Implementation/LiveThumbnailView.cs](../../../src/Eve-O-Preview/View/Implementation/LiveThumbnailView.cs) | Persistent DWM maintenance and replacement-before-unregister recovery. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/LiveThumbnailView.resx](../../../src/Eve-O-Preview/View/Implementation/LiveThumbnailView.resx) | Resource schema/designer metadata; binary payloads are not C# logic. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/MainForm.Designer.cs](../../../src/Eve-O-Preview/View/Implementation/MainForm.Designer.cs) | Main form layout, defaults and event wiring; FPS Go is a focus target. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Implementation/MainForm.cs](../../../src/Eve-O-Preview/View/Implementation/MainForm.cs) | View properties, controls, settings commits, validation, tray and profile UI. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Implementation/MainForm.resx](../../../src/Eve-O-Preview/View/Implementation/MainForm.resx) | Designer metadata, hints/strings, embedded icon payloads. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Implementation/StaticThumbnailImage.cs](../../../src/Eve-O-Preview/View/Implementation/StaticThumbnailImage.cs) | PictureBox returning HTTRANSPARENT for form input. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/StaticThumbnailView.cs](../../../src/Eve-O-Preview/View/Implementation/StaticThumbnailView.cs) | Forced bitmap capture, image replacement/disposal, geometry. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailDescription.cs](../../../src/Eve-O-Preview/View/Implementation/ThumbnailDescription.cs) | Full title and mutable disabled state for UI lists. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.Designer.cs](../../../src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.Designer.cs) | Overlay label/image control layout and wiring. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs](../../../src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs) | Owned transparent label form and click forwarding. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.resx](../../../src/Eve-O-Preview/View/Implementation/ThumbnailOverlay.resx) | Resource schema/designer metadata; binary payloads are not C# logic. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailView.Designer.cs](../../../src/Eve-O-Preview/View/Implementation/ThumbnailView.Designer.cs) | Move/resize context menu, tooltip and 350 ms right-click timer. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailView.cs](../../../src/Eve-O-Preview/View/Implementation/ThumbnailView.cs) | Base preview geometry, native restore, highlight, zoom and mouse modes. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailView.resx](../../../src/Eve-O-Preview/View/Implementation/ThumbnailView.resx) | Resource schema/designer metadata; binary payloads are not C# logic. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs](../../../src/Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs) | Per-view creation with cached compatibility/font settings. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Interface/IClientNameInputBoxView.cs](../../../src/Eve-O-Preview/View/Interface/IClientNameInputBoxView.cs) | View callback/property contract; implemented by production controls. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Interface/IMainFormView.cs](../../../src/Eve-O-Preview/View/Interface/IMainFormView.cs) | View callback/property contract; implemented by production controls. | [application-and-configuration](application-and-configuration.md) |
| [src/Eve-O-Preview/View/Interface/IThumbnailDescription.cs](../../../src/Eve-O-Preview/View/Interface/IThumbnailDescription.cs) | View callback/property contract; implemented by production controls. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Interface/IThumbnailView.cs](../../../src/Eve-O-Preview/View/Interface/IThumbnailView.cs) | View callback/property contract; implemented by production controls. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Interface/IThumbnailViewFactory.cs](../../../src/Eve-O-Preview/View/Interface/IThumbnailViewFactory.cs) | View callback/property contract; implemented by production controls. | [windows-and-thumbnails](windows-and-thumbnails.md) |
| [src/Eve-O-Preview/View/Interface/ViewZoomAnchor.cs](../../../src/Eve-O-Preview/View/Interface/ViewZoomAnchor.cs) | View enum order paired with persisted ZoomAnchor. | [windows-and-thumbnails](windows-and-thumbnails.md) |

## Automated tests (10)

| File | Responsibility / review note | Guide |
| --- | --- | --- |
| [src/tests/Eve-O-Preview.Tests/Checks/CustomAudioTests.cs](../../../src/tests/Eve-O-Preview.Tests/Checks/CustomAudioTests.cs) | Parser, production UI/persistence, synthetic host pipe checks. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Checks/FeatureAvailabilityTests.cs](../../../src/tests/Eve-O-Preview.Tests/Checks/FeatureAvailabilityTests.cs) | Legacy license-field compatibility, feature controls and FPS routing. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Checks/LiveThumbnailTests.cs](../../../src/tests/Eve-O-Preview.Tests/Checks/LiveThumbnailTests.cs) | Simulated healthy/failed DWM lifecycle and unregistered behavior. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Checks/ThumbnailZOrderTests.cs](../../../src/tests/Eve-O-Preview.Tests/Checks/ThumbnailZOrderTests.cs) | Private-desktop preview/overlay order, visibility/focus, immediate activation. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj](../../../src/tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj) | Project target, dependencies, configuration, artifacts and build inclusion. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Infrastructure/Native.cs](../../../src/tests/Eve-O-Preview.Tests/Infrastructure/Native.cs) | Test desktop/window/process native declarations. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Infrastructure/PrivateDesktopRunner.cs](../../../src/tests/Eve-O-Preview.Tests/Infrastructure/PrivateDesktopRunner.cs) | Hidden desktop, apphost worker, timeout/output and handle cleanup. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Infrastructure/Stub.cs](../../../src/tests/Eve-O-Preview.Tests/Infrastructure/Stub.cs) | DispatchProxy-based interface test doubles. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/Program.cs](../../../src/tests/Eve-O-Preview.Tests/Program.cs) | Custom STA xUnit versus private-desktop-worker entry dispatch. | [build-and-test](build-and-test.md) |
| [src/tests/Eve-O-Preview.Tests/README.md](../../../src/tests/Eve-O-Preview.Tests/README.md) | Harness usage, discovery, worker isolation and scenario documentation. | [build-and-test](build-and-test.md) |

## AI documentation added by this review

- [Defect investigation guide](reported-bugs.md): twelve technical investigation candidates with source routes and focused checks.
- [Git-root AGENTS.md](../../../AGENTS.md): discovers the source guides from the repository root.
- [Source README](../../README.md): architecture, task routes, performance map, prompt starters.
- [Source AGENTS.md](../../AGENTS.md): concise common instructions.
- [Main-app instructions](../../Eve-O-Preview/AGENTS.md), [Robin instructions](../../Eve-O-Preview.Robin/AGENTS.md), [test instructions](../../tests/AGENTS.md): local entry points.
- [Application/configuration guide](application-and-configuration.md), [window/thumbnail guide](windows-and-thumbnails.md), [Robin guide](robin.md), [build/test guide](build-and-test.md), and this index: detailed source context.
- [Future feature backlog](feature-backlog.md): nineteen unimplemented/partial/exploratory ideas, current source distinctions and acceptance checks.

Update the appropriate table when adding, deleting or moving a source file. Keep baseline coverage distinct from subsequent changes; do not count a generated path list as evidence of a fresh semantic review.
