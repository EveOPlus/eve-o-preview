# Preview presentation and overlay rendering

This guide describes the Windows-first implementation introduced after the [technology review](preview-rendering-review.md). Windows keeps DWM live images and supports an independent native graphics overlay. The portable Avalonia renderer is a comparison/future-platform implementation; it is not used for production Windows thumbnails.

## Ownership and portable boundaries

- [Eve-O-Preview.Preview](../../Eve-O-Preview.Preview/OverlayContract.cs) contains toolkit-independent scenes, finite alerts and renderer capabilities. [IPreviewBackend/IPreviewSession](../../Eve-O-Preview.Preview/PreviewContract.cs) represent image presentation without requiring captured frames.
- [WindowsDwmPreviewBackend](../../Eve-O-Preview/View/Rendering/WindowsDwmPreviewBackend.cs) owns persistent DWM relationships. Bounds changes update existing properties; maintenance retains healthy registrations and populates replacements before releasing obsolete ones. Its source/destination HWND resolver belongs entirely to Windows.
- [WindowsStaticPreviewBackend](../../Eve-O-Preview/View/Rendering/WindowsStaticPreviewBackend.cs) owns the compatibility bitmap and input-transparent image control. Forced maintenance captures through the existing `WindowManager.GetStaticThumbnail`; failed captures retain the last valid frame.
- [LiveThumbnailView](../../Eve-O-Preview/View/Implementation/LiveThumbnailView.cs) and [StaticThumbnailView](../../Eve-O-Preview/View/Implementation/StaticThumbnailView.cs) adapt the established Windows manager/view lifecycle to those sessions. Discovery, cycling, CPU affinity, Robin and the named-pipe protocol remain on their existing routes.
- [ThumbnailView](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs) continues owning preview geometry, input, menus, hover zoom and nonactivating MRU restoration. Changing its overlay renderer replaces only the overlay window; it does not replace its DWM session.

`PreviewClientId` is ephemeral adapter identity. Existing full window titles remain profile/layout/cycle keys. The new contracts do not make the Windows manager or application lifetime portable; a Linux host still needs its own discovery, window control, shortcuts and capture integration.

## Windows graphics path

[NativeCompositionOverlayRenderer](../../Eve-O-Preview/View/Rendering/NativeCompositionOverlayRenderer.cs) shares one hardware D3D11/DirectComposition device among overlays on the UI thread. It retains graphics surfaces and submits finite compositor animations. It has no application animation timer, screenshot loop, game-frame readback or new IPC. Initialization deliberately does not fall back to a software GPU device.

Text/marker/stat assets come from [OverlaySceneRasterizer](../../Eve-O-Preview/View/Rendering/OverlaySceneRasterizer.cs). They are CPU-rasterized only when their scene changes, then retained for GPU composition. Title/marker and stat assets are cropped to visible ink and retained independently, so a stat update does not rerasterize the title. Tint and border visuals stretch two one-pixel surfaces; hover zoom does not allocate a bitmap covering the enlarged preview. This uses GDI+ for changed assets; it is not an all-DirectWrite text implementation. Font family, size, styles, colors, outlines and signed offsets retain their existing settings. Alpha antialiasing replaces the old color-key edge treatment.

[ThumbnailOverlay](../../Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs) is a thin Windows host. Native mode uses `WS_EX_NOREDIRECTIONBITMAP`, without `WS_EX_LAYERED` or a transparency key. Whole-overlay opacity belongs to the composition tree. The legacy reference controls are hidden in native mode. `WM_NCHITTEST` returns `HTTRANSPARENT`, routing image/title/alert interaction to the paired preview on the same thread; this preserves hover, right-hold movement, resize and menu input. Both hosts use `WS_EX_NOACTIVATE`: game focus changes still go through the established explicit source activation route.

Graphics creation or managed COM update failure switches the overlay to the existing compatibility renderer. The fallback must recreate the overlay HWND to remove `WS_EX_NOREDIRECTIONBITMAP` and restore the GDI backing, transparency key and existing label/marker controls. It retains the game image HWND and DWM session. HWND destruction disposes the target; new handles get new graphics resources. Native restoration explicitly reasserts graphics visibility independently of WinForms' cached visibility.

Ordinary preview maintenance also checks device health, at most once per shared device per second. `ID3D11Device.GetDeviceRemovedReason` reads device status without submitting graphics or waiting for GPU completion. A cached failure reaches each existing owner immediately; the removed device is excluded from future acquisitions while its old owners finish disposing. This makes an idle title eligible for compatibility recovery even when its scene never changes. Mocked failure tests exercise this route; a physical driver reset remains untested.

Each thumbnail owns its scene and alert state. Calling `ShowAlert` on one view affects only that view, even though Windows renderers share the graphics device. `SetOverlayStats` accepts up to eight generic text rows. `ShowAlert` accepts finite pulse/tint/border and optional alert-graphic shake parameters; normalization bounds duration, intensity and amplitude. Hiding, disposal, clearing and renderer changes cancel transient effects. Effects never move the desktop window, change saved geometry or modify game pixels. These APIs are ready for future event producers; combat-log ingestion, real damage detection and live combat stats are not implemented by this renderer change. No shield/armor percentages or other unavailable telemetry are inferred. Tests use explicitly synthetic labels and counters.

`OverlayScene.ActiveBorder` describes a retained selection frame using four visuals sharing a cached one-pixel color surface. It sits above the image, alerts and text. All four edges use the configured native-pixel thickness. Selection does not change the DWM image destination, so aspect ratio is unchanged and no image update or host repaint is needed. Switching visibility retains the pixel for the next activation. Color changes upload that one pixel; border-only changes do not rasterize title/stat assets. The native renderer advertises `ActiveBorder`; the portable candidate does not yet advertise this optional capability.

`OverlayScene.AlertBounds` optionally limits alert graphics to a fixed rectangle inside that frame, preserving even a one-pixel border during full-intensity alerts. A fixed parent owns the clip and position; only its child animates, so optional shake cannot move the clip over the border. Title/stat layout remains independent. Switching active clients changes these retained bounds without uploading text or restarting an in-progress alert. Focus is requested before either frame is changed; see the [switching order and foreground notifications](windows-and-thumbnails.md#focus-switching-and-prediction).

## Preferences and workspace

`ApplicationPreferences.PreviewOverlayRenderer` is a global preference in the existing settings file, independent of gameplay profiles and the Light/Dark/Legacy theme choice. Values are `NativeComposition` and `Legacy` (the latter names the old graphics implementation, not the settings theme). The modern Previews advanced page exposes Enhanced graphics / Compatibility graphics and a character selector for a synthetic visual-alert test. The test flashes red without shaking; optional shake remains available to future event producers. Apply renderer changes before testing. The command requires a target and alerts at most one matching visible preview; an absent or unsupported target produces a result message. Legacy gains no new controls.

[ThumbnailViewFactory](../../Eve-O-Preview/View/Implementation/ThumbnailViewFactory.cs) applies the preference and tracks live views for overlay-only changes. Unrelated preference notifications do not recreate unchanged renderers. The [Windows workspace preview renderer](../../Eve-O-Preview/View/Implementation/WindowsWorkspacePreviewRenderer.cs) uses the selected asset renderer for draft title previews; the configured title size is not multiplied by settings-window DPI.

## Portable renderer and Linux boundary

[AvaloniaPreviewOverlay](../../Eve-O-Preview.UI/Previews/AvaloniaPreviewOverlay.cs) implements the same graphics contract with retained scene drawing and finite composition animations. Its [window wrapper](../../Eve-O-Preview.UI/Previews/AvaloniaPreviewOverlayWindow.cs) is a validation host. Platform ownership, hit testing and nonactivation still require an OS adapter. Native pixels are mapped through the actual Avalonia render scale.

No Linux capture/backend or full Linux application host is shipped by this change. This validation machine has no installed WSL/Linux environment. X11/Wayland capture and activation remain separate work described in the technology review. Unsupported Linux features may remain unavailable without limiting the Windows implementation.

## Validation boundaries

Use the existing [build/test guide](build-and-test.md) and thumbnail lifecycle/input suites. Native overlay checks exercise hardware resource lifecycle, unchanged-state reuse, hidden effects and compatibility fallback; the real desktop harness separately checks visible compositor pixels.

- [Windows rendering harness](../../tests/Preview.RenderingSmoke/Program.cs): production DWM views over mock or explicitly selected live clients, renderer comparison, counters, focus preservation and scoped preview captures. Live runs require an interactive desktop and explicit live/restore options. Source placements and foreground must be restored in `finally`; never send gameplay commands to produce a synthetic visual effect.
- [Portable rendering smoke](../../tests/Eve-O-Preview.Preview.Smoke/Program.cs): retained scene appearance, alert expiry, style/marker/stats coverage and unchanged-state behavior. Headless rendering does not establish real Linux capture or desktop interaction.

Do not confuse maintenance-call duration with end-to-end switching latency, application CPU with compositor/GPU work, repeated previews of two sources with many independent game clients, or synthetic alerts with detected combat events. The rendering migration leaves Robin unchanged; existing native audio/FPS behavior still has its own validation requirements.

## Executed validation, 2026-09-08

The Windows suite passed **159/159** checks after the focus-first, uniform overlay-border and asynchronous logging changes, including thirteen native overlay cases, existing thumbnail/input/lifetime scenarios, renderer switching without DWM replacement, active-client highlighting, global preferences, target routing and workspace rendering. The blocked diagnostic-writer case verifies bounded, nonblocking producers and shutdown drain. The portable overlay smoke previously passed its scene, style, marker, unchanged-state, animation-expiry, hide/clear and protected-border checks. The workspace smoke previously passed **160** renders, including all eighteen language catalogs, selected-client alert commands and absence of new controls in Legacy.

Desktop measurements used Debug harness builds, two existing EVE processes, 384 by 216 pixel previews, and a 24-logical-processor Windows machine. Available GPUs were NVIDIA GeForce RTX 4070 Ti (driver 32.0.15.9649) and Intel Graphics (32.0.101.6129); Windows reported 10.0.26200.0. Early captures showed active hangar scenes. The two clients later displayed connection-lost dialogs; the matched one-alert comparison and staggered proof use those real disconnected-client windows, not an active combat scenario. The measurements below used the earlier pulse-plus-shake workload; the final test button and live demonstration were subsequently changed to flash only. The harness did not reconnect, send gameplay input, inject code or edit profiles. Both sources returned to their original minimized state.

| Workload | App CPU, percent of one core | Maintenance p95, whole batch | Final private memory |
| --- | ---: | ---: | ---: |
| Native, two previews idle, 60 seconds | 0.75% | 3.32 ms | 74.0 MiB |
| Native, one of two previews alerted every two seconds, 30 seconds | 0.78% | 2.78 ms | 74.8 MiB |
| Avalonia candidate, same one-alert workload, 30 seconds | 7.44% | 3.50 ms | 120.4 MiB |

Each native idle renderer retained its upload/commit counts for the full minute; GDI and USER object counts stayed unchanged. The matched one-alert runs each retained two DWM registrations with 124 updates and no failures or maintenance focus captures. The unalerted native preview submitted no additional uploads or commits. **One native maintenance sample took 239 ms (p99); its cause remains unexplained.** These measurements do not establish end-to-end switch latency or a universal performance ranking. Earlier short 4/12/24-preview mock comparisons passed all fifteen runs and also favored native graphics for active alerts; short idle CPU results were noisy. Repeated previews of two animated mock sources are not independent game clients.

Two quiet 30-second native repetitions added harness-only per-preview and DWM-call timing. Across 116 maintenance batches no individual preview or DWM update exceeded 25 ms; batch p99 values were 4.19 and 3.00 ms. The original 239 ms event did not recur, so these repetitions do not identify its cause. Both retained their two DWM sessions and passed focus checks. The diagnostic threshold adds detailed records only for slow calls, without changing production code.

A subsequent final verification overlapped other validation and recorded a 58.08 ms DWM update with 0.49 ms remaining host work, plus untimed startup DWM calls of approximately 1.28 and 1.03 seconds. It is not a clean benchmark and does not identify the earlier outlier, but demonstrates why compositor API delays must be separated from overlay processing and why these samples cannot guarantee worst-case latency.

External PDH samples from the one-alert runs measured native process 3D-engine activity below 0.001%, versus about 0.119% for Avalonia, and dedicated GPU allocations of approximately 14.1 versus 32.2 MiB. Native composition work is also performed by DWM: sampled DWM 3D activity was approximately 0.90% versus 1.10%, including the entire desktop. These figures must not be interpreted as zero GPU cost or as isolated compositor overhead. The standalone harness initializes Avalonia only for that candidate; the complete application already uses Avalonia for settings, so the memory difference is not an incremental full-application comparison.

The final staggered proof ran the current native device-health and clipping code. A two-second red flash started on the first thumbnail, followed 0.822 seconds later by a 1.8-second cyan flash on the second; both explicitly used zero shake. Scoped captures confirmed first-only at 0.461 seconds, both active at 1.113 seconds, second-only at 2.156 seconds, and both expired at 2.932 seconds. Starting the second left the first renderer's upload/commit counts unchanged; natural expiry required no further submissions. Keyboard focus stayed unchanged. Local evidence is under `src/bin/preview-rendering-results/final-native-staggered-live-2`.

Initial active-border validation found and fixed full-intensity alerts obscuring thin highlights. Those early checks observed border properties before source activation and waited before captures; they did not establish input-to-visible-frame latency. The current `ThumbnailManager.SetActive` regression instead requires focus before graphics work, followed by immediate retained-frame submission. It exercises 200 switches with no message pump, no DWM image updates and no repeated uploads; it also covers same-thumbnail clicks, uniform widths, custom colors, external-event reconciliation and stale/stopped notifications. Earlier local captures under `src/bin/preview-rendering-results/final-active-highlight` establish exact one-pixel GreenYellow and five-pixel DeepSkyBlue color through a stationary red flash, but use the earlier inset geometry. Hook/IPC services remained stubbed in that harness.

After the uniform retained-frame change, live switching checks used two running
clients with hangar scenes. The isolated manager confirmed exact uniform edge
pixels through a stationary flash, retained both DWM registrations and submitted
no DWM image updates or repeated border uploads during 200 cycles. Later
foreground-notification repetitions passed all 20 external-switch pixel checks,
but showed variable foreground completion latency. These are separate checks
from the full application's input, affinity and logging paths.

On 2026-09-09, the restarted Debug production application was tested through its
existing F16 global cycle binding, with no test thumbnail windows. The corrected
harness sent 200 switches with 50.06–52.17 ms between inputs (mean 50.59 ms), then
20 additional switches with selected/cleared border pixel comparisons. All 20
pixel checks passed. Across those 220 production activation log records, native
focus requests began 0–1 ms after cycle handling started; border submission took
1–3 ms (median 1 ms, p95 2 ms), with no repeated requested client. Log resolution
is 1 ms and this measures submission, not compositor presentation.

The full live run **did not pass the 50 ms focus deadline**: 30 of 200 samples
still reported no foreground window at the deadline. This must not be described
as instant end-to-end switching. Pixel readback observations took 28–65 ms and
include capture overhead; they do not measure monitor scan-out. The active profile
had automatic CPU affinity enabled and the FPS limiter disabled. Earlier
production runs used coarse sampling and catch-up bursts after missed deadlines;
their timing results are superseded by this fixed-cadence run. Local evidence is
under `src/bin/preview-rendering-results/production-fixed-cadence`, including
`production-result.json` and `application-timings.json`. The remaining delay needs
target-client/input-queue and presentation measurements, rather than assuming
another full-image redraw is responsible.

The Windows suite passed 159/159 after the focus, foreground observer and logging
changes. The workspace preview checks then passed 9/9, including three added
landscape/portrait pixel cases requiring the configured thickness on every edge.

The optimized 24-preview mock run also passed production zoom/restore: Windows clamped the requested 25x window to 5580 by 1940 pixels and restored 384 by 216. Its first renderer retained 8,816 scene pixels and two alert pixels. A separate native regression exercised a 9600 by 5400 viewport with fewer than 100,000 retained scene pixels; clipped or entirely off-screen ink does not allocate zero-sized bitmaps.

The unsigned Release application was published as a framework-dependent `win-x64` single file under `src/bin/preview-modernized`. Copying only the executable to an isolated directory and running `--validate-workspace` passed with exit code zero, verifying bundled contracts, UI resources and native rendering dependencies. Robin was unchanged and was not rebuilt or packaged in this application-only check. Existing DPI/Costura warnings and an unavailable NuGet vulnerability-feed warning remain. No Cake, signing, Linux runtime, physical GPU-reset, mixed-DPI/HDR, real audio/FPS-hook or game-frame-time validation ran for this change.
