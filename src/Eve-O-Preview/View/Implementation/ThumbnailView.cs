//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Input;
using EveOPreview.Mediator.Messages;
using EveOPreview.Preview;
using EveOPreview.Services;
using EveOPreview.Services.Interop;
using EveOPreview.View.CustomControl;
using EveOPreview.View.Rendering;
using MediatR;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace EveOPreview.View;

/// <summary>
/// Avalonia owns the top-level lifetime and input. Persisted/native geometry remains
/// physical pixels; WindowsPreviewWindowAdapter is the only pixel/DIP boundary.
/// The image HWND and separately owned overlay HWND never change on refresh.
/// </summary>
public abstract class ThumbnailView : Window, IThumbnailView, IDisposable
{
    private ThumbnailOverlay _overlay;
    private readonly WindowsPreviewWindowAdapter _native;
    private readonly IThumbnailConfiguration _config;
    private readonly IThumbnailManager _thumbnailManager;
    private readonly IMediator _mediator;
    private readonly IGlobalPointerInput _keyboardMouseEvents;
    private readonly DispatcherTimer holdRightClickToMoveTimer;
    private readonly ContextMenu thumbnailContextMenu;
    private readonly MenuItem _skipItem;
    private readonly ThumbnailSnapSession _snap = new();
    private readonly List<ThumbnailSnapTarget> _snapTargets = new();
    private ThumbnailSnapGuideWindow _snapGuides;
    private bool _isOverlayVisible;
    private bool _isTopMost;
    private bool _isHighlightEnabled;
    private bool _isHighlightRequested;
    private bool _isHighlightChanged;
    private bool _isLocationChanged = true;
    private bool _isSizeChanged = true;
    private bool _menuOpening;
    private bool _menuHoverExitPending;
    private bool _isZoomed;
    private bool _nativeInteraction;
    private bool _dpiTransition;
    private bool _disposed;
    private int _geometryWriteDepth;
    private int _highlightWidth;
    private Color _highlightColor;
    private double _opacity = .1;
    private MouseMode _customMouseModeActive;
    private double _thumbnailRatioAtStartOfResize = 1;
    private Point _rightClickStartPosition;
    private Point _baseMousePosition;
    private Point _dragPointerOrigin;
    private Point _dragWindowOrigin;
    private Size _dragClientSize;
    private Size _baseZoomSize;
    private Point _baseZoomLocation;
    private Size _baseZoomMaximumSize;
    private Size _minimumSize = new(64, 64);
    private Size _maximumSize;
    private Size _pixelClientSize;
    private FontSettings _titleFontSettings;

    protected ThumbnailView(IWindowManager windowManager, IThumbnailConfiguration config,
        IThumbnailManager thumbnailManager, IMediator mediator, IGlobalPointerInput keyboardMouseEvents)
    {
        WindowManager = windowManager;
        _config = config;
        _thumbnailManager = thumbnailManager;
        _mediator = mediator;
        _keyboardMouseEvents = keyboardMouseEvents;
        ShowActivated = false;
        ShowInTaskbar = false;
        CanMinimize = false;
        CanMaximize = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SystemDecorations = SystemDecorations.Full;
        Background = Brushes.Black;
        Content = ImageSurface;
        _native = new(this);
        Win32Properties.AddWndProcHookCallback(this, NativeMessages);
        ClientSize = new(153, 89);
        _overlay = new(this);
        _native.SetOpacity(_opacity);
        _overlay.Opacity = .55;

        holdRightClickToMoveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        holdRightClickToMoveTimer.Tick += holdRightClickToMoveTimer_Tick;
        thumbnailContextMenu = new ContextMenu();
        thumbnailContextMenu.Items.Add(CreateMenuItem("menuMinimize", "Minimize", menuMinimize_Click));
        thumbnailContextMenu.Items.Add(CreateMenuItem("minimizeAllToolStripMenuItem", "Minimize All", minimizeAllToolStripMenuItem_Click));
        _skipItem = CreateMenuItem("menuCycleSkip", "Skip while cycling", (_, _) =>
            _ = _mediator.Send(new SetClientCycleSkipped(Title, !_config.IsClientCycleSkipped(Title))));
        ToolTip.SetTip(_skipItem, "Skip this character in all cycle groups for this session. Its thumbnail stays clickable.");
        thumbnailContextMenu.Items.Add(_skipItem);
        thumbnailContextMenu.Items.Add(CreateMenuItem("menuReposition", "Move", menuReposition_Click));
        var resize = CreateMenuItem("resizeThumbnailToolStripMenuItem", "Resize", resizeThumbnailToolStripMenuItem_Click);
        ToolTip.SetTip(resize, "Hold Shift to maintain aspect ratio while resizing");
        thumbnailContextMenu.Items.Add(resize);
        NativeMenuTheme.Track(thumbnailContextMenu, thumbnail: true);
        thumbnailContextMenu.Opening += (_, _) => PrepareContextMenu();
        thumbnailContextMenu.Opened += (_, _) => PrepareContextMenu();
        thumbnailContextMenu.AddHandler(PointerReleasedEvent, (_, _) => holdRightClickToMoveTimer.Stop(),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        thumbnailContextMenu.Closed += (_, _) =>
        {
            holdRightClickToMoveTimer.Stop();
            if (_menuHoverExitPending)
            {
                _menuHoverExitPending = false;
                ReleaseHoverOutside();
            }
        };
        PointerPressed += PointerPressed_Handler;
        PointerReleased += MouseUp_Handler;
        PointerEntered += MouseEnter_Handler;
        PointerExited += MouseLeave_Handler;
        PositionChanged += (_, _) => Move_Handler(this, EventArgs.Empty);
        Resized += (_, _) =>
        {
            _isSizeChanged = true;
            if (_nativeInteraction && !_dpiTransition && _geometryWriteDepth == 0)
            {
                _pixelClientSize = _native.ClientSize;
                Resize_Handler(this, EventArgs.Empty);
            }
            if (IsActive) RefreshAppearance();
        };
        ScalingChanged += (_, _) =>
        {
            if (_pixelClientSize.IsEmpty) return;
            SetGeometry(() => { ApplySizeLimits(); _native.ClientSize = _pixelClientSize; });
            if (IsActive) RefreshAppearance();
        };
        Closed += (_, _) => ReleaseResources();
        _config.CycleSkipChanged += RefreshCycleSkipIndicator;
    }

    internal Canvas ImageSurface { get; } = new() { Background = Brushes.Transparent };
    public IntPtr Handle => _native.Handle;
    public bool IsDisposed => _disposed;
    public bool Visible => IsVisible;
    public event EventHandler Disposed;
    public IWindowManager WindowManager { get; }
    public IntPtr Id { get; set; }
    public new bool IsActive { get; set; }
    public bool IsOverlayEnabled { get; set; }
    public bool IsContextMenuOpen => _menuOpening || thumbnailContextMenu.IsOpen;
    public bool IsInteracting => _nativeInteraction || _customMouseModeActive != MouseMode.Disabled;
    public OverlayCapabilities GraphicsCapabilities => _overlay.GraphicsCapabilities;
    public OverlayRendererKind OverlayRenderer => _overlay.RendererKind;

    public new string Title
    {
        get => base.Title ?? "";
        set
        {
            base.Title = value;
            _overlay?.SetOverlayLabel(value.Replace("EVE - ", ""));
            RefreshCycleSkipIndicator();
        }
    }

    public FontSettings TitleFontSettings
    {
        get => _titleFontSettings;
        set { _titleFontSettings = value; _overlay.SetOverlayFont(value); }
    }

    public Point ThumbnailLocation { get => Location; set => Location = value; }
    public Size ThumbnailSize { get => ClientSize; set => ClientSize = value; }
    public Point Location
    {
        get => _native.Location;
        set => SetGeometry(() => _native.Location = value);
    }
    public new Size ClientSize
    {
        get => _native.ClientSize;
        set => SetGeometry(() => { _pixelClientSize = ClampSize(value); _native.ClientSize = _pixelClientSize; });
    }
    public Size Size
    {
        get => _native.Size;
        set => SetGeometry(() => { _native.Size = value; _pixelClientSize = _native.ClientSize; });
    }
    public new int Width { get => Size.Width; set => Size = new(value, Size.Height); }
    public new int Height { get => Size.Height; set => Size = new(Size.Width, value); }
    public new System.Drawing.Rectangle Bounds
    {
        get => new(Location, Size);
        set { Location = value.Location; Size = value.Size; }
    }
    public bool TopMost { get => Topmost; set => SetTopMost(value); }
    public new double Opacity { get => _opacity; set => SetOpacity(value); }
    public Size MinimumSize { get => _minimumSize; set { _minimumSize = value; ApplySizeLimits(); } }
    public Size MaximumSize { get => _maximumSize; set { _maximumSize = value; ApplySizeLimits(); } }
    public Color BackColor
    {
        get => Background is SolidColorBrush brush ? Color.FromArgb(unchecked((int)brush.Color.ToUInt32())) : Color.Black;
        set => Background = new SolidColorBrush(Avalonia.Media.Color.FromUInt32(unchecked((uint)value.ToArgb())));
    }
    public Point PointToScreen(Point point) => _native.PointToScreen(point);
    public Point PointToClient(Point point) => _native.PointToClient(point);

    public Action<IntPtr> ThumbnailResized { get; set; }
    public Action<IntPtr> ThumbnailMoved { get; set; }
    public Action<IntPtr> ThumbnailFocused { get; set; }
    public Action<IntPtr> ThumbnailLostFocus { get; set; }
    public Action<IntPtr> ThumbnailActivated { get; set; }
    public Action<IntPtr, bool> ThumbnailDeactivated { get; set; }

    private void SetGeometry(Action update)
    {
        _geometryWriteDepth++;
        try { update(); }
        finally { _geometryWriteDepth--; }
        _isLocationChanged = _isSizeChanged = true;
    }

    private Size ClampSize(Size value) => new(
        Math.Clamp(value.Width, Math.Max(1, _minimumSize.Width), _maximumSize.Width > 0 ? Math.Max(_minimumSize.Width, _maximumSize.Width) : int.MaxValue),
        Math.Clamp(value.Height, Math.Max(1, _minimumSize.Height), _maximumSize.Height > 0 ? Math.Max(_minimumSize.Height, _maximumSize.Height) : int.MaxValue));

    private void ApplySizeLimits()
    {
        double scale = RenderScaling;
        MinWidth = Math.Max(1, _minimumSize.Width) / scale;
        MinHeight = Math.Max(1, _minimumSize.Height) / scale;
        MaxWidth = _maximumSize.Width > 0 ? Math.Max(_minimumSize.Width, _maximumSize.Width) / scale : double.PositiveInfinity;
        MaxHeight = _maximumSize.Height > 0 ? Math.Max(_minimumSize.Height, _maximumSize.Height) / scale : double.PositiveInfinity;
    }

    public void SetSizeLimitations(Size minimumSize, Size maximumSize)
    {
        _minimumSize = minimumSize;
        _maximumSize = maximumSize;
        ApplySizeLimits();
    }

    public override void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _geometryWriteDepth++;
        try { base.Show(); _native.EnsureStyles(); }
        finally { _geometryWriteDepth--; }
        IsActive = true;
        _isLocationChanged = _isSizeChanged = true;
        _isOverlayVisible = false;
        Refresh(true);
    }

    public override void Hide()
    {
        thumbnailContextMenu.Close();
        holdRightClickToMoveTimer.Stop();
        ExitCustomMouseMode();
        _nativeInteraction = false;
        ClearSnapGuides();
        IsActive = false;
        _isOverlayVisible = false;
        _overlay.Hide();
        base.Hide();
    }

    public new virtual void Close()
    {
        if (_disposed) return;
        ReleaseResources();
        base.Close();
    }

    public void Dispose() { Close(); GC.SuppressFinalize(this); }
    private void ReleaseResources()
    {
        if (_disposed) return;
        _disposed = true;
        Dispose(true);
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        IsActive = false;
        ExitCustomMouseMode();
        thumbnailContextMenu.Close();
        holdRightClickToMoveTimer.Stop();
        holdRightClickToMoveTimer.Tick -= holdRightClickToMoveTimer_Tick;
        _config.CycleSkipChanged -= RefreshCycleSkipIndicator;
        _overlay.Dispose();
        ClearSnapGuides();
        Win32Properties.RemoveWndProcHookCallback(this, NativeMessages);
        _native.Dispose();
    }

    public bool IsKnownHandle(IntPtr handle) => handle != IntPtr.Zero &&
        (Id == handle || Handle == handle || _overlay.Handle == handle ||
         (IsContextMenuOpen && TopLevel.GetTopLevel(thumbnailContextMenu)?.TryGetPlatformHandle()?.Handle == handle));

    public void SetOpacity(double opacity)
    {
        if (opacity >= .9) opacity = 1;
        if (Math.Abs(opacity - _opacity) < .1) return;
        _native.SetOpacity(opacity);
        _overlay.Opacity = opacity > .8 ? 1 : 1 - (1 - opacity) / 2;
        _opacity = opacity;
    }

    public void SetFrames(bool enable)
    {
        var decorations = enable ? SystemDecorations.Full : SystemDecorations.None;
        if (SystemDecorations == decorations) return;
        // A frame changes only outer size. Never save a transient framework client
        // rectangle as the user's requested image size (BUG-014).
        Size client = ClientSize;
        Point location = Location;
        SetGeometry(() =>
        {
            SystemDecorations = decorations;
            _native.EnsureStyles();
            _native.ClientSize = client;
            _native.Location = location;
        });
        if (IsActive) RefreshAppearance();
    }

    public void SetTopMost(bool enableTopmost)
    {
        if (_isTopMost == enableTopmost) return;
        _overlay.TopMost = enableTopmost;
        Topmost = enableTopmost;
        _isTopMost = enableTopmost;
    }

    public bool RestoreAndBringToFront()
    {
        if (!IsActive || IsContextMenuOpen) return true;
        SetTopMost(true);
        _isOverlayVisible = true;
        _overlay.RestoreGraphicsVisibility();
        if (!_native.Restore(topmost: true, raise: true)) return false;
        const uint flags = InteropConstants.SWP_NOMOVE | InteropConstants.SWP_NOSIZE
            | InteropConstants.SWP_NOACTIVATE | InteropConstants.SWP_SHOWWINDOW | InteropConstants.SWP_NOOWNERZORDER;
        if (User32NativeMethods.IsIconic(_overlay.Handle))
            User32NativeMethods.ShowWindow(_overlay.Handle, InteropConstants.SW_SHOWNOACTIVATE);
        return User32NativeMethods.SetWindowPos(_overlay.Handle, InteropConstants.HWND_TOPMOST, 0, 0, 0, 0, flags);
    }

    public void SetDefaultBorderColor() { _isHighlightChanged = true; }
    public void SetHighlight() => SetHighlight(_config.EnableActiveClientHighlight, _config.ActiveClientHighlightThickness);
    public void SetHighlight(bool enabled, int width)
    {
        Color color = _config.PerClientActiveClientHighlightColor.TryGetValue(Title, out var perClient)
            ? perClient : _config.ActiveClientHighlightColor;
        if (_isHighlightRequested == enabled && (!enabled || (_highlightWidth == width && _highlightColor == color))) return;
        _isHighlightRequested = enabled;
        if (enabled) { _highlightWidth = width; _highlightColor = color; }
        _isHighlightChanged = true;
    }
    public void ClearBorder() { SetHighlight(false, 0); RefreshAppearance(); }

    public void ZoomIn(ViewZoomAnchor anchor, int zoomFactor)
    {
        if (IsInteracting) return;
        if (_isZoomed) return;
        if (_baseZoomSize.IsEmpty) SaveWindowSizeAndLocation();
        _isZoomed = true;
        var original = Size;
        var client = ClientSize;
        var origin = Location;
        MaximumSize = System.Drawing.Size.Empty;
        ClientSize = new(client.Width * zoomFactor, client.Height * zoomFactor);
        var enlarged = Size;
        int column = (int)anchor % 3;
        int row = (int)anchor / 3;
        Location = new(origin.X - (enlarged.Width - original.Width) * column / 2,
            origin.Y - (enlarged.Height - original.Height) * row / 2);
    }

    public void ZoomOut()
    {
        if (!_isZoomed) return;
        _isZoomed = false;
        RestoreWindowSizeAndLocation();
    }

    public void Refresh(bool forceRefresh)
    {
        RefreshThumbnail(forceRefresh);
        if (forceRefresh) _overlay.MaintainGraphics();
        RefreshAppearance(forceRefresh);
    }
    public void Refresh() => RefreshAppearance();
    public void RefreshAppearance() => RefreshAppearance(false);
    private void RefreshAppearance(bool forceRefresh)
    {
        var renderer = _overlay.RendererKind;
        HighlightThumbnail(forceRefresh || _isSizeChanged || _isHighlightChanged);
        RefreshOverlay(forceRefresh || _isSizeChanged || _isLocationChanged);
        if (renderer != _overlay.RendererKind) HighlightThumbnail(true);
        _isSizeChanged = _isHighlightChanged = false;
    }
    protected abstract void RefreshThumbnail(bool forceRefresh);
    protected abstract void ResizeThumbnail(int baseWidth, int baseHeight, int highlightWidthTop,
        int highlightWidthRight, int highlightWidthBottom, int highlightWidthLeft);

    private void HighlightThumbnail(bool forceRefresh)
    {
        if (!forceRefresh && _isHighlightRequested == _isHighlightEnabled) return;
        _isHighlightEnabled = _isHighlightRequested;
        int width = ClientSize.Width;
        int height = ClientSize.Height;
        if (!_isHighlightRequested)
        {
            ResizeThumbnail(width, height, 0, 0, 0, 0);
            _overlay.SetActiveBorder(null);
            if (_overlay.RendererKind == OverlayRendererKind.Legacy) BackColor = System.Drawing.SystemColors.Control;
            return;
        }
        if (_overlay.RendererKind == OverlayRendererKind.NativeComposition)
        {
            int thickness = Math.Clamp(_highlightWidth, 0, Math.Min(width, height) / 2);
            _overlay.SetActiveBorder(new OverlayBorder(unchecked((uint)_highlightColor.ToArgb()),
                new(thickness, thickness, Math.Max(0, width - 2 * thickness), Math.Max(0, height - 2 * thickness))));
            if (_overlay.RendererKind != OverlayRendererKind.NativeComposition)
            {
                HighlightThumbnail(true); // Device loss must immediately restore the compatibility inset.
                return;
            }
            // Native selection is a retained frame. Activation never changes the
            // DWM destination, re-registers its image, or copies any game pixels.
            ResizeThumbnail(width, height, 0, 0, 0, 0);
            return;
        }
        int top = Math.Clamp(_highlightWidth, 0, height / 2);
        int actualHeight = height - 2 * top;
        int actualWidth = height > 0 ? (int)Math.Round(actualHeight * (double)width / height,
            MidpointRounding.AwayFromZero) : 0;
        int left = (width - actualWidth) / 2;
        int right = width - actualWidth - left;
        BackColor = _highlightColor;
        ResizeThumbnail(width, height, top, right, top, left);
        _overlay.SetAlertBounds(new(left, top, Math.Max(0, actualWidth), Math.Max(0, actualHeight)));
    }

    private void RefreshOverlay(bool forceRefresh)
    {
        if (_isOverlayVisible && !forceRefresh) return;
        _overlay.EnableOverlayLabel(IsOverlayEnabled);
        var bounds = _native.ClientScreenBounds;
        _overlay.Size = bounds.Size;
        _overlay.Location = bounds.Location;
        if (!_isOverlayVisible && IsVisible)
        {
            _overlay.Show();
            _isOverlayVisible = true;
        }
        _isLocationChanged = false;
        _overlay.Refresh();
    }

    private void RefreshCycleSkipIndicator()
    {
        if (_disposed || _overlay == null || _overlay.IsDisposed) return;
        _overlay.SetCycleSkipIndicator(_config.IsClientCycleSkipped(Title), _config.CycleSkipIndicatorStyle, _config.CycleSkipIndicatorColor);
    }

    public void SetOverlayRenderer(OverlayRendererKind kind)
    {
        if (_overlay.RendererKind == kind) return;
        var scene = _overlay.Scene;
        bool wasVisible = _isOverlayVisible;
        _overlay.Dispose();
        _overlay = new(this, kind);
        _overlay.SetOverlayLabel(Title.Replace("EVE - ", ""));
        if (_titleFontSettings != null) _overlay.SetOverlayFont(_titleFontSettings);
        _overlay.EnableOverlayLabel(IsOverlayEnabled);
        _overlay.SetStats(scene.Stats, scene.StatsStyle);
        _overlay.SetSubtitle(scene.Subtitle, scene.SubtitleColor, scene.SubtitlePlacement, scene.SubtitleFontSize);
        _overlay.SetDamageFlash(scene.TitleColor, scene.DamageTint, scene.DamageFlashIntensity);
        _overlay.SetTitlePosition(scene.TitlePosition);
        _overlay.SetAlertBounds(scene.AlertBounds);
        _overlay.TopMost = _isTopMost;
        _overlay.Opacity = _opacity > .8 ? 1 : 1 - (1 - _opacity) / 2;
        RefreshCycleSkipIndicator();
        _isOverlayVisible = false;
        _isHighlightChanged = true;
        if (wasVisible && IsActive) RefreshAppearance(true);
    }

    public void SetOverlayStats(IReadOnlyList<OverlayStat> stats) => _overlay.SetStats(stats);
    public void SetOverlayStats(IReadOnlyList<OverlayStat> stats, OverlayStatsStyle style) => _overlay.SetStats(stats, style);
    public void SetSystemName(string system, uint color, SubtitlePlacement placement = SubtitlePlacement.Below, float? fontSize = null)
        => _overlay.SetSubtitle(system, color, placement, fontSize);
    public void SetDamageTitleColor(uint? color) => _overlay.SetTitleColor(color);
    public void SetDamageFlash(uint? titleColor, uint? tint, double intensity = 1) => _overlay.SetDamageFlash(titleColor, tint, intensity);
    public void SetTitlePosition(OverlayPosition position) => _overlay.SetTitlePosition(position);
    public void ShowAlert(PreviewAlert alert) { if (IsActive) _overlay.ShowAlert(alert); }
    public void ClearAlerts() => _overlay.ClearAlerts();

    private IntPtr NativeMessages(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case 0x0021: // WM_MOUSEACTIVATE
                // Avalonia 11.3 invokes the multicast hook once, retaining only
                // its last return value. Preserve the adapter's noactivate result.
                handled = true;
                return new IntPtr(3);
            case 0x0201: // WM_LBUTTONDOWN
            case 0x0203: // WM_LBUTTONDBLCLK
            case 0x0204: // WM_RBUTTONDOWN
            case 0x0206: // WM_RBUTTONDBLCLK
            case 0x0207: // WM_MBUTTONDOWN
            case 0x0209: // WM_MBUTTONDBLCLK
                // Avalonia 11.3's Win32 input path can call SetFocus before it
                // dispatches PointerPressed, even on a WS_EX_NOACTIVATE window.
                // This surface has no focusable controls: dispatch its gestures
                // here so clicking the image never transiently focuses the host.
                var button = message is 0x0201 or 0x0203 ? PointerButtons.Left
                    : message is 0x0204 or 0x0206 ? PointerButtons.Right : PointerButtons.Middle;
                var point = new Point(unchecked((short)(lParam.ToInt64() & 0xffff)),
                    unchecked((short)((lParam.ToInt64() >> 16) & 0xffff)));
                var modifiers = _keyboardMouseEvents.Modifiers;
                MouseDownEventHandler(new(button, _keyboardMouseEvents.Buttons, point, modifiers), modifiers);
                handled = true;
                return IntPtr.Zero;
            case 0x0202: // WM_LBUTTONUP
            case 0x0205: // WM_RBUTTONUP
            case 0x0208: // WM_MBUTTONUP
                holdRightClickToMoveTimer.Stop();
                handled = true;
                return IntPtr.Zero;
            case 0x0231: // WM_ENTERSIZEMOVE
                _nativeInteraction = true;
                ZoomOut();
                _dragPointerOrigin = _keyboardMouseEvents.Position;
                _dragWindowOrigin = Location;
                _snap.Reset();
                break;
            case 0x0216 when _nativeInteraction: // WM_MOVING: replace the proposed native rectangle immediately.
                var rectangle = Marshal.PtrToStructure<NativeRectangle>(lParam);
                var pointer = _keyboardMouseEvents.Position;
                var raw = new PreviewRect(_dragWindowOrigin.X + pointer.X - _dragPointerOrigin.X,
                    _dragWindowOrigin.Y + pointer.Y - _dragPointerOrigin.Y,
                    rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
                var result = Snap(raw, (_keyboardMouseEvents.Modifiers & ShortcutKeys.Shift) != 0);
                rectangle = new(result.X, result.Y, result.X + result.Width, result.Y + result.Height);
                Marshal.StructureToPtr(rectangle, lParam, false);
                handled = true;
                return new IntPtr(1);
            case 0x0232: // WM_EXITSIZEMOVE
                _nativeInteraction = false;
                _snap.Reset();
                ClearSnapGuides();
                SaveWindowSizeAndLocation();
                ReleaseHoverOutside();
                break;
            case 0x02E0: // WM_DPICHANGED: never interpret monitor scaling as a user image resize.
                _dpiTransition = true;
                Size pixels = _pixelClientSize;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_disposed) return;
                    try
                    {
                        SetGeometry(() => { ApplySizeLimits(); _native.ClientSize = pixels; });
                        _pixelClientSize = pixels;
                        if (IsActive) RefreshAppearance();
                    }
                    finally { _dpiTransition = false; }
                }, DispatcherPriority.Loaded);
                break;
        }
        return IntPtr.Zero;
    }

    private void Move_Handler(object sender, EventArgs args)
    {
        _isLocationChanged = true;
        if (_geometryWriteDepth == 0 && _nativeInteraction) ThumbnailMoved?.Invoke(Id);
        if (IsActive) RefreshAppearance();
    }
    private void Resize_Handler(object sender, EventArgs args)
    {
        _isSizeChanged = true;
        if (_geometryWriteDepth == 0) ThumbnailResized?.Invoke(Id);
    }
    private void MouseEnter_Handler(object sender, EventArgs args)
    {
        if (IsContextMenuOpen || IsInteracting || _isZoomed) return;
        SaveWindowSizeAndLocation();
        ThumbnailFocused?.Invoke(Id);
    }
    private void MouseLeave_Handler(object sender, EventArgs args)
    {
        if (IsContextMenuOpen) { _menuHoverExitPending = true; return; }
        if (!IsInteracting) ThumbnailLostFocus?.Invoke(Id);
    }
    private void ReleaseHoverOutside()
    {
        if (_disposed || IsInteracting || IsContextMenuOpen) return;
        // PointerExited can arrive during a global/native resize. Once that
        // gesture finishes there may be no second exit event to release hover.
        var position = PointToClient(_keyboardMouseEvents.Position);
        if (!new System.Drawing.Rectangle(Point.Empty, ClientSize).Contains(position))
            ThumbnailLostFocus?.Invoke(Id);
    }
    private void MouseUp_Handler(object sender, EventArgs args) => holdRightClickToMoveTimer.Stop();
    private void PointerPressed_Handler(object sender, PointerPressedEventArgs args)
    {
        var position = args.GetPosition(this);
        var properties = args.GetCurrentPoint(this).Properties;
        var button = properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => PointerButtons.Left,
            PointerUpdateKind.RightButtonPressed => PointerButtons.Right,
            PointerUpdateKind.MiddleButtonPressed => PointerButtons.Middle,
            _ => PointerButtons.None
        };
        var point = new Point((int)Math.Round(position.X * RenderScaling), (int)Math.Round(position.Y * RenderScaling));
        MouseDownEventHandler(new(button, _keyboardMouseEvents.Buttons, point, _keyboardMouseEvents.Modifiers), _keyboardMouseEvents.Modifiers);
        args.Handled = true;
    }

    protected virtual void MouseDownEventHandler(GlobalPointerEventArgs args, ShortcutKeys modifierKeys)
    {
        if (IsInteracting) return;
        switch (args.Button)
        {
            case PointerButtons.Left when modifierKeys == ShortcutKeys.Control:
                ThumbnailDeactivated?.Invoke(Id, false);
                break;
            case PointerButtons.Left when modifierKeys == (ShortcutKeys.Control | ShortcutKeys.Shift):
                break;
            case PointerButtons.Left:
                ThumbnailActivated?.Invoke(Id);
                break;
            case PointerButtons.Right:
                _rightClickStartPosition = _keyboardMouseEvents.Position;
                _menuOpening = true;
                try
                {
                    PrepareContextMenu();
                    thumbnailContextMenu.Placement = PlacementMode.AnchorAndGravity;
                    // The pointer is in client pixels; menu padding/rows are in
                    // DIPs. Keep the click inside the first row at every scale.
                    // An empty rectangle loses its location in Avalonia's popup
                    // transform; a one-DIP anchor retains the actual click point.
                    thumbnailContextMenu.PlacementRect = new Rect(args.X / RenderScaling, args.Y / RenderScaling, 1, 1);
                    thumbnailContextMenu.HorizontalOffset = -30;
                    thumbnailContextMenu.VerticalOffset = -16;
                    thumbnailContextMenu.PlacementAnchor = Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.TopLeft;
                    thumbnailContextMenu.PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.BottomRight;
                    thumbnailContextMenu.Open(this);
                    holdRightClickToMoveTimer.Start();
                }
                finally { _menuOpening = false; }
                break;
        }
    }

    private void PrepareContextMenu()
    {
        _skipItem.Header = _config.IsClientCycleSkipped(Title) ? "Resume cycling this character" : "Skip while cycling";
        _skipItem.IsEnabled = !string.IsNullOrWhiteSpace(Title);
        NativeMenuTheme.ApplyThumbnailOrder(thumbnailContextMenu);
    }

    private MenuItem CreateMenuItem(string name, string title, EventHandler handler)
    {
        var item = new MenuItem { Name = name, Header = title };
        item.Click += (sender, args) => handler(sender, args);
        // Preserve the second right-click on the first action. Avalonia normally
        // invokes menu items with the primary button only.
        bool rightPressedHere = false;
        item.AddHandler(PointerPressedEvent, (sender, args) =>
        {
            rightPressedHere = args.GetCurrentPoint(item).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;
        }, RoutingStrategies.Tunnel);
        item.AddHandler(PointerReleasedEvent, (sender, args) =>
        {
            bool invoke = rightPressedHere && args.InitialPressMouseButton == MouseButton.Right && item.IsEnabled;
            rightPressedHere = false;
            if (!invoke) return;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            thumbnailContextMenu.Close();
            args.Handled = true;
        }, RoutingStrategies.Tunnel);
        return item;
    }

    private void SaveWindowSizeAndLocation()
    {
        _baseZoomSize = Size;
        _baseZoomLocation = Location;
        _baseZoomMaximumSize = MaximumSize;
    }
    private void RestoreWindowSizeAndLocation()
    {
        if (_baseZoomSize.IsEmpty) { SaveWindowSizeAndLocation(); return; }
        MaximumSize = _baseZoomMaximumSize;
        Size = _baseZoomSize;
        Location = _baseZoomLocation;
    }
    private void EnterCustomMouseMode(MouseMode mode, bool snapCursorPosition = true)
    {
        if (_disposed) return;
        ExitCustomMouseMode();
        ZoomOut();
        if (snapCursorPosition)
        {
            var client = ClientSize;
            var target = mode == MouseMode.Move ? new Point(client.Width / 2, client.Height / 2)
                : new Point(Math.Max(0, client.Width - 5), Math.Max(0, client.Height - 5));
            _keyboardMouseEvents.Position = PointToScreen(target);
        }
        _customMouseModeActive = mode;
        _baseMousePosition = _dragPointerOrigin = _keyboardMouseEvents.Position;
        _dragWindowOrigin = Location;
        _dragClientSize = ClientSize;
        _thumbnailRatioAtStartOfResize = _dragClientSize.Height > 0 ? (double)_dragClientSize.Width / _dragClientSize.Height : 1;
        _snap.Reset();
        _keyboardMouseEvents.MouseMove += ProcessCustomMouseMode;
        _keyboardMouseEvents.MouseUp += ExitCustomMouseMode;
    }
    private void ProcessCustomMouseMode(object sender, GlobalPointerEventArgs args) =>
        ProcessCustomMouseMode(args.Location, args.Modifiers);
    private void ProcessCustomMouseMode(Point current, ShortcutKeys modifiers)
    {
        _baseMousePosition = current;
        int dx = current.X - _dragPointerOrigin.X;
        int dy = current.Y - _dragPointerOrigin.Y;
        bool shift = (modifiers & ShortcutKeys.Shift) != 0;
        switch (_customMouseModeActive)
        {
            case MouseMode.Move:
                var size = Size;
                var snapped = Snap(new(_dragWindowOrigin.X + dx, _dragWindowOrigin.Y + dy, size.Width, size.Height), shift);
                Location = new(snapped.X, snapped.Y);
                _baseZoomLocation = Location;
                ThumbnailMoved?.Invoke(Id);
                break;
            case MouseMode.Resize:
                int width = _dragClientSize.Width + dx;
                int height = shift ? (int)Math.Round(width / _thumbnailRatioAtStartOfResize) : _dragClientSize.Height + dy;
                ClientSize = new(width, height);
                ThumbnailResized?.Invoke(Id);
                break;
        }
        RefreshAppearance();
    }
    private void ExitCustomMouseMode()
    {
        bool wasInteracting = _customMouseModeActive != MouseMode.Disabled;
        if (_customMouseModeActive != MouseMode.Disabled)
        {
            _keyboardMouseEvents.MouseMove -= ProcessCustomMouseMode;
            _keyboardMouseEvents.MouseUp -= ExitCustomMouseMode;
            _customMouseModeActive = MouseMode.Disabled;
            SaveWindowSizeAndLocation();
        }
        _snap.Reset();
        ClearSnapGuides();
        if (wasInteracting) ReleaseHoverOutside();
    }
    private void ExitCustomMouseMode(object sender, GlobalPointerEventArgs args) => ExitCustomMouseMode();
    private void holdRightClickToMoveTimer_Tick(object sender, EventArgs args)
    {
        holdRightClickToMoveTimer.Stop();
        if (_keyboardMouseEvents.Buttons != PointerButtons.Right) return;
        // Closing the popup must not perform deferred hover restoration halfway
        // through the transition to a global drag.
        _menuHoverExitPending = false;
        thumbnailContextMenu.Close();
        EnterCustomMouseMode(MouseMode.Move, false);
        _dragPointerOrigin = _rightClickStartPosition;
        ProcessCustomMouseMode(_keyboardMouseEvents.Position, _keyboardMouseEvents.Modifiers);
    }

    private PreviewRect Snap(PreviewRect raw, bool shift)
    {
        _snapTargets.Clear();
        if (_config.EnableThumbnailSnap && !shift && _thumbnailManager != null)
        {
            foreach (var view in _thumbnailManager.GetAllKnownClients().Values)
            {
                if (view.Id == Id || !view.IsActive || _config.IsThumbnailDisabled(view.Title)) continue;
                var location = view.ThumbnailLocation;
                var size = view is ThumbnailView native ? native.Size : view.ThumbnailSize;
                _snapTargets.Add(new(view.Id.ToInt64(), new(location.X, location.Y, size.Width, size.Height)));
            }
        }
        var result = _snap.Move(raw, _snapTargets, (int)Math.Round(8 * RenderScaling), (int)Math.Round(16 * RenderScaling),
            shift || !_config.EnableThumbnailSnap);
        if (result.VerticalGuide != null || result.HorizontalGuide != null)
        {
            _snapGuides ??= new ThumbnailSnapGuideWindow();
            _snapGuides.UpdateGuides(result.VerticalGuide, result.HorizontalGuide, this);
        }
        else ClearSnapGuides();
        return result.Bounds;
    }
    private void ClearSnapGuides() { _snapGuides?.Dispose(); _snapGuides = null; }

    private void menuMinimize_Click(object sender, EventArgs args) => _ = _mediator.Send(new MinimizeClient(Id));
    private void minimizeAllToolStripMenuItem_Click(object sender, EventArgs args) => _ = _mediator.Send(new MinimizeAllClients());
    private void menuReposition_Click(object sender, EventArgs args) => EnterCustomMouseMode(MouseMode.Move);
    private void resizeThumbnailToolStripMenuItem_Click(object sender, EventArgs args) => EnterCustomMouseMode(MouseMode.Resize);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeRectangle(int Left, int Top, int Right, int Bottom);
}

public enum MouseMode { Disabled, Move, Resize }
