# Preview presentation and overlay rendering

Avalonia owns production preview and overlay top-level windows. Windows keeps DWM live images and an independent retained DirectComposition overlay, following the [technology review](preview-rendering-review.md). The portable Avalonia vector renderer remains a comparison/future-platform implementation; compatibility graphics uses the Windows glyph rasterizer in retained Avalonia image controls.

## Ownership and portable boundaries

[Augments](combat-logs.md) now supplies real log-derived alpha/DPS/repair/location
rows through `ThumbnailView.SetOverlayStats`. Each `OverlayStat` can carry five
independently coloured symbols and an inline `Suffix` with its own text colour
and trailing icons. `Visible = false` reserves the row without drawing ink, so
incoming/outgoing rows never move when activity stops. `OverlayStatsStyle` controls font/offset/edge
placement in preview-client pixels. Four damage and 32 fixed weapon-platform SVGs are
embedded in the portable contract assembly and loaded once as polygon geometry.
Native and portable drawing consume the same geometry, without an SVG runtime
or file reads on frame callbacks. Scene equality includes stats and their style,
so unchanged updates retain native assets. Alpha/repair expiry uses a finite
manager timer; log ingestion never enters the compositor or Robin. `OverlayScene.Subtitle`
holds the plain system name relative to the title, separate from combat rows.
`SubtitlePlacement` supports Below (default), Above, Left and Right;
`SubtitleFontSize` optionally overrides title size. `OverlayScene.TitleLayout`
positions the combined block at the title offsets using renderer-measured widths.
Native measurement, drawing and portable previews share that layout. Horizontal
neighbours do not wrap. System changes invalidate the native title asset; changing
graphics renderer preserves the size and placement. Compatibility graphics draws
all title/system and stat assets through the same rasterizer. The former
`OutlinedLabel` survives only as a test reference for glyph size, colour, outline
and offset parity.
The native title asset includes the subtitle in both rasterization and equality;
the separate stats asset explicitly excludes it. Renderer switches retain it.

- [Eve-O-Preview.Preview](../../Eve-O-Preview.Preview/OverlayContract.cs) contains toolkit-independent scenes, finite alerts and renderer capabilities. [IPreviewBackend/IPreviewSession](../../Eve-O-Preview.Preview/PreviewContract.cs) represent image presentation without requiring captured frames.
- [WindowsDwmPreviewBackend](../../Eve-O-Preview/View/Rendering/WindowsDwmPreviewBackend.cs) owns persistent DWM relationships. Bounds changes update existing properties; maintenance retains healthy registrations and populates replacements before releasing obsolete ones. Its source/destination HWND resolver belongs entirely to Windows.
- [WindowsStaticPreviewBackend](../../Eve-O-Preview/View/Rendering/WindowsStaticPreviewBackend.cs) owns one retained Avalonia bitmap and an input-transparent image inside the thumbnail canvas. Forced maintenance captures through `WindowManager.GetStaticThumbnail`; failed captures retain the last valid frame. This explicitly selected capture backend is independent of the ordinary DWM path.
- [LiveThumbnailView](../../Eve-O-Preview/View/Implementation/LiveThumbnailView.cs) and [StaticThumbnailView](../../Eve-O-Preview/View/Implementation/StaticThumbnailView.cs) adapt the established Windows manager/view lifecycle to those sessions. Discovery, cycling, CPU affinity, Robin and the named-pipe protocol remain on their existing routes.
- [ThumbnailView](../../Eve-O-Preview/View/Implementation/ThumbnailView.cs) continues owning preview geometry, input, menus, hover zoom and nonactivating MRU restoration. Changing its overlay renderer replaces only the overlay window; it does not replace its DWM session.

`PreviewClientId` is ephemeral adapter identity. Existing full window titles remain profile/layout/cycle keys. The new contracts do not make the Windows manager or application lifetime portable; a Linux host still needs its own discovery, window control, shortcuts and capture integration.

`OverlayScene.DamageTint` is an optional transient ARGB wash. Native composition
keeps one colour pixel behind text and the selection frame; on/off phases attach
or detach that surface without uploading the image or rasterizing text. Resize
only changes its scale. The portable renderer uses a separate retained rectangle.
Compatibility graphics uses a retained Avalonia tint rectangle in the overlay
window. Flash phases change its opacity; no extra tint HWND, game screenshot or
bitmap upload is required. Title flashes rasterize their blended glyph colour,
preserving the compatibility renderer's full glyph alpha.
The main preview/DWM HWND and image relationship are unchanged in either path.

`DamageFlashIntensity` supplies a shared finite Blink/Fade strength. Native
composition cross-fades cached normal/highlight title assets and changes tint
visual opacity; no glyph uploads occur on intensity-only frames. Portable
graphics retain geometry and blend the title brush; compatibility graphics
blend the title colour and retained tint rectangle. Only active damage requests fade ticks.
`CombatLogSettings.FlashOpacityPercent` sets the tint's maximum alpha before this
intensity is applied, at 0–100% (default 20%). Zero suppresses the wash, and title
blending remains independent. Fade is the default; explicit Blink choices survive.
`OverlayLayout.Arrange` positions measured title/system and stats blocks at nine
anchors and stacks matching anchors. Native asset cropping happens after layout,
so splitting title and stats surfaces cannot remove their collision reservation.

Repair symbols use the embedded [repair SVGs](../../Eve-O-Preview.Preview/Assets/Repairs/README.md): shield rim, helmet silhouette and three-face cube. They retain the existing `Shield`, `Armor` and `Hull` identities and configured colours. Polygon geometry loads once through `OverlaySymbols`, shared by both rendering paths. The repair row's IN/OUT prefix supplies direction.

## Windows graphics path

`OverlaySceneRasterizer` supplies compatibility titles, settings samples and
native title/DPS assets. Its Windows GDI+ drawing is an explicit rasterization
adapter with a `System.Drawing.Common` dependency; it needs no WinForms controls
or message loop. Fonts retain their historical drawing units and signed offsets.
`OverlayStatsStyle.FontFamily` and `FontStyle` are nullable: absent values inherit
the title family/style while preserving the independent combat size and colours.
The Augments advanced editor saves overrides through `LogOverlayOptions`, with a
reset to inheritance. Both native asset bounds and portable geometry use those
values, including alpha spacing and text decorations. A title style change also
invalidates inherited stats. The settings simulation uses the same options.
`OverlayStat.Segments` walks the immutable inline suffix chain. Repair segments
set `PrefixIcons` to place each type icon before its number; alpha retains its
trailing-icon layout. Native ink bounds, arrangement and drawing and portable
geometry all measure the complete chain, including gaps. Repair icon and number
share the configured type colour. Assets still update only when scenes change.

Augments alternates `OverlayScene.TitleColor` for a transient name blink, preserving
the configured `OverlayFont.Foreground`. Both renderers include it in retained
scene equality. A shared phase calculation supplies the overview and thumbnail;
the manager schedules the next transition without reading files or changing DWM.
Repeated damage extends expiry without restarting the blink cycle.
`OverlayStat.Icons()` supports four damage components plus an
optional weapon/unresolved symbol. Raster bounds include every icon; changing
damage metadata never recreates the source DWM relationship.

[NativeCompositionOverlayRenderer](../../Eve-O-Preview/View/Rendering/NativeCompositionOverlayRenderer.cs) shares one hardware D3D11/DirectComposition device among overlays on the UI thread. It retains graphics surfaces and submits finite compositor animations. It has no application animation timer, screenshot loop, game-frame readback or new IPC. Initialization deliberately does not fall back to a software GPU device.

Text/marker/stat assets come from [OverlaySceneRasterizer](../../Eve-O-Preview/View/Rendering/OverlaySceneRasterizer.cs). They are CPU-rasterized only when their scene changes, then retained for GPU composition. Title/marker and stat assets are cropped to visible ink and retained independently, so a stat update does not rerasterize the title. Tint and border visuals stretch two one-pixel surfaces; hover zoom does not allocate a bitmap covering the enlarged preview. This uses GDI+ for changed assets; it is not an all-DirectWrite text implementation. Font family, size, styles, colors, outlines and signed offsets retain their existing settings. Alpha antialiasing replaces the old color-key edge treatment.

[ThumbnailOverlay](../../Eve-O-Preview/View/Implementation/ThumbnailOverlay.cs) is an owned transparent Avalonia `Window`. Its top-level HWND receives the native composition target directly; whole-overlay opacity belongs to that tree. [WindowsPreviewWindowAdapter](../../Eve-O-Preview/View/Rendering/WindowsPreviewWindowAdapter.cs) enforces `WS_EX_NOACTIVATE`, tool-window styles and native pixel geometry. Overlay `WM_NCHITTEST` returns `HTTRANSPARENT`, and `WM_MOUSEACTIVATE` rejects activation; image/title/alert interaction routes to the paired preview on the same UI thread. Source activation remains explicit and precedes appearance work.

While native graphics owns the overlay, `ThumbnailOverlay` suspends Avalonia's
unused rendering with the protected `StopRendering` lifecycle method after show
and native window-state restoration. This reduces redundant framework graphics
resources without replacing the HWND or modifying the DWM image host. In the
pinned [Avalonia 11.3.20 TopLevel](https://github.com/AvaloniaUI/Avalonia/blob/11.3.20/src/Avalonia.Controls/TopLevel.cs),
`StopRendering` removes the top-level from `MediaContext`; `StartRendering`
reenables it. Suspension does not promise to dispose every resource already
allocated by the framework. Do not dispose its renderer or reflect into internals
in production: the same window must support recovery.

Graphics creation or managed COM update failure releases the native target and
switches that overlay HWND to [CompatibilityOverlayRenderer](../../Eve-O-Preview/View/Rendering/CompatibilityOverlayRenderer.cs).
Fallback resumes Avalonia rendering and applies the latest scene, size and opacity.
No window recreation, transparency key or label controls are needed. The source
image HWND and healthy DWM registration remain unchanged. Closing releases the
native target before its HWND is destroyed. Native restoration explicitly
reasserts graphics visibility independently of the framework's cached visibility.

Hidden overlays retain their latest scene and defer text rasterization, uploads,
size and opacity application until shown. Their existing graphics remain cached
for fast restoration; hiding is not a resource eviction timer. The `Hide` override
also covers hiding through an owner/base-window reference. Compatibility graphics
disposes the temporary GDI bitmap immediately after copying it into Avalonia;
only the destination bitmap and placement rectangle remain owned.

Native text and colour surfaces reuse their allocation when pixel dimensions
match. [BeginDraw/EndDraw](https://learn.microsoft.com/en-us/windows/win32/api/dcomp/nf-dcomp-idcompositionsurface-begindraw)
uploads every pixel, including transparent background, before commit. A changed
size creates an exact replacement before releasing the old surface; empty text
detaches/releases its surface. There is no growing texture pool that retains a
large zoom/font allocation. `SurfaceAllocationCount` and `SurfaceUploadCount`
distinguish allocation reuse from avoided drawing. Surface reuse reduces churn;
it does not eliminate rasterization/upload of changed text.

The composition arrangement was checked with Avalonia 11.3.20 and actual Windows compositor pixels before integrating the host. Both DWM source and destination are top-level windows, as required by [DwmRegisterThumbnail](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail); no native child image surface or bitmap copy is substituted. A separately owned transparent Avalonia top-level accepts the native DirectComposition target while Avalonia owns its lifetime. The permanent [rendering harness](../../tests/Preview.RenderingSmoke/README.md) `--host-proof` exercises the production host using a controlled source and backdrop: source `FF0F5AB4` blends to `FF082D5A` at 50% whole-window opacity; half-red tint produces `FF872D5A`; full-red tint retains all four green edges and every sampled opaque white title pixel. Both native and compatibility graphics pass the actual compositor border/title checks. Compatibility tint clips to the inset image's `AlertBounds`, preserving the host-drawn frame, and clearing selection restores full-image tint. Renderer switching retains the image HWND/DWM relationship. The source-lifetime phase exercises real source minimize/restore, then production manager removal/recreation through controlled discovery snapshots; a same-title new source produces new pixels and all registrations balance at disposal. These checks preserve foreground and do not establish EVE discovery, physical device-loss or live gameplay behavior; those have separate validation boundaries.

Native visual construction runs from back to front. `AddVisual` passes `insertAbove = false` with a null reference visual to append above existing siblings, matching [the Windows API semantics](https://learn.microsoft.com/en-us/windows/win32/api/dcomp/nf-dcomp-idcompositionvisual-addvisual). The previous value inserted later children underneath earlier ones; a synthetic compositor capture demonstrated damage tint covering the title and active frame. The correction keeps both above the tint. Treat this as a corrected baseline defect when comparing captures.

Ordinary preview maintenance also checks device health, at most once per shared device per second. `ID3D11Device.GetDeviceRemovedReason` reads device status without submitting graphics or waiting for GPU completion. A cached failure reaches each existing owner immediately; the removed device is excluded from future acquisitions while its old owners finish disposing. This makes an idle title eligible for compatibility recovery even when its scene never changes. Mocked failure tests exercise this route; a physical driver reset remains untested.

Each thumbnail owns its scene and alert state. Calling `ShowAlert` on one view affects only that view, even though Windows renderers share the graphics device. `SetOverlayStats` accepts up to eight generic text rows. `ShowAlert` accepts finite pulse/tint/border and optional alert-graphic shake parameters; normalization bounds duration, intensity and amplitude. Hiding, disposal, clearing and renderer changes cancel transient effects. Effects never move the desktop window, change saved geometry or modify game pixels. [Augments](combat-logs.md) supplies log-derived stats and damage flashes through independent scene updates. No shield/armor percentages or other unavailable telemetry are inferred. Renderer tests use explicitly synthetic labels and counters.

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

## Current validation and open acceptance gates

The current production host retains native DWM images and DirectComposition
graphics. The resource-change rendering suite passed 35/35 checks. The full
integrated run passed 492/493, with no skipped cases: the documented intermittent
`settings-resources` process-handle check observed +13 against its unchanged
[-10, 10] bound, then passed when rerun alone. That full run is not an all-green
result. New tests cover surface reuse, grow/shrink/clear, hidden updates,
native minimize/restore and framework-renderer suspension/recovery.
The permanent `--host-proof` passed 30 compositor checks and 13 source-lifetime
checks, including actual pixels after reuse and fallback on the same HWND.
The current mixed-DPI run could not execute because the configured monitors
did not expose two different scales. Earlier migration validation covered
100%/125% transitions; it is not new validation of these resource changes.
See the [migration report](avalonia-migration.md) for the wider application/package
evidence and remaining live-input limits.

Five live clients passed the 15-second native image/alert workload and 2x hover
restoration with five persistent DWM relationships, no failed updates and no
preview focus capture. A guarded two-client run passed all fourteen real mouse,
menu, move/resize and hover checks, all four requested source activations, and
the one-/five-pixel border comparisons. Its overall result remained **failed**:
the raw foreground HWND changed during one alert interval. The cause was not
identified, and earlier live pointer runs were inconsistent. The test restored
the original foreground and cursor. This is not an unconditional live-input pass.

Current resource checks compare the preserved pre-optimization Avalonia host
with the optimized host, using twelve 384x216 native previews of two animated
synthetic sources, alerts on every preview, a one-second warmup and 30-second
intervals. These are twelve previews, not twelve independent EVE clients.
Debug builds use identical message pumps; CPU includes the source painting and
is a percentage of one logical core on a 24-logical-processor machine. Evidence
is under `src/bin/preview-memory-results/` (`baseline-matched`, `candidate-final`,
and `host-proof-final`), with isolated binaries under `src/bin/preview-memory-*`.

| Metric | Before | Optimized |
| --- | ---: | ---: |
| Application dedicated GPU allocation, MiB | 161.33 | 89.41 |
| Final process private memory, MiB | 180.73 | 149.43 |
| Final process handles | 1320 | 1295 |
| CPU, percent of one logical core | 11.66 | 9.32 |
| Maintenance p50 / p95 / p99, ms | 1.03 / 2.23 / 58.28 | 1.14 / 2.51 / 59.31 |

The allocation reduction is about 45% dedicated GPU memory and 17% process
private memory in this workload. An earlier candidate with the host suspension
alone was similar (89.24 MiB GPU and 148.97 MiB private), supporting the redundant
framework rendering path as the main saving. Each matched run retained twelve
DWM registrations, made 720 successful image updates, submitted 168 alerts and
recorded zero preview focus captures. The GPU figures are external PDH allocation
counters, not GPU execution time; private memory is the whole harness process,
not memory exclusively owned by previews. Shared GPU allocations are reported
separately in the raw counters and must not be added to physical-memory savings.

Native reuse tests exercise 100 same-sized colour changes and 100 changing
fixed-width counters without a new surface allocation, then grow/shrink/clear
the assets. Hidden-overlay tests exercise 100 scene/size updates without native
uploads or compatibility bitmap replacement, and check current state on show.
The actual compositor proof verifies changed pixels after reuse, hidden updates,
and same-HWND fallback. The proof's foreground assertion initially failed in both
baseline and candidate. Diagnostics identified its topmost black fixture backdrop
as the new foreground window. `SolidSource` now uses `WS_EX_NOACTIVATE` as well as
`ShowWithoutActivation`; the complete proof passes with the assertion intact.
This fixture correction does not resolve the separate live-input report above.

CPU and maintenance timing do not establish a speedup: the p99 tail remains
about 59 ms, and desktop/scheduling variation is not isolated. No application
3D-engine utilization instance was reported, which does not mean zero GPU work.
DWM includes the whole desktop and cannot attribute its workload solely to
these previews. Invalid counter samples are excluded by status in summaries.
Long-session resource stability, matched compatibility-renderer performance,
real-client FPS, and end-to-end input latency remain unverified.
Screen readback and WM_NULL response timings must never be called monitor scan-out
or game-frame latency. Physical GPU reset, HDR and multi-GPU behavior remain
separate hardware validation. Existing Robin endpoints were inspected without
changing their state; ordinary production startup against those clients needs
state-preserving validation. Linux capture/input remains unimplemented.
