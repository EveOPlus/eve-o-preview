# Portable overlay smoke

Run from `src`:

```powershell
dotnet run --project tests/Eve-O-Preview.Preview.Smoke/Eve-O-Preview.Preview.Smoke.csproj -p:UsedAvaloniaProducts= -- --output bin/portable-preview-smoke
```

This uses the actual `AvaloniaPreviewOverlayWindow` and renderer with Avalonia Headless
and Skia at the repository's pinned version. It checks outlined font styles, all skip
markers with titles hidden, stats, unchanged-state drawing idleness, compositor alerts,
finite expiry, clear and hide/show recovery. Generated PNGs show the retained scene and
active/expired alerts. The scene-render count measures UI drawing, not GPU presentations.
The clipping check compares a one-pixel protected frame under maximum-intensity tint and
24-pixel optional shake, verifies bounds changes preserve retained title/stat geometry
and animation expiry, and exercises empty, negative and extreme producer bounds.

It does not validate native alpha layering, ownership, input, focus, game capture, GPU
overhead, real monitor DPI or Linux integration. Use the Windows rendering benchmark for
the actual DWM/overlay path and keep Linux capability claims tied to a live Linux host.
