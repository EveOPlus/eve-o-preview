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
using System.Threading.Tasks;
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
public abstract partial class ThumbnailView : Window, IThumbnailView, IDisposable
{
    // Temporary editors must remain above images and their separately owned labels.
    // Notify only on native stacking changes, never on an ordinary refresh tick.
    internal static event Action ZOrderChanged;
    // All thumbnail windows share the Avalonia UI thread. Own only the currently
    // open menu, and release the reference/subscription when it closes.
    private static ThumbnailView _openMenuOwner;
    private static ThumbnailView _pendingMenuSelectionOwner;
    private ThumbnailView _pendingSelectionTarget;
    private Point _pendingSelectionPoint;
    private int _menuSelectionGeneration;
    private ThumbnailOverlay _overlay;
    private readonly WindowsPreviewWindowAdapter _native;
    private readonly IThumbnailConfiguration _config;
    private readonly IThumbnailManager _thumbnailManager;
    private readonly IMediator _mediator;
    private readonly IGlobalPointerInput _keyboardMouseEvents;
    private readonly DispatcherTimer holdRightClickToMoveTimer;
    private readonly ContextMenu thumbnailContextMenu;
    private readonly MenuItem _skipItem;
    private readonly MenuItem _aspectLockItem;
    private readonly MenuItem _resetAspectItem;
    private readonly MenuItem _resizeMenu;
    private readonly MenuItem _resizeAllItem;
    private readonly MenuItem _legacyResizeItem;
    private readonly MenuItem _undockItem;
    private readonly Control[] _singleMenuItems;
    private readonly MenuItem _selectionMoveItem;
    private readonly MenuItem _selectionResizeItem;
    private readonly MenuItem _selectionResetAspectItem;
    private readonly MenuItem _selectionResetSizeItem;
    private readonly MenuItem _selectionSkipItem;
    private readonly MenuItem _undoItem;
    private bool? _selectionSkipState;
    private bool _updatingSelectionSkip;
    private bool _selectionMenuClick;
    private bool _groupInteraction;
    private bool _resizingSelection;
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
    private bool _menuOpeningReleasePending;
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
        _skipItem = CreateMenuItem("menuCycleSkip", "Skip while cycling", (_, _) => _ = ToggleClientCycleSkipped());
        ToolTip.SetTip(_skipItem, "Skip this character in all cycle groups for this session. Its thumbnail stays clickable.");
        thumbnailContextMenu.Items.Add(_skipItem);
        thumbnailContextMenu.Items.Add(CreateMenuItem("menuReposition", "Move", menuReposition_Click));
        _resizeAllItem = CreateMenuItem("menuResizeAll", "Resize all", (_, _) => BeginResizeAll());
        _legacyResizeItem = CreateMenuItem("resizeThumbnailToolStripMenuItem", "Resize", (_, _) => BeginResizeAll());
        thumbnailContextMenu.Items.Add(_resizeAllItem);
        var resize = _resizeMenu = new MenuItem { Name = "resizeThumbnailToolStripMenuItem", Header = "Resize" };
        resize.Items.Add(CreateMenuItem("menuResizeIndividual", "Resize individual", resizeThumbnailToolStripMenuItem_Click));
        _resetAspectItem = CreateMenuItem("menuResetAspectRatio", "Reset aspect ratio to client", (_, _) => ResetAspectRatio());
        resize.Items.Add(_resetAspectItem);
        _aspectLockItem = CreateMenuItem("menuLockAspectRatio", "Maintain aspect ratio", (_, _) =>
            _config.MaintainThumbnailAspectRatio = _aspectLockItem.IsChecked);
        _aspectLockItem.ToggleType = MenuItemToggleType.CheckBox;
        resize.Items.Add(_aspectLockItem);
        resize.Items.Add(CreateMenuItem("menuResetSize", "Reset to default size", (_, _) => RunThumbnailEdit(ResetDefaultSize)));
        thumbnailContextMenu.Items.Add(resize);
        _singleMenuItems = thumbnailContextMenu.Items.OfType<Control>().ToArray();
        _selectionMoveItem = CreateMenuItem("menuMoveSelected", "Move", (_, _) => BeginSelectionTransform(MouseMode.Move));
        _selectionResizeItem = CreateMenuItem("menuResizeSelected", "Resize", (_, _) => BeginSelectionTransform(MouseMode.Resize));
        _selectionResetAspectItem = CreateMenuItem("menuResetSelectedAspectRatio", "Reset Aspect Ratio", (_, _) => ResetSelectionAspectRatio());
        _selectionResetSizeItem = CreateMenuItem("menuResetSelectedSize", "Reset Size", (_, _) =>
        {
            var selected = SelectedThumbnails();
            PreserveSelectionForMenuClick(selected);
            RunThumbnailEdit(() => { foreach (var view in selected) view.ResetDefaultSize(); }, selected: true);
        });
        _selectionSkipItem = CreateMenuItem("menuCycleSkipSelected", "Skip Cycling", (_, _) => _ = ToggleSelectionCycleSkipped());
        _undockItem = CreateMenuItem("menuUndock", "Undock", async (_, _) =>
        {
            try { await _thumbnailManager.UndockThumbnail(Id); _undockItem.Header = "Undock"; }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Could not undock thumbnail");
                _undockItem.Header = "Undock failed - try again";
            }
        });
        _undoItem = CreateMenuItem("menuUndo", "Undo", (_, _) =>
        {
            PreserveSelectionForMenuClick(SelectedThumbnails());
            _ = _thumbnailManager.UndoThumbnailEdit();
        });
        NativeMenuTheme.Track(thumbnailContextMenu, thumbnail: true);
        thumbnailContextMenu.Opening += (_, _) => PrepareContextMenu();
        thumbnailContextMenu.Opened += (_, _) =>
        {
            if (_openMenuOwner != this) _openMenuOwner?.DismissContextMenu();
            _openMenuOwner = this;
            _keyboardMouseEvents.MouseUp -= DismissMenuOnOutsideClick;
            _keyboardMouseEvents.MouseUp += DismissMenuOnOutsideClick;
            PrepareContextMenu();
        };
        thumbnailContextMenu.AddHandler(PointerReleasedEvent, (_, _) => holdRightClickToMoveTimer.Stop(),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        thumbnailContextMenu.AddHandler(PointerPressedEvent, SelectThroughContextMenu, RoutingStrategies.Tunnel);
        thumbnailContextMenu.Closed += (_, _) =>
        {
            _menuOpeningReleasePending = false;
            if (_openMenuOwner == this) _openMenuOwner = null;
            _keyboardMouseEvents.MouseUp -= DismissMenuOnOutsideClick;
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
    public bool IsResizingAll { get; private set; }
    public bool IsSelected { get; private set; }
    private bool AspectLocked => _config.MaintainThumbnailAspectRatio;
    public bool IsInteracting => _groupInteraction || _nativeInteraction || _regionDockPending || _customMouseModeActive != MouseMode.Disabled;
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
    public Size ThumbnailSize
    {
        get => ClientSize;
        set { ClientSize = value; if (!_isZoomed && !IsInteracting) SaveWindowSizeAndLocation(); }
    }

    public void CancelInteraction()
    {
        CancelPendingMenuSelection();
        ExitCustomMouseMode();
        _nativeInteraction = false;
        _thumbnailManager?.CompleteThumbnailEdit(Id);
        IsResizingAll = false;
        ClearSnapGuides();
    }

    public void SetGroupInteraction(bool active)
    {
        _groupInteraction = active;
        if (!active && !_isZoomed) SaveWindowSizeAndLocation();
    }

    public void SetSelected(bool selected)
    {
        if (!selected) CancelPendingMenuSelection();
        if (IsSelected == selected) return;
        IsSelected = selected;
        if (!selected && thumbnailContextMenu.Items.Contains(_selectionMoveItem)) DismissContextMenu();
        _keyboardMouseEvents.MouseUp -= ClearSelectionOnOutsideClick;
        if (selected) _keyboardMouseEvents.MouseUp += ClearSelectionOnOutsideClick;
        _isHighlightChanged = true;
        if (!_disposed && IsActive) RefreshAppearance();
        _openMenuOwner?.PrepareContextMenu();
    }

    private void ClearSelectionOnOutsideClick(object sender, GlobalPointerEventArgs args)
    {
        if (!IsSelected || IsInteracting || _selectionMenuClick || _pendingMenuSelectionOwner != null) return;
        if (args.Button == PointerButtons.Right && _openMenuOwner?._menuOpeningReleasePending == true) return;
        if (args.Button == PointerButtons.Right && (args.Modifiers & ShortcutKeys.Shift) != 0) return;
        if (_openMenuOwner != null && (PopupContainsPoint(_openMenuOwner.thumbnailContextMenu, args.Location)
            || SubmenusContainPoint(_openMenuOwner.thumbnailContextMenu.Items.OfType<MenuItem>(), args.Location))) return;
        if (_thumbnailManager.GetAllKnownClients().Values.OfType<ThumbnailView>()
            .Any(view => view.IsSelected && view.IsActive && view.Bounds.Contains(args.Location))) return;
        _thumbnailManager.ClearThumbnailSelection();
    }
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
        CancelPendingMenuSelection();
        _thumbnailManager?.RemoveThumbnailSelection(Id);
        SetSelected(false);
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
        CancelPendingMenuSelection();
        _thumbnailManager?.RemoveThumbnailSelection(Id);
        SetSelected(false);
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
         (IsContextMenuOpen && (TopLevel.GetTopLevel(thumbnailContextMenu)?.TryGetPlatformHandle()?.Handle == handle
             || MenuContainsHandle(thumbnailContextMenu.Items.OfType<MenuItem>(), handle))));

    private static bool MenuContainsHandle(IEnumerable<MenuItem> items, IntPtr handle) => items.Any(item =>
        TopLevel.GetTopLevel(item)?.TryGetPlatformHandle()?.Handle == handle
        || MenuContainsHandle(item.Items.OfType<MenuItem>(), handle));

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
        enable &= _config.GetThumbnailRegion(Title) == null;
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
        ZOrderChanged?.Invoke();
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
        bool restored = User32NativeMethods.SetWindowPos(_overlay.Handle, InteropConstants.HWND_TOPMOST, 0, 0, 0, 0, flags);
        int error = Marshal.GetLastWin32Error();
        ZOrderChanged?.Invoke();
        Marshal.SetLastPInvokeError(error);
        return restored;
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
        if (IsInteracting || IsSelected) return;
        if (_isZoomed || _config.GetThumbnailRegion(Title) != null) return;
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
        bool enabled = IsSelected || _isHighlightRequested;
        int borderWidth = IsSelected ? Math.Max(3, (int)Math.Round(3 * RenderScaling)) : _highlightWidth;
        Color borderColor = IsSelected ? Color.Yellow : _highlightColor;
        if (!forceRefresh && enabled == _isHighlightEnabled) return;
        _isHighlightEnabled = enabled;
        int width = ClientSize.Width;
        int height = ClientSize.Height;
        if (!enabled)
        {
            ResizeThumbnail(width, height, 0, 0, 0, 0);
            _overlay.SetActiveBorder(null);
            if (_overlay.RendererKind == OverlayRendererKind.Legacy) BackColor = System.Drawing.SystemColors.Control;
            return;
        }
        if (_overlay.RendererKind == OverlayRendererKind.NativeComposition)
        {
            int thickness = Math.Clamp(borderWidth, 0, Math.Min(width, height) / 2);
            _overlay.SetActiveBorder(new OverlayBorder(unchecked((uint)borderColor.ToArgb()),
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
        int top = Math.Clamp(borderWidth, 0, height / 2);
        int actualHeight = height - 2 * top;
        int actualWidth = height > 0 ? (int)Math.Round(actualHeight * (double)width / height,
            MidpointRounding.AwayFromZero) : 0;
        int left = (width - actualWidth) / 2;
        int right = width - actualWidth - left;
        BackColor = borderColor;
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
            ZOrderChanged?.Invoke();
        }
        _isLocationChanged = false;
        _overlay.Refresh();
    }

    private void RefreshCycleSkipIndicator()
    {
        if (_disposed || _overlay == null || _overlay.IsDisposed) return;
        _overlay.SetCycleSkipIndicator(_config.IsClientCycleSkipped(Title), _config.CycleSkipIndicatorStyle, _config.CycleSkipIndicatorColor);
        if (IsSelected && IsContextMenuOpen && !_updatingSelectionSkip) PrepareSelectionMenu();
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
                _nativeMoving = false;
                IsResizingAll = false;
                ZoomOut();
                _thumbnailManager?.BeginThumbnailEdit(Id, ThumbnailEditKind.Geometry);
                _dragClientSize = ClientSize;
                _thumbnailRatioAtStartOfResize = (double)_dragClientSize.Width / _dragClientSize.Height;
                _dragPointerOrigin = _keyboardMouseEvents.Position;
                _dragWindowOrigin = Location;
                _snap.Reset();
                break;
            case 0x0216 when _nativeInteraction: // WM_MOVING: replace the proposed native rectangle immediately.
                if (!_nativeMoving) { _nativeMoving = true; BeginRegionDrag(); }
                var rectangle = Marshal.PtrToStructure<NativeRectangle>(lParam);
                var pointer = _keyboardMouseEvents.Position;
                UpdateRegionDrag(pointer, _keyboardMouseEvents.Modifiers);
                var raw = new PreviewRect(_dragWindowOrigin.X + pointer.X - _dragPointerOrigin.X,
                    _dragWindowOrigin.Y + pointer.Y - _dragPointerOrigin.Y,
                    rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
                var result = Snap(raw, (_keyboardMouseEvents.Modifiers & ShortcutKeys.Shift) != 0);
                _nativeMoveLocation = new(result.X, result.Y);
                rectangle = new(result.X, result.Y, result.X + result.Width, result.Y + result.Height);
                Marshal.StructureToPtr(rectangle, lParam, false);
                handled = true;
                return new IntPtr(1);
            case 0x0214 when _nativeInteraction: // WM_SIZING
                var proposed = Marshal.PtrToStructure<NativeRectangle>(lParam);
                int edge = wParam.ToInt32();
                var resized = ResizeBounds(new(proposed.Left, proposed.Top, proposed.Right - proposed.Left, proposed.Bottom - proposed.Top),
                    edge is 1 or 4 or 7, edge is 2 or 5 or 8, edge is 3 or 4 or 5, edge is 6 or 7 or 8,
                    (_keyboardMouseEvents.Modifiers & ShortcutKeys.Shift) != 0);
                Marshal.StructureToPtr(new NativeRectangle(resized.X, resized.Y, resized.X + resized.Width, resized.Y + resized.Height), lParam, false);
                handled = true;
                return new IntPtr(1);
            case 0x0232: // WM_EXITSIZEMOVE
                _nativeInteraction = false;
                UpdateRegionDrag(_keyboardMouseEvents.Position, _keyboardMouseEvents.Modifiers);
                // Native Escape restores the starting rectangle. Do not dock a cancelled move.
                var nativeDockTarget = EndRegionDrag(_nativeMoving && Location == _nativeMoveLocation);
                _nativeMoving = false;
                _snap.Reset();
                ClearSnapGuides();
                SaveWindowSizeAndLocation();
                _thumbnailManager?.CompleteThumbnailEdit(Id);
                _ = CommitRegionDockAsync(nativeDockTarget);
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
        bool selecting = args.Button == PointerButtons.Right && (modifierKeys & ShortcutKeys.Shift) != 0
            && NativeMenuTheme.CurrentTheme != "Legacy";
        // Native nonactivating clicks bypass Avalonia's normal light-dismiss
        // input route. A click on the owner dismisses; another preview may act
        // or open its own menu immediately. Clicks on popup actions use their
        // existing menu handlers, including the quick second right-click.
        if (_openMenuOwner != null)
        {
            bool dismissOnly = _openMenuOwner == this;
            _openMenuOwner.DismissContextMenu();
            if (dismissOnly && !selecting)
            {
                if (args.Button == PointerButtons.Left) _thumbnailManager?.ClearThumbnailSelection();
                return;
            }
        }
        if (selecting)
        {
            _thumbnailManager?.ToggleThumbnailSelection(Id);
            if (!IsSelected) return;
        }
        else if (args.Button == PointerButtons.Left || args.Button == PointerButtons.Right && !IsSelected)
            _thumbnailManager?.ClearThumbnailSelection();
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
                _menuOpeningReleasePending = (args.Buttons & PointerButtons.Right) != 0;
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
                    if (!selecting) holdRightClickToMoveTimer.Start();
                }
                finally { _menuOpening = false; }
                break;
        }
    }

    private void PrepareContextMenu()
    {
        bool legacy = NativeMenuTheme.CurrentTheme == "Legacy";
        if (legacy && IsSelected) _thumbnailManager?.ClearThumbnailSelection();
        if (IsSelected && !legacy)
        {
            PrepareSelectionMenu();
            return;
        }
        if (thumbnailContextMenu.Items.Contains(_selectionMoveItem))
        {
            thumbnailContextMenu.Items.Clear();
            foreach (var item in _singleMenuItems) thumbnailContextMenu.Items.Add(item);
        }
        thumbnailContextMenu.Items.Remove(legacy ? _resizeMenu : _legacyResizeItem);
        var resize = legacy ? _legacyResizeItem : _resizeMenu;
        if (!thumbnailContextMenu.Items.Contains(resize)) thumbnailContextMenu.Items.Add(resize);
        if (legacy) thumbnailContextMenu.Items.Remove(_resizeAllItem);
        else if (!thumbnailContextMenu.Items.Contains(_resizeAllItem)) thumbnailContextMenu.Items.Add(_resizeAllItem);
        _skipItem.Header = _config.IsClientCycleSkipped(Title) ? "Resume cycling this character" : "Skip while cycling";
        _skipItem.IsEnabled = !string.IsNullOrWhiteSpace(Title);
        bool docked = _config.GetThumbnailRegion(Title) != null;
        foreach (var item in thumbnailContextMenu.Items.OfType<MenuItem>())
            if (item.Name is "menuReposition" or "menuResizeAll" or "resizeThumbnailToolStripMenuItem") item.IsEnabled = !docked;
        _aspectLockItem.IsChecked = AspectLocked;
        _resetAspectItem.IsEnabled = TryGetClientRatio(out _);
        thumbnailContextMenu.Items.Remove(_undockItem);
        NativeMenuTheme.ApplyThumbnailOrder(thumbnailContextMenu);
        if (docked && !legacy) thumbnailContextMenu.Items.Add(_undockItem);
        if (!legacy && _thumbnailManager?.CanUndoThumbnailEdit == true) thumbnailContextMenu.Items.Add(_undoItem);
    }

    private ThumbnailView[] SelectedThumbnails() => _thumbnailManager.GetAllKnownClients().Values
        .OfType<ThumbnailView>().Where(view => view.IsSelected && view.IsActive && !view.IsDisposed
            && !_config.IsThumbnailDisabled(view.Title)).ToArray();

    private void PrepareSelectionMenu()
    {
        var selected = SelectedThumbnails();
        _selectionResetAspectItem.IsEnabled = selected.Length > 0 && selected.All(view => view.TryGetClientRatio(out _));
        bool skipped = selected.Length > 0 && _config.IsClientCycleSkipped(selected[0].Title);
        _selectionSkipState = selected.Length > 0 && selected.All(view => !string.IsNullOrWhiteSpace(view.Title)
            && _config.IsClientCycleSkipped(view.Title) == skipped) ? skipped : null;
        _selectionSkipItem.Header = skipped ? "Resume Cycling" : "Skip Cycling";
        Control[] items = _selectionSkipState.HasValue
            ? [_selectionMoveItem, _selectionResizeItem, _selectionResetAspectItem, _selectionResetSizeItem, _selectionSkipItem]
            : [_selectionMoveItem, _selectionResizeItem, _selectionResetAspectItem, _selectionResetSizeItem];
        if (_thumbnailManager.CanUndoThumbnailEdit) items = [.. items, _undoItem];
        if (thumbnailContextMenu.Items.SequenceEqual(items)) return;
        thumbnailContextMenu.Items.Clear();
        foreach (var item in items) thumbnailContextMenu.Items.Add(item);
    }

    private void ResetSelectionAspectRatio()
    {
        var selected = SelectedThumbnails();
        PreserveSelectionForMenuClick(selected);
        var ratios = new double[selected.Length];
        // If one source is unavailable, leave the whole selection unchanged.
        for (int i = 0; i < selected.Length; i++)
            if (!selected[i].TryGetClientRatio(out ratios[i])) return;
        RunThumbnailEdit(() =>
        {
            for (int i = 0; i < selected.Length; i++) selected[i].ApplyClientAspectRatio(ratios[i]);
        }, selected: true);
    }

    private void RunThumbnailEdit(Action edit, bool selected = false)
    {
        _thumbnailManager?.BeginThumbnailEdit(Id, ThumbnailEditKind.Geometry, selected);
        try { edit(); }
        finally { _thumbnailManager?.CompleteThumbnailEdit(Id); }
    }

    private async Task ToggleClientCycleSkipped()
    {
        _thumbnailManager?.BeginThumbnailEdit(Id, ThumbnailEditKind.CycleSkip);
        try { await _mediator.Send(new SetClientCycleSkipped(Title, !_config.IsClientCycleSkipped(Title))); }
        finally { _thumbnailManager?.CompleteThumbnailEdit(Id); }
    }

    private async Task ToggleSelectionCycleSkipped()
    {
        var selected = SelectedThumbnails();
        PreserveSelectionForMenuClick(selected);
        if (_selectionSkipState is not bool skipped || selected.Length == 0
            || selected.Any(view => string.IsNullOrWhiteSpace(view.Title) || _config.IsClientCycleSkipped(view.Title) != skipped)) return;
        _updatingSelectionSkip = true;
        _thumbnailManager.BeginThumbnailEdit(Id, ThumbnailEditKind.CycleSkip, selected: true);
        try
        {
            foreach (string title in selected.Select(view => view.Title).Distinct())
                await _mediator.Send(new SetClientCycleSkipped(title, !skipped));
        }
        finally
        {
            _thumbnailManager.CompleteThumbnailEdit(Id);
            _updatingSelectionSkip = false;
            if (IsContextMenuOpen) PrepareSelectionMenu();
        }
    }

    private static void PreserveSelectionForMenuClick(ThumbnailView[] selected)
    {
        // Discrete commands can close the popup before the global release arrives,
        // and the menu's lower rows may lie outside every selected thumbnail.
        foreach (var view in selected) view._selectionMenuClick = true;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var view in selected) view._selectionMenuClick = false;
        }, DispatcherPriority.Background);
    }

    private void DismissContextMenu()
    {
        _menuHoverExitPending = true;
        thumbnailContextMenu.Close();
    }

    private void SelectThroughContextMenu(object sender, PointerPressedEventArgs args)
    {
        if (NativeMenuTheme.CurrentTheme == "Legacy" || (_keyboardMouseEvents.Modifiers & ShortcutKeys.Shift) == 0
            || args.GetCurrentPoint(thumbnailContextMenu).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed) return;
        // The popup can cover the next preview in a row. Shift+right-click is
        // selection even over a menu item, never that item's quick-right-click action.
        args.Handled = true;
        var screen = thumbnailContextMenu.PointToScreen(args.GetPosition(thumbnailContextMenu));
        var point = new Point(screen.X, screen.Y);
        var candidates = _thumbnailManager.GetAllKnownClients().Values.OfType<ThumbnailView>()
            .Where(view => view.IsActive && !view.IsDisposed && !_config.IsThumbnailDisabled(view.Title)
                && view.Bounds.Contains(point)).ToDictionary(view => view.Handle);
        if (candidates.Count == 0) return;
        ThumbnailView target = null;
        // Resolve overlapping previews in native z-order, ignoring the popup and
        // click-through overlays rather than relying on title or discovery order.
        for (IntPtr window = User32NativeMethods.GetWindow(Handle, 0); window != IntPtr.Zero;
             window = User32NativeMethods.GetWindow(window, 2))
        {
            if (candidates.TryGetValue(window, out target)) break;
        }
        if (target == null) return;
        _pendingMenuSelectionOwner?.CancelPendingMenuSelection();
        _pendingMenuSelectionOwner = this;
        _pendingSelectionTarget = target;
        _pendingSelectionPoint = point;
        _keyboardMouseEvents.MouseUp += CompleteMenuSelection;
        // Keep the native popup alive through button-up. Closing it on press
        // strands Avalonia's right-button state and loses the next selection click.
    }

    private void CompleteMenuSelection(object sender, GlobalPointerEventArgs args)
    {
        if (args.Button != PointerButtons.Right) return;
        var target = _pendingSelectionTarget;
        var screen = _pendingSelectionPoint;
        CancelPendingMenuSelection();
        int generation = _menuSelectionGeneration;
        // Finish dispatching the old popup's release/dismissal before opening the
        // new owner's popup. Its first action must not receive the opening click.
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || generation != _menuSelectionGeneration || target == null || target.IsDisposed
                || !target.IsActive || NativeMenuTheme.CurrentTheme == "Legacy") return;
            var local = target.PointToClient(screen);
            DismissContextMenu();
            target.MouseDownEventHandler(new(PointerButtons.Right, PointerButtons.None, local, ShortcutKeys.Shift), ShortcutKeys.Shift);
        }, DispatcherPriority.Background);
    }

    private void CancelPendingMenuSelection()
    {
        _menuSelectionGeneration++;
        if (_pendingSelectionTarget == null) return;
        _keyboardMouseEvents.MouseUp -= CompleteMenuSelection;
        _pendingSelectionTarget = null;
        if (_pendingMenuSelectionOwner == this) _pendingMenuSelectionOwner = null;
    }

    private void DismissMenuOnOutsideClick(object sender, GlobalPointerEventArgs args)
    {
        holdRightClickToMoveTimer.Stop();
        // Movement between press and release is still part of the opening click,
        // not a new outside click. The handoff path opens after release instead.
        if (args.Button == PointerButtons.Right && _menuOpeningReleasePending)
        {
            _menuOpeningReleasePending = false;
            return;
        }
        if (_pendingSelectionTarget != null) return;
        if (!thumbnailContextMenu.IsOpen || PopupContainsPoint(thumbnailContextMenu, args.Location)
            || SubmenusContainPoint(thumbnailContextMenu.Items.OfType<MenuItem>(), args.Location)) return;
        DismissContextMenu();
    }

    private static bool PopupContainsPoint(Control control, Point screen)
    {
        var popup = TopLevel.GetTopLevel(control);
        return popup != null && popup.IsVisible
            && new Rect(popup.Bounds.Size).Contains(popup.PointToClient(new PixelPoint(screen.X, screen.Y)));
    }

    private static bool SubmenusContainPoint(IEnumerable<MenuItem> items, Point screen) =>
        items.Where(item => item.IsSubMenuOpen).Any(item =>
            item.Items.OfType<MenuItem>().Any(child => PopupContainsPoint(child, screen))
            || SubmenusContainPoint(item.Items.OfType<MenuItem>(), screen));

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
            if (item.ToggleType == MenuItemToggleType.CheckBox) item.IsChecked = !item.IsChecked;
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
        if (_config.GetThumbnailRegion(Title) != null
            && !(mode == MouseMode.Move && !snapCursorPosition && NativeMenuTheme.CurrentTheme != "Legacy"
                && _config.EnableRegionDragDocking)) return;
        if (_disposed || _regionDockPending) return;
        ExitCustomMouseMode();
        ZoomOut();
        if (snapCursorPosition)
        {
            var client = ClientSize;
            var target = mode == MouseMode.Move ? new Point(client.Width / 2, client.Height / 2)
                : new Point(Math.Max(0, client.Width - 5), Math.Max(0, client.Height - 5));
            _keyboardMouseEvents.Position = PointToScreen(target);
        }
        IsResizingAll = false;
        _customMouseModeActive = mode;
        _baseMousePosition = _dragPointerOrigin = _keyboardMouseEvents.Position;
        _dragWindowOrigin = Location;
        _dragClientSize = ClientSize;
        _thumbnailRatioAtStartOfResize = _dragClientSize.Height > 0 ? (double)_dragClientSize.Width / _dragClientSize.Height : 1;
        _thumbnailManager?.BeginThumbnailEdit(Id, ThumbnailEditKind.Geometry);
        _snap.Reset();
        if (mode == MouseMode.Move) BeginRegionDrag(allowUndock: !snapCursorPosition);
        _keyboardMouseEvents.MouseMove += ProcessCustomMouseMode;
        _keyboardMouseEvents.MouseUp += ExitCustomMouseMode;
    }

    private void BeginSelectionTransform(MouseMode mode, bool snapCursorPosition = true)
    {
        // Establish the owner's pointer baseline before marking every member busy.
        EnterCustomMouseMode(mode, snapCursorPosition);
        if (_thumbnailManager.BeginSelectionTransform(Id, mode == MouseMode.Resize))
        {
            _resizingSelection = mode == MouseMode.Resize;
            _thumbnailManager.BeginThumbnailEdit(Id, ThumbnailEditKind.Geometry, selected: true);
        }
        else ExitCustomMouseMode();
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
                UpdateRegionDrag(current, modifiers);
                var size = Size;
                var snapped = Snap(new(_dragWindowOrigin.X + dx, _dragWindowOrigin.Y + dy, size.Width, size.Height), shift);
                Location = new(snapped.X, snapped.Y);
                _baseZoomLocation = Location;
                if (_config.GetThumbnailRegion(Title) == null) ThumbnailMoved?.Invoke(Id);
                break;
            case MouseMode.Resize:
                var frame = Size - ClientSize;
                var resized = ResizeBounds(new(_dragWindowOrigin.X, _dragWindowOrigin.Y,
                    _dragClientSize.Width + dx + frame.Width, _dragClientSize.Height + dy + frame.Height), false, true, false, true, shift);
                ClientSize = new(resized.Width - frame.Width, resized.Height - frame.Height);
                ThumbnailResized?.Invoke(Id);
                if (ClientSize != new Size(resized.Width - frame.Width, resized.Height - frame.Height)) ClearSnapGuides();
                break;
        }
        RefreshAppearance();
    }
    private void ExitCustomMouseMode()
    {
        EndRegionDrag(false);
        bool wasInteracting = _customMouseModeActive != MouseMode.Disabled;
        if (_customMouseModeActive != MouseMode.Disabled)
        {
            _keyboardMouseEvents.MouseMove -= ProcessCustomMouseMode;
            _keyboardMouseEvents.MouseUp -= ExitCustomMouseMode;
            _customMouseModeActive = MouseMode.Disabled;
            IsResizingAll = false;
            _resizingSelection = false;
            _thumbnailManager?.EndSelectionTransform(Id);
            _thumbnailManager?.CompleteThumbnailEdit(Id);
            SaveWindowSizeAndLocation();
        }
        _snap.Reset();
        ClearSnapGuides();
        if (wasInteracting) ReleaseHoverOutside();
    }
    private void ExitCustomMouseMode(object sender, GlobalPointerEventArgs args)
    {
        UpdateRegionDrag(args.Location, args.Modifiers);
        var candidate = EndRegionDrag(true);
        ExitCustomMouseMode();
        _ = CommitRegionDockAsync(candidate);
    }
    private void holdRightClickToMoveTimer_Tick(object sender, EventArgs args)
    {
        holdRightClickToMoveTimer.Stop();
        if (_keyboardMouseEvents.Buttons != PointerButtons.Right) return;
        // Closing the popup must not perform deferred hover restoration halfway
        // through the transition to a global drag.
        _menuHoverExitPending = false;
        thumbnailContextMenu.Close();
        if (IsSelected) BeginSelectionTransform(MouseMode.Move, false);
        else EnterCustomMouseMode(MouseMode.Move, false);
        _dragPointerOrigin = _rightClickStartPosition;
        ProcessCustomMouseMode(_keyboardMouseEvents.Position, _keyboardMouseEvents.Modifiers);
    }

    private void CollectSnapTargets(bool shift)
    {
        _snapTargets.Clear();
        if (_config.EnableThumbnailSnap && !shift)
            DesktopSnapTargets.Collect(_snapTargets, this, _config, _thumbnailManager, Id, _groupInteraction);
    }

    private PreviewRect Snap(PreviewRect raw, bool shift)
    {
        CollectSnapTargets(shift);
        var result = _snap.Move(raw, _snapTargets, (int)Math.Round(8 * RenderScaling), (int)Math.Round(16 * RenderScaling),
            shift || !_config.EnableThumbnailSnap);
        ShowSnapGuides(result);
        return result.Bounds;
    }

    private void ShowSnapGuides(ThumbnailSnapResult result)
    {
        if (result.VerticalGuide != null || result.HorizontalGuide != null)
        {
            _snapGuides ??= new ThumbnailSnapGuideWindow();
            _snapGuides.UpdateGuides(result.VerticalGuide, result.HorizontalGuide, this);
        }
        else ClearSnapGuides();
    }
    private void BeginResizeAll()
    {
        if (_config.GetThumbnailRegion(Title) != null) return;
        EnterCustomMouseMode(MouseMode.Resize);
        IsResizingAll = true;
        _thumbnailManager.BeginResizeAll(Id);
        _thumbnailManager.BeginThumbnailEdit(Id, ThumbnailEditKind.ResizeAll);
    }

    private bool TryGetClientRatio(out double ratio)
    {
        ratio = 1;
        if (User32NativeMethods.IsIconic(Id) || !User32NativeMethods.GetClientRect(Id, out var rect)
            || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return false;
        ratio = (double)(rect.Right - rect.Left) / (rect.Bottom - rect.Top);
        return true;
    }

    private void ResetAspectRatio()
    {
        if (!TryGetClientRatio(out double ratio)) return;
        RunThumbnailEdit(() => ApplyClientAspectRatio(ratio));
    }

    private void ApplyClientAspectRatio(double ratio)
    {
        if (_config.GetThumbnailRegion(Title) != null) return;
        ZoomOut();
        ClientSize = RatioSize(ClientSize.Width, ratio);
        ThumbnailResized?.Invoke(Id);
        SaveWindowSizeAndLocation();
        RefreshAppearance();
    }

    private void ResetDefaultSize()
    {
        if (_config.GetThumbnailRegion(Title) != null) return;
        ZoomOut();
        ClientSize = _config.ThumbnailSize;
        _config.PerClientThumbnailSizes.Remove(Title);
        SaveWindowSizeAndLocation();
        RefreshAppearance();
    }

    private Size RatioSize(int width, double ratio)
    {
        double lower = Math.Max(_minimumSize.Width, _minimumSize.Height * ratio);
        double upper = Math.Min(_maximumSize.Width > 0 ? _maximumSize.Width : int.MaxValue,
            _maximumSize.Height > 0 ? _maximumSize.Height * ratio : int.MaxValue);
        // An impossible ratio within both limits uses the closest permitted rectangle.
        double fitted = lower <= upper ? Math.Clamp(width, lower, upper) : upper;
        return ClampSize(new((int)Math.Round(fitted), (int)Math.Round(fitted / ratio)));
    }

    private PreviewRect ResizeBounds(PreviewRect raw, bool left, bool right, bool top, bool bottom, bool shift)
    {
        var frame = Size - ClientSize;
        bool locked = IsResizingAll || _resizingSelection || AspectLocked || shift;
        Size Constrain(int width, int height, bool useHeight) => locked
            ? RatioSize(useHeight ? (int)Math.Round(height * _thumbnailRatioAtStartOfResize) : width, _thumbnailRatioAtStartOfResize)
            : ClampSize(new(width, height));
        PreviewRect Fit(Size client) => new(
            left ? raw.X + raw.Width - client.Width - frame.Width : raw.X,
            top ? raw.Y + raw.Height - client.Height - frame.Height : raw.Y,
            client.Width + frame.Width, client.Height + frame.Height);
        bool heightDriven = !left && !right || locked && !shift && (top || bottom)
            && Math.Abs(raw.Height - frame.Height - _dragClientSize.Height)
                > Math.Abs(raw.Width - frame.Width - _dragClientSize.Width) / _thumbnailRatioAtStartOfResize;
        var size = Constrain(raw.Width - frame.Width, raw.Height - frame.Height, heightDriven);
        var bounded = Fit(size);
        CollectSnapTargets(shift);
        var result = _snap.Resize(bounded, _snapTargets, (int)Math.Round(8 * RenderScaling), (int)Math.Round(16 * RenderScaling),
            // During a group resize, neighbours' left/top edges stay fixed while their sizes change.
            left, right, top, bottom, shift || !_config.EnableThumbnailSnap, leadingTargetsOnly: IsResizingAll);
        bool useHeight = result.HorizontalGuide != null && (result.VerticalGuide == null
            || Math.Abs(result.Bounds.Height - bounded.Height) < Math.Abs(result.Bounds.Width - bounded.Width));
        size = Constrain(result.Bounds.Width - frame.Width, result.Bounds.Height - frame.Height, useHeight);
        var final = Fit(size);
        ShowSnapGuides(result.WithConstrainedBounds(final, left, top));
        return final;
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
