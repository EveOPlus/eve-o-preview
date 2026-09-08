using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Mediator.Messages;
using EveOPreview.UI;
using MediatR;
using Serilog;
using EveOPreview.View.CustomControl;
using EveOPreview.Services.Interop;

namespace EveOPreview.View;

/// <summary>Windows lifetime/tray host. All workspace content lives in the portable UI project.</summary>
public sealed class WorkspaceForm : Form, IMainFormView, IAsyncSettingsView
{
    private readonly ApplicationContext _context;
    private readonly NotifyIcon _tray;
    private readonly WorkspaceView _workspace;
    private readonly WorkspaceAvaloniaHost _workspaceHost;
    private readonly ApplicationPreferences _preferences;
    private Action _exitApplication;
    private bool _refreshPending;
    private bool _applyingTitleBarTheme;
    private bool? _legacyLayout;
    // Device-independent size: a Legacy round trip can include a monitor/DPI change.
    private SizeF _modernClientSize = new(1180, 800);
    public WindowsWorkspaceBackend Backend { get; }

    public WorkspaceForm(ApplicationContext context, ILogger logger, IMediator mediator,
        IConfigurationStorage storage, IThumbnailConfiguration configuration, IProfileManager profiles,
        ApplicationPreferences preferences, IWorkspacePortraitProvider portraits, IWorkspacePreviewCapture previewCapture = null,
        EveOPreview.Services.Implementation.CharacterIdentityCache characters = null)
    {
        _context = context;
        _preferences = preferences;
        _preferences.Changed += ApplyNativeTheme;
        Text = "EVE-O Preview";
        StartPosition = FormStartPosition.CenterScreen;
        // Initial geometry is already scaled to WinForms' current DPI. Record that
        // baseline so startup and subsequent monitor transitions only scale it once.
        AutoScaleDimensions = new SizeF(DeviceDpi, DeviceDpi);
        AutoScaleMode = AutoScaleMode.Dpi;
        ApplyNativeTheme();
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? SystemIcons.Application;
        Backend = new WindowsWorkspaceBackend(this, this, mediator, storage, configuration, profiles, preferences, logger, portraits, previewCapture, characters);
        _workspace = new WorkspaceView(Backend);
        _workspaceHost = new WorkspaceAvaloniaHost { Dock = DockStyle.Fill, Content = _workspace };
        Controls.Add(_workspaceHost);
        var menu = new ContextMenuStrip();
        NativeMenuTheme.Track(menu);
        menu.Items.Add("Open EVE-O Preview", null, (_, _) => RestoreWorkspace());
        menu.Items.Add("Hide / show previews", null, (_, _) => ToggleHideAllActiveClients?.Invoke());
        menu.Items.Add("Minimize all clients", null, (_, _) => MinimizeAllClients?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ApplicationExitRequested?.Invoke());
        _tray = new NotifyIcon { Icon = Icon, Text = "EVE-O Preview", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => RestoreWorkspace();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) FormMinimized?.Invoke(); };
        FormClosing += (_, e) =>
        {
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
    }

    public new void Show()
    {
        _context.MainForm = this;
        FormActivated?.Invoke();
        RequestRefresh();
        Application.Run(_context);
    }

    private void RestoreWorkspace()
    {
        base.Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ApplyNativeTheme()
    {
        NativeMenuTheme.CurrentTheme = _preferences.Theme;
        NativeMenuTheme.ThumbnailMenuOrder = _preferences.ThumbnailMenuOrder;
        NativeMenuTheme.ThumbnailTheme = _preferences.ThumbnailMenuTheme;
        ApplyTitleBarTheme();
        bool legacy = _preferences.Theme == "Legacy";
        if (_legacyLayout == legacy) return;
        if (_legacyLayout == false && WindowState == FormWindowState.Normal)
            _modernClientSize = new SizeF(ClientSize.Width * 96f / DeviceDpi, ClientSize.Height * 96f / DeviceDpi);
        WindowState = FormWindowState.Normal;
        _legacyLayout = legacy;
        UpdateMinimumSize();
        MaximizeBox = !legacy;
        MinimizeBox = !legacy;
        ClientSize = ScaleWorkspaceSize(legacy ? new SizeF(460, 417) : _modernClientSize);
    }

    private Size ScaleWorkspaceSize(SizeF logicalSize) => new(
        (int)Math.Round(logicalSize.Width * DeviceDpi / 96.0),
        (int)Math.Round(logicalSize.Height * DeviceDpi / 96.0));

    private void UpdateMinimumSize() => MinimumSize = _legacyLayout == true
        ? SizeFromClientSize(ScaleWorkspaceSize(new SizeF(460, 417)))
        : ScaleWorkspaceSize(new SizeF(800, 620));

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

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBarTheme();
        RequestRefresh();
    }

    private void ApplyTitleBarTheme()
    {
        if (!IsHandleCreated || IsDisposed || _preferences is null || _applyingTitleBarTheme) return;
        var palette = WorkspaceTheme.Get(_preferences.Theme);
        bool useSystemColors = SystemInformation.HighContrast;
        int dark = !useSystemColors && palette.Name == "Dark" ? 1 : 0;
        int caption = useSystemColors ? DwmNativeMethods.DWMWA_COLOR_DEFAULT : ColorTranslator.ToWin32(ColorTranslator.FromHtml(palette.Sidebar));
        int text = useSystemColors ? DwmNativeMethods.DWMWA_COLOR_DEFAULT : ColorTranslator.ToWin32(ColorTranslator.FromHtml(palette.Text));
        // Use the standard Windows caption/buttons and the same portable palette as the workspace.
        // Explicit caption colors also support a Dark app when Windows itself uses Light.
        _applyingTitleBarTheme = true;
        try
        {
            DwmNativeMethods.DwmSetWindowAttribute(Handle, DwmNativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            DwmNativeMethods.DwmSetWindowAttribute(Handle, DwmNativeMethods.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmNativeMethods.DwmSetWindowAttribute(Handle, DwmNativeMethods.DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
        finally { _applyingTitleBarTheme = false; }
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        const int WmSettingChange = 0x001A, WmThemeChanged = 0x031A, WmDpiChanged = 0x02E0;
        if (message.Msg == WmDpiChanged)
        {
            // WinForms has applied the suggested monitor bounds and dock layout.
            UpdateMinimumSize();
            if (_legacyLayout == true) ClientSize = ScaleWorkspaceSize(new SizeF(460, 417));
            _workspaceHost?.SynchronizeDpi(DeviceDpi);
        }
        if (message.Msg is WmSettingChange or WmThemeChanged) ApplyTitleBarTheme();
    }

    private void RequestRefresh()
    {
        if (!IsHandleCreated || IsDisposed || _refreshPending) return;
        _refreshPending = true;
        BeginInvoke(() =>
        {
            _refreshPending = false;
            if (!IsDisposed) Backend?.NotifyChanged();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray?.ContextMenuStrip?.Dispose();
            _tray?.Dispose();
            if (_preferences != null) _preferences.Changed -= ApplyNativeTheme;
            (_workspace as IDisposable)?.Dispose();
            Backend?.Dispose();
        }
        base.Dispose(disposing);
    }

    public void Minimize() => WindowState = FormWindowState.Minimized;
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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MinimizeToTray { get; set { field = value; RequestRefresh(); } } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ThumbnailOpacity { get; set { field = value; RequestRefresh(); } } = 0.5;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EnableClientLayoutTracking { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HideActiveClientThumbnail { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MinimizeInactiveClients { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowThumbnailsAlwaysOnTop { get; set { field = value; RequestRefresh(); } } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HideThumbnailsOnLostFocus { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EnablePerClientThumbnailLayouts { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Size ThumbnailSize { get; set { field = value; RequestRefresh(); } } = new(384, 216);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EnableThumbnailZoom { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ThumbnailZoomFactor { get; set { field = value; RequestRefresh(); } } = 2;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ViewZoomAnchor ThumbnailZoomAnchor { get; set { field = value; RequestRefresh(); } } = ViewZoomAnchor.NW;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowThumbnailOverlays { get; set { field = value; RequestRefresh(); } } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowThumbnailFrames { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EnableActiveClientHighlight { get; set { field = value; RequestRefresh(); } } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ActiveClientHighlightColor { get; set { field = value; RequestRefresh(); } } = Color.Orange;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FontSettings TitleFontSettings { get; set { field = value; RequestRefresh(); } } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FpsLimiterSettings FpsLimiterSettings { get; set { field = value; RequestRefresh(); } } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AudioMuteSettings AudioMuteSettings { get; set { field = value; RequestRefresh(); } } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ToggleHideAllActiveHotkey { get; set { field = value; RequestRefresh(); } } = "";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string MinimizeAllClientsHotkey { get; set { field = value; RequestRefresh(); } } = "";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string LoadedProfileName { get; set { field = value; RequestRefresh(); } } = "Default";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<CycleGroup> CycleGroups { get; set { field = value; RequestRefresh(); } } = [];

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EnableAutomaticCpuAffinity { get; set { field = value; RequestRefresh(); } } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action ApplicationExitRequested { get => RequestApplicationExit; set => _exitApplication = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action FormActivated { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action FormMinimized { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<ViewCloseRequest> FormCloseRequested { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action ApplicationSettingsChanged { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action ThumbnailsSizeChanged { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<string> ThumbnailStateChanged { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action DocumentationLinkActivated { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string> GetClientNameFromInput { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, CaptureNewHotkeyResponse> CaptureNewHotkey { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action FpsLimiterChanged { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action FpsLimiterEnabledChanged { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action AudioSettingsChanged { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action ToggleHideAllActiveClients { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action MinimizeAllClients { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action CloneCurrentProfile { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action DeleteCurrentProfile { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<string> RenameCurrentProfile { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<ProfileLocation> SwitchToProfile { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Task> CommitSettingsAsync { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Task> CommitSizeAsync { get; set; }
}
