# Windows preview renderer validation

This opt-in Windows executable exercises production Avalonia `LiveThumbnailView`
and `ThumbnailOverlay` windows with real `WindowManager` DWM thumbnails. It can use its own animated source
windows or already-running EVE clients. It does not launch the production app,
save profiles, install Robin, or send gameplay input. The default renderer checks
use isolated collaborators; the explicit modes below can read production profiles
and use the existing focus/prediction pipe. Source layout/minimize methods deliberately
throw; source activation is limited to the explicit active-highlight check.

Build from `src` with an isolated output directory when an existing app is open:

```powershell
dotnet build tests/Preview.RenderingSmoke/Preview.RenderingSmoke.csproj -c Debug -p:OutputPath=C:\dev\eve-o-preview\src\bin\preview-validation\ -p:UsedAvaloniaProducts=
```

The executable needs the interactive Windows desktop for DWM composition and
optional preview capture. A restricted or private desktop may enumerate no
clients and have a zero foreground HWND. `--list` only reports the accessible
live client HWNDs, process IDs, titles, minimized state and foreground HWND:

```powershell
& .\bin\preview-validation\Preview.RenderingSmoke.exe --list
```

The default run creates two animated source windows and two production preview
windows. Select `legacy`, `native`, or `avalonia`; all production image hosts are
Avalonia top-levels. `legacy` uses retained raster title/stat assets in an Avalonia
overlay and `native` retains the DirectComposition overlay. `avalonia` selects the
portable candidate renderer above the same production DWM host. That candidate
is a renderer comparison, separate from the production native/compatibility paths.
Native fallback to Legacy fails the run so fallback cannot masquerade as a
successful native benchmark.
WinForms remains only in synthetic source/input fixtures; both message loops are
pumped so production Avalonia layout, input and timers execute normally.

```powershell
& .\bin\preview-validation\Preview.RenderingSmoke.exe --renderer native --seconds 15 --capture --output .\bin\preview-results\mock-native
& .\bin\preview-validation\Preview.RenderingSmoke.exe --renderer avalonia --effects all --seconds 15 --capture --output .\bin\preview-results\mock-avalonia-alerts
```

`--host-proof` is a short controlled compositor regression using an owned solid
source and black backdrop. It verifies exact DWM image pixels, 50% whole-window
opacity, premultiplied tint alpha, all four active borders and opaque title glyphs
above full-intensity `DamageTint`, nonactivation, pointer-transparent overlay
styles, and retained HWND/DWM relationships after hide/show. It saves only the
owned preview rectangle and writes `host-proof.json`. Unlike the short alert
animation, full-window `DamageTint` is not clipped inside the border, so this
regression catches native sibling-order errors. These functional checks run
outside performance sampling and do not establish equivalent old/new appearance
for the previously incorrect tint/frame order.
The same proof selects Compatibility graphics and compares source, tint and title
pixels through the actual compositor, including intermediate title color without
alpha loss. Its source-lifetime phase uses controlled discovery snapshots with the
production manager: a real source minimizes/restores, exits, and reconnects under
the same title with a new HWND and distinct pixels. Removed hosts must close and
all native registrations must balance on disposal. Only owned source windows are
changed; the fixture does not claim to validate EVE process discovery itself.

```powershell
& .\bin\preview-validation\Preview.RenderingSmoke.exe --host-proof --output .\bin\preview-results\native-host
```

`--mixed-dpi` exercises real production preview transitions across the currently
configured monitors. It records native client/outer rectangles and render scale,
checks frame changes, saved physical dimensions, hover restoration and retained
native relationships. It changes only owned test windows and does not change
Windows display scaling. Distinct monitor scales are required for a mixed-DPI
claim; synthetic scale tests cover a different boundary.

`--live` uses the existing EVE HWNDs instead. Obtain authorization before live
validation. `--restore-sources` temporarily restores originally minimized sources
without activation and returns those sources to minimized state in `finally`.
Source sizes and positions are not edited. Restoration is checked before exit.
Without that option, minimized sources remain minimized and may show frozen
content. `--capture` saves only the owned preview rectangles while raising their
image/overlay pair; it does not capture an entire monitor or the desktop.

```powershell
& .\bin\preview-validation\Preview.RenderingSmoke.exe --live --restore-sources --renderer native --seconds 30 --capture --output .\bin\preview-results\live-native
& .\bin\preview-validation\Preview.RenderingSmoke.exe --live --restore-sources --renderer native --effects all --seconds 30 --capture --output .\bin\preview-results\live-native-alerts
```

`--combat-simulation` exercises the Augments service -> manager -> native renderer
with real EVE windows and shared log reads, using isolated settings and a separate
real-history database. The default target is all visible harness thumbnails.
Events follow normal aggregation and notification paths without TEST labels;
temporary overview statistics disappear on Stop/expiry while real ingestion
continues. Captures verify plain system names below titles and damage/repair
graphics. A deterministic incoming all-types event also verifies the actual title
colour, synchronised whole-thumbnail tint and all four incoming alpha icons beside
DPS on every thumbnail, including restoration after simulation ends.
Fade captures verify low, intermediate and peak strengths on the shared clock;
the fade stage uses 10% maximum thumbnail opacity and asserts its native ARGB alpha.
A six-source deterministic repair sample verifies three compact type-coloured
icon/amount pairs in each direction, combined as HP/s over the configured window.
Streaming repair checks retain normal hit-size variation. A three-second rate
window verifies single, combined, then single repair samples on both native
rows, each retaining its IN/OUT prefix.
Every platform is also tested with and without known damage composition, including
weapon-only, damage-only, combined and empty icon strips without unknown markers.
top-left, centre and bottom-right captures exercise stacked title/system and DPS
positions with reordered rows through the normal settings path.
`combat-result.json` records temporary totals, persisted-entry exclusion,
event counts, focus and DWM relationships. Choose a count no greater than the
number of real clients; this mode rejects duplicate sources.

```powershell
& .\bin\preview-validation\Preview.RenderingSmoke.exe --live --renderer native --combat-simulation --count 3 --seconds 15 --capture --output .\bin\preview-results\live-combat
```

`--effects one|all` adds neutral synthetic text/counter rows and
a one-second synthetic red flash every two seconds, without shaking,
on one or all previews. It never reads actual incoming damage or game stats. Legacy supports
only `--effects none`. `--count 1..24` repeats available sources if necessary;
24 previews of two games are not equivalent to 24 independent game clients.
The preview dimensions default to 384 by 216 native pixels and can be set with
`--width` and `--height`. A run lasts 1–600 seconds; default 15.
`--zoom 2..25` additionally exercises production hover zoom on the first preview
after timing, records enlarged geometry/resources and restores the saved bounds.
This checks the real host path without changing source client geometry. Alert
captures preserve `--effects one` targeting; `expired-*.png` follows natural
animation completion rather than an explicit clear.

`result.json` records source identities, foreground HWNDs, creation focus capture,
maintenance focus captures, native image/overlay HWNDs, persistent DWM registration
and update counts, process CPU, private/working memory and GDI/USER/process handles.
Graphics counters record native uploads/commits and scene pixel allocation, or
Avalonia scene updates/renders, before and after the timed workload.
CPU percentage is a percentage of **one logical core**; divide by the reported
logical processor count for Task Manager's approximate whole-machine convention.
The p50/p95/p99 figures time an entire maintenance call over all previews, not
end-to-end client switching or compositor presentation latency. Initial resource
samples follow a one-second warmup. Image captures and final border/raise checks
are excluded from the CPU sampling interval.
Per-preview maintenance calls exceeding 25 ms record separately timed DWM update
and remaining host time. Slow DWM calls also retain their maintenance batch index;
batch zero denotes initialization or later untimed checks. These wall-clock
durations include scheduling delays and do not prove GPU execution time.

`--effects staggered --capture --count 2` is an animation independence proof,
performed after the idle timing interval. The first preview receives a red
two-second flash; the second receives a cyan 1.8-second flash about 0.8 seconds
later. It captures both previews in first-only, overlap, second-only and fully
expired phases. Starting the second alert must not change the first renderer's
resources or transaction count. Both flashes use zero shake. Phase timestamps and graphics counters are
recorded under `StaggeredCheck`; the short idle interval is not an animation
performance benchmark.

`--renderer native --active-highlight` is an additional authorized interaction check. It registers
the existing two production views with a production `ThumbnailManager` using
isolated dependency stubs, then calls its real selection path. One guarded click
per preview exercises the normal mouse activation route: `WindowFromPoint` must
identify that preview or its overlay immediately before input, and no modifier
may be held. It never clicks a game/source window or another application. The
cursor, original foreground and originally minimized source states are restored.
The check verifies that no graphics work precedes the native focus request and that
the new border is submitted immediately afterward. It
records requested/actual foreground HWNDs, clears the previous border and checks
1-pixel GreenYellow and 5-pixel DeepSkyBlue borders through a full-intensity
stationary red flash and natural expiry. Captures and edge-pixel comparisons
confirm the configured color; persistent DWM registrations must remain intact.
Without a user gesture, Windows may reject background foreground requests; the
recorded activation result must be consulted rather than assuming they succeeded.
The short proof does not measure end-to-end switch latency.

Add `--mouse-checks` to `--active-highlight` to exercise real image/title clicks,
hover zoom, Avalonia context-menu actions, right-hold dragging, free resize and
Shift aspect-preserving resize through the production global pointer service.
The same guarded checks work with the default synthetic sources or an explicitly
authorized `--live --count 2` subset. Cursor, geometry and foreground are restored.

`--renderer native --rapid-switch` also runs that proof, then 200 production
`CycleNextClient` calls paced at 20 per second. It records entry to and return from
the native focus request, return from the complete switching call (an upper bound
on outline submission), actual foreground confirmation, missed 50 ms deadlines,
DWM image updates and surface uploads. Message-aware waits avoid fixed sleeps
masking foreground notifications. A separate 20-transition pass tests actual
foreground notifications and visible selected/cleared edge pixels. Those pixel
timings include screen readback and are not monitor scan-out measurements.
The manager's discovery interval is 60 seconds so polling cannot satisfy these
notification checks. Wake/prediction IPC and affinity collaborators remain stubs;
this is not a low-FPS Robin or combat-frame latency benchmark. With
`--live --rapid-switch --wake-existing`, the harness first requires both existing
Robin pipes to answer Ping, then uses the normal wake/prediction commands. It
does not install hooks, change FPS targets, or stop the production app's native
services. Affinity remains stubbed in this mode.

`--production-hotkeys --production-pid <PID>` tests the already-running application
without creating test thumbnails. It requires exactly two visible EVE clients,
identifiable production previews, and an F16 cycle binding containing exactly
those two titles in every local profile. Every profile must also specify enabled,
opaque Lime active borders. It reads these settings and refuses an ambiguous
binding; it never saves them. One guarded click establishes the first client,
followed by 200 F16 down/up pairs through the production global hotkey route.
Actual application wake, affinity and layout settings therefore participate.
The process must remain alive and modifiers/F16 must remain released.

```powershell
& .\bin\preview-validation\Preview.RenderingSmoke.exe --production-hotkeys --production-pid 12345 --output .\bin\preview-results\production
```

Production inputs are at least 50 ms apart, with actual intervals recorded;
missed deadlines do not cause catch-up bursts. A validation-only high-resolution
waitable timer bounds sampling waits without changing system timer resolution.
`production-result.json` separates foreground confirmation within 50 ms from
20 selected/cleared border pixel checks. A correct border can precede completion
of Windows activation; a zero foreground HWND is retained as unconfirmed, never
counted as success. Pixel timings include screen readback and do not measure
monitor scan-out. The cursor and original foreground are restored in `finally`.
This mode sends only the verified switching key, and never inputs into the game
client itself. Run it only as part of authorized live switching validation.

## Live hotkey input latency

`--live --input-latency` exercises the production `WindowsHotkeyService`,
`ThumbnailManager`, native DWM/composition views, `WindowManager` and CPU affinity
handler against every accessible EVE client (at least two). Close EVE-O Preview
first to avoid competing registrations/CPU assignments. This is an explicit live
interaction test: it sends 50 Ctrl+F16 forward cycles and 50 Ctrl+F17 backward
cycles while keeping Ctrl held, with at least 50 ms between inputs. It does not
edit user profiles or inject Robin. Existing Robin wake endpoints participate
only if every client reports a version. Add `--windows-hotkeys` to exercise
`RegisterHotKey` instead of the default dedicated global hook.

```powershell
dotnet run --project .\tests\Preview.RenderingSmoke\Preview.RenderingSmoke.csproj -c Debug -- --live --input-latency --mouse-checks --output .\bin\hotkey-validation\global
dotnet run --project .\tests\Preview.RenderingSmoke\Preview.RenderingSmoke.csproj -c Debug -- --live --input-latency --mouse-checks --windows-hotkeys --output .\bin\hotkey-validation\windows
```

`input-latency.json` records input-to-focus-request, input-to-foreground,
request-to-foreground, and input-to-processed-`WM_NULL` distributions and samples.
Foreground sampling runs independently of the app's UI at approximately 1 ms.
`WM_NULL` is a measurement after activation, never a prerequisite for production
focus; its reply confirms message processing, not an EVE gameplay action or frame
presentation. The input mode is set only on the harness's in-memory profile.
Original CPU assignments, minimized source states and foreground are restored.

The harness also sends F24 only to a guarded test window on a separate recipient
UI thread while deliberately pausing the hotkey owner's UI for 250 ms. This checks
that unrelated input is not held behind the owner's UI. The test returns failure
for a missing switch/message response or an unrelated-input delay of 100 ms or more.
The high-resolution sampling timer and deliberate pause exist only in validation.

`--mouse-checks` also sends guarded mouse input to the actual preview image,
title/overlay and menus, verifies source activation and hover zoom, and exercises
right-button hold dragging, menu-driven movement/free resizing, Shift-resizing
and mouse-up cleanup through the real shared MouseKeyHook subscriptions. It
restores preview geometry and cursor position. Only clicks targeting a verified
preview or its open menu are sent. Modifier-click and minimize-command routing,
mode changes/capture during movement, and subscription cleanup on closure are
covered separately by the private-desktop `ThumbnailMouseTests`.

Current Debug validation used five real clients, native composition and automatic
CPU affinity, with no active Robin endpoint. Both input modes completed 100/100
switches while Ctrl stayed held, and passed all 14 live mouse checks. Global
input-to-request median/p95 was 0.29/1.04 ms; input-to-foreground median/p95 was
15.36/30.93 ms (maximum 32.38 ms). All clients answered the post-focus message
within 39.24 ms of input. Unrelated input arrived in 7.03 ms during the 250 ms UI
pause. Windows mode's input-to-request median/p95 was 1.18/2.55 ms and foreground
median/p95 was 16.91/33.35 ms. These are observed runs, not universal latency
guarantees or a long-session CPU benchmark; low-FPS Robin wake behavior was not
exercised.

### Diagnostic passthrough and trigger timing

`--diagnostic-input` creates two guarded test windows and sends F24 only while
one owns foreground. It uses the production Windows input service to check
key-down/key-up triggers, suppression on/off, original-recipient delivery before
the diagnostic focus switch, recording suppression, and restoration after
capture or disabling diagnostics. It restores foreground and releases the test
key on exit. It does not send keys to EVE or change saved preferences.

```powershell
dotnet run --project .\tests\Preview.RenderingSmoke\Preview.RenderingSmoke.csproj -c Debug -- --diagnostic-input --output .\bin\hotkey-validation\diagnostic-input
```

The native check passed all 33 assertions and writes `diagnostic-input.json`.
It caught and guards against using Send-priority dispatch for passthrough: that
can focus the destination before the original key is delivered. Diagnostic
actions now yield to pending UI input; normal hotkeys retain their immediate
dispatch path. These guarded Windows recipients establish event delivery, not
EVE's gameplay handling or a cross-process acknowledgement. The portable UI
smoke separately checks neutral labels, both trigger choices, ten-cycle unlock,
idle-gap reset, hidden search, mode availability and Legacy isolation in Light
and Dark.

For external GPU sampling, start a sufficiently long harness run on the
interactive desktop, then collect performance counters in a separate PowerShell:

```powershell
$previewProcessIds = @((Get-Process Preview.RenderingSmoke).Id) + @((Get-Process exefile).Id) + @((Get-Process dwm).Id)
& .\tests\Preview.RenderingSmoke\collect-gpu.ps1 -ProcessIds $previewProcessIds -Samples 15 -Output .\bin\preview-results\gpu.json
```

The optional helper stores only the supplied process IDs, their GPU engine and
dedicated/shared allocation counters, and matching DWM process CPU. Individual
engines and adapters remain separate; do not add their percentages into a claimed
total GPU utilization. Invalid PDH samples are retained with status codes and
excluded from the means. These English counter paths depend on Windows' counter
availability. DWM includes the entire desktop, so its activity is not solely the
cost of these overlays. Counter startup can take several seconds; allow the
sampler to finish before the harness exits. Sampling runs outside the preview
process and does not install instrumentation or hooks into a game.

Creation focus capture is recorded separately and the original foreground is
restored before timing if the harness captured it. A nonzero input-desktop
foreground is required for a live run. Maintenance and border/native raising
fail if they capture foreground. Source restoration and window disposal occur
even after failure. Regular healthy maintenance must not replace DWM sessions,
and native image/overlay visibility and relative stacking must remain correct.

Compare equivalent builds, preview sizes/counts, sources and effect workloads.
Keep other heavy work out of the timed interval. Startup/runtime initialization
differs: this isolated harness initializes Avalonia only for the Avalonia
candidate, whereas the normal application already initializes Avalonia for its
settings workspace. Therefore standalone memory differences are not a direct
measurement of incremental memory in the complete application.

The main executable alone does not measure GPU allocation/utilization; use the
optional counter helper for those. Neither tool measures source frame times,
capture age, actual combat alert latency or native Robin hooking/audio. The
opt-in input test measures Windows foreground/message timing, not the time to
the next game frame or gameplay response.
DWM process counters can be inaccessible even on the interactive desktop. The
automated private-desktop suite remains necessary for configured hiding, native
menus, hover/zoom, MRU cycling and compatibility capture behavior. Multi-monitor
DPI, HDR, independent large client counts, Linux/Wine and hardware diversity
remain separate validation environments.
