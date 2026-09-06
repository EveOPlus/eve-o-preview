# Build, test, and validation guide

Use this guide when choosing a project, diagnosing test discovery, changing tests, or assessing what a passing test run proves. All command examples start in `src` and use PowerShell. The project files and test bodies are authoritative; commands below were inspected against those files, not executed as part of the documentation review.


## Choose the project deliberately

| Entry point | Current role and build contract |
| --- | --- |
| [EVE-O-Preview.sln](../../EVE-O-Preview.sln) | Full solution: application, Robin, Mock, tests, and the release tooling in `../build`. Use this solution for Test Explorer. Its `Build` solution configuration selects the release-tool project plus Debug configurations of Robin/tests; the name does not mean an ordinary Release build. |
| [Eve-O-Preview.csproj](../../Eve-O-Preview/Eve-O-Preview.csproj) | SDK project, `net10.0-windows`, WinForms with WPF enabled, assembly name `EVE-O Preview`, unsafe code. Debug is explicitly x64. Release publishing selects `win-x64`. There is no project reference to Robin: building the app does not create the native injection DLL. |
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

The [Fody configuration](../../Eve-O-Preview/FodyWeavers.xml) enables Costura and excludes embedded debug symbols. [FodyWeavers.xsd](../../Eve-O-Preview/FodyWeavers.xsd) is generated schema; edit the XML/project configuration when changing weaving, then let the schema regenerate. The app project copies the license, verbose launcher, and root certificate. The native Robin DLL is produced separately. `AboutBox.cs`, its designer, and its resources remain tracked but are explicitly excluded from the app project.

[app.manifest](../../Eve-O-Preview/app.manifest) runs `asInvoker`, sets `uiAccess=false`, and declares per-monitor DPI awareness including `PerMonitorV2`. Its commented historical OS declarations are not current support guarantees. [Program.cs](../../Eve-O-Preview/Program.cs) accepts `-v`/`--verbose`, writes rolling `logs/EVE-O Preview Log-.txt` files relative to the working directory, rolls at 10 MiB as well as daily, and retains seven files. The [verbose launcher](<../../Eve-O-Preview/Launch Eve-O Preview with Verbose Logging.cmd>) starts `EVE-O Preview.exe -v` without changing directory. Account for the working directory when locating logs or reproducing launches.

The [Cake entry point](../../../build/Program.cs) uses `../` from the build project as its working directory. [Configuration](../../../build/Configuration.cs) names repository-root `bin`, `publish`, and `tools`, the two publish projects, Release configuration, and a machine-specific signing-certificate path. Its declared MSBuild executable path is not consumed by the current `DotNetPublish` task.

Execution flows through these files:

1. [Lifetime.Setup](../../../build/Lifetime.cs) recursively deletes `bin` and `publish` and downloads `tools/nuget.exe` if absent. This setup runs even when selecting a narrower Cake task; it is not a harmless substitute for `dotnet build`.
2. [Documentation](../../../build/Tasks/Documentation.cs) converts repository-root `readme.md` to `bin/readme.pdf` using the [HTML](../../../build/Themes/Github/Theme.html)/[CSS](../../../build/Themes/Github/Theme.css) theme. It does not package this AI guide automatically.
3. [Build](../../../build/Tasks/Build.cs) publishes the main app as a framework-dependent `win-x64` single file with extraction options, then publishes Robin as self-contained NativeAOT to the same root `bin`. The release is therefore not a fully self-contained GUI application.
4. [Sign](../../../build/Tasks/Sign.cs) uses the configured certificate, prompts for its password, and timestamps through DigiCert; a blank certificate path skips signing. Certificate installation is not an application-build prerequisite.
5. [Zip](../../../build/Tasks/Zip.cs) includes the app, native Robin DLL, license, user PDF, and verbose launcher in `publish/EVE-O Preview.zip`; copies the public certificate separately. Its source certificate path assumes `bin/net10.0-windows/win-x64/EveoPreviewRootCA.crt` even though publish output is set to root `bin`; verify actual output before relying on that path.
6. [Default](../../../build/Tasks/Default.cs) waits on `Console.ReadLine()` after packaging.

These are concrete release-workflow side effects and assumptions, not requirements for ordinary source edits. No tracked CI workflow, standalone tool script, or source under `tools` was present at the review. Ignored downloads and prior binary outputs are not a substitute for current source or a verified release build.
