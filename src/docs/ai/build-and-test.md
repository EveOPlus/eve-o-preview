# Build, test, and validation guide

Use this guide when choosing a project, diagnosing test discovery, changing tests, or assessing what a passing test run proves. All command examples start in `src` and use PowerShell. The project files and test bodies are authoritative. The baseline documentation review inspected commands without running them; later executed checks are recorded in the validation sections below.


## Choose the project deliberately

| Entry point | Current role and build contract |
| --- | --- |
| [EVE-O-Preview.sln](../../EVE-O-Preview.sln) | Full solution: application, Robin, Mock, tests, and the release tooling in `../build`. Use this solution for Test Explorer. Its `Build` solution configuration selects the release-tool project plus Debug configurations of Robin/tests; the name does not mean an ordinary Release build. |
| [Eve-O-Preview.csproj](../../Eve-O-Preview/Eve-O-Preview.csproj) | SDK project, `net10.0-windows`, WinForms with WPF enabled, assembly name `EVE-O Preview`, unsafe code. Debug is explicitly x64. Release publishing selects `win-x64`. There is no project reference to Robin: building the app does not create the native injection DLL. |
| [Eve-O-Preview.UI.csproj](../../Eve-O-Preview.UI/Eve-O-Preview.UI.csproj) | Portable `net10.0` Avalonia settings UI library. The Windows host references it and embeds its controls through Avalonia WinForms interoperability. It has no game-process, WinForms or Windows handle contract. |
| [Preview contracts](../../Eve-O-Preview.Preview/Eve-O-Preview.Preview.csproj) | Plain `net10.0` image-session and overlay contracts. No Windows or UI toolkit dependency; Windows keeps its persistent DWM backend. |
| [UI smoke project](../../tests/Eve-O-Preview.UI.Smoke/Eve-O-Preview.UI.Smoke.csproj) | Plain `net10.0` executable using Avalonia Headless and Skia. Renders the real UI with a controlled in-memory backend; it does not start the production Windows host or inject Robin. |
| [Portable overlay smoke](../../tests/Eve-O-Preview.Preview.Smoke/Eve-O-Preview.Preview.Smoke.csproj) | Plain `net10.0` retained scene and finite compositor-animation checks with the portable Avalonia overlay candidate. Does not prove Linux capture/hosting. |
| [Windows renderer harness](../../tests/Preview.RenderingSmoke/Preview.RenderingSmoke.csproj) | Opt-in mock/live comparison using production DWM views and Legacy/native/Avalonia overlays. Requires an interactive desktop for real compositor evidence; see its [instructions](../../tests/Preview.RenderingSmoke/README.md). |
| [Nested application solution](../../Eve-O-Preview/Eve-O-Preview.sln) | Contains only the application. Opening it will not expose the test project or Robin/Mock/build projects. |
| [Robin project](../../Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj) | `net10.0`, x64, unsafe, NativeAOT shared library (`PublishAot`, `NativeLib=Shared`). A managed `dotnet build` does not establish that the native DLL can be published, loaded, or hooked. |
| [Test project](../../tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj) | `net10.0-windows` executable with WinForms/WPF, xUnit v3, `Microsoft.NET.Test.Sdk`, and the Visual Studio adapter. References the application project, not Robin. Uses its own entry point. |
| [Mock project](../../Eve-O-Mock/Eve-O-Mock.csproj) | Legacy non-SDK WPF project targeting .NET Framework 4.8, `packages.config`, assembly name `ExeFile`. Needs the .NET Framework targeting/build tools and restored `src/packages` dependencies. |
| [Build project](../../../build/Build.csproj) | .NET 10 Cake Frosting release pipeline; its execution has cleanup, documentation, publish, signing, and packaging side effects described below. |

The development target is Windows with the .NET 10 SDK. No tracked `global.json` pins a particular SDK patch. `dotnet --info` establishes the local SDK/runtime and architecture. The [root user README](../../../README.md) and [application app.config](../../Eve-O-Preview/app.config) still contain .NET Framework-era runtime claims; do not infer the current application target from those. The Mock's [App.config](../../Eve-O-Mock/App.config) does match its Framework 4.8 target.

## Routine commands

The host also references `Microsoft.Data.Sqlite` for [Augments](combat-logs.md).
Keep its SQLite native runtime assets when publishing; the portable UI and Robin
do not reference that package. The damage/platform SVGs and offline SDE catalog are
embedded resources, not external files the user must install.
The portable UI also references `Avalonia.Controls.ColorPicker` at the same
version as Avalonia. Include that assembly and its Fluent theme resources in
application packaging; `--validate-workspace` checks the bundled resource route.
`FodyWeavers.xml` excludes both the UI and its shared Preview contract from
Costura embedding so normal copy-local builds update them together. An older
loose Preview DLL can otherwise shadow the newly embedded contract and fail to
load added types during workspace startup. Keep both DLLs with ordinary builds;
SDK single-file publishing handles them together with the application.

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

### Character reset and bundled log samples (2026-09-14)

Character-scoped reset regressions and three bundled anonymized log replays are
ordinary xUnit cases in Visual Studio Test Explorer. The existing runner and smoke
project structure remain unchanged. Unit tests use simulated clients and bundled
data; live/hardware diagnostics remain separate integration tools. The fixtures
contain 183 combat messages, including zero-HP repairs and sparse artillery hits.
The final version **10.1.0.16** run passed **321/321** unit tests. An existing
identity-cache retry case failed in the first full run, then passed in isolation
and in the final full run; no unrelated test was changed. **336** UI renders
passed, including both character-reset popups, scoped host requests and all 18
language catalogs. Light/Dark confirmation captures were inspected.
The final isolated and installed startup checks passed. Version 10.1.0.16 was
installed with settings/database/binary backups and 72 verified file copies;
settings stayed unchanged and the reopened application had a responsive window.

### Recorded DPS and combined statistics (2026-09-14)

**115/115** focused combat, repair, activity, backend and localization checks
passed. New regressions cover complete middle intervals, 15/30/60-second volleys,
weighted averages across encounters, independent directions/categories, idle gaps,
scoped resets, delayed files, duplicate delivery, rollback, version-2 migration,
retention/restart and simulation isolation. The isolated Debug app build passed.
The UI smoke passed **390** production renders, including installed-SDE simulator
choices, combined totals and labelled repair HP/cycles. The new overview and repair
detail screens were inspected in Light/Dark; search destinations passed.

The isolated corpus replay read **2,980** game files (**382,635** parsed entries)
and **1,439** Local files. It retained **312,084** accepted DPS sample seconds;
each stream's accepted damage remained within its logged total. Totals,
duplicate-delivery checks and simulation isolation passed. These checks use
recorded logs and simulated UI, not new live combat. Isolated and installed Windows
workspace startup checks passed. Installation backed up the settings, combat
database and binaries, verified 72 copied files, preserved settings and reopened
the application with a responsive native window.

### Simultaneous repairs and simulator dropdowns (2026-09-14)

**94/94** focused combat, native thumbnail, layout, backend and localization
checks passed. Coverage includes paired shield/armour/hull events through the
production parser, separate repair rows, retained row order when one expires,
both directions in temporary activity totals and no repair persistence after
simulation. The UI smoke passed **380** renders, including the actual installed
weapon catalog, standard weapon/ammunition dropdowns and both-direction repair
previews in Light/Dark. Dropdown and repair captures were inspected.

The isolated native harness showed paired shield, armour and hull repairs on
two real EVE DWM thumbnails. Focus and the two DWM registrations stayed unchanged;
no simulated entries reached disk, and Stop restored live data. App builds and
isolated/installed workspace startup validation passed; the updated application
was reopened with its settings unchanged. These checks use synthetic repairs,
not observed live logistics combat.

### Simple augment visibility (2026-09-14)

The Simple-mode DPS, alpha and weapon-icon switches passed **77/77** focused
combat, backend and localization checks and **316** production UI renders.
Light/Dark checks cover shared defaults, per-client overrides, mode switching,
retention of Advanced colours/symbols and simulator enable actions that keep
the current configuration mode. Saved Simple visibility is also checked through
`ApplicationPreferences` reload and the production formatter. The actual Simple
screens were inspected in both themes; isolated native workspace startup passed.

### Complete weapon platforms and configurable fade (2026-09-14)

The full SDE audit examined all 52,999 types and 1,610 groups in verified build
3503375. The final catalog has 187 standard weapons across 27 modeled families,
303 ammunition choices and 5,223 NPCs. Every compatible weapon/ammo pair was
round-tripped in both directions, plus every NPC's incoming/outgoing scenario:
**13,869 entries, zero parser differences**. The real corpus replay read 2,980
game files / 382,519 entries and 1,439 Local files; duplicate delivery preserved
totals and temporary resets left the real store unchanged.

**135/135** focused static-data, combat, activity, layout, backend and native
checks passed. Regressions cover installed-index rollback/local upgrade, explicit
old enum values, inherited custom styles, metadata-only enrichment, fighter
primary attributes, delayed superweapon bursts/cooldowns, disintegrator ramp and
0–100% fade opacity. Both renderers checked all **32** monochrome platform SVGs
at 12/16/24 pixels. The UI smoke passed **370** renders, including all 18 catalogs,
every new platform editor and **54** selections using the actual installed SDE
catalog in Light/Dark. Keep artifact output under ignored `bin/`.

The live native harness checked two current EVE windows with isolated state and
no gameplay input. Its existing phase/expiry, DWM/focus and history-isolation
checks now also require configured 10% thumbnail alpha during the fade. These
are synthetic attacks on real DWM thumbnails, not observed live superweapon combat.
The isolated Debug application build and installed `--validate-workspace` passed.
The existing installation was updated without a version bump and reopened to a
responding workspace. Its complete 676,271-record SDE upgraded to classification
revision 2 locally. No Cake packaging or NativeAOT rebuild was needed.

### Icon samples and solar system layout (2026-09-14)

The focused backend, combat, localization and native preview checks passed
**91/91**. System placement covers all four directions at two font sizes,
including cropped native asset bounds. Settings checks cover persistence,
validation, shared updates preserving per-client combat styles, and title-size
inheritance. The UI smoke passed **258** production renders, including icon
samples following draft colours and selected symbols, solar system Apply/Reset
controls, all placements in Light/Dark and all 18 catalogs. The compact title
sample expands to show the combined title/system block. These checks use
controlled backends and private desktops; they do not establish live game combat.

### Character activity and shared pickers (2026-09-14)

The focused combat/activity/static-data/localization/rendering checks passed
**57/57**. Coverage includes retained-history migration, pruning/replay, restart,
source retraction, travel ordering and reconnects, reset, rollback and simulation
isolation. The workspace smoke passed **240** production renders, including
compact portrait rows, keyboard selection of separate character details, stable
focus/expanders, existing name flashing, standard font dropdowns and Title &
Highlight RGB popup/draft behavior. All 18 catalogs passed key/format checks.
The rebuilt Windows executable passed `--validate-workspace`. These UI renders
use the headless backend; they are not evidence of new live EVE combat or jumps.

The subsequent weapon-platform/log-format pass checked all 2,980 year-to-date
game logs (382,519 entries) and round-tripped 12,508 SDE simulations through the
live parser without metadata differences. **59/59** focused checks and **242**
UI renders passed. The two added regressions cover separator-bearing NPC names,
outgoing damage attribution, invariant numeric log text and independent alpha
weapon colouring. UI checks cover hidden-alpha dependencies, shared/per-client
enable actions, platform selection and outgoing NPC weapon controls. The final
localization check and Windows `--validate-workspace` also passed.

### Augments checks (2026-09-13)

The Windows regression suite passed **198/198** after log ingestion, persistent
stats, automatic/manual directory selection, alpha/DPS modes and SVG rendering.
The workspace smoke passed **176** production renders, including Augments in
Light/Dark, Simple/Advanced selection, custom colours, measurement modes, manual
folder/automatic reset, draft retention, simulations, repairs, overview name-flash
colours/source filters, shared defaults/per-client controls and compact layout.
The portable overlay smoke passed retained scene, unchanged-state, clipping and
finite animation checks.

Focused command:

```powershell
dotnet test tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj -c Debug --filter "FullyQualifiedName~Combat"
```

`CombatLogTests` uses temporary synthetic files to verify split Unicode/newlines,
shared handles, exclusive-writer recovery, multiple concurrent files, rotation,
restart, watcher overflow recovery, classification, storage rollback/pruning and
no source-file polling during DPS decay. The native `combat-overlay` scenario
uses the production thumbnail and graphics renderer on a private desktop with
stubbed DWM/game services. It verifies production-identical values, colours,
expiry, unchanged asset reuse and preserved focus/DWM counts. Service tests check
that the overview temporarily includes simulation, the on-disk store excludes it,
and real concurrent damage survives Stop/expiry. SVG pixel
checks cover custom colours at 14px and clipping without row wrapping.

The opt-in [live harness](../../tests/Preview.RenderingSmoke/README.md) also passed
`--combat-simulation --live --renderer native --count 3 --seconds 12 --capture`.
It received 35 synthetic events through production dispatch, showed real detected
system names, restored totals and retained zero simulation entries on disk. The
same foreground HWND and three DWM relationships survived the run. Captures were
inspected. Its settings/database are isolated from the running application.

Static data checks also cover full-dataset retention, compressed block lookups,
failed/cancelled update restoration and offline reuse. The complete live FC
build download and a year-to-date log corpus passed through the production service
and parser; see [static data](static-data.md) for commands and coverage limits.
The native run verifies title colour and all four incoming alpha icons; UI renders
verify the moved indicator settings and static-data progress/cancellation.

The inline-alpha update passed 29 combat checks, 13 native renderer checks,
184 workspace renders and the portable renderer smoke. Pixel comparisons cover
stable outgoing-row placement with incoming hidden, at both top and bottom edges.
A subsequent two-client live harness run received 18 events and verified title
colour, inline alpha icons, simulation cleanup and unchanged focus/DWM counts.
It used isolated preferences and history, with zero simulation entries persisted.

These checks do not establish live EVE combat notification latency, every client
language, arbitrary custom overview labels or network-filesystem notification
reliability. Cached writes can delay Windows change notifications; the reader
does not flush EVE's files. No game commands, Robin changes, signing or Cake
packaging ran for this feature. Existing NuGet vulnerability-feed availability,
DPI, Costura and test cancellation-analyzer warnings remain separate from test
success. See [Augments research and limits](combat-logs.md).

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

For preview-renderer changes, run the native overlay cases with the existing suite and the portable overlay smoke separately:

```powershell
dotnet test .\tests\Eve-O-Preview.Tests\Eve-O-Preview.Tests.csproj -c Debug --filter "FullyQualifiedName~NativeOverlayRenderingTests" -p:UsedAvaloniaProducts=
dotnet run --project .\tests\Eve-O-Preview.Preview.Smoke\Eve-O-Preview.Preview.Smoke.csproj -p:UsedAvaloniaProducts= -- --output .\bin\portable-preview-smoke
```

The native checks cover retained uploads/commits, isolated alert state, clipped asset sizing, resource disposal and compatibility fallback. They do not reproduce a physical GPU reset. Use the [rendering guide](preview-rendering.md) for the implemented architecture and measured desktop evidence. When another app is running from the normal output directory, pass an absolute isolated `-p:OutputPath=` under `src/bin` to builds/tests; do not overwrite its assemblies.

Character identity checks use synthetic launch claims and stub HTTP responses:
`--filter "FullyQualifiedName~CharacterIdentityTests|FullyQualifiedName~CharacterPortraitCacheTests"`.
The UI smoke runner also writes `portraits-*.png` for modern client and cycle-order
rows, checking delayed identity lookup, stable geometry and RTL behavior. See
[the identity guide](character-identities.md) for the live-check security boundary.

Build the UI independently when changing portable controls, theme tokens or the workspace contract. The host build remains necessary for Windows adapter and integration changes:

```powershell
dotnet build .\Eve-O-Preview.UI\Eve-O-Preview.UI.csproj -c Debug
dotnet run --project .\tests\Eve-O-Preview.UI.Smoke\Eve-O-Preview.UI.Smoke.csproj -- --output .\bin\ui-review
```

The UI and host currently pin Avalonia packages to 11.3.20. Restore the project references together when changing those versions. The standalone UI project is a library, so building it does not create a launchable desktop app. Launch the ordinary Windows host for an integrated run; the smoke executable is an isolated rendering and interaction check.

Release 10.1.0.15 aligns the main application's `Version`, `AssemblyVersion` and `FileVersion` with the UI and Robin project `Version` values (their assembly/file versions are SDK-generated). Keep all three projects aligned on future release bumps. Cake publishes these project files directly; there is no separate hard-coded release version in its tasks. Verify built managed assembly/file metadata and Robin's `GetAssemblyVersion` MSBuild target when checking a version-only change; this does not validate native injection.

Shutdown-fix validation (2026-09-13): the Debug Windows suite passed 172/172, including eight session-message scenarios and two blocked log-write/disposal cases. The version-bumped Release app build passed; built app/UI assembly and file versions and Robin's `GetAssemblyVersion` properties were verified as 10.1.0.15. Isolated output directories avoided overwriting a running app. Existing DPI/Fody/analyzer warnings remain; the Release restore also reported unavailable NuGet vulnerability data. No Cake packaging, native publish or actual machine shutdown was run.

The smoke runner uses the production workspace controls with sample state and an in-memory backend, writes page/theme images under the requested output directory, and checks control bounds and command routing. Its snapshots can reveal clipping, missing fields and theme regressions, but they do not verify actual profile persistence, WinForms embedding, native hotkeys, tray behavior, game previews, injection or Linux integration. Keep its generated images out of unrelated source changes. Check the runner source for the current scenarios and consult the recorded validation result before claiming they passed.

The Windows suite also includes `WorkspacePreferencesTests` for the generic global settings file and profile accent behavior, `WorkspaceBackendTests` for the production adapter and asynchronous commit/native routes, and `WorkspaceHostTests` for a private-desktop embedded WinForms/Avalonia host lifecycle. These are separate from the sample backend used by the portable renderer. Use the ordinary focused `dotnet test` command above, or a `FullyQualifiedName~Workspace` filter when investigating only this layer.

`WorkspaceLocalizationTests` checks every embedded language catalog for matching
keys, nonempty translations and preserved format arguments, along with regional
fallback, culture isolation and atomic language persistence. The smoke harness
exercises the language selector for all bundled languages, localized search,
retained font drafts and raw character names, profile-independent selection and
Legacy isolation. It captures all language selectors plus representative German,
Arabic, Japanese and Hindi pages at compact sizes. These checks do not establish
native-speaker translation quality. In restricted build environments, pass
`-p:UsedAvaloniaProducts=` to disable Avalonia's build statistics task if its
per-user log directory is unavailable.

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
| Windows shutdown | WindowsShutdownTests sends private-desktop query/end-session messages through production forms, presenter and stop handler. Covers tray on/off, cancelled shutdown/sign-out, busy/draft states, stalled saves/native cleanup and overlapping Exit. LoggingResponsivenessTests also bounds blocked diagnostic writes/disposal. No actual machine shutdown or live injection occurs. |

These tests do not inject Robin or prove real desktop/GPU/audio performance. The optional [native smoke driver](../../tests/Robin.NativeSmoke/README.md) publishes/loads the real AOT DLL through production injection, then checks actual DXGI pacing and synthetic audio/pipe/lifecycle boundaries. Its allocation measurement compiles the same source as managed code and is explicitly separate from native timing. See [recorded results and remaining checks](reported-bugs.md).

## Manual Mock integration

[MainWindow.xaml](../../Eve-O-Mock/MainWindow.xaml) enables swap-chain rendering with FXAA/MSAA disabled. [MainWindow.xaml.cs](../../Eve-O-Mock/MainWindow.xaml.cs) creates a rotating cube with randomized material, background, and window-title suffix, and disposes the effects manager on close. The rotation deliberately makes stalled/frozen previews visible; this small rendering loop is a manual fixture, not a performance model for EVE.

The assembly is intentionally named `ExeFile`: [ProcessMonitor](../../Eve-O-Preview/Services/Implementation/ProcessMonitor.cs) matches that process name case-insensitively. A `Mock...` title still produces a monitored process because detection uses the process name. Renaming the assembly without adjusting the fixture contract breaks discovery. Multiple instances give distinguishable windows for order, title, geometry, and movement checks.

For a relevant manual regression, record the application/Robin build, render mode, refresh period, number of clients, OS, GPU, display/DPI layout, and exact sequence. Check a normal state, the regression trigger, and the recovery/disabled state. Mock checks can validate process discovery and animated preview behavior; they do not emulate EVE's audio module or establish EVE-specific injection compatibility.

## Packaging, resources, and operational paths

The [Fody configuration](../../Eve-O-Preview/FodyWeavers.xml) enables Costura and excludes embedded debug symbols, `Eve-O-Preview.UI` and `Eve-O-Preview.Preview`. These libraries stay copy-local for incremental development builds and is bundled by the SDK for single-file publishing. An old loose UI DLL previously shadowed its newer embedded copy, causing an Autofac-wrapped `TypeLoadException`. [FodyWeavers.xsd](../../Eve-O-Preview/FodyWeavers.xsd) is generated schema; edit the XML/project configuration when changing weaving, then let the schema regenerate. The app project copies the license, verbose launcher, and public root certificate; all three are excluded from single-file extraction so release tooling can distribute them separately. The native Robin DLL is produced separately. `AboutBox.cs`, its designer, and its resources remain tracked but are explicitly excluded from the app project.

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

### Augments review validation, 2026-09-14

- All 226 Windows tests passed, including older preference defaults, SQLite version-one migration/backfill without duplicate counters, native flash rendering and shared title/DPS placement. Fade-only updates do not upload new native text surfaces. The compatibility renderer retains the blended title colour while editing fonts.
- The UI smoke harness covers 272 renders across all 18 languages, modern-theme Augments, title-position Apply commands, per-client/shared settings, colour/font pickers, constrained layouts, and missing-static-data confirmation/decline behavior. The compact title sample follows the selected position. Legacy receives no new controls.
- The opt-in [corpus audit](../../tests/StaticData.Smoke/README.md) replayed 2,980 game logs (382,519 entries) and 1,439 Local logs from the available preceding-year corpus. Incoming/outgoing totals matched, duplicate delivery did not inflate them, and temporary simulation state stayed isolated. Of 39,491 positive incoming hits, 37,995 had identifiable damage types; hidden player ammunition remains an evidence limitation. One damage-looking line without a listener was safely excluded.
- The installed complete static database supplied 112 standard T2 weapons, 299 standard/T2 ammunition choices, 5,223 NPCs and 18 factions. All 11,721 generated simulation cases round-tripped through the production log parser with no metadata differences. These counts describe that database build, not a permanent catalogue size.
- The [native live harness](../../tests/Preview.RenderingSmoke/README.md) passed on two EVE clients: shared title/thumbnail fade phases, three matching title/DPS positions, reordered rows, repairs, icons and font changes. It delivered 53 synthetic events through the normal path, retained two DWM registrations and unchanged foreground, and saved no simulated history. The portable rendering harness also covered all nine positions and five fade intensities.
- All 13 README files were reviewed; documentation links and whitespace checks passed. The normal Debug build passed `--validate-workspace` and reopened successfully. These checks did not sign/package a release or establish unrelated injection, audio, HDR or multi-monitor behavior.

These are concrete release-workflow side effects and assumptions, not requirements for ordinary source edits. No tracked CI workflow, standalone tool script, or source under `tools` was present at the review. Ignored downloads and prior binary outputs are not a substitute for current source or a verified release build.

### Simulator grouping, repair rates and scoped reset follow-up, 2026-09-14

- The focused Windows selection passed 158 tests: combat/activity, repair rates,
  reset transaction/restart/replay behavior, static data, layout, workspace and
  localization. Repair-only rates expire on the service timer without new logs.
- The full local SDE audit checked 319 standard T1/T2 weapons and 303 ammunition
  choices, with 14,901 parser round-trips and zero differences. Live item
  identification remains unrestricted by these simulator filters.
- All 384 UI render checks passed, including keyboard skipping of disabled
  platform headers, multi-source repair
  previews, all four reset scopes and cancellation. Portable rendering also
  passed its retained-geometry, expiry and 32-symbol size checks.
- The two-client native check delivered 78 synthetic events, verified compact
  three-type rates in both directions, retained two DWM registrations and the
  same foreground handle, and persisted zero simulated events. Real totals
  remained unchanged. Captures were reviewed in Light/Dark and on live DWM images.
- The isolated Debug app build and `--validate-workspace` passed. The closed app
  was updated with 72 verified files, unchanged settings, and a backup; the
  installed workspace validation passed and the reopened process was responding
  with its normal window. Existing DPI
  and Costura build warnings remain; no Cake, signing or Robin changes were used.

### Repair cycling, complete previews and settings search, 2026-09-14

- The focused Windows selection passed 161 tests, covering repair rates and
  single/combined expiry, complete appearance samples, combat overlays, static
  data, workspace and localization. All 332 UI render checks passed, including
  direct Augments section navigation from English and translated search terms.
- Portable rendering passed the 32 weapon and three repair SVG checks at 12, 16
  and 24 pixels, plus retained geometry and alert expiry. Armour uses a left-facing
  helmet; hull has three filled faces with narrow seams; shield keeps its rim.
- The two-client native harness delivered 120 synthetic events and verified
  single/combined/single repair rows with IN/OUT prefixes, revised icons, and
  synchronized fade strengths. It retained both DWM registrations and the same
  foreground handle, left real totals unchanged, and persisted no simulation events.
- The isolated Debug application build and `--validate-workspace` passed. Existing
  DPI and Costura warnings remain; the portable build could not refresh NuGet
  vulnerability metadata. No Cake, signing or Robin changes were used.
- After user closure, 72 installed files were backed up and replaced with matching
  hashes. Settings stayed unchanged, installed `--validate-workspace` passed, and
  the reopened application was responding with its normal main window.

### Known weapon icons with absent ammunition, 2026-09-14

- Recent live module-only hits already retained their correct weapon platform;
  the unknown marker represented missing ammunition damage metadata. The shared
  formatter now draws only established damage components and enabled weapon icons,
  with no automatic placeholder for missing facts. Stored metadata is unchanged.
- All 108 focused combat/static-data/native-overlay/layout tests passed. The
  installed and embedded catalog audit checked 12,913 aliases in both directions
  (51,652 entries), including 23,000 module-only cases and all 32 platforms.
  The real-log replay passed over 2,980 files and 382,605 parsed entries with
  duplicate-delivery checks and unchanged replay totals.
- Portable icon renders passed at 12, 16 and 24 pixels. All 332 workspace UI
  checks passed, including weapon platform, ammunition/ammo, damage type and
  unknown search terms opening Thumbnail augments.
- Two live EVE thumbnails passed 66 platform/damage combinations through the
  normal service/manager/native-renderer path. Both DWM registrations and foreground
  were retained; no simulated events were persisted. Real combat continued during
  the check, so live totals legitimately increased rather than remaining fixed.
- The isolated and installed workspace validations passed. Installation backed up
  and verified 72 files, preserved settings, and reopened a responsive main window.
  Existing DPI/Costura/analyzer warnings and unavailable NuGet vulnerability
  metadata remain. No Cake, signing, classifier/index migration or Robin changes ran.

### Augments sidebar placement, 2026-09-14

The modern sidebar places Augments after Previews & layout under WORKSPACE with
the existing bar-chart icon. All 332 UI checks passed, including navigation,
settings search and Legacy isolation; Light/Dark captures were inspected. The
application build and isolated/installed workspace validations passed. Installation
verified 72 files with backups and unchanged settings, then reopened a responsive
main window. This navigation-only change did not require live combat validation.
