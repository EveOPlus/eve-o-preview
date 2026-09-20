using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using EveOPreview.UI;
using EveOPreview.Services.Interop;
using MediatR;
using Serilog;
using Size = System.Drawing.Size;

namespace EveOPreview.View;

/// <summary>Avalonia settings window and tray owner. Native preview rendering remains behind Windows adapters.</summary>
public sealed class WorkspaceWindow : Window, IMainFormView, IAsyncSettingsView, IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly TrayIcon _tray;
    private readonly WorkspaceView _workspace;
    private readonly ApplicationPreferences _preferences;
    private readonly WindowsSessionLifetime _windowsSession;
    private Action _exitApplication;
    private bool _refreshPending, _applyingTitleBarTheme, _started, _disposed, _sessionEnding;
    private bool? _legacyLayout;
    private Avalonia.Size _modernClientSize = new(1180, 800);
    public WindowsWorkspaceBackend Backend { get; }
    public WorkspaceView Workspace => _workspace;
    internal bool IsDisposed => _disposed;
    internal IntPtr SessionMessageWindow => _windowsSession.MessageWindow;

    public WorkspaceWindow(IClassicDesktopStyleApplicationLifetime lifetime, ILogger logger, IMediator mediator,
        IConfigurationStorage storage, IThumbnailConfiguration configuration, IProfileManager profiles,
        ApplicationPreferences preferences, IWorkspacePortraitProvider portraits, IWorkspacePreviewCapture previewCapture = null,
        EveOPreview.Services.Implementation.CharacterIdentityCache characters = null,
        EveOPreview.Services.IThumbnailManager thumbnails = null, IWorkspaceCombatLogs combatLogs = null, IWorkspaceStaticData staticData = null)
    {
        _lifetime = lifetime;
        _preferences = preferences;
        Title = "EVE-O Preview";
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = LoadIcon();
        Backend = new WindowsWorkspaceBackend(this, this, mediator, storage, configuration, profiles, preferences, logger, portraits, previewCapture, characters, thumbnails, combatLogs, staticData);
        _workspace = new WorkspaceView(Backend, combatLogs is null ? null : new[] { CombatLogView.CreateModule() });
        Content = _workspace;
        _windowsSession = new WindowsSessionLifetime(EndWindowsSession, logger);
        var menu = new NativeMenu();
        AddMenu(menu, "Open EVE-O Preview", RestoreWorkspace);
        AddMenu(menu, "Hide / show previews", () => ToggleHideAllActiveClients?.Invoke());
        AddMenu(menu, "Minimize all clients", () => MinimizeAllClients?.Invoke());
        menu.Items.Add(new NativeMenuItemSeparator());
        AddMenu(menu, "Exit", () => ApplicationExitRequested?.Invoke());
        _tray = new TrayIcon { Icon = Icon, ToolTipText = "EVE-O Preview", Menu = menu, IsVisible = true };
        _tray.Clicked += (_, _) => RestoreWorkspace();
        _preferences.Changed += ApplyNativeTheme;
        ApplyNativeTheme();
        Win32Properties.AddWndProcHookCallback(this, WindowMessage);
        Opened += (_, _) =>
        {
            ApplyTitleBarTheme();
            if (!_started) { _started = true; FormActivated?.Invoke(); }
            RequestRefresh();
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized) FormMinimized?.Invoke();
        };
        Closing += (_, e) =>
        {
            if (_sessionEnding || _disposed) return;
            if (Backend.IsBusy) { e.Cancel = true; return; }
            if (!MinimizeToTray && _workspace.HasUnappliedEdits)
            {
                e.Cancel = true;
                _workspace.RequestDiscardDrafts(Close);
                return;
            }
            var request = new ViewCloseRequest();
            FormCloseRequested?.Invoke(request);
            e.Cancel = !request.Allow;
        };
        Closed += (_, _) => Dispose();
    }

    private static WindowIcon LoadIcon()
    {
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? SystemIcons.Application;
        using var stream = new MemoryStream();
        icon.Save(stream); stream.Position = 0;
        return new WindowIcon(stream);
    }
    private static void AddMenu(NativeMenu menu, string title, Action action)
    {
        var item = new NativeMenuItem(title);
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
    public void RestoreWorkspace()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    private void ApplyNativeTheme()
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(ApplyNativeTheme); return; }
        if (_disposed) return;
        EveOPreview.View.CustomControl.NativeMenuTheme.CurrentTheme = _preferences.Theme;
        EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailMenuOrder = _preferences.ThumbnailMenuOrder;
        EveOPreview.View.CustomControl.NativeMenuTheme.ThumbnailTheme = _preferences.ThumbnailMenuTheme;
        RequestedThemeVariant = _preferences.Theme == "Dark" ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        if (Avalonia.Application.Current is { } application) application.RequestedThemeVariant = RequestedThemeVariant;
        ApplyTitleBarTheme();
        bool legacy = _preferences.Theme == "Legacy";
        if (_legacyLayout == legacy) return;
        if (_legacyLayout == false && WindowState == WindowState.Normal) _modernClientSize = ClientSize;
        WindowState = WindowState.Normal;
        _legacyLayout = legacy;
        MinWidth = legacy ? 460 : 800;
        MinHeight = legacy ? 417 : 620;
        CanResize = !legacy;
        CanMaximize = !legacy;
        CanMinimize = !legacy;
        var size = legacy ? new Avalonia.Size(460, 417) : _modernClientSize;
        Width = size.Width; Height = size.Height;
        CorrectNativeClientSize(size);
    }
    private void CorrectNativeClientSize(Avalonia.Size size)
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var outer) || !GetClientRect(handle, out var client)) return;
        int width = (int)Math.Round(size.Width * RenderScaling), height = (int)Math.Round(size.Height * RenderScaling);
        if (client.Right == width && client.Bottom == height) return;
        // Style changes may use different nonclient metrics from the current render
        // scale. Preserve the requested client extent using the actual native frame;
        // this runs only on a theme layout transition, never preview/frame hot paths.
        SetWindowPos(handle, IntPtr.Zero, 0, 0,
            width + outer.Right - outer.Left - client.Right, height + outer.Bottom - outer.Top - client.Bottom,
            0x0002 | 0x0004 | 0x0010);
    }
    private void RequestApplicationExit()
    {
        if (Backend.IsBusy) return;
        if (_workspace.HasUnappliedEdits)
        {
            RestoreWorkspace();
            _workspace.RequestDiscardDrafts(() => _exitApplication?.Invoke());
        }
        else _exitApplication?.Invoke();
    }
    private void ApplyTitleBarTheme()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero || _disposed || _applyingTitleBarTheme) return;
        var palette = WorkspaceTheme.Get(_preferences.Theme);
        var contrast = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        bool systemColors = SystemParametersInfo(0x0042, contrast.Size, ref contrast, 0) && (contrast.Flags & 1) != 0;
        int dark = !systemColors && palette.Name == "Dark" ? 1 : 0;
        int caption = systemColors ? DwmNativeMethods.DWMWA_COLOR_DEFAULT : ColorTranslator.ToWin32(ColorTranslator.FromHtml(palette.Sidebar));
        int text = systemColors ? DwmNativeMethods.DWMWA_COLOR_DEFAULT : ColorTranslator.ToWin32(ColorTranslator.FromHtml(palette.Text));
        _applyingTitleBarTheme = true;
        try
        {
            DwmNativeMethods.DwmSetWindowAttribute(handle, DwmNativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            DwmNativeMethods.DwmSetWindowAttribute(handle, DwmNativeMethods.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmNativeMethods.DwmSetWindowAttribute(handle, DwmNativeMethods.DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
        finally { _applyingTitleBarTheme = false; }
    }
    private IntPtr WindowMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0011) { handled = true; return new IntPtr(1); }
        if (message == 0x0016)
        {
            handled = true;
            if (wParam != IntPtr.Zero) EndWindowsSession();
            return IntPtr.Zero;
        }
        if (message is 0x001A or 0x031A) ApplyTitleBarTheme();
        return IntPtr.Zero;
    }
    private void EndWindowsSession()
    {
        if (_sessionEnding) return;
        _sessionEnding = true;
        WindowsSessionEnding?.Invoke();
        _lifetime.Shutdown();
    }
    private void RequestRefresh()
    {
        if (_disposed || _refreshPending) return;
        _refreshPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshPending = false;
            if (!_disposed) Backend?.NotifyChanged();
        }, DispatcherPriority.Background);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tray?.Dispose();
        _windowsSession?.Dispose();
        _preferences.Changed -= ApplyNativeTheme;
        Win32Properties.RemoveWndProcHookCallback(this, WindowMessage);
        (_workspace as IDisposable)?.Dispose();
        Backend?.Dispose();
        Close();
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast { public uint Size, Flags; public IntPtr Scheme; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRectangle rectangle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref HighContrast data, uint flags);

    public void Minimize() => WindowState = WindowState.Minimized;
    public void SetDocumentationUrl(string url) => Backend.DocumentationUrl = url;
    public void SetVersionInfo(string version) { Backend.Version = version; RequestRefresh(); }
    public void SetThumbnailSizeLimitations(Size minimumSize, Size maximumSize)
    {
        Backend.MinimumThumbnailSize = minimumSize;
        Backend.MaximumThumbnailSize = maximumSize;
    }
    public void AddThumbnails(IList<IThumbnailDescription> thumbnails) { Backend.AddClients(thumbnails); RequestRefresh(); }
    public void RemoveThumbnails(IList<IThumbnailDescription> thumbnails) { Backend.RemoveClients(thumbnails); RequestRefresh(); }
    public void RefreshZoomSettings() => RequestRefresh();
    public void UpdateThumbnailToggleHideAllStatus(bool notificationIsHidden) => RequestRefresh();
    public void UpdateProfileList(List<ProfileLocation> profiles) => RequestRefresh();

    public bool MinimizeToTray { get; set { field = value; RequestRefresh(); } } = true;

    public double ThumbnailOpacity { get; set { field = value; RequestRefresh(); } } = 0.5;

    public bool EnableClientLayoutTracking { get; set { field = value; RequestRefresh(); } } = false;

    public bool HideActiveClientThumbnail { get; set { field = value; RequestRefresh(); } } = false;

    public bool MinimizeInactiveClients { get; set { field = value; RequestRefresh(); } } = false;

    public bool ShowThumbnailsAlwaysOnTop { get; set { field = value; RequestRefresh(); } } = true;

    public bool HideThumbnailsOnLostFocus { get; set { field = value; RequestRefresh(); } } = false;

    public bool EnablePerClientThumbnailLayouts { get; set { field = value; RequestRefresh(); } } = false;

    public Size ThumbnailSize { get; set { field = value; RequestRefresh(); } } = new(384, 216);

    public bool EnableThumbnailZoom { get; set { field = value; RequestRefresh(); } } = false;

    public int ThumbnailZoomFactor { get; set { field = value; RequestRefresh(); } } = 2;

    public ViewZoomAnchor ThumbnailZoomAnchor { get; set { field = value; RequestRefresh(); } } = ViewZoomAnchor.NW;

    public bool ShowThumbnailOverlays { get; set { field = value; RequestRefresh(); } } = true;

    public bool ShowThumbnailFrames { get; set { field = value; RequestRefresh(); } } = false;

    public bool EnableActiveClientHighlight { get; set { field = value; RequestRefresh(); } } = false;

    public Color ActiveClientHighlightColor { get; set { field = value; RequestRefresh(); } } = Color.Orange;

    public FontSettings TitleFontSettings { get; set { field = value; RequestRefresh(); } } = new();

    public FpsLimiterSettings FpsLimiterSettings { get; set { field = value; RequestRefresh(); } } = new();

    public AudioMuteSettings AudioMuteSettings { get; set { field = value; RequestRefresh(); } } = new();

    public string ToggleHideAllActiveHotkey { get; set { field = value; RequestRefresh(); } } = "";

    public string MinimizeAllClientsHotkey { get; set { field = value; RequestRefresh(); } } = "";

    public string LoadedProfileName { get; set { field = value; RequestRefresh(); } } = "Default";

    public List<CycleGroup> CycleGroups { get; set { field = value; RequestRefresh(); } } = [];

    public bool EnableAutomaticCpuAffinity { get; set { field = value; RequestRefresh(); } } = true;

    public Action ApplicationExitRequested { get => RequestApplicationExit; set => _exitApplication = value; }
    public Action WindowsSessionEnding { get; set; }

    public Action FormActivated { get; set; }

    public Action FormMinimized { get; set; }

    public Action<ViewCloseRequest> FormCloseRequested { get; set; }

    public Action ApplicationSettingsChanged { get; set; }

    public Action ThumbnailsSizeChanged { get; set; }

    public Action<string> ThumbnailStateChanged { get; set; }

    public Action DocumentationLinkActivated { get; set; }

    public Func<string> GetClientNameFromInput { get; set; }

    public Func<string, CaptureNewHotkeyResponse> CaptureNewHotkey { get; set; }

    public Action FpsLimiterChanged { get; set; }

    public Action FpsLimiterEnabledChanged { get; set; }

    public Action AudioSettingsChanged { get; set; }

    public Action ToggleHideAllActiveClients { get; set; }

    public Action MinimizeAllClients { get; set; }

    public Action CloneCurrentProfile { get; set; }

    public Action DeleteCurrentProfile { get; set; }

    public Action<string> RenameCurrentProfile { get; set; }

    public Action<ProfileLocation> SwitchToProfile { get; set; }

    public Func<Task> CommitSettingsAsync { get; set; }

    public Func<Task> CommitSizeAsync { get; set; }
}
