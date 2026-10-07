# Avalonia desktop migration

## Acceptance status

Production cutover is implemented and packaged for local review. Full release
acceptance remains open: native-host GPU allocation increased, one maintenance
batch and one live foreground interval need investigation, and sustained
resource/input-latency validation has not been completed. The baseline is commit `1b2b15a`
on `AvaloniaUI`, with a clean working tree at the start. Avalonia remains pinned to
11.3.20; the host targets .NET 10 on Windows. The separate .NET Framework WPF mock
application is outside the production UI migration.

## Ownership and dependency order

| Area | Owner | Replacement and retained boundary |
| --- | --- | --- |
| Program, workspace, tray, dialogs, presenter lifecycle | Lifetime worker | Avalonia classic desktop lifetime and workspace window; existing backend/settings contracts |
| Global keyboard and pointer input, parsed shortcut values | Lifetime worker | Native Windows hooks/message window behind input interfaces; invariant shortcut strings |
| Thumbnail window, gestures, menus, geometry, snapping | Thumbnail worker | Avalonia top-level host with physical client-pixel geometry; native nonactivation adapter |
| Overlay hosts, static sink, native graphics | Rendering worker | Avalonia-owned hosts, persistent DWM sessions and retained DirectComposition; GDI+ changed-asset rasterization |
| Manager dispatch/timers, shared menu styling, project/dependency cutover | Lead | Avalonia dispatcher priorities, coalesced updates, production dependency audit |
| Existing test migration, harnesses, packaging, integration review | Lead | Existing xUnit/private-desktop infrastructure and standalone publish |

Workers own non-overlapping files. Shared contracts are agreed before integration.
The native Avalonia/DWM/DirectComposition prototype is a prerequisite for broad
thumbnail integration. Native timing and live-input checks run serially without
concurrent builds. Source HWND, destination HWND and process identity stay distinct.

## Production dependency inventory

| Former dependency | Replacement | State |
| --- | --- | --- |
| `Application.Run`, `ApplicationContext`, `WorkspaceForm`, embedded `WorkspaceAvaloniaHost` | Avalonia classic desktop lifetime and `WorkspaceWindow` | Implemented |
| `NotifyIcon`, Forms tray menus | Avalonia tray icon and native menu | Implemented |
| Forms message/font/color/client dialogs | Avalonia dialogs and existing workspace editors | Implemented |
| `ThumbnailView : Form`, designer/resources | Avalonia thumbnail window, native HWND enforcement | Implemented |
| `ThumbnailOverlay`, compatibility tint Forms | Avalonia-owned overlay hosts, native compositor adapter | Implemented |
| Static `PictureBox`, `OutlinedLabel` | Avalonia static sink; shared native scene rasterizer | Implemented |
| Forms menus/renderers | Avalonia context menus using existing shared palettes/order | Implemented |
| MouseKeyHook and Forms keyboard/mouse types | Native pointer adapter and toolkit-independent shortcut values | Implemented |
| WPF dispatcher/timers | Avalonia UI dispatcher and timers | Implemented |
| `UseWindowsForms`, `UseWPF`, desktop targets, Avalonia interoperability package | Removed from shipping project | Verified resolved graph/runtime |
| Transitive runtime/publish dependencies | Retired packages absent from resolved graph and running standalone executable | Verified |
| Original `MainForm` and reference controls | Isolated test-only parity fixtures under tests/Eve-O-Preview.Tests/LegacyReference; never shipped | Moved |
| Excluded historical `AboutBox` files, separate WPF mock | Remain excluded from production | Unchanged |

`System.Drawing.Common` remains justified for Windows static capture and
changed-scene glyph rasterization. It does not provide the application UI or loop.
Robin and its loading/pipe ABI are preserved.

Remaining Forms references are confined to the test project and synthetic
source/input harnesses. `tests/Eve-O-Preview.Tests/LegacyReference` retains the original MainForm,
client-name dialog, OutlinedLabel and menu renderers for independent comparisons.
The excluded AboutBox source/resources are historical. ResX reader/writer names
in resource metadata are build-time descriptors, not runtime UI dependencies.

## Feature and validation matrix

| Area | Required coverage | Current evidence |
| --- | --- | --- |
| Startup/lifetime | Mutex, profile resolution, tray start/restore/Exit, bounded teardown, cancelled/confirmed session end | Migrated focused regression checks passed |
| Workspace | Light/Dark/Legacy, localization, drafts, profiles, dialogs, fonts/icons/assets | 380 workspace renders across Light/Dark/Legacy and all 18 languages passed; dialog captures inspected |
| Input | Both hotkey modes, serialized shortcuts, held modifiers, capture/suppression, pointer subscription cleanup | Final 472-test suite passed, including native keyboard lifecycle, full legacy key grammar and bounded pointer dispatch |
| Thumbnail geometry | Native client versus outer pixels, frame toggles, first show, limits, hover, signed coordinates, save/reload | Final suite passed; two real 100%/125% DPI runs each passed 397 checks |
| Snapping | Realtime edge alignment, Shift bypass, breakaway hysteresis, visual guides | Portable 8-DIP acquire/16-DIP release hysteresis, Shift bypass and nonactivating guides; pure and native gesture tests passed |
| Rendering | Persistent DWM, static last-valid frame, title/stats/markers/active frame/alerts, fallback/device recovery | Prototype and permanent actual-compositor proof passed; focused fallback/device-recovery tests passed |
| Visibility/focus | Nonactivation, ownership, MRU/topmost, external hide/minimize/demotion recovery, open menu protection | Final native window regressions and isolated compositor/lifetime checks passed |
| Performance | Comparable idle/alerts/many previews/switching/hover and sustained resource sampling | Matched 30-second native 2-idle and 12-alert runs completed; CPU close, memory/GPU allocation higher, one unexplained 68 ms batch; sustained and latency gates remain open |
| Packaging | Fresh standalone Windows publish, bundled resources/SQLite, independent startup/settings/preview/exit | Framework-dependent single executable passed 17 desktop checks and workspace validation from an otherwise empty folder; fresh Robin companion passed controlled-child native smoke |
| Environment-dependent gates | Real monitor crossings and authorized live-client checks | Real 100%/125% crossings passed twice; five-client rendering passed; two-client gestures/activation passed with one unresolved foreground-stability interval. Driver loss and session end are injected/message tests, not physical driver reset or OS shutdown. |

## Reproducible baseline

From `src`, the serial baseline suite passed **457/457**, with no skipped cases:

```powershell
dotnet test tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj -c Debug -p:UsedAvaloniaProducts= -p:OutputPath=C:\dev\eve-o-preview\src\bin\avalonia-migration\baseline\tests\ --logger 'trx;LogFileName=baseline.trx' --results-directory C:\dev\eve-o-preview\src\bin\avalonia-migration\baseline\results -- xUnit.ParallelizeTestCollections=false
```

Baseline binaries/results are retained under ignored `src/bin/avalonia-migration/baseline`.
SDK 10.0.103, Windows 10.0.26200, x64. Existing Forms DPI-manifest, Costura
PrivateAssets and xUnit cancellation warnings are baseline warnings. Passing tests
does not resolve the live-validation gaps in [reported bugs](reported-bugs.md).


## Current native and visual evidence

- The final integrated suite passed **472/472**, with no skipped cases, in 1m44s.
  VSTest independently discovered all 472 tests. The original baseline passed
  457/457. Native assertions were retained; additional regressions cover new
  host boundaries and defects found during integration.
- The actual compositor prototype established an Avalonia destination HWND for
  DWM and a separate owned, nonactivating Avalonia overlay HWND for DirectComposition.
  No live-image bitmap upload is introduced. The permanent --host-proof passed
  23 compositor checks plus 13 source-lifetime checks. It checks
  source pixels, whole-window opacity, tint/frame/title order, retained HWNDs,
  registration count and foreground preservation.
  Its production manager fixture also covers source minimize/restore, closure
  and same-title replacement with a different HWND and image. Discovery is an
  explicit fixture seam; owned native windows and DWM are real.
- At 50% opacity, source FF0F5AB4 over the owned black fixture produced FF082D5A.
  Full red tint left all four configured lime edges and 338/338 opaque title
  pixels intact. An existing DirectComposition child insertion-order error was
  corrected; this is independently guarded by composed pixels.
- Actual --mixed-dpi validation passed twice in fresh processes: 397 assertions
  and 42 stages per run, using real 96/120-DPI monitors including negative X/Y.
  Framed and borderless 337x191 client pixels survived crossings, hover zoom,
  saved-geometry JSON reload and recreated hosts. Fourteen DWM relationships per
  run (including recreated hosts) had zero failed updates. No gameplay input or
  display-setting changes were made.
- The workspace smoke passed 380 renders, all three themes and all 18 languages.
  The portable graphics smoke passed fixed platform/repair SVGs, retained scenes,
  title styles, animation expiry and clipping. Native dialog/font/color captures
  were inspected. These headless renders are separate from interactive DWM evidence.
- Both native hotkey modes, full legacy shortcut value/string grammar, bounded
  pointer dispatch and zero-allocation motion ingestion passed focused tests.
  The session-end regression uses the actual manager with deliberately blocked
  keyboard cleanup and proves that saving/client reset start within its budget.
- The framework-dependent Release executable, copied alone into a fresh folder,
  passed all 17 --validate-desktop checks and --validate-workspace (both exit 0).
  This exercises all three workspace themes, embedded assets, native SQLite,
  actual DWM/static hosts, DirectComposition and teardown. Its runtime contract
  is Microsoft.NETCore.App 10.0; no Windows Desktop runtime is required.
- A fresh unchanged-source NativeAOT Robin build (10.1.0.23) passed controlled-child
  DXGI/audio, pipe recovery, owner exit and shutdown checks. Eight 1 FPS wake
  observations ranged from 0.53 to 15.81 ms. Warm frame/audio lookup allocation
  was zero. This is synthetic audio evidence, not live EVE audio verification.
- Five authorized live clients passed native rendering with alerts and 2x zoom:
  five persistent registrations, 159 updates, zero failures and zero preview
  focus captures. A two-client run passed all 14 mouse/menu/hover/move/resize
  checks and four requested source activations. One alert interval changed the
  foreground unexpectedly, so its overall active-highlight result remains failed.
  The cause is unassigned; zero preview focus captures does not clear that failure.

Ignored evidence lives under src/bin/avalonia-migration, including baseline,
integrated, native-host-proof, mixed-dpi, mixed-dpi-repeat, ui-smoke and
portable-preview. Isolated focused worker output also lives under
src/bin/avalonia-thumbnail-validation and src/bin/avalonia-lifetime-validation.

## Reproduction commands and artifacts

Run from `src`, with native/input tests serialized and no competing builds:

```powershell
$evidence = 'C:\dev\eve-o-preview\src\bin\avalonia-migration'
dotnet test tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj -c Debug -p:UsedAvaloniaProducts= -nodeReuse:false "-p:OutputPath=$evidence\integrated\tests\" --logger 'trx;LogFileName=final.trx' --results-directory "$evidence\integrated\results" -- xUnit.ParallelizeTestCollections=false
dotnet test tests/Eve-O-Preview.Tests/Eve-O-Preview.Tests.csproj -c Debug --no-build --list-tests "-p:OutputPath=$evidence\integrated\tests\"
dotnet run --project tests/Eve-O-Preview.UI.Smoke/Eve-O-Preview.UI.Smoke.csproj -c Debug -p:UsedAvaloniaProducts= -- --output bin/avalonia-migration/ui-smoke
dotnet run --project tests/Preview.Smoke/Preview.Smoke.csproj -c Debug -p:UsedAvaloniaProducts= -- --output bin/avalonia-migration/portable-preview
dotnet build tests/Preview.RenderingSmoke/Preview.RenderingSmoke.csproj -c Debug -p:UsedAvaloniaProducts= "-p:OutputPath=$evidence\migrated-renderer\"
& "$evidence\migrated-renderer\Preview.RenderingSmoke.exe" --host-proof --output "$evidence\native-host-proof"
& "$evidence\migrated-renderer\Preview.RenderingSmoke.exe" --mixed-dpi --output "$evidence\mixed-dpi"
dotnet publish Eve-O-Preview/Eve-O-Preview.csproj -c Release -r win-x64 --self-contained false -p:SelfContained=false -p:PublishSelfContained=false -p:UsedAvaloniaProducts= -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -nodeReuse:false "-p:OutputPath=$evidence\release-final\" -o bin/avalonia-migration/publish-final
```

Copy only the executable into a fresh directory for the bundle check; then use
`Start-Process -Wait -PassThru -WindowStyle Hidden` with `--validate-desktop
--validation-output <absolute-directory>` and, separately, `--validate-workspace`.
PowerShell does not wait for a WinExe invoked with `&`; inspect each process's
ExitCode. Set both self-contained properties explicitly and inspect the generated
runtimeconfig: the framework entry must be `Microsoft.NETCore.App`, not
`includedFrameworks` or `Microsoft.WindowsDesktop.App`.

The native companion uses the x64 Visual Studio developer environment and existing
[Robin smoke procedure](../../tests/Robin.NativeSmoke/README.md). The fresh command
was `dotnet publish Eve-O-Preview.Robin/Eve-O-Preview.Robin.csproj -c Release -r
win-x64 -p:IlcUseEnvironmentalTools=true
-p:OutputPath=C:/dev/eve-o-preview/src/bin/avalonia-migration/robin-build/
-p:IntermediateOutputPath=C:/dev/eve-o-preview/src/bin/avalonia-migration/robin-obj/
-o bin/avalonia-migration/robin-native`; the native driver used that DLL and the
owned `tests/Robin.NativeSmoke/bin/native/probe.exe`.

| Evidence | Location below `src/bin/avalonia-migration` |
| --- | --- |
| Final suite and discovery | `integrated/results/final.trx`, `final-suite.log`, `discovery.log` |
| Workspace and portable graphics | `ui-smoke`, `portable-preview` |
| Composed pixels and source lifetime | `native-host-proof/host-proof.json` and cropped PNGs |
| Physical monitor transitions | `mixed-dpi/mixed-dpi.json`, `mixed-dpi-repeat/mixed-dpi.json` |
| Standalone executable | `publish-final/EVE-O Preview.exe`, isolated copy in `standalone-final` |
| Bundled-resource/native-host checks | `published-final-desktop/desktop-validation.json` and three theme PNGs |
| Fresh native companion | `robin-native/Eve-O-Preview.Robin.dll`, `robin-native-publish.log`, `robin-native-smoke.log` |
| Authorized live checks | `live-five-native/result.json`, `live-two-input/result.json` |

## Matched performance comparison

The preserved WinForms binaries and migrated binaries used the same Avalonia
initialization, dispatcher pumping, owned source windows, dimensions, monitor,
renderer and workload. Each sample lasted 30 seconds with 58 maintenance batches;
the idle pair was repeated in reverse order. CPU is a percentage of one core.

| Workload | Baseline CPU | Avalonia CPU | Baseline batch p50 / p95 / p99, ms | Avalonia batch p50 / p95 / p99, ms |
| --- | ---: | ---: | --- | --- |
| 2 idle previews | 10.83% | 12.18% | 0.54 / 1.50 / 1.85 | 0.64 / 1.39 / 10.33 |
| 2 idle, reverse-order repeat | 10.15% | 9.89% | 0.55 / 1.21 / 1.57 | 0.57 / 2.17 / 8.26 |
| 12 previews, alerts and 2x zoom | 10.99% | 11.55% | 1.61 / 3.49 / 4.18 | 1.74 / 3.60 / 68.36 |

The first idle pair ended at 109.34 -> 136.46 MiB private memory and 949 -> 1221
handles; the twelve-preview pair ended at 113.63 -> 175.77 MiB and 955 -> 1329
handles. Twelve-preview Avalonia handles stayed between 1328 and 1330 after five
seconds, but this short sample does not establish absence of leaks. Native DWM
registrations stayed at 2/12 respectively, with 120/724 updates and no failures.

External PDH samples measured process dedicated GPU allocation of 23.88 -> 53.77
MiB for idle and 24.01 -> 159.44 MiB for twelve alerts. The busiest DWM 3D engine
averaged 1.249 -> 1.354% and 2.098 -> 2.124% respectively; DWM covers the whole
desktop and five other live clients. Application 3D counter instances were absent,
so this is not evidence of zero application GPU work. Invalid PDH samples were
excluded by status. These are allocation/activity counters, not frame times.

Evidence is under `src/bin/avalonia-migration/performance`, with one directory per
workload containing `result.json` and `gpu.json`. Commands use `--renderer native
--count 2 --seconds 30 --effects none --zoom 1 --capture`; the twelve-preview
case uses `--count 12 --effects all --zoom 2`. The preserved comparison executable
is in `baseline/host-aware-renderer`; the migrated executable is in
`migrated-renderer`. See the [renderer guide](preview-rendering.md) for sampling
limits. Higher allocation and unexplained batch tails remain acceptance gaps;
these results do not support an unconditional no-regression claim.

## Remaining acceptance work

- Investigate the increased process GPU allocation and the 68.36 ms maintenance
  batch in the twelve-preview run. No individual view/DWM call exceeded the
  harness's 25 ms diagnostic threshold; there is no GC/ETW trace identifying
  the whole-batch delay. These measurements do not establish performance parity.
- Resolve the failed live foreground-stability interval with before/after native
  window identity and a controlled repeat. Earlier live pointer/hover checks also
  varied; the final guarded run passed those assertions. No gameplay input was sent.
- Complete the sustained resource/lifetime run, matched Compatibility performance
  pair and live keyboard latency distributions. Short maintenance timings measure
  native/host work, not input-to-visible or game-frame latency.
- Validate ordinary published Main startup against an isolated client, in addition
  to the executable's isolated desktop/workspace modes. Four existing live clients
  already have Robin loaded. Ordinary startup would claim their native ownership
  and replace settings even when injection is disabled; the protocol cannot read
  every field needed to restore that state. Their settings were left intact.
- Physical monitor transitions cover 100% and 125%. The 150%/200% cases use
  synthetic scale messages. Physical driver reset and actual Windows shutdown
  remain separate from tested device-failure and session-message paths.

## Design and review decisions

Avalonia 11.3.20 remains pinned. The application uses one classic desktop lifetime;
native hook threads have small message-only HWNDs and never run a second UI
framework. The physical-pixel adapter owns the conversion between persisted
client/outer geometry and Avalonia DIPs. Native source activation precedes
graphics updates, and healthy DWM registrations survive routine maintenance.

The hidden Avalonia Windows dispatcher HWND is intercepted for the Windows
query/end-session pair: a query accepts shutdown without saving/stopping;
only confirmed session end uses bounded, idempotent cleanup. UI timers stop on
their owner; native input and client reset can complete independently off-thread.

Snapping uses the raw pointer rectangle, nearest eligible edges and separate
axis retention. Shift releases both axes immediately, while a larger release
threshold allows deliberate breakaway without jitter. Guides are transparent,
nonactivating top-level windows. The thresholds are product choices, not OS
constants. Microsoft's [FancyZones interaction guidance](https://learn.microsoft.com/en-us/windows/powertoys/fancyzones)
informed modifier bypass and visible alignment feedback.

The Windows native arrangement follows [DWM destination-window requirements](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail).
Implementation-specific Avalonia references are pinned in the
[window guide](windows-and-thumbnails.md) and [lifetime guide](application-and-configuration.md).

Independent reviews cover lifetime/input, thumbnail/native geometry, shared
rendering and the combined production diff. Concrete review findings receive
regressions before release; the final integrated outcome remains recorded above.

## Deployment and rollback

Implementation commits are `fdeda5d` (portable snapping), `4a955f4` (production
desktop/input cutover and regression coverage), and `2a2e279` (native validation).
The navigation/report updates form a separate documentation commit.

No personal installation or profile has been changed. No release has been signed
or published. Keep the original installed build while reviewing the local package.
Configuration JSON keys, full-title identities and profile/global-preference
separation remain compatible. The local package is under
`src/bin/avalonia-migration/package/EVE-O-Preview-10.1.0.23-Avalonia-win-x64`.
The matching ZIP is `src/bin/avalonia-migration/EVE-O-Preview-10.1.0.23-Avalonia-win-x64.zip`
(18,230,796 bytes), SHA-256
`490E0D20CD8DE3D5D9CFA1121D746997FB02D67AFFB499A6887B1BF9F6D9F809`.
Archive integrity and all six payload hashes passed; `CHECKSUMS.sha256` is included.
The executable hash is `7873F40B4293BE2CF31D18D0E5BF3C1A4D1B618B946673C2EA4040CF88D176E2`;
the Robin hash is `9468C196846E3B9FC8FA1752F052F0584C773864B035E3328D16B17F838835FB`.
The validation limits above prevent declaring full end-to-end release acceptance.
The candidate is unsigned and has not been installed.
To roll back, Exit the candidate and return to the prior executable/Robin pair;
retain the same complete Profiles and global settings/database directories. No
configuration key rename, profile identity change or schema migration is introduced.
