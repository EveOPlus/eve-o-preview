# Portable preview graphics candidate

`AvaloniaPreviewOverlay` implements `IOverlayRenderer` from the portable Preview project.
It preserves native-pixel title size, offsets, outlined styles and all three skip markers,
including markers with titles hidden. Scene changes rebuild retained geometry; up to 8
stat rows use nine selectable positions. `OverlayLayout.Arrange` measures and stacks
title/system and meter blocks sharing an anchor. An equal replacement state does
not invalidate the scene. Incoming damage uses a separate retained tint rectangle;
fade frames update its opacity and the title brush without rebuilding glyph geometry.

The alert layer is a separate `CompositionCustomVisual`. Finite opacity and offset
keyframes run in Avalonia's compositor. This separate alert API has no UI timer, managed per-frame scene
rebuild or alert-frame request loop. Shake only moves alert graphics within fixed clipped
bounds. Clear, hide, detach and disposal stop animations and hide the alert visual.
Reduced motion uses a steady alert followed by expiry. Alerts received before attachment
or while hidden are discarded; they are not queued for later display.

`OverlayScene.AlertBounds` places a fixed composition parent around the animated child,
so alert tint and optional shake cannot obscure the host's active-client border. Null
uses the whole preview. Empty rectangles, negative dimensions and rectangles entirely
outside the preview produce no visible alert; partially outside bounds are clipped.
Bounds updates preserve the running animation's deadline and do not rebuild title/stat
geometry. Native-pixel clipping also applies after render-scale conversion.

`AvaloniaPreviewOverlayWindow` is a transparent candidate host for platform validation.
It does not implement capture, native ownership, click-through, z-order or client control.
The Windows validation harness configures those native rules above an existing DWM
preview. Production Windows uses its independently selected native renderer; this
candidate does not force a shared rendering cost onto Windows.

Coordinates passed through `IOverlayRenderer` are native preview pixels. The control
applies inverse `TopLevel.RenderScaling` when drawing and laying out the window. Text
uses the configured font em-size without point-to-DIP conversion. Avalonia 11.3.20's
outlined text geometry did not preserve underline/strikeout in the verified smoke path,
so those styles use explicit vector decoration rectangles with the same fill and outline.

Linux capture, Wine window activation and native X11/Wayland behavior remain unimplemented
and unverified. The 2026-09-08 environment probe found WSL not installed and no Docker or
Bash executable. No distribution was installed or system configuration changed. A portable
UI build and headless renderer are not a working Linux EVE client backend.

See the [portable smoke runner](../../tests/Eve-O-Preview.Preview.Smoke/README.md) and
[technology review](../../docs/ai/preview-rendering-review.md) for validation boundaries.
