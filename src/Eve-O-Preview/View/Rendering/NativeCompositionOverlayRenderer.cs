using EveOPreview.Preview;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using static EveOPreview.View.Rendering.NativeCompositionInterop;

namespace EveOPreview.View.Rendering;

/// <summary>
/// Windows retained overlay. DWM continues presenting the game; a separate, owned,
/// nonactivating WS_EX_NOREDIRECTIONBITMAP HWND receives this alpha visual tree.
/// All instances on the UI thread share one hardware D3D/DirectComposition device.
/// Changed text is uploaded once; finite pulse/shake animations run in DWM. There
/// is no application rendering timer, game capture, GPU readback, or injected code.
/// </summary>
public sealed class NativeCompositionOverlayRenderer : IOverlayRenderer
{
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private SharedDevice _device;
    private nint _target, _root, _sceneVisual, _statsVisual, _alertClipVisual, _alertVisual, _rootEffect, _alertEffect;
    private nint _tintVisual, _topVisual, _bottomVisual, _leftVisual, _rightVisual;
    private nint _sceneSurface, _statsSurface, _alertSurface, _tintSurface;
    private nint _borderRoot, _borderEffect, _borderSurface;
    private nint _borderTop, _borderBottom, _borderLeft, _borderRight;
    private uint? _borderColor;
    private nint _damageVisual, _damageSurface;
    private uint? _damageColor;
    private nint _damageEffect, _titleEffect, _flashTitleVisual, _flashTitleEffect, _flashTitleSurface;
    private long _flashTitlePixels;
    private long _titlePixels, _statsPixels;
    private PreviewSize _size;
    private OverlayScene _scene = new();
    private uint? _alertColor;
    private double _opacity = 1;
    private bool _visible = true, _alertActive, _disposed;

    public NativeCompositionOverlayRenderer(nint hwnd)
    {
        if (hwnd == 0) throw new ArgumentException("A created overlay HWND is required.", nameof(hwnd));
        try
        {
            _device = SharedDevice.Acquire();
            _target = CreateTarget(_device.Composition, hwnd);
            _root = CreateVisual(_device.Composition);
            _damageVisual = CreateVisual(_device.Composition);
            _sceneVisual = CreateVisual(_device.Composition);
            _flashTitleVisual = CreateVisual(_device.Composition);
            _damageEffect = CreateEffect(_device.Composition);
            _titleEffect = CreateEffect(_device.Composition);
            _flashTitleEffect = CreateEffect(_device.Composition);
            SetEffect(_damageVisual, _damageEffect);
            SetEffect(_sceneVisual, _titleEffect);
            SetEffect(_flashTitleVisual, _flashTitleEffect);
            NativeCompositionInterop.SetOpacity(_titleEffect, 1);
            NativeCompositionInterop.SetOpacity(_flashTitleEffect, 0);
            _statsVisual = CreateVisual(_device.Composition);
            _alertClipVisual = CreateVisual(_device.Composition);
            _alertVisual = CreateVisual(_device.Composition);
            _tintVisual = CreateVisual(_device.Composition);
            _topVisual = CreateVisual(_device.Composition);
            _bottomVisual = CreateVisual(_device.Composition);
            _leftVisual = CreateVisual(_device.Composition);
            _rightVisual = CreateVisual(_device.Composition);
            _borderRoot = CreateVisual(_device.Composition);
            _borderTop = CreateVisual(_device.Composition);
            _borderBottom = CreateVisual(_device.Composition);
            _borderLeft = CreateVisual(_device.Composition);
            _borderRight = CreateVisual(_device.Composition);
            _borderEffect = CreateEffect(_device.Composition);
            NativeCompositionInterop.SetOpacity(_borderEffect, 0);
            SetEffect(_borderRoot, _borderEffect);
            AddVisual(_borderRoot, _borderTop);
            AddVisual(_borderRoot, _borderBottom);
            AddVisual(_borderRoot, _borderLeft);
            AddVisual(_borderRoot, _borderRight);
            _rootEffect = CreateEffect(_device.Composition);
            _alertEffect = CreateEffect(_device.Composition);
            NativeCompositionInterop.SetOpacity(_rootEffect, 1);
            NativeCompositionInterop.SetOpacity(_alertEffect, 0);
            SetEffect(_root, _rootEffect);
            SetEffect(_alertVisual, _alertEffect);
            AddVisual(_alertVisual, _tintVisual);
            AddVisual(_alertVisual, _topVisual);
            AddVisual(_alertVisual, _bottomVisual);
            AddVisual(_alertVisual, _leftVisual);
            AddVisual(_alertVisual, _rightVisual);
            AddVisual(_alertClipVisual, _alertVisual);
            // Damage tint and alerts are behind text so names/stats remain legible.
            AddVisual(_root, _damageVisual);
            AddVisual(_root, _alertClipVisual);
            AddVisual(_root, _sceneVisual);
            AddVisual(_root, _flashTitleVisual);
            AddVisual(_root, _statsVisual);
            AddVisual(_root, _borderRoot);
            SetRoot(_target, _root);
            Commit(_device.Composition);
        }
        catch { Dispose(); throw; }
    }

    public OverlayCapabilities Capabilities => OverlayCapabilities.Title | OverlayCapabilities.CycleMarker |
        OverlayCapabilities.Stats | OverlayCapabilities.Pulse | OverlayCapabilities.Shake |
        OverlayCapabilities.CompositorAnimations | OverlayCapabilities.ActiveBorder;

    /// <summary>Instrumentation counts state uploads/transactions; never counts game frames.</summary>
    public long SurfaceUploadCount { get; private set; }
    public long CommitCount { get; private set; }
    public long SceneSurfacePixels => _titlePixels + _flashTitlePixels + _statsPixels;
    public long AlertSurfacePixels => _alertSurface == 0 ? 0 : 2;
    public long DamageTintSurfacePixels => _damageSurface == 0 ? 0 : 1;
    public PreviewRect AlertClipBounds { get; private set; }
    public OverlayBorder ActiveBorder => _scene.ActiveBorder;

    /// <summary>
    /// Maintenance-only device status check, shared and throttled to once a second
    /// on this UI thread. A known removal is reported immediately to every owner
    /// so idle overlays can switch to the compatibility path without drawing.
    /// </summary>
    public void CheckDeviceHealth()
    {
        VerifyThread();
        _device.CheckHealth();
    }

    public void Resize(PreviewSize size)
    {
        VerifyThread();
        size = new PreviewSize(Math.Clamp(size.Width, 1, 16384), Math.Clamp(size.Height, 1, 16384));
        if (_size == size) return;
        _size = size;
        ClearAlertCore();
        SetClip(_root, size.Width, size.Height);
        UpdateAlertGeometry();
        UpdateBorder();
        UpdateDamageTint();
        UploadScene(titleChanged: true, statsChanged: true);
        UpdateDamageIntensity();
        CommitChanges();
    }

    public void SetScene(OverlayScene scene)
    {
        VerifyThread();
        ArgumentNullException.ThrowIfNull(scene);
        if (SceneEquals(_scene, scene)) return;
        bool titleChanged = !TitleEquals(_scene, scene);
        bool statsChanged = _scene.Font.Family != scene.Font.Family || _scene.Font.Style != scene.Font.Style
            || _scene.StatsStyle != scene.StatsStyle || !_scene.Stats.SequenceEqual(scene.Stats);
        // Placement can depend on both blocks. Colour/intensity changes cannot.
        bool layoutChanged = _scene.TitlePosition != scene.TitlePosition || _scene.StatsStyle != scene.StatsStyle
            || _scene.Font != scene.Font || _scene.Title != scene.Title || _scene.Subtitle != scene.Subtitle
            || _scene.SubtitlePlacement != scene.SubtitlePlacement || _scene.SubtitleFontSize != scene.SubtitleFontSize
            || _scene.ShowTitle != scene.ShowTitle || _scene.CycleSkipped != scene.CycleSkipped || _scene.Stats.Count != scene.Stats.Count;
        titleChanged |= layoutChanged;
        statsChanged |= layoutChanged;
        bool alertBoundsChanged = _scene.AlertBounds != scene.AlertBounds;
        bool borderChanged = _scene.ActiveBorder != scene.ActiveBorder;
        bool damageChanged = _scene.DamageTint != scene.DamageTint;
        _scene = scene;
        if (_size.Width == 0) return;
        UploadScene(titleChanged, statsChanged);
        if (alertBoundsChanged) UpdateAlertGeometry();
        if (borderChanged) UpdateBorder();
        if (damageChanged) UpdateDamageTint();
        UpdateDamageIntensity();
        CommitChanges();
    }

    public void SetOpacity(double opacity)
    {
        VerifyThread();
        opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;
        if (_opacity == opacity) return;
        _opacity = opacity;
        NativeCompositionInterop.SetOpacity(_rootEffect, _visible ? (float)opacity : 0);
        CommitChanges();
    }

    public void SetVisible(bool visible)
    {
        VerifyThread();
        if (_visible == visible) return;
        _visible = visible;
        if (!visible) ClearAlertCore();
        NativeCompositionInterop.SetOpacity(_rootEffect, visible ? (float)_opacity : 0);
        CommitChanges();
    }

    public void ShowAlert(PreviewAlert alert)
    {
        VerifyThread();
        ArgumentNullException.ThrowIfNull(alert);
        if (!_visible || _size.Width == 0) return;
        alert = alert.Normalize();
        if (alert.Intensity <= 0) { ClearAlerts(); return; }
        if (_alertColor != alert.Color || _alertSurface == 0)
        {
            // A zoomed preview can cover tens of millions of pixels. Two 1px
            // assets are scaled by composition; four edges share the same pixel.
            using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
            var color = Color.FromArgb(unchecked((int)alert.Color));
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.Clear(color);
            ReplaceSurface(ref _alertSurface, _topVisual, bitmap);
            SetContent(_bottomVisual, _alertSurface);
            SetContent(_leftVisual, _alertSurface);
            SetContent(_rightVisual, _alertSurface);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.Clear(Color.FromArgb(color.A / 3, color));
            ReplaceSurface(ref _tintSurface, _tintVisual, bitmap);
            _alertColor = alert.Color;
        }

        nint pulse = 0, shake = 0;
        try
        {
            pulse = CreateAnimation(_device.Composition);
            float intensity = (float)alert.Intensity;
            if (alert.ReducedMotion)
                AddCubic(pulse, 0, intensity);
            else
            {
                // One monotonic finite fade avoids repeated high-contrast flashing.
                AddCubic(pulse, 0, intensity, (float)(-intensity / alert.DurationSeconds));
            }
            EndAnimation(pulse, alert.DurationSeconds, 0);
            SetOpacityAnimation(_alertEffect, pulse);
            if (alert.ShakePixels > 0)
            {
                shake = CreateAnimation(_device.Composition);
                // Move only the alert graphic within the root clip, never the HWND,
                // persisted geometry, game destination rectangle, or title hit targets.
                AddSinusoidal(shake, 0, (float)alert.ShakePixels, 9, 0);
                EndAnimation(shake, alert.DurationSeconds, 0);
                SetOffsetAnimation(_alertVisual, shake);
            }
            else SetOffset(_alertVisual, 0);
            _alertActive = true;
            CommitChanges();
        }
        finally { Release(ref shake); Release(ref pulse); }
    }

    public void ClearAlerts()
    {
        VerifyThread();
        if (!_alertActive) return;
        ClearAlertCore();
        CommitChanges();
    }

    /// <summary>Only for explicit validation; ordinary UI updates never wait for DWM.</summary>
    public void WaitForPendingCommit()
    {
        VerifyThread();
        WaitForCommit(_device.Composition);
    }

    private void ClearAlertCore()
    {
        if (!_alertActive) return;
        NativeCompositionInterop.SetOpacity(_alertEffect, 0);
        SetOffset(_alertVisual, 0);
        _alertActive = false;
    }

    private void UpdateAlertGeometry()
    {
        var requested = _scene.AlertBounds ?? new PreviewRect(0, 0, _size.Width, _size.Height);
        int left = (int)Math.Clamp((long)requested.X, 0, _size.Width);
        int top = (int)Math.Clamp((long)requested.Y, 0, _size.Height);
        int right = (int)Math.Clamp((long)requested.X + Math.Max(0, requested.Width), 0, _size.Width);
        int bottom = (int)Math.Clamp((long)requested.Y + Math.Max(0, requested.Height), 0, _size.Height);
        int width = Math.Max(0, right - left), height = Math.Max(0, bottom - top);
        AlertClipBounds = new PreviewRect(left, top, width, height);
        // The clip is on a fixed parent. Shaking the child can never move the clip
        // over the active-client border, even when it is only one pixel thick.
        SetOffset(_alertClipVisual, left);
        SetOffsetY(_alertClipVisual, top);
        SetClip(_alertClipVisual, width, height);
        float border = Math.Min(3, Math.Min(width, height) / 2f);
        SetScaleAndOffset(_tintVisual, width, height, 0, 0);
        SetScaleAndOffset(_topVisual, width, border, 0, 0);
        SetScaleAndOffset(_bottomVisual, width, border, 0, height - border);
        SetScaleAndOffset(_leftVisual, border, Math.Max(0, height - 2 * border), 0, border);
        SetScaleAndOffset(_rightVisual, border, Math.Max(0, height - 2 * border), width - border, border);
    }

    private void UpdateDamageTint()
    {
        SetScaleAndOffset(_damageVisual, _size.Width, _size.Height, 0, 0);
        if (_scene.DamageTint is not { } tint)
        {
            SetContent(_damageVisual, 0);
            return; // Keep the single colour pixel for the next on phase.
        }
        if (_damageColor != tint || _damageSurface == 0)
        {
            using var pixel = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(pixel)) graphics.Clear(Color.FromArgb(unchecked((int)tint)));
            ReplaceSurface(ref _damageSurface, _damageVisual, pixel);
            _damageColor = tint;
        }
        else SetContent(_damageVisual, _damageSurface);
    }

    private void UpdateDamageIntensity()
    {
        float amount = (float)Math.Clamp(_scene.DamageFlashIntensity, 0, 1);
        NativeCompositionInterop.SetOpacity(_damageEffect, amount);
        float title = _scene.TitleColor.HasValue ? amount : 0;
        NativeCompositionInterop.SetOpacity(_titleEffect, 1 - title);
        NativeCompositionInterop.SetOpacity(_flashTitleEffect, title);
    }

    private void UpdateBorder()
    {
        var border = _scene.ActiveBorder;
        if (border == null)
        {
            NativeCompositionInterop.SetOpacity(_borderEffect, 0);
            return; // Retain the pixel for the next selection of this client.
        }
        if (_borderColor != border.Color || _borderSurface == 0)
        {
            using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.Clear(Color.FromArgb(unchecked((int)border.Color)));
            ReplaceSurface(ref _borderSurface, _borderTop, bitmap);
            SetContent(_borderBottom, _borderSurface);
            SetContent(_borderLeft, _borderSurface);
            SetContent(_borderRight, _borderSurface);
            _borderColor = border.Color;
        }
        var inner = border.InnerBounds;
        int left = Math.Clamp(inner.X, 0, _size.Width), top = Math.Clamp(inner.Y, 0, _size.Height);
        int right = (int)Math.Clamp((long)inner.X + Math.Max(0, inner.Width), left, _size.Width);
        int bottom = (int)Math.Clamp((long)inner.Y + Math.Max(0, inner.Height), top, _size.Height);
        SetScaleAndOffset(_borderTop, _size.Width, top, 0, 0);
        SetScaleAndOffset(_borderBottom, _size.Width, _size.Height - bottom, 0, bottom);
        SetScaleAndOffset(_borderLeft, left, bottom - top, 0, top);
        SetScaleAndOffset(_borderRight, _size.Width - right, bottom - top, right, top);
        NativeCompositionInterop.SetOpacity(_borderEffect, 1);
    }

    private void UploadScene(bool titleChanged, bool statsChanged)
    {
        if (titleChanged)
        {
            using var title = OverlaySceneRasterizer.RenderAsset(_scene with { TitleColor = null }, _size, drawStats: false);
            UpdateAsset(ref _sceneSurface, _sceneVisual, title, ref _titlePixels);
            // Two bounded text assets cross-fade using retained visual opacity.
            // Animation ticks never rerasterize glyphs or upload game pixels.
            using var flashTitle = _scene.TitleColor.HasValue ? OverlaySceneRasterizer.RenderAsset(
                _scene with { DamageFlashIntensity = 1 }, _size, drawStats: false) : null;
            UpdateAsset(ref _flashTitleSurface, _flashTitleVisual, flashTitle, ref _flashTitlePixels);
        }
        if (statsChanged)
        {
            using var stats = OverlaySceneRasterizer.RenderAsset(_scene, _size, drawTitle: false);
            UpdateAsset(ref _statsSurface, _statsVisual, stats, ref _statsPixels);
        }
    }

    private void UpdateAsset(ref nint surface, nint visual, OverlaySceneRasterizer.Asset asset, ref long pixels)
    {
        if (asset == null)
        {
            SetContent(visual, 0);
            Release(ref surface);
            pixels = 0;
            return;
        }
        ReplaceSurface(ref surface, visual, asset.Bitmap);
        SetOffset(visual, asset.X);
        SetOffsetY(visual, asset.Y);
        pixels = (long)asset.Bitmap.Width * asset.Bitmap.Height;
    }

    private void ReplaceSurface(ref nint stored, nint visual, Bitmap bitmap)
    {
        nint replacement = CreateSurface(_device.Composition, bitmap.Width, bitmap.Height);
        try
        {
            Upload(replacement, _device.Factory, bitmap);
            SetContent(visual, replacement);
            Release(ref stored);
            stored = replacement;
            replacement = 0;
            SurfaceUploadCount++;
        }
        finally { Release(ref replacement); }
    }

    private void CommitChanges() { Commit(_device.Composition); CommitCount++; }
    private static bool TitleEquals(OverlayScene left, OverlayScene right) =>
        left.TitlePosition == right.TitlePosition &&
        left.Title == right.Title && left.TitleColor == right.TitleColor && left.ShowTitle == right.ShowTitle && left.Subtitle == right.Subtitle && left.SubtitleColor == right.SubtitleColor &&
        left.SubtitlePlacement == right.SubtitlePlacement && left.SubtitleFontSize == right.SubtitleFontSize &&
        left.Font == right.Font && left.CycleSkipped == right.CycleSkipped && left.MarkerStyle == right.MarkerStyle &&
        left.MarkerColor == right.MarkerColor;
    private static bool SceneEquals(OverlayScene left, OverlayScene right) =>
        ReferenceEquals(left, right) || TitleEquals(left, right) &&
        left.AlertBounds == right.AlertBounds && left.ActiveBorder == right.ActiveBorder && left.DamageTint == right.DamageTint && left.StatsStyle == right.StatsStyle &&
        left.DamageFlashIntensity == right.DamageFlashIntensity &&
        (ReferenceEquals(left.Stats, right.Stats) || left.Stats.SequenceEqual(right.Stats));

    private void VerifyThread()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_threadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Composition overlays must be used on their owning UI thread.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_target != 0 && _device != null)
            {
                try { SetRoot(_target, 0); Commit(_device.Composition); }
                catch (System.Runtime.InteropServices.COMException) { /* Device loss: release the detached resources below. */ }
            }
        }
        finally
        {
            Release(ref _sceneSurface); Release(ref _statsSurface); Release(ref _alertSurface); Release(ref _tintSurface);
            Release(ref _damageSurface); Release(ref _damageVisual);
            Release(ref _damageEffect); Release(ref _titleEffect); Release(ref _flashTitleEffect);
            Release(ref _flashTitleSurface); Release(ref _flashTitleVisual);
            Release(ref _sceneVisual); Release(ref _statsVisual); Release(ref _alertVisual); Release(ref _alertClipVisual); Release(ref _root);
            Release(ref _tintVisual); Release(ref _topVisual); Release(ref _bottomVisual); Release(ref _leftVisual); Release(ref _rightVisual);
            Release(ref _rootEffect); Release(ref _alertEffect); Release(ref _target);
            Release(ref _borderSurface); Release(ref _borderTop); Release(ref _borderBottom);
            Release(ref _borderLeft); Release(ref _borderRight); Release(ref _borderRoot); Release(ref _borderEffect);
            _device?.ReleaseOwner(); _device = null;
        }
    }

    private sealed class SharedDevice
    {
        [ThreadStatic] private static SharedDevice _current;
        private nint _d3d, _dxgi, _composition, _factory;
        private int _owners;
        private int _removedReason;
        private long _lastHealthCheck = -1;
        private Func<int> _healthProbe;
        internal nint Composition => _composition;
        internal nint Factory => _factory;

        internal static SharedDevice Acquire()
        {
            if (_current == null || _current._removedReason < 0) _current = new SharedDevice();
            _current._owners++;
            return _current;
        }

        private SharedDevice()
        {
            nint context = 0;
            try
            {
                // Hardware only: a machine that cannot initialize this path retains
                // the compatibility renderer rather than silently paying WARP cost.
                Check(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out _d3d, out _, out context));
                _dxgi = Query(_d3d, DxgiDeviceId);
                Check(DCompositionCreateDevice(_dxgi, CompositionDeviceId, out _composition));
                Check(D2D1CreateFactory(0, D2DFactoryId, 0, out _factory));
                _healthProbe = () => GetDeviceRemovedReason(_d3d);
            }
            catch { ReleaseResources(); throw; }
            finally { Release(ref context); }
        }

        internal void ReleaseOwner()
        {
            if (--_owners != 0) return;
            // Owners of a removed device can outlive its replacement.
            if (ReferenceEquals(_current, this)) _current = null;
            ReleaseResources();
        }

        internal void CheckHealth()
        {
            if (_removedReason < 0) Check(_removedReason);
            long now = Environment.TickCount64;
            if (_lastHealthCheck >= 0 && now - _lastHealthCheck < 1000) return;
            _lastHealthCheck = now;
            _removedReason = _healthProbe();
            if (_removedReason >= 0) return;
            // Keep this device alive for its existing owners to dispose, but
            // ensure newly opened previews can acquire a fresh hardware device.
            if (ReferenceEquals(_current, this)) _current = null;
            Check(_removedReason);
        }
        private void ReleaseResources()
        { Release(ref _factory); Release(ref _composition); Release(ref _dxgi); Release(ref _d3d); }
    }
}
