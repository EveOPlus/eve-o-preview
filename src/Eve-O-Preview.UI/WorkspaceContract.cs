namespace EveOPreview.UI;

// The UI contract contains no HWND, WinForms, System.Drawing, EVE process or credential types.
// Platform adapters own persistence, validation and native operations.
public interface IWorkspaceBackend
{
    WorkspaceSnapshot Read();
    event Action? Changed;
    Task<CommandResult> ExecuteAsync(WorkspaceCommand command);
}

/// <summary>Optional asynchronous access to public character portraits, independent of ESI login.</summary>
public interface IWorkspacePortraitProvider
{
    Task<byte[]?> GetCharacterPortraitAsync(long characterId);
}

/// <summary>Optional platform capability for an exact preview of the platform's live title rendering.</summary>
public interface IWorkspacePreviewRenderer
{
    WorkspacePreviewImage RenderPreview(WorkspacePreviewRequest request);
    IReadOnlyList<string> FontFamilies { get; }
}

/// <summary>Optional one-shot client capture. Images stay in memory and never require activating a client.</summary>
public interface IWorkspacePreviewCapture
{
    Task<WorkspaceClientStill?> CapturePreviewStillAsync(string preferredTitle);
}

public sealed record WorkspaceClientStill(string Title, byte[] Png);

// Settings include saved values overlaid with unapplied edits. Rendering never persists them.
// Width and height are the configured preview client size; Png is displayed without implicit font scaling.
public sealed record WorkspacePreviewRequest(IReadOnlyDictionary<string, string> Settings,
    string Title = "EVE - Sample Name", bool Active = true, string BackgroundColor = "#101825", bool CycleSkipped = false,
    byte[]? BackgroundPng = null);
public sealed record WorkspacePreviewImage(byte[] Png, int Width, int Height);

public sealed record WorkspaceSnapshot(
    string ProfileName,
    string Version,
    string Theme,
    bool AllPreviewsHidden,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<ClientItem> Clients,
    IReadOnlyList<ProfileItem> Profiles,
    IReadOnlyList<CycleGroupItem> CycleGroups,
    IReadOnlyList<string>? ThumbnailMenuOrder = null,
    string ThumbnailMenuTheme = ThumbnailMenuThemes.FollowApp,
    IReadOnlyList<string>? SavedClientTitles = null,
    IReadOnlyList<ClientPreferenceItem>? ClientPreferences = null);

public sealed record ClientPreferenceItem(string Title, bool Priority, string BorderColor = "");

public sealed record ClientItem(string Title, bool PreviewVisible);
public sealed record ProfileItem(string Id, string Name, bool IsDefault);
public sealed record CycleGroupItem(int Id, string Name, IReadOnlyList<string> Clients,
    IReadOnlyList<string> ForwardHotkeys, IReadOnlyList<string> BackwardHotkeys,
    IReadOnlyList<string>? SkippedClients = null);

// Actions: setting (Target=key, Value=value), theme (Value=Light/Dark/Legacy),
// thumbnail-menu-move (Target=action id, Position=zero-based destination), thumbnail-menu-reset,
// thumbnail-menu-divider-add (Target=action before divider), thumbnail-menu-divider-remove (Target=divider id),
// thumbnail-menu-theme (Value=palette id or app),
// font-picker, color-picker (Target=title/highlight/profile color key): optional platform dialogs,
// toggle-all, minimize-all, client-visible (Target=full title, Value=true/false),
// profile-switch (Target=id), profile-clone, profile-rename (Value=name), profile-delete,
// group-add, group-delete (Target=group id), group-rename (Target=id, Value=name),
// group-client-add/remove/up/down (Target=group id, Value=full title),
// group-client-move (Target=group id, Value=full title, Position=zero-based destination),
// client-cycle-skip (Target=full title, Value=true/false): session-only, across all groups,
// hotkey-capture/clear (Target=ToggleHideAllActiveHotkey/MinimizeAllClientsHotkey
// or group:{id}:forward/backward:{slot}), documentation, discord, exit.
// preview-size-limits (Settings=four size bounds); client-preferences (Target=title,
// Value=border color or empty to inherit, Settings[Priority]=true/false).
public sealed record WorkspaceCommand(string Action, string Target = "", string Value = "", int? Position = null,
    IReadOnlyDictionary<string, string>? Settings = null);
public sealed record CommandResult(bool Success, string Message, bool WasCancelled = false)
{
    public static CommandResult Ok(string message = "Changes applied") => new(true, message);
    public static CommandResult Error(string message) => new(false, message);
    public static CommandResult Cancelled(string message = "Selection cancelled") => new(true, message, true);
}
