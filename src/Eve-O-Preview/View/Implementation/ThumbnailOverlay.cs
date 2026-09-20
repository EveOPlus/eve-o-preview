using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Preview;
using EveOPreview.View.Rendering;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;

namespace EveOPreview.View;

/// <summary>Avalonia owns this transparent, nonactivating overlay top-level. The Windows
/// compositor retains native assets independently of the paired DWM image window.</summary>
public class ThumbnailOverlay : Window, IDisposable
{
    private readonly Window _owner;
    private readonly WindowsPreviewWindowAdapter _native;
    private OverlayRendererKind _rendererKind;
    private IOverlayRenderer _renderer;
    private OverlayScene _scene = new();
    private double _overlayOpacity = 1;
    private bool _disposed;

    public ThumbnailOverlay(Window owner, OverlayRendererKind rendererKind = OverlayRendererKind.Legacy)
    {
        _owner = owner; _rendererKind = rendererKind;
        Title = "EVE-O Preview overlay";
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false; ShowActivated = false; CanResize = false;
        Background = Avalonia.Media.Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowStartupLocation = WindowStartupLocation.Manual;
        _native = new WindowsPreviewWindowAdapter(this, clickThrough: true);
        Closed += (_, _) => ReleaseResources();
        SizeChanged += (_, _) => ResizeRenderer();
    }
    public IntPtr Handle => _native.Handle;
    public bool IsHandleCreated => Handle != IntPtr.Zero;
    public bool IsDisposed => _disposed;
    public bool Visible => IsVisible;
    public bool TopMost { get => Topmost; set => Topmost = value; }
    public Point Location { get => _native.Location; set => _native.Location = value; }
    public Size Size { get => _native.Size; set => _native.Size = value; }
    public new Size ClientSize { get => _native.ClientSize; set => _native.ClientSize = value; }
    public Point PointToScreen(Point point) => _native.PointToScreen(point);
    public Point PointToClient(Point point) => _native.PointToClient(point);
    public OverlayRendererKind RendererKind => _rendererKind;
    internal OverlayScene Scene => _scene;
    public OverlayCapabilities GraphicsCapabilities => _renderer?.Capabilities ?? (OverlayCapabilities.Title | OverlayCapabilities.CycleMarker);
    // Visual.Opacity does not affect the independent DirectComposition tree.
    public new double Opacity
    {
        get => _overlayOpacity;
        set { _overlayOpacity = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1; Render(r => r.SetOpacity(_overlayOpacity)); }
    }
    public new void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsVisible) { if (_owner != null && _owner.IsVisible) base.Show(_owner); else base.Show(); }
        _native.EnsureStyles(); EnsureRenderer(); Render(r => r.SetVisible(true));
    }
    public new void Hide() { if (_disposed) return; Render(r => r.SetVisible(false)); base.Hide(); }
    public void Refresh() => ResizeRenderer();
    public void RestoreGraphicsVisibility() => Render(r => r.SetVisible(true));
    public void ClearAlerts() => Render(r => r.ClearAlerts());
    public void ShowAlert(PreviewAlert alert) { if (IsVisible && !_disposed) Render(r => r.ShowAlert(alert.Normalize())); }
    private void EnsureRenderer()
    {
        if (_renderer != null || _disposed) return;
        if (_rendererKind == OverlayRendererKind.NativeComposition)
        {
            try { _renderer = CreateNativeRenderer(Handle); }
            catch (Exception ex) { UseCompatibilityRenderer(ex); return; }
        }
        else CreateCompatibilityRenderer();
        ApplyRendererState();
    }
    protected virtual IOverlayRenderer CreateNativeRenderer(IntPtr handle) => new NativeCompositionOverlayRenderer(handle);
    private void ApplyRendererState() => Render(r =>
    {
        var size = ClientSize;
        r.Resize(new PreviewSize(size.Width, size.Height)); r.SetScene(_scene);
        r.SetOpacity(_overlayOpacity); r.SetVisible(IsVisible);
    });
    private void CreateCompatibilityRenderer()
    {
        var renderer = new CompatibilityOverlayRenderer(); _renderer = renderer; Content = renderer;
    }
    private void UseCompatibilityRenderer(Exception ex)
    {
        _renderer?.Dispose(); _renderer = null; _rendererKind = OverlayRendererKind.Legacy;
        Serilog.Log.Warning(ex, "Native preview overlay unavailable; using compatibility graphics");
        // Avalonia retains its transparent target. Releasing only our DComp target
        // restores that surface without replacing this HWND or the DWM image HWND.
        CreateCompatibilityRenderer(); ApplyRendererState();
    }
    internal void MaintainGraphics()
    {
        if (_renderer is not NativeCompositionOverlayRenderer native) return;
        try { native.CheckDeviceHealth(); }
        catch (System.Runtime.InteropServices.COMException ex) { UseCompatibilityRenderer(ex); }
    }
    private void Render(Action<IOverlayRenderer> action)
    {
        if (_renderer == null || _disposed) return;
        try { action(_renderer); }
        catch (System.Runtime.InteropServices.COMException ex) when (_rendererKind == OverlayRendererKind.NativeComposition)
        { UseCompatibilityRenderer(ex); }
    }
    private void ResizeRenderer()
    {
        if (_native == null) return;
        var size = ClientSize; Render(r => r.Resize(new PreviewSize(size.Width, size.Height)));
    }
    private void SetScene(OverlayScene scene)
    {
        if (_scene == scene) return;
        _scene = scene; Render(r => r.SetScene(scene));
    }
    public void SetOverlayLabel(string label) => SetScene(_scene with { Title = label });
    public void EnableOverlayLabel(bool enable) => SetScene(_scene with { ShowTitle = enable });
    public void SetOverlayFont(FontSettings settings) => SetScene(_scene with
    {
        Font = new OverlayFont(settings.Name, settings.Size, (OverlayFontStyle)settings.Style,
            unchecked((uint)settings.ForeColor.ToArgb()), unchecked((uint)settings.OutlineColor.ToArgb()),
            settings.OutlineWidth, settings.PositionOffsetFromLeft, settings.PositionOffsetFromTop)
    });
    public void SetCycleSkipIndicator(bool skipped, string style, Color color) => SetScene(_scene with
    {
        CycleSkipped = skipped, MarkerStyle = style switch { "Pause" => CycleMarkerStyle.Pause, "Cross" => CycleMarkerStyle.Cross, _ => CycleMarkerStyle.CircleSlash },
        MarkerColor = unchecked((uint)color.ToArgb())
    });
    public void SetStats(IReadOnlyList<OverlayStat> stats, OverlayStatsStyle style = null)
    {
        var next = stats?.Take(8).ToArray() ?? Array.Empty<OverlayStat>(); style ??= _scene.StatsStyle;
        if (!_scene.Stats.SequenceEqual(next) || _scene.StatsStyle != style) SetScene(_scene with { Stats = next, StatsStyle = style });
    }
    public void SetSubtitle(string text, uint color, SubtitlePlacement placement = SubtitlePlacement.Below, float? fontSize = null) =>
        SetScene(_scene with { Subtitle = text, SubtitleColor = color, SubtitlePlacement = placement, SubtitleFontSize = fontSize });
    public void SetTitleColor(uint? color) => SetDamageFlash(color, _scene.DamageTint, _scene.DamageFlashIntensity);
    public void SetTitlePosition(OverlayPosition position) => SetScene(_scene with { TitlePosition = position });
    public void SetDamageFlash(uint? titleColor, uint? tint, double intensity = 1) => SetScene(_scene with
    {
        TitleColor = titleColor, DamageTint = tint, DamageFlashIntensity = double.IsFinite(intensity) ? Math.Clamp(intensity, 0, 1) : 0
    });
    internal void SetAlertBounds(PreviewRect? bounds) => SetScene(_scene with { AlertBounds = bounds });
    internal void SetActiveBorder(OverlayBorder border) => SetScene(_scene with { ActiveBorder = border, AlertBounds = border?.InnerBounds });
    public void Dispose() { if (_disposed) return; ReleaseResources(); Close(); }
    private void ReleaseResources()
    {
        if (_disposed) return;
        _disposed = true; _renderer?.Dispose(); _renderer = null; _native.Dispose();
    }
}
