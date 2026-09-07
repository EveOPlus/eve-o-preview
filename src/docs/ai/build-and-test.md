# Build, test, and validation guide

Use this guide when choosing a project, diagnosing test discovery, changing tests, or assessing what a passing test run proves. All command examples start in `src` and use PowerShell. The project files and test bodies are authoritative. The baseline documentation review inspected commands without running them; later executed checks are recorded in the validation sections below.


## Choose the project deliberately

| Entry point | Current role and build contract |
| --- | --- |
| [EVE-O-Preview.sln](../../EVE-O-Preview.sln) | Full solution: application, Robin, Mock, tests, and the release tooling in `../build`. Use this solution for Test Explorer. Its `Build` solution configuration selects the release-tool project plus Debug configurations of Robin/tests; the name does not mean an ordinary Release build. |
| [Eve-O-Preview.csproj](../../Eve-O-Preview/Eve-O-Preview.csproj) | SDK project, `net10.0-windows`, WinForms with WPF enabled, assembly name `EVE-O Preview`, unsafe code. Debug is explicitly x64. Release publishing selects `win-x64`. There is no project reference to Robin: building the app does not create the native injection DLL. |
| [Eve-O-Preview.UI.csproj](../../Eve-O-Preview.UI/Eve-O-Preview.UI.csproj) | Portable `net10.0` Avalonia settings UI library. The Windows host references it and embeds its controls through Avalonia WinForms interoperability. It has no game-process, WinForms or Windows handle contract. |
| [UI smoke project](../../tests/Eve-O-Preview.UI.Smoke/Eve-O-Preview.UI.Smoke.csproj) | Plain `net10.0` executable using Avalonia Headless and Skia. Renders the real UI with a controlled in-memory backend; it does not start the production Windows host or inject Robin. |
| [Nested application solution](../../Eve-O-Preview/Eve-O-Preview.sln) | Contains only the application. Opening it will not expose the test project or Robin/Mock/build projects. |
| [Robin project](../../Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj) | `net10.0`, x64, unsafe, NativeAOT shared library (`PublishAot`, `NativeLib=Shared`). A managed `dotnet build` does not establish that the native DLL can be published, loaded, or hooked. |
| [Test project](../../tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj) | `net10.0-windows` executable with WinForms/WPF, xUnit v3, `Microsoft.NET.Test.Sdk`, and the Visual Studio adapter. References the application project, not Robin. Uses its own entry point. |
| [Mock project](../../Eve-O-Mock/Eve-O-Mock.csproj) | Legacy non-SDK WPF project targeting .NET Framework 4.8, `packages.config`, assembly name `ExeFile`. Needs the .NET Framework targeting/build tools and restored `src/packages` dependencies. |
| [Build project](../../../build/Build.csproj) | .NET 10 Cake Frosting release pipeline; its execution has cleanup, documentation, publish, signing, and packaging side effects described below. |

The development target is Windows with the .NET 10 SDK. No tracked `global.json` pins a particular SDK patch. `dotnet --info` establishes the local SDK/runtime and architecture. The [root user README](../../../README.md) and [application app.config](../../Eve-O-Preview/app.config) still contain .NET Framework-era runtime claims; do not infer the current application target from those. The Mock's [App.config](../../Eve-O-Mock/App.config) does match its Framework 4.8 target.

## Routine commands

For application compilation or the existing regression suite:

```powershell
dotnet build .\Eve-O-Preview\Eve-O-Preview.csproj -c Debug
dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug
```

`dotnet test` builds its application reference. A separate app build is useful when compilation alone is the intended check; running both is not mandatory. Debug app output is configured under `src/bin` and the SDK appends its target-framework directory. Release app output defaults to repository-root `bin`, outside `src`. Test output uses its own project `bin/Debug/net10.0-windows` directory. Build artifacts, packages, `.vs`, and user settings are ignored by the [repository ignore rules](../../../.gitignore).

For discovery and focused runs:

```powershell
dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug --list-tests
dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug --filter "DisplayName~HiddenWindowRecovery"
dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug --filter "FullyQualifiedName~CustomAudioTests"
```

Use `--no-build` only after building that same configuration and its current sources. For documentation-only changes, verify links, symbols, inventory, and the diff; running the app or rebuilding native code does not validate prose.

For Robin compilation:

```powershell
dotnet build .\Eve-O-Preview.Robin\Eve-O-Preview.Robin.csproj -c Debug
```

When the change actually concerns NativeAOT output, publish to a distinct staging directory with the required Windows native build tools available:

```powershell
dotnet publish .\Eve-O-Preview.Robin\Eve-O-Preview.Robin.csproj -c Release -r win-x64 -p:PublishAot=true -o .\bin\robin-native-check
```

The injected artifact is the published native `Eve-O-Preview.Robin.dll`, not the managed assembly with the same filename produced by an ordinary build. Treat successful publication, DLL loading, hook installation, and observed frame/audio behavior as separate validation steps.

For Mock development, use Visual Studio with the .NET desktop/.NET Framework 4.8 build tools. Its [package list](../../Eve-O-Mock/packages.config) includes HelixToolkit 2.27.3, SharpDX 4.2.0, and support libraries. The project uses explicit `../packages/...` hint paths and errors when the logging-package build import is missing. In a Developer PowerShell with NuGet CLI and full MSBuild available:

```powershell
nuget restore .\Eve-O-Mock\packages.config -PackagesDirectory .\packages
MSBuild.exe .\Eve-O-Mock\Eve-O-Mock.csproj /p:Configuration=Debug /p:Platform=AnyCPU
```

A failure in the legacy Mock's WPF or package-import targets during a full-solution build does not by itself show that the application or tests fail to build. Select the relevant project before diagnosing a change.

## Portable UI checks

Build the UI independently when changing portable controls, theme tokens or the workspace contract. The host build remains necessary for Windows adapter and integration changes:

```powershell
dotnet build .\Eve-O-Preview.UI\Eve-O-Preview.UI.csproj -c Debug
dotnet run --project .\tests\Eve-O-Preview.UI.Smoke\Eve-O-Preview.UI.Smoke.csproj -- --output .\bin\ui-review
```

The UI and host currently pin Avalonia packages to 11.3.20. Restore the project references together when changing those versions. The standalone UI project is a library, so building it does not create a launchable desktop app. Launch the ordinary Windows host for an integrated run; the smoke executable is an isolated rendering and interaction check.

Release 10.1.0.13 aligns the main application's `Version`, `AssemblyVersion` and `FileVersion` with the UI and Robin project `Version` values (their assembly/file versions are SDK-generated). Keep all three projects aligned on future release bumps. Cake publishes these project files directly; there is no separate hard-coded release version in its tasks. Verify built managed assembly/file metadata and Robin's `GetAssemblyVersion` MSBuild target when checking a version-only change; this does not validate native injection.

The smoke runner uses the production workspace controls with sample state and an in-memory backend, writes page/theme images under the requested output directory, and checks control bounds and command routing. Its snapshots can reveal clipping, missing fields and theme regressions, but they do not verify actual profile persistence, WinForms embedding, native hotkeys, tray behavior, game previews, injection or Linux integration. Keep its generated images out of unrelated source changes. Check the runner source for the current scenarios and consult the recorded validation result before claiming they passed.

The Windows suite also includes `WorkspacePreferencesTests` for the generic global settings file and profile accent behavior, `WorkspaceBackendTests` for the production adapter and asynchronous commit/native routes, and `WorkspaceHostTests` for a private-desktop embedded WinForms/Avalonia host lifecycle. These are separate from the sample backend used by the portable renderer. Use the ordinary focused `dotnet test` command above, or a `FullyQualifiedName~Workspace` filter when investigating only this layer.

`WorkspaceHostTests.WorkspaceTracksDpiChangesWithoutReplacingContentOrLosingDrafts` sends synthetic 100/125/150/200% DPI transitions through the production form. It checks the Avalonia render scale and logical bounds, unchanged HWNDs/content, focused draft retention, native-pixel title sample sizing, Legacy scaling, and modern size restoration after leaving Legacy at a different DPI. The pre-fix check reproduced a 125% WinForms host with Avalonia still at 100%. The worker also moves its own form through available monitors and reports their DPI; the validation environment exposed three 96-DPI monitors. Synthetic messages do not change Windows' nonclient metrics or establish physical mixed-DPI behavior. Manually drag the workspace both ways between differently scaled monitors, including while editing, after minimizing/restoring, and while using Legacy. Do not change the user's display settings or capture the desktop for automated validation.

For a release, verify the published output includes the UI assembly, Avalonia dependencies and required native renderer assets. A successful library build is not a packaged-host launch check. Use a separate staging output and the focused publish guidance above; do not invoke Cake packaging as a UI smoke check.

The following focused host publish was verified without running the release packager. Use a distinct build output when changing self-contained mode so cached runtime metadata from another publish cannot be reused:

```powershell
$uiBuildOutput = Join-Path $PWD 'bin\ui-publish-build'
dotnet restore .\Eve-O-Preview\Eve-O-Preview.csproj -r win-x64 -p:SelfContained=false -p:PublishSelfContained=false --ignore-failed-sources
dotnet publish .\Eve-O-Preview\Eve-O-Preview.csproj -c Debug -r win-x64 -p:SelfContained=false -p:PublishSelfContained=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true "-p:OutputPath=$uiBuildOutput" -o .\bin\ui-publish-check --no-restore -v minimal
```

### UI modernization validation recorded 2026-09-07

The first table records the initial implementation. The follow-up below supersedes its visual acceptance: user review found poor title-preview fidelity, wasted editing space and insufficient resemblance to the original Legacy UI despite the initial checks passing.

| Check | Recorded result | What it establishes |
| --- | --- | --- |
| `dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug` | 83 of 83 passed for the initial implementation | Existing regressions plus production workspace adapter, generic global preference persistence, actual profile accent round-trip, and real WinForms/Avalonia control-host lifecycle on a private desktop. The host case verifies native simulated-client foreground preservation during background refresh and cancel/discard handling for unapplied edits on close. |
| Portable UI smoke command above | Passed; 47 production-control captures | All three themes and feature pages, including dense lower-page content; theme/setting command routing, invalid input, draft retention, search, and profile accent changes across profile selection without changing the global theme. |
| Render sizes | 1180 by 820, 940 by 650 and 784 by 581 | Normal, compact and minimum host-client-area layouts were captured. Representative Dark, Light and Legacy views, including profile/cycle pages and minimum-size examples, were visually inspected without observed clipping. This is narrower than testing arbitrary text scaling or real mixed-DPI monitors. |
| Direct host publish to `src/bin/ui-publish-check` | Passed | Debug `win-x64` framework-dependent single-file staging; generated runtime metadata was checked to reference the installed .NET frameworks. This validates publish output generation, not a completed signed release or a game integration run. |

The build/publish output retained the existing DPI-manifest and Costura warnings. This UI validation did not run Cake, publish/inject Robin, test a live EVE session, or implement/validate a Linux backend. Real game focus/rendering/audio behavior, screen-reader interaction and multi-monitor DPI remain separate acceptance checks.

#### Follow-up title and Legacy corrections

| Check | Recorded result | Evidence and limits |
| --- | --- | --- |
| Full Windows regression suite | 86 of 86 passed | Adds original-form capture, actual title/highlight pixel comparison and production Windows editor interaction to the prior 83 cases. |
| `WorkspacePreviewRenderingTests` | Passed | Exact pixel equality for four actual `ThumbnailOverlay` label instances across font families, styles, fill/outline colors, fractional outline widths and signed offsets. The reference invokes the production label's paint method using its real size/location because capturing the layered form through `DrawToBitmap` returns a blank title. Highlight comparison uses actual `ThumbnailView` image insets at 1, 3 and 6 pixels with native window frames disabled. |
| `WorkspaceVisualReviewTests` | Passed | Actual Windows backend rendering in the embedded workspace; decimal font size 14.25 and outline width 1.25 survive typing/blur, draft font/style/color/position changes update pixels before saving, grouped Apply reaches the backend, and the preview stays fixed while scrolling. At the minimum 784 × 581 client area, the editor viewport must retain at least 200 pixels of height. |
| Original / Legacy captures | All nine tabs inspected | Original native form and replacement Legacy are compared at a 460 × 417 client area. Fixed spinner overlap, panel borders and two-line text fit; the gray font sample remains at actual size. Normal emulation differences remain in native versus Avalonia control rendering. |
| Portable smoke | Passed; 42 captures | Compact modern pages, 460 × 417 Legacy, theme/profile identity, search, validation and commands. Its illustrative fallback is not the Windows font-fidelity reference; the native tests above supply that evidence. |
| Final layout checks | 2 of 2 host/visual cases and 42 portable captures passed again | Reran after widening the normal-window preview pane to fit the default 384 × 216 content at actual size; compact editor dimensions remain unchanged. |
| Updated host publish | Passed to `src/bin/ui-review-fixed` | Debug `win-x64` framework-dependent single-file host, 22,170,579 bytes. Used isolated build output `src/bin/ui-review-fixed-build` and verified runtime metadata references the installed .NET frameworks. This is a replacement host executable, not a complete release package including Robin. |

The running development app locked its normal output executable during this review. Tests therefore used isolated output without stopping it. In the restricted environment, the optional Avalonia statistics target was disabled for these commands:

```powershell
dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug -p:OutputPath=C:\dev\eve-o-preview\src\bin\ui-validation\ -p:UsedAvaloniaProducts= --no-restore -v minimal
```

These corrections do not add a settings file, change the live overlay implementation or touch game/native IPC. Actual/Fit display scaling is implemented, but physical mixed-DPI monitor testing remains separate from the 96-DPI private-desktop captures.

The final publish retained the existing DPI-manifest and Costura warnings. NuGet's vulnerability feed was unreachable in the restricted environment (`NU1900`); cached package restore and publication succeeded. No running user application was stopped or overwritten.

## Test Explorer and private desktop workers

Read the [test README](../../tests/Eve-O-Preview.Tests/README.md), [test entry point](../../tests/Eve-O-Preview.Tests/Program.cs), and [PrivateDesktopRunner](../../tests/Eve-O-Preview.Tests/Infrastructure/PrivateDesktopRunner.cs) together.

- Normal entry, including IDE launches, delegates to xUnit's in-process console runner. `XunitAutoGeneratedEntryPoint=false` preserves this dispatch. Do not replace it with a worker-only runner or remove the test adapter to solve discovery symptoms.
- The exact three-argument `--private-desktop` entry is internal worker dispatch. The parent creates a new Windows desktop, never switches the user to it, and starts the test assembly's `.exe` apphost with `CREATE_NO_WINDOW`. Using the current IDE/testhost/dotnet process path here would launch the wrong program.
- Each UI/window scenario runs on a fresh STA process, creates real WinForms windows on the hidden desktop, and writes stdout/stderr to a temporary file. Exit failure or a 30-second timeout fails the xUnit case; worker output is copied into test output. Handles and the temporary log are cleaned up. Debugging a window scenario requires attaching to its worker process.
- Tests instantiate production forms and services directly, using reflection for internal types/private methods. They do not run the application's startup, global input hooks, injection, or debugger sidecar. `Stub` supplies interface implementations; the thumbnail subclass replaces image rendering, and live-view tests replace DWM operations with simulated results.
- Focus checks require a real nonzero active simulated-client handle and compare active/foreground handles immediately before/after the operation. The hidden desktop is not the user's input desktop; this is narrower than live EVE foreground validation.
- When no tests are discovered, check the selected solution, restore/build output, adapter/package references, and the CLI discovery command first. The IDE's active Test Explorer log filename alone is not evidence of a particular failure. Preserve actual failure output instead of attributing it to the IDE without a reproduction.

## Existing validation coverage

The focused xUnit suite passed 50 cases during the 2026-09-06 defect investigation (baseline 36). It retains the xUnit v3 adapter/custom private-desktop entry point.

| Area | Coverage |
| --- | --- |
| Legacy profiles / feature availability | Existing feature tests retain obsolete-key migration, FPS enable/disable, resource and UI availability checks. |
| Custom audio | Parsing/UI/persistence plus both legacy clear/add and current atomic replacement through the production host pipe sender. |
| Visibility and DWM | Eleven private-desktop z-order/activation scenarios and five live-image cases preserve nonactivation, immediate raises, recovery and stable DWM relationships with a simulated renderer. |
| Profiles | ProfileWorkflowTests covers omitted/null defaults, malformed-load rollback, migration idempotence/duplicate keys, missing Default, clone and rename/save/reload. |
| Settings/input/resources | SettingsIntegrationTests uses production forms, manager/factory and event subscriptions. It exercises Move Up/empty groups/font suppression, live profile propagation, modifier release, immediate borders/selection with pending affinity and wake-before-activation window messages, repeated real process enumeration/handle stability, actual affinity restoration/terminal shutdown, delayed UI cleanup/repeated close, and GDI capture cleanup in an isolated worker. |
| Pipe lifecycle | PipeLifecycleTests verifies zero targets, shutdown audio clearing and bounded responses from connected-but-silent/truncated peers, plus ready-pipe wake/prediction issuance and old zero-buffer compatibility. |

These tests do not inject Robin or prove real desktop/GPU/audio performance. The optional [native smoke driver](../../tests/Robin.NativeSmoke/README.md) publishes/loads the real AOT DLL through production injection, then checks actual DXGI pacing and synthetic audio/pipe/lifecycle boundaries. Its allocation measurement compiles the same source as managed code and is explicitly separate from native timing. See [recorded results and remaining checks](reported-bugs.md).

## Manual Mock integration

[MainWindow.xaml](../../Eve-O-Mock/MainWindow.xaml) enables swap-chain rendering with FXAA/MSAA disabled. [MainWindow.xaml.cs](../../Eve-O-Mock/MainWindow.xaml.cs) creates a rotating cube with randomized material, background, and window-title suffix, and disposes the effects manager on close. The rotation deliberately makes stalled/frozen previews visible; this small rendering loop is a manual fixture, not a performance model for EVE.

The assembly is intentionally named `ExeFile`: [ProcessMonitor](../../Eve-O-Preview/Services/Implementation/ProcessMonitor.cs) matches that process name case-insensitively. A `Mock...` title still produces a monitored process because detection uses the process name. Renaming the assembly without adjusting the fixture contract breaks discovery. Multiple instances give distinguishable windows for order, title, geometry, and movement checks.

For a relevant manual regression, record the application/Robin build, render mode, refresh period, number of clients, OS, GPU, display/DPI layout, and exact sequence. Check a normal state, the regression trigger, and the recovery/disabled state. Mock checks can validate process discovery and animated preview behavior; they do not emulate EVE's audio module or establish EVE-specific injection compatibility.

## Packaging, resources, and operational paths

The [Fody configuration](../../Eve-O-Preview/FodyWeavers.xml) enables Costura and excludes embedded debug symbols and `Eve-O-Preview.UI`. The UI library stays copy-local for incremental development builds and is bundled by the SDK for single-file publishing. An old loose UI DLL previously shadowed its newer embedded copy, causing an Autofac-wrapped `TypeLoadException`. [FodyWeavers.xsd](../../Eve-O-Preview/FodyWeavers.xsd) is generated schema; edit the XML/project configuration when changing weaving, then let the schema regenerate. The app project copies the license, verbose launcher, and public root certificate; all three are excluded from single-file extraction so release tooling can distribute them separately. The native Robin DLL is produced separately. `AboutBox.cs`, its designer, and its resources remain tracked but are explicitly excluded from the app project.

[app.manifest](../../Eve-O-Preview/app.manifest) runs `asInvoker`, sets `uiAccess=false`, and declares per-monitor DPI awareness including `PerMonitorV2`. Its commented historical OS declarations are not current support guarantees. [Program.cs](../../Eve-O-Preview/Program.cs) accepts `-v`/`--verbose`, writes rolling `logs/EVE-O Preview Log-.txt` files relative to the working directory, rolls at 10 MiB as well as daily, and retains seven files. The [verbose launcher](<../../Eve-O-Preview/Launch Eve-O Preview with Verbose Logging.cmd>) starts `EVE-O Preview.exe -v` without changing directory. Account for the working directory when locating logs or reproducing launches.

The [Cake entry point](../../../build/Program.cs) uses `../` from the build project as its working directory. [Configuration](../../../build/Configuration.cs) names `tools`, the two publish projects, Release configuration, and a machine-specific signing-certificate path. [Context](../../../build/Context.cs) defaults to repository-root `bin` and `publish`; `--output-root` selects a separate parent inside the repository, including intermediate app/Robin output. The declared MSBuild executable path is not consumed by `DotNetPublish`.

Execution flows through these files:

1. [Lifetime.Setup](../../../build/Lifetime.cs) recursively deletes `bin` and `publish` beneath the selected output root and downloads `tools/nuget.exe` if absent. This setup runs even when selecting a narrower Cake task; it is not a harmless substitute for `dotnet build`. Verify the selected output paths before running.
2. [Documentation](../../../build/Tasks/Documentation.cs) converts repository-root `readme.md` to `bin/readme.pdf` using the [HTML](../../../build/Themes/Github/Theme.html)/[CSS](../../../build/Themes/Github/Theme.css) theme. It does not package this AI guide automatically.
3. [Build](../../../build/Tasks/Build.cs) publishes the main app as an explicitly framework-dependent `win-x64` single file with native/content extraction, then publishes Robin as self-contained NativeAOT to the selected `bin`. The GUI requires the .NET 10 Windows Desktop runtime. Optional Avalonia build statistics are disabled for reproducible release builds without per-user telemetry writes.
4. [ValidateWorkspace](../../../build/Tasks/ValidateWorkspace.cs) copies only the published executable into an empty staging directory and runs `--validate-workspace` with a 30-second timeout. It resolves production workspace dependencies using temporary configuration and renders the actual UI through Skia. Missing bundled UI assemblies, resources or native rendering dependencies fail the pipeline before signing. It does not start discovery, global input subscriptions, injection or the debugger sidecar, and does not read user profiles.
5. [Sign](../../../build/Tasks/Sign.cs) signs the top-level published executable/native DLL with the configured certificate, prompts for its password, and timestamps through DigiCert. A blank certificate path or explicit `--skip-signing=true` skips signing. Keep credentials at the interactive prompt; do not add them to configuration or command files. Certificate installation is not an application-build prerequisite.
6. [Zip](../../../build/Tasks/Zip.cs) includes the app, native Robin DLL, license, user PDF, and verbose launcher in `publish/EVE-O Preview.zip`; copies `bin/EveoPreviewRootCA.crt` separately. The UI and Avalonia native dependencies are inside the executable, not additional ZIP entries.
7. [Default](../../../build/Tasks/Default.cs) waits on `Console.ReadLine()` after packaging. Select `--target=Zip` to finish without that final pause.

For an isolated release validation, run from `build/`:

```powershell
dotnet run --project .\Build.csproj -- --target=Zip --output-root=src/bin/cake-validation
```

Add `--skip-signing=true` for an unsigned local package. Normal `Default` continues to sign by default. When NativeAOT's Visual Studio discovery prints a `vswhere.exe` command-not-found message into the linker path, add the Visual Studio Installer directory to the current process's `PATH`, or use a correctly configured Visual Studio developer shell; do not modify Robin's native code to work around tool discovery.

### Startup and Cake validation, 2026-09-07

- Normal Debug build reproduced the stale UI DLL crash, then passed `--validate-workspace` after the copy-local fix; source/output UI hashes matched.
- All 87 Windows regression cases passed, including the added production Autofac composition check.
- The actual Cake `Zip` task chain passed documentation, Release host publishing, Robin NativeAOT publishing, isolated executable rendering, signing, timestamping and ZIP creation under `src/bin/cake-validation`.
- The five-entry ZIP was extracted into a separate directory; its signed executable passed `--validate-workspace` without loose UI/Avalonia DLLs. The public certificate was copied beside the ZIP.
- SignTool recorded both signatures and verified their DigiCert timestamps. Windows trust verification returned `0x800B010A` because this machine could not build the signer chain to its private EVE-O root CA. The certificate store was not changed. Signing in the restricted process initially failed; the authorized run with Windows certificate-key access succeeded.
- Existing DPI-manifest/Costura warnings and Cake's transitive NuGet low-severity advisories remain. No live game injection, audio, or Linux runtime validation was performed in this packaging check.

### Donation UI and portrait cache validation, 2026-09-07

- The normal Debug application build passed. All 90 Windows regression cases passed, including production dependency composition, donation dialog interactions and portrait cache download/refresh/failure behavior.
- The portable UI smoke harness passed 48 captures across Light, Dark and Legacy, including constrained window sizes, delayed portrait delivery, clipboard actions, keyboard dismissal and preservation of settings drafts.
- The real Windows executable started with four EVE clients present, downloaded Aura Asuna's portrait and stored `Cache/Portraits/95465272.jpg` beside its resolved application settings. A restored settings-window capture confirmed the donation invitation at the top of Overview and the final wording without an advertising promise.
- These UI changes were not repackaged or signed in this follow-up. The earlier Cake results above apply to that earlier build. The live startup and settings capture do not establish native injection, audio or frame-pacing correctness.

### Character order and temporary skip validation, 2026-09-07

- The application built and all 93 Windows tests passed using isolated `src/bin/cycle-validation` output while the user's running app stayed open. New coverage exercises exact-title move commands, shared skip state, profile isolation, session-only serialization, thumbnail menu toggles, forward/backward selection and prediction, all/one eligible member, and manual activation of a skipped character.
- The UI smoke harness passed 56 captures. Actual pointer input verifies drag/drop in both directions, one save per drop, Escape cancellation, edge scrolling while held, scrollbar clearance, Skip/Resume actions and scroll retention. The expanded editor fills the existing window; Windows host checks assert that its bounds and window state remain unchanged in all three themes.
- Native pixel comparisons cover the default red circle/slash, Pause and Cross markers, including a marker without title text. They compare the actual overlay control with the Windows settings preview renderer.
- This follow-up did not run Cake/signing or a live EVE combat scenario. The existing simulated/native-window regression checks remain distinct from real game injection, audio and frame-pacing validation.

### Thumbnail menu order validation, 2026-09-07

- The application built and all 96 Windows tests passed with isolated `src/bin/menu-validation` output. The private-desktop `ThumbnailMenuOrdering` scenario opens production menus and sends a second right-click at the original pointer position, verifying default Minimize and configured Skip/Resume across all themes. Reordering and restoring defaults retain all actions on existing thumbnails.
- Preference/backend checks cover restart persistence in the existing global file, future fields, malformed/older action lists, invalid moves and no gameplay-profile saves. Host checks verify immediate propagation when preferences change.
- The UI harness passed 59 captures, including the compact menu editor in each theme, reorder/reset controls and unchanged Legacy window bounds. Appearance's profile-accent card explains profile identity independently of the Legacy theme choice.
- This follow-up did not run Cake or a live EVE interaction; validation used isolated native windows and the production Avalonia render harness.

### Menu hover, dividers and palettes follow-up, 2026-09-07

- A new `ThumbnailMenuHover` regression failed before the fix: entering the open menu reset zoomed bounds/opacity. The fix defers hover exit until close, recognizes the menu's native handle, and defers dirty MRU raises while a menu is open. The check now passes across refresh ticks, focus-based hiding, overlay z-order and explicit thumbnail hiding. This isolates the hover/menu mechanism; it is not a live EVE-session reproduction.
- All 97 Windows tests passed with `src/bin/menu-validation` output. After final overlay-click coordinate/lifecycle edits, all 14 thumbnail scenarios passed again. `ThumbnailMenuOrdering` verifies first-action hit testing on the image, title and overlay, exact default/custom divider placement, all 11 native palette colors and existing double-right-click actions. Native menu PNGs are captured under `native-menu-themes` in that output directory.
- The portable smoke harness passed 92 renders. It drives real pointer dragging for menu actions/dividers, cancellation, insert/remove/reset, revisiting saved choices and every palette in Light/Dark/Legacy. The two-divider default is represented in both the editor and live preview. Earlier saved action-only lists remain action-only until reset or edited; dividers are no longer inferred during opening.
- No Cake/signing or live EVE interaction ran for this follow-up. The user's running app was left open; tests and captures use isolated output and desktops.

These are concrete release-workflow side effects and assumptions, not requirements for ordinary source edits. No tracked CI workflow, standalone tool script, or source under `tools` was present at the review. Ignored downloads and prior binary outputs are not a substitute for current source or a verified release build.
